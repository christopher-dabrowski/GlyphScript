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

    // Function context tracking
    private FunctionInfo? _currentFunction;
    private readonly Stack<FunctionInfo> _functionStack = new();

    public LlvmVisitor(LLVMModuleRef llvmModule, ILogger<GlyphScriptLlvmCompiler>? logger = null)
    {
        LlvmModule = llvmModule;
        _logger = logger ?? NullLogger<GlyphScriptLlvmCompiler>.Instance;

        // Initialize the global scope
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

    /// <summary>
    /// Creates and enters a new variable scope
    /// </summary>
    private void EnterScope()
    {
        var newScope = new VariableScope(_currentScope);
        _scopeStack.Push(newScope);
        _currentScope = newScope;
    }

    /// <summary>
    /// Exits the current variable scope and returns to the parent scope
    /// </summary>
    private void ExitScope()
    {
        if (_scopeStack.Count <= 1)
            throw new InvalidOperationException("Cannot exit the global scope");

        _scopeStack.Pop();
        _currentScope = _scopeStack.Peek();
    }

    /// <summary>
    /// Enters a function context
    /// </summary>
    private void EnterFunction(FunctionInfo functionInfo)
    {
        _functionStack.Push(functionInfo);
        _currentFunction = functionInfo;
    }

    /// <summary>
    /// Exits the current function context
    /// </summary>
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

        // First pass: declare all function signatures to allow forward references
        _logger.LogDebug("Starting first pass - function signatures");
        foreach (var statement in statements)
        {
            if (statement.functionDeclaration() != null)
            {
                _logger.LogDebug("Processing function signature: {FunctionName}", statement.functionDeclaration().ID().GetText());
                VisitFunctionSignature(statement.functionDeclaration());
            }
        }

        // Second pass: declare all global variables with default values
        _logger.LogDebug("Starting second pass - global variables");
        foreach (var statement in statements)
        {
            if (statement.functionDeclaration() == null && statement.declaration() != null)
            {
                var declaration = statement.declaration();
                _logger.LogDebug("Processing declaration in second pass");

                // Handle default declarations (e.g., 🔢 x)
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

                    // Get default value for the type
                    var operationSignature = new OperationSignature(OperationKind.DefaultValue, [type]);
                    var createDefaultValueOperation = _availableOperations.GetValueOrDefault(operationSignature);
                    if (createDefaultValueOperation is null)
                        throw new OperationNotAvailableException(defaultDecl, operationSignature);

                    var defaultValue = createDefaultValueOperation(defaultDecl, [])
                        ?? throw new InvalidOperationException($"Failed to create default value for type {type}");

                    // Create the global variable with default value
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
                // Handle initializing declarations (e.g., 📦🔢 ages = [21, 34, 27])
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

                    // Visit(initDecl);
                    ArrayTypeInfo? arrayInfo = null;
                    if (type == GlyphScriptType.Array)
                        arrayInfo = Visit(initDecl.type().arrayOfType()) as ArrayTypeInfo;

                    // Create the global variable - for arrays use null pointer, for scalars use zero
                    var llvmType = GetLlvmType(type, arrayInfo);
                    var variable = LLVM.AddGlobal(LlvmModule, llvmType, id);

                    LLVMValueRef initializer;
                    if (type == GlyphScriptType.Array)
                    {
                        // For arrays, initialize with null pointer
                        initializer = LLVM.ConstPointerNull(llvmType);
                    }
                    else
                    {
                        // For scalar types, get their default value
                        var operationSignature = new OperationSignature(OperationKind.DefaultValue, [type]);
                        var createDefaultValueOperation = _availableOperations.GetValueOrDefault(operationSignature);
                        if (createDefaultValueOperation is null)
                            throw new OperationNotAvailableException(initDecl, operationSignature);

                        var defaultValue = createDefaultValueOperation(initDecl, [])
                            ?? throw new InvalidOperationException($"Failed to create default value for type {type}");
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

        // Third pass: process function bodies (now all globals are declared)
        _logger.LogDebug("Starting third pass - function bodies");
        foreach (var statement in statements)
        {
            if (statement.functionDeclaration() != null)
            {
                _logger.LogDebug("Processing function body: {FunctionName}", statement.functionDeclaration().ID().GetText());
                VisitFunctionBody(statement.functionDeclaration());
            }
        }

        // Create main function for the program execution
        _logger.LogDebug("Creating main function");
        LlvmHelper.CreateMain(LlvmModule, _llvmBuilder);

        // Fourth pass: process all statements in order (including initializing declarations)
        _logger.LogDebug("Starting fourth pass - processing all statements");
        foreach (var statement in statements)
        {
            if (statement.functionDeclaration() == null)
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

        // Get array type information if this is an array
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

        // Check for variable redeclaration in the current scope only
        // Exception: Allow re-declaration only for global variables during different compilation passes
        // This happens when initializing declarations (e.g., 📦🔢 ages = [21, 34, 27])
        // are processed in the second pass (for arrays) and fourth pass (for all declarations)
        if (_currentScope.HasLocalVariable(id))
        {
            // Allow reuse only if we're in global scope (first scope) AND this is an existing global variable from a previous pass
            if (_scopeStack.Count == 1 && _currentScope.TryGetVariable(id, out var existingVariable))
            {
                // This is a global variable being reprocessed in a different pass
                return existingVariable;
            }
            else
            {
                // This is a local variable redeclaration, which is not allowed
                throw new InvalidOperationException($"Variable '{id}' is already defined.");
            }
        }

        var llvmType = GetLlvmType(type, arrayInfo);
        LLVMValueRef variable;

        // Check if we're in global scope (first scope on the stack, not nested)
        bool isGlobalScope = _scopeStack.Count == 1;

        if (isGlobalScope)
        {
            // Create global variable
            variable = LLVM.AddGlobal(LlvmModule, llvmType, id);
            LLVM.SetInitializer(variable, value.Value);
        }
        else
        {
            // Create local variable (for block scopes or function scopes)
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

        // Handle automatic type detection
        if (type == GlyphScriptType.Auto)
        {
            _logger.LogDebug("Auto type detected for variable '{VariableId}'. Expression type: {ExpressionType}", id, expressionValue.Type);
            type = expressionValue.Type;

            // If the expression is an array, copy the array info
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

        // Check if the variable is already declared in the CURRENT scope only
        // For global scope: allow reprocessing during multi-pass compilation
        // For local scopes: declarations always create new variables (shadowing outer scopes)
        if (_currentScope.HasLocalVariable(id))
        {
            // For global scope, allow reprocessing during different compilation passes
            if (_scopeStack.Count == 1 && _currentScope.TryGetVariable(id, out var existingGlobalVariable))
            {
                // This is a global variable being reprocessed in a different pass
                LLVM.BuildStore(_llvmBuilder, expressionValue.Value, existingGlobalVariable.Value);
                return existingGlobalVariable;
            }
            else
            {
                // This is a duplicate declaration in the same scope, which is not allowed
                throw new DuplicateVariableDeclarationException(context) { VariableName = id };
            }
        }

        var llvmType = GetLlvmType(type, arrayInfo);
        LLVMValueRef variable;

        // Check if we're in global scope (first scope on the stack, not nested)
        bool isGlobalScope = _scopeStack.Count == 1;

        if (isGlobalScope)
        {
            // For global variables that are initialized with non-constant expressions (like function calls),
            // we need to create the variable as uninitialized and then assign the value in main
            variable = LLVM.AddGlobal(LlvmModule, llvmType, id);

            // Check if the expression value is a constant
            var isConstant = LLVM.IsConstant(expressionValue.Value);

            if (isConstant)
            {
                LLVM.SetInitializer(variable, expressionValue.Value);
            }
            else
            {
                // For non-constant expressions, we'll initialize with a default value
                // and store the actual value during main execution
                LLVM.SetInitializer(variable, LLVM.ConstInt(llvmType, 0, false));

                // Store the actual value now (we're in main function context)
                LLVM.BuildStore(_llvmBuilder, expressionValue.Value, variable);
            }
        }
        else
        {
            // Create local variable (for block scopes or function scopes)
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
        // Handle field assignment: ID '.' ID '=' expression
        if (context.ID().Length == 2)
            return FieldAssignmentOperation(context);

        // Handle array element assignment: ID '[' expression ']' '=' expression  
        if (context.expression().Length > 1)
            return ArrayAssignmentOperation(context);

        // Normal variable assignment: ID '=' expression
        var id = context.ID(0).GetText();
        var expressionValue = Visit(context.expression(0)) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to create expression");

        if (!_currentScope.TryGetVariable(id, out var variable))
            throw new UndefinedVariableUsageException(context) { VariableName = id };

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

        if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(arrayVariable.ArrayInfo.ElementType, rhsValue.Type))
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
        var structId = context.ID(0).GetText();
        var fieldName = context.ID(1).GetText();
        var rhsValue = Visit(context.expression(0)) as GlyphScriptValue ??
                       throw new InvalidOperationException("Failed to create expression value");

        if (!_currentScope.TryGetVariable(structId, out var structVariable))
            throw new UndefinedVariableUsageException(context) { VariableName = structId };

        if (structVariable.Type != GlyphScriptType.Struct || structVariable.StructInfo == null)
            throw new InvalidSyntaxException(context, $"Variable '{structId}' is not a structure");

        // Find the field in the structure
        var fieldIndex = -1;
        GlyphScriptType fieldType = GlyphScriptType.Void;
        
        for (int i = 0; i < structVariable.StructInfo.Fields.Length; i++)
        {
            if (structVariable.StructInfo.Fields[i].FieldName == fieldName)
            {
                fieldIndex = i;
                fieldType = structVariable.StructInfo.Fields[i].Type;
                break;
            }
        }

        if (fieldIndex == -1)
            throw new InvalidSyntaxException(context, $"Field '{fieldName}' does not exist in structure '{structVariable.StructInfo.Name}'");

        if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(fieldType, rhsValue.Type))
        {
            throw new AssignmentOfInvalidTypeException(context)
            {
                VariableName = $"{structId}.{fieldName}",
                VariableGlyphScriptType = fieldType,
                ValueGlyphScriptType = rhsValue.Type
            };
        }

        // Get pointer to the field using GEP (GetElementPtr)
        var fieldPtr = LLVM.BuildStructGEP(_llvmBuilder, structVariable.Value, (uint)fieldIndex, $"{structId}_{fieldName}_ptr");
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
            throw new InvalidSyntaxException(context, $"Condition in if statement must be a boolean expression. Found: {conditionValue.Type}");

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

        // The 'then' block creates its own scope via the Visit(block) call
        LLVM.PositionBuilderAtEnd(_llvmBuilder, thenBlock);
        Visit(context.block(0));
        LLVM.BuildBr(_llvmBuilder, mergeBlock);

        if (elseBlock != null)
        {
            // The 'else' block creates its own scope via the Visit(block) call
            LLVM.PositionBuilderAtEnd(_llvmBuilder, elseBlock.Value);
            Visit(context.block(1));
            LLVM.BuildBr(_llvmBuilder, mergeBlock);
        }

        LLVM.PositionBuilderAtEnd(_llvmBuilder, mergeBlock);

        return null;
    }

    public override object? VisitBlock(GlyphScriptParser.BlockContext context)
    {
        if (context.BEGIN() != null)
        {
            // Create a new scope for the block
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
                // Always exit the scope when leaving the block
                ExitScope();
            }
        }

        return Visit(context.statement(0));
    }

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

        return operation(context, [leftValue, rightValue]);
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

        return operation(context, [leftValue, rightValue]);
    }

    public override object? VisitPowerExp(GlyphScriptParser.PowerExpContext context)
    {
        const OperationKind operationKind = OperationKind.Power;

        var leftValue = Visit(context.expression(0)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the left expression");
        var rightValue = Visit(context.expression(1)) as GlyphScriptValue ?? throw new InvalidOperationException("Unable to resolve the right expression");

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
            throw new InvalidOperationException($"Variable '{id}' is not defined.");

        if (variable.Type == GlyphScriptType.Array && variable.ArrayInfo != null)
        {
            return variable with { Value = LLVM.BuildLoad(_llvmBuilder, variable.Value, id) };
        }
        
        if (variable.Type == GlyphScriptType.Struct && variable.StructInfo != null)
        {
            // For structures, return the variable with its allocation pointer (don't load)
            // This is needed for field access operations which use GEP
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
            var structName = context.ID().GetText();
            // Validate that the ID refers to a defined struct type
            if (!_currentScope.TryGetStructType(structName, out _, out _))
            {
                throw new InvalidSyntaxException(context, $"Undefined struct type '{structName}'");
            }
            return GlyphScriptType.Struct;
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
            GlyphScriptType.Struct => throw new InvalidOperationException("Struct type requires specific structure information. Use the overload with StructTypeInfo."),
            GlyphScriptType.Auto => throw new InvalidOperationException("Auto type should have been resolved to a concrete type before reaching LLVM type mapping"),
            _ => throw new InvalidOperationException($"Unsupported type: {glyphScriptType}")
        };
    }

    private LLVMTypeRef GetLlvmType(GlyphScriptType glyphScriptType, StructTypeInfo structInfo)
    {
        if (glyphScriptType != GlyphScriptType.Struct)
            throw new InvalidOperationException("This overload is only for struct types");

        // Check if we already have the LLVM type for this struct
        if (_currentScope.TryGetStructType(structInfo.Name, out _, out var existingLlvmType))
            return existingLlvmType;

        // Create LLVM struct type
        var fieldTypes = structInfo.Fields.Select(f => GetLlvmType(f.Type)).ToArray();
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

        // Branch to the condition block
        LLVM.BuildBr(_llvmBuilder, conditionBlock);

        // Position at the condition block to evaluate the condition
        LLVM.PositionBuilderAtEnd(_llvmBuilder, conditionBlock);
        var conditionValue = Visit(context.expression()) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to evaluate while condition expression");

        if (conditionValue.Type != GlyphScriptType.Boolean)
            throw new InvalidSyntaxException(context, $"Condition in while statement must be a boolean expression. Found: {conditionValue.Type}");

        // Branch based on the condition
        LLVM.BuildCondBr(_llvmBuilder, conditionValue.Value, loopBlock, afterLoopBlock);

        // Position at the loop body
        // The loop body creates its own scope via the Visit(block) call
        LLVM.PositionBuilderAtEnd(_llvmBuilder, loopBlock);
        Visit(context.block());

        // At the end of the loop body, jump back to the condition
        LLVM.BuildBr(_llvmBuilder, conditionBlock);

        // Position at the block after the loop
        LLVM.PositionBuilderAtEnd(_llvmBuilder, afterLoopBlock);

        return null;
    }

    public override object? VisitFunctionDeclaration(GlyphScriptParser.FunctionDeclarationContext context)
    {
        // Function declarations are handled in two phases by VisitFunctionSignature and VisitFunctionBody
        // This method should not be called directly in the normal visitor pattern
        var functionName = context.ID().GetText();
        if (_currentScope.TryGetFunction(functionName, out var functionInfo))
        {
            return functionInfo;
        }

        throw new InvalidOperationException($"Function '{functionName}' not found. This indicates an issue with the compilation pipeline.");
    }

    private object? VisitFunctionSignature(GlyphScriptParser.FunctionDeclarationContext context)
    {
        var returnType = GetTypeFromContext(context.type());
        var functionName = context.ID().GetText();

        // Parse parameters
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

        // Create LLVM function type
        var paramTypes = parameters.Select(p => GetLlvmType(p.Type)).ToArray();
        var llvmReturnType = GetLlvmType(returnType);
        var functionType = LLVM.FunctionType(llvmReturnType, paramTypes, false);

        // Create LLVM function
        var llvmFunction = LLVM.AddFunction(LlvmModule, functionName, functionType);
        LLVM.SetFunctionCallConv(llvmFunction, (uint)LLVMCallConv.LLVMCCallConv);

        // Create function info and register it
        var functionInfo = new FunctionInfo(functionName, returnType, parameters.ToArray(), llvmFunction);

        if (_currentScope.HasLocalFunction(functionName))
            throw new InvalidOperationException($"Function '{functionName}' is already defined in the current scope.");

        _currentScope.DeclareFunction(functionName, functionInfo);

        return functionInfo;
    }    private object? VisitFunctionBody(GlyphScriptParser.FunctionDeclarationContext context)
    {
        var functionName = context.ID().GetText();

        if (!_currentScope.TryGetFunction(functionName, out var functionInfo))
            throw new InvalidOperationException($"Function '{functionName}' signature not found.");

        var returnType = functionInfo.ReturnType;

        // Save the current builder position (main function)
        var previousBlock = LLVM.GetInsertBlock(_llvmBuilder);

        // Create entry block for function
        var entryBlock = LLVM.AppendBasicBlock(functionInfo.LlvmFunction, "entry");
        LLVM.PositionBuilderAtEnd(_llvmBuilder, entryBlock);

        // Enter function context and new scope for parameters
        EnterFunction(functionInfo);
        EnterScope();

        try
        {
            // Declare parameters as local variables
            for (int i = 0; i < functionInfo.Parameters.Length; i++)
            {
                var (paramType, paramName) = functionInfo.Parameters[i];

                var param = LLVM.GetParam(functionInfo.LlvmFunction, (uint)i);

                // Allocate space for parameter and store the parameter value
                var paramAlloca = LLVM.BuildAlloca(_llvmBuilder, GetLlvmType(paramType), paramName);
                LLVM.BuildStore(_llvmBuilder, param, paramAlloca);

                var paramValue = new GlyphScriptValue(paramAlloca, paramType);
                _currentScope.DeclareVariable(paramName, paramValue);
            }

            // Visit function body
            Visit(context.block());

            // If function is void and no explicit return, add void return
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

            // Restore the builder position back to the main function
            LLVM.PositionBuilderAtEnd(_llvmBuilder, previousBlock);
        }

        return functionInfo;
    }

    public override object? VisitFunctionCall(GlyphScriptParser.FunctionCallContext context)
    {
        var functionName = context.ID().GetText();

        if (!_currentScope.TryGetFunction(functionName, out var functionInfo))
            throw new InvalidOperationException($"Function '{functionName}' is not defined.");

        // Evaluate arguments
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

        // Validate argument count
        if (arguments.Count != functionInfo.Parameters.Length)
            throw new InvalidOperationException(
                $"Function '{functionName}' expects {functionInfo.Parameters.Length} arguments but got {arguments.Count}");

        // Validate argument types
        for (int i = 0; i < arguments.Count; i++)
        {
            var expectedType = functionInfo.Parameters[i].Type;
            var actualType = arguments[i].Type;

            if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(expectedType, actualType))
                throw new InvalidOperationException(
                    $"Function '{functionName}' parameter {i + 1} expects {expectedType} but got {actualType}");
        }

        // Call function
        var argValues = arguments.Select(arg => arg.Value).ToArray();

        if (functionInfo.ReturnType == GlyphScriptType.Void)
        {
            // For void functions, don't assign a name to the call
            LLVM.BuildCall(_llvmBuilder, functionInfo.LlvmFunction, argValues, "");
            return null;
        }
        else
        {
            // For non-void functions, assign a name to the call result
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
            // Return with value
            var returnValue = Visit(context.expression()) as GlyphScriptValue ??
                throw new InvalidOperationException("Failed to evaluate return expression");

            if (_currentFunction.ReturnType == GlyphScriptType.Void)
                throw new InvalidOperationException($"Cannot return a value from void function '{_currentFunction.Name}'");

            if (!_expressionResultTypeEngine.AreTypesCompatibleForAssignment(_currentFunction.ReturnType, returnValue.Type))
                throw new InvalidOperationException(
                    $"Function '{_currentFunction.Name}' expects return type {_currentFunction.ReturnType} but got {returnValue.Type}");

            LLVM.BuildRet(_llvmBuilder, returnValue.Value);
        }
        else
        {
            // Return without value (void)
            if (_currentFunction.ReturnType != GlyphScriptType.Void)
                throw new InvalidOperationException($"Function '{_currentFunction.Name}' expects return type {_currentFunction.ReturnType} but got void");

            LLVM.BuildRetVoid(_llvmBuilder);
        }

        return null;
    }

    public override object? VisitStructDeclaration(GlyphScriptParser.StructDeclarationContext context)
    {
        var structName = context.ID().GetText();
        var fields = new List<(GlyphScriptType Type, string FieldName)>();

        // Parse all fields
        foreach (var fieldContext in context.structField())
        {
            var fieldType = GetTypeFromContext(fieldContext.type());
            var fieldName = fieldContext.ID().GetText();
            fields.Add((fieldType, fieldName));
        }

        // Create structure type info
        var structInfo = new StructTypeInfo(structName, fields.ToArray());

        // Create LLVM struct type
        var fieldTypes = fields.Select(f => GetLlvmType(f.Type)).ToArray();
        var llvmStructType = LLVM.StructType(fieldTypes, false);

        // For now, we'll store the struct type info in the scope
        // In a full implementation, we'd have a type registry
        _currentScope.DeclareStructType(structName, structInfo, llvmStructType);

        return structInfo;
    }

    public override object? VisitStructInstantiation(GlyphScriptParser.StructInstantiationContext context)
    {
        var structTypeName = context.ID(0).GetText(); // First ID is the struct type name
        var variableName = context.ID(1).GetText();   // Second ID is the variable name
        
        // Get the structure type from scope
        if (!_currentScope.TryGetStructType(structTypeName, out var structInfo, out var llvmStructType))
            throw new InvalidOperationException($"Structure type '{structTypeName}' is not defined");

        // Create an instance of the structure
        var structAlloca = LLVM.BuildAlloca(_llvmBuilder, llvmStructType, variableName);

        // Initialize with default values
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
                    var fieldPtr = LLVM.BuildStructGEP(_llvmBuilder, structAlloca, (uint)i, $"{variableName}_{field.FieldName}_init_ptr");
                    LLVM.BuildStore(_llvmBuilder, defaultValue.Value, fieldPtr);
                }
            }
        }

        var structValue = new GlyphScriptValue(structAlloca, GlyphScriptType.Struct, null, structInfo);
        _currentScope.DeclareVariable(variableName, structValue);
        
        return structValue;
    }

    public override object? VisitFieldAccessExp(GlyphScriptParser.FieldAccessExpContext context)
    {
        var structExpression = Visit(context.expression()) as GlyphScriptValue ??
            throw new InvalidOperationException("Failed to resolve structure expression");

        if (structExpression.Type != GlyphScriptType.Struct || structExpression.StructInfo == null)
            throw new InvalidSyntaxException(context, "Field access is only valid on structure types");

        var fieldName = context.ID().GetText();

        // Find the field in the structure
        var fieldIndex = -1;
        GlyphScriptType fieldType = GlyphScriptType.Void;
        
        for (int i = 0; i < structExpression.StructInfo.Fields.Length; i++)
        {
            if (structExpression.StructInfo.Fields[i].FieldName == fieldName)
            {
                fieldIndex = i;
                fieldType = structExpression.StructInfo.Fields[i].Type;
                break;
            }
        }

        if (fieldIndex == -1)
            throw new InvalidSyntaxException(context, $"Field '{fieldName}' does not exist in structure '{structExpression.StructInfo.Name}'");

        // Get pointer to the field using GEP (GetElementPtr)
        var fieldPtr = LLVM.BuildStructGEP(_llvmBuilder, structExpression.Value, (uint)fieldIndex, $"{structExpression.StructInfo.Name}_{fieldName}_ptr");
        
        // Load the field value
        var fieldValue = LLVM.BuildLoad(_llvmBuilder, fieldPtr, $"{structExpression.StructInfo.Name}_{fieldName}");

        return new GlyphScriptValue(fieldValue, fieldType);
    }
}
