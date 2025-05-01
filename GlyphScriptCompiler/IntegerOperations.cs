using LLVMSharp;
using static GlyphScriptCompiler.OperationKind;

namespace GlyphScriptCompiler;

public class IntegerOperations : IOperationProvider
{
    private readonly LLVMModuleRef _llvmModule;
    private readonly LLVMBuilderRef _llvmBuilder;

    public IntegerOperations(LLVMModuleRef llvmModule, LLVMBuilderRef llvmBuilder)
    {
        _llvmModule = llvmModule;
        _llvmBuilder = llvmBuilder;
    }

    public GlyphScriptValue? DefaultValueImplementation(IReadOnlyList<GlyphScriptValue> parameters) =>
        GetDefaultValue();

    public GlyphScriptValue GetDefaultValue() =>
        new GlyphScriptValue(LLVM.ConstInt(LLVM.Int32Type(), 0, false), GlyphScriptType.Int);

    public GlyphScriptValue? AdditionImplementation(IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 2)
            throw new InvalidOperationException("Invalid number of parameters for addition");

        var left = parameters[0];
        var right = parameters[1];

        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for addition");

        return Add(left, right);
    }

    public GlyphScriptValue Add(GlyphScriptValue left, GlyphScriptValue right)
    {
        if (left.Type != GlyphScriptType.Int || right.Type != GlyphScriptType.Int)
            throw new InvalidOperationException("Invalid types for addition");

        var result = LLVM.BuildAdd(_llvmBuilder, left.Value, right.Value, "add");
        return new GlyphScriptValue(result, GlyphScriptType.Int);
    }

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations =>
        new Dictionary<OperationSignature, OperationImplementation>()
        {
            { new OperationSignature(DefaultValue, Array.Empty<GlyphScriptType>()), DefaultValueImplementation },
            { new OperationSignature(Addition, [GlyphScriptType.Int, GlyphScriptType.Int]), AdditionImplementation}
        };
}
