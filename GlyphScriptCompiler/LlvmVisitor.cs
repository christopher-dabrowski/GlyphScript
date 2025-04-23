using GlyphScriptCompiler.Antlr;
using GlyphScriptCompiler.SyntaxErrors;
using LLVMSharp;

namespace GlyphScriptCompiler;

public enum TypeKind
{
    Int,
    Long,
    Float,
    Double,
    String
}

public sealed class LlvmVisitor : GlyphScriptBaseVisitor<object?>, IDisposable
{
    public LLVMModuleRef LlvmModule { get; }
    private readonly LLVMBuilderRef _llvmBuilder = LLVM.CreateBuilder();

    private readonly Dictionary<string, (LLVMValueRef Value, TypeKind Type)> _variables = [];
    private int _stringConstCounter = 0;

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
        return VisitChildren(context);
    }

    public override object? VisitDefaultDeclaration(GlyphScriptParser.DefaultDeclarationContext context)
    {
        var type = GetTypeFromContext(context.type());
        var id = context.ID().GetText();

        var value = GetDefaultValueForType(type);

        if (_variables.ContainsKey(id))
        {
            throw new InvalidOperationException($"Variable '{id}' is already defined.");
        }

        var llvmType = GetLlvmType(type);
        var variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);

        LLVM.BuildStore(_llvmBuilder, value, variable);

        _variables[id] = (variable, type);
        return null;
    }

    public override object? VisitInitializingDeclaration(GlyphScriptParser.InitializingDeclarationContext context)
    {
        var type = GetTypeFromContext(context.type());
        var id = context.ID().GetText();

        var value = (LLVMValueRef)(Visit(context.expression()) ??
            throw new InvalidOperationException("Failed to create expression"));

        if (_variables.ContainsKey(id))
            throw new DuplicateVariableDeclarationException(context) { VariableName = id };

        var llvmType = GetLlvmType(type);
        var variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);

        var valueTypeKind = LLVM.GetTypeKind(LLVM.TypeOf(value));
        if (valueTypeKind != LLVM.GetTypeKind(llvmType))
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
        var value = (LLVMValueRef)(Visit(context.expression()) ??
            throw new InvalidOperationException("Failed to create expression"));

        if (!_variables.TryGetValue(id, out var variable))
            throw new UndefinedVariableUsageException(context) { VariableName = id };

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
        var value = (LLVMValueRef)(Visit(context.expression()) ??
            throw new InvalidOperationException("Failed to create expression"));

        var valueType = LLVM.TypeOf(value);
        var valueKind = LLVM.GetTypeKind(valueType);
        var printfFormatStr = valueKind switch
        {
            LLVMTypeKind.LLVMIntegerTypeKind when LLVM.GetIntTypeWidth(valueType) == 32 => GetPrintfFormatString(TypeKind.Int),
            LLVMTypeKind.LLVMIntegerTypeKind when LLVM.GetIntTypeWidth(valueType) == 64 => GetPrintfFormatString(TypeKind.Long),
            LLVMTypeKind.LLVMFloatTypeKind => GetPrintfFormatString(TypeKind.Float),
            LLVMTypeKind.LLVMDoubleTypeKind => GetPrintfFormatString(TypeKind.Double),
            LLVMTypeKind.LLVMPointerTypeKind => GetPrintfFormatString(TypeKind.String),
            _ => throw new InvalidOperationException($"Unsupported type for printing: {valueKind}")
        };

        var printfFunc = LLVM.GetNamedFunction(LlvmModule, "printf");

        // To print float we need to convert it to double first
        if (valueKind == LLVMTypeKind.LLVMFloatTypeKind)
            value = LLVM.BuildFPExt(_llvmBuilder, value, LLVM.DoubleType(), string.Empty);

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
        if (context.STRING_LITERAL() != null)
        {
            // Extract the string content by removing quotes
            var text = context.STRING_LITERAL().GetText();
            var value = text.Substring(1, text.Length - 2); // Remove quotes

            // Create a unique name for the string constant
            var stringConstName = $"str_{_stringConstCounter++}";
            var stringGlobal = CreateStringConstant(LlvmModule, stringConstName, value);

            // Return the pointer to the string
            return GetStringPtr(_llvmBuilder, stringGlobal);
        }
        throw new InvalidOperationException("Invalid immediate value");
    }

    public override object? VisitParenthesisExp(GlyphScriptParser.ParenthesisExpContext context)
    {
        return Visit(context.expression());
    }

    public override object? VisitMulDivExp(GlyphScriptParser.MulDivExpContext context)
    {
        var left = (LLVMValueRef)(Visit(context.expression(0)) ?? throw new InvalidOperationException("Unable to resolve the expression"));
        var right = (LLVMValueRef)(Visit(context.expression(1)) ?? throw new InvalidOperationException("Unable to resolve the expression"));

        var leftType = LLVM.TypeOf(left);
        var rightType = LLVM.TypeOf(right);
        var leftKind = LLVM.GetTypeKind(leftType);
        var rightKind = LLVM.GetTypeKind(rightType);

        if (leftKind == LLVMTypeKind.LLVMIntegerTypeKind && rightKind == LLVMTypeKind.LLVMIntegerTypeKind)
        {
            var leftWidth = LLVM.GetIntTypeWidth(leftType);
            var rightWidth = LLVM.GetIntTypeWidth(rightType);
            if (leftWidth != rightWidth)
            {
                if (leftWidth == 32 && rightWidth == 64)
                {
                    left = LLVM.BuildSExt(_llvmBuilder, left, LLVM.Int64Type(), "sext_to_long");
                    leftType = LLVM.Int64Type();
                }
                else if (leftWidth == 64 && rightWidth == 32)
                {
                    right = LLVM.BuildSExt(_llvmBuilder, right, LLVM.Int64Type(), "sext_to_long");
                    rightType = LLVM.Int64Type();
                }
                else
                {
                    throw new InvalidOperationException("Unsupported integer width combination in multiplication/division operation");
                }
            }
        }
        else if (leftKind != rightKind)
        {
            if (leftKind is LLVMTypeKind.LLVMIntegerTypeKind && rightKind is LLVMTypeKind.LLVMFloatTypeKind or LLVMTypeKind.LLVMDoubleTypeKind)
            {
                left = LLVM.BuildSIToFP(_llvmBuilder, left, rightType, "promote_to_float");
                leftType = rightType;
            }
            else if (rightKind is LLVMTypeKind.LLVMIntegerTypeKind && leftKind is LLVMTypeKind.LLVMFloatTypeKind or LLVMTypeKind.LLVMDoubleTypeKind)
            {
                right = LLVM.BuildSIToFP(_llvmBuilder, right, leftType, "promote_to_float");
                rightType = leftType;
            }
            else
            {
                throw new InvalidOperationException("Type mismatch in multiplication/division operation");
            }
        }

        var isFloatingPoint = LLVM.GetTypeKind(leftType) is LLVMTypeKind.LLVMFloatTypeKind or LLVMTypeKind.LLVMDoubleTypeKind;

        return context.MULTIPLICATION_SYMBOL() != null
            ? (isFloatingPoint
                ? LLVM.BuildFMul(_llvmBuilder, left, right, "fmul")
                : LLVM.BuildMul(_llvmBuilder, left, right, "mul"))
            : (isFloatingPoint
                ? LLVM.BuildFDiv(_llvmBuilder, left, right, "fdiv")
                : LLVM.BuildSDiv(_llvmBuilder, left, right, "div"));
    }

    public override object? VisitAddSubExp(GlyphScriptParser.AddSubExpContext context)
    {
        var left = (LLVMValueRef)(Visit(context.expression(0)) ?? throw new InvalidOperationException("Unable to resolve the expression"));
        var right = (LLVMValueRef)(Visit(context.expression(1)) ?? throw new InvalidOperationException("Unable to resolve the expression"));

        var leftType = LLVM.TypeOf(left);
        var rightType = LLVM.TypeOf(right);
        var leftKind = LLVM.GetTypeKind(leftType);
        var rightKind = LLVM.GetTypeKind(rightType);

        // Handle int/long promotion
        if (leftKind == LLVMTypeKind.LLVMIntegerTypeKind && rightKind == LLVMTypeKind.LLVMIntegerTypeKind)
        {
            var leftWidth = LLVM.GetIntTypeWidth(leftType);
            var rightWidth = LLVM.GetIntTypeWidth(rightType);
            if (leftWidth != rightWidth)
            {
                if (leftWidth == 32 && rightWidth == 64)
                {
                    left = LLVM.BuildSExt(_llvmBuilder, left, LLVM.Int64Type(), "sext_to_long");
                    leftType = LLVM.Int64Type();
                }
                else if (leftWidth == 64 && rightWidth == 32)
                {
                    right = LLVM.BuildSExt(_llvmBuilder, right, LLVM.Int64Type(), "sext_to_long");
                    rightType = LLVM.Int64Type();
                }
                else
                {
                    throw new InvalidOperationException("Unsupported integer width combination in addition/subtraction operation");
                }
            }
        }
        else if (leftKind != rightKind)
        {
            if (leftKind is LLVMTypeKind.LLVMIntegerTypeKind && rightKind is LLVMTypeKind.LLVMFloatTypeKind or LLVMTypeKind.LLVMDoubleTypeKind)
            {
                left = LLVM.BuildSIToFP(_llvmBuilder, left, rightType, "promote_to_float");
                leftType = rightType;
            }
            else if (rightKind is LLVMTypeKind.LLVMIntegerTypeKind && leftKind is LLVMTypeKind.LLVMFloatTypeKind or LLVMTypeKind.LLVMDoubleTypeKind)
            {
                right = LLVM.BuildSIToFP(_llvmBuilder, right, leftType, "promote_to_float");
                rightType = leftType;
            }
            else
            {
                throw new InvalidOperationException("Type mismatch in addition/subtraction operation");
            }
        }

        var isFloatingPoint = LLVM.GetTypeKind(leftType) is LLVMTypeKind.LLVMFloatTypeKind or LLVMTypeKind.LLVMDoubleTypeKind;

        return context.ADDITION_SYMBOL() != null
            ? (isFloatingPoint
                ? LLVM.BuildFAdd(_llvmBuilder, left, right, "fadd")
                : LLVM.BuildAdd(_llvmBuilder, left, right, "add"))
            : (isFloatingPoint
                ? LLVM.BuildFSub(_llvmBuilder, left, right, "fsub")
                : LLVM.BuildSub(_llvmBuilder, left, right, "sub"));
    }

    public override object? VisitPowerExp(GlyphScriptParser.PowerExpContext context)
    {
        var left = (LLVMValueRef)(Visit(context.expression(0)) ?? throw new InvalidOperationException("Unable to resolve the expression"));
        var right = (LLVMValueRef)(Visit(context.expression(1)) ?? throw new InvalidOperationException("Unable to resolve the expression"));

        var leftType = LLVM.TypeOf(left);
        var rightType = LLVM.TypeOf(right);
        var leftKind = LLVM.GetTypeKind(leftType);
        var rightKind = LLVM.GetTypeKind(rightType);

        // Handle int/long promotion
        if (leftKind == LLVMTypeKind.LLVMIntegerTypeKind && rightKind == LLVMTypeKind.LLVMIntegerTypeKind)
        {
            var leftWidth = LLVM.GetIntTypeWidth(leftType);
            var rightWidth = LLVM.GetIntTypeWidth(rightType);
            if (leftWidth != rightWidth)
            {
                if (leftWidth == 32 && rightWidth == 64)
                {
                    left = LLVM.BuildSExt(_llvmBuilder, left, LLVM.Int64Type(), "sext_to_long");
                    leftType = LLVM.Int64Type();
                }
                else if (leftWidth == 64 && rightWidth == 32)
                {
                    right = LLVM.BuildSExt(_llvmBuilder, right, LLVM.Int64Type(), "sext_to_long");
                    rightType = LLVM.Int64Type();
                }
                else
                {
                    throw new InvalidOperationException("Unsupported integer width combination in power operation");
                }
            }
        }
        else if (leftKind != rightKind)
        {
            if (leftKind is LLVMTypeKind.LLVMIntegerTypeKind && rightKind is LLVMTypeKind.LLVMFloatTypeKind or LLVMTypeKind.LLVMDoubleTypeKind)
            {
                left = LLVM.BuildSIToFP(_llvmBuilder, left, rightType, "promote_to_float");
                leftType = rightType;
            }
            else if (rightKind is LLVMTypeKind.LLVMIntegerTypeKind && leftKind is LLVMTypeKind.LLVMFloatTypeKind or LLVMTypeKind.LLVMDoubleTypeKind)
            {
                right = LLVM.BuildSIToFP(_llvmBuilder, right, leftType, "promote_to_float");
                rightType = leftType;
            }
            else
            {
                throw new InvalidOperationException("Type mismatch in power operation");
            }
        }

        var powFunc = LLVM.GetNamedFunction(LlvmModule, "pow");
        if (powFunc.Pointer == IntPtr.Zero)
        {
            var powType = LLVM.FunctionType(LLVM.DoubleType(), [LLVM.DoubleType(), LLVM.DoubleType()], false);
            powFunc = LLVM.AddFunction(LlvmModule, "pow", powType);
        }

        if (LLVM.GetTypeKind(leftType) != LLVMTypeKind.LLVMDoubleTypeKind)
        {
            left = LLVM.BuildSIToFP(_llvmBuilder, left, LLVM.DoubleType(), "to_double");
        }
        if (LLVM.GetTypeKind(rightType) != LLVMTypeKind.LLVMDoubleTypeKind)
        {
            right = LLVM.BuildSIToFP(_llvmBuilder, right, LLVM.DoubleType(), "to_double");
        }

        return LLVM.BuildCall(_llvmBuilder, powFunc, [left, right], "pow");
    }

    public override object? VisitValueExp(GlyphScriptParser.ValueExpContext context)
    {
        return Visit(context.immediateValue());
    }

    public override object? VisitIdAtomExp(GlyphScriptParser.IdAtomExpContext context)
    {
        var id = context.ID().GetText();
        if (!_variables.TryGetValue(id, out var variable))
        {
            throw new InvalidOperationException($"Variable '{id}' is not defined.");
        }

        return LLVM.BuildLoad(_llvmBuilder, variable.Value, id);
    }

    private static TypeKind GetTypeFromImmediateValue(GlyphScriptParser.ImmediateValueContext context)
    {
        if (context.INT_LITERAL() != null) return TypeKind.Int;
        if (context.LONG_LITERAL() != null) return TypeKind.Long;
        if (context.FLOAT_LITERAL() != null) return TypeKind.Float;
        if (context.DOUBLE_LITERAL() != null) return TypeKind.Double;
        if (context.STRING_LITERAL() != null) return TypeKind.String;
        throw new InvalidOperationException("Invalid immediate value");
    }

    private static TypeKind GetTypeFromContext(GlyphScriptParser.TypeContext context)
    {
        if (context.INT() != null) return TypeKind.Int;
        if (context.LONG() != null) return TypeKind.Long;
        if (context.FLOAT() != null) return TypeKind.Float;
        if (context.DOUBLE() != null) return TypeKind.Double;
        if (context.STRING_TYPE() != null) return TypeKind.String;
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
            TypeKind.String => LLVM.PointerType(LLVM.Int8Type(), 0),
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
        CreateStringConstant(module, "strp_string", "%s\n\0");

        CreateStringConstant(module, "strs_int", "%d\0");
        CreateStringConstant(module, "strs_long", "%ld\0");
        CreateStringConstant(module, "strs_float", "%f\0");
        CreateStringConstant(module, "strs_double", "%lf\0");
        CreateStringConstant(module, "strs_string", "%s\0");

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
            TypeKind.String => LLVM.GetNamedGlobal(LlvmModule, "strp_string"),
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
            TypeKind.String => LLVM.GetNamedGlobal(LlvmModule, "strs_string"),
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
        var isNullTerminated = value.LastOrDefault() == '\0';
        if (!isNullTerminated)
            value += '\0';

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

    private static LLVMValueRef GetDefaultValueForType(TypeKind type)
    {
        return type switch
        {
            TypeKind.Int => LLVM.ConstInt(LLVM.Int32Type(), 0, false),
            TypeKind.Long => LLVM.ConstInt(LLVM.Int64Type(), 0, false),
            TypeKind.Float => LLVM.ConstReal(LLVM.FloatType(), 0.0f),
            TypeKind.Double => LLVM.ConstReal(LLVM.DoubleType(), 0.0),
            TypeKind.String => LLVM.ConstPointerNull(LLVM.PointerType(LLVM.Int8Type(), 0)), // Default empty string
            _ => throw new InvalidOperationException($"Unsupported type: {type}")
        };
    }

    public void Dispose()
    {
        LLVM.DisposeBuilder(_llvmBuilder);
    }
}
