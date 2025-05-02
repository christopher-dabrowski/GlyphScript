namespace GlyphScriptCompiler;

public interface IOperationProvider
{
    IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations { get; }
}
