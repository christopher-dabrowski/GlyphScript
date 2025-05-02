using LLVMSharp;

namespace GlyphScriptCompiler;

public record GlyphScriptValue
(
    LLVMValueRef Value,
    GlyphScriptType Type
);
