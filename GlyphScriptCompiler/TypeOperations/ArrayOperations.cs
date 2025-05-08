using GlyphScriptCompiler.Contracts;

namespace GlyphScriptCompiler.TypeOperations;

public class ArrayOperations : IOperationProvider
{
    private readonly LLVMModuleRef _module;
    private readonly LLVMBuilderRef _builder;
    private readonly Dictionary<OperationSignature, OperationImplementation> _operations = new();

    public ArrayOperations(LLVMModuleRef module, LLVMBuilderRef builder)
    {
        _module = module;
        _builder = builder;

        RegisterCreateArrayOperation(GlyphScriptType.Int);
        RegisterCreateArrayOperation(GlyphScriptType.Long);
        RegisterCreateArrayOperation(GlyphScriptType.Float);
        RegisterCreateArrayOperation(GlyphScriptType.Double);
        RegisterCreateArrayOperation(GlyphScriptType.Boolean);
        RegisterCreateArrayOperation(GlyphScriptType.String);

        RegisterArrayAccessOperation(GlyphScriptType.Int);
        RegisterArrayAccessOperation(GlyphScriptType.Long);
        RegisterArrayAccessOperation(GlyphScriptType.Float);
        RegisterArrayAccessOperation(GlyphScriptType.Double);
        RegisterArrayAccessOperation(GlyphScriptType.Boolean);
        RegisterArrayAccessOperation(GlyphScriptType.String);

        RegisterArrayElementAssignmentOperation(GlyphScriptType.Int);
        RegisterArrayElementAssignmentOperation(GlyphScriptType.Long);
        RegisterArrayElementAssignmentOperation(GlyphScriptType.Float);
        RegisterArrayElementAssignmentOperation(GlyphScriptType.Double);
        RegisterArrayElementAssignmentOperation(GlyphScriptType.Boolean);
        RegisterArrayElementAssignmentOperation(GlyphScriptType.String);

        _operations.Add(
            new OperationSignature(OperationKind.Print, [GlyphScriptType.Array]),
            PrintArray
        );
    }

    public void Initialize()
    {
        LlvmHelper.CreateStringConstant(_module, "indexOutOfBoundsError", "Array index out of bounds\\n");
    }

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations => _operations;

    private void RegisterCreateArrayOperation(GlyphScriptType elementType) =>
        _operations.Add(
            new OperationSignature(OperationKind.CreateArray, [elementType]),
            (context, values) => CreateArray(elementType, values)
        );

    private void RegisterArrayAccessOperation(GlyphScriptType elementType)
    {
        _operations.Add(
            new OperationSignature(OperationKind.ArrayAccess,
                [GlyphScriptType.Array, GlyphScriptType.Int, elementType]),
            (context, values) => AccessArray(values, elementType)
        );

        _operations.Add(
            new OperationSignature(OperationKind.ArrayAccess,
                [GlyphScriptType.Array, GlyphScriptType.Long, elementType]),
            (context, values) => AccessArray(values, elementType)
        );
    }

    private void RegisterArrayElementAssignmentOperation(GlyphScriptType elementType)
    {
        _operations.Add(
            new OperationSignature(OperationKind.ArrayElementAssignment,
                [GlyphScriptType.Array, GlyphScriptType.Int, elementType, elementType]),
            (context, values) => AssignArrayElement(values, elementType)
        );

        _operations.Add(
            new OperationSignature(OperationKind.ArrayElementAssignment,
                [GlyphScriptType.Array, GlyphScriptType.Long, elementType, elementType]),
            (context, values) => AssignArrayElement(values, elementType)
        );
    }

