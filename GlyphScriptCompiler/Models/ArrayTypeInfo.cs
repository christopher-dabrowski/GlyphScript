namespace GlyphScriptCompiler.Models;

public class ArrayTypeInfo
{
    public GlyphScriptType ElementType { get; }

    public ArrayTypeInfo(GlyphScriptType elementType)
    {
        ElementType = elementType;
    }
}
