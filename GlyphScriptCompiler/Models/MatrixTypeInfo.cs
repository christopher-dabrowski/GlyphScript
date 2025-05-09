namespace GlyphScriptCompiler.Models;

public class MatrixTypeInfo
{
    public int RowCount { get; }
    public int ColumnCount { get; }
    public GlyphScriptType ElementType { get; }

    public MatrixTypeInfo(int rowCount, int columnCount, GlyphScriptType elementType)
    {
        RowCount = rowCount;
        ColumnCount = columnCount;
        ElementType = elementType;
    }
}