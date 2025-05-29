using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class AutoTypeTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/AutoTypeExamples";

    private readonly ProgramRunner _runner;

    public AutoTypeTests(ITestOutputHelper output)
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
    public async Task ShouldDetectIntegerType()
    {
        var output = await RunProgram("autoInteger.gs");

        var expectedOutput = "42\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldDetectLongType()
    {
        var output = await RunProgram("autoLong.gs");

        var expectedOutput = "1234567890\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldDetectFloatType()
    {
        var output = await RunProgram("autoFloat.gs");

        var expectedOutput = "3.140000\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldDetectDoubleType()
    {
        var output = await RunProgram("autoDouble.gs");

        var expectedOutput = "3.141593\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldDetectStringType()
    {
        var output = await RunProgram("autoString.gs");

        var expectedOutput = "Hello World\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldDetectBooleanType()
    {
        var output = await RunProgram("autoBoolean.gs");

        var expectedOutput = "true\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldDetectTypeFromExpression()
    {
        var output = await RunProgram("autoExpression.gs");

        var expectedOutput = "50\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldDetectTypeFromFunctionCall()
    {
        var output = await RunProgram("autoFunctionCall.gs");

        var expectedOutput = "42\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldDetectMultipleAutoTypes()
    {
        var output = await RunProgram("multipleAutoTypes.gs");

        var expectedOutput = "10\n3.140000\nTest\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
