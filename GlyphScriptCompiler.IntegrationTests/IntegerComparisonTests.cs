using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class IntegerComparisonTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/IntegerComparisonExamples";

    private readonly ProgramRunner _runner;

    public IntegerComparisonTests(ITestOutputHelper output)
    {
        _runner = new ProgramRunner(output);
    }

    private async Task<string> RunProgram(string program, string input = "")
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        var programPath = Path.Combine(currentDir.FullName, TestFilesDirectory, program);

        var output = await _runner.RunProgramAsync(programPath, input);
        return output;
    }

    [Fact]
    public async Task ShouldPerformIntegerEquality()
    {
        var output = await RunProgram("integerEquality.gs");

        var expectedOutput = "true\nfalse\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldPerformIntegerLessThan()
    {
        var output = await RunProgram("integerLessThan.gs");

        var expectedOutput = "true\nfalse\nfalse\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldPerformIntegerGreaterThan()
    {
        var output = await RunProgram("integerGreaterThan.gs");

        var expectedOutput = "true\nfalse\nfalse\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldCompareWithVariables()
    {
        var output = await RunProgram("compareWithVariables.gs");

        var expectedOutput = "true\ntrue\nfalse\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldCompareWithUserInput()
    {
        var output = await RunProgram("compareWithInput.gs", "42");

        var expectedOutput = "true\nfalse\nfalse\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldUseComparisonInVariableAssignment()
    {
        var output = await RunProgram("comparisonInAssignment.gs");

        var expectedOutput = "true\ntrue\nfalse\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
