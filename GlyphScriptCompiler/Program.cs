using Antlr4.Runtime;
using GlyphScriptCompiler;

var input = "";

var inputStream = new AntlrInputStream(input.ToString());
var lexer = new GlyphScriptLexer(inputStream);
var tokenStream = new CommonTokenStream(lexer);
var parser = new GlyphScriptParser(tokenStream);

var context = parser.program();
var visitor = new LlvmVisitor();
visitor.Visit(context);


