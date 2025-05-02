using static GlyphScriptCompiler.OperationKind;

namespace GlyphScriptCompiler.TypeOperations;

public class FloatOperations : IOperationProvider
{
    private readonly LLVMModuleRef _llvmModule;
    private readonly LLVMBuilderRef _llvmBuilder;

    public FloatOperations(LLVMModuleRef llvmModule, LLVMBuilderRef llvmBuilder)
    {
        _llvmModule = llvmModule;
        _llvmBuilder = llvmBuilder;
    }

    public GlyphScriptValue? DefaultValueImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters) =>
        GetDefaultValue();

    public GlyphScriptValue GetDefaultValue() =>
        new GlyphScriptValue(LLVM.ConstReal(LLVM.FloatType(), 0.0f), GlyphScriptType.Float);

    public GlyphScriptValue? AdditionImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for addition");

        var left = parameters[0];
        var right = parameters[1];

        return Add(left, right);
    }

    public GlyphScriptValue Add(GlyphScriptValue left, GlyphScriptValue right)
    {
        var promotedLeft = PromoteToFloat(left);
        var promotedRight = PromoteToFloat(right);

        var result = LLVM.BuildFAdd(_llvmBuilder, promotedLeft, promotedRight, "add_float");
        return new GlyphScriptValue(result, GlyphScriptType.Float);
    }

    public GlyphScriptValue? SubtractionImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for subtraction");

        var left = parameters[0];
        var right = parameters[1];

        return Subtract(left, right);
    }

    public GlyphScriptValue Subtract(GlyphScriptValue left, GlyphScriptValue right)
    {
        var promotedLeft = PromoteToFloat(left);
        var promotedRight = PromoteToFloat(right);

        var result = LLVM.BuildFSub(_llvmBuilder, promotedLeft, promotedRight, "sub_float");
        return new GlyphScriptValue(result, GlyphScriptType.Float);
    }

    public GlyphScriptValue? MultiplicationImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for multiplication");

        var left = parameters[0];
        var right = parameters[1];

        return Multiply(left, right);
    }

    public GlyphScriptValue Multiply(GlyphScriptValue left, GlyphScriptValue right)
    {
        var promotedLeft = PromoteToFloat(left);
        var promotedRight = PromoteToFloat(right);

        var result = LLVM.BuildFMul(_llvmBuilder, promotedLeft, promotedRight, "mul_float");
        return new GlyphScriptValue(result, GlyphScriptType.Float);
    }

    public GlyphScriptValue? DivisionImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for division");

        var left = parameters[0];
        var right = parameters[1];

        return Divide(left, right);
    }

    public GlyphScriptValue Divide(GlyphScriptValue left, GlyphScriptValue right)
    {
        var promotedLeft = PromoteToFloat(left);
        var promotedRight = PromoteToFloat(right);

        var result = LLVM.BuildFDiv(_llvmBuilder, promotedLeft, promotedRight, "div_float");
        return new GlyphScriptValue(result, GlyphScriptType.Float);
    }

    public GlyphScriptValue? PowerImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for power");

        var left = parameters[0];
        var right = parameters[1];

        return Power(left, right);
    }

    public GlyphScriptValue Power(GlyphScriptValue left, GlyphScriptValue right)
    {
        var leftDouble = ConvertToDouble(left.Value, left.Type);
        var rightDouble = ConvertToDouble(right.Value, right.Type);

        var powFunc = LLVM.GetNamedFunction(_llvmModule, "pow");
        if (powFunc.Pointer == IntPtr.Zero)
        {
            var powType = LLVM.FunctionType(LLVM.DoubleType(), [LLVM.DoubleType(), LLVM.DoubleType()], false);
            powFunc = LLVM.AddFunction(_llvmModule, "pow", powType);
        }

        var powResult = LLVM.BuildCall(_llvmBuilder, powFunc, [leftDouble, rightDouble], "pow_call");

        var floatResult = LLVM.BuildFPTrunc(_llvmBuilder, powResult, LLVM.FloatType(), "to_float");
        return new GlyphScriptValue(floatResult, GlyphScriptType.Float);
    }

    private LLVMValueRef PromoteToFloat(GlyphScriptValue value)
    {
        return value.Type switch
        {
            GlyphScriptType.Float => value.Value,
            GlyphScriptType.Int => LLVM.BuildSIToFP(_llvmBuilder, value.Value, LLVM.FloatType(), "int_to_float"),
            GlyphScriptType.Long => LLVM.BuildSIToFP(_llvmBuilder, value.Value, LLVM.FloatType(), "long_to_float"),
            GlyphScriptType.Double => LLVM.BuildFPTrunc(_llvmBuilder, value.Value, LLVM.FloatType(), "double_to_float"),
            _ => throw new InvalidOperationException($"Cannot convert {value.Type} to Float in this operation provider")
        };
    }

    private LLVMValueRef ConvertToDouble(LLVMValueRef value, GlyphScriptType sourceType)
    {
        return sourceType switch
        {
            GlyphScriptType.Double => value,
            GlyphScriptType.Float => LLVM.BuildFPExt(_llvmBuilder, value, LLVM.DoubleType(), "float_to_double"),
            GlyphScriptType.Int or GlyphScriptType.Long => LLVM.BuildSIToFP(_llvmBuilder, value, LLVM.DoubleType(), "int_to_double"),
            _ => throw new InvalidOperationException($"Cannot convert {sourceType} to Double")
        };
    }

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations =>
        new Dictionary<OperationSignature, OperationImplementation>
        {
            { new OperationSignature(DefaultValue, [GlyphScriptType.Float]), DefaultValueImplementation },

            // Float-Float operations
            { new OperationSignature(Addition, [GlyphScriptType.Float, GlyphScriptType.Float]), AdditionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Float, GlyphScriptType.Float]), SubtractionImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Float, GlyphScriptType.Float]), MultiplicationImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Float, GlyphScriptType.Float]), DivisionImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Float, GlyphScriptType.Float]), PowerImplementation },

            // Float-Int operations
            { new OperationSignature(Addition, [GlyphScriptType.Float, GlyphScriptType.Int]), AdditionImplementation },
            { new OperationSignature(Addition, [GlyphScriptType.Int, GlyphScriptType.Float]), AdditionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Float, GlyphScriptType.Int]), SubtractionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Int, GlyphScriptType.Float]), SubtractionImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Float, GlyphScriptType.Int]), MultiplicationImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Int, GlyphScriptType.Float]), MultiplicationImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Float, GlyphScriptType.Int]), DivisionImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Int, GlyphScriptType.Float]), DivisionImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Float, GlyphScriptType.Int]), PowerImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Int, GlyphScriptType.Float]), PowerImplementation },

            // Float-Long operations
            { new OperationSignature(Addition, [GlyphScriptType.Float, GlyphScriptType.Long]), AdditionImplementation },
            { new OperationSignature(Addition, [GlyphScriptType.Long, GlyphScriptType.Float]), AdditionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Float, GlyphScriptType.Long]), SubtractionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Long, GlyphScriptType.Float]), SubtractionImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Float, GlyphScriptType.Long]), MultiplicationImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Long, GlyphScriptType.Float]), MultiplicationImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Float, GlyphScriptType.Long]), DivisionImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Long, GlyphScriptType.Float]), DivisionImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Float, GlyphScriptType.Long]), PowerImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Long, GlyphScriptType.Float]), PowerImplementation },
        };
}
