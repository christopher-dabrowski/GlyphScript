using Antlr4.Runtime;

namespace GlyphScriptCompiler.SyntaxErrors;

public sealed class AssignemntOfInvalidTypeException(ParserRuleContext context)
    : InvalidSyntaxException(context)
{
    public required string VariableName { get; init; }
    public required TypeKind VariableType { get; init; }
    public required TypeKind ValueType { get; init; }

    public override string Reason => $"The variable '{VariableName}' has type '{VariableType}'" +
                                     $" but a value of type '{ValueType}' was assigned to it";
}
