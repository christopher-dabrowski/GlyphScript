using Antlr4.Runtime;

namespace GlyphScriptCompiler.SyntaxErrors;

public class InvalidBinaryExpressionTypesException(ParserRuleContext context) : InvalidSyntaxException(context)
{
    private readonly ParserRuleContext _context = context;
    public required GlyphScriptType FirsType { get; init; }
    public required GlyphScriptType SecondType { get; init; }

    public override string Reason => $"Types {FirsType} and {SecondType} are not valid for the {_context.GetText()}";
}
