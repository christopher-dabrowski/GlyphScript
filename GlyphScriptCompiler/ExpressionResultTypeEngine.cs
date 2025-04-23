using GlyphScriptCompiler.SyntaxErrors;
using Antlr4.Runtime;

namespace GlyphScriptCompiler;

/// <summary>
/// Predict the resulting type of the given expression based on the input types
/// </summary>
public class ExpressionResultTypeEngine
{
    public GlyphScriptType GetAdditionResultType(ParserRuleContext context, GlyphScriptType a, GlyphScriptType b)
    {
        var types = (a, b);
        return types switch
        {
            (GlyphScriptType.Int, GlyphScriptType.Int) => GlyphScriptType.Int,
            (GlyphScriptType.Int, GlyphScriptType.Long) => GlyphScriptType.Long,
            (GlyphScriptType.Long, GlyphScriptType.Int) => GlyphScriptType.Long,
            (GlyphScriptType.Long, GlyphScriptType.Long) => GlyphScriptType.Long,

            (GlyphScriptType.Float, GlyphScriptType.Float) => GlyphScriptType.Float,
            (GlyphScriptType.Double, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Float, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Double, GlyphScriptType.Float) => GlyphScriptType.Double,

            (GlyphScriptType.Int, GlyphScriptType.Float) => GlyphScriptType.Float,
            (GlyphScriptType.Float, GlyphScriptType.Int) => GlyphScriptType.Float,
            (GlyphScriptType.Int, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Double, GlyphScriptType.Int) => GlyphScriptType.Double,
            (GlyphScriptType.Long, GlyphScriptType.Float) => GlyphScriptType.Float,
            (GlyphScriptType.Float, GlyphScriptType.Long) => GlyphScriptType.Float,
            (GlyphScriptType.Long, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Double, GlyphScriptType.Long) => GlyphScriptType.Double,

            (GlyphScriptType.String, GlyphScriptType.String) => GlyphScriptType.String,

            _ => throw new InvalidBinaryExpressionTypesException(context) { FirsType = a, SecondType = b }
        };
    }

    public GlyphScriptType GetSubtractionResultType(ParserRuleContext context, GlyphScriptType a, GlyphScriptType b)
    {
        var types = (a, b);
        return types switch
        {
            (GlyphScriptType.Int, GlyphScriptType.Int) => GlyphScriptType.Int,
            (GlyphScriptType.Int, GlyphScriptType.Long) => GlyphScriptType.Long,
            (GlyphScriptType.Long, GlyphScriptType.Int) => GlyphScriptType.Long,
            (GlyphScriptType.Long, GlyphScriptType.Long) => GlyphScriptType.Long,

            (GlyphScriptType.Float, GlyphScriptType.Float) => GlyphScriptType.Float,
            (GlyphScriptType.Double, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Float, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Double, GlyphScriptType.Float) => GlyphScriptType.Double,

            (GlyphScriptType.Int, GlyphScriptType.Float) => GlyphScriptType.Float,
            (GlyphScriptType.Float, GlyphScriptType.Int) => GlyphScriptType.Float,
            (GlyphScriptType.Int, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Double, GlyphScriptType.Int) => GlyphScriptType.Double,
            (GlyphScriptType.Long, GlyphScriptType.Float) => GlyphScriptType.Float,
            (GlyphScriptType.Float, GlyphScriptType.Long) => GlyphScriptType.Float,
            (GlyphScriptType.Long, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Double, GlyphScriptType.Long) => GlyphScriptType.Double,

            _ => throw new InvalidBinaryExpressionTypesException(context) { FirsType = a, SecondType = b }
        };
    }

