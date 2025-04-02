using LLVMSharp;

namespace GlyphScriptCompiler;

public sealed class LlvmVisitor : GlyphScriptBaseVisitor<object?>, IDisposable
{
    public LLVMModuleRef LlvmModule { get; }
    private readonly LLVMBuilderRef _llvmBuilder = LLVM.CreateBuilder();

    private readonly Dictionary<string, LLVMValueRef> _variables = [];

    public LlvmVisitor(LLVMModuleRef llvmModule)
    {
        LlvmModule = llvmModule;
    }


    public override object? VisitProgram(GlyphScriptParser.ProgramContext context)
    {
        SetupGlobalFunctions(LlvmModule);
        CreateMain(LlvmModule, _llvmBuilder);

        var result = VisitChildren(context);
        LLVM.BuildRet(_llvmBuilder, LLVM.ConstInt(LLVM.Int32Type(), 0, false));

        return result;
    }

    public override object? VisitAssign(GlyphScriptParser.AssignContext context)
    {
        // TODO: Interpret expression when they are added
        var id = context.IDENTIFIER().GetText();
        // TODO: Validate type
        var value = int.Parse(context.INT().GetText());
        var llvmValue = LLVM.ConstInt(LLVM.Int32Type(), (ulong)value, false);

        if (!_variables.ContainsKey(id))
        {
            _variables[id] = LLVM.BuildAlloca(_llvmBuilder, LLVM.Int32Type(), id);
        }

        var variable = _variables[id];
        LLVM.BuildStore(_llvmBuilder, llvmValue, variable);

        return null;
    }

    public void Dispose()
    {
        LLVM.DisposeBuilder(_llvmBuilder);
    }

    private void CreateMain(LLVMModuleRef module, LLVMBuilderRef builder)
    {
        var mainRetType = LLVM.Int32Type();
        var mainFuncType = LLVM.FunctionType(mainRetType, [], false);
        var mainFunc = LLVM.AddFunction(module, "main", mainFuncType);
        LLVM.SetFunctionCallConv(mainFunc, (uint)LLVMCallConv.LLVMCCallConv);
        LLVM.AddAttributeAtIndex(mainFunc, LLVMAttributeIndex.LLVMAttributeFunctionIndex,
            CreateAttribute("nounwind"));

        var entryBlock = LLVM.AppendBasicBlock(mainFunc, "entry");
        LLVM.PositionBuilderAtEnd(builder, entryBlock);
    }

    private void SetupGlobalFunctions(LLVMModuleRef module)
    {
        var i8Type = LLVM.Int8Type();
        var i8PtrType = LLVM.PointerType(i8Type, 0);

        var printfFormatStr = CreateStringConstant(module, "strp", "%d\n\0");

        var scanfFormatStr = CreateStringConstant(module, "strs", "%d\0");

        // Declare external functions (printf and scanf)
        LLVMTypeRef[] printfParamTypes = [i8PtrType];
        var printfType = LLVM.FunctionType(LLVM.Int32Type(),
            printfParamTypes, true);
        var printfFunc = LLVM.AddFunction(module, "printf", printfType);

        var scanfType = LLVM.FunctionType(LLVM.Int32Type(),
            printfParamTypes, true);
        var scanfFunc = LLVM.AddFunction(module, "scanf", scanfType);
    }

    private static LLVMValueRef CreateStringConstant(
        LLVMModuleRef module,
        string name,
        string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var length = (uint)bytes.Length;

        var i8Type = LLVM.Int8Type();
        var arrayType = LLVM.ArrayType(i8Type, length);

        var global = LLVM.AddGlobal(module, arrayType, name);
        LLVM.SetLinkage(global, LLVMLinkage.LLVMExternalLinkage);
        LLVM.SetGlobalConstant(global, true);

        var stringConstant = LLVM.ConstString(value, (uint)value.Length, true);
        LLVM.SetInitializer(global, stringConstant);

        return global;
    }

    private static LLVMValueRef GetStringPtr(LLVMBuilderRef builder, LLVMValueRef stringGlobal)
    {
        LLVMValueRef[] indices =
        [
            LLVM.ConstInt(LLVM.Int32Type(), 0, false),
            LLVM.ConstInt(LLVM.Int32Type(), 0, false)
        ];

        return LLVM.BuildGEP(builder, stringGlobal, indices, string.Empty);
    }

    private static LLVMAttributeRef CreateAttribute(string name)
    {
        return LLVM.CreateEnumAttribute(
            LLVM.GetGlobalContext(),
            LLVM.GetEnumAttributeKindForName(name, name.Length), 0);
    }
}
