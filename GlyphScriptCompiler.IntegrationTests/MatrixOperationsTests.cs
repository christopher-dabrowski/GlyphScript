using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class MatrixOperationsTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/MatrixOperationsExamples";

    private readonly ProgramRunner _runner;

    public MatrixOperationsTests(ITestOutputHelper output)
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
    public async Task ShouldDeclareAndPrintMatrix()
    {
        var output = await RunProgram("declareAndPrintMatrix.gs");

        var expectedOutput = "[\n  [1, 2]\n  [3, 4]\n]\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldAccessMatrixElements()
    {
        var output = await RunProgram("matrixElementAccess.gs");

        var expectedOutput = "1\n2\n3\n4\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleEmptyMatrix()
    {
        var output = await RunProgram("emptyMatrix.gs");

        var expectedOutput = "[]\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHandleMatrixWithDifferentRowSizes()
    {
        var output = await RunProgram("invalidMatrixRowSizes.gs");

        Assert.Contains("All rows in the matrix must have the same number of elements", output);
    }

    [Fact]
    public async Task ShouldHandleMatrixWithMixedElementTypes()
    {
        var output = await RunProgram("invalidMatrixElementTypes.gs");

        Assert.Contains("All elements in the matrix must be of the same type", output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}