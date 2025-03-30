using System.Runtime.InteropServices;
using System.Text;
using LLVMSharp;

namespace GlyphScriptCompiler;

public class LlvmGenerator
{
    public void GenerateSampleProgram()
    {
        // Initialize LLVM
        // LLVM.LinkInMCJIT();
        // LLVM.InitializeX86TargetMC();
        // LLVM.InitializeX86Target();
        // LLVM.InitializeX86TargetInfo();
        // LLVM.InitializeX86AsmParser();
        // LLVM.InitializeX86AsmPrinter();

        // Create module and context
        LLVMContextRef context = LLVM.ContextCreate();
        LLVMModuleRef module = LLVM.ModuleCreateWithNameInContext("main_module", context);

        // Create format strings as global constants
        LLVMTypeRef i8Type = LLVM.Int8TypeInContext(context);
        LLVMTypeRef i8PtrType = LLVM.PointerType(i8Type, 0);

        // Create "%d\n\0" string constant
        LLVMValueRef printfFormatStr = CreateStringConstant(context, module, "strp", "%d\n\0");

        // Create "%d\0" string constant
        LLVMValueRef scanfFormatStr = CreateStringConstant(context, module, "strs", "%d\0");

        // Declare external functions (printf and scanf)
        LLVMTypeRef[] printfParamTypes = new LLVMTypeRef[] { i8PtrType };
        LLVMTypeRef printfType = LLVM.FunctionType(LLVM.Int32TypeInContext(context),
            printfParamTypes, true);
        LLVMValueRef printfFunc = LLVM.AddFunction(module, "printf", printfType);

        LLVMTypeRef scanfType = LLVM.FunctionType(LLVM.Int32TypeInContext(context),
            printfParamTypes, true);
        LLVMValueRef scanfFunc = LLVM.AddFunction(module, "scanf", scanfType);
        //
        // Create main function
        LLVMTypeRef mainRetType = LLVM.Int32TypeInContext(context);
        LLVMTypeRef mainFuncType = LLVM.FunctionType(mainRetType, Array.Empty<LLVMTypeRef>(), false);
        LLVMValueRef mainFunc = LLVM.AddFunction(module, "main", mainFuncType);
        LLVM.SetFunctionCallConv(mainFunc, (uint)LLVMCallConv.LLVMCCallConv);
        LLVM.AddAttributeAtIndex(mainFunc, LLVMAttributeIndex.LLVMAttributeFunctionIndex,
            CreateAttribute(context, "nounwind"));

        // Create main function body
        LLVMBasicBlockRef entryBlock = LLVM.AppendBasicBlock(mainFunc, "entry");
        LLVMBuilderRef builder = LLVM.CreateBuilder();
        LLVM.PositionBuilderAtEnd(builder, entryBlock);


        // Allocate x variable on the stack
        LLVMValueRef x = LLVM.BuildAlloca(builder, LLVM.Int32TypeInContext(context), "x");

        // Store initial value 42 to x
        LLVM.BuildStore(builder, LLVM.ConstInt(LLVM.Int32TypeInContext(context), 42, false), x);

        // Load value of x for printf
        LLVMValueRef loadedX = LLVM.BuildLoad(builder, x, "");

        // Get pointer to the printf format string
        LLVMValueRef printfFormatPtr = GetStringPtr(builder, printfFormatStr);

        // Call printf with the loaded value
        LLVMValueRef[] printfArgs = new LLVMValueRef[] { printfFormatPtr, loadedX };
        LLVM.BuildCall(builder, printfFunc, printfArgs, "");

        // Get pointer to the scanf format string
        LLVMValueRef scanfFormatPtr = GetStringPtr(builder, scanfFormatStr);

        // Call scanf, storing result into x
        LLVMValueRef[] scanfArgs = new LLVMValueRef[] { scanfFormatPtr, x };
        LLVM.BuildCall(builder, scanfFunc, scanfArgs, "");

        // Return 0
        LLVM.BuildRet(builder, LLVM.ConstInt(LLVM.Int32TypeInContext(context), 0, false));

        // Print the generated IR
        // var codePtr = LLVM.PrintModuleToString(module);
        // var llvmCode = Marshal.PtrToStringAnsi(codePtr);
        // Marshal.FreeHGlobal(codePtr);
        //
        // Console.WriteLine(llvmCode);

        LLVM.DumpModule(module);
    }

    private static LLVMValueRef CreateStringConstant(
        LLVMContextRef context,
        LLVMModuleRef module,
        string name,
        string value)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
        uint length = (uint)bytes.Length;

        LLVMTypeRef i8Type = LLVM.Int8TypeInContext(context);
        LLVMTypeRef arrayType = LLVM.ArrayType(i8Type, length);

        LLVMValueRef global = LLVM.AddGlobal(module, arrayType, name);
        LLVM.SetLinkage(global, LLVMLinkage.LLVMExternalLinkage);
        LLVM.SetGlobalConstant(global, true);

        var stringConstant = LLVM.ConstStringInContext(context, value, (uint)value.Length, true);
        LLVM.SetInitializer(global, stringConstant);

        return global;
    }

    private static LLVMValueRef GetStringPtr(LLVMBuilderRef builder, LLVMValueRef stringGlobal)
    {
        LLVMValueRef[] indices = new LLVMValueRef[]
        {
            LLVM.ConstInt(LLVM.Int32Type(), 0, false),
            LLVM.ConstInt(LLVM.Int32Type(), 0, false)
        };

        return LLVM.BuildGEP(builder, stringGlobal, indices, "");
    }

    private static LLVMAttributeRef CreateAttribute(LLVMContextRef context, string name)
    {
        return LLVM.CreateEnumAttribute(context,
            LLVM.GetEnumAttributeKindForName(name, name.Length), 0);
    }
}
