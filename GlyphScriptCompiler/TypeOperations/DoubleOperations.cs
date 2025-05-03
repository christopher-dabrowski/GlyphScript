using GlyphScriptCompiler.Contracts;
using static GlyphScriptCompiler.Models.OperationKind;

namespace GlyphScriptCompiler.TypeOperations;

public class DoubleOperations : IOperationProvider
{
    private readonly LLVMModuleRef _llvmModule;
    private readonly LLVMBuilderRef _llvmBuilder;

    public DoubleOperations(LLVMModuleRef llvmModule, LLVMBuilderRef llvmBuilder)
    {
        _llvmModule = llvmModule;
        _llvmBuilder = llvmBuilder;
    }

    public void Initialize()
    {
        LlvmHelper.CreateStringConstant(_llvmModule, "strp_double", "%lf\n\0");
        LlvmHelper.CreateStringConstant(_llvmModule, "strs_double", "%lf\0");
    }

    public GlyphScriptValue? DefaultValueImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters) =>
        GetDefaultValue();

    public GlyphScriptValue GetDefaultValue() =>
        new(LLVM.ConstReal(LLVM.DoubleType(), 0.0), GlyphScriptType.Double);

    public GlyphScriptValue? AdditionImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for addition");

        return Add(parameters[0], parameters[1]);
    }

    public GlyphScriptValue Add(GlyphScriptValue left, GlyphScriptValue right)
    {
        var promotedLeft = PromoteToDouble(left);
        var promotedRight = PromoteToDouble(right);

        var result = LLVM.BuildFAdd(_llvmBuilder, promotedLeft, promotedRight, "add_double");
        return new(result, GlyphScriptType.Double);
    }

    public GlyphScriptValue? SubtractionImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for subtraction");

        return Subtract(parameters[0], parameters[1]);
    }

    public GlyphScriptValue Subtract(GlyphScriptValue left, GlyphScriptValue right)
    {
        var promotedLeft = PromoteToDouble(left);
        var promotedRight = PromoteToDouble(right);

        var result = LLVM.BuildFSub(_llvmBuilder, promotedLeft, promotedRight, "sub_double");
        return new(result, GlyphScriptType.Double);
    }

    public GlyphScriptValue? MultiplicationImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for multiplication");

        return Multiply(parameters[0], parameters[1]);
    }

    public GlyphScriptValue Multiply(GlyphScriptValue left, GlyphScriptValue right)
    {
        var promotedLeft = PromoteToDouble(left);
        var promotedRight = PromoteToDouble(right);

        var result = LLVM.BuildFMul(_llvmBuilder, promotedLeft, promotedRight, "mul_double");
        return new(result, GlyphScriptType.Double);
    }

    public GlyphScriptValue? DivisionImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for division");

        return Divide(parameters[0], parameters[1]);
    }

    public GlyphScriptValue Divide(GlyphScriptValue left, GlyphScriptValue right)
    {
        var promotedLeft = PromoteToDouble(left);
        var promotedRight = PromoteToDouble(right);

        var result = LLVM.BuildFDiv(_llvmBuilder, promotedLeft, promotedRight, "div_double");
        return new(result, GlyphScriptType.Double);
    }

    public GlyphScriptValue? PowerImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for power");

        return Power(parameters[0], parameters[1]);
    }

    public GlyphScriptValue Power(GlyphScriptValue left, GlyphScriptValue right)
    {
        var promotedLeft = PromoteToDouble(left);
        var promotedRight = PromoteToDouble(right);

        var powFunc = LLVM.GetNamedFunction(_llvmModule, "pow");
        if (powFunc.Pointer == IntPtr.Zero)
        {
            var powType = LLVM.FunctionType(LLVM.DoubleType(), [LLVM.DoubleType(), LLVM.DoubleType()], false);
            powFunc = LLVM.AddFunction(_llvmModule, "pow", powType);
        }

        var powResult = LLVM.BuildCall(_llvmBuilder, powFunc, [promotedLeft, promotedRight], "pow_call");
        return new(powResult, GlyphScriptType.Double);
    }

    public GlyphScriptValue? PrintImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 1)
            throw new InvalidOperationException("Invalid number of parameters for print operation");

        var value = parameters[0];

        if (value.Type != GlyphScriptType.Double)
            throw new InvalidOperationException("Invalid type for print operation");

        // Get printf function
        var printfFunc = LLVM.GetNamedFunction(_llvmModule, "printf");
        if (printfFunc.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("printf function not found");

        // Get format string for double printing
        var formatGlobal = LLVM.GetNamedGlobal(_llvmModule, "strp_double");
        if (formatGlobal.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Format string for double printing not found");

        // Create GEP to get a pointer to the format string
        var formatPtr = LlvmHelper.GetStringPtr(_llvmBuilder, formatGlobal);

        // Call printf with the format string and the double value
        LLVM.BuildCall(_llvmBuilder, printfFunc, [formatPtr, value.Value], string.Empty);

        return value;
    }

    public GlyphScriptValue? ReadImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 1)
            throw new InvalidOperationException("Invalid number of parameters for read operation");

        var variable = parameters[0];

        if (variable.Type != GlyphScriptType.Double)
            throw new InvalidOperationException("Invalid type for read operation");

        // Get scanf function
        var scanfFunc = LLVM.GetNamedFunction(_llvmModule, "scanf");
        if (scanfFunc.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("scanf function not found");

        // Get format string for double reading
        var formatGlobal = LLVM.GetNamedGlobal(_llvmModule, "strs_double");
        if (formatGlobal.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Format string for double reading not found");

        // Create GEP to get a pointer to the format string
        var formatPtr = LlvmHelper.GetStringPtr(_llvmBuilder, formatGlobal);

        // Create a temporary variable to store the read value
        var tempVar = LLVM.BuildAlloca(_llvmBuilder, LLVM.DoubleType(), "temp_double");

        // Call scanf with the format string and the address of the temporary variable
        LLVM.BuildCall(_llvmBuilder, scanfFunc, [formatPtr, tempVar], string.Empty);

        // Load the value from the temporary variable
        var readValue = LLVM.BuildLoad(_llvmBuilder, tempVar, "read_double");

        return new GlyphScriptValue(readValue, GlyphScriptType.Double);
    }

    public GlyphScriptValue? ParseImmediateImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        var immediateValueContext = context as GlyphScriptParser.ImmediateValueContext
            ?? throw new InvalidOperationException("Invalid context for parsing immediate value");
        var rawValue = immediateValueContext.DOUBLE_LITERAL()?.GetText()
            ?? throw new InvalidOperationException("Invalid context for parsing double value");

        var value = double.Parse(rawValue.TrimEnd('d', 'D'));
        return new GlyphScriptValue(LLVM.ConstReal(LLVM.DoubleType(), value), GlyphScriptType.Double);
    }

    private LLVMValueRef PromoteToDouble(GlyphScriptValue value) => value.Type switch
    {
        GlyphScriptType.Double => value.Value,
        GlyphScriptType.Float => LLVM.BuildFPExt(_llvmBuilder, value.Value, LLVM.DoubleType(), "float_to_double"),
        GlyphScriptType.Int or GlyphScriptType.Long => LLVM.BuildSIToFP(_llvmBuilder, value.Value, LLVM.DoubleType(), "int_to_double"),
        _ => throw new InvalidOperationException($"Cannot convert {value.Type} to Double in this operation provider")
    };

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations =>
        new Dictionary<OperationSignature, OperationImplementation>
        {
            { new OperationSignature(DefaultValue, [GlyphScriptType.Double]), DefaultValueImplementation },
            { new OperationSignature(ParseImmediate, [GlyphScriptType.Double]), ParseImmediateImplementation },

            // Double-Double operations
            { new OperationSignature(Addition, [GlyphScriptType.Double, GlyphScriptType.Double]), AdditionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Double, GlyphScriptType.Double]), SubtractionImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Double, GlyphScriptType.Double]), MultiplicationImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Double, GlyphScriptType.Double]), DivisionImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Double, GlyphScriptType.Double]), PowerImplementation },

            // Double-Float operations
            { new OperationSignature(Addition, [GlyphScriptType.Double, GlyphScriptType.Float]), AdditionImplementation },
            { new OperationSignature(Addition, [GlyphScriptType.Float, GlyphScriptType.Double]), AdditionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Double, GlyphScriptType.Float]), SubtractionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Float, GlyphScriptType.Double]), SubtractionImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Double, GlyphScriptType.Float]), MultiplicationImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Float, GlyphScriptType.Double]), MultiplicationImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Double, GlyphScriptType.Float]), DivisionImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Float, GlyphScriptType.Double]), DivisionImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Double, GlyphScriptType.Float]), PowerImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Float, GlyphScriptType.Double]), PowerImplementation },

            // Double-Int operations
            { new OperationSignature(Addition, [GlyphScriptType.Double, GlyphScriptType.Int]), AdditionImplementation },
            { new OperationSignature(Addition, [GlyphScriptType.Int, GlyphScriptType.Double]), AdditionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Double, GlyphScriptType.Int]), SubtractionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Int, GlyphScriptType.Double]), SubtractionImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Double, GlyphScriptType.Int]), MultiplicationImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Int, GlyphScriptType.Double]), MultiplicationImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Double, GlyphScriptType.Int]), DivisionImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Int, GlyphScriptType.Double]), DivisionImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Double, GlyphScriptType.Int]), PowerImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Int, GlyphScriptType.Double]), PowerImplementation },

            // Double-Long operations
            { new OperationSignature(Addition, [GlyphScriptType.Double, GlyphScriptType.Long]), AdditionImplementation },
            { new OperationSignature(Addition, [GlyphScriptType.Long, GlyphScriptType.Double]), AdditionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Double, GlyphScriptType.Long]), SubtractionImplementation },
            { new OperationSignature(Subtraction, [GlyphScriptType.Long, GlyphScriptType.Double]), SubtractionImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Double, GlyphScriptType.Long]), MultiplicationImplementation },
            { new OperationSignature(Multiplication, [GlyphScriptType.Long, GlyphScriptType.Double]), MultiplicationImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Double, GlyphScriptType.Long]), DivisionImplementation },
            { new OperationSignature(Division, [GlyphScriptType.Long, GlyphScriptType.Double]), DivisionImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Double, GlyphScriptType.Long]), PowerImplementation },
            { new OperationSignature(OperationKind.Power, [GlyphScriptType.Long, GlyphScriptType.Double]), PowerImplementation },

            // IO operations
            { new OperationSignature(Print, [GlyphScriptType.Double]), PrintImplementation },
            { new OperationSignature(Read, [GlyphScriptType.Double]), ReadImplementation }
        };
}
