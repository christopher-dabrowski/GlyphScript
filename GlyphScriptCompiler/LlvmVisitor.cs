using GlyphScriptCompiler.Contracts;
using GlyphScriptCompiler.SyntaxErrors;
using GlyphScriptCompiler.TypeOperations;

namespace GlyphScriptCompiler;

public sealed class LlvmVisitor : GlyphScriptBaseVisitor<object?>, IDisposable
{
    public LLVMModuleRef LlvmModule { get; }
    private readonly LLVMBuilderRef _llvmBuilder = LLVM.CreateBuilder();
    private readonly ExpressionResultTypeEngine _expressionResultTypeEngine = new();
    private readonly ILogger<GlyphScriptLlvmCompiler> _logger;

    private VariableScope _currentScope;
    private readonly Stack<VariableScope> _scopeStack = new();
    private readonly Dictionary<OperationSignature, OperationImplementation> _availableOperations = new();


    private FunctionInfo? _currentFunction;
    private readonly Stack<FunctionInfo> _functionStack = new();

    public LlvmVisitor(LLVMModuleRef llvmModule, ILogger<GlyphScriptLlvmCompiler>? logger = null)
    {
        LlvmModule = llvmModule;
        _logger = logger ?? NullLogger<GlyphScriptLlvmCompiler>.Instance;


        _currentScope = new VariableScope();
        _scopeStack.Push(_currentScope);

        IOperationProvider[] initialOperationProviders =
        [
            new IntegerOperations(llvmModule, _llvmBuilder),
            new LongOperations(llvmModule, _llvmBuilder),
            new FloatOperations(llvmModule, _llvmBuilder),
            new DoubleOperations(llvmModule, _llvmBuilder),
            new StringOperations(llvmModule, _llvmBuilder),
            new BoolOperations(llvmModule, _llvmBuilder),
            new ArrayOperations(llvmModule, _llvmBuilder),
            new StructOperations(llvmModule, _llvmBuilder)
        ];

        foreach (var provider in initialOperationProviders)
        {
            provider.Initialize();
            RegisterOperations(provider);
        }
    }


    private void EnterScope()
    {
        var newScope = new VariableScope(_currentScope);
        _scopeStack.Push(newScope);
        _currentScope = newScope;
    }


    private void ExitScope()
    {
        if (_scopeStack.Count <= 1)
            throw new InvalidOperationException("Cannot exit the global scope");

        _scopeStack.Pop();
        _currentScope = _scopeStack.Peek();
    }


    private void EnterFunction(FunctionInfo functionInfo)
    {
        _functionStack.Push(functionInfo);
        _currentFunction = functionInfo;
    }


    private void ExitFunction()
    {
        if (_functionStack.Count == 0)
            throw new InvalidOperationException("Cannot exit function context when not in a function");

        _functionStack.Pop();
        _currentFunction = _functionStack.Count > 0 ? _functionStack.Peek() : null;
    }

    private void RegisterOperations(IOperationProvider provider)
    {
        foreach (var (operationSignature, implementation) in provider.Operations)
            _availableOperations.Add(operationSignature, implementation);
    }

    public override object? VisitProgram(GlyphScriptParser.ProgramContext context)
    {
        _logger.LogDebug("VisitProgram starting");
        SetupGlobalFunctions(LlvmModule);

        var statements = context.statement() ?? [];
        _logger.LogDebug("Found {StatementCount} statements in program", statements.Length);


        _logger.LogDebug("Starting first pass - function signatures and class declarations");
        foreach (var statement in statements)
        {
            if (statement.functionDeclaration() != null)
            {
                _logger.LogDebug("Processing function signature: {FunctionName}",
                    statement.functionDeclaration().ID().GetText());
                VisitFunctionSignature(statement.functionDeclaration());
            }
            else if (statement.classDeclaration() != null)
            {
                _logger.LogDebug("Processing class declaration: {ClassName}",
                    statement.classDeclaration().ID().GetText());
                Visit(statement.classDeclaration());
            }
        }


        _logger.LogDebug("Starting second pass - global variables");
        foreach (var statement in statements)
        {
            if (statement.functionDeclaration() == null && statement.declaration() != null)
            {
                var declaration = statement.declaration();
                _logger.LogDebug("Processing declaration in second pass");


                if (declaration.defaultDeclaration() != null)
                {
                    var defaultDecl = declaration.defaultDeclaration();
                    var defaultId = defaultDecl.ID().GetText();
                    _logger.LogDebug("Processing default declaration for '{VariableId}'", defaultId);
                    var type = GetTypeFromContext(defaultDecl.type());
                    var id = defaultDecl.ID().GetText();

                    ArrayTypeInfo? arrayInfo = null;
                    if (type == GlyphScriptType.Array)
                        arrayInfo = Visit(defaultDecl.type().arrayOfType()) as ArrayTypeInfo;


                    var operationSignature = new OperationSignature(OperationKind.DefaultValue, [type]);
                    var createDefaultValueOperation = _availableOperations.GetValueOrDefault(operationSignature);
                    if (createDefaultValueOperation is null)
                        throw new OperationNotAvailableException(defaultDecl, operationSignature);

                    var defaultValue = createDefaultValueOperation(defaultDecl, [])
                                       ?? throw new InvalidOperationException(
                                           $"Failed to create default value for type {type}");


                    var llvmType = GetLlvmType(type, arrayInfo);
                    var variable = LLVM.AddGlobal(LlvmModule, llvmType, defaultId);
                    LLVM.SetInitializer(variable, defaultValue.Value);

                    GlyphScriptValue result;
                    if (type == GlyphScriptType.Array && arrayInfo != null)
                    {
                        result = new GlyphScriptValue(variable, type, arrayInfo);
                    }
                    else
                    {
                        result = new GlyphScriptValue(variable, type);
                    }

                    _currentScope.DeclareVariable(defaultId, result);
                }

                else if (declaration.initializingDeclaration() != null)
                {
                    var initDecl = declaration.initializingDeclaration();
                    var initId = initDecl.ID().GetText();
                    _logger.LogDebug("Processing initializing declaration for '{VariableId}' in second pass", initId);
                    var id = initDecl.ID().GetText();

                    var type = GetTypeFromContext(initDecl.type());
                    if (type == GlyphScriptType.Auto)
                    {
                        var expressionValue = Visit(initDecl.expression()) as GlyphScriptValue ??
                                              throw new InvalidOperationException("Failed to create expression");
                        type = expressionValue.Type;
                    }


                    ArrayTypeInfo? arrayInfo = null;
                    if (type == GlyphScriptType.Array)
                        arrayInfo = Visit(initDecl.type().arrayOfType()) as ArrayTypeInfo;


                    var llvmType = GetLlvmType(type, arrayInfo);
                    var variable = LLVM.AddGlobal(LlvmModule, llvmType, id);

                    LLVMValueRef initializer;
                    if (type == GlyphScriptType.Array)
                    {
                        initializer = LLVM.ConstPointerNull(llvmType);
                    }
                    else
                    {
                        var operationSignature = new OperationSignature(OperationKind.DefaultValue, [type]);
                        var createDefaultValueOperation = _availableOperations.GetValueOrDefault(operationSignature);
                        if (createDefaultValueOperation is null)
                            throw new OperationNotAvailableException(initDecl, operationSignature);

                        var defaultValue = createDefaultValueOperation(initDecl, [])
                                           ?? throw new InvalidOperationException(
                                               $"Failed to create default value for type {type}");
                        initializer = defaultValue.Value;
                    }

                    LLVM.SetInitializer(variable, initializer);

                    GlyphScriptValue result;
                    if (type == GlyphScriptType.Array && arrayInfo != null)
                    {
                        result = new GlyphScriptValue(variable, type, arrayInfo);
                    }
                    else
                    {
                        result = new GlyphScriptValue(variable, type);
                    }

                    _currentScope.DeclareVariable(id, result);
                }
            }
        }


        _logger.LogDebug("Starting third pass - function bodies and class method bodies");
        foreach (var statement in statements)
        {
            if (statement.functionDeclaration() != null)
            {
                _logger.LogDebug("Processing function body: {FunctionName}",
                    statement.functionDeclaration().ID().GetText());
                VisitFunctionBody(statement.functionDeclaration());
            }
            else if (statement.classDeclaration() != null)
            {
                _logger.LogDebug("Processing class method bodies: {ClassName}",
                    statement.classDeclaration().ID().GetText());
                VisitClassMethodBodies(statement.classDeclaration());
            }
        }


        _logger.LogDebug("Creating main function");
        LlvmHelper.CreateMain(LlvmModule, _llvmBuilder);


        _logger.LogDebug("Starting fourth pass - processing all statements");
        foreach (var statement in statements)
        {
            if (statement.functionDeclaration() == null && statement.classDeclaration() == null)
            {
                _logger.LogDebug("Processing statement in fourth pass");
                var result = Visit(statement);
                _logger.LogDebug("Statement result: {ResultType}", result?.GetType().Name ?? "null");
            }
        }

        _logger.LogDebug("Building return statement for main function");
        LLVM.BuildRet(_llvmBuilder, LLVM.ConstInt(LLVM.Int32Type(), 0, false));
        _logger.LogDebug("VisitProgram completed");
        return null;
    }

