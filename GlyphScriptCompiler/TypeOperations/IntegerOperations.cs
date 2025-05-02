using static GlyphScriptCompiler.OperationKind;

namespace GlyphScriptCompiler.TypeOperations;

public class IntegerOperations : IOperationProvider
{
    private readonly LLVMModuleRef _llvmModule;
    private readonly LLVMBuilderRef _llvmBuilder;

    public IntegerOperations(LLVMModuleRef llvmModule, LLVMBuilderRef llvmBuilder)
    {
        _llvmModule = llvmModule;
        _llvmBuilder = llvmBuilder;
    }

    public GlyphScriptValue? DefaultValueImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters) =>
        GetDefaultValue();

    public GlyphScriptValue GetDefaultValue() =>
        new GlyphScriptValue(LLVM.ConstInt(LLVM.Int32Type(), 0, false), GlyphScriptType.Int);

    public GlyphScriptValue? AdditionImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for addition");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for addition");

        return Add(left, right);
    }

    public GlyphScriptValue Add(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for addition");

        var result = LLVM.BuildAdd(_llvmBuilder, left.Value, right.Value, "add_int");
        return new GlyphScriptValue(result, GlyphScriptType.Int);
    }

    public GlyphScriptValue? SubtractionImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for subtraction");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for subtraction");

        return Subtract(left, right);
    }

    public GlyphScriptValue Subtract(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for subtraction");

        var result = LLVM.BuildSub(_llvmBuilder, left.Value, right.Value, "sub_int");
        return new GlyphScriptValue(result, GlyphScriptType.Int);
    }

    public GlyphScriptValue? MultiplicationImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for multiplication");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for multiplication");

        return Multiply(left, right);
    }

    public GlyphScriptValue Multiply(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for multiplication");

        var result = LLVM.BuildMul(_llvmBuilder, left.Value, right.Value, "mul_int");
        return new GlyphScriptValue(result, GlyphScriptType.Int);
    }

    public GlyphScriptValue? DivisionImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for division");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for division");

        return Divide(left, right);
    }

    public GlyphScriptValue Divide(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for division");

        var result = LLVM.BuildSDiv(_llvmBuilder, left.Value, right.Value, "div_int");
        return new GlyphScriptValue(result, GlyphScriptType.Int);
    }

    public GlyphScriptValue? PowerImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for power");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for power");

        return Power(left, right);
    }

    public GlyphScriptValue Power(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
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

        var intResult = LLVM.BuildFPToSI(_llvmBuilder, powResult, LLVM.Int32Type(), "to_int");

        return new GlyphScriptValue(intResult, GlyphScriptType.Int);
    }

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations =>
        new Dictionary<OperationSignature, OperationImplementation>()
        {
            { new OperationSignature(DefaultValue, [GlyphScriptType.Int]), DefaultValueImplementation },
            { new OperationSignature(Addition, [GlyphScriptType.Int, GlyphScriptType.Int]), AdditionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Int, GlyphScriptType.Int]), SubtractionImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Int, GlyphScriptType.Int]), MultiplicationImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Int, GlyphScriptType.Int]), DivisionImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Int, GlyphScriptType.Int]), PowerImplementation }
        };
}
