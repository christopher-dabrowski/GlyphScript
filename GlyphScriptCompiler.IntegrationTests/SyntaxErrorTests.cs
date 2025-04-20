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
        var output = await RunProgram("invalidDeclaration.gs", "");
        // Expected: Syntax error about invalid type or missing type
    }

    [Fact]
    public async Task ShouldDetectInvalidExpression()
    {
        var output = await RunProgram("invalidExpression.gs", "");
        // Expected: Syntax error about invalid expression
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
        var output = await RunProgram("invalidPrint.gs", "");
        // Expected: Syntax error about invalid print statement
    }

    [Fact]
    public async Task ShouldDetectInvalidReadStatement()
    {
        var output = await RunProgram("invalidRead.gs", "");
        // Expected: Syntax error about invalid read statement
    }

    [Fact]
    public async Task ShouldDetectUnmatchedParentheses()
    {
        var output = await RunProgram("unmatchedParentheses.gs", "");
        // Expected: Syntax error about unmatched parentheses
    }

    [Fact]
    public async Task ShouldDetectInvalidOperatorUsage()
    {
        var output = await RunProgram("invalidOperator.gs", "");
        // Expected: Syntax error about invalid operator usage
    }

    [Fact]
    public async Task ShouldDetectInvalidTypeConversion()
    {
        var output = await RunProgram("invalidTypeConversion.gs", "");
        // Expected: Syntax error about invalid type conversion
    }

    [Fact]
    public async Task ShouldDetectInvalidIdentifier()
    {
        var output = await RunProgram("invalidIdentifier.gs", "");
        // Expected: Syntax error about invalid identifier
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
