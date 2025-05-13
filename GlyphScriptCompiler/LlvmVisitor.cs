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

        var llvmType = GetLlvmType(type, arrayInfo);
        var variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);

        LLVM.BuildStore(_llvmBuilder, value.Value, variable);

        var result = type == GlyphScriptType.Array && arrayInfo != null
            ? new GlyphScriptValue(variable, type, arrayInfo)
            : new GlyphScriptValue(variable, type);

        _variables[id] = result;
        return result;
    }

    public override object? VisitInitializingDeclaration(GlyphScriptParser.InitializingDeclarationContext context)
    {
        var type = GetTypeFromContext(context.type());
        var id = context.ID().GetText();

        ArrayTypeInfo? arrayInfo = null;
        if (type == GlyphScriptType.Array)
            arrayInfo = Visit(context.type().arrayOfType()) as ArrayTypeInfo;

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

        var llvmType = GetLlvmType(type, arrayInfo);
        var variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);

        LLVM.BuildStore(_llvmBuilder, expressionValue.Value, variable);

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
            return ArrayAssignmentOperation(context);

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

    private object? ArrayAssignmentOperation(GlyphScriptParser.AssignmentContext context)
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

        if (indexValue.Type != GlyphScriptType.Int && indexValue.Type != GlyphScriptType.Long)
            throw new InvalidSyntaxException(context,
                $"Array indices must be integer types. Found: {indexValue.Type}");

        if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(arrayVariable.ArrayInfo.ElementType, rhsValue.Type))
        {
            throw new AssignmentOfInvalidTypeException(context)
            {
                VariableName = $"{id}[index]",
                VariableGlyphScriptType = arrayVariable.ArrayInfo.ElementType,
                ValueGlyphScriptType = rhsValue.Type
            };
        }

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

    public override object? VisitIfStatement(GlyphScriptParser.IfStatementContext context)
    {
        var conditionValue = Visit(context.expression()) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to evaluate if condition expression");

        if (conditionValue.Type != GlyphScriptType.Boolean)
            throw new InvalidSyntaxException(context, $"Condition in if statement must be a boolean expression. Found: {conditionValue.Type}");

        var currentFunction = LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_llvmBuilder));
        var thenBlock = LLVM.AppendBasicBlock(currentFunction, "if_then");
        LLVMBasicBlockRef? elseBlock = null;
        if (context.ELSE() != null)
        {
            elseBlock = LLVM.AppendBasicBlock(currentFunction, "if_else");
        }
        var mergeBlock = LLVM.AppendBasicBlock(currentFunction, "if_merge");

        if (elseBlock != null)
            LLVM.BuildCondBr(_llvmBuilder, conditionValue.Value, thenBlock, elseBlock.Value);
        else
            LLVM.BuildCondBr(_llvmBuilder, conditionValue.Value, thenBlock, mergeBlock);

        LLVM.PositionBuilderAtEnd(_llvmBuilder, thenBlock);
        Visit(context.block(0));
        LLVM.BuildBr(_llvmBuilder, mergeBlock);

        if (elseBlock != null)
        {
            LLVM.PositionBuilderAtEnd(_llvmBuilder, elseBlock.Value);
            Visit(context.block(1));
            LLVM.BuildBr(_llvmBuilder, mergeBlock);
        }

        LLVM.PositionBuilderAtEnd(_llvmBuilder, mergeBlock);

        return null;
    }

    public override object? VisitBlock(GlyphScriptParser.BlockContext context)
    {
        if (context.BEGIN() != null)
        {
            var statements = context.statement();
            foreach (var statement in statements)
            {
                Visit(statement);
            }
            return null;
        }

        return Visit(context.statement(0));
    }

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

    public override object? VisitParenthesisExp(GlyphScriptParser.ParenthesisExpContext context) =>
        Visit(context.expression());

    public override object? VisitIdAtomExp(GlyphScriptParser.IdAtomExpContext context)
    {
        var id = context.ID().GetText();
        if (!_variables.TryGetValue(id, out var variable))
            throw new InvalidOperationException($"Variable '{id}' is not defined.");

        if (variable.Type == GlyphScriptType.Array && variable.ArrayInfo != null)
        {
            return variable with { Value = LLVM.BuildLoad(_llvmBuilder, variable.Value, id) };
        }

        return new GlyphScriptValue(
            LLVM.BuildLoad(_llvmBuilder, variable.Value, id),
            variable.Type);
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

    public override object? VisitComparisonExpr(GlyphScriptParser.ComparisonExprContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue
            ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue
            ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationKind = OperationKind.Comparison;
        return VisitBinaryExpression(context, leftValue, rightValue, operationKind);
    }

    public override object? VisitLessThanExpr(GlyphScriptParser.LessThanExprContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue
            ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue
            ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationKind = OperationKind.LessThan;
        return VisitBinaryExpression(context, leftValue, rightValue, operationKind);
    }

    public override object? VisitGreaterThanExpr(GlyphScriptParser.GreaterThanExprContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue
            ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue
            ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationKind = OperationKind.GreaterThan;
        return VisitBinaryExpression(context, leftValue, rightValue, operationKind);
    }

    public override object? VisitArrayLiteral(GlyphScriptParser.ArrayLiteralContext context)
    {
        const OperationKind operationKind = OperationKind.CreateArray;

        if (context.expressionList() == null)
        {
            var defaultElementType = GlyphScriptType.Int;
            var operationSignature = new OperationSignature(operationKind, [defaultElementType]);

            var createEmptyArrayOperation = _availableOperations.GetValueOrDefault(operationSignature);
            if (createEmptyArrayOperation is null)
                throw new OperationNotAvailableException(context, operationSignature);

            return createEmptyArrayOperation(context, []);
        }

        var expressionValues = new List<GlyphScriptValue>();

        foreach (var expr in context.expressionList().expression())
        {
            var value = Visit(expr) as GlyphScriptValue ??
                throw new InvalidOperationException("Failed to evaluate array element expression");
            expressionValues.Add(value);
        }

        var elementType = expressionValues[0].Type;
        for (int i = 1; i < expressionValues.Count; i++)
        {
            if (expressionValues[i].Type != elementType)
                throw new InvalidSyntaxException(context,
                    $"Array elements must be of the same type. Found {elementType} and {expressionValues[i].Type}");
        }

        var arrayOperationSignature = new OperationSignature(operationKind, [elementType]);
        var createArrayOperation = _availableOperations.GetValueOrDefault(arrayOperationSignature);

        if (createArrayOperation is null)
            throw new OperationNotAvailableException(context, arrayOperationSignature);

        return createArrayOperation(context, expressionValues.ToArray());
    }

    public override object? VisitArrayAccessExp(GlyphScriptParser.ArrayAccessExpContext context)
    {
        const OperationKind operationKind = OperationKind.ArrayAccess;

        var arrayValue = Visit(context.expression(0)) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to resolve array expression");

        if (arrayValue.Type != GlyphScriptType.Array || arrayValue.ArrayInfo == null)
            throw new InvalidSyntaxException(context, $"Expression is not an array: {context.expression(0).GetText()}");

        var indexValue = Visit(context.expression(1)) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to resolve index expression");

        if (indexValue.Type != GlyphScriptType.Int && indexValue.Type != GlyphScriptType.Long)
            throw new InvalidSyntaxException(context,
                $"Array indices must be integer types. Found: {indexValue.Type}");

        var accessOperationSignature = new OperationSignature(operationKind,
            [arrayValue.Type, indexValue.Type, arrayValue.ArrayInfo.ElementType]);

        var arrayAccessOperation = _availableOperations.GetValueOrDefault(accessOperationSignature);
        if (arrayAccessOperation is null)
            throw new OperationNotAvailableException(context, accessOperationSignature);

        return arrayAccessOperation(context, [arrayValue, indexValue]);
    }

    public override object? VisitArrayOfType(GlyphScriptParser.ArrayOfTypeContext context)
    {
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
            GlyphScriptType.Array when arrayInfo != null => LLVM.PointerType(LLVM.Int8Type(), 0),
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

    private object? VisitBinaryExpression(
        GlyphScriptParser.ExpressionContext context,
        GlyphScriptValue left,
        GlyphScriptValue right,
        OperationKind operationKind)
    {
        var operationSignature = new OperationSignature(operationKind, [left.Type, right.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation(context, [left, right]);
    }

    public void Dispose()
    {
        LLVM.DisposeBuilder(_llvmBuilder);
    }
}
