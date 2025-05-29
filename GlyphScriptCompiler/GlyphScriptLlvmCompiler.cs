using Antlr4.Runtime.Misc;
using GlyphScriptCompiler.SyntaxErrors;
using Microsoft.Extensions.Logging.Abstractions;

namespace GlyphScriptCompiler;

public sealed class GlyphScriptLlvmCompiler
{
    private readonly ILogger<GlyphScriptLlvmCompiler> _logger;

    public GlyphScriptLlvmCompiler(ILogger<GlyphScriptLlvmCompiler>? logger = null)
    {
        _logger = logger ?? NullLogger<GlyphScriptLlvmCompiler>.Instance;
    }

    public LLVMModuleRef Compile(string codeFilePath)
    {
        _logger.LogDebug("Opening code file: {CodeFilePath}", codeFilePath);
        var codeFile = OpenCodeFile(codeFilePath);

        _logger.LogDebug("Creating lexer");
        var lexer = new GlyphScriptLexer(codeFile);
        var tokenStream = new CommonTokenStream(lexer);
        _logger.LogDebug("Creating parser");
        var parser = new GlyphScriptParser(tokenStream)
        {
            ErrorHandler = new BailErrorStrategy()
        };

        _logger.LogDebug("Parsing program");
        var context = ParseProgram(parser);

        _logger.LogDebug("Creating LLVM module");
        var moduleName = Path.GetFileNameWithoutExtension(codeFilePath);
        var module = LLVM.ModuleCreateWithName(moduleName);

        _logger.LogDebug("Creating visitor");
        var visitor = new LlvmVisitor(module, _logger);
        _logger.LogDebug("Context type: {ContextType}", context.GetType().Name);
        _logger.LogDebug("Calling visitor.Visit(context)");
        visitor.Visit(context);
        _logger.LogDebug("Visitor.Visit completed");

        return visitor.LlvmModule;
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

    private GlyphScriptParser.ProgramContext ParseProgram(GlyphScriptParser parser)
    {
        try
        {
            return parser.program();
        }
        catch (ParseCanceledException e)
        {
            if (e.InnerException is NoViableAltException nve)
                throw new InvalidSyntaxException(nve);
            throw;
        }
    }
}
