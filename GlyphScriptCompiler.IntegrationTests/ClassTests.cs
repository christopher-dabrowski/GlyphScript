using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class ClassTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/ClassExamples";
    private readonly ProgramRunner _runner;

    public ClassTests(ITestOutputHelper output)
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
    public async Task ClassFieldAssignment_ShouldExecuteCorrectly()
    {
        // This test verifies that class field assignment and access works correctly
        var output = await RunProgram("classFieldAssignment.gs");

        // Should print the values that were assigned
        Assert.Contains("Tesla", output);
        Assert.Contains("2023", output);
    }

    [Fact]
    public async Task ClassBasicExample_ShouldExecuteCorrectly()
    {
        // This test verifies that class methods and field assignments work correctly
        var output = await RunProgram("classBasicExample.gs");

        // Should print name and age values through different access methods
        Assert.Contains("Alice", output);
        Assert.Contains("25", output);
        Assert.Contains("30", output); // After setAge method call
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
