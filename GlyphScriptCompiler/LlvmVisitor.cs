using GlyphScriptCompiler.Contracts;
using GlyphScriptCompiler.SyntaxErrors;
using GlyphScriptCompiler.TypeOperations;

namespace GlyphScriptCompiler;

public sealed class LlvmVisitor : GlyphScriptBaseVisitor<object?>, IDisposable
{
    public LLVMModuleRef LlvmModule { get; }
    private readonly LLVMBuilderRef _llvmBuilder = LLVM.CreateBuilder();
    private readonly ExpressionResultTypeEngine _expressionResultTypeEngine = new();

    private readonly Dictionary<string, GlyphScriptValue> _variables = [];
    private readonly Dictionary<OperationSignature, OperationImplementation> _availableOperations = new();

    public LlvmVisitor(LLVMModuleRef llvmModule)
    {
        LlvmModule = llvmModule;

        IOperationProvider[] initialOperationProviders =
        [
            new IntegerOperations(llvmModule, _llvmBuilder),
            new LongOperations(llvmModule, _llvmBuilder),
            new FloatOperations(llvmModule, _llvmBuilder),
            new DoubleOperations(llvmModule, _llvmBuilder),
            new StringOperations(llvmModule, _llvmBuilder),
            new BoolOperations(llvmModule, _llvmBuilder),
            new ArrayOperations(llvmModule, _llvmBuilder)
        ];

        foreach (var provider in initialOperationProviders)
        {
            provider.Initialize();
            RegisterOperations(provider);
        }
    }

    private void RegisterOperations(IOperationProvider provider)
    {
        foreach (var (operationSignature, implementation) in provider.Operations)
            _availableOperations.Add(operationSignature, implementation);
    }

    public override object? VisitProgram(GlyphScriptParser.ProgramContext context)
    {
        SetupGlobalFunctions(LlvmModule);
        LlvmHelper.CreateMain(LlvmModule, _llvmBuilder);

        var result = VisitChildren(context);
        LLVM.BuildRet(_llvmBuilder, LLVM.ConstInt(LLVM.Int32Type(), 0, false));

        return result;
    }

    public override object? VisitDeclaration(GlyphScriptParser.DeclarationContext context)
    {
        return VisitChildren(context);
    }

    public override object? VisitDefaultDeclaration(GlyphScriptParser.DefaultDeclarationContext context)
    {
        const OperationKind operationKind = OperationKind.DefaultValue;

        var type = GetTypeFromContext(context.type());
        var id = context.ID().GetText();

        // Get array type information if this is an array
        ArrayTypeInfo? arrayInfo = null;
        if (type == GlyphScriptType.Array)
        {
            arrayInfo = Visit(context.type().arrayOfType()) as ArrayTypeInfo;
        }

        var operationSignature = new OperationSignature(operationKind, [type]);
        var createDefaultValueOperation = _availableOperations.GetValueOrDefault(operationSignature);
        if (createDefaultValueOperation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        var value = createDefaultValueOperation(context, [])
            ?? throw new InvalidOperationException($"Failed to create default value for type {type}");

        if (_variables.ContainsKey(id))
            throw new InvalidOperationException($"Variable '{id}' is already defined.");

        // Pass array info to GetLlvmType
        var llvmType = GetLlvmType(type, arrayInfo);
        var variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);

        LLVM.BuildStore(_llvmBuilder, value.Value, variable);

        // Create GlyphScriptValue with array information if applicable
        GlyphScriptValue result;
        if (type == GlyphScriptType.Array && arrayInfo != null)
        {
            result = new GlyphScriptValue(variable, type, arrayInfo);
        }
        else
        {
            result = new GlyphScriptValue(variable, type);
        }

        _variables[id] = result;
        return result;
    }

    public override object? VisitInitializingDeclaration(GlyphScriptParser.InitializingDeclarationContext context)
    {
        var type = GetTypeFromContext(context.type());
        var id = context.ID().GetText();

        // Get array type information if this is an array
        ArrayTypeInfo? arrayInfo = null;
        if (type == GlyphScriptType.Array)
        {
            arrayInfo = Visit(context.type().arrayOfType()) as ArrayTypeInfo;
        }

        var expressionValue = Visit(context.expression()) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to create expression");

        if (_variables.ContainsKey(id))
            throw new DuplicateVariableDeclarationException(context) { VariableName = id };

        if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(type, expressionValue.Type))
        {
            throw new AssignmentOfInvalidTypeException(context)
            {
                VariableName = id,
                VariableGlyphScriptType = type,
                ValueGlyphScriptType = expressionValue.Type
            };
        }

