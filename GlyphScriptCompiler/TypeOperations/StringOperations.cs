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
        // Get or declare strcat function
        var strcatFunc = LLVM.GetNamedFunction(_llvmModule, "strcat");
        if (strcatFunc.Pointer == IntPtr.Zero)
        {
            var i8PtrType = LLVM.PointerType(LLVM.Int8Type(), 0);
            var strcatType = LLVM.FunctionType(i8PtrType, [i8PtrType, i8PtrType], false);
            strcatFunc = LLVM.AddFunction(_llvmModule, "strcat", strcatType);
        }

        // Get or declare strlen function
        var strlenFunc = LLVM.GetNamedFunction(_llvmModule, "strlen");
        if (strlenFunc.Pointer == IntPtr.Zero)
        {
            var i8PtrType = LLVM.PointerType(LLVM.Int8Type(), 0);
            var strlenType = LLVM.FunctionType(LLVM.Int64Type(), [i8PtrType], false);
            strlenFunc = LLVM.AddFunction(_llvmModule, "strlen", strlenType);
        }

        // Get or declare strcpy function
        var strcpyFunc = LLVM.GetNamedFunction(_llvmModule, "strcpy");
        if (strcpyFunc.Pointer == IntPtr.Zero)
        {
            var i8PtrType = LLVM.PointerType(LLVM.Int8Type(), 0);
            var strcpyType = LLVM.FunctionType(i8PtrType, [i8PtrType, i8PtrType], false);
            strcpyFunc = LLVM.AddFunction(_llvmModule, "strcpy", strcpyType);
        }

        // Get or declare malloc function
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

        // Copy first string to buffer
        LLVM.BuildCall(_llvmBuilder, strcpyFunc, [buffer, left.Value], "copy_left");

        // Concatenate second string
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

            var stringPtr = CreateStringConstant(_llvmModule, $"str_const_{_stringConstCounter++}", value);
            return new GlyphScriptValue(GetStringPtr(_llvmBuilder, stringPtr), GlyphScriptType.String);
        }

        return null;
    }

    private LLVMValueRef CreateStringConstant(string value)
    {
        var isNullTerminated = value.LastOrDefault() == '\0';
        if (!isNullTerminated)
            value += '\0';

        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var length = (uint)bytes.Length;

        var i8Type = LLVM.Int8Type();
        var arrayType = LLVM.ArrayType(i8Type, length);

        var name = $"str_{_stringConstCounter++}";
        var global = LLVM.AddGlobal(_llvmModule, arrayType, name);
        LLVM.SetLinkage(global, LLVMLinkage.LLVMExternalLinkage);
        LLVM.SetGlobalConstant(global, true);

        var stringConstant = LLVM.ConstString(value, (uint)value.Length, true);
        LLVM.SetInitializer(global, stringConstant);

        return GetStringPtr(global);
    }

    private LLVMValueRef CreateStringConstant(LLVMModuleRef module, string name, string value)
    {
        var isNullTerminated = value.LastOrDefault() == '\0';
        if (!isNullTerminated)
            value += '\0';

        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var length = (uint)bytes.Length;

        var i8Type = LLVM.Int8Type();
        var arrayType = LLVM.ArrayType(i8Type, length);

        var global = LLVM.AddGlobal(module, arrayType, name);
        LLVM.SetLinkage(global, LLVMLinkage.LLVMExternalLinkage);
        LLVM.SetGlobalConstant(global, true);

        var stringConstant = LLVM.ConstString(value, (uint)value.Length, true);
        LLVM.SetInitializer(global, stringConstant);

        return global;
    }

    private LLVMValueRef GetStringPtr(LLVMValueRef stringGlobal)
    {
        LLVMValueRef[] indices =
        [
            LLVM.ConstInt(LLVM.Int32Type(), 0, false),
            LLVM.ConstInt(LLVM.Int32Type(), 0, false)
        ];

        return LLVM.BuildGEP(_llvmBuilder, stringGlobal, indices, string.Empty);
    }

    private LLVMValueRef GetStringPtr(LLVMBuilderRef builder, LLVMValueRef stringGlobal)
    {
        LLVMValueRef[] indices =
        [
            LLVM.ConstInt(LLVM.Int32Type(), 0, false),
            LLVM.ConstInt(LLVM.Int32Type(), 0, false)
        ];

        return LLVM.BuildGEP(builder, stringGlobal, indices, string.Empty);
    }

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations =>
        new Dictionary<OperationSignature, OperationImplementation>
        {
            { new OperationSignature(DefaultValue, [GlyphScriptType.String]), DefaultValueImplementation },
            { new OperationSignature(Addition, [GlyphScriptType.String, GlyphScriptType.String]), AdditionImplementation },
            { new OperationSignature(ParseImmediate, [GlyphScriptType.String]), ParseImmediateImplementation }
        };
}
