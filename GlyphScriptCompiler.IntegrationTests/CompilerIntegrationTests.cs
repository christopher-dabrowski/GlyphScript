using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class CompilerIntegrationTests : IDisposable
{
    private const string TestFilesDirectory = "TestData";

    private readonly string _testProgramPath;
    private readonly ITestOutputHelper _output;
    private readonly ProgramRunner _runner;

    public CompilerIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _runner = new ProgramRunner(output);

        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        _testProgramPath = Path.Combine(currentDir.FullName, TestFilesDirectory, "program.gs");
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

    public void Dispose()
    {
        _runner.Dispose();
    }
}
