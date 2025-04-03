using Xunit.Abstractions;
using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class CompilerIntegrationTests : IDisposable
{
    private readonly string _testProgramPath;
    private readonly ITestOutputHelper _output;
    private readonly ProgramRunner _runner;

    public CompilerIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _runner = new ProgramRunner(output);

        // Find test program path
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        _testProgramPath = Path.Combine(currentDir.FullName, "TestData", "program.gs");

        if (!File.Exists(_testProgramPath))
        {
            throw new Exception($"Test program not found at: {_testProgramPath}");
        }
    }

    [Fact]
    public async Task CompileAndRunProgram_ShouldProduceExpectedOutput()
    {
        var output = await _runner.RunProgramAsync(_testProgramPath, "42");

        const string expectedOutput = "5\n42\n"; // First write shows 5, read changes it to 5, second write shows 5
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
