using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class DifferentPrecisionOperationsTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/DifferentPrecisionExamples";

    private readonly ProgramRunner _runner;

    public DifferentPrecisionOperationsTests(ITestOutputHelper output)
    {
        _runner = new ProgramRunner(output);
    }

    private async Task<string> RunProgram(string program, string input)
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        var programPath = Path.Combine(currentDir.FullName, TestFilesDirectory, program);

        var output = await _runner.RunProgramAsync(programPath, input);
        return output;
    }

    // Int Tests
    [Fact]
    public async Task ShouldDeclareAndReadInt()
    {
        var output = await RunProgram("declareAndReadInt.gs", "42\n");

        var expectedOutput = "42\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldInitializeInt()
    {
        var output = await RunProgram("initializeInt.gs", "");

        var expectedOutput = "100\n";
        Assert.Equal(expectedOutput, output);
    }

    // Long Tests
    [Fact]
    public async Task ShouldDeclareAndReadLong()
    {
        var output = await RunProgram("declareAndReadLong.gs", "9223372036854775807\n");

        var expectedOutput = "9223372036854775807\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldInitializeLong()
    {
        var output = await RunProgram("initializeLong.gs", "");

        var expectedOutput = "9223372036854775800\n";
        Assert.Equal(expectedOutput, output);
    }

    // Float Tests
    [Fact]
    public async Task ShouldDeclareAndReadFloat()
    {
        var output = await RunProgram("declareAndReadFloat.gs", "3.14\n");

        var expectedOutput = "3.140000\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldInitializeFloat()
    {
        var output = await RunProgram("initializeFloat.gs", "");

        var expectedOutput = "3.141500\n";
        Assert.Equal(expectedOutput, output);
    }

    // Double Tests
    [Fact]
    public async Task ShouldDeclareAndReadDouble()
    {
        var output = await RunProgram("declareAndReadDouble.gs", "3.14159265359\n");

        var expectedOutput = "3.141593\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldInitializeDouble()
    {
        var output = await RunProgram("initializeDouble.gs", "");

        var expectedOutput = "2.718282\n";
        Assert.Equal(expectedOutput, output);
    }

    // Mixed Operations Tests
    [Fact]
    public async Task ShouldPerformMixedOperationsWithInt()
    {
        var output = await RunProgram("mixedOperationsInt.gs", "10\n");

        var expectedOutput = "20\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldPerformMixedOperationsWithLong()
    {
        var output = await RunProgram("mixedOperationsLong.gs", "100\n");

        var expectedOutput = "200\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldPerformMixedOperationsWithFloat()
    {
        var output = await RunProgram("mixedOperationsFloat.gs", "2.5\n");

        var expectedOutput = "7.500000\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldPerformMixedOperationsWithDouble()
    {
        var output = await RunProgram("mixedOperationsDouble.gs", "3.14\n");

        var expectedOutput = "9.420000\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
