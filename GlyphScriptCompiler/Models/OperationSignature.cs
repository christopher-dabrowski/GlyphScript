namespace GlyphScriptCompiler.Models;

public record OperationSignature(
    OperationKind Kind,
    IReadOnlyList<GlyphScriptType> Parameters)
{
    public virtual bool Equals(OperationSignature? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Kind == other.Kind && Parameters.SequenceEqual(other.Parameters);
    }

    public override int GetHashCode()
    {
        var parametersHash = Parameters.Aggregate(0, HashCode.Combine);
        return HashCode.Combine((int)Kind, parametersHash);
    }
}
