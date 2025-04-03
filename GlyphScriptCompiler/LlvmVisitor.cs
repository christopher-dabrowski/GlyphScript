using GlyphScriptCompiler.Antlr;
using LLVMSharp;

namespace GlyphScriptCompiler;

public enum TypeKind
{
    Int,
    Long,
    Float,
    Double
}

public sealed class LlvmVisitor : GlyphScriptBaseVisitor<object?>, IDisposable
{
    public LLVMModuleRef LlvmModule { get; }
    private readonly LLVMBuilderRef _llvmBuilder = LLVM.CreateBuilder();

    private readonly Dictionary<string, (LLVMValueRef Value, TypeKind Type)> _variables = [];

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

    public override object? VisitDeclaration(GlyphScriptParser.DeclarationContext context)
    {
        var type = GetTypeFromContext(context.type());
        var id = context.ID().GetText();
        var value = (LLVMValueRef)(VisitImmediateValue(context.immediateValue()) ??
            throw new InvalidOperationException("Failed to create immediate value"));

        if (_variables.ContainsKey(id))
        {
            throw new InvalidOperationException($"Variable '{id}' is already defined.");
        }

        var llvmType = GetLlvmType(type);
        var variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);

        // Ensure the value type matches the variable type
        if (LLVM.GetTypeKind(LLVM.TypeOf(value)) != LLVM.GetTypeKind(llvmType))
        {
            throw new InvalidOperationException($"Type mismatch in declaration of variable '{id}'");
        }

        LLVM.BuildStore(_llvmBuilder, value, variable);

