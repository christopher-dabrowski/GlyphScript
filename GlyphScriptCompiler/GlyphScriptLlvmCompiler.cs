using Antlr4.Runtime;
using GlyphScriptCompiler.Antlr;
using LLVMSharp;

namespace GlyphScriptCompiler;

public sealed class GlyphScriptLlvmCompiler
{
    public LLVMModuleRef Compile(string codeFilePath)
    {
        var codeFile = OpenCodeFile(codeFilePath);

        var lexer = new GlyphScriptLexer(codeFile);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new GlyphScriptParser(tokenStream);

        var context = parser.program();

        var moduleName = Path.GetFileNameWithoutExtension(codeFilePath);
        var module = LLVM.ModuleCreateWithName(moduleName);

        var visitor = new LlvmVisitor(module);
        visitor.Visit(context);

        return module;
    }

    public void CompileToFile(string codeFilePath, string outputFilePath)
    {
        var module = Compile(codeFilePath);

        LLVM.PrintModuleToFile(module, outputFilePath, out var errorMessage);
        if (!string.IsNullOrEmpty(errorMessage))
            throw new InvalidOperationException(errorMessage);
    }

    private ICharStream OpenCodeFile(string? filePath)
    {
        if (filePath is null)
            throw new InvalidOperationException("No code file specified. Pass a file name to run");
        if (!File.Exists(filePath))
            throw new InvalidOperationException($"File {filePath} does not exist.");

        return CharStreams.fromPath(filePath);
    }
}