    public override object? VisitDeclaration(GlyphScriptParser.DeclarationContext context)
    {
        _logger.LogDebug("VisitDeclaration called");
        return VisitChildren(context);
    }

    public override object? VisitDefaultDeclaration(GlyphScriptParser.DefaultDeclarationContext context)
    {
        const OperationKind operationKind = OperationKind.DefaultValue;

        var type = GetTypeFromContext(context.type());
        var id = context.ID().GetText();


        ArrayTypeInfo? arrayInfo = null;
        if (type == GlyphScriptType.Array)
        {
            arrayInfo = Visit(context.type().arrayOfType()) as ArrayTypeInfo;
        }

        var operationSignature = new OperationSignature(operationKind, [type]);
        var createDefaultValueOperation = _availableOperations.GetValueOrDefault(operationSignature);
        if (createDefaultValueOperation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        var value = createDefaultValueOperation(context, [])
                    ?? throw new InvalidOperationException($"Failed to create default value for type {type}");


        if (_currentScope.HasLocalVariable(id))
        {
            if (_scopeStack.Count == 1 && _currentScope.TryGetVariable(id, out var existingVariable))
            {
                return existingVariable;
            }
            else
            {
                throw new InvalidOperationException($"Variable '{id}' is already defined.");
            }
        }

        var llvmType = GetLlvmType(type, arrayInfo);
        LLVMValueRef variable;


        bool isGlobalScope = _scopeStack.Count == 1;

        if (isGlobalScope)
        {
            variable = LLVM.AddGlobal(LlvmModule, llvmType, id);
            LLVM.SetInitializer(variable, value.Value);
        }
        else
        {
            variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);
            LLVM.BuildStore(_llvmBuilder, value.Value, variable);
        }

        var result = type == GlyphScriptType.Array && arrayInfo != null
            ? new GlyphScriptValue(variable, type, arrayInfo)
            : new GlyphScriptValue(variable, type);

        _currentScope.DeclareVariable(id, result);
        return result;
    }

    public override object? VisitInitializingDeclaration(GlyphScriptParser.InitializingDeclarationContext context)
    {
        var type = GetTypeFromContext(context.type());
        var id = context.ID().GetText();

        _logger.LogDebug("VisitInitializingDeclaration called for '{VariableId}' with type: {Type}", id, type);

        ArrayTypeInfo? arrayInfo = null;
        if (type == GlyphScriptType.Array)
            arrayInfo = Visit(context.type().arrayOfType()) as ArrayTypeInfo;

        var expressionValue = Visit(context.expression()) as GlyphScriptValue ??
                              throw new InvalidOperationException("Failed to create expression");


        if (type == GlyphScriptType.Auto)
        {
            _logger.LogDebug("Auto type detected for variable '{VariableId}'. Expression type: {ExpressionType}", id,
                expressionValue.Type);
            type = expressionValue.Type;


            if (type == GlyphScriptType.Array && expressionValue.ArrayInfo != null)
            {
                arrayInfo = expressionValue.ArrayInfo;
            }

            _logger.LogDebug("After inference, type is: {InferredType}", type);
        }

        if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(type, expressionValue.Type))
        {
            throw new AssignmentOfInvalidTypeException(context)
            {
                VariableName = id,
                VariableGlyphScriptType = type,
                ValueGlyphScriptType = expressionValue.Type
            };
        }


        if (_currentScope.HasLocalVariable(id))
        {
            if (_scopeStack.Count == 1 && _currentScope.TryGetVariable(id, out var existingGlobalVariable))
            {
                LLVM.BuildStore(_llvmBuilder, expressionValue.Value, existingGlobalVariable.Value);
                return existingGlobalVariable;
            }
            else
            {
                throw new DuplicateVariableDeclarationException(context) { VariableName = id };
            }
        }

        var llvmType = GetLlvmType(type, arrayInfo);
        LLVMValueRef variable;


        bool isGlobalScope = _scopeStack.Count == 1;

        if (isGlobalScope)
        {
            variable = LLVM.AddGlobal(LlvmModule, llvmType, id);


            var isConstant = LLVM.IsConstant(expressionValue.Value);

            if (isConstant)
            {
                LLVM.SetInitializer(variable, expressionValue.Value);
            }
            else
            {
                LLVM.SetInitializer(variable, LLVM.ConstInt(llvmType, 0, false));


                LLVM.BuildStore(_llvmBuilder, expressionValue.Value, variable);
            }
        }
        else
        {
            variable = LLVM.BuildAlloca(_llvmBuilder, llvmType, id);
            LLVM.BuildStore(_llvmBuilder, expressionValue.Value, variable);
        }

        GlyphScriptValue result;
        if (type == GlyphScriptType.Array && arrayInfo != null)
        {
            result = new GlyphScriptValue(variable, type, arrayInfo);
        }
        else
        {
            result = new GlyphScriptValue(variable, type);
        }

