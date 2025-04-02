using Antlr4.Runtime;
using GlyphScriptCompiler;
using LLVMSharp;

// var llvmGenerator = new LlvmGenerator();
// llvmGenerator.GenerateSampleProgram();

// Console.WriteLine("The end");

// return 0;

var codeFilePath = args.FirstOrDefault();
var codeFile = OpenCodeFile(codeFilePath);
if (codeFile == null)
    return 1;

var inputStream = new AntlrInputStream(codeFile);
var lexer = new GlyphScriptLexer(inputStream);
var tokenStream = new CommonTokenStream(lexer);
var parser = new GlyphScriptParser(tokenStream);

var context = parser.program();

var moduleName = Path.GetFileNameWithoutExtension(codeFilePath);
var llvmModule = LLVM.ModuleCreateWithName(moduleName);

var visitor = new LlvmVisitor(llvmModule);
visitor.Visit(context);

// TODO: Write module to a file
LLVM.DumpModule(llvmModule);

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