        _variables[id] = (variable, type);
        return null;
    }

    public override object? VisitAssignment(GlyphScriptParser.AssignmentContext context)
    {
        var id = context.ID().GetText();
        var value = (LLVMValueRef)(VisitImmediateValue(context.immediateValue()) ??
            throw new InvalidOperationException("Failed to create immediate value"));

        if (!_variables.TryGetValue(id, out var variable))
        {
            throw new InvalidOperationException($"Variable '{id}' is not defined.");
        }

        var valueType = GetTypeFromImmediateValue(context.immediateValue());
        if (valueType != variable.Type)
        {
            throw new InvalidOperationException(
                $"Type mismatch: Cannot assign {valueType} value to variable of type {variable.Type}");
        }

        // Ensure the value type matches the variable type
        var llvmType = GetLlvmType(variable.Type);
        if (LLVM.GetTypeKind(LLVM.TypeOf(value)) != LLVM.GetTypeKind(llvmType))
        {
            throw new InvalidOperationException($"Type mismatch in assignment to variable '{id}'");
        }

        LLVM.BuildStore(_llvmBuilder, value, variable.Value);
        return null;
    }

    public override object? VisitPrint(GlyphScriptParser.PrintContext context)
    {
        var id = context.ID().GetText();

        if (!_variables.TryGetValue(id, out var variable))
        {
            throw new InvalidOperationException($"Variable '{id}' is not defined.");
        }

        var printfFormatStr = GetPrintfFormatString(variable.Type);
        var printfFunc = LLVM.GetNamedFunction(LlvmModule, "printf");

        var value = LLVM.BuildLoad(_llvmBuilder, variable.Value, string.Empty);

        // Convert float to double if needed
        if (variable.Type == TypeKind.Float)
        {
            value = LLVM.BuildFPExt(_llvmBuilder, value, LLVM.DoubleType(), string.Empty);
        }

        var args = new[] { GetStringPtr(_llvmBuilder, printfFormatStr), value };

        LLVM.BuildCall(_llvmBuilder, printfFunc, args, string.Empty);
        return null;
    }

    public override object? VisitRead(GlyphScriptParser.ReadContext context)
    {
        var id = context.ID().GetText();

        if (!_variables.TryGetValue(id, out var variable))
        {
            throw new InvalidOperationException($"Variable '{id}' is not defined.");
        }

        var scanfFormatStr = GetScanfFormatString(variable.Type);
        var scanfFunc = LLVM.GetNamedFunction(LlvmModule, "scanf");

        var args = new[] { GetStringPtr(_llvmBuilder, scanfFormatStr), variable.Value };
        LLVM.BuildCall(_llvmBuilder, scanfFunc, args, string.Empty);
        return null;
    }

    public override object? VisitImmediateValue(GlyphScriptParser.ImmediateValueContext context)
    {
        if (context.INT_LITERAL() != null)
        {
            var value = int.Parse(context.INT_LITERAL().GetText());
            return LLVM.ConstInt(LLVM.Int32Type(), (ulong)value, false);
        }
        if (context.LONG_LITERAL() != null)
        {
            var value = long.Parse(context.LONG_LITERAL().GetText().TrimEnd('l', 'L'));
            return LLVM.ConstInt(LLVM.Int64Type(), (ulong)value, false);
        }
        if (context.FLOAT_LITERAL() != null)
        {
            var value = float.Parse(context.FLOAT_LITERAL().GetText().TrimEnd('f', 'F'));
            return LLVM.ConstReal(LLVM.FloatType(), value);
        }
        if (context.DOUBLE_LITERAL() != null)
        {
            var value = double.Parse(context.DOUBLE_LITERAL().GetText().TrimEnd('d', 'D'));
            return LLVM.ConstReal(LLVM.DoubleType(), value);
        }
        throw new InvalidOperationException("Invalid immediate value");
    }

    private static TypeKind GetTypeFromImmediateValue(GlyphScriptParser.ImmediateValueContext context)
    {
        if (context.INT_LITERAL() != null) return TypeKind.Int;
        if (context.LONG_LITERAL() != null) return TypeKind.Long;
        if (context.FLOAT_LITERAL() != null) return TypeKind.Float;
        if (context.DOUBLE_LITERAL() != null) return TypeKind.Double;
        throw new InvalidOperationException("Invalid immediate value");
    }

    private static TypeKind GetTypeFromContext(GlyphScriptParser.TypeContext context)
    {
        if (context.INT() != null) return TypeKind.Int;
        if (context.LONG() != null) return TypeKind.Long;
        if (context.FLOAT() != null) return TypeKind.Float;
        if (context.DOUBLE() != null) return TypeKind.Double;
        throw new InvalidOperationException("Invalid type");
    }

    private static LLVMTypeRef GetLlvmType(TypeKind type)
    {
        return type switch
        {
            TypeKind.Int => LLVM.Int32Type(),
            TypeKind.Long => LLVM.Int64Type(),
            TypeKind.Float => LLVM.FloatType(),
            TypeKind.Double => LLVM.DoubleType(),
            _ => throw new InvalidOperationException($"Unsupported type: {type}")
        };
    }

    private void SetupGlobalFunctions(LLVMModuleRef module)
    {
        var i8Type = LLVM.Int8Type();
        var i8PtrType = LLVM.PointerType(i8Type, 0);

        // Format strings for different types
        CreateStringConstant(module, "strp_int", "%d\n\0");
        CreateStringConstant(module, "strp_long", "%ld\n\0");
        CreateStringConstant(module, "strp_float", "%f\n\0");
        CreateStringConstant(module, "strp_double", "%lf\n\0");

        CreateStringConstant(module, "strs_int", "%d\0");
        CreateStringConstant(module, "strs_long", "%ld\0");
        CreateStringConstant(module, "strs_float", "%f\0");
        CreateStringConstant(module, "strs_double", "%lf\0");

        // Declare external functions (printf and scanf)
        LLVMTypeRef[] printfParamTypes = [i8PtrType];
        var printfType = LLVM.FunctionType(LLVM.Int32Type(), printfParamTypes, true);
        LLVM.AddFunction(module, "printf", printfType);

        var scanfType = LLVM.FunctionType(LLVM.Int32Type(), printfParamTypes, true);
        LLVM.AddFunction(module, "scanf", scanfType);
    }

    private LLVMValueRef GetPrintfFormatString(TypeKind type)
    {
        return type switch
        {
            TypeKind.Int => LLVM.GetNamedGlobal(LlvmModule, "strp_int"),
            TypeKind.Long => LLVM.GetNamedGlobal(LlvmModule, "strp_long"),
            TypeKind.Float => LLVM.GetNamedGlobal(LlvmModule, "strp_float"),
            TypeKind.Double => LLVM.GetNamedGlobal(LlvmModule, "strp_double"),
            _ => throw new InvalidOperationException($"Unsupported type for printf: {type}")
        };
    }

    private LLVMValueRef GetScanfFormatString(TypeKind type)
    {
        return type switch
        {
            TypeKind.Int => LLVM.GetNamedGlobal(LlvmModule, "strs_int"),
            TypeKind.Long => LLVM.GetNamedGlobal(LlvmModule, "strs_long"),
            TypeKind.Float => LLVM.GetNamedGlobal(LlvmModule, "strs_float"),
            TypeKind.Double => LLVM.GetNamedGlobal(LlvmModule, "strs_double"),
            _ => throw new InvalidOperationException($"Unsupported type for scanf: {type}")
        };
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

    public void Dispose()
    {
        LLVM.DisposeBuilder(_llvmBuilder);
    }
}