    private GlyphScriptValue CreateArray(GlyphScriptType elementType, IReadOnlyList<GlyphScriptValue> elementValues)
    {
        LLVMTypeRef llvmElementType = GetLlvmType(elementType);

        int count = elementValues.Count;
        var arraySizeValue = LLVM.ConstInt(LLVM.Int32Type(), (ulong)count, false);

        var elementSizeValue = GetSizeOfType(llvmElementType);

        var elementsSize = LLVM.BuildMul(_builder,
            elementSizeValue,
            LLVM.ConstInt(LLVM.Int64Type(), (ulong)count, false),
            "elementsSize");

        var totalSize = LLVM.BuildAdd(_builder,
            LLVM.ConstInt(LLVM.Int64Type(), 4, false),
            elementsSize,
            "totalSize");

        var mallocFunc = LLVM.GetNamedFunction(_module, "malloc");
        var arrayPtr = LLVM.BuildCall(_builder, mallocFunc, [totalSize], "arrayMalloc");

        var arraySizePtr = LLVM.BuildBitCast(_builder, arrayPtr, LLVM.PointerType(LLVM.Int32Type(), 0), "arraySizePtr");
        LLVM.BuildStore(_builder, arraySizeValue, arraySizePtr);

        var firstElementOffset = LLVM.ConstInt(LLVM.Int32Type(), 1, false); // Skip 1 int32

        var elementsPtr = LLVM.BuildGEP(_builder, arraySizePtr, [firstElementOffset], "elementsPtr");

        var typedElementsPtr = LLVM.BuildBitCast(_builder,
            elementsPtr, LLVM.PointerType(llvmElementType, 0), "typedElementsPtr");

        for (int i = 0; i < count; i++)
        {
            var index = LLVM.ConstInt(LLVM.Int32Type(), (ulong)i, false);
            var elementPtr = LLVM.BuildGEP(_builder, typedElementsPtr, [index], $"element{i}Ptr");

            LLVM.BuildStore(_builder, elementValues[i].Value, elementPtr);
        }

        var arrayValue = LLVM.BuildBitCast(_builder, arrayPtr,
            LLVM.PointerType(LLVM.Int8Type(), 0), "arrayValue");

        var arrayInfo = new ArrayTypeInfo(elementType);

        return new GlyphScriptValue(arrayValue, GlyphScriptType.Array, arrayInfo);
    }

