using GlyphScriptCompiler.Contracts;
using static GlyphScriptCompiler.Models.OperationKind;

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

    public void Initialize()
    {
        LlvmHelper.CreateStringConstant(_llvmModule, "strp_int", "%d\n\0");
        LlvmHelper.CreateStringConstant(_llvmModule, "strs_int", "%d\0");
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

    public GlyphScriptValue? PrintImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 1)
            throw new InvalidOperationException("Invalid number of parameters for print operation");

        var value = parameters[0];

        if (value.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid type for print operation");

        // Get printf function
        var printfFunc = LLVM.GetNamedFunction(_llvmModule, "printf");
        if (printfFunc.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("printf function not found");

        // Get format string for integer printing
        var formatGlobal = LLVM.GetNamedGlobal(_llvmModule, "strp_int");
        if (formatGlobal.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Format string for int printing not found");

        // Create GEP to get a pointer to the format string
        var formatPtr = LlvmHelper.GetStringPtr(_llvmBuilder, formatGlobal);

        // Call printf with the format string and the integer value
        LLVM.BuildCall(_llvmBuilder, printfFunc, [formatPtr, value.Value], string.Empty);

        return value;
    }

    public GlyphScriptValue? ReadImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 1)
            throw new InvalidOperationException("Invalid number of parameters for read operation");

        var variable = parameters[0];

        if (variable.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid type for read operation");

        // Get scanf function
        var scanfFunc = LLVM.GetNamedFunction(_llvmModule, "scanf");
        if (scanfFunc.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("scanf function not found");

        // Get format string for integer reading
        var formatGlobal = LLVM.GetNamedGlobal(_llvmModule, "strs_int");
        if (formatGlobal.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Format string for int reading not found");

        // Create GEP to get a pointer to the format string
        var formatPtr = LlvmHelper.GetStringPtr(_llvmBuilder, formatGlobal);

        // Create a temporary variable to store the read value
        var tempVar = LLVM.BuildAlloca(_llvmBuilder, LLVM.Int32Type(), "temp_int");

        // Call scanf with the format string and the address of the temporary variable
        LLVM.BuildCall(_llvmBuilder, scanfFunc, [formatPtr, tempVar], string.Empty);

        // Load the value from the temporary variable
        var readValue = LLVM.BuildLoad(_llvmBuilder, tempVar, "read_int");

        return new GlyphScriptValue(readValue, GlyphScriptType.Int);
    }

    public GlyphScriptValue? ParseImmediateImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        var immediateValueContext = context as GlyphScriptParser.ImmediateValueContext
            ?? throw new InvalidOperationException("Invalid context for parsing immediate value");
        var rawValue = immediateValueContext.INT_LITERAL()?.GetText()
            ?? throw new InvalidOperationException("Invalid context for parsing integer value");

        var value = int.Parse(rawValue);
        return new GlyphScriptValue(LLVM.ConstInt(LLVM.Int32Type(), (ulong)value, false), GlyphScriptType.Int);
    }

    public GlyphScriptValue? ComparisonImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for comparison");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for comparison");

        return Compare(left, right);
    }
    public GlyphScriptValue Compare(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for comparison");

        var result = LLVM.BuildICmp(_llvmBuilder, LLVMIntPredicate.LLVMIntEQ, left.Value, right.Value, "cmp_int");
        return new GlyphScriptValue(result, GlyphScriptType.Boolean);
    }

    public GlyphScriptValue? LesThanImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for less than operation");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for less than operation");

        return LessThan(left, right);
    }
    public GlyphScriptValue LessThan(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for less than operation");

        var result = LLVM.BuildICmp(_llvmBuilder, LLVMIntPredicate.LLVMIntSLT, left.Value, right.Value, "lt_int");
        return new GlyphScriptValue(result, GlyphScriptType.Boolean);
    }

    public GlyphScriptValue? GreaterThanImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for greater than operation");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for greater than operation");

        return GreaterThan(left, right);
    }
    public GlyphScriptValue GreaterThan(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for greater than operation");

        var result = LLVM.BuildICmp(_llvmBuilder, LLVMIntPredicate.LLVMIntSGT, left.Value, right.Value, "gt_int");
        return new GlyphScriptValue(result, GlyphScriptType.Boolean);
    }


    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations =>
        new Dictionary<OperationSignature, OperationImplementation>()
        {
            { new OperationSignature(DefaultValue, [GlyphScriptType.Int]), DefaultValueImplementation },
            { new OperationSignature(ParseImmediate, [GlyphScriptType.Int]), ParseImmediateImplementation },
            { new OperationSignature(Addition, [GlyphScriptType.Int, GlyphScriptType.Int]), AdditionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Int, GlyphScriptType.Int]), SubtractionImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Int, GlyphScriptType.Int]), MultiplicationImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Int, GlyphScriptType.Int]), DivisionImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Int, GlyphScriptType.Int]), PowerImplementation },
            { new OperationSignature(Print, [GlyphScriptType.Int]), PrintImplementation },
            { new OperationSignature(Read, [GlyphScriptType.Int]), ReadImplementation },

            { new OperationSignature(Comparison, [GlyphScriptType.Int, GlyphScriptType.Int]), ComparisonImplementation },
            { new OperationSignature(OperationKind.LessThan, [GlyphScriptType.Int, GlyphScriptType.Int]), LesThanImplementation },
            { new OperationSignature(OperationKind.GreaterThan, [GlyphScriptType.Int, GlyphScriptType.Int]), GreaterThanImplementation }
        };
}
