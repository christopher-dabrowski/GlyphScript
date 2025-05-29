using GlyphScriptCompiler;
using GlyphScriptCompiler.SyntaxErrors;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole()
           .SetMinimumLevel(LogLevel.Debug));

var logger = loggerFactory.CreateLogger<Program>();

logger.LogDebug("Program starting");
var arguments = CliParameters.Parse(args);
logger.LogDebug("Arguments parsed - Input: {InputFilePath}, Output: {OutputFilePath}", arguments.InputFilePath, arguments.OutputFilePath);
try
{
    logger.LogDebug("Validating arguments");
    arguments.Validate();

    logger.LogDebug("Creating compiler");
    var compilerLogger = loggerFactory.CreateLogger<GlyphScriptLlvmCompiler>();
    var compiler = new GlyphScriptLlvmCompiler(compilerLogger);
    logger.LogDebug("Starting compilation");
    compiler.CompileToFile(arguments.InputFilePath, arguments.OutputFilePath);
    logger.LogInformation("Compilation completed successfully");
    return 0;
}
catch (InvalidSyntaxException e)
{
    logger.LogError("InvalidSyntaxException caught: {Message}", e.Message);
    Console.Error.WriteLine($"Unable to compile the {arguments.InputFilePath} file. Syntax error detected:");
    Console.Error.WriteLine(e.Message);
    return 1;
}
catch (Exception ex)
{
    logger.LogError(ex, "General exception caught: {Message}", ex.Message);
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
    return 1;
}
