using Antlr4.Runtime;

namespace GlyphScriptCompiler.SyntaxErrors;

public sealed class AssignmentOfInvalidTypeException(ParserRuleContext context)
    : InvalidSyntaxException(context)
{
    public required string VariableName { get; init; }
    public required GlyphScriptType VariableGlyphScriptType { get; init; }
    public required GlyphScriptType ValueGlyphScriptType { get; init; }

    public override string Reason => $"The variable '{VariableName}' has type '{VariableGlyphScriptType}'" +
                                     $" but a value of type '{ValueGlyphScriptType}' was assigned to it";
}
