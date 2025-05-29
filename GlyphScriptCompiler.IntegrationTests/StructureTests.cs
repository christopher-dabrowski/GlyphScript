using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class StructureTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/StructureExamples";
    private readonly ProgramRunner _runner;

    public StructureTests(ITestOutputHelper output)
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
    public async Task StructDeclarationAndInstantiation_ShouldCompileSuccessfully()
    {
        // This test verifies that basic structure declaration and instantiation works
        var output = await RunProgram("basicStruct.gs");
        
        // If we get here without exceptions, the compilation was successful
        Assert.NotNull(output);
    }

    [Fact]
    public async Task StructFieldAssignment_ShouldCompileSuccessfully()
    {
        // This test verifies that field assignment works
        var output = await RunProgram("structFieldAssignment.gs");
        
        // If we get here without exceptions, the compilation was successful
        Assert.NotNull(output);
    }

    [Fact]
    public async Task StructFieldAccess_ShouldCompileAndExecuteCorrectly()
    {
        // This test verifies that field access and value printing works
        var output = await RunProgram("structFieldAccess.gs");
        
        // Should print the age that was assigned
        Assert.Contains("25", output);
    }

    public void Dispose()
    {
        _runner?.Dispose();
    }
}
