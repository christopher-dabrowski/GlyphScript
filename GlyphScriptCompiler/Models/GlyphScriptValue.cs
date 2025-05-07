namespace GlyphScriptCompiler.Models;

public record GlyphScriptValue
(
    LLVMValueRef Value,
    GlyphScriptType Type,
    ArrayTypeInfo? ArrayInfo = null
);
