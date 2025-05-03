using GlyphScriptCompiler.Contracts;
using static GlyphScriptCompiler.Models.OperationKind;

namespace GlyphScriptCompiler.TypeOperations;

public class LongOperations : IOperationProvider
{
    private readonly LLVMModuleRef _llvmModule;
    private readonly LLVMBuilderRef _llvmBuilder;

    public LongOperations(LLVMModuleRef llvmModule, LLVMBuilderRef llvmBuilder)
    {
        _llvmModule = llvmModule;
        _llvmBuilder = llvmBuilder;
    }

    public void Initialize()
    {
        LlvmHelper.CreateStringConstant(_llvmModule, "strp_long", "%ld\n\0");
        LlvmHelper.CreateStringConstant(_llvmModule, "strs_long", "%ld\0");
    }

    public GlyphScriptValue? DefaultValueImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters) =>
        GetDefaultValue();

    public GlyphScriptValue GetDefaultValue() =>
        new GlyphScriptValue(LLVM.ConstInt(LLVM.Int64Type(), 0, false), GlyphScriptType.Long);

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
        var promotedLeft = PromoteToLong(left);
        var promotedRight = PromoteToLong(right);

        var result = LLVM.BuildAdd(_llvmBuilder, promotedLeft, promotedRight, "add_long");
        return new GlyphScriptValue(result, GlyphScriptType.Long);
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
        var promotedLeft = PromoteToLong(left);
        var promotedRight = PromoteToLong(right);

        var result = LLVM.BuildSub(_llvmBuilder, promotedLeft, promotedRight, "sub_long");
        return new GlyphScriptValue(result, GlyphScriptType.Long);
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
        var promotedLeft = PromoteToLong(left);
        var promotedRight = PromoteToLong(right);

        var result = LLVM.BuildMul(_llvmBuilder, promotedLeft, promotedRight, "mul_long");
        return new GlyphScriptValue(result, GlyphScriptType.Long);
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
        var promotedLeft = PromoteToLong(left);
        var promotedRight = PromoteToLong(right);

        var result = LLVM.BuildSDiv(_llvmBuilder, promotedLeft, promotedRight, "div_long");
        return new GlyphScriptValue(result, GlyphScriptType.Long);
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
        // Convert operands to double for the pow function
        var leftDouble = ConvertToDouble(left.Value, left.Type);
        var rightDouble = ConvertToDouble(right.Value, right.Type);

        // Get or create pow function
        var powFunc = LLVM.GetNamedFunction(_llvmModule, "pow");
        if (powFunc.Pointer == IntPtr.Zero)
        {
            var powType = LLVM.FunctionType(LLVM.DoubleType(), [LLVM.DoubleType(), LLVM.DoubleType()], false);
            powFunc = LLVM.AddFunction(_llvmModule, "pow", powType);
        }

        // Call pow function
        var powResult = LLVM.BuildCall(_llvmBuilder, powFunc, [leftDouble, rightDouble], "pow_call");

        // Convert the result back to long
        var longResult = LLVM.BuildFPToSI(_llvmBuilder, powResult, LLVM.Int64Type(), "to_long");
        return new GlyphScriptValue(longResult, GlyphScriptType.Long);
    }

    public GlyphScriptValue? ParseImmediateImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        var immediateValueContext = context as GlyphScriptParser.ImmediateValueContext
            ?? throw new InvalidOperationException("Invalid context for parsing immediate value");
        var rawValue = immediateValueContext.LONG_LITERAL()?.GetText()
            ?? throw new InvalidOperationException("Invalid context for parsing long value");

        var value = long.Parse(rawValue.TrimEnd('L', 'l'));
        return new GlyphScriptValue(LLVM.ConstInt(LLVM.Int64Type(), (ulong)value, false), GlyphScriptType.Long);
    }

    private LLVMValueRef PromoteToLong(GlyphScriptValue value)
    {
        if (value.Type == GlyphScriptType.Long)
            return value.Value;

        if (value.Type == GlyphScriptType.Int)
            return LLVM.BuildSExt(_llvmBuilder, value.Value, LLVM.Int64Type(), "int_to_long");

        throw new InvalidOperationException($"Cannot convert {value.Type} to Long in this operation provider");
    }

    private LLVMValueRef ConvertToDouble(LLVMValueRef value, GlyphScriptType sourceType)
    {
        if (sourceType == GlyphScriptType.Double)
            return value;

        if (sourceType == GlyphScriptType.Float)
            return LLVM.BuildFPExt(_llvmBuilder, value, LLVM.DoubleType(), "float_to_double");

        if (sourceType == GlyphScriptType.Int || sourceType == GlyphScriptType.Long)
            return LLVM.BuildSIToFP(_llvmBuilder, value, LLVM.DoubleType(), "int_to_double");

        throw new InvalidOperationException($"Cannot convert {sourceType} to Double");
    }

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations =>
        new Dictionary<OperationSignature, OperationImplementation>()
        {
            { new OperationSignature(DefaultValue, [GlyphScriptType.Long]), DefaultValueImplementation },
            { new OperationSignature(ParseImmediate, [GlyphScriptType.Long]), ParseImmediateImplementation },

            // Long-Long operations
            { new OperationSignature(Addition, [GlyphScriptType.Long, GlyphScriptType.Long]), AdditionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Long, GlyphScriptType.Long]), SubtractionImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Long, GlyphScriptType.Long]), MultiplicationImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Long, GlyphScriptType.Long]), DivisionImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Long, GlyphScriptType.Long]), PowerImplementation },

            // Long-Int operations
            { new OperationSignature(Addition, [GlyphScriptType.Long, GlyphScriptType.Int]), AdditionImplementation },
            { new OperationSignature(Addition, [GlyphScriptType.Int, GlyphScriptType.Long]), AdditionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Long, GlyphScriptType.Int]), SubtractionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Int, GlyphScriptType.Long]), SubtractionImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Long, GlyphScriptType.Int]), MultiplicationImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Int, GlyphScriptType.Long]), MultiplicationImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Long, GlyphScriptType.Int]), DivisionImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Int, GlyphScriptType.Long]), DivisionImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Long, GlyphScriptType.Int]), PowerImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Int, GlyphScriptType.Long]), PowerImplementation },
        };
}
