//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     ANTLR Version: 4.13.2
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Generated from GlyphScript.g4 by ANTLR 4.13.2

// Unreachable code detected
#pragma warning disable 0162
// The variable '...' is assigned but its value is never used
#pragma warning disable 0219
// Missing XML comment for publicly visible type or member '...'
#pragma warning disable 1591
// Ambiguous reference in cref attribute
#pragma warning disable 419

namespace GlyphScriptCompiler.Antlr {
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using IToken = Antlr4.Runtime.IToken;

/// <summary>
/// This interface defines a complete generic visitor for a parse tree produced
/// by <see cref="GlyphScriptParser"/>.
/// </summary>
/// <typeparam name="Result">The return type of the visit operation.</typeparam>
[System.CodeDom.Compiler.GeneratedCode("ANTLR", "4.13.2")]
[System.CLSCompliant(false)]
public interface IGlyphScriptVisitor<Result> : IParseTreeVisitor<Result> {
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.program"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitProgram([NotNull] GlyphScriptParser.ProgramContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.statement"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitStatement([NotNull] GlyphScriptParser.StatementContext context);
	/// <summary>
	/// Visit a parse tree produced by the <c>greaterThanExpr</c>
	/// labeled alternative in <see cref="GlyphScriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitGreaterThanExpr([NotNull] GlyphScriptParser.GreaterThanExprContext context);
	/// <summary>
	/// Visit a parse tree produced by the <c>notExpr</c>
	/// labeled alternative in <see cref="GlyphScriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitNotExpr([NotNull] GlyphScriptParser.NotExprContext context);
	/// <summary>
	/// Visit a parse tree produced by the <c>powerExp</c>
	/// labeled alternative in <see cref="GlyphScriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitPowerExp([NotNull] GlyphScriptParser.PowerExpContext context);
	/// <summary>
	/// Visit a parse tree produced by the <c>xorExp</c>
	/// labeled alternative in <see cref="GlyphScriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitXorExp([NotNull] GlyphScriptParser.XorExpContext context);
	/// <summary>
	/// Visit a parse tree produced by the <c>mulDivExp</c>
	/// labeled alternative in <see cref="GlyphScriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitMulDivExp([NotNull] GlyphScriptParser.MulDivExpContext context);
	/// <summary>
	/// Visit a parse tree produced by the <c>lessThanExpr</c>
	/// labeled alternative in <see cref="GlyphScriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitLessThanExpr([NotNull] GlyphScriptParser.LessThanExprContext context);
	/// <summary>
	/// Visit a parse tree produced by the <c>parenthesisExp</c>
	/// labeled alternative in <see cref="GlyphScriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitParenthesisExp([NotNull] GlyphScriptParser.ParenthesisExpContext context);
	/// <summary>
	/// Visit a parse tree produced by the <c>comparisonExpr</c>
	/// labeled alternative in <see cref="GlyphScriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitComparisonExpr([NotNull] GlyphScriptParser.ComparisonExprContext context);
	/// <summary>
	/// Visit a parse tree produced by the <c>idAtomExp</c>
	/// labeled alternative in <see cref="GlyphScriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitIdAtomExp([NotNull] GlyphScriptParser.IdAtomExpContext context);
	/// <summary>
	/// Visit a parse tree produced by the <c>arrayAccessExp</c>
	/// labeled alternative in <see cref="GlyphScriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitArrayAccessExp([NotNull] GlyphScriptParser.ArrayAccessExpContext context);
	/// <summary>
	/// Visit a parse tree produced by the <c>addSubExp</c>
	/// labeled alternative in <see cref="GlyphScriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitAddSubExp([NotNull] GlyphScriptParser.AddSubExpContext context);
	/// <summary>
	/// Visit a parse tree produced by the <c>valueExp</c>
	/// labeled alternative in <see cref="GlyphScriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitValueExp([NotNull] GlyphScriptParser.ValueExpContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.ifStatement"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitIfStatement([NotNull] GlyphScriptParser.IfStatementContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.whileStatement"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitWhileStatement([NotNull] GlyphScriptParser.WhileStatementContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.block"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitBlock([NotNull] GlyphScriptParser.BlockContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.print"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitPrint([NotNull] GlyphScriptParser.PrintContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.read"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitRead([NotNull] GlyphScriptParser.ReadContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.assignment"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitAssignment([NotNull] GlyphScriptParser.AssignmentContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.declaration"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitDeclaration([NotNull] GlyphScriptParser.DeclarationContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.defaultDeclaration"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitDefaultDeclaration([NotNull] GlyphScriptParser.DefaultDeclarationContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.initializingDeclaration"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitInitializingDeclaration([NotNull] GlyphScriptParser.InitializingDeclarationContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.immediateValue"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitImmediateValue([NotNull] GlyphScriptParser.ImmediateValueContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.type"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitType([NotNull] GlyphScriptParser.TypeContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.arrayOfType"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitArrayOfType([NotNull] GlyphScriptParser.ArrayOfTypeContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.arrayLiteral"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitArrayLiteral([NotNull] GlyphScriptParser.ArrayLiteralContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="GlyphScriptParser.expressionList"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitExpressionList([NotNull] GlyphScriptParser.ExpressionListContext context);
}
} // namespace GlyphScriptCompiler.Antlr
