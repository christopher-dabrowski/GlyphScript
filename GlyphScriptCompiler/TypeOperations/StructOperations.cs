using GlyphScriptCompiler.Contracts;
using static GlyphScriptCompiler.Models.OperationKind;

namespace GlyphScriptCompiler.TypeOperations;

public class StructOperations : IOperationProvider
{
    private readonly LLVMModuleRef _module;
    private readonly LLVMBuilderRef _builder;

    public StructOperations(LLVMModuleRef module, LLVMBuilderRef builder)
    {
        _module = module;
        _builder = builder;
    }

    public void Initialize()
    {
        // Initialize any module-level setup for struct operations if needed
    }

    public GlyphScriptValue? DefaultValueImplementation(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        // For structures, default value creation is handled at the instantiation level
        // This method returns null to indicate that default values should be handled
        // by the structure instantiation process
        return null;
    }

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations =>
        new Dictionary<OperationSignature, OperationImplementation>
        {
            { new OperationSignature(DefaultValue, [GlyphScriptType.Struct]), DefaultValueImplementation }
        };
}
