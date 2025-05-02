namespace GlyphScriptCompiler;

public delegate GlyphScriptValue? OperationImplementation(
    RuleContext context,
    IReadOnlyList<GlyphScriptValue> parameters);
