using GlyphScriptCompiler.Contracts;

namespace GlyphScriptCompiler.TypeOperations;

public class ArrayOperations : IOperationProvider
{
    private readonly LLVMModuleRef _module;
    private readonly LLVMBuilderRef _builder;
    private readonly Dictionary<OperationSignature, OperationImplementation> _operations = new();

    private LLVMValueRef _indexOutOfBoundsGlobal;

    private LLVMValueRef _arrayOpenBracketMsg;
    private LLVMValueRef _arrayCloseBracketMsg;
    private LLVMValueRef _arrayCommaMsg;
    private LLVMValueRef _intFormatMsg;
    private LLVMValueRef _longFormatMsg;
    private LLVMValueRef _floatFormatMsg;
    private LLVMValueRef _doubleFormatMsg;
    private LLVMValueRef _stringFormatMsg;
    private LLVMValueRef _trueMsg;
    private LLVMValueRef _falseMsg;
    private LLVMValueRef _unknownFormatMsg;

    public ArrayOperations(
        LLVMModuleRef module,
        LLVMBuilderRef builder)
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
        _indexOutOfBoundsGlobal = LlvmHelper.CreateStringConstant(_module, "indexOutOfBoundsError", "Array index out of bounds\\n");
        _arrayOpenBracketMsg = LlvmHelper.CreateStringConstant(_module, "arrayOpenBracket", "[");
        _arrayCloseBracketMsg = LlvmHelper.CreateStringConstant(_module, "arrayCloseBracket", "]\n");
        _arrayCommaMsg = LlvmHelper.CreateStringConstant(_module, "arrayComma", ", ");
        _unknownFormatMsg = LlvmHelper.CreateStringConstant(_module, "unknownFormat", "?");

        _intFormatMsg = LLVM.GetNamedGlobal(_module, "strs_int");
        if (_intFormatMsg.Pointer == IntPtr.Zero)
            _intFormatMsg = LlvmHelper.CreateStringConstant(_module, "intFormat", "%d");

        _longFormatMsg = LLVM.GetNamedGlobal(_module, "strs_long");
        if (_longFormatMsg.Pointer == IntPtr.Zero)
            _longFormatMsg = LlvmHelper.CreateStringConstant(_module, "longFormat", "%lld");

        _floatFormatMsg = LLVM.GetNamedGlobal(_module, "strs_float");
        if (_floatFormatMsg.Pointer == IntPtr.Zero)
            _floatFormatMsg = LlvmHelper.CreateStringConstant(_module, "floatFormat", "%f");

        _doubleFormatMsg = LLVM.GetNamedGlobal(_module, "strs_double");
        if (_doubleFormatMsg.Pointer == IntPtr.Zero)
            _doubleFormatMsg = LlvmHelper.CreateStringConstant(_module, "doubleFormat", "%f");

        _stringFormatMsg = LLVM.GetNamedGlobal(_module, "strs_string");
        if (_stringFormatMsg.Pointer == IntPtr.Zero)
            _stringFormatMsg = LlvmHelper.CreateStringConstant(_module, "stringFormat", "\"%s\"");

        _trueMsg = LLVM.GetNamedGlobal(_module, "trueString");
        if (_trueMsg.Pointer == IntPtr.Zero)
            _trueMsg = LlvmHelper.CreateStringConstant(_module, "trueString", "true");

