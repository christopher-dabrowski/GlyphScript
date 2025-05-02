using GlyphScriptCompiler.Antlr;
using GlyphScriptCompiler.SyntaxErrors;
using GlyphScriptCompiler.TypeOperations;
using LLVMSharp;

namespace GlyphScriptCompiler;

public record GlyphScriptValue
(
    LLVMValueRef Value,
    GlyphScriptType Type
);

public enum OperationKind
{
    Assignment,
    Addition,
    Subtraction,
    Multiplication,
    Division,
    Power,
    Print,
    Read,
    DefaultValue
}

public record OperationSignature(
    OperationKind Kind,
    IReadOnlyList<GlyphScriptType> Parameters)
{
    public virtual bool Equals(OperationSignature? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Kind == other.Kind && Parameters.SequenceEqual(other.Parameters);
    }

    public override int GetHashCode()
    {
        var parametersHash = Parameters.Aggregate(0, HashCode.Combine);
        return HashCode.Combine((int)Kind, parametersHash);
    }
}

public delegate GlyphScriptValue? OperationImplementation(IReadOnlyList<GlyphScriptValue> parameters);

public interface IOperationProvider
{
    IReadOnlyDictionary<OperationSignature, OperationImplementation> Operations { get; }
}

public sealed class LlvmVisitor : GlyphScriptBaseVisitor<object?>, IDisposable
{
    public LLVMModuleRef LlvmModule { get; }
    private readonly LLVMBuilderRef _llvmBuilder = LLVM.CreateBuilder();
    private readonly ExpressionResultTypeEngine _expressionResultTypeEngine = new();

    private readonly Dictionary<string, GlyphScriptValue> _variables = [];
    private int _stringConstCounter = 0;

    private Dictionary<OperationSignature, OperationImplementation> _availableOperations = new();

    public LlvmVisitor(LLVMModuleRef llvmModule)
    {
        LlvmModule = llvmModule;

        IOperationProvider[] initialOperationProviders =
        [
            new IntegerOperations(llvmModule, _llvmBuilder),
            new LongOperations(llvmModule, _llvmBuilder),
            new FloatOperations(llvmModule, _llvmBuilder),
            new DoubleOperations(llvmModule, _llvmBuilder),
            new StringOperations(llvmModule, _llvmBuilder)
        ];

        foreach (var provider in initialOperationProviders)
            RegisterOperations(provider);
    }

    private void RegisterOperations(IOperationProvider provider)
    {
        foreach (var (operationSignature, implementation) in provider.Operations)
            _availableOperations.Add(operationSignature, implementation);
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
        const OperationKind operationKind = OperationKind.DefaultValue;

        var type = GetTypeFromContext(context.type());
        var id = context.ID().GetText();

        var operationSignature = new OperationSignature(operationKind, [type]);
        var createDefaultValueOperation = _availableOperations.GetValueOrDefault(operationSignature);
        if (createDefaultValueOperation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        var value = createDefaultValueOperation([])
            ?? throw new InvalidOperationException($"Failed to create default value for type {type}");

        if (_variables.ContainsKey(id))
            throw new InvalidOperationException($"Variable '{id}' is already defined.");

        var llvmType = GetLlvmType(type);
        var variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);

        LLVM.BuildStore(_llvmBuilder, value.Value, variable);

        var result = new GlyphScriptValue(variable, type);
        _variables[id] = result;
        return result;
    }

    public override object? VisitInitializingDeclaration(GlyphScriptParser.InitializingDeclarationContext context)
    {
        var type = GetTypeFromContext(context.type());
        var id = context.ID().GetText();

        var expressionValue = Visit(context.expression()) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to create expression");

        if (_variables.ContainsKey(id))
            throw new DuplicateVariableDeclarationException(context) { VariableName = id };

        // Check if the types are compatible for assignment
        if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(type, expressionValue.Type))
        {
            throw new AssignmentOfInvalidTypeException(context)
            {
                VariableName = id,
                VariableGlyphScriptType = type,
                ValueGlyphScriptType = expressionValue.Type
            };
        }

