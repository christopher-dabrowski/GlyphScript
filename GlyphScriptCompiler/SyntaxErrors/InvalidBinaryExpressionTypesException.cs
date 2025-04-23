using Antlr4.Runtime;

namespace GlyphScriptCompiler.SyntaxErrors;

public class InvalidBinaryExpressionTypesException(ParserRuleContext context) : InvalidSyntaxException(context)
{
    public required GlyphScriptType FirsType { get; init; }
    public required GlyphScriptType SecondType { get; init; }

    public override string Reason => $"Types {FirsType} and {SecondType} are not valid for the {context.GetText()}";
}
