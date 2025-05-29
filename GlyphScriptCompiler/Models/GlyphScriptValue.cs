namespace GlyphScriptCompiler.Models;

public record GlyphScriptValue
(
    LLVMValueRef Value,
    GlyphScriptType Type,
    ArrayTypeInfo? ArrayInfo = null,
    StructTypeInfo? StructInfo = null,
    ClassTypeInfo? ClassInfo = null
);
