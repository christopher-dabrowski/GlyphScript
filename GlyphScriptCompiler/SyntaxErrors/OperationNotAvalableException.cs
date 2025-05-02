using System.Text.Json;
using GlyphScriptCompiler.Models;

namespace GlyphScriptCompiler.SyntaxErrors;

public class OperationNotAvailableException(ParserRuleContext context, OperationSignature Operation)
    : InvalidSyntaxException(context)
{
    public override string Reason =>
        $"The operation {Operation.Kind} " +
        $"is not supported with parameters {JsonSerializer.Serialize(Operation.Parameters)}'";
}
