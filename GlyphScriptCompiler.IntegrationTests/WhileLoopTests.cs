using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class WhileLoopTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/WhileLoopExamples";

    private readonly ProgramRunner _runner;

    public WhileLoopTests(ITestOutputHelper output)
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
    public async Task ShouldExecuteBasicWhileLoop()
    {
        var output = await RunProgram("basicWhileLoop.gs");

        var expectedOutput = "0\n1\n2\n3\n4\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldExecuteNestedWhileLoops()
    {
        var output = await RunProgram("nestedWhileLoops.gs");

        var expectedOutput = "0,0\n0,1\n0,2\n1,0\n1,1\n1,2\n2,0\n2,1\n2,2\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldSkipWhileLoopWhenConditionIsFalse()
    {
        var output = await RunProgram("skipWhileLoop.gs");

        var expectedOutput = "Loop skipped\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldBreakFromWhileLoopWithCondition()
    {
        var output = await RunProgram("conditionalBreakWhileLoop.gs");

        var expectedOutput = "0\n1\n2\nBroke out of loop\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldUpdateVariablesInWhileLoop()
    {
        var output = await RunProgram("updateVariablesWhileLoop.gs");

        var expectedOutput = "Sum: 55\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleWhileLoopWithUserInput()
    {
        var output = await RunProgram("whileLoopWithInput.gs", "5");

        var expectedOutput = "Enter a number: 5\n0\n1\n2\n3\n4\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
