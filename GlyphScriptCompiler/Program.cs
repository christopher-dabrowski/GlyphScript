using GlyphScriptCompiler;
using GlyphScriptCompiler.SyntaxErrors;

var arguments = CliParameters.Parse(args);
try
{
    arguments.Validate();

    var compiler = new GlyphScriptLlvmCompiler();
    compiler.CompileToFile(arguments.InputFilePath, arguments.OutputFilePath);
    return 0;
}
catch (InvalidSyntaxException e)
{
    Console.Error.WriteLine($"Unable to compile the {arguments.InputFilePath} file. Syntax error detected:");
    Console.Error.WriteLine(e);
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
