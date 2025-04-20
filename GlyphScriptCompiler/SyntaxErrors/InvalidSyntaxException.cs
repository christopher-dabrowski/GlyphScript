using System.Diagnostics.CodeAnalysis;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace GlyphScriptCompiler.SyntaxErrors;

public class InvalidSyntaxException(ParserRuleContext context) : InvalidOperationException
{
    public int Line { get; } = context.Start.Line;
    public int Column { get; } = context.Start.Column;

    public virtual string Reason => "Syntax error";

    public override string Message => $"{Reason} detected in Line {Line}, Column {Column}";
}
