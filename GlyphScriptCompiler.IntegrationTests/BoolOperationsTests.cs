using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class BoolOperationsTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/BoolOperationsExamples";

    private readonly ProgramRunner _runner;

    public BoolOperationsTests(ITestOutputHelper output)
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
    public async Task ShouldDeclareAndPrintTrueBool()
    {
        var output = await RunProgram("declareAndPrintBool.gs");

        var expectedOutput = "true\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldDeclareAndPrintFalseBool()
    {
        var output = await RunProgram("declareAndPrintFalse.gs");

        var expectedOutput = "false\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldReadBoolFromInput()
    {
        const string testInput = "true";
        var output = await RunProgram("declareAndReadBool.gs", testInput);

        var expectedOutput = "true\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldReadFalseBoolFromInput()
    {
        const string testInput = "false";
        var output = await RunProgram("declareAndReadBool.gs", testInput);

        var expectedOutput = "false\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldReassignBoolValue()
    {
        var output = await RunProgram("reassignBool.gs");

        var expectedOutput = "true\nfalse\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldWorkWithTextBooleanLiterals()
    {
        var output = await RunProgram("textBooleanLiterals.gs");

        var expectedOutput = "true\nfalse\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}