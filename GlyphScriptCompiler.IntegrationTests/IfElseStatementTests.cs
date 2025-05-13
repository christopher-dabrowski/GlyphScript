using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class IfElseStatementTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/IfElseStatementExamples";

    private readonly ProgramRunner _runner;

    public IfElseStatementTests(ITestOutputHelper output)
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
    public async Task ShouldExecuteIfBranchWhenConditionIsTrue()
    {
        var output = await RunProgram("simpleIfTrue.gs");

        var expectedOutput = "Condition is true\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldSkipIfBranchWhenConditionIsFalse()
    {
        var output = await RunProgram("simpleIfFalse.gs");

        var expectedOutput = "";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldExecuteElseBranchWhenConditionIsFalse()
    {
        var output = await RunProgram("ifElseFalse.gs");

        var expectedOutput = "Condition is false\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldExecuteIfBranchWhenConditionIsTrueWithElse()
    {
        var output = await RunProgram("ifElseTrue.gs");

        var expectedOutput = "Condition is true\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleNestedIfStatements()
    {
        var output = await RunProgram("nestedIf.gs");

        var expectedOutput = "Outer condition is true\nInner condition is true\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleIfElseWithComparison()
    {
        var output = await RunProgram("ifWithComparison.gs");

        var expectedOutput = "5 is greater than 3\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleIfElseWithUserInput()
    {
        var output = await RunProgram("ifWithInput.gs", "10");

        var expectedOutput = "Input is greater than or equal to 5\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleMultipleStatementsInBlock()
    {
        var output = await RunProgram("multiStatementBlock.gs");

        var expectedOutput = "First statement\nSecond statement\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