        _currentScope.DeclareVariable(id, result);
        return result;
    }

    public override object? VisitAssignment(GlyphScriptParser.AssignmentContext context)
    {
        if (context.ID().Length == 2)
            return FieldAssignmentOperation(context);


        if (context.expression().Length > 1)
            return ArrayAssignmentOperation(context);


        var id = context.ID(0).GetText();
        var expressionValue = Visit(context.expression(0)) as GlyphScriptValue ??
                              throw new InvalidOperationException("Failed to create expression");

        if (!_currentScope.TryGetVariable(id, out var variable))
        {
            if (_currentFunction != null && _currentScope.TryGetVariable("this", out var thisVariable) &&
                thisVariable.Type == GlyphScriptType.Class && thisVariable.ClassInfo != null)
            {
                var fieldIndex = Array.FindIndex(thisVariable.ClassInfo.Fields, field => field.FieldName == id);
                if (fieldIndex >= 0)
                {
                    var fieldInfo = thisVariable.ClassInfo.Fields[fieldIndex];


                    var classPtr = LLVM.BuildLoad(_llvmBuilder, thisVariable.Value, "this_loaded");
                    var fieldPtr = LLVM.BuildStructGEP(_llvmBuilder, classPtr, (uint)fieldIndex, $"{id}_ptr");


                    if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(fieldInfo.Type,
                            expressionValue.Type))
                    {
                        throw new AssignmentOfInvalidTypeException(context)
                        {
                            VariableName = id,
                            VariableGlyphScriptType = fieldInfo.Type,
                            ValueGlyphScriptType = expressionValue.Type
                        };
                    }


                    LLVM.BuildStore(_llvmBuilder, expressionValue.Value, fieldPtr);
                    return expressionValue;
                }
            }

            throw new UndefinedVariableUsageException(context) { VariableName = id };
        }

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

    private object? ArrayAssignmentOperation(GlyphScriptParser.AssignmentContext context)
    {
        var id = context.ID(0).GetText();
        var indexValue = Visit(context.expression(0)) as GlyphScriptValue ??
                         throw new InvalidOperationException("Failed to resolve index expression");
        var rhsValue = Visit(context.expression(1)) as GlyphScriptValue ??
                       throw new InvalidOperationException("Failed to create expression value");

        if (!_currentScope.TryGetVariable(id, out var arrayVariable))
            throw new UndefinedVariableUsageException(context) { VariableName = id };

        if (arrayVariable.Type != GlyphScriptType.Array || arrayVariable.ArrayInfo == null)
            throw new InvalidSyntaxException(context, $"Variable '{id}' is not an array");

        if (indexValue.Type != GlyphScriptType.Int && indexValue.Type != GlyphScriptType.Long)
            throw new InvalidSyntaxException(context,
                $"Array indices must be integer types. Found: {indexValue.Type}");

        if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(arrayVariable.ArrayInfo.ElementType,
                rhsValue.Type))
        {
            throw new AssignmentOfInvalidTypeException(context)
            {
                VariableName = $"{id}[index]",
                VariableGlyphScriptType = arrayVariable.ArrayInfo.ElementType,
                ValueGlyphScriptType = rhsValue.Type
            };
        }

        var loadedArrayValue = LLVM.BuildLoad(_llvmBuilder, arrayVariable.Value, $"{id}Value");
        var arrayValue = new GlyphScriptValue(loadedArrayValue, arrayVariable.Type, arrayVariable.ArrayInfo);

        const OperationKind operationKind = OperationKind.ArrayElementAssignment;
        var operationSignature = new OperationSignature(
            operationKind,
            [arrayVariable.Type, indexValue.Type, rhsValue.Type, arrayVariable.ArrayInfo.ElementType]
        );

        var arrayAssignmentOperation = _availableOperations.GetValueOrDefault(operationSignature);
        if (arrayAssignmentOperation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return arrayAssignmentOperation(context, [arrayValue, indexValue, rhsValue]);
    }

    private object? FieldAssignmentOperation(GlyphScriptParser.AssignmentContext context)
    {
        var instanceId = context.ID(0).GetText();
        var fieldName = context.ID(1).GetText();
        var rhsValue = Visit(context.expression(0)) as GlyphScriptValue ??
                       throw new InvalidOperationException("Failed to create expression value");

        if (!_currentScope.TryGetVariable(instanceId, out var instanceVariable))
            throw new UndefinedVariableUsageException(context) { VariableName = instanceId };


        var fieldIndex = -1;
        GlyphScriptType fieldType = GlyphScriptType.Void;
        string typeName;

        if (instanceVariable.Type == GlyphScriptType.Struct && instanceVariable.StructInfo != null)
        {
            for (int i = 0; i < instanceVariable.StructInfo.Fields.Length; i++)
            {
                if (instanceVariable.StructInfo.Fields[i].FieldName == fieldName)
                {
                    fieldIndex = i;
                    fieldType = instanceVariable.StructInfo.Fields[i].Type;
                    break;
                }
            }

            typeName = instanceVariable.StructInfo.Name;
        }
        else if (instanceVariable.Type == GlyphScriptType.Class && instanceVariable.ClassInfo != null)
        {
            for (int i = 0; i < instanceVariable.ClassInfo.Fields.Length; i++)
            {
                if (instanceVariable.ClassInfo.Fields[i].FieldName == fieldName)
                {
                    fieldIndex = i;
                    fieldType = instanceVariable.ClassInfo.Fields[i].Type;
                    break;
                }
            }

            typeName = instanceVariable.ClassInfo.Name;
        }
        else
        {
            throw new InvalidSyntaxException(context, $"Variable '{instanceId}' is not a structure or class");
        }

        if (fieldIndex == -1)
            throw new InvalidSyntaxException(context,
                $"Field '{fieldName}' does not exist in {(instanceVariable.Type == GlyphScriptType.Struct ? "structure" : "class")} '{typeName}'");

        if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(fieldType, rhsValue.Type))
        {
            throw new AssignmentOfInvalidTypeException(context)
            {
                VariableName = $"{instanceId}.{fieldName}",
                VariableGlyphScriptType = fieldType,
                ValueGlyphScriptType = rhsValue.Type
            };
        }


        var fieldPtr = LLVM.BuildStructGEP(_llvmBuilder, instanceVariable.Value, (uint)fieldIndex,
            $"{instanceId}_{fieldName}_ptr");
        LLVM.BuildStore(_llvmBuilder, rhsValue.Value, fieldPtr);

        return rhsValue;
    }

    public override object? VisitPrint(GlyphScriptParser.PrintContext context)
    {
        _logger.LogDebug("VisitPrint called");
        const OperationKind operationKind = OperationKind.Print;

        var expressionValue = Visit(context.expression()) as GlyphScriptValue ??
                              throw new InvalidOperationException("Failed to create expression");

        var operationSignature = new OperationSignature(operationKind, [expressionValue.Type]);

        var printOperation = _availableOperations.GetValueOrDefault(operationSignature);
        if (printOperation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return printOperation(context, [expressionValue]);
    }

    public override object? VisitRead(GlyphScriptParser.ReadContext context)
    {
        const OperationKind operationKind = OperationKind.Read;

        var id = context.ID().GetText();

        if (!_currentScope.TryGetVariable(id, out var variable))
            throw new UndefinedVariableUsageException(context) { VariableName = id };

        var operationSignature = new OperationSignature(operationKind, [variable.Type]);

        var readOperation = _availableOperations.GetValueOrDefault(operationSignature);
        if (readOperation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        var result = readOperation(context, [variable]) as GlyphScriptValue;

        if (result != null)
            LLVM.BuildStore(_llvmBuilder, result.Value, variable.Value);

        return result;
    }

    public override object? VisitImmediateValue(GlyphScriptParser.ImmediateValueContext context)
    {
        if (context.arrayLiteral() != null)
        {
            return Visit(context.arrayLiteral());
        }

        const OperationKind operationKind = OperationKind.ParseImmediate;

        static GlyphScriptType DetectType(GlyphScriptParser.ImmediateValueContext context)
        {
            if (context.INT_LITERAL() != null) return GlyphScriptType.Int;
            if (context.LONG_LITERAL() != null) return GlyphScriptType.Long;
            if (context.FLOAT_LITERAL() != null) return GlyphScriptType.Float;
            if (context.DOUBLE_LITERAL() != null) return GlyphScriptType.Double;
            if (context.STRING_LITERAL() != null) return GlyphScriptType.String;
            if (context.TRUE_LITERAL() != null || context.FALSE_LITERAL() != null) return GlyphScriptType.Boolean;
            throw new InvalidOperationException("Invalid immediate value");
        }

        var type = DetectType(context);
        var operationSignature = new OperationSignature(operationKind, [type]);
        var createImmediateValueOperation = _availableOperations.GetValueOrDefault(operationSignature)
                                            ?? throw new OperationNotAvailableException(context, operationSignature);

        return createImmediateValueOperation(context, [])
               ?? throw new InvalidOperationException($"Failed to create immediate value for type {type}");
    }

    public override object? VisitIfStatement(GlyphScriptParser.IfStatementContext context)
    {
        var conditionValue = Visit(context.expression()) as GlyphScriptValue ??
                             throw new InvalidOperationException("Failed to evaluate if condition expression");

        if (conditionValue.Type != GlyphScriptType.Boolean)
            throw new InvalidSyntaxException(context,
                $"Condition in if statement must be a boolean expression. Found: {conditionValue.Type}");

        var currentFunction = LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_llvmBuilder));
        var thenBlock = LLVM.AppendBasicBlock(currentFunction, "if_then");
        LLVMBasicBlockRef? elseBlock = null;
        if (context.ELSE() != null)
        {
            elseBlock = LLVM.AppendBasicBlock(currentFunction, "if_else");
        }

        var mergeBlock = LLVM.AppendBasicBlock(currentFunction, "if_merge");

        if (elseBlock != null)
            LLVM.BuildCondBr(_llvmBuilder, conditionValue.Value, thenBlock, elseBlock.Value);
        else
            LLVM.BuildCondBr(_llvmBuilder, conditionValue.Value, thenBlock, mergeBlock);


        LLVM.PositionBuilderAtEnd(_llvmBuilder, thenBlock);
        Visit(context.block(0));
        
        // Check if the current block (where the builder is positioned) has a terminator
        var currentBlock = LLVM.GetInsertBlock(_llvmBuilder);
        var currentBlockTerminated = LLVM.GetBasicBlockTerminator(currentBlock).Pointer != IntPtr.Zero;
        if (!currentBlockTerminated)
        {
            LLVM.BuildBr(_llvmBuilder, mergeBlock);
        }

        var elseTerminated = false;
        if (elseBlock != null)
        {
            LLVM.PositionBuilderAtEnd(_llvmBuilder, elseBlock.Value);
            Visit(context.block(1));
            
            // Check if the current block (where the builder is positioned) has a terminator
            var currentElseBlock = LLVM.GetInsertBlock(_llvmBuilder);
            elseTerminated = LLVM.GetBasicBlockTerminator(currentElseBlock).Pointer != IntPtr.Zero;
            if (!elseTerminated)
            {
                LLVM.BuildBr(_llvmBuilder, mergeBlock);
            }
        }

        // Check if the original then block was terminated (for cleanup logic)
        var thenBlockTerminated = LLVM.GetBasicBlockTerminator(thenBlock).Pointer != IntPtr.Zero;

        if (elseBlock != null && thenBlockTerminated && elseTerminated)
        {
            LLVM.DeleteBasicBlock(mergeBlock);
        }
        else
        {
            LLVM.PositionBuilderAtEnd(_llvmBuilder, mergeBlock);
        }

        return null;
    }

    public override object? VisitBlock(GlyphScriptParser.BlockContext context)
    {
        if (context.BEGIN() != null)
        {
            EnterScope();

            try
            {
                var statements = context.statement();
                foreach (var statement in statements)
                {
                    Visit(statement);
                }

                return null;
            }
            finally
            {
                ExitScope();
            }
        }

        return Visit(context.statement(0));
    }

    public override object? VisitMulDivExp(GlyphScriptParser.MulDivExpContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue ??
                        throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue ??
                         throw new InvalidOperationException("Unable to resolve the right expression");

        var operationKind = context.MULTIPLICATION_SYMBOL() != null
            ? OperationKind.Multiplication
            : OperationKind.Division;
        var operationSignature = new OperationSignature(operationKind, [leftValue.Type, rightValue.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation(context, [leftValue, rightValue]);
    }

    public override object? VisitAddSubExp(GlyphScriptParser.AddSubExpContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue ??
                        throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue ??
                         throw new InvalidOperationException("Unable to resolve the right expression");

        var operationKind = context.ADDITION_SYMBOL() != null
            ? OperationKind.Addition
            : OperationKind.Subtraction;
        var operationSignature = new OperationSignature(operationKind, [leftValue.Type, rightValue.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation(context, [leftValue, rightValue]);
    }

    public override object? VisitPowerExp(GlyphScriptParser.PowerExpContext context)
    {
        const OperationKind operationKind = OperationKind.Power;

        var leftValue = Visit(context.expression(0)) as GlyphScriptValue ??
                        throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue ??
                         throw new InvalidOperationException("Unable to resolve the right expression");

        var operationSignature = new OperationSignature(operationKind, [leftValue.Type, rightValue.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation(context, [leftValue, rightValue]);
    }

    public override object? VisitValueExp(GlyphScriptParser.ValueExpContext context) =>
        Visit(context.immediateValue());

    public override object? VisitParenthesisExp(GlyphScriptParser.ParenthesisExpContext context) =>
        Visit(context.expression());

    public override object? VisitIdAtomExp(GlyphScriptParser.IdAtomExpContext context)
    {
        var id = context.ID().GetText();
        if (!_currentScope.TryGetVariable(id, out var variable))
        {
            if (_currentFunction != null && _currentScope.TryGetVariable("this", out var thisVariable) &&
                thisVariable.Type == GlyphScriptType.Class && thisVariable.ClassInfo != null)
            {
                var fieldIndex = Array.FindIndex(thisVariable.ClassInfo.Fields, field => field.FieldName == id);
                if (fieldIndex >= 0)
                {
                    var fieldInfo = thisVariable.ClassInfo.Fields[fieldIndex];


                    var classPtr = LLVM.BuildLoad(_llvmBuilder, thisVariable.Value, "this_loaded");
                    var fieldPtr = LLVM.BuildStructGEP(_llvmBuilder, classPtr, (uint)fieldIndex, $"{id}_ptr");

                    if (fieldInfo.Type == GlyphScriptType.Array)
                    {
                        return new GlyphScriptValue(fieldPtr, fieldInfo.Type) { ArrayInfo = null };
                    }

                    if (fieldInfo.Type == GlyphScriptType.Struct)
                    {
                        return new GlyphScriptValue(fieldPtr, fieldInfo.Type) { StructInfo = null };
                    }

                    if (fieldInfo.Type == GlyphScriptType.Class)
                    {
                        return new GlyphScriptValue(fieldPtr, fieldInfo.Type) { ClassInfo = null };
                    }


                    return new GlyphScriptValue(
                        LLVM.BuildLoad(_llvmBuilder, fieldPtr, id),
                        fieldInfo.Type);
                }
            }

            throw new InvalidOperationException($"Variable '{id}' is not defined.");
        }

        if (variable.Type == GlyphScriptType.Array && variable.ArrayInfo != null)
        {
            return variable with { Value = LLVM.BuildLoad(_llvmBuilder, variable.Value, id) };
        }

        if (variable.Type == GlyphScriptType.Struct && variable.StructInfo != null)
        {
            return variable;
        }

        if (variable.Type == GlyphScriptType.Class && variable.ClassInfo != null)
        {
            return variable;
        }

        return new GlyphScriptValue(
            LLVM.BuildLoad(_llvmBuilder, variable.Value, id),
            variable.Type);
    }

    public override object? VisitNotExpr(GlyphScriptParser.NotExprContext context)
    {
        const OperationKind operationKind = OperationKind.Not;

        var value = Visit(context.expression()) as GlyphScriptValue
                    ?? throw new InvalidOperationException("Unable to resolve the expression");

        var operationSignature = new OperationSignature(operationKind, [value.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation(context, [value]);
    }

    public override object? VisitXorExp(GlyphScriptParser.XorExpContext context)
    {
        const OperationKind operationKind = OperationKind.Xor;

        var leftValue = Visit(context.expression(0)) as GlyphScriptValue
                        ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue
                         ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationSignature = new OperationSignature(operationKind, [leftValue.Type, rightValue.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation(context, [leftValue, rightValue]);
    }

    public override object? VisitComparisonExpr(GlyphScriptParser.ComparisonExprContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue
                        ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue
                         ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationKind = OperationKind.Comparison;
        return VisitBinaryExpression(context, leftValue, rightValue, operationKind);
    }

    public override object? VisitLessThanExpr(GlyphScriptParser.LessThanExprContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue
                        ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue
                         ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationKind = OperationKind.LessThan;
        return VisitBinaryExpression(context, leftValue, rightValue, operationKind);
    }

    public override object? VisitGreaterThanExpr(GlyphScriptParser.GreaterThanExprContext context)
    {
        var leftValue = Visit(context.expression(0)) as GlyphScriptValue
                        ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue
                         ?? throw new InvalidOperationException("Unable to resolve the right expression");

        var operationKind = OperationKind.GreaterThan;
        return VisitBinaryExpression(context, leftValue, rightValue, operationKind);
    }

    public override object? VisitArrayLiteral(GlyphScriptParser.ArrayLiteralContext context)
    {
        const OperationKind operationKind = OperationKind.CreateArray;

        if (context.expressionList() == null)
        {
            var defaultElementType = GlyphScriptType.Int;
            var operationSignature = new OperationSignature(operationKind, [defaultElementType]);

            var createEmptyArrayOperation = _availableOperations.GetValueOrDefault(operationSignature);
            if (createEmptyArrayOperation is null)
                throw new OperationNotAvailableException(context, operationSignature);

            return createEmptyArrayOperation(context, []);
        }

        var expressionValues = new List<GlyphScriptValue>();

        foreach (var expr in context.expressionList().expression())
        {
            var value = Visit(expr) as GlyphScriptValue ??
                        throw new InvalidOperationException("Failed to evaluate array element expression");
            expressionValues.Add(value);
        }

        var elementType = expressionValues[0].Type;
        for (int i = 1; i < expressionValues.Count; i++)
        {
            if (expressionValues[i].Type != elementType)
                throw new InvalidSyntaxException(context,
                    $"Array elements must be of the same type. Found {elementType} and {expressionValues[i].Type}");
        }

        var arrayOperationSignature = new OperationSignature(operationKind, [elementType]);
        var createArrayOperation = _availableOperations.GetValueOrDefault(arrayOperationSignature);

        if (createArrayOperation is null)
            throw new OperationNotAvailableException(context, arrayOperationSignature);

        return createArrayOperation(context, expressionValues.ToArray());
    }

    public override object? VisitArrayAccessExp(GlyphScriptParser.ArrayAccessExpContext context)
    {
        const OperationKind operationKind = OperationKind.ArrayAccess;

        var arrayValue = Visit(context.expression(0)) as GlyphScriptValue ??
                         throw new InvalidOperationException("Failed to resolve array expression");

        if (arrayValue.Type != GlyphScriptType.Array || arrayValue.ArrayInfo == null)
            throw new InvalidSyntaxException(context, $"Expression is not an array: {context.expression(0).GetText()}");

        var indexValue = Visit(context.expression(1)) as GlyphScriptValue ??
                         throw new InvalidOperationException("Failed to resolve index expression");

        if (indexValue.Type != GlyphScriptType.Int && indexValue.Type != GlyphScriptType.Long)
            throw new InvalidSyntaxException(context,
                $"Array indices must be integer types. Found: {indexValue.Type}");

        var accessOperationSignature = new OperationSignature(operationKind,
            [arrayValue.Type, indexValue.Type, arrayValue.ArrayInfo.ElementType]);

        var arrayAccessOperation = _availableOperations.GetValueOrDefault(accessOperationSignature);
        if (arrayAccessOperation is null)
            throw new OperationNotAvailableException(context, accessOperationSignature);

        return arrayAccessOperation(context, [arrayValue, indexValue]);
    }

    public override object? VisitArrayOfType(GlyphScriptParser.ArrayOfTypeContext context)
    {
        var elementType = GetTypeFromContext(context.type());
        return new ArrayTypeInfo(elementType);
    }

    private GlyphScriptType GetTypeFromContext(GlyphScriptParser.TypeContext context)
    {
        if (context.INT() != null) return GlyphScriptType.Int;
        if (context.LONG() != null) return GlyphScriptType.Long;
        if (context.FLOAT() != null) return GlyphScriptType.Float;
        if (context.DOUBLE() != null) return GlyphScriptType.Double;
        if (context.STRING_TYPE() != null) return GlyphScriptType.String;
        if (context.BOOLEAN_TYPE() != null) return GlyphScriptType.Boolean;
        if (context.VOID_TYPE() != null) return GlyphScriptType.Void;
        if (context.AUTO() != null) return GlyphScriptType.Auto;
        if (context.arrayOfType() != null) return GlyphScriptType.Array;
        if (context.ID() != null)
        {
            var typeName = context.ID().GetText();

            if (_currentScope.TryGetStructType(typeName, out _, out _))
            {
                return GlyphScriptType.Struct;
            }

            if (_currentScope.TryGetClassType(typeName, out _, out _))
            {
                return GlyphScriptType.Class;
            }

            throw new InvalidSyntaxException(context, $"Undefined type '{typeName}'");
        }

        throw new InvalidOperationException("Invalid type");
    }

    private static LLVMTypeRef GetLlvmType(GlyphScriptType glyphScriptType, ArrayTypeInfo? arrayInfo = null)
    {
        return glyphScriptType switch
        {
            GlyphScriptType.Int => LLVM.Int32Type(),
            GlyphScriptType.Long => LLVM.Int64Type(),
            GlyphScriptType.Float => LLVM.FloatType(),
            GlyphScriptType.Double => LLVM.DoubleType(),
            GlyphScriptType.String => LLVM.PointerType(LLVM.Int8Type(), 0),
            GlyphScriptType.Boolean => LLVM.Int1Type(),
            GlyphScriptType.Void => LLVM.VoidType(),
            GlyphScriptType.Array when arrayInfo != null => LLVM.PointerType(LLVM.Int8Type(), 0),
            GlyphScriptType.Struct => throw new InvalidOperationException(
                "Struct type requires specific structure information. Use the overload with StructTypeInfo."),
            GlyphScriptType.Class => throw new InvalidOperationException(
                "Class type requires specific class information. Use the overload with ClassTypeInfo."),
            GlyphScriptType.Auto => throw new InvalidOperationException(
                "Auto type should have been resolved to a concrete type before reaching LLVM type mapping"),
            _ => throw new InvalidOperationException($"Unsupported type: {glyphScriptType}")
        };
    }

    private LLVMTypeRef GetLlvmType(GlyphScriptType glyphScriptType, StructTypeInfo structInfo)
    {
        if (glyphScriptType != GlyphScriptType.Struct)
            throw new InvalidOperationException("This overload is only for struct types");


        if (_currentScope.TryGetStructType(structInfo.Name, out _, out var existingLlvmType))
            return existingLlvmType;


        var fieldTypes = structInfo.Fields.Select(f => GetLlvmType(f.Type)).ToArray();
        return LLVM.StructType(fieldTypes, false);
    }

    private LLVMTypeRef GetLlvmType(GlyphScriptType glyphScriptType, ClassTypeInfo classInfo)
    {
        if (glyphScriptType != GlyphScriptType.Class)
            throw new InvalidOperationException("This overload is only for class types");


        if (_currentScope.TryGetClassType(classInfo.Name, out _, out var existingLlvmType))
            return existingLlvmType;


        var fieldTypes = classInfo.Fields.Select(f => GetLlvmType(f.Type)).ToArray();
        return LLVM.StructType(fieldTypes, false);
    }

    private void SetupGlobalFunctions(LLVMModuleRef module)
    {
        var i8Type = LLVM.Int8Type();
        var i8PtrType = LLVM.PointerType(i8Type, 0);

        LLVMTypeRef[] printfParamTypes = [i8PtrType];
        var printfType = LLVM.FunctionType(LLVM.Int32Type(), printfParamTypes, true);
        LLVM.AddFunction(module, "printf", printfType);

        var scanfType = LLVM.FunctionType(LLVM.Int32Type(), printfParamTypes, true);
        LLVM.AddFunction(module, "scanf", scanfType);

        var mallocType = LLVM.FunctionType(i8PtrType, [LLVM.Int64Type()], false);
        LLVM.AddFunction(module, "malloc", mallocType);
    }

    private object? VisitBinaryExpression(
        GlyphScriptParser.ExpressionContext context,
        GlyphScriptValue left,
        GlyphScriptValue right,
        OperationKind operationKind)
    {
        var operationSignature = new OperationSignature(operationKind, [left.Type, right.Type]);

        var operation = _availableOperations.GetValueOrDefault(operationSignature);
        if (operation is null)
            throw new OperationNotAvailableException(context, operationSignature);

        return operation(context, [left, right]);
    }

    public void Dispose()
    {
        LLVM.DisposeBuilder(_llvmBuilder);
    }

    public override object? VisitWhileStatement(GlyphScriptParser.WhileStatementContext context)
    {
        var currentFunction = LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_llvmBuilder));
        var conditionBlock = LLVM.AppendBasicBlock(currentFunction, "while_cond");
        var loopBlock = LLVM.AppendBasicBlock(currentFunction, "while_body");
        var afterLoopBlock = LLVM.AppendBasicBlock(currentFunction, "while_end");


        LLVM.BuildBr(_llvmBuilder, conditionBlock);


        LLVM.PositionBuilderAtEnd(_llvmBuilder, conditionBlock);
        var conditionValue = Visit(context.expression()) as GlyphScriptValue ??
                             throw new InvalidOperationException("Failed to evaluate while condition expression");

        if (conditionValue.Type != GlyphScriptType.Boolean)
            throw new InvalidSyntaxException(context,
                $"Condition in while statement must be a boolean expression. Found: {conditionValue.Type}");


        LLVM.BuildCondBr(_llvmBuilder, conditionValue.Value, loopBlock, afterLoopBlock);


        LLVM.PositionBuilderAtEnd(_llvmBuilder, loopBlock);
        Visit(context.block());


        LLVM.BuildBr(_llvmBuilder, conditionBlock);


        LLVM.PositionBuilderAtEnd(_llvmBuilder, afterLoopBlock);

        return null;
    }

    public override object? VisitFunctionDeclaration(GlyphScriptParser.FunctionDeclarationContext context)
    {
        var functionName = context.ID().GetText();
        if (_currentScope.TryGetFunction(functionName, out var functionInfo))
        {
            return functionInfo;
        }

        throw new InvalidOperationException(
            $"Function '{functionName}' not found. This indicates an issue with the compilation pipeline.");
    }

    private object? VisitFunctionSignature(GlyphScriptParser.FunctionDeclarationContext context)
    {
        var returnType = GetTypeFromContext(context.type());
        var functionName = context.ID().GetText();


        var parameters = new List<(GlyphScriptType Type, string Name)>();
        if (context.parameterList() != null)
        {
            foreach (var param in context.parameterList().parameter())
            {
                var paramType = GetTypeFromContext(param.type());
                var paramName = param.ID().GetText();
                parameters.Add((paramType, paramName));
            }
        }


        var paramTypes = parameters.Select(p => GetLlvmType(p.Type)).ToArray();
        var llvmReturnType = GetLlvmType(returnType);
        var functionType = LLVM.FunctionType(llvmReturnType, paramTypes, false);


        var llvmFunction = LLVM.AddFunction(LlvmModule, functionName, functionType);
        LLVM.SetFunctionCallConv(llvmFunction, (uint)LLVMCallConv.LLVMCCallConv);


        var functionInfo = new FunctionInfo(functionName, returnType, parameters.ToArray(), llvmFunction);

        if (_currentScope.HasLocalFunction(functionName))
            throw new InvalidOperationException($"Function '{functionName}' is already defined in the current scope.");

        _currentScope.DeclareFunction(functionName, functionInfo);

        return functionInfo;
    }

    private object? VisitFunctionBody(GlyphScriptParser.FunctionDeclarationContext context)
    {
        var functionName = context.ID().GetText();

        if (!_currentScope.TryGetFunction(functionName, out var functionInfo))
            throw new InvalidOperationException($"Function '{functionName}' signature not found.");

        var returnType = functionInfo.ReturnType;


        var previousBlock = LLVM.GetInsertBlock(_llvmBuilder);


        var entryBlock = LLVM.AppendBasicBlock(functionInfo.LlvmFunction, "entry");
        LLVM.PositionBuilderAtEnd(_llvmBuilder, entryBlock);


        EnterFunction(functionInfo);
        EnterScope();

        try
        {
            for (int i = 0; i < functionInfo.Parameters.Length; i++)
            {
                var (paramType, paramName) = functionInfo.Parameters[i];

                var param = LLVM.GetParam(functionInfo.LlvmFunction, (uint)i);


                var paramAlloca = LLVM.BuildAlloca(_llvmBuilder, GetLlvmType(paramType), paramName);
                LLVM.BuildStore(_llvmBuilder, param, paramAlloca);

                var paramValue = new GlyphScriptValue(paramAlloca, paramType);
                _currentScope.DeclareVariable(paramName, paramValue);
            }


            Visit(context.block());


            if (returnType == GlyphScriptType.Void)
            {
                var currentBlock = LLVM.GetInsertBlock(_llvmBuilder);
                var terminator = LLVM.GetBasicBlockTerminator(currentBlock);
                if (terminator.Pointer == IntPtr.Zero)
                {
                    LLVM.BuildRetVoid(_llvmBuilder);
                }
            }
        }
        finally
        {
            ExitScope();
            ExitFunction();


            LLVM.PositionBuilderAtEnd(_llvmBuilder, previousBlock);
        }

        return functionInfo;
    }

    public override object? VisitFunctionCall(GlyphScriptParser.FunctionCallContext context)
    {
        var functionName = context.ID().GetText();

        if (!_currentScope.TryGetFunction(functionName, out var functionInfo))
            throw new InvalidOperationException($"Function '{functionName}' is not defined.");


        var arguments = new List<GlyphScriptValue>();
        if (context.argumentList() != null)
        {
            foreach (var argExpr in context.argumentList().expression())
            {
                var argValue = Visit(argExpr) as GlyphScriptValue ??
                               throw new InvalidOperationException("Failed to evaluate function argument");
                arguments.Add(argValue);
            }
        }


        if (arguments.Count != functionInfo.Parameters.Length)
            throw new InvalidOperationException(
                $"Function '{functionName}' expects {functionInfo.Parameters.Length} arguments but got {arguments.Count}");


        for (int i = 0; i < arguments.Count; i++)
        {
            var expectedType = functionInfo.Parameters[i].Type;
            var actualType = arguments[i].Type;

            if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(expectedType, actualType))
                throw new InvalidOperationException(
                    $"Function '{functionName}' parameter {i + 1} expects {expectedType} but got {actualType}");
        }


        var argValues = arguments.Select(arg => arg.Value).ToArray();

        if (functionInfo.ReturnType == GlyphScriptType.Void)
        {
            LLVM.BuildCall(_llvmBuilder, functionInfo.LlvmFunction, argValues, "");
            return null;
        }
        else
        {
            var result = LLVM.BuildCall(_llvmBuilder, functionInfo.LlvmFunction, argValues, "call");
            var returnValue = new GlyphScriptValue(result, functionInfo.ReturnType);
            return returnValue;
        }
    }

    public override object? VisitFunctionCallExp(GlyphScriptParser.FunctionCallExpContext context)
    {
        return Visit(context.functionCall());
    }

    public override object? VisitReturnStatement(GlyphScriptParser.ReturnStatementContext context)
    {
        if (_currentFunction == null)
            throw new InvalidOperationException("Return statement can only be used inside a function");

        if (context.expression() != null)
        {
            var returnValue = Visit(context.expression()) as GlyphScriptValue ??
                              throw new InvalidOperationException("Failed to evaluate return expression");

            if (_currentFunction.ReturnType == GlyphScriptType.Void)
                throw new InvalidOperationException(
                    $"Cannot return a value from void function '{_currentFunction.Name}'");

            if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(_currentFunction.ReturnType,
                    returnValue.Type))
                throw new InvalidOperationException(
                    $"Function '{_currentFunction.Name}' expects return type {_currentFunction.ReturnType} but got {returnValue.Type}");

            LLVM.BuildRet(_llvmBuilder, returnValue.Value);
        }
        else
        {
            if (_currentFunction.ReturnType != GlyphScriptType.Void)
                throw new InvalidOperationException(
                    $"Function '{_currentFunction.Name}' expects return type {_currentFunction.ReturnType} but got void");

            LLVM.BuildRetVoid(_llvmBuilder);
        }

        return null;
    }

    public override object? VisitStructDeclaration(GlyphScriptParser.StructDeclarationContext context)
    {
        var structName = context.ID().GetText();
        var fields = new List<(GlyphScriptType Type, string FieldName)>();


        foreach (var fieldContext in context.structField())
        {
            var fieldType = GetTypeFromContext(fieldContext.type());
            var fieldName = fieldContext.ID().GetText();
            fields.Add((fieldType, fieldName));
        }


        var structInfo = new StructTypeInfo(structName, fields.ToArray());


        var fieldTypes = fields.Select(f => GetLlvmType(f.Type)).ToArray();
        var llvmStructType = LLVM.StructType(fieldTypes, false);


        _currentScope.DeclareStructType(structName, structInfo, llvmStructType);

        return structInfo;
    }

    public override object? VisitClassDeclaration(GlyphScriptParser.ClassDeclarationContext context)
    {
        var className = context.ID().GetText();
        var fields = new List<(GlyphScriptType Type, string FieldName)>();
        var methods = new List<ClassMethodInfo>();


        foreach (var fieldContext in context.classField())
        {
            var fieldType = GetTypeFromContext(fieldContext.type());
            var fieldName = fieldContext.ID().GetText();
            fields.Add((fieldType, fieldName));
        }

        foreach (var methodContext in context.classMethod())
        {
            var returnType = GetTypeFromContext(methodContext.type());
            var methodName = methodContext.ID().GetText();


            var parameters = new List<(GlyphScriptType Type, string Name)>();
            if (methodContext.parameterList() != null)
            {
                foreach (var param in methodContext.parameterList().parameter())
                {
                    var paramType = GetTypeFromContext(param.type());
                    var paramName = param.ID().GetText();
                    parameters.Add((paramType, paramName));
                }
            }


            var methodParameters = new List<(GlyphScriptType Type, string Name)>
            {
                (GlyphScriptType.Class, "this")
            };
            methodParameters.AddRange(parameters);


            var paramTypes = new List<LLVMTypeRef>();


            paramTypes.Add(LLVM.PointerType(LLVM.Int8Type(), 0));


            for (int i = 1; i < methodParameters.Count; i++)
            {
                var paramType = methodParameters[i].Type;
                if (paramType == GlyphScriptType.Class)
                {
                    paramTypes.Add(LLVM.PointerType(LLVM.Int8Type(), 0));
                }
                else
                {
                    paramTypes.Add(GetLlvmType(paramType));
                }
            }

            var llvmReturnType = returnType == GlyphScriptType.Class
                ? LLVM.PointerType(LLVM.Int8Type(), 0)
                : GetLlvmType(returnType);
            var functionType = LLVM.FunctionType(llvmReturnType, paramTypes.ToArray(), false);


            var mangledName = $"{className}_{methodName}";
            var llvmFunction = LLVM.AddFunction(LlvmModule, mangledName, functionType);
            LLVM.SetFunctionCallConv(llvmFunction, (uint)LLVMCallConv.LLVMCCallConv);

            var methodInfo = new ClassMethodInfo(methodName, returnType, methodParameters.ToArray(), llvmFunction);
            methods.Add(methodInfo);
        }


        var classInfo = new ClassTypeInfo(className, fields.ToArray(), methods.ToArray());


        var fieldTypes = fields.Select(f => GetLlvmType(f.Type)).ToArray();
        var llvmClassType = LLVM.StructType(fieldTypes, false);

        _currentScope.DeclareClassType(className, classInfo, llvmClassType);

        return classInfo;
    }

    public override object? VisitStructInstantiation(GlyphScriptParser.StructInstantiationContext context)
    {
        var structTypeName = context.ID(0).GetText();
        var variableName = context.ID(1).GetText();


        if (!_currentScope.TryGetStructType(structTypeName, out var structInfo, out var llvmStructType))
            throw new InvalidOperationException($"Structure type '{structTypeName}' is not defined");


        var structAlloca = LLVM.BuildAlloca(_llvmBuilder, llvmStructType, variableName);


        for (int i = 0; i < structInfo.Fields.Length; i++)
        {
            var field = structInfo.Fields[i];
            var operationSignature = new OperationSignature(OperationKind.DefaultValue, [field.Type]);
            var createDefaultValueOperation = _availableOperations.GetValueOrDefault(operationSignature);

            if (createDefaultValueOperation != null)
            {
                var defaultValue = createDefaultValueOperation(context, []) as GlyphScriptValue;
                if (defaultValue != null)
                {
                    var fieldPtr = LLVM.BuildStructGEP(_llvmBuilder, structAlloca, (uint)i,
                        $"{variableName}_{field.FieldName}_init_ptr");
                    LLVM.BuildStore(_llvmBuilder, defaultValue.Value, fieldPtr);
                }
            }
        }

        var structValue = new GlyphScriptValue(structAlloca, GlyphScriptType.Struct, null, structInfo);
        _currentScope.DeclareVariable(variableName, structValue);

        return structValue;
    }

    public override object? VisitClassInstantiation(GlyphScriptParser.ClassInstantiationContext context)
    {
        var classTypeName = context.ID(0).GetText();
        var variableName = context.ID(1).GetText();


        if (!_currentScope.TryGetClassType(classTypeName, out var classInfo, out var llvmClassType))
            throw new InvalidOperationException($"Class type '{classTypeName}' is not defined");


        var classAlloca = LLVM.BuildAlloca(_llvmBuilder, llvmClassType, variableName);


        for (int i = 0; i < classInfo.Fields.Length; i++)
        {
            var field = classInfo.Fields[i];
            var operationSignature = new OperationSignature(OperationKind.DefaultValue, [field.Type]);
            var createDefaultValueOperation = _availableOperations.GetValueOrDefault(operationSignature);

            if (createDefaultValueOperation != null)
            {
                var defaultValue = createDefaultValueOperation(context, []) as GlyphScriptValue;
                if (defaultValue != null)
                {
                    var fieldPtr = LLVM.BuildStructGEP(_llvmBuilder, classAlloca, (uint)i,
                        $"{variableName}_{field.FieldName}_init_ptr");
                    LLVM.BuildStore(_llvmBuilder, defaultValue.Value, fieldPtr);
                }
            }
        }

        var classValue = new GlyphScriptValue(classAlloca, GlyphScriptType.Class, null, null, classInfo);
        _currentScope.DeclareVariable(variableName, classValue);

        return classValue;
    }

    public override object? VisitFieldAccessExp(GlyphScriptParser.FieldAccessExpContext context)
    {
        var expression = Visit(context.expression()) as GlyphScriptValue ??
                         throw new InvalidOperationException("Failed to resolve expression");

        var fieldName = context.ID().GetText();


        if (expression.Type == GlyphScriptType.Struct && expression.StructInfo != null)
        {
            var fieldIndex = -1;
            GlyphScriptType fieldType = GlyphScriptType.Void;

            for (int i = 0; i < expression.StructInfo.Fields.Length; i++)
            {
                if (expression.StructInfo.Fields[i].FieldName == fieldName)
                {
                    fieldIndex = i;
                    fieldType = expression.StructInfo.Fields[i].Type;
                    break;
                }
            }

            if (fieldIndex == -1)
                throw new InvalidSyntaxException(context,
                    $"Field '{fieldName}' does not exist in structure '{expression.StructInfo.Name}'");


            var fieldPtr = LLVM.BuildStructGEP(_llvmBuilder, expression.Value, (uint)fieldIndex,
                $"{expression.StructInfo.Name}_{fieldName}_ptr");


            var fieldValue = LLVM.BuildLoad(_llvmBuilder, fieldPtr, $"{expression.StructInfo.Name}_{fieldName}");

            return new GlyphScriptValue(fieldValue, fieldType);
        }

        else if (expression.Type == GlyphScriptType.Class && expression.ClassInfo != null)
        {
            var fieldIndex = -1;
            GlyphScriptType fieldType = GlyphScriptType.Void;

            for (int i = 0; i < expression.ClassInfo.Fields.Length; i++)
            {
                if (expression.ClassInfo.Fields[i].FieldName == fieldName)
                {
                    fieldIndex = i;
                    fieldType = expression.ClassInfo.Fields[i].Type;
                    break;
                }
            }

            if (fieldIndex == -1)
                throw new InvalidSyntaxException(context,
                    $"Field '{fieldName}' does not exist in class '{expression.ClassInfo.Name}'");


            var fieldPtr = LLVM.BuildStructGEP(_llvmBuilder, expression.Value, (uint)fieldIndex,
                $"{expression.ClassInfo.Name}_{fieldName}_ptr");


            var fieldValue = LLVM.BuildLoad(_llvmBuilder, fieldPtr, $"{expression.ClassInfo.Name}_{fieldName}");

            return new GlyphScriptValue(fieldValue, fieldType);
        }
        else
        {
            throw new InvalidSyntaxException(context, "Field access is only valid on structure and class types");
        }
    }

    public override object? VisitMethodCallExp(GlyphScriptParser.MethodCallExpContext context)
    {
        var className = context.methodCall().ID(0).GetText();

        if (!_currentScope.TryGetVariable(className, out var instanceVariable))
            throw new InvalidSyntaxException(context, $"Instance variable '{className}' is not defined");
        var instanceExpression = instanceVariable;
        if (instanceExpression.Type != GlyphScriptType.Class || instanceExpression.ClassInfo == null)
            throw new InvalidSyntaxException(context, "Method calls are only valid on class instances");

        var methodName = context.methodCall().ID(1).GetText();


        ClassMethodInfo? methodInfo = null;
        foreach (var method in instanceExpression.ClassInfo.Methods)
        {
            if (method.Name == methodName)
            {
                methodInfo = method;
                break;
            }
        }

        if (methodInfo == null)
            throw new InvalidSyntaxException(context,
                $"Method '{methodName}' does not exist in class '{instanceExpression.ClassInfo.Name}'");


        var arguments = new List<GlyphScriptValue>();
        if (context.methodCall().argumentList() != null)
        {
            foreach (var argExpr in context.methodCall().argumentList().expression())
            {
                var argValue = Visit(argExpr) as GlyphScriptValue ??
                               throw new InvalidOperationException("Failed to evaluate method argument");
                arguments.Add(argValue);
            }
        }


        var expectedArgCount = methodInfo.Parameters.Length - 1;
        if (arguments.Count != expectedArgCount)
            throw new InvalidOperationException(
                $"Method '{methodName}' expects {expectedArgCount} arguments but got {arguments.Count}");


        for (int i = 0; i < arguments.Count; i++)
        {
            var expectedType = methodInfo.Parameters[i + 1].Type;
            var actualType = arguments[i].Type;

            if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(expectedType, actualType))
                throw new InvalidOperationException(
                    $"Method '{methodName}' parameter {i + 1} expects {expectedType} but got {actualType}");
        }


        var genericPointerType = LLVM.PointerType(LLVM.Int8Type(), 0);
        var castedInstancePtr =
            LLVM.BuildPointerCast(_llvmBuilder, instanceExpression.Value, genericPointerType, "instance_cast");

        var callArguments = new List<LLVMValueRef> { castedInstancePtr };
        callArguments.AddRange(arguments.Select(arg => arg.Value));

        if (methodInfo.ReturnType == GlyphScriptType.Void)
        {
            LLVM.BuildCall(_llvmBuilder, methodInfo.LlvmFunction, callArguments.ToArray(), "");
            return null;
        }
        else
        {
            var result = LLVM.BuildCall(_llvmBuilder, methodInfo.LlvmFunction, callArguments.ToArray(), "method_call");
            return new GlyphScriptValue(result, methodInfo.ReturnType);
        }
    }

    public override object? VisitMethodCall(GlyphScriptParser.MethodCallContext context)
    {
        var className = context.ID(0).GetText();

        if (!_currentScope.TryGetVariable(className, out var instanceVariable))
            throw new InvalidSyntaxException(context, $"Instance variable '{className}' is not defined");
        var instanceExpression = instanceVariable;
        if (instanceExpression.Type != GlyphScriptType.Class || instanceExpression.ClassInfo == null)
            throw new InvalidSyntaxException(context, "Method calls are only valid on class instances");

        var methodName = context.ID(1).GetText();


        ClassMethodInfo? methodInfo = null;
        foreach (var method in instanceExpression.ClassInfo.Methods)
        {
            if (method.Name == methodName)
            {
                methodInfo = method;
                break;
            }
        }

        if (methodInfo == null)
            throw new InvalidSyntaxException(context,
                $"Method '{methodName}' does not exist in class '{instanceExpression.ClassInfo.Name}'");


        var arguments = new List<GlyphScriptValue>();
        if (context.argumentList() != null)
        {
            foreach (var argExpr in context.argumentList().expression())
            {
                var argValue = Visit(argExpr) as GlyphScriptValue ??
                               throw new InvalidOperationException("Failed to evaluate method argument");
                arguments.Add(argValue);
            }
        }


        var expectedArgCount = methodInfo.Parameters.Length - 1;
        if (arguments.Count != expectedArgCount)
            throw new InvalidOperationException(
                $"Method '{methodName}' expects {expectedArgCount} arguments but got {arguments.Count}");


        for (int i = 0; i < arguments.Count; i++)
        {
            var expectedType = methodInfo.Parameters[i + 1].Type;
            var actualType = arguments[i].Type;

            if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(expectedType, actualType))
                throw new InvalidOperationException(
                    $"Method '{methodName}' parameter {i + 1} expects {expectedType} but got {actualType}");
        }


        var genericPointerType = LLVM.PointerType(LLVM.Int8Type(), 0);
        var castedInstancePtr =
            LLVM.BuildPointerCast(_llvmBuilder, instanceExpression.Value, genericPointerType, "instance_cast");

        var callArguments = new List<LLVMValueRef> { castedInstancePtr };
        callArguments.AddRange(arguments.Select(arg => arg.Value));


        LLVM.BuildCall(_llvmBuilder, methodInfo.LlvmFunction, callArguments.ToArray(), "");

        return null;
    }

    private object? VisitClassMethodBodies(GlyphScriptParser.ClassDeclarationContext context)
    {
        var className = context.ID().GetText();


        if (!_currentScope.TryGetClassType(className, out var classInfo, out var llvmClassType))
            throw new InvalidOperationException($"Class '{className}' not found during method body processing.");


        foreach (var methodContext in context.classMethod())
        {
            var methodName = methodContext.ID().GetText();


            ClassMethodInfo? methodInfo = null;
            foreach (var method in classInfo.Methods)
            {
                if (method.Name == methodName)
                {
                    methodInfo = method;
                    break;
                }
            }

            if (methodInfo == null)
                throw new InvalidOperationException($"Method '{methodName}' not found in class '{className}'.");

            _logger.LogDebug("Processing method body: {ClassName}.{MethodName}", className, methodName);


            var previousBlock = LLVM.GetInsertBlock(_llvmBuilder);


            var entryBlock = LLVM.AppendBasicBlock(methodInfo.LlvmFunction, "entry");
            LLVM.PositionBuilderAtEnd(_llvmBuilder, entryBlock);


            EnterFunction(new FunctionInfo(methodInfo.Name, methodInfo.ReturnType, methodInfo.Parameters,
                methodInfo.LlvmFunction));
            EnterScope();

            try
            {
                for (int i = 0; i < methodInfo.Parameters.Length; i++)
                {
                    var (paramType, paramName) = methodInfo.Parameters[i];
                    var param = LLVM.GetParam(methodInfo.LlvmFunction, (uint)i);


                    if (i == 0 && paramName == "this")
                    {
                        var thisAlloca = LLVM.BuildAlloca(_llvmBuilder, LLVM.PointerType(llvmClassType, 0), "this");

                        var castedThis = LLVM.BuildPointerCast(_llvmBuilder, param, LLVM.PointerType(llvmClassType, 0),
                            "this_cast");
                        LLVM.BuildStore(_llvmBuilder, castedThis, thisAlloca);

                        var thisValue = new GlyphScriptValue(thisAlloca, GlyphScriptType.Class, null, null, classInfo);
                        _currentScope.DeclareVariable("this", thisValue);
                    }
                    else
                    {
                        var paramAlloca = LLVM.BuildAlloca(_llvmBuilder, GetLlvmType(paramType), paramName);
                        LLVM.BuildStore(_llvmBuilder, param, paramAlloca);

                        var paramValue = new GlyphScriptValue(paramAlloca, paramType);
                        _currentScope.DeclareVariable(paramName, paramValue);
                    }
                }


                Visit(methodContext.block());


                if (methodInfo.ReturnType == GlyphScriptType.Void)
                {
                    var currentBlock = LLVM.GetInsertBlock(_llvmBuilder);
                    var terminator = LLVM.GetBasicBlockTerminator(currentBlock);
                    if (terminator.Pointer == IntPtr.Zero)
                    {
                        LLVM.BuildRetVoid(_llvmBuilder);
                    }
                }
            }
            finally
            {
                ExitScope();
                ExitFunction();


                LLVM.PositionBuilderAtEnd(_llvmBuilder, previousBlock);
            }
        }

        return classInfo;
    }
}
