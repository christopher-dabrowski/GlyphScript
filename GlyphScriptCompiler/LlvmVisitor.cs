using GlyphScriptCompiler.SyntaxErrors;
using GlyphScriptCompiler.TypeOperations;

namespace GlyphScriptCompiler;

public sealed class LlvmVisitor : GlyphScriptBaseVisitor<object?>, IDisposable
{
    public LLVMModuleRef LlvmModule { get; }
    private readonly LLVMBuilderRef _llvmBuilder = LLVM.CreateBuilder();
    // TODO: Remove ExpressionResultTypeEngine
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
            new StringOperations(llvmModule, _llvmBuilder)
        ];

        foreach (var provider in initialOperationProviders)
            RegisterOperations(provider);
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

        var operationSignature = new OperationSignature(operationKind, [type]);
        var createDefaultValueOperation = _availableOperations.GetValueOrDefault(operationSignature);
        if (createDefaultValueOperation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        var value = createDefaultValueOperation(context, [])
            ?? throw new InvalidOperationException($"Failed to create default value for type {type}");

        if (_variables.ContainsKey(id))
            throw new InvalidOperationException($"Variable '{id}' is already defined.");

        var llvmType = GetLlvmType(type);
        var variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);

        LLVM.BuildStore(_llvmBuilder, value.Value, variable);

        var result = new GlyphScriptValue(variable, type);
        _variables[id] = result;
        return result;
    }

    public override object? VisitInitializingDeclaration(GlyphScriptParser.InitializingDeclarationContext context)
    {
        var type = GetTypeFromContext(context.type());
        var id = context.ID().GetText();

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

        var llvmType = GetLlvmType(type);
        var variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);

        LLVM.BuildStore(_llvmBuilder, expressionValue.Value, variable);

        var result = new GlyphScriptValue(variable, type);
        _variables[id] = result;
        return result;
    }

    public override object? VisitAssignment(GlyphScriptParser.AssignmentContext context)
    {
        var id = context.ID().GetText();
        var expressionValue = Visit(context.expression()) as GlyphScriptValue ??
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

    public override object? VisitPrint(GlyphScriptParser.PrintContext context)
    {
        var expressionValue = Visit(context.expression()) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to create expression");

        var value = expressionValue.Value;
        var valueType = LLVM.TypeOf(value);
        var valueKind = LLVM.GetTypeKind(valueType);
        var printfFormatStr = GetPrintfFormatString(expressionValue.Type);

        var printfFunc = LLVM.GetNamedFunction(LlvmModule, "printf");

        // To print float we need to convert it to double first
        if (valueKind == LLVMTypeKind.LLVMFloatTypeKind)
            value = LLVM.BuildFPExt(_llvmBuilder, value, LLVM.DoubleType(), string.Empty);

        var args = new[] { LlvmHelper.GetStringPtr(_llvmBuilder, printfFormatStr), value };

        LLVM.BuildCall(_llvmBuilder, printfFunc, args, "printf_call");
        return null;
    }

    public override object? VisitRead(GlyphScriptParser.ReadContext context)
    {
        var id = context.ID().GetText();

        if (!_variables.TryGetValue(id, out var variable))
        {
            throw new UndefinedVariableUsageException(context) { VariableName = id };
        }

        if (variable.Type == GlyphScriptType.String)
        {
            var mallocFunc = LLVM.GetNamedFunction(LlvmModule, "malloc");
            var bufferSize = LLVM.ConstInt(LLVM.Int64Type(), 256, false);
            var buffer = LLVM.BuildCall(_llvmBuilder, mallocFunc, [bufferSize], "string_buffer");

            LLVM.BuildStore(_llvmBuilder, buffer, variable.Value);

            var scanfFunc = LLVM.GetNamedFunction(LlvmModule, "scanf");
            var scanfLineFormatStr = LLVM.GetNamedGlobal(LlvmModule, "strs_line");

            var args = new[] { LlvmHelper.GetStringPtr(_llvmBuilder, scanfLineFormatStr), buffer };
            LLVM.BuildCall(_llvmBuilder, scanfFunc, args, "scanf_call");

            var loadedValue = LLVM.BuildLoad(_llvmBuilder, variable.Value, $"read_{id}");
            return new GlyphScriptValue(loadedValue, variable.Type);
        }
        else
        {
            // For non-string types, use standard approach
            var scanfFunc = LLVM.GetNamedFunction(LlvmModule, "scanf");
            var scanfFormatStr = GetScanfFormatString(variable.Type);

            var args = new[] { LlvmHelper.GetStringPtr(_llvmBuilder, scanfFormatStr), variable.Value };
            LLVM.BuildCall(_llvmBuilder, scanfFunc, args, "scanf_call");

            // Load the value to return
            var loadedValue = LLVM.BuildLoad(_llvmBuilder, variable.Value, $"read_{id}");
            return new GlyphScriptValue(loadedValue, variable.Type);
        }
    }

    public override object? VisitImmediateValue(GlyphScriptParser.ImmediateValueContext context)
    {
        const OperationKind operationKind = OperationKind.ParseImmediate;

        static GlyphScriptType DetectType(GlyphScriptParser.ImmediateValueContext context)
        {
            if (context.INT_LITERAL() != null) return GlyphScriptType.Int;
            if (context.LONG_LITERAL() != null) return GlyphScriptType.Long;
            if (context.FLOAT_LITERAL() != null) return GlyphScriptType.Float;
            if (context.DOUBLE_LITERAL() != null) return GlyphScriptType.Double;
            if (context.STRING_LITERAL() != null) return GlyphScriptType.String;
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

        return new GlyphScriptValue(LLVM.BuildLoad(_llvmBuilder, variable.Value, id), variable.Type);
    }

    private static GlyphScriptType GetTypeFromContext(GlyphScriptParser.TypeContext context)
    {
        if (context.INT() != null) return GlyphScriptType.Int;
        if (context.LONG() != null) return GlyphScriptType.Long;
        if (context.FLOAT() != null) return GlyphScriptType.Float;
        if (context.DOUBLE() != null) return GlyphScriptType.Double;
        if (context.STRING_TYPE() != null) return GlyphScriptType.String;
        throw new InvalidOperationException("Invalid type");
    }

    private static LLVMTypeRef GetLlvmType(GlyphScriptType glyphScriptType)
    {
        return glyphScriptType switch
        {
            GlyphScriptType.Int => LLVM.Int32Type(),
            GlyphScriptType.Long => LLVM.Int64Type(),
            GlyphScriptType.Float => LLVM.FloatType(),
            GlyphScriptType.Double => LLVM.DoubleType(),
            GlyphScriptType.String => LLVM.PointerType(LLVM.Int8Type(), 0),
            _ => throw new InvalidOperationException($"Unsupported type: {glyphScriptType}")
        };
    }

    private void SetupGlobalFunctions(LLVMModuleRef module)
    {
        var i8Type = LLVM.Int8Type();
        var i8PtrType = LLVM.PointerType(i8Type, 0);

        LlvmHelper.CreateStringConstant(module, "strp_int", "%d\n\0");
        LlvmHelper.CreateStringConstant(module, "strp_long", "%ld\n\0");
        LlvmHelper.CreateStringConstant(module, "strp_float", "%f\n\0");
        LlvmHelper.CreateStringConstant(module, "strp_double", "%lf\n\0");
        LlvmHelper.CreateStringConstant(module, "strp_string", "%s\n\0");

        LlvmHelper.CreateStringConstant(module, "strs_int", "%d\0");
        LlvmHelper.CreateStringConstant(module, "strs_long", "%ld\0");
        LlvmHelper.CreateStringConstant(module, "strs_float", "%f\0");
        LlvmHelper.CreateStringConstant(module, "strs_double", "%lf\0");
        LlvmHelper.CreateStringConstant(module, "strs_string", "%s\0");

        LlvmHelper.CreateStringConstant(module, "strs_line", "%[^\n]\0");

        LLVMTypeRef[] printfParamTypes = [i8PtrType];
        var printfType = LLVM.FunctionType(LLVM.Int32Type(), printfParamTypes, true);
        LLVM.AddFunction(module, "printf", printfType);

        var scanfType = LLVM.FunctionType(LLVM.Int32Type(), printfParamTypes, true);
        LLVM.AddFunction(module, "scanf", scanfType);

        var mallocType = LLVM.FunctionType(i8PtrType, [LLVM.Int64Type()], false);
        LLVM.AddFunction(module, "malloc", mallocType);
    }

    private LLVMValueRef GetPrintfFormatString(GlyphScriptType glyphScriptType)
    {
        return glyphScriptType switch
        {
            GlyphScriptType.Int => LLVM.GetNamedGlobal(LlvmModule, "strp_int"),
            GlyphScriptType.Long => LLVM.GetNamedGlobal(LlvmModule, "strp_long"),
            GlyphScriptType.Float => LLVM.GetNamedGlobal(LlvmModule, "strp_float"),
            GlyphScriptType.Double => LLVM.GetNamedGlobal(LlvmModule, "strp_double"),
            GlyphScriptType.String => LLVM.GetNamedGlobal(LlvmModule, "strp_string"),
            _ => throw new InvalidOperationException($"Unsupported type for printf: {glyphScriptType}")
        };
    }

    private LLVMValueRef GetScanfFormatString(GlyphScriptType glyphScriptType)
    {
        return glyphScriptType switch
        {
            GlyphScriptType.Int => LLVM.GetNamedGlobal(LlvmModule, "strs_int"),
            GlyphScriptType.Long => LLVM.GetNamedGlobal(LlvmModule, "strs_long"),
            GlyphScriptType.Float => LLVM.GetNamedGlobal(LlvmModule, "strs_float"),
            GlyphScriptType.Double => LLVM.GetNamedGlobal(LlvmModule, "strs_double"),
            GlyphScriptType.String => LLVM.GetNamedGlobal(LlvmModule, "strs_string"),
            _ => throw new InvalidOperationException($"Unsupported type for scanf: {glyphScriptType}")
        };
    }

    public void Dispose()
    {
        LLVM.DisposeBuilder(_llvmBuilder);
    }
}
