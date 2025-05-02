namespace GlyphScriptCompiler.SyntaxErrors;

public sealed class UndefinedVariableUsageException(ParserRuleContext context)
    : InvalidSyntaxException(context)
{
    public required string VariableName { get; init; }

    public override string Reason => $"Usage of undefined variable '{VariableName}'";
}