        _falseMsg = LLVM.GetNamedGlobal(_module, "falseString");
        if (_falseMsg.Pointer == IntPtr.Zero)
            _falseMsg = LlvmHelper.CreateStringConstant(_module, "falseString", "false");
    }

    public IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations => _operations;

    private void RegisterCreateArrayOperation(GlyphScriptType elementType) =>
        _operations.Add(
            new OperationSignature(OperationKind.CreateArray, [elementType]),
            (_, values) => CreateArray(elementType, values)
        );

    private void RegisterArrayAccessOperation(GlyphScriptType elementType)
    {
        _operations.Add(
            new OperationSignature(OperationKind.ArrayAccess,
                [GlyphScriptType.Array, GlyphScriptType.Int, elementType]),
            (_, values) => AccessArray(values, elementType)
        );

        _operations.Add(
            new OperationSignature(OperationKind.ArrayAccess,
                [GlyphScriptType.Array, GlyphScriptType.Long, elementType]),
            (_, values) => AccessArray(values, elementType)
        );
    }

    private void RegisterArrayElementAssignmentOperation(GlyphScriptType elementType)
    {
        _operations.Add(
            new OperationSignature(OperationKind.ArrayElementAssignment,
                [GlyphScriptType.Array, GlyphScriptType.Int, elementType, elementType]),
            (_, values) => AssignArrayElement(values, elementType)
        );

        _operations.Add(
            new OperationSignature(OperationKind.ArrayElementAssignment,
                [GlyphScriptType.Array, GlyphScriptType.Long, elementType, elementType]),
            (_, values) => AssignArrayElement(values, elementType)
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

        var errorMsgPtr = LlvmHelper.GetStringPtr(_builder, _indexOutOfBoundsGlobal);

        var printfFunc = LLVM.GetNamedFunction(_module, "printf");
        LLVM.BuildCall(_builder, printfFunc, [errorMsgPtr], "printfError");

        var defaultValue = CreateDefaultValue(elementType);
        LLVM.BuildBr(_builder, continueBlock);

        LLVM.PositionBuilderAtEnd(_builder, inBoundsBlock);

        var firstElementOffset = LLVM.ConstInt(LLVM.Int32Type(), 1, false); // Skip 1 int32
        var elementsPtr = LLVM.BuildGEP(_builder, arraySizePtr, [firstElementOffset], "elementsPtr");

        var llvmElementType = GetLlvmType(elementType);
        var typedElementsPtr = LLVM.BuildBitCast(_builder, elementsPtr,
            LLVM.PointerType(llvmElementType, 0), "typedElementsPtr");

        var elementPtr = LLVM.BuildGEP(_builder, typedElementsPtr, [index.Value], "elementPtr");

        var elementValue = LLVM.BuildLoad(_builder, elementPtr, "elementValue");
        LLVM.BuildBr(_builder, continueBlock);

        LLVM.PositionBuilderAtEnd(_builder, continueBlock);
        var resultPhi = LLVM.BuildPhi(_builder, llvmElementType, "result");

        var incomingValues = new[] { elementValue, defaultValue };
        var incomingBlocks = new[] { inBoundsBlock, outOfBoundsBlock };
        LLVM.AddIncoming(resultPhi, incomingValues, incomingBlocks, 2);

        return new GlyphScriptValue(resultPhi, elementType);
    }

    private GlyphScriptValue AssignArrayElement(IReadOnlyList<GlyphScriptValue> values, GlyphScriptType elementType)
    {
        var array = values[0];
        var index = values[1];
        var valueToAssign = values[2];

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
        var inBoundsBlock = LLVM.AppendBasicBlock(currentFunction, "assignInBounds");
        var outOfBoundsBlock = LLVM.AppendBasicBlock(currentFunction, "assignOutOfBounds");
        var continueBlock = LLVM.AppendBasicBlock(currentFunction, "assignContinue");

        LLVM.BuildCondBr(_builder, isOutOfBounds, outOfBoundsBlock, inBoundsBlock);

        LLVM.PositionBuilderAtEnd(_builder, outOfBoundsBlock);
        var errorMsgPtr = LlvmHelper.GetStringPtr(_builder, _indexOutOfBoundsGlobal);

        var printfFunc = LLVM.GetNamedFunction(_module, "printf");
        LLVM.BuildCall(_builder, printfFunc, [errorMsgPtr], "printfError");
        LLVM.BuildBr(_builder, continueBlock);

        LLVM.PositionBuilderAtEnd(_builder, inBoundsBlock);

        var firstElementOffset = LLVM.ConstInt(LLVM.Int32Type(), 1, false); // Skip 1 int32
        var elementsPtr = LLVM.BuildGEP(_builder, arraySizePtr, [firstElementOffset], "elementsPtr");

        var llvmElementType = GetLlvmType(elementType);
        var typedElementsPtr = LLVM.BuildBitCast(_builder, elementsPtr,
            LLVM.PointerType(llvmElementType, 0), "typedElementsPtr");

        var elementPtr = LLVM.BuildGEP(_builder, typedElementsPtr, [index.Value], "elementAssignPtr");

        LLVM.BuildStore(_builder, valueToAssign.Value, elementPtr);
        LLVM.BuildBr(_builder, continueBlock);

        LLVM.PositionBuilderAtEnd(_builder, continueBlock);

        return valueToAssign;
    }

    private GlyphScriptValue PrintArray(RuleContext context, IReadOnlyList<GlyphScriptValue> values)
    {
        var array = values[0];
        if (array.ArrayInfo == null)
            throw new InvalidOperationException("Array type info is missing");

        var arraySizePtr = LLVM.BuildBitCast(_builder, array.Value,
            LLVM.PointerType(LLVM.Int32Type(), 0), "arraySizePtr");

        var arraySize = LLVM.BuildLoad(_builder, arraySizePtr, "arraySize");

        var openBracketMsgPtr = LlvmHelper.GetStringPtr(_builder, _arrayOpenBracketMsg);
        var printfFunc = LLVM.GetNamedFunction(_module, "printf");
        LLVM.BuildCall(_builder, printfFunc, [openBracketMsgPtr], "printfOpenBracket");

        var currentFunction = LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_builder));
        var condBlock = LLVM.AppendBasicBlock(currentFunction, "forCond");
        var bodyBlock = LLVM.AppendBasicBlock(currentFunction, "forBody");
        var exitBlock = LLVM.AppendBasicBlock(currentFunction, "forExit");

        var counterPtr = LLVM.BuildAlloca(_builder, LLVM.Int32Type(), "i");
        LLVM.BuildStore(_builder, LLVM.ConstInt(LLVM.Int32Type(), 0, false), counterPtr);
        LLVM.BuildBr(_builder, condBlock);

        LLVM.PositionBuilderAtEnd(_builder, condBlock);
        var counter = LLVM.BuildLoad(_builder, counterPtr, "iValue");
        var continueLoop = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLT,
            counter, arraySize, "continueLoop");
        LLVM.BuildCondBr(_builder, continueLoop, bodyBlock, exitBlock);

        LLVM.PositionBuilderAtEnd(_builder, bodyBlock);

        var firstElementOffset = LLVM.ConstInt(LLVM.Int32Type(), 1, false); // Skip 1 int32
        var elementsPtr = LLVM.BuildGEP(_builder, arraySizePtr, [firstElementOffset], "elementsPtr");

        var elementType = array.ArrayInfo.ElementType;
        var llvmElementType = GetLlvmType(elementType);
        var typedElementsPtr = LLVM.BuildBitCast(_builder, elementsPtr,
            LLVM.PointerType(llvmElementType, 0), "typedElementsPtr");

        var elementPtr = LLVM.BuildGEP(_builder, typedElementsPtr, [counter], "elementPtr");

        var elementValue = LLVM.BuildLoad(_builder, elementPtr, "elementValue");

        var isFirstElement = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ,
            counter, LLVM.ConstInt(LLVM.Int32Type(), 0, false), "isFirstElement");
        var commaBlock = LLVM.AppendBasicBlock(currentFunction, "printComma");
        var printElementBlock = LLVM.AppendBasicBlock(currentFunction, "printElement");

        LLVM.BuildCondBr(_builder, isFirstElement, printElementBlock, commaBlock);

        LLVM.PositionBuilderAtEnd(_builder, commaBlock);
        var commaMsgPtr = LlvmHelper.GetStringPtr(_builder, _arrayCommaMsg);
        LLVM.BuildCall(_builder, printfFunc, [commaMsgPtr], "printfComma");
        LLVM.BuildBr(_builder, printElementBlock);

        LLVM.PositionBuilderAtEnd(_builder, printElementBlock);

        switch (elementType)
        {
            case GlyphScriptType.Int:
                var intFormatMsgPtr = LlvmHelper.GetStringPtr(_builder, _intFormatMsg);
                LLVM.BuildCall(_builder, printfFunc, [intFormatMsgPtr, elementValue], "printfInt");
                break;
            case GlyphScriptType.Long:
                var longFormatMsgPtr = LlvmHelper.GetStringPtr(_builder, _longFormatMsg);
                LLVM.BuildCall(_builder, printfFunc, [longFormatMsgPtr, elementValue], "printfLong");
                break;
            case GlyphScriptType.Float:
                // Convert float to double for printf
                var floatAsDouble = LLVM.BuildFPExt(_builder, elementValue, LLVM.DoubleType(), "floatAsDouble");
                var floatFormatMsgPtr = LlvmHelper.GetStringPtr(_builder, _floatFormatMsg);
                LLVM.BuildCall(_builder, printfFunc, [floatFormatMsgPtr, floatAsDouble], "printfFloat");
                break;
            case GlyphScriptType.Double:
                var doubleFormatMsgPtr = LlvmHelper.GetStringPtr(_builder, _doubleFormatMsg);
                LLVM.BuildCall(_builder, printfFunc, [doubleFormatMsgPtr, elementValue], "printfDouble");
                break;
            case GlyphScriptType.Boolean:
                var trueBlock = LLVM.AppendBasicBlock(currentFunction, "printTrue");
                var falseBlock = LLVM.AppendBasicBlock(currentFunction, "printFalse");
                var boolContinueBlock = LLVM.AppendBasicBlock(currentFunction, "boolContinue");

                LLVM.BuildCondBr(_builder, elementValue, trueBlock, falseBlock);

                LLVM.PositionBuilderAtEnd(_builder, trueBlock);
                var trueMsgPtr = LlvmHelper.GetStringPtr(_builder, _trueMsg);
                LLVM.BuildCall(_builder, printfFunc, [trueMsgPtr], "printfTrue");
                LLVM.BuildBr(_builder, boolContinueBlock);

                LLVM.PositionBuilderAtEnd(_builder, falseBlock);
                var falseMsgPtr = LlvmHelper.GetStringPtr(_builder, _falseMsg);
                LLVM.BuildCall(_builder, printfFunc, [falseMsgPtr], "printfFalse");
                LLVM.BuildBr(_builder, boolContinueBlock);

                LLVM.PositionBuilderAtEnd(_builder, boolContinueBlock);
                break;
            case GlyphScriptType.String:
                var stringMsgPtr = LlvmHelper.GetStringPtr(_builder, _stringFormatMsg);
                LLVM.BuildCall(_builder, printfFunc, [stringMsgPtr, elementValue], "printfString");
                break;
            default:
                var unknownMsgPtr = LlvmHelper.GetStringPtr(_builder, _unknownFormatMsg);
                LLVM.BuildCall(_builder, printfFunc, [unknownMsgPtr], "printfUnknown");
                break;
        }

        var nextCounter = LLVM.BuildAdd(_builder, counter,
            LLVM.ConstInt(LLVM.Int32Type(), 1, false), "nextI");
        LLVM.BuildStore(_builder, nextCounter, counterPtr);

        LLVM.BuildBr(_builder, condBlock);

        LLVM.PositionBuilderAtEnd(_builder, exitBlock);

        var closeBracketMsgPtr = LlvmHelper.GetStringPtr(_builder, _arrayCloseBracketMsg);
        LLVM.BuildCall(_builder, printfFunc, [closeBracketMsgPtr], "printfCloseBracket");

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
        var currentBlock = LLVM.GetInsertBlock(_builder);
        var currentFunction = LLVM.GetBasicBlockParent(currentBlock);
        var entryBlock = LLVM.GetEntryBasicBlock(currentFunction);
        var firstInstruction = LLVM.GetFirstInstruction(entryBlock);

        LLVM.PositionBuilder(_builder, entryBlock, firstInstruction);

        var tempAlloca = LLVM.BuildAlloca(_builder, type, "sizeofTemp");

        var nextPtr = LLVM.BuildGEP(_builder, tempAlloca,
            [LLVM.ConstInt(LLVM.Int32Type(), 1, false)], "nextPtr");

        var ptr1 = LLVM.BuildPtrToInt(_builder, nextPtr, LLVM.Int64Type(), "ptr1AsInt");
        var ptr0 = LLVM.BuildPtrToInt(_builder, tempAlloca, LLVM.Int64Type(), "ptr0AsInt");

        var size = LLVM.BuildSub(_builder, ptr1, ptr0, "sizeof");

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
