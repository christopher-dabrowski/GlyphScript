namespace GlyphScriptCompiler.Models;

public record ClassTypeInfo(
    string Name,
    (GlyphScriptType Type, string FieldName)[] Fields,
    ClassMethodInfo[] Methods
);

public record ClassMethodInfo(
    string Name,
    GlyphScriptType ReturnType,
    (GlyphScriptType Type, string Name)[] Parameters,
    LLVMValueRef LlvmFunction
);
