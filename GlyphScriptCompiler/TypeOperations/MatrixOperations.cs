using GlyphScriptCompiler;
using GlyphScriptCompiler.Contracts;

public class MatrixOperations : IOperationProvider
{
    private readonly LLVMModuleRef _module;
    private readonly LLVMBuilderRef _builder;
    private readonly Dictionary<OperationSignature, OperationImplementation> _operations = new();

    public MatrixOperations(LLVMModuleRef module, LLVMBuilderRef builder)
    {
        _module = module;
        _builder = builder;
    }

    public void Initialize()
    {
        _operations[new OperationSignature(OperationKind.CreateMatrix, [GlyphScriptType.Int])] = CreateMatrix;
        _operations[new OperationSignature(OperationKind.CreateMatrix, [GlyphScriptType.Float])] = CreateMatrix;
        _operations[new OperationSignature(OperationKind.Print, [GlyphScriptType.Matrix])] = PrintMatrix;
    }

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations => _operations;

    private GlyphScriptValue? CreateMatrix(RuleContext context, IReadOnlyList<GlyphScriptValue> elements)
    {
        var rowCount = elements.Count; // Assuming row-major order
        var elementType = elements[0].Type;

        // Allocate memory for the matrix
        var matrixType = LLVM.ArrayType(GetLlvmType(elementType), (uint)rowCount);
        var matrix = LLVM.BuildAlloca(_builder, matrixType, "matrix");

        // Populate the matrix
        for (int i = 0; i < elements.Count; i++)
        {
            var elementPtr = LLVM.BuildGEP(_builder, matrix, new[] { LLVM.ConstInt(LLVM.Int32Type(), (ulong)i, false) }, "elementPtr");
            LLVM.BuildStore(_builder, elements[i].Value, elementPtr);
        }

        return new GlyphScriptValue(matrix, GlyphScriptType.Matrix, null, new MatrixTypeInfo(rowCount, elements.Count / rowCount, elementType));
    }

    private GlyphScriptValue? PrintMatrix(RuleContext context, IReadOnlyList<GlyphScriptValue> parameters)
    {
        if (parameters.Count != 1)
            throw new InvalidOperationException("Invalid number of parameters for print operation");

        var matrix = parameters[0];
        if (matrix.Type != GlyphScriptType.Matrix || matrix.MatrixInfo == null)
            throw new InvalidOperationException("Invalid matrix type or uninitialized MatrixInfo");

        var rowCount = matrix.MatrixInfo.RowCount;
        var columnCount = matrix.MatrixInfo.ColumnCount;

        // Get printf function
        var printfFunc = LLVM.GetNamedFunction(_module, "printf");
        if (printfFunc.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("printf function not found");

        // Print opening bracket for the matrix
        var openBracket = LlvmHelper.CreateStringConstant(_module, "matrixOpenBracket", "[\n");
        var openBracketPtr = LlvmHelper.GetStringPtr(_builder, openBracket);
        LLVM.BuildCall(_builder, printfFunc, new[] { openBracketPtr }, string.Empty);

        // Iterate over rows and elements
        for (int i = 0; i < rowCount; i++)
        {
            var rowOpenBracket = LlvmHelper.CreateStringConstant(_module, "rowOpenBracket", "  [");
            var rowOpenBracketPtr = LlvmHelper.GetStringPtr(_builder, rowOpenBracket);
            LLVM.BuildCall(_builder, printfFunc, new[] { rowOpenBracketPtr }, string.Empty);

            for (int j = 0; j < columnCount; j++)
            {
                var element = GetMatrixElement(matrix, i, j);
                var formatString = GetFormatStringForType(matrix.MatrixInfo.ElementType);
                var formatPtr = LlvmHelper.GetStringPtr(_builder, formatString);
                LLVM.BuildCall(_builder, printfFunc, new[] { formatPtr, element.Value }, string.Empty);

                if (j < columnCount - 1)
                {
                    var comma = LlvmHelper.CreateStringConstant(_module, "comma", ", ");
                    var commaPtr = LlvmHelper.GetStringPtr(_builder, comma);
                    LLVM.BuildCall(_builder, printfFunc, new[] { commaPtr }, string.Empty);
                }
            }

            var rowCloseBracket = LlvmHelper.CreateStringConstant(_module, "rowCloseBracket", "]\n");
            var rowCloseBracketPtr = LlvmHelper.GetStringPtr(_builder, rowCloseBracket);
            LLVM.BuildCall(_builder, printfFunc, new[] { rowCloseBracketPtr }, string.Empty);
        }

        var closeBracket = LlvmHelper.CreateStringConstant(_module, "matrixCloseBracket", "]\n");
        var closeBracketPtr = LlvmHelper.GetStringPtr(_builder, closeBracket);
        LLVM.BuildCall(_builder, printfFunc, new[] { closeBracketPtr }, string.Empty);

        return matrix;
    }

    private LLVMTypeRef GetLlvmType(GlyphScriptType type)
    {
        return type switch
        {
            GlyphScriptType.Int => LLVM.Int32Type(),
            GlyphScriptType.Float => LLVM.FloatType(),
            _ => throw new InvalidOperationException($"Unsupported matrix element type: {type}")
        };
    }

    private GlyphScriptValue GetMatrixElement(GlyphScriptValue matrix, int row, int column)
    {
        // Logic to retrieve the element at (row, column) from the matrix
        // This depends on how the matrix is stored in memory
        throw new NotImplementedException();
    }

    private LLVMValueRef GetFormatStringForType(GlyphScriptType type)
    {
        return type switch
        {
            GlyphScriptType.Int => LlvmHelper.CreateStringConstant(_module, "intFormat", "%d"),
            GlyphScriptType.Float => LlvmHelper.CreateStringConstant(_module, "floatFormat", "%f"),
            _ => throw new InvalidOperationException($"Unsupported matrix element type: {type}")
        };
    }
}