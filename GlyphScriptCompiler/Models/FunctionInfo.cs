namespace GlyphScriptCompiler.Models;

public record FunctionInfo(
    string Name,
    GlyphScriptType ReturnType,
    (GlyphScriptType Type, string Name)[] Parameters,
    LLVMValueRef LlvmFunction
);
