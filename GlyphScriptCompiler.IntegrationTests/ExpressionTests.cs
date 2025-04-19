using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class ExpressionTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/ExpresionExamples";

    private readonly ProgramRunner _runner;

    public ExpressionTests(ITestOutputHelper output)
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

    [Fact]
    public async Task ShouldPerformIntegerArithmetic()
    {
        var output = await RunProgram("intArithmetic.gs", "");

        // Expected results: sum=15, diff=5, prod=50, div=2
        var expectedOutput = "15\n5\n50\n2\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldPerformDoubleArithmetic()
    {
        var output = await RunProgram("doubleArithmetic.gs", "");

        // Expected results: sum=13.0, diff=8.0, prod=26.25, div=4.2
        var expectedOutput = "13.000000\n8.000000\n26.250000\n4.200000\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleMixedTypeOperations()
    {
        var output = await RunProgram("mixedTypeOperations.gs", "");

        // Expected result: 7.5 (int 5 + double 2.5)
        var expectedOutput = "7.500000\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldCalculateIntegerPower()
    {
        var output = await RunProgram("intPowerOperation.gs", "");

        // 2^3 = 8
        var expectedOutput = "8.000000\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldCalculateDoublePower()
    {
        var output = await RunProgram("doublePowerOperation.gs", "");

        // 2.5^2 = 6.25
        var expectedOutput = "6.250000\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
