namespace GlyphScriptCompiler.Contracts;

public interface IOperationProvider
{
    void Initialize() {}

    IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations { get; }
}
