using GlyphScriptCompiler.IntegrationTests.TestHelpers;

namespace GlyphScriptCompiler.IntegrationTests;

public class ClassTests : IDisposable
{
    private const string TestFilesDirectory = "TestData/ClassExamples";
    private readonly ProgramRunner _runner;

    public ClassTests(ITestOutputHelper output)
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
    public async Task ClassFieldAssignment_ShouldExecuteCorrectly()
    {
        // This test verifies that class field assignment and access works correctly
        var output = await RunProgram("classFieldAssignment.gs");

        // Should print the values that were assigned
        Assert.Contains("Tesla", output);
        Assert.Contains("2023", output);
    }

    [Fact]
    public async Task ClassBasicExample_ShouldExecuteCorrectly()
    {
        // This test verifies that class methods and field assignments work correctly
        var output = await RunProgram("classBasicExample.gs");

        // Should print name and age values through different access methods
        Assert.Contains("Alice", output);
        Assert.Contains("25", output);
        Assert.Contains("30", output); // After setAge method call
    }

    [Fact]
    public async Task ClassMethodCalls_ShouldExecuteCorrectly()
    {
        // This test verifies that class methods with parameters and return values work correctly
        var output = await RunProgram("classMethodCalls.gs");

        // Should print initial result, sum, product, and final result
        Assert.Contains("10", output);  // Initial result
        Assert.Contains("8", output);   // Sum of 5 + 3
        Assert.Contains("28", output);  // Product of 4 * 7
        Assert.Contains("8", output);   // Final result should be 8 (from add method)
    }

    [Fact]
    public async Task MultipleClasses_ShouldExecuteCorrectly()
    {
        // This test verifies that multiple different classes can coexist and interact
        var output = await RunProgram("multipleClasses.gs");

        // Should print student and teacher information
        Assert.Contains("Alice", output);
        Assert.Contains("95", output);
        Assert.Contains("Mathematics", output);
        Assert.Contains("15", output);
    }

    [Fact]
    public async Task MixedFieldTypes_ShouldExecuteCorrectly()
    {
        // This test verifies that classes can have fields of different types (string, int, bool, float)
        var output = await RunProgram("mixedFieldTypes.gs");

        // Should print product information with different data types
        Assert.Contains("Gaming Laptop", output);
        Assert.Contains("1500", output);
        Assert.Contains("true", output); // Available should be true
        Assert.Contains("4.5", output);
        Assert.Contains("1200", output); // Price after update
    }

    [Fact]
    public async Task MethodFieldInteraction_ShouldExecuteCorrectly()
    {
        // This test verifies that methods can access and modify multiple class fields
        var output = await RunProgram("methodFieldInteraction.gs");

        // Should print rectangle calculations and field modifications
        Assert.Contains("200", output);  // Initial area (10 * 20)
        Assert.Contains("60", output);   // Initial perimeter (2 * (10 + 20))
        Assert.Contains("false", output); // Not a square initially
        Assert.Contains("20", output);   // Width after scaling (10 * 2)
        Assert.Contains("40", output);   // Height after scaling (20 * 2)
        Assert.Contains("800", output);  // Area after scaling (20 * 40)
        Assert.Contains("true", output); // Is square after height change
    }

    [Fact]
    public async Task EmptyClass_ShouldExecuteCorrectly()
    {
        // This test verifies that empty classes (no fields or methods) can be instantiated
        var output = await RunProgram("emptyClass.gs");

        // Should successfully instantiate and print message
        Assert.Contains("Empty class instantiated successfully", output);
    }

    [Fact]
    public async Task MethodsOnlyClass_ShouldExecuteCorrectly()
    {
        // This test verifies that classes with only methods (no fields) work correctly
        var output = await RunProgram("methodsOnlyClass.gs");

        // Should print results of method calls
        Assert.Contains("25", output);  // Square of 5
        Assert.Contains("27", output);  // Cube of 3
        Assert.Contains("10", output);  // Max of 10 and 7
        Assert.Contains("9", output);   // Max of 4 and 9
    }

    [Fact]
    public async Task FieldsOnlyClass_ShouldExecuteCorrectly()
    {
        // This test verifies that classes with only fields (no methods) work correctly
        var output = await RunProgram("fieldsOnlyClass.gs");

        // Should print field values for both instances
        Assert.Contains("0", output);   // Origin coordinates
        Assert.Contains("10", output);  // Point1 x
        Assert.Contains("20", output);  // Point1 y
        Assert.Contains("30", output);  // Point1 z
    }

    [Fact]
    public async Task ComplexInteraction_ShouldExecuteCorrectly()
    {
        // This test verifies complex class interactions with multiple method calls
        var output = await RunProgram("complexInteraction.gs");

        // Should print counter values through various operations
        Assert.Contains("5", output);   // Initial value
        Assert.Contains("6", output);   // After first increment
        Assert.Contains("7", output);   // After second increment
        Assert.Contains("17", output);  // After adding 10
        Assert.Contains("16", output);  // After decrement
        Assert.Contains("0", output);   // After reset
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
