using Antlr4.Runtime;

namespace GlyphScriptCompiler.SyntaxErrors;

public sealed class DuplicateVariableDeclarationException(ParserRuleContext context)
    : InvalidSyntaxException(context)
{
    public required string VariableName { get; init; }

    public override string Reason => $"The variable '{VariableName}' is already declared in this scope.";
}
