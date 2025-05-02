using LLVMSharp;
using static GlyphScriptCompiler.OperationKind;

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

    public GlyphScriptValue? DefaultValueImplementation(IReadOnlyList<GlyphScriptValue> parameters) =>
        GetDefaultValue();

    public GlyphScriptValue GetDefaultValue() =>
        new GlyphScriptValue(LLVM.ConstInt(LLVM.Int64Type(), 0, false), GlyphScriptType.Long);

    public GlyphScriptValue? AdditionImplementation(IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for addition");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Long || right.Type != GlyphScriptType.Long)
            throw new InvalidOperationException("Invalid types for addition");

        return Add(left, right);
    }

    public GlyphScriptValue Add(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Long || right.Type != GlyphScriptType.Long)
            throw new InvalidOperationException("Invalid types for addition");

        var result = LLVM.BuildAdd(_llvmBuilder, left.Value, right.Value, "add_long");
        return new GlyphScriptValue(result, GlyphScriptType.Long);
    }

    public GlyphScriptValue? SubtractionImplementation(IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for subtraction");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Long || right.Type != GlyphScriptType.Long)
            throw new InvalidOperationException("Invalid types for subtraction");

        return Subtract(left, right);
    }

    public GlyphScriptValue Subtract(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Long || right.Type != GlyphScriptType.Long)
            throw new InvalidOperationException("Invalid types for subtraction");

        var result = LLVM.BuildSub(_llvmBuilder, left.Value, right.Value, "sub_long");
        return new GlyphScriptValue(result, GlyphScriptType.Long);
    }

    public GlyphScriptValue? MultiplicationImplementation(IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for multiplication");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Long || right.Type != GlyphScriptType.Long)
            throw new InvalidOperationException("Invalid types for multiplication");

        return Multiply(left, right);
    }

    public GlyphScriptValue Multiply(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Long || right.Type != GlyphScriptType.Long)
            throw new InvalidOperationException("Invalid types for multiplication");

        var result = LLVM.BuildMul(_llvmBuilder, left.Value, right.Value, "mul_long");
        return new GlyphScriptValue(result, GlyphScriptType.Long);
    }

    public GlyphScriptValue? DivisionImplementation(IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for division");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Long || right.Type != GlyphScriptType.Long)
            throw new InvalidOperationException("Invalid types for division");

        return Divide(left, right);
    }

    public GlyphScriptValue Divide(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Long || right.Type != GlyphScriptType.Long)
            throw new InvalidOperationException("Invalid types for division");

        var result = LLVM.BuildSDiv(_llvmBuilder, left.Value, right.Value, "div_long");
        return new GlyphScriptValue(result, GlyphScriptType.Long);
    }

    public GlyphScriptValue? PowerImplementation(IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for power");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Long || right.Type != GlyphScriptType.Long)
            throw new InvalidOperationException("Invalid types for power");

        return Power(left, right);
    }

    public GlyphScriptValue Power(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Long || right.Type != GlyphScriptType.Long)
            throw new InvalidOperationException("Invalid types for power");

        var leftDouble = LLVM.BuildSIToFP(_llvmBuilder, left.Value, LLVM.DoubleType(), "to_double_left");
        var rightDouble = LLVM.BuildSIToFP(_llvmBuilder, right.Value, LLVM.DoubleType(), "to_double_right");

        var powFunc = LLVM.GetNamedFunction(_llvmModule, "pow");
        if (powFunc.Pointer == IntPtr.Zero)
        {
            var powType = LLVM.FunctionType(LLVM.DoubleType(), [LLVM.DoubleType(), LLVM.DoubleType()], false);
            powFunc = LLVM.AddFunction(_llvmModule, "pow", powType);
        }

        var powResult = LLVM.BuildCall(_llvmBuilder, powFunc, [leftDouble, rightDouble], "pow_call");
        var longResult = LLVM.BuildFPToSI(_llvmBuilder, powResult, LLVM.Int64Type(), "to_long");

        return new GlyphScriptValue(longResult, GlyphScriptType.Long);
    }

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations =>
        new Dictionary<OperationSignature, OperationImplementation>()
        {
            { new OperationSignature(DefaultValue, Array.Empty<GlyphScriptType>()), DefaultValueImplementation },
            { new OperationSignature(Addition, [GlyphScriptType.Long, GlyphScriptType.Long]), AdditionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Long, GlyphScriptType.Long]), SubtractionImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Long, GlyphScriptType.Long]), MultiplicationImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Long, GlyphScriptType.Long]), DivisionImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Long, GlyphScriptType.Long]), PowerImplementation }
        };
}
