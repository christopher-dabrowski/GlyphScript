using GlyphScriptCompiler.Contracts;
using static GlyphScriptCompiler.Models.OperationKind;

namespace GlyphScriptCompiler.TypeOperations;

public class BoolOperations : IOperationProvider
{
    private readonly LLVMModuleRef _llvmModule;
    private readonly LLVMBuilderRef _llvmBuilder;

    public BoolOperations(LLVMModuleRef llvmModule, LLVMBuilderRef llvmBuilder)
    {
        _llvmModule = llvmModule;
        _llvmBuilder = llvmBuilder;
    }

    public void Initialize()
    {
        LlvmHelper.CreateStringConstant(_llvmModule, "strp_bool_true", "true\n\0");
        LlvmHelper.CreateStringConstant(_llvmModule, "strp_bool_false", "false\n\0");
        LlvmHelper.CreateStringConstant(_llvmModule, "strs_bool", "%s\0");
    }

    public GlyphScriptValue? DefaultValueImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters) =>
        GetDefaultValue();

    public GlyphScriptValue GetDefaultValue() =>
        new GlyphScriptValue(LLVM.ConstInt(LLVM.Int1Type(), 0, false), GlyphScriptType.Boolean);

    public GlyphScriptValue? PrintImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 1)
            throw new InvalidOperationException("Invalid number of parameters for print operation");

        var value = parameters[0];

        if (value.Type != GlyphScriptType.Boolean)
            throw new InvalidOperationException("Invalid type for print operation");

        var printfFunc = LLVM.GetNamedFunction(_llvmModule, "printf");
        if (printfFunc.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("printf function not found");

        var currentBlock = LLVM.GetInsertBlock(_llvmBuilder);
        var function = LLVM.GetBasicBlockParent(currentBlock);

        var trueBlock = LLVM.AppendBasicBlock(function, "print_true");
        var falseBlock = LLVM.AppendBasicBlock(function, "print_false");
        var mergeBlock = LLVM.AppendBasicBlock(function, "print_merge");

        LLVM.BuildCondBr(_llvmBuilder, value.Value, trueBlock, falseBlock);

        LLVM.PositionBuilderAtEnd(_llvmBuilder, trueBlock);
        var trueFormatGlobal = LLVM.GetNamedGlobal(_llvmModule, "strp_bool_true");
        if (trueFormatGlobal.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Format string for true printing not found");
        var trueFormatPtr = LlvmHelper.GetStringPtr(_llvmBuilder, trueFormatGlobal);
        LLVM.BuildCall(_llvmBuilder, printfFunc, [trueFormatPtr], string.Empty);
        LLVM.BuildBr(_llvmBuilder, mergeBlock);

        LLVM.PositionBuilderAtEnd(_llvmBuilder, falseBlock);
        var falseFormatGlobal = LLVM.GetNamedGlobal(_llvmModule, "strp_bool_false");
        if (falseFormatGlobal.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Format string for false printing not found");
        var falseFormatPtr = LlvmHelper.GetStringPtr(_llvmBuilder, falseFormatGlobal);
        LLVM.BuildCall(_llvmBuilder, printfFunc, [falseFormatPtr], string.Empty);
        LLVM.BuildBr(_llvmBuilder, mergeBlock);

        LLVM.PositionBuilderAtEnd(_llvmBuilder, mergeBlock);

        return value;
    }

    public GlyphScriptValue? ReadImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 1)
            throw new InvalidOperationException("Invalid number of parameters for read operation");

        var variable = parameters[0];

        if (variable.Type != GlyphScriptType.Boolean)
            throw new InvalidOperationException("Invalid type for read operation");

        var scanfFunc = LLVM.GetNamedFunction(_llvmModule, "scanf");
        if (scanfFunc.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("scanf function not found");

        var formatGlobal = LLVM.GetNamedGlobal(_llvmModule, "strs_bool");
        if (formatGlobal.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Format string for boolean reading not found");

        var formatPtr = LlvmHelper.GetStringPtr(_llvmBuilder, formatGlobal);

        var bufferSize = LLVM.ConstInt(LLVM.Int32Type(), 10, false); // Big enough for "true" or "false"
        var buffer = LLVM.BuildArrayAlloca(_llvmBuilder, LLVM.Int8Type(), bufferSize, "bool_buffer");

        LLVM.BuildCall(_llvmBuilder, scanfFunc, [formatPtr, buffer], string.Empty);

        var trueString = LLVM.BuildGlobalStringPtr(_llvmBuilder, "true", "true_const");
        var strcmpFunc = LLVM.GetNamedFunction(_llvmModule, "strcmp");
        if (strcmpFunc.Pointer == IntPtr.Zero)
        {
            var strcmpType = LLVM.FunctionType(LLVM.Int32Type(), [LLVM.PointerType(LLVM.Int8Type(), 0), LLVM.PointerType(LLVM.Int8Type(), 0)], false);
            strcmpFunc = LLVM.AddFunction(_llvmModule, "strcmp", strcmpType);
        }

        var comparisonResult = LLVM.BuildCall(_llvmBuilder, strcmpFunc, [buffer, trueString], "strcmp_result");

        var isTrue = LLVM.BuildICmp(_llvmBuilder, LLVMIntPredicate.LLVMIntEQ, comparisonResult, LLVM.ConstInt(LLVM.Int32Type(), 0, false), "is_true");

        return new GlyphScriptValue(isTrue, GlyphScriptType.Boolean);
    }

    public GlyphScriptValue? ParseImmediateImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        var immediateValueContext = context as GlyphScriptParser.ImmediateValueContext
            ?? throw new InvalidOperationException("Invalid context for parsing immediate value");

        if (immediateValueContext.TRUE_LITERAL() != null)
        {
            return new GlyphScriptValue(LLVM.ConstInt(LLVM.Int1Type(), 1, false), GlyphScriptType.Boolean);
        }
        else if (immediateValueContext.FALSE_LITERAL() != null)
        {
            return new GlyphScriptValue(LLVM.ConstInt(LLVM.Int1Type(), 0, false), GlyphScriptType.Boolean);
        }

        throw new InvalidOperationException("Invalid context for parsing boolean value");
    }

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations =>
        new Dictionary<OperationSignature, OperationImplementation>()
        {
            { new OperationSignature(DefaultValue, [GlyphScriptType.Boolean]), DefaultValueImplementation },
            { new OperationSignature(ParseImmediate, [GlyphScriptType.Boolean]), ParseImmediateImplementation },
            { new OperationSignature(Print, [GlyphScriptType.Boolean]), PrintImplementation },
            { new OperationSignature(Read, [GlyphScriptType.Boolean]), ReadImplementation }
        };
}
