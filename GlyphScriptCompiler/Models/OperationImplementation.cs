namespace GlyphScriptCompiler.Models;

public delegate GlyphScriptValue? OperationImplementation(
    RuleContext context,
    IReadOnlyList<GlyphScriptValue> parameters);
