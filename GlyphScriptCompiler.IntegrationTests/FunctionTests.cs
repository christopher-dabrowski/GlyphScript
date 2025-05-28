using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class FunctionTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/FunctionExamples";

    private readonly ProgramRunner _runner;

    public FunctionTests(ITestOutputHelper output)
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
    public async Task ShouldExecuteBasicFunction()
    {
        var output = await RunProgram("basicFunction.gs");

        var expectedOutput = "8\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldExecuteVoidFunction()
    {
        var output = await RunProgram("voidFunction.gs");

        var expectedOutput = "Hello, World!\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldExecuteRecursiveFunction()
    {
        var output = await RunProgram("recursiveFunction.gs");

        var expectedOutput = "120\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldExecuteFunctionWithMultipleParameters()
    {
        var output = await RunProgram("multipleParameters.gs");

        var expectedOutput = "10\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleFunctionScope()
    {
        var output = await RunProgram("functionScope.gs");

        var expectedOutput = "42\n20\n10\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldExecuteNestedFunctionCalls()
    {
        var output = await RunProgram("nestedFunctionCalls.gs");

        var expectedOutput = "20\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
