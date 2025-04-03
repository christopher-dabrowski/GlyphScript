using GlyphScriptCompiler;

try
{
    var arguments = CliParameters.Parse(args);
    arguments.Validate();

    var compiler = new GlyphScriptLlvmCompiler();
    compiler.CompileToFile(arguments.InputFilePath, arguments.OutputFilePath);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