    public GlyphScriptType GetMultiplicationResultType(ParserRuleContext context, GlyphScriptType a, GlyphScriptType b)
    {
        var types = (a, b);
        return types switch
        {
            (GlyphScriptType.Int, GlyphScriptType.Int) => GlyphScriptType.Int,
            (GlyphScriptType.Int, GlyphScriptType.Long) => GlyphScriptType.Long,
            (GlyphScriptType.Long, GlyphScriptType.Int) => GlyphScriptType.Long,
            (GlyphScriptType.Long, GlyphScriptType.Long) => GlyphScriptType.Long,

            (GlyphScriptType.Float, GlyphScriptType.Float) => GlyphScriptType.Float,
            (GlyphScriptType.Double, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Float, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Double, GlyphScriptType.Float) => GlyphScriptType.Double,

            (GlyphScriptType.Int, GlyphScriptType.Float) => GlyphScriptType.Float,
            (GlyphScriptType.Float, GlyphScriptType.Int) => GlyphScriptType.Float,
            (GlyphScriptType.Int, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Double, GlyphScriptType.Int) => GlyphScriptType.Double,
            (GlyphScriptType.Long, GlyphScriptType.Float) => GlyphScriptType.Float,
            (GlyphScriptType.Float, GlyphScriptType.Long) => GlyphScriptType.Float,
            (GlyphScriptType.Long, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Double, GlyphScriptType.Long) => GlyphScriptType.Double,

            _ => throw new InvalidBinaryExpressionTypesException(context) { FirsType = a, SecondType = b }
        };
    }

    public GlyphScriptType GetDivisionResultType(ParserRuleContext context, GlyphScriptType a, GlyphScriptType b)
    {
        var types = (a, b);
        return types switch
        {
            (GlyphScriptType.Int, GlyphScriptType.Int) => GlyphScriptType.Int,
            (GlyphScriptType.Int, GlyphScriptType.Long) => GlyphScriptType.Long,
            (GlyphScriptType.Long, GlyphScriptType.Int) => GlyphScriptType.Long,
            (GlyphScriptType.Long, GlyphScriptType.Long) => GlyphScriptType.Long,

            (GlyphScriptType.Float, GlyphScriptType.Float) => GlyphScriptType.Float,
            (GlyphScriptType.Double, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Float, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Double, GlyphScriptType.Float) => GlyphScriptType.Double,

            (GlyphScriptType.Int, GlyphScriptType.Float) => GlyphScriptType.Float,
            (GlyphScriptType.Float, GlyphScriptType.Int) => GlyphScriptType.Float,
            (GlyphScriptType.Int, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Double, GlyphScriptType.Int) => GlyphScriptType.Double,
            (GlyphScriptType.Long, GlyphScriptType.Float) => GlyphScriptType.Float,
            (GlyphScriptType.Float, GlyphScriptType.Long) => GlyphScriptType.Float,
            (GlyphScriptType.Long, GlyphScriptType.Double) => GlyphScriptType.Double,
            (GlyphScriptType.Double, GlyphScriptType.Long) => GlyphScriptType.Double,

            _ => throw new InvalidBinaryExpressionTypesException(context) { FirsType = a, SecondType = b }
        };
    }

    public GlyphScriptType GetPowerResultType(ParserRuleContext context, GlyphScriptType a, GlyphScriptType b)
    {
        var types = (a, b);
        return types switch
        {
            (GlyphScriptType.Int, _) => GlyphScriptType.Double,
            (GlyphScriptType.Long, _) => GlyphScriptType.Double,
            (GlyphScriptType.Float, _) => GlyphScriptType.Double,
            (GlyphScriptType.Double, _) => GlyphScriptType.Double,

            _ => throw new InvalidBinaryExpressionTypesException(context) { FirsType = a, SecondType = b }
        };
    }

    public bool AreTypesCompatibleForAssignment(GlyphScriptType targetType, GlyphScriptType valueType)
    {
        return (targetType, valueType) switch
        {
            var (t, v) when t == v => true,

            (GlyphScriptType.Long, GlyphScriptType.Int) => true,

            (GlyphScriptType.Double, GlyphScriptType.Float) => true,
            (GlyphScriptType.Float, GlyphScriptType.Int) => true,
            (GlyphScriptType.Double, GlyphScriptType.Int) => true,
            (GlyphScriptType.Float, GlyphScriptType.Long) => true,
            (GlyphScriptType.Double, GlyphScriptType.Long) => true,

            _ => false
        };
    }
}
