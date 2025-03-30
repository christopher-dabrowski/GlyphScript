using Antlr4.Runtime;
using GlyphScriptCompiler;

var codeFilePath = args.FirstOrDefault();
var codeFile = OpenCodeFile(codeFilePath);
if (codeFile == null)
    return 1;

var inputStream = new AntlrInputStream(codeFile);
var lexer = new GlyphScriptLexer(inputStream);
var tokenStream = new CommonTokenStream(lexer);
var parser = new GlyphScriptParser(tokenStream);

var context = parser.program();
var visitor = new LlvmVisitor();
visitor.Visit(context);

return 0;

Stream? OpenCodeFile(string? codeFilePath)
{
    if (codeFilePath is null)
    {
        Console.Error.WriteLine("No code file specified. Pass a file name to run.");
        return null;
    }
    if (!File.Exists(codeFilePath))
    {
        Console.Error.WriteLine($"File {codeFilePath} does not exist.");
        return null;
    }

    return File.OpenRead(codeFilePath);
}
