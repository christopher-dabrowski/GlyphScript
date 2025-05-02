using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class StringOperationsTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/StringOperationsExamples";

    private readonly ProgramRunner _runner;

    public StringOperationsTests(ITestOutputHelper output)
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
    public async Task ShouldDeclareAndPrintString()
    {
        var output = await RunProgram("declareAndPrintString.gs");

        var expectedOutput = "Hello, World!\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldInitializeString()
    {
        var output = await RunProgram("initializeString.gs");

        var expectedOutput = "GlyphScript is awesome!\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleEmptyString()
    {
        var output = await RunProgram("emptyString.gs");

        var expectedOutput = "\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldReassignString()
    {
        var output = await RunProgram("reassignString.gs");

        var expectedOutput = "First string\nUpdated string\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldJoinStrings()
    {
        var output = await RunProgram("joinStrings.gs");

        var expectedOutput = "Hello, World!\nWelcome to GlyphScript!\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
