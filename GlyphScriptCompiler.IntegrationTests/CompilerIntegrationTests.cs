using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace GlyphScriptCompiler.IntegrationTests;

public class CompilerIntegrationTests : IDisposable
{
    private readonly string _solutionRoot;
    private readonly string _testProgramPath;
    private readonly string _outputPath;
    private readonly string _compilerPath;
    private readonly ITestOutputHelper _output;

    public CompilerIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        // Find solution root directory
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "GlyphScript.slnx")))
        {
            currentDir = currentDir.Parent;
        }

        if (currentDir == null)
        {
            throw new Exception("Could not find solution root directory");
        }

        _solutionRoot = currentDir.FullName;
        _testProgramPath = Path.Combine(AppContext.BaseDirectory, "TestData", "program.gs");
        _outputPath = Path.Combine(Path.GetTempPath(), "test_output.ll");

        // Build the compiler project
        var buildProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build {Path.Combine(_solutionRoot, "GlyphScriptCompiler", "GlyphScriptCompiler.csproj")}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = _solutionRoot
            }
        };

        buildProcess.Start();
        var buildOutput = buildProcess.StandardOutput.ReadToEnd();
        var buildError = buildProcess.StandardError.ReadToEnd();
        buildProcess.WaitForExit();

        _output.WriteLine($"Build output: {buildOutput}");
        if (!string.IsNullOrEmpty(buildError))
        {
            _output.WriteLine($"Build error: {buildError}");
        }

        if (buildProcess.ExitCode != 0)
        {
            throw new Exception($"Failed to build the compiler project. Exit code: {buildProcess.ExitCode}");
        }

        _compilerPath = Path.Combine(_solutionRoot, "GlyphScriptCompiler", "bin", "Debug", "net9.0", "GlyphScriptCompiler");
        _output.WriteLine($"Compiler path: {_compilerPath}");

        if (!File.Exists(_compilerPath))
        {
            throw new Exception($"Compiler executable not found at: {_compilerPath}");
        }

        if (!File.Exists(_testProgramPath))
        {
            throw new Exception($"Test program not found at: {_testProgramPath}");
        }
    }

    [Fact]
    public async Task CompileAndRunProgram_ShouldProduceExpectedOutput()
    {
        // Arrange
        var compilerProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _compilerPath,
                Arguments = $"{_testProgramPath} {_outputPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = _solutionRoot
            }
        };

        // Act - Compile the program
        compilerProcess.Start();
        var compilerOutput = await compilerProcess.StandardOutput.ReadToEndAsync();
        var compilerError = await compilerProcess.StandardError.ReadToEndAsync();
        await compilerProcess.WaitForExitAsync();

        _output.WriteLine($"Compiler output: {compilerOutput}");
        if (!string.IsNullOrEmpty(compilerError))
        {
            _output.WriteLine($"Compiler error: {compilerError}");
        }

        if (compilerProcess.ExitCode != 0)
        {
            Assert.Fail($"Compiler failed with error: {compilerError}");
        }

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
                UseShellExecute = false,
                WorkingDirectory = _solutionRoot
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
