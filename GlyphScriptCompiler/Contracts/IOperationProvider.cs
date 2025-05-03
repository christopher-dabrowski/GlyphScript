namespace GlyphScriptCompiler.Contracts;

public interface IOperationProvider
{
    IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations { get; }
}
