using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class SimpleIoTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/SimpleIoExemples";

    private readonly ProgramRunner _runner;

    public SimpleIoTests(ITestOutputHelper output)
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
    public async Task ShouldReadAndPrintInt()
    {
        var output = await RunProgram("readAndPrintInt.gs", "123\n");

        var expectedOutput = "123\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldReadAndPrintFloat()
    {
        var output = await RunProgram("readAndPrintFloat.gs", "2.718\n");

        var expectedOutput = "2.718000\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