        var llvmType = GetLlvmType(type);
        var variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);

        LLVM.BuildStore(_llvmBuilder, expressionValue.Value, variable);

        var result = new GlyphScriptValue(variable, type);
        _variables[id] = result;
        return result;
    }

    public override object? VisitAssignment(GlyphScriptParser.AssignmentContext context)
    {
        var id = context.ID().GetText();
        var expressionValue = Visit(context.expression()) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to create expression");

        if (!_variables.TryGetValue(id, out var variable))
            throw new UndefinedVariableUsageException(context) { VariableName = id };

        // Check if the types are compatible for assignment
        if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(variable.Type, expressionValue.Type))
        {
            throw new AssignmentOfInvalidTypeException(context)
            {
                VariableName = id,
                VariableGlyphScriptType = variable.Type,
                ValueGlyphScriptType = expressionValue.Type
            };
        }

        LLVM.BuildStore(_llvmBuilder, expressionValue.Value, variable.Value);
        return expressionValue;
    }

    public override object? VisitPrint(GlyphScriptParser.PrintContext context)
    {
        var expressionValue = Visit(context.expression()) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to create expression");

        var value = expressionValue.Value;
        var valueType = LLVM.TypeOf(value);
        var valueKind = LLVM.GetTypeKind(valueType);
        var printfFormatStr = GetPrintfFormatString(expressionValue.Type);

        var printfFunc = LLVM.GetNamedFunction(LlvmModule, "printf");

        // To print float we need to convert it to double first
        if (valueKind == LLVMTypeKind.LLVMFloatTypeKind)
            value = LLVM.BuildFPExt(_llvmBuilder, value, LLVM.DoubleType(), string.Empty);

        var args = new[] { GetStringPtr(_llvmBuilder, printfFormatStr), value };

        var printfResult = LLVM.BuildCall(_llvmBuilder, printfFunc, args, "printf_call");
        return expressionValue; // Return the expression value that was printed
    }

    public override object? VisitRead(GlyphScriptParser.ReadContext context)
    {
        var id = context.ID().GetText();

        if (!_variables.TryGetValue(id, out var variable))
        {
            throw new UndefinedVariableUsageException(context) { VariableName = id };
        }

        var scanfFormatStr = GetScanfFormatString(variable.Type);
        var scanfFunc = LLVM.GetNamedFunction(LlvmModule, "scanf");

        var args = new[] { GetStringPtr(_llvmBuilder, scanfFormatStr), variable.Value };
        var scanfResult = LLVM.BuildCall(_llvmBuilder, scanfFunc, args, "scanf_call");

        // After reading, build a load to get the current value and return it
        var loadedValue = LLVM.BuildLoad(_llvmBuilder, variable.Value, $"read_{id}");
        return new GlyphScriptValue(loadedValue, variable.Type);
    }

    public override object? VisitImmediateValue(GlyphScriptParser.ImmediateValueContext context)
    {
        if (context.INT_LITERAL() != null)
        {
            var value = int.Parse(context.INT_LITERAL().GetText());
            return new GlyphScriptValue(LLVM.ConstInt(LLVM.Int32Type(), (ulong)value, false), GlyphScriptType.Int);
        }
        if (context.LONG_LITERAL() != null)
        {
            var value = long.Parse(context.LONG_LITERAL().GetText().TrimEnd('l', 'L'));
            return new GlyphScriptValue(LLVM.ConstInt(LLVM.Int64Type(), (ulong)value, false), GlyphScriptType.Long);
        }
        if (context.FLOAT_LITERAL() != null)
        {
            var value = float.Parse(context.FLOAT_LITERAL().GetText().TrimEnd('f', 'F'));
            return new GlyphScriptValue(LLVM.ConstReal(LLVM.FloatType(), value), GlyphScriptType.Float);
        }
        if (context.DOUBLE_LITERAL() != null)
        {
            var value = double.Parse(context.DOUBLE_LITERAL().GetText().TrimEnd('d', 'D'));
            return new GlyphScriptValue(LLVM.ConstReal(LLVM.DoubleType(), value), GlyphScriptType.Double);
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
            return new GlyphScriptValue(GetStringPtr(_llvmBuilder, stringGlobal), GlyphScriptType.String);
        }
        throw new InvalidOperationException("Invalid immediate value");
    }

    public override object? VisitParenthesisExp(GlyphScriptParser.ParenthesisExpContext context) =>
        Visit(context.expression());

    public override object? VisitMulDivExp(GlyphScriptParser.MulDivExpContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationKind = context.MULTIPLICATION_SYMBOL() != null
            ? OperationKind.Multiplication
            : OperationKind.Division;
        var operationSignature = new OperationSignature(operationKind, [leftValue.Type, rightValue.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation([leftValue, rightValue]);
    }

    public override object? VisitAddSubExp(GlyphScriptParser.AddSubExpContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationKind = context.ADDITION_SYMBOL() != null
            ? OperationKind.Addition
            : OperationKind.Subtraction;
        var operationSignature = new OperationSignature(operationKind, [leftValue.Type, rightValue.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation([leftValue, rightValue]);
    }

    public override object? VisitPowerExp(GlyphScriptParser.PowerExpContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var left = leftValue.Value;
        var right = rightValue.Value;

        // Use the type engine to determine the result type
        var resultType = _expressionResultTypeEngine.GetPowerResultType(context, leftValue.Type, rightValue.Type);

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

        return new GlyphScriptValue(
            LLVM.BuildCall(_llvmBuilder, powFunc, [left, right], "pow"),
            resultType);
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

        return new GlyphScriptValue(LLVM.BuildLoad(_llvmBuilder, variable.Value, id), variable.Type);
    }

    private static GlyphScriptType GetTypeFromImmediateValue(GlyphScriptParser.ImmediateValueContext context)
    {
        if (context.INT_LITERAL() != null) return GlyphScriptType.Int;
        if (context.LONG_LITERAL() != null) return GlyphScriptType.Long;
        if (context.FLOAT_LITERAL() != null) return GlyphScriptType.Float;
        if (context.DOUBLE_LITERAL() != null) return GlyphScriptType.Double;
        if (context.STRING_LITERAL() != null) return GlyphScriptType.String;
        throw new InvalidOperationException("Invalid immediate value");
    }

    private static GlyphScriptType GetTypeFromContext(GlyphScriptParser.TypeContext context)
    {
        if (context.INT() != null) return GlyphScriptType.Int;
        if (context.LONG() != null) return GlyphScriptType.Long;
        if (context.FLOAT() != null) return GlyphScriptType.Float;
        if (context.DOUBLE() != null) return GlyphScriptType.Double;
        if (context.STRING_TYPE() != null) return GlyphScriptType.String;
        throw new InvalidOperationException("Invalid type");
    }

    private static LLVMTypeRef GetLlvmType(GlyphScriptType glyphScriptType)
    {
        return glyphScriptType switch
        {
            GlyphScriptType.Int => LLVM.Int32Type(),
            GlyphScriptType.Long => LLVM.Int64Type(),
            GlyphScriptType.Float => LLVM.FloatType(),
            GlyphScriptType.Double => LLVM.DoubleType(),
            GlyphScriptType.String => LLVM.PointerType(LLVM.Int8Type(), 0),
            _ => throw new InvalidOperationException($"Unsupported type: {glyphScriptType}")
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

    private LLVMValueRef GetPrintfFormatString(GlyphScriptType glyphScriptType)
    {
        return glyphScriptType switch
        {
            GlyphScriptType.Int => LLVM.GetNamedGlobal(LlvmModule, "strp_int"),
            GlyphScriptType.Long => LLVM.GetNamedGlobal(LlvmModule, "strp_long"),
            GlyphScriptType.Float => LLVM.GetNamedGlobal(LlvmModule, "strp_float"),
            GlyphScriptType.Double => LLVM.GetNamedGlobal(LlvmModule, "strp_double"),
            GlyphScriptType.String => LLVM.GetNamedGlobal(LlvmModule, "strp_string"),
            _ => throw new InvalidOperationException($"Unsupported type for printf: {glyphScriptType}")
        };
    }

    private LLVMValueRef GetScanfFormatString(GlyphScriptType glyphScriptType)
    {
        return glyphScriptType switch
        {
            GlyphScriptType.Int => LLVM.GetNamedGlobal(LlvmModule, "strs_int"),
            GlyphScriptType.Long => LLVM.GetNamedGlobal(LlvmModule, "strs_long"),
            GlyphScriptType.Float => LLVM.GetNamedGlobal(LlvmModule, "strs_float"),
            GlyphScriptType.Double => LLVM.GetNamedGlobal(LlvmModule, "strs_double"),
            GlyphScriptType.String => LLVM.GetNamedGlobal(LlvmModule, "strs_string"),
            _ => throw new InvalidOperationException($"Unsupported type for scanf: {glyphScriptType}")
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

    public void Dispose()
    {
        LLVM.DisposeBuilder(_llvmBuilder);
    }
}
