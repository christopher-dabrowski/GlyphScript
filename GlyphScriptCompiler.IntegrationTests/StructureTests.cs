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

    [Fact]
    public async Task MultipleFields_ShouldHandleVariousDataTypes()
    {
        // Test structures with multiple field types
        var output = await RunProgram("multipleFields.gs");

        Assert.Contains("30", output);      // int age
        Assert.Contains("John", output);    // string name
        Assert.Contains("6.2", output);     // float height
        Assert.Contains("true", output);    // boolean isActive
    }

    [Fact]
    public async Task MultipleStructTypes_ShouldHandleDifferentStructures()
    {
        // Test multiple different structure types in same program
        var output = await RunProgram("multipleStructTypes.gs");

        Assert.Contains("10", output);   // Point.x
        Assert.Contains("20", output);   // Point.y
        Assert.Contains("100", output);  // Rectangle.width
        Assert.Contains("50", output);   // Rectangle.height
    }

    [Fact]
    public async Task StructWithExpressions_ShouldEvaluateExpressionsInAssignment()
    {
        // Test assigning expressions to struct fields
        var output = await RunProgram("structWithExpressions.gs");

        Assert.Contains("8", output);    // 5 + 3
        Assert.Contains("16", output);   // 8 * 2
        Assert.Contains("15", output);   // 16 - 1
    }

    [Fact]
    public async Task StructWithRegularVariables_ShouldWorkWithMixedVariableTypes()
    {
        // Test structures alongside regular variables
        var output = await RunProgram("structWithRegularVariables.gs");

        Assert.Contains("25", output);    // age from struct
        Assert.Contains("Alice", output); // name from struct
        Assert.Contains("25", output);    // regular age variable
        Assert.Contains("Alice", output); // regular name variable
    }

    [Fact]
    public async Task MultipleStructInstances_ShouldHandleMultipleInstancesOfSameType()
    {
        // Test multiple instances of the same structure type
        var output = await RunProgram("multipleStructInstances.gs");

        Assert.Contains("1001", output);  // student1.id
        Assert.Contains("John", output);  // student1.name
        Assert.Contains("3.75", output);  // student1.gpa
        Assert.Contains("1002", output);  // student2.id
        Assert.Contains("Jane", output);  // student2.name
        Assert.Contains("3.90", output);  // student2.gpa
    }

    [Fact]
    public async Task StructInControlFlow_ShouldWorkInIfStatements()
    {
        // Test structure fields in control flow conditions
        var output = await RunProgram("structInControlFlow.gs");

        Assert.Contains("0", output);     // Initial counter value
        Assert.Contains("Final count:", output);
        Assert.Contains("5", output);     // Final counter value
    }

    [Fact]
    public async Task StructFieldsInCalculations_ShouldUseFieldsInComplexExpressions()
    {
        // Test using struct fields in mathematical calculations
        var output = await RunProgram("structFieldsInCalculations.gs");

        Assert.Contains("Volume:", output);
        Assert.Contains("150", output);    // 10 * 5 * 3
        Assert.Contains("Surface Area:", output);
        Assert.Contains("190", output);    // 2 * (10*5 + 5*3 + 3*10) = 2 * (50 + 15 + 30) = 2 * 95 = 190
    }

    [Fact]
    public async Task EmptyStruct_ShouldCompileSuccessfully()
    {
        // Test structure with no fields
        var output = await RunProgram("emptyStruct.gs");

        Assert.Contains("Empty struct created successfully", output);
    }

    [Fact]
    public async Task SingleFieldStruct_ShouldWorkWithOneField()
    {
        // Test structure with single field
        var output = await RunProgram("singleFieldStruct.gs");

        Assert.Contains("42", output);
    }

    public void Dispose()
    {
        _runner?.Dispose();
    }
}
