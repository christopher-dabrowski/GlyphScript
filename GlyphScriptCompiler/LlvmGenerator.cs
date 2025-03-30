using LLVMSharp.Interop;

namespace GlyphScriptCompiler;

public class LlvmGenerator
{
    private static unsafe LLVMValueRef CreateStringConstant(LLVMContextRef context, LLVMModuleRef module,
        string name, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var length = (uint)bytes.Length;

        LLVMTypeRef i8Type = LLVM.Int8TypeInContext(context);
        LLVMTypeRef arrayType = LLVM.ArrayType(i8Type, length);

        fixed (byte* namePtr = System.Text.Encoding.UTF8.GetBytes(name + "\0"))
        {
            LLVMValueRef global = LLVM.AddGlobal(module, arrayType, (sbyte*)namePtr);
            LLVM.SetLinkage(global, LLVMLinkage.LLVMExternalLinkage);
            LLVM.SetGlobalConstant(global, 1);

            fixed (byte* valuePtr = System.Text.Encoding.UTF8.GetBytes(value))
            {
                LLVMValueRef stringConstant = LLVM.ConstStringInContext(context, (sbyte*)valuePtr, (uint)value.Length, 0);
                LLVM.SetInitializer(global, stringConstant);
            }

            return global;
        }
    }

    private static unsafe LLVMValueRef GetStringPtr(LLVMBuilderRef builder, LLVMValueRef stringGlobal)
    {
        LLVMOpaqueValue** indices = stackalloc LLVMOpaqueValue*[2];
        indices[0] = LLVM.ConstInt(LLVM.Int32Type(), 0, 0);
        indices[1] = LLVM.ConstInt(LLVM.Int32Type(), 0, 0);

        fixed (byte* emptyPtr = System.Text.Encoding.UTF8.GetBytes("\0"))
        {
            return LLVM.BuildGEP2(
                builder,
                LLVM.GetElementType(LLVM.TypeOf(stringGlobal)),
                stringGlobal,
                indices,
                2u,
                (sbyte*)emptyPtr);
        }
    }

    private static unsafe LLVMAttributeRef CreateAttribute(LLVMContextRef context, string name)
    {
        fixed (byte* namePtr = System.Text.Encoding.UTF8.GetBytes(name))
            return LLVM.CreateEnumAttribute(context,
                LLVM.GetEnumAttributeKindForName((sbyte*)namePtr, (UIntPtr)name.Length), 0);
    }
}
