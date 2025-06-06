namespace GlyphScriptCompiler.SyntaxErrors;

public class InvalidSyntaxException : InvalidOperationException
{
    private InvalidSyntaxException(IToken offendingToken)
    {
        Line = offendingToken.Line;
        Column = offendingToken.Column;
    }

    public InvalidSyntaxException(ParserRuleContext context)
        : this(context.Start)
    {
    }

    public InvalidSyntaxException(ParserRuleContext context, string reason)
        : this(context.Start)
    {
        Reason = reason;
    }

    public InvalidSyntaxException(NoViableAltException noViableAltException)
        : this(noViableAltException.OffendingToken)
    {
    }

    public int Line { get; }
    public int Column { get; }

    public virtual string Reason { get; } = "Syntax error";

    public override string Message => $"{Reason} detected in Line {Line}, Column {Column}";
}
