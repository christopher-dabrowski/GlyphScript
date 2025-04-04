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

    [Fact]
    public async Task ShouldReadAndPrintInt()
    {
        var output = await RunProgram("readAndPrintInt.gs", "123\n");

        var expectedOutput = "123\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldPrintFloat()
    {
        var output = await RunProgram("printFloat.gs", "");

        var expectedOutput = "3.140000\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldReadAndPrintFloat()
    {
        var output = await RunProgram("readAndPrintFloat.gs", "2.718\n");

        var expectedOutput = "2.718000\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldPrintDouble()
    {
        var output = await RunProgram("printDouble.gs", "");

        var expectedOutput = "3.141593\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
