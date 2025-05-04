using GlyphScriptCompiler.Contracts;
using static GlyphScriptCompiler.Models.OperationKind;

namespace GlyphScriptCompiler.TypeOperations;

public class StringOperations : IOperationProvider
{
    private readonly LLVMModuleRef _llvmModule;
    private readonly LLVMBuilderRef _llvmBuilder;
    private int _stringConstCounter = 0;

    public StringOperations(LLVMModuleRef llvmModule, LLVMBuilderRef llvmBuilder)
    {
        _llvmModule = llvmModule;
        _llvmBuilder = llvmBuilder;
    }

    public void Initialize()
    {
        LlvmHelper.CreateStringConstant(_llvmModule, "strp_string", "%s\n\0");
        LlvmHelper.CreateStringConstant(_llvmModule, "strs_string", "%s\0");
        LlvmHelper.CreateStringConstant(_llvmModule, "strs_line", "%[^\n]\0");
    }

    public GlyphScriptValue? DefaultValueImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters) =>
        GetDefaultValue();

    public GlyphScriptValue GetDefaultValue() =>
        new(LLVM.ConstPointerNull(LLVM.PointerType(LLVM.Int8Type(), 0)), GlyphScriptType.String);

    public GlyphScriptValue? AdditionImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for string concatenation");

        var left = parameters[0];
        var right = parameters[1];

        return Concatenate(left, right);
    }

    public GlyphScriptValue Concatenate(GlyphScriptValue left, GlyphScriptValue right)
    {
        var strcatFunc = LLVM.GetNamedFunction(_llvmModule, "strcat");
        if (strcatFunc.Pointer == IntPtr.Zero)
        {
            var i8PtrType = LLVM.PointerType(LLVM.Int8Type(), 0);
            var strcatType = LLVM.FunctionType(i8PtrType, [i8PtrType, i8PtrType], false);
            strcatFunc = LLVM.AddFunction(_llvmModule, "strcat", strcatType);
        }

        var strlenFunc = LLVM.GetNamedFunction(_llvmModule, "strlen");
        if (strlenFunc.Pointer == IntPtr.Zero)
        {
            var i8PtrType = LLVM.PointerType(LLVM.Int8Type(), 0);
            var strlenType = LLVM.FunctionType(LLVM.Int64Type(), [i8PtrType], false);
            strlenFunc = LLVM.AddFunction(_llvmModule, "strlen", strlenType);
        }

        var strcpyFunc = LLVM.GetNamedFunction(_llvmModule, "strcpy");
        if (strcpyFunc.Pointer == IntPtr.Zero)
        {
            var i8PtrType = LLVM.PointerType(LLVM.Int8Type(), 0);
            var strcpyType = LLVM.FunctionType(i8PtrType, [i8PtrType, i8PtrType], false);
            strcpyFunc = LLVM.AddFunction(_llvmModule, "strcpy", strcpyType);
        }

        var mallocFunc = LLVM.GetNamedFunction(_llvmModule, "malloc");
        if (mallocFunc.Pointer == IntPtr.Zero)
        {
            var mallocType = LLVM.FunctionType(LLVM.PointerType(LLVM.Int8Type(), 0), [LLVM.Int64Type()], false);
            mallocFunc = LLVM.AddFunction(_llvmModule, "malloc", mallocType);
        }

        // Calculate required buffer size
        var leftLength = LLVM.BuildCall(_llvmBuilder, strlenFunc, [left.Value], "left_len");
        var rightLength = LLVM.BuildCall(_llvmBuilder, strlenFunc, [right.Value], "right_len");

        // Add 1 for null terminator
        var totalLength = LLVM.BuildAdd(_llvmBuilder, leftLength, rightLength, "total_len");
        var bufferSize = LLVM.BuildAdd(_llvmBuilder, totalLength, LLVM.ConstInt(LLVM.Int64Type(), 1, false), "buffer_size");

        // Allocate buffer for the concatenated string
        var buffer = LLVM.BuildCall(_llvmBuilder, mallocFunc, [bufferSize], "concat_buffer");

        LLVM.BuildCall(_llvmBuilder, strcpyFunc, [buffer, left.Value], "copy_left");

        var result = LLVM.BuildCall(_llvmBuilder, strcatFunc, [buffer, right.Value], "concat_result");

        return new GlyphScriptValue(result, GlyphScriptType.String);
    }

    public GlyphScriptValue? ParseImmediateImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        var immediateValueContext = context as GlyphScriptParser.ImmediateValueContext
            ?? throw new InvalidOperationException("Invalid context for parsing immediate value");
        var rawValue = immediateValueContext.STRING_LITERAL()?.GetText()
            ?? throw new InvalidOperationException("Invalid context for parsing string value");

        // Remove quotes and process escape sequences
        if (rawValue.Length >= 2 && rawValue.StartsWith("\"") && rawValue.EndsWith("\""))
        {
            var value = rawValue.Substring(1, rawValue.Length - 2);
            value = System.Text.RegularExpressions.Regex.Unescape(value);

            var stringPtr = LlvmHelper.CreateStringConstant(_llvmModule, $"str_const_{_stringConstCounter++}", value);
            return new GlyphScriptValue(LlvmHelper.GetStringPtr(_llvmBuilder, stringPtr), GlyphScriptType.String);
        }

        return null;
    }

    public GlyphScriptValue? PrintImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 1)
            throw new InvalidOperationException("Invalid number of parameters for print operation");

        var value = parameters[0];

        if (value.Type != GlyphScriptType.String)
            throw new InvalidOperationException("Invalid type for print operation");

        var printfFunc = LLVM.GetNamedFunction(_llvmModule, "printf");
        if (printfFunc.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("printf function not found");

        var formatGlobal = LLVM.GetNamedGlobal(_llvmModule, "strp_string");
        if (formatGlobal.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Format string for string printing not found");

        var formatPtr = LlvmHelper.GetStringPtr(_llvmBuilder, formatGlobal);

        LLVM.BuildCall(_llvmBuilder, printfFunc, [formatPtr, value.Value], string.Empty);

        return value;
    }

    public GlyphScriptValue? ReadImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 1)
            throw new InvalidOperationException("Invalid number of parameters for read operation");

        var variable = parameters[0];

        if (variable.Type != GlyphScriptType.String)
            throw new InvalidOperationException("Invalid type for read operation");

        var scanfFunc = LLVM.GetNamedFunction(_llvmModule, "scanf");
        if (scanfFunc.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("scanf function not found");

        var mallocFunc = LLVM.GetNamedFunction(_llvmModule, "malloc");
        if (mallocFunc.Pointer == IntPtr.Zero)
        {
            var mallocType = LLVM.FunctionType(LLVM.PointerType(LLVM.Int8Type(), 0), [LLVM.Int64Type()], false);
            mallocFunc = LLVM.AddFunction(_llvmModule, "malloc", mallocType);
        }

        var formatGlobal = LLVM.GetNamedGlobal(_llvmModule, "strs_line");
        if (formatGlobal.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Format string for string reading not found");

        var formatPtr = LlvmHelper.GetStringPtr(_llvmBuilder, formatGlobal);

        var bufferSize = LLVM.ConstInt(LLVM.Int64Type(), 1024, false);
        var buffer = LLVM.BuildCall(_llvmBuilder, mallocFunc, [bufferSize], "string_buffer");

        LLVM.BuildCall(_llvmBuilder, scanfFunc, [formatPtr, buffer], string.Empty);

        return new GlyphScriptValue(buffer, GlyphScriptType.String);
    }

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations =>
        new Dictionary<OperationSignature, OperationImplementation>
        {
            { new OperationSignature(DefaultValue, [GlyphScriptType.String]), DefaultValueImplementation },
            { new OperationSignature(Addition, [GlyphScriptType.String, GlyphScriptType.String]), AdditionImplementation },
            { new OperationSignature(ParseImmediate, [GlyphScriptType.String]), ParseImmediateImplementation },
            { new OperationSignature(Print, [GlyphScriptType.String]), PrintImplementation },
            { new OperationSignature(Read, [GlyphScriptType.String]), ReadImplementation }
        };
}
