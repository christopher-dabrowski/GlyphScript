using System.CommandLine;

namespace GlyphScriptCompiler;

public record CliParameters
{
    public required string InputFilePath { get; init; }
    public required string OutputFilePath { get; init; }

    public static CliParameters Parse(string[] args)
    {
        var inputFileOption = new Option<FileInfo?>(
            name: "--input",
            description: "Input GlyphScript file path")
        { IsRequired = true};
        inputFileOption.AddAlias("-i");

        var outputFileOption = new Option<FileInfo?>(
            name: "--output",
            description: "Output LLVM IR file path")
        { IsRequired = true };
        outputFileOption.AddAlias("-o");

        var rootCommand = new RootCommand("GlyphScript Compiler")
        {
            inputFileOption,
            outputFileOption
        };

        CliParameters? result = null;
        rootCommand.SetHandler((inputFile, outputFile) =>
        {
            result = new CliParameters
            {
                InputFilePath = inputFile?.FullName ?? throw new ArgumentException("Input file is required"),
                OutputFilePath = outputFile?.FullName ?? throw new ArgumentException("Output file is required")
            };
        }, inputFileOption, outputFileOption);

        rootCommand.Invoke(args);

        return result ?? throw new ArgumentException("Failed to parse arguments");
    }

    public void Validate()
    {
        if (!File.Exists(InputFilePath))
        {
            throw new FileNotFoundException($"Input file not found: {InputFilePath}");
        }

        var outputDirectory = Path.GetDirectoryName(OutputFilePath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
    }
}
