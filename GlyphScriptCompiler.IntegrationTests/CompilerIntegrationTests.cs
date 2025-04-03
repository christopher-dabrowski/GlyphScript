using System.Diagnostics;
using Xunit.Abstractions;

namespace GlyphScriptCompiler.IntegrationTests;

public class CompilerIntegrationTests : IDisposable
{
    private readonly string _testProgramPath;
    private readonly string _outputPath;
    private readonly ITestOutputHelper _output;
    private readonly GlyphScriptLlvmCompiler _compiler;

    public CompilerIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _compiler = new GlyphScriptLlvmCompiler();

        // Find test program path
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        _testProgramPath = Path.Combine(currentDir.FullName, "TestData", "program.gs");
        _outputPath = Path.Combine(Path.GetTempPath(), "test_output.ll");

        if (!File.Exists(_testProgramPath))
        {
            throw new Exception($"Test program not found at: {_testProgramPath}");
        }
    }

    [Fact]
    public async Task CompileAndRunProgram_ShouldProduceExpectedOutput()
    {
        // Act - Compile the program
        _compiler.CompileToFile(_testProgramPath, _outputPath);

        // Act - Run the compiled program
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

        // Create a task to read the output
        var outputTask = lliProcess.StandardOutput.ReadToEndAsync();
        var errorTask = lliProcess.StandardError.ReadToEndAsync();

        // Write input value for the read command
        await lliProcess.StandardInput.WriteLineAsync("5");
        await lliProcess.StandardInput.FlushAsync();
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
            Assert.Fail($"LLI failed with error: {lliError}");
        }

        // Assert
        var expectedOutput = "5\n5\n"; // First write shows 5, read changes it to 5, second write shows 5
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        if (File.Exists(_outputPath))
        {
            File.Delete(_outputPath);
        }
    }
}
