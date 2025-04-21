using GlyphScriptCompiler.IntegrationTests.TestHelpers;
using GlyphScriptCompiler.SyntaxErrors;

namespace GlyphScriptCompiler.IntegrationTests;

public class SyntaxErrorTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/SyntaxErrorExamples";
    private readonly ProgramRunner _runner;

    public SyntaxErrorTests(ITestOutputHelper output)
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
    public async Task ShouldDetectInvalidVariableDeclaration()
    {
        var exception = await Assert.ThrowsAsync<InvalidSyntaxException>(async () =>
        {
            await RunProgram("invalidDeclaration.gs", "");
        });
    }

    [Fact]
    public async Task ShouldDetectInvalidExpression()
    {
        var exception = await Assert.ThrowsAsync<InvalidSyntaxException>(async () =>
        {
            await RunProgram("invalidExpression.gs", "");
        });
    }

    [Fact]
    public async Task ShouldDetectInvalidAssignment()
    {
        var exception = await Assert.ThrowsAsync<UndefinedVariableUsageException>(async () =>
        {
            await RunProgram("invalidAssignment.gs", "");
        });

        Assert.Equal(3, exception.Line);
        Assert.Equal(0, exception.Column);
        Assert.Equal("y", exception.VariableName);
    }

    [Fact]
    public async Task ShouldDetectInvalidPrintStatement()
    {
        var exception = await Assert.ThrowsAsync<InvalidSyntaxException>(async () =>
        {
            await RunProgram("invalidPrint.gs", "");
        });
    }

    [Fact]
    public async Task ShouldDetectInvalidReadStatement()
    {
        var exception = await Assert.ThrowsAsync<InvalidSyntaxException>(async () =>
        {
            await RunProgram("invalidRead.gs", "");
        });
    }

    [Fact]
    public async Task ShouldDetectUnmatchedParentheses()
    {
        var exception = await Assert.ThrowsAsync<InvalidSyntaxException>(async () =>
        {
            await RunProgram("unmatchedParentheses.gs", "");
        });
    }

    [Fact]
    public async Task ShouldDetectInvalidOperatorUsage()
    {
        var exception = await Assert.ThrowsAsync<InvalidSyntaxException>(async () =>
        {
            await RunProgram("invalidOperator.gs", "");
        });
    }

    [Fact]
    public async Task ShouldDetectInvalidTypeConversion()
    {
        var exception = await Assert.ThrowsAsync<InvalidSyntaxException>(async () =>
        {
            await RunProgram("invalidTypeConversion.gs", "");
        });
    }

    [Fact]
    public async Task ShouldDetectInvalidIdentifier()
    {
        var exception = await Assert.ThrowsAsync<InvalidSyntaxException>(async () =>
        {
            await RunProgram("invalidIdentifier.gs", "");
        });
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
