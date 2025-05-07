using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class ArrayOperationsTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/ArrayOperationsExamples";

    private readonly ProgramRunner _runner;

    public ArrayOperationsTests(ITestOutputHelper output)
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
    public async Task ShouldDeclareAndPrintIntArray()
    {
        var output = await RunProgram("declareAndPrintIntArray.gs");

        var expectedOutput = "[21, 34, 27]\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldAccessArrayElements()
    {
        var output = await RunProgram("arrayElementAccess.gs");

        var expectedOutput = "21\n34\n27\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleEmptyArray()
    {
        var output = await RunProgram("emptyArray.gs");

        var expectedOutput = "[]\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleStringArray()
    {
        var output = await RunProgram("stringArray.gs");

        var expectedOutput = "[\"Alice\", \"Bob\", \"Charlie\"]\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleBooleanArray()
    {
        var output = await RunProgram("booleanArray.gs");

        var expectedOutput = "[true, false, true]\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleFloatDoubleArray()
    {
        var output = await RunProgram("floatDoubleArray.gs");

        // Note: Floating point output format may have trailing zeros
        var expectedOutput = "[3.140000, 2.718000]\n[1.414000, 1.732000]\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleOutOfBoundsAccess()
    {
        var output = await RunProgram("outOfBoundsAccess.gs");

        // The array operation should print an error message and return a default value
        Assert.Contains("Array index out of bounds", output);
        Assert.Contains("0", output); // Default value for int is 0
    }

    [Fact]
    public async Task ShouldReassignArrayElements()
    {
        var output = await RunProgram("reassignArrayElements.gs");

        var expectedOutput = "[10, 20, 30]\n[10, 99, 30]\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
