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

var outputFilePath = args.ElementAtOrDefault(1);
if (outputFilePath == null)
{
    Console.WriteLine("No output file specified");
    return 1;
}

// var inputStream = new AntlrInputStream(codeFile);
var lexer = new GlyphScriptLexer(codeFile);
var tokenStream = new CommonTokenStream(lexer);
var parser = new GlyphScriptParser(tokenStream);

var context = parser.program();

var moduleName = Path.GetFileNameWithoutExtension(codeFilePath);
var llvmModule = LLVM.ModuleCreateWithName(moduleName);

var visitor = new LlvmVisitor(llvmModule);
visitor.Visit(context);

LLVM.PrintModuleToFile(llvmModule, outputFilePath, out var errorMessage);
if (!string.IsNullOrEmpty(errorMessage))
{
    Console.Error.WriteLine(errorMessage);
}

return 0;

ICharStream? OpenCodeFile(string? filePath)
{
    if (filePath is null)
    {
        Console.Error.WriteLine("No code file specified. Pass a file name to run.");
        return null;
    }
    if (!File.Exists(filePath))
    {
        Console.Error.WriteLine($"File {filePath} does not exist.");
        return null;
    }

    return CharStreams.fromPath(filePath);
}
