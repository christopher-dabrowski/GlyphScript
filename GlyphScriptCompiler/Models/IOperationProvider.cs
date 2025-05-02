namespace GlyphScriptCompiler.Models;

public interface IOperationProvider
{
    IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations { get; }
}
