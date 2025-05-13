using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class VariableScopeTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/VariableScopeExamples";

    private readonly ProgramRunner _runner;

    public VariableScopeTests(ITestOutputHelper output)
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
    public async Task ShouldHaveBlockLevelScope()
    {
        var output = await RunProgram("blockScope.gs");

        var expectedOutput = "Global x:\n10\nLocal x:\n20\nGlobal x after block:\n10\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldShadowVariablesInNestedScopes()
    {
        var output = await RunProgram("nestedScopes.gs");

        var expectedOutput = "Outer x:\n10\nMiddle x:\n20\nInner x:\n30\nMiddle x after inner:\n20\nOuter x after all:\n10\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldHaveScopeForIfStatement()
    {
        var output = await RunProgram("ifStatementScope.gs");

        var expectedOutput = "Outside value:\n5\nInside if:\n10\nOutside after if:\n5\nInside else:\n15\nOutside after else:\n5\n";
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public async Task ShouldAccessOuterScopeVariables()
    {
        var output = await RunProgram("accessOuterScope.gs");

        var expectedOutput = "Outer x:\n10\nInner uses outer x:\n10\nOuter x modified from inner:\n20\n";
        Assert.Equal(expectedOutput, output);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