        // Pass array info to GetLlvmType
        var llvmType = GetLlvmType(type, arrayInfo);
        var variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);

        LLVM.BuildStore(_llvmBuilder, expressionValue.Value, variable);

        // Create GlyphScriptValue with array information if applicable
        GlyphScriptValue result;
        if (type == GlyphScriptType.Array && arrayInfo != null)
        {
            result = new GlyphScriptValue(variable, type, arrayInfo);
        }
        else
        {
            result = new GlyphScriptValue(variable, type);
        }

        _variables[id] = result;
        return result;
    }

    public override object? VisitAssignment(GlyphScriptParser.AssignmentContext context)
    {
        // Handle array element assignment
        if (context.expression().Length > 1)
        {
            var id = context.ID().GetText();
            var indexValue = Visit(context.expression(0)) as GlyphScriptValue ??
                throw new InvalidOperationException("Failed to resolve index expression");
            var rhsValue = Visit(context.expression(1)) as GlyphScriptValue ??
                throw new InvalidOperationException("Failed to create expression value");

            if (!_variables.TryGetValue(id, out var arrayVariable))
                throw new UndefinedVariableUsageException(context) { VariableName = id };

            if (arrayVariable.Type != GlyphScriptType.Array || arrayVariable.ArrayInfo == null)
                throw new InvalidSyntaxException(context, $"Variable '{id}' is not an array");

            // Ensure index is an integer type
            if (indexValue.Type != GlyphScriptType.Int && indexValue.Type != GlyphScriptType.Long)
                throw new InvalidSyntaxException(context,
                    $"Array indices must be integer types. Found: {indexValue.Type}");

            // Ensure the RHS value type matches the array element type
            if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(arrayVariable.ArrayInfo.ElementType, rhsValue.Type))
            {
                throw new AssignmentOfInvalidTypeException(context)
                {
                    VariableName = $"{id}[index]",
                    VariableGlyphScriptType = arrayVariable.ArrayInfo.ElementType,
                    ValueGlyphScriptType = rhsValue.Type
                };
            }

            // Load the array value from the variable first - this is key for assignment to work
            var loadedArrayValue = LLVM.BuildLoad(_llvmBuilder, arrayVariable.Value, $"{id}Value");
            var arrayValue = new GlyphScriptValue(loadedArrayValue, arrayVariable.Type, arrayVariable.ArrayInfo);

            const OperationKind operationKind = OperationKind.ArrayElementAssignment;
            var operationSignature = new OperationSignature(
                operationKind,
                [arrayVariable.Type, indexValue.Type, rhsValue.Type, arrayVariable.ArrayInfo.ElementType]
            );

            var arrayAssignmentOperation = _availableOperations.GetValueOrDefault(operationSignature);
            if (arrayAssignmentOperation is null)
                throw new OperationNotAvailableException(context, operationSignature);

            return arrayAssignmentOperation(context, [arrayValue, indexValue, rhsValue]);
        }
        else
        {
            // Normal variable assignment
            var id = context.ID().GetText();
            var expressionValue = Visit(context.expression(0)) as GlyphScriptValue ??
                throw new InvalidOperationException("Failed to create expression");

            if (!_variables.TryGetValue(id, out var variable))
                throw new UndefinedVariableUsageException(context) { VariableName = id };

            if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(variable.Type, expressionValue.Type))
            {
                throw new AssignmentOfInvalidTypeException(context)
                {
                    VariableName = id,
                    VariableGlyphScriptType = variable.Type,
                    ValueGlyphScriptType = expressionValue.Type
                };
            }

            LLVM.BuildStore(_llvmBuilder, expressionValue.Value, variable.Value);
            return expressionValue;
        }
    }

    public override object? VisitPrint(GlyphScriptParser.PrintContext context)
    {
        const OperationKind operationKind = OperationKind.Print;

        var expressionValue = Visit(context.expression()) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to create expression");

        var operationSignature = new OperationSignature(operationKind, [expressionValue.Type]);

        var printOperation = _availableOperations.GetValueOrDefault(operationSignature);
        if (printOperation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return printOperation(context, [expressionValue]);
    }

    public override object? VisitRead(GlyphScriptParser.ReadContext context)
    {
        const OperationKind operationKind = OperationKind.Read;

        var id = context.ID().GetText();

        if (!_variables.TryGetValue(id, out var variable))
            throw new UndefinedVariableUsageException(context) { VariableName = id };

        var operationSignature = new OperationSignature(operationKind, [variable.Type]);

        var readOperation = _availableOperations.GetValueOrDefault(operationSignature);
        if (readOperation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        var result = readOperation(context, [variable]) as GlyphScriptValue;

        if (result != null)
            LLVM.BuildStore(_llvmBuilder, result.Value, variable.Value);

        return result;
    }

    public override object? VisitImmediateValue(GlyphScriptParser.ImmediateValueContext context)
    {
        if (context.arrayLiteral() != null)
        {
            return Visit(context.arrayLiteral());
        }

        const OperationKind operationKind = OperationKind.ParseImmediate;

        static GlyphScriptType DetectType(GlyphScriptParser.ImmediateValueContext context)
        {
            if (context.INT_LITERAL() != null) return GlyphScriptType.Int;
            if (context.LONG_LITERAL() != null) return GlyphScriptType.Long;
            if (context.FLOAT_LITERAL() != null) return GlyphScriptType.Float;
            if (context.DOUBLE_LITERAL() != null) return GlyphScriptType.Double;
            if (context.STRING_LITERAL() != null) return GlyphScriptType.String;
            if (context.TRUE_LITERAL() != null || context.FALSE_LITERAL() != null) return GlyphScriptType.Boolean;
            throw new InvalidOperationException("Invalid immediate value");
        }

        var type = DetectType(context);
        var operationSignature = new OperationSignature(operationKind, [type]);
        var createImmediateValueOperation = _availableOperations.GetValueOrDefault(operationSignature)
            ?? throw new OperationNotAvailableException(context, operationSignature);

        return createImmediateValueOperation(context, [])
            ?? throw new InvalidOperationException($"Failed to create immediate value for type {type}");
    }

    public override object? VisitParenthesisExp(GlyphScriptParser.ParenthesisExpContext context) =>
        Visit(context.expression());

    public override object? VisitMulDivExp(GlyphScriptParser.MulDivExpContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationKind = context.MULTIPLICATION_SYMBOL() != null
            ? OperationKind.Multiplication
            : OperationKind.Division;
        var operationSignature = new OperationSignature(operationKind, [leftValue.Type, rightValue.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation(context, [leftValue, rightValue]);
    }

    public override object? VisitAddSubExp(GlyphScriptParser.AddSubExpContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationKind = context.ADDITION_SYMBOL() != null
            ? OperationKind.Addition
            : OperationKind.Subtraction;
        var operationSignature = new OperationSignature(operationKind, [leftValue.Type, rightValue.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation(context, [leftValue, rightValue]);
    }

    public override object? VisitPowerExp(GlyphScriptParser.PowerExpContext context)
    {
        const OperationKind operationKind = OperationKind.Power;

        var leftValue = Visit(context.expression(0)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationSignature = new OperationSignature(operationKind, [leftValue.Type, rightValue.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation(context, [leftValue, rightValue]);
    }

    public override object? VisitValueExp(GlyphScriptParser.ValueExpContext context) =>
        Visit(context.immediateValue());

    public override object? VisitIdAtomExp(GlyphScriptParser.IdAtomExpContext context)
    {
        var id = context.ID().GetText();
        if (!_variables.TryGetValue(id, out var variable))
            throw new InvalidOperationException($"Variable '{id}' is not defined.");

        // Maintain array type info when loading array variables
        if (variable.Type == GlyphScriptType.Array && variable.ArrayInfo != null)
        {
            return new GlyphScriptValue(
                LLVM.BuildLoad(_llvmBuilder, variable.Value, id),
                variable.Type,
                variable.ArrayInfo);
        }
        else
        {
            return new GlyphScriptValue(
                LLVM.BuildLoad(_llvmBuilder, variable.Value, id),
                variable.Type);
        }
    }

    public override object? VisitNotExpr(GlyphScriptParser.NotExprContext context)
    {
        const OperationKind operationKind = OperationKind.Not;

        var value = Visit(context.expression()) as GlyphScriptValue
            ?? throw new InvalidOperationException("Unable to resolve the expression");

        var operationSignature = new OperationSignature(operationKind, [value.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation(context, [value]);
    }

    public override object? VisitXorExp(GlyphScriptParser.XorExpContext context)
    {
        const OperationKind operationKind = OperationKind.Xor;

        var leftValue = Visit(context.expression(0)) as GlyphScriptValue
            ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue
            ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationSignature = new OperationSignature(operationKind, [leftValue.Type, rightValue.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation(context, [leftValue, rightValue]);
    }

    public override object? VisitArrayLiteral(GlyphScriptParser.ArrayLiteralContext context)
    {
        const OperationKind operationKind = OperationKind.CreateArray;

        // If this is an empty array, we need to determine the element type from context
        if (context.expressionList() == null)
        {
            // Default to integer array if no element type can be determined
            var defaultElementType = GlyphScriptType.Int;
            var operationSignature = new OperationSignature(operationKind, [defaultElementType]);

            var createEmptyArrayOperation = _availableOperations.GetValueOrDefault(operationSignature);
            if (createEmptyArrayOperation is null)
                throw new OperationNotAvailableException(context, operationSignature);

            return createEmptyArrayOperation(context, []);
        }

        // Get all expressions in the array
        var expressionValues = new List<GlyphScriptValue>();

        // Visit each expression to get its value
        foreach (var expr in context.expressionList().expression())
        {
            var value = Visit(expr) as GlyphScriptValue ??
                throw new InvalidOperationException("Failed to evaluate array element expression");
            expressionValues.Add(value);
        }

        // Ensure all elements have the same type
        var elementType = expressionValues[0].Type;
        for (int i = 1; i < expressionValues.Count; i++)
        {
            if (expressionValues[i].Type != elementType)
                throw new InvalidSyntaxException(context,
                    $"Array elements must be of the same type. Found {elementType} and {expressionValues[i].Type}");
        }

        // Get the operation to create an array of this element type
        var arrayOperationSignature = new OperationSignature(operationKind, [elementType]);
        var createArrayOperation = _availableOperations.GetValueOrDefault(arrayOperationSignature);

        if (createArrayOperation is null)
            throw new OperationNotAvailableException(context, arrayOperationSignature);

        return createArrayOperation(context, expressionValues.ToArray());
    }

    public override object? VisitArrayAccessExp(GlyphScriptParser.ArrayAccessExpContext context)
    {
        const OperationKind operationKind = OperationKind.ArrayAccess;

        // Get the array value
        var arrayValue = Visit(context.expression(0)) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to resolve array expression");

        if (arrayValue.Type != GlyphScriptType.Array || arrayValue.ArrayInfo == null)
            throw new InvalidSyntaxException(context, $"Expression is not an array: {context.expression(0).GetText()}");

        // Get the index value
        var indexValue = Visit(context.expression(1)) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to resolve index expression");

        // Ensure index is an integer type
        if (indexValue.Type != GlyphScriptType.Int && indexValue.Type != GlyphScriptType.Long)
            throw new InvalidSyntaxException(context,
                $"Array indices must be integer types. Found: {indexValue.Type}");

        // Get the operation for array access with this element type
        var accessOperationSignature = new OperationSignature(operationKind,
            [arrayValue.Type, indexValue.Type, arrayValue.ArrayInfo.ElementType]);

        var arrayAccessOperation = _availableOperations.GetValueOrDefault(accessOperationSignature);
        if (arrayAccessOperation is null)
            throw new OperationNotAvailableException(context, accessOperationSignature);

        return arrayAccessOperation(context, [arrayValue, indexValue]);
    }

    public override object? VisitArrayOfType(GlyphScriptParser.ArrayOfTypeContext context)
    {
        // Get the element type
        var elementType = GetTypeFromContext(context.type());
        return new ArrayTypeInfo(elementType);
    }

    private static GlyphScriptType GetTypeFromContext(GlyphScriptParser.TypeContext context)
    {
        if (context.INT() != null) return GlyphScriptType.Int;
        if (context.LONG() != null) return GlyphScriptType.Long;
        if (context.FLOAT() != null) return GlyphScriptType.Float;
        if (context.DOUBLE() != null) return GlyphScriptType.Double;
        if (context.STRING_TYPE() != null) return GlyphScriptType.String;
        if (context.BOOLEAN_TYPE() != null) return GlyphScriptType.Boolean;
        if (context.arrayOfType() != null) return GlyphScriptType.Array;
        throw new InvalidOperationException("Invalid type");
    }

    // Update GetLlvmType to support arrays
    private static LLVMTypeRef GetLlvmType(GlyphScriptType glyphScriptType, ArrayTypeInfo? arrayInfo = null)
    {
        return glyphScriptType switch
        {
            GlyphScriptType.Int => LLVM.Int32Type(),
            GlyphScriptType.Long => LLVM.Int64Type(),
            GlyphScriptType.Float => LLVM.FloatType(),
            GlyphScriptType.Double => LLVM.DoubleType(),
            GlyphScriptType.String => LLVM.PointerType(LLVM.Int8Type(), 0),
            GlyphScriptType.Boolean => LLVM.Int1Type(),
            GlyphScriptType.Array when arrayInfo != null => LLVM.PointerType(LLVM.Int8Type(), 0), // Arrays are pointers to memory
            _ => throw new InvalidOperationException($"Unsupported type: {glyphScriptType}")
        };
    }

    private void SetupGlobalFunctions(LLVMModuleRef module)
    {
        var i8Type = LLVM.Int8Type();
        var i8PtrType = LLVM.PointerType(i8Type, 0);

        LLVMTypeRef[] printfParamTypes = [i8PtrType];
        var printfType = LLVM.FunctionType(LLVM.Int32Type(), printfParamTypes, true);
        LLVM.AddFunction(module, "printf", printfType);

        var scanfType = LLVM.FunctionType(LLVM.Int32Type(), printfParamTypes, true);
        LLVM.AddFunction(module, "scanf", scanfType);

        var mallocType = LLVM.FunctionType(i8PtrType, [LLVM.Int64Type()], false);
        LLVM.AddFunction(module, "malloc", mallocType);
    }

    public void Dispose()
    {
        LLVM.DisposeBuilder(_llvmBuilder);
    }
}
