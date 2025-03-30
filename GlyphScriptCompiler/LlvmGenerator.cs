using System.Runtime.InteropServices;
using System.Text;
using LLVMSharp;

namespace GlyphScriptCompiler;

public class LlvmGenerator
{
    public void GenerateSampleProgram()
    {
        var module = LLVM.ModuleCreateWithName("main_module");

        // Create format strings as global constants
        var i8Type = LLVM.Int8Type();
        var i8PtrType = LLVM.PointerType(i8Type, 0);

        var printfFormatStr = CreateStringConstant(module, "strp", "%d\n\0");

        var scanfFormatStr = CreateStringConstant(module, "strs", "%d\0");

        // Declare external functions (printf and scanf)
        var printfParamTypes = new LLVMTypeRef[] { i8PtrType };
        var printfType = LLVM.FunctionType(LLVM.Int32Type(),
            printfParamTypes, true);
        var printfFunc = LLVM.AddFunction(module, "printf", printfType);

        var scanfType = LLVM.FunctionType(LLVM.Int32Type(),
            printfParamTypes, true);
        var scanfFunc = LLVM.AddFunction(module, "scanf", scanfType);

        // Create main function
        var mainRetType = LLVM.Int32Type();
        var mainFuncType = LLVM.FunctionType(mainRetType, Array.Empty<LLVMTypeRef>(), false);
        var mainFunc = LLVM.AddFunction(module, "main", mainFuncType);
        LLVM.SetFunctionCallConv(mainFunc, (uint)LLVMCallConv.LLVMCCallConv);
        LLVM.AddAttributeAtIndex(mainFunc, LLVMAttributeIndex.LLVMAttributeFunctionIndex,
            CreateAttribute("nounwind"));

        // Create main function body
        var entryBlock = LLVM.AppendBasicBlock(mainFunc, "entry");
        var builder = LLVM.CreateBuilder();
        LLVM.PositionBuilderAtEnd(builder, entryBlock);


        var x = LLVM.BuildAlloca(builder, LLVM.Int32Type(), "x");

        LLVM.BuildStore(builder, LLVM.ConstInt(LLVM.Int32Type(), 42, false), x);

        // Load value of x for printf
        var loadedX = LLVM.BuildLoad(builder, x, string.Empty);

        // Get pointer to the printf format string
        var printfFormatPtr = GetStringPtr(builder, printfFormatStr);

        // Call printf with the loaded value
        LLVMValueRef[] printfArgs = [printfFormatPtr, loadedX];
        LLVM.BuildCall(builder, printfFunc, printfArgs, string.Empty);

        // Get pointer to the scanf format string
        var scanfFormatPtr = GetStringPtr(builder, scanfFormatStr);

        // Call scanf, storing result into x
        var scanfArgs = new LLVMValueRef[] { scanfFormatPtr, x };
        LLVM.BuildCall(builder, scanfFunc, scanfArgs, string.Empty);

        // Return 0
        LLVM.BuildRet(builder, LLVM.ConstInt(LLVM.Int32Type(), 0, false));

        // Print the generated IR
        // var codePtr = LLVM.PrintModuleToString(module);
        // var llvmCode = Marshal.PtrToStringAnsi(codePtr);
        // Marshal.FreeHGlobal(codePtr);
        //
        // Console.WriteLine(llvmCode);

        LLVM.DumpModule(module);
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
