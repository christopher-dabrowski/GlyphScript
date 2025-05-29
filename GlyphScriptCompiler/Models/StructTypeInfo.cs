namespace GlyphScriptCompiler.Models;

public record StructTypeInfo(
    string Name,
    (GlyphScriptType Type, string FieldName)[] Fields
);
