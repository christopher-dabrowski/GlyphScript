using GlyphScriptCompiler;
using LLVMSharp;

try
{
    var arguments = CliParameters.Parse(args);
    arguments.Validate();

    var compiler = new GlyphScriptLlvmCompiler();
    var llvmModule = compiler.Compile(arguments.InputFilePath);

    LLVM.PrintModuleToFile(llvmModule, arguments.OutputFilePath, out var errorMessage);
    if (!string.IsNullOrEmpty(errorMessage))
        throw new InvalidOperationException(errorMessage);

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