    private GlyphScriptValue AccessArray(IReadOnlyList<GlyphScriptValue> values, GlyphScriptType elementType)
    {
        var array = values[0];
        var index = values[1];

        var arraySizePtr = LLVM.BuildBitCast(_builder, array.Value,
            LLVM.PointerType(LLVM.Int32Type(), 0), "arraySizePtr");

        var arraySize = LLVM.BuildLoad(_builder, arraySizePtr, "arraySize");

        var isOutOfBounds = LLVM.BuildOr(_builder,
            LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLT, index.Value,
                LLVM.ConstInt(LLVM.Int32Type(), 0, false), "isNegative"),
            LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSGE, index.Value,
                arraySize, "isTooBig"),
            "isOutOfBounds");

        var currentFunction = LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_builder));
        var inBoundsBlock = LLVM.AppendBasicBlock(currentFunction, "inBounds");
        var outOfBoundsBlock = LLVM.AppendBasicBlock(currentFunction, "outOfBounds");
        var continueBlock = LLVM.AppendBasicBlock(currentFunction, "continue");

        LLVM.BuildCondBr(_builder, isOutOfBounds, outOfBoundsBlock, inBoundsBlock);

        LLVM.PositionBuilderAtEnd(_builder, outOfBoundsBlock);

        var indexOutOfBoundsGlobal = LLVM.GetNamedGlobal(_module, "indexOutOfBoundsError");
        var errorMsgPtr = LlvmHelper.GetStringPtr(_builder, indexOutOfBoundsGlobal);

        var printfFunc = LLVM.GetNamedFunction(_module, "printf");
        LLVM.BuildCall(_builder, printfFunc, [errorMsgPtr], "printfError");

        // Create a default value for the element type
        var defaultValue = CreateDefaultValue(elementType);
        LLVM.BuildBr(_builder, continueBlock);

        // Handle in-bounds access
        LLVM.PositionBuilderAtEnd(_builder, inBoundsBlock);

        // Get pointer to the first element
        var firstElementOffset = LLVM.ConstInt(LLVM.Int32Type(), 1, false); // Skip 1 int32
        var elementsPtr = LLVM.BuildGEP(_builder, arraySizePtr, [firstElementOffset], "elementsPtr");

        // Cast to the right element type
        var llvmElementType = GetLlvmType(elementType);
        var typedElementsPtr = LLVM.BuildBitCast(_builder, elementsPtr,
            LLVM.PointerType(llvmElementType, 0), "typedElementsPtr");

        // Get the pointer to the element at the index
        var elementPtr = LLVM.BuildGEP(_builder, typedElementsPtr, [index.Value], "elementPtr");

        // Load the element
        var elementValue = LLVM.BuildLoad(_builder, elementPtr, "elementValue");
        LLVM.BuildBr(_builder, continueBlock);

        // Continue block - uses PHI node to merge results
        LLVM.PositionBuilderAtEnd(_builder, continueBlock);
        var resultPhi = LLVM.BuildPhi(_builder, llvmElementType, "result");

        var incomingValues = new[] { elementValue, defaultValue };
        var incomingBlocks = new[] { inBoundsBlock, outOfBoundsBlock };
        LLVM.AddIncoming(resultPhi, incomingValues, incomingBlocks, 2);

        return new GlyphScriptValue(resultPhi, elementType);
    }

    private GlyphScriptValue AssignArrayElement(IReadOnlyList<GlyphScriptValue> values, GlyphScriptType elementType)
    {
        // Extract values from parameters
        var array = values[0];        // The array
        var index = values[1];        // The index
        var valueToAssign = values[2]; // The value to assign

        // Cast void* to int* to access the size field
        var arraySizePtr = LLVM.BuildBitCast(_builder, array.Value,
            LLVM.PointerType(LLVM.Int32Type(), 0), "arraySizePtr");

        // Load the array size
        var arraySize = LLVM.BuildLoad(_builder, arraySizePtr, "arraySize");

        // Check if index is out of bounds
        var isOutOfBounds = LLVM.BuildOr(_builder,
            LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLT, index.Value,
                LLVM.ConstInt(LLVM.Int32Type(), 0, false), "isNegative"),
            LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSGE, index.Value,
                arraySize, "isTooBig"),
            "isOutOfBounds");

        // Create basic blocks for the bounds check
        var currentFunction = LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_builder));
        var inBoundsBlock = LLVM.AppendBasicBlock(currentFunction, "assignInBounds");
        var outOfBoundsBlock = LLVM.AppendBasicBlock(currentFunction, "assignOutOfBounds");
        var continueBlock = LLVM.AppendBasicBlock(currentFunction, "assignContinue");

        // Branch based on the check
        LLVM.BuildCondBr(_builder, isOutOfBounds, outOfBoundsBlock, inBoundsBlock);

        // Handle out of bounds access - print error
        LLVM.PositionBuilderAtEnd(_builder, outOfBoundsBlock);
        var errorMsg = LlvmHelper.CreateStringConstant(_module, "indexOutOfBoundsErrorAssign",
            "Array index out of bounds for assignment\n");
        var errorMsgPtr = LlvmHelper.GetStringPtr(_builder, errorMsg);

        var printfFunc = LLVM.GetNamedFunction(_module, "printf");
        LLVM.BuildCall(_builder, printfFunc, [errorMsgPtr], "printfError");
        LLVM.BuildBr(_builder, continueBlock);

        // Handle in-bounds access
        LLVM.PositionBuilderAtEnd(_builder, inBoundsBlock);

        // Get pointer to the first element
        var firstElementOffset = LLVM.ConstInt(LLVM.Int32Type(), 1, false); // Skip 1 int32
        var elementsPtr = LLVM.BuildGEP(_builder, arraySizePtr, [firstElementOffset], "elementsPtr");

        // Cast to the right element type
        var llvmElementType = GetLlvmType(elementType);
        var typedElementsPtr = LLVM.BuildBitCast(_builder, elementsPtr,
            LLVM.PointerType(llvmElementType, 0), "typedElementsPtr");

        // Get the pointer to the element at the index
        var elementPtr = LLVM.BuildGEP(_builder, typedElementsPtr, [index.Value], "elementAssignPtr");

        // Store the new value at that location
        LLVM.BuildStore(_builder, valueToAssign.Value, elementPtr);
        LLVM.BuildBr(_builder, continueBlock);

        // Continue block for both paths
        LLVM.PositionBuilderAtEnd(_builder, continueBlock);

        // Return the assigned value
        return valueToAssign;
    }

    private GlyphScriptValue PrintArray(Antlr4.Runtime.RuleContext context, IReadOnlyList<GlyphScriptValue> values)
    {
        // Extract array value
        var array = values[0];
        if (array.ArrayInfo == null)
            throw new InvalidOperationException("Array type info is missing");

        // Cast void* to int* to access the size field
        var arraySizePtr = LLVM.BuildBitCast(_builder, array.Value,
            LLVM.PointerType(LLVM.Int32Type(), 0), "arraySizePtr");

        // Load the array size
        var arraySize = LLVM.BuildLoad(_builder, arraySizePtr, "arraySize");

        // Print opening bracket
        var openBracketMsg = LlvmHelper.CreateStringConstant(_module, "arrayOpenBracket", "[");
        var openBracketMsgPtr = LlvmHelper.GetStringPtr(_builder, openBracketMsg);
        var printfFunc = LLVM.GetNamedFunction(_module, "printf");
        LLVM.BuildCall(_builder, printfFunc, [openBracketMsgPtr], "printfOpenBracket");

        // Setup for loop to print each element
        var currentFunction = LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_builder));
        var condBlock = LLVM.AppendBasicBlock(currentFunction, "forCond");
        var bodyBlock = LLVM.AppendBasicBlock(currentFunction, "forBody");
        var exitBlock = LLVM.AppendBasicBlock(currentFunction, "forExit");

        // Initialize loop counter
        var counterPtr = LLVM.BuildAlloca(_builder, LLVM.Int32Type(), "i");
        LLVM.BuildStore(_builder, LLVM.ConstInt(LLVM.Int32Type(), 0, false), counterPtr);
        LLVM.BuildBr(_builder, condBlock);

        // Loop condition
        LLVM.PositionBuilderAtEnd(_builder, condBlock);
        var counter = LLVM.BuildLoad(_builder, counterPtr, "iValue");
        var continueLoop = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLT,
            counter, arraySize, "continueLoop");
        LLVM.BuildCondBr(_builder, continueLoop, bodyBlock, exitBlock);

        // Loop body
        LLVM.PositionBuilderAtEnd(_builder, bodyBlock);

        // Get element at the current index
        // Get pointer to the first element
        var firstElementOffset = LLVM.ConstInt(LLVM.Int32Type(), 1, false); // Skip 1 int32
        var elementsPtr = LLVM.BuildGEP(_builder, arraySizePtr, [firstElementOffset], "elementsPtr");

        // Cast to the right element type
        var elementType = array.ArrayInfo.ElementType;
        var llvmElementType = GetLlvmType(elementType);
        var typedElementsPtr = LLVM.BuildBitCast(_builder, elementsPtr,
            LLVM.PointerType(llvmElementType, 0), "typedElementsPtr");

        // Get the pointer to the element at the index
        var elementPtr = LLVM.BuildGEP(_builder, typedElementsPtr, [counter], "elementPtr");

        // Load the element
        var elementValue = LLVM.BuildLoad(_builder, elementPtr, "elementValue");

        // Print comma if not the first element
        var isFirstElement = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ,
            counter, LLVM.ConstInt(LLVM.Int32Type(), 0, false), "isFirstElement");
        var commaBlock = LLVM.AppendBasicBlock(currentFunction, "printComma");
        var printElementBlock = LLVM.AppendBasicBlock(currentFunction, "printElement");

        LLVM.BuildCondBr(_builder, isFirstElement, printElementBlock, commaBlock);

        // Print comma
        LLVM.PositionBuilderAtEnd(_builder, commaBlock);
        var commaMsg = LlvmHelper.CreateStringConstant(_module, "arrayComma", ", ");
        var commaMsgPtr = LlvmHelper.GetStringPtr(_builder, commaMsg);
        LLVM.BuildCall(_builder, printfFunc, [commaMsgPtr], "printfComma");
        LLVM.BuildBr(_builder, printElementBlock);

        // Print element
        LLVM.PositionBuilderAtEnd(_builder, printElementBlock);

        // Print element based on its type
        switch (elementType)
        {
            case GlyphScriptType.Int:
                var intFormatMsg = LlvmHelper.CreateStringConstant(_module, "intFormat", "%d");
                var intFormatMsgPtr = LlvmHelper.GetStringPtr(_builder, intFormatMsg);
                LLVM.BuildCall(_builder, printfFunc, [intFormatMsgPtr, elementValue], "printfInt");
                break;
            case GlyphScriptType.Long:
                var longFormatMsg = LlvmHelper.CreateStringConstant(_module, "longFormat", "%lld");
                var longFormatMsgPtr = LlvmHelper.GetStringPtr(_builder, longFormatMsg);
                LLVM.BuildCall(_builder, printfFunc, [longFormatMsgPtr, elementValue], "printfLong");
                break;
            case GlyphScriptType.Float:
                // Convert float to double for printf
                var floatAsDouble = LLVM.BuildFPExt(_builder, elementValue, LLVM.DoubleType(), "floatAsDouble");
                var floatFormatMsg = LlvmHelper.CreateStringConstant(_module, "floatFormat", "%f");
                var floatFormatMsgPtr = LlvmHelper.GetStringPtr(_builder, floatFormatMsg);
                LLVM.BuildCall(_builder, printfFunc, [floatFormatMsgPtr, floatAsDouble], "printfFloat");
                break;
            case GlyphScriptType.Double:
                var doubleFormatMsg = LlvmHelper.CreateStringConstant(_module, "doubleFormat", "%f");
                var doubleFormatMsgPtr = LlvmHelper.GetStringPtr(_builder, doubleFormatMsg);
                LLVM.BuildCall(_builder, printfFunc, [doubleFormatMsgPtr, elementValue], "printfDouble");
                break;
            case GlyphScriptType.Boolean:
                // Create blocks for true/false paths
                var trueBlock = LLVM.AppendBasicBlock(currentFunction, "printTrue");
                var falseBlock = LLVM.AppendBasicBlock(currentFunction, "printFalse");
                var boolContinueBlock = LLVM.AppendBasicBlock(currentFunction, "boolContinue");

                // Branch based on boolean value
                LLVM.BuildCondBr(_builder, elementValue, trueBlock, falseBlock);

                // Print "true"
                LLVM.PositionBuilderAtEnd(_builder, trueBlock);
                var trueMsg = LlvmHelper.CreateStringConstant(_module, "trueString", "true");
                var trueMsgPtr = LlvmHelper.GetStringPtr(_builder, trueMsg);
                LLVM.BuildCall(_builder, printfFunc, [trueMsgPtr], "printfTrue");
                LLVM.BuildBr(_builder, boolContinueBlock);

                // Print "false"
                LLVM.PositionBuilderAtEnd(_builder, falseBlock);
                var falseMsg = LlvmHelper.CreateStringConstant(_module, "falseString", "false");
                var falseMsgPtr = LlvmHelper.GetStringPtr(_builder, falseMsg);
                LLVM.BuildCall(_builder, printfFunc, [falseMsgPtr], "printfFalse");
                LLVM.BuildBr(_builder, boolContinueBlock);

                // Continue
                LLVM.PositionBuilderAtEnd(_builder, boolContinueBlock);
                break;
            case GlyphScriptType.String:
                var stringMsg = LlvmHelper.CreateStringConstant(_module, "stringFormat", "\"%s\"");
                var stringMsgPtr = LlvmHelper.GetStringPtr(_builder, stringMsg);
                LLVM.BuildCall(_builder, printfFunc, [stringMsgPtr, elementValue], "printfString");
                break;
            default:
                // For unsupported types, just print a placeholder
                var unknownMsg = LlvmHelper.CreateStringConstant(_module, "unknownFormat", "?");
                var unknownMsgPtr = LlvmHelper.GetStringPtr(_builder, unknownMsg);
                LLVM.BuildCall(_builder, printfFunc, [unknownMsgPtr], "printfUnknown");
                break;
        }

        // Increment counter
        var nextCounter = LLVM.BuildAdd(_builder, counter,
            LLVM.ConstInt(LLVM.Int32Type(), 1, false), "nextI");
        LLVM.BuildStore(_builder, nextCounter, counterPtr);

        // Loop back
        LLVM.BuildBr(_builder, condBlock);

        // Loop exit
        LLVM.PositionBuilderAtEnd(_builder, exitBlock);

        // Print closing bracket and newline
        var closeBracketMsg = LlvmHelper.CreateStringConstant(_module, "arrayCloseBracket", "]\n");
        var closeBracketMsgPtr = LlvmHelper.GetStringPtr(_builder, closeBracketMsg);
        LLVM.BuildCall(_builder, printfFunc, [closeBracketMsgPtr], "printfCloseBracket");

        // Return the array itself
        return array;
    }

    private static LLVMTypeRef GetLlvmType(GlyphScriptType type)
    {
        return type switch
        {
            GlyphScriptType.Int => LLVM.Int32Type(),
            GlyphScriptType.Long => LLVM.Int64Type(),
            GlyphScriptType.Float => LLVM.FloatType(),
            GlyphScriptType.Double => LLVM.DoubleType(),
            GlyphScriptType.String => LLVM.PointerType(LLVM.Int8Type(), 0),
            GlyphScriptType.Boolean => LLVM.Int1Type(),
            _ => throw new InvalidOperationException($"Unsupported type: {type}")
        };
    }

    private LLVMValueRef GetSizeOfType(LLVMTypeRef type)
    {
        // Create a temporary alloca to calculate the size
        var currentBlock = LLVM.GetInsertBlock(_builder);
        var currentFunction = LLVM.GetBasicBlockParent(currentBlock);
        var entryBlock = LLVM.GetEntryBasicBlock(currentFunction);
        var firstInstruction = LLVM.GetFirstInstruction(entryBlock);

        // Position at the start of the entry block to ensure size calculation happens early
        LLVM.PositionBuilder(_builder, entryBlock, firstInstruction);

        var tempAlloca = LLVM.BuildAlloca(_builder, type, "sizeofTemp");

        // Calculate the pointer difference between this pointer and the next
        var ptrType = LLVM.TypeOf(tempAlloca);
        var nextPtr = LLVM.BuildGEP(_builder, tempAlloca,
            [LLVM.ConstInt(LLVM.Int32Type(), 1, false)], "nextPtr");

        // Cast both pointers to i64 for subtraction
        var ptr1 = LLVM.BuildPtrToInt(_builder, nextPtr, LLVM.Int64Type(), "ptr1AsInt");
        var ptr0 = LLVM.BuildPtrToInt(_builder, tempAlloca, LLVM.Int64Type(), "ptr0AsInt");

        // Calculate difference
        var size = LLVM.BuildSub(_builder, ptr1, ptr0, "sizeof");

        // Position back to where we were before
        LLVM.PositionBuilderAtEnd(_builder, currentBlock);

        return size;
    }

    private LLVMValueRef CreateDefaultValue(GlyphScriptType type)
    {
        switch (type)
        {
            case GlyphScriptType.Int:
                return LLVM.ConstInt(LLVM.Int32Type(), 0, false);
            case GlyphScriptType.Long:
                return LLVM.ConstInt(LLVM.Int64Type(), 0, false);
            case GlyphScriptType.Float:
                return LLVM.ConstReal(LLVM.FloatType(), 0);
            case GlyphScriptType.Double:
                return LLVM.ConstReal(LLVM.DoubleType(), 0);
            case GlyphScriptType.Boolean:
                return LLVM.ConstInt(LLVM.Int1Type(), 0, false);
            case GlyphScriptType.String:
                return LlvmHelper.CreateStringConstant(_module, "emptyString", "");
            default:
                throw new InvalidOperationException($"Cannot create default value for type: {type}");
        }
    }
}
