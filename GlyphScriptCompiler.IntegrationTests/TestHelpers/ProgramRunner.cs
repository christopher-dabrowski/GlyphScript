using System.Diagnostics;

namespace GlyphScriptCompiler.IntegrationTests.TestHelpers;

public sealed class ProgramRunner : IDisposable
{
    private readonly string _outputPath;
    private readonly ITestOutputHelper _output;
    private readonly GlyphScriptLlvmCompiler _compiler;

    public ProgramRunner(ITestOutputHelper output)
    {
        _output = output;
        var logger = NullLogger<GlyphScriptLlvmCompiler>.Instance;
        _compiler = new GlyphScriptLlvmCompiler(logger);
        _outputPath = Path.Combine(Path.GetTempPath(), $"test_output_{Guid.NewGuid()}.ll");
    }

    public async Task<string> RunProgramAsync(string programPath, string? standardInput = null)
    {
        // Compile the program
        _compiler.CompileToFile(programPath, _outputPath);

        // Run the compiled program
        var lliProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "lli",
                Arguments = _outputPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        lliProcess.Start();

        // Create tasks to read the output
        var outputTask = lliProcess.StandardOutput.ReadToEndAsync();
        var errorTask = lliProcess.StandardError.ReadToEndAsync();

        // Write input if provided
        if (!string.IsNullOrEmpty(standardInput))
        {
            await lliProcess.StandardInput.WriteLineAsync(standardInput);
            await lliProcess.StandardInput.FlushAsync();
        }
        lliProcess.StandardInput.Close();

        // Wait for the process to complete
        var output = await outputTask;
        var lliError = await errorTask;
        await lliProcess.WaitForExitAsync();

        if (!string.IsNullOrEmpty(lliError))
        {
            _output.WriteLine($"LLI error: {lliError}");
        }

        if (lliProcess.ExitCode != 0)
        {
            throw new Exception($"LLI failed with error: {lliError}");
        }

        return output;
    }

    public void Dispose()
    {
        if (File.Exists(_outputPath))
        {
            File.Delete(_outputPath);
        }
    }
}
