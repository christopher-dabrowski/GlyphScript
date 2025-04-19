using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class BasicNumberTypesTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/BasicNumberTypesExamples";

    private readonly ProgramRunner _runner;

    public BasicNumberTypesTests(ITestOutputHelper output)
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
    public async Task ShouldDeclareAndPrintInt()
    {
        var output = await RunProgram("declareAndPrintInt.gs", "");

        var expectedOutput = "42\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldPrintDouble()
    {
        var output = await RunProgram("declareAndPrintDouble.gs", "");

        var expectedOutput = "3.141593\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
