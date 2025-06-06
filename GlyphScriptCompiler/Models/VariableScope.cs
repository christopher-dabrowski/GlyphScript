using System.Diagnostics.CodeAnalysis;

namespace GlyphScriptCompiler.Models;

public class VariableScope
{
    private readonly Dictionary<string, GlyphScriptValue> _variables = [];
    private readonly Dictionary<string, FunctionInfo> _functions = [];
    private readonly Dictionary<string, (StructTypeInfo StructInfo, LLVMTypeRef LlvmType)> _structTypes = [];
    private readonly Dictionary<string, (ClassTypeInfo ClassInfo, LLVMTypeRef LlvmType)> _classTypes = [];
    private readonly VariableScope? _parent;

    public VariableScope(VariableScope? parent = null)
    {
        _parent = parent;
    }

    public void DeclareVariable(string name, GlyphScriptValue value)
    {
        if (!_variables.TryAdd(name, value))
            throw new InvalidOperationException($"Variable '{name}' is already defined in the current scope.");
    }

    public bool TryGetVariable(string name, [MaybeNullWhen(false)] out GlyphScriptValue value)
    {
        if (_variables.TryGetValue(name, out value))
            return true;

        if (_parent != null)
            return _parent.TryGetVariable(name, out value);

        return false;
    }

    public bool HasLocalVariable(string name)
    {
        return _variables.ContainsKey(name);
    }

    public bool UpdateVariable(string name, GlyphScriptValue value)
    {
        if (_variables.ContainsKey(name))
        {
            _variables[name] = value;
            return true;
        }

        if (_parent != null)
            return _parent.UpdateVariable(name, value);

        return false;
    }

    public void DeclareFunction(string name, FunctionInfo functionInfo)
    {
        if (!_functions.TryAdd(name, functionInfo))
            throw new InvalidOperationException($"Function '{name}' is already defined in the current scope.");
    }

    public bool TryGetFunction(string name, [MaybeNullWhen(false)] out FunctionInfo functionInfo)
    {
        if (_functions.TryGetValue(name, out functionInfo))
            return true;

        if (_parent != null)
            return _parent.TryGetFunction(name, out functionInfo);

        return false;
    }

    public bool HasLocalFunction(string name)
    {
        return _functions.ContainsKey(name);
    }

    public void DeclareStructType(string name, StructTypeInfo structInfo, LLVMTypeRef llvmType)
    {
        if (!_structTypes.TryAdd(name, (structInfo, llvmType)))
            throw new InvalidOperationException($"Structure type '{name}' is already defined in the current scope.");
    }

    public bool TryGetStructType(string name, [MaybeNullWhen(false)] out StructTypeInfo structInfo, out LLVMTypeRef llvmType)
    {
        if (_structTypes.TryGetValue(name, out var typeInfo))
        {
            structInfo = typeInfo.StructInfo;
            llvmType = typeInfo.LlvmType;
            return true;
        }

        if (_parent != null)
            return _parent.TryGetStructType(name, out structInfo, out llvmType);

        structInfo = null;
        llvmType = default;
        return false;
    }

    public bool HasLocalStructType(string name)
    {
        return _structTypes.ContainsKey(name);
    }

    public void DeclareClassType(string name, ClassTypeInfo classInfo, LLVMTypeRef llvmType)
    {
        if (!_classTypes.TryAdd(name, (classInfo, llvmType)))
            throw new InvalidOperationException($"Class type '{name}' is already defined in the current scope.");
    }

    public bool TryGetClassType(string name, [MaybeNullWhen(false)] out ClassTypeInfo classInfo, out LLVMTypeRef llvmType)
    {
        if (_classTypes.TryGetValue(name, out var typeInfo))
        {
            classInfo = typeInfo.ClassInfo;
            llvmType = typeInfo.LlvmType;
            return true;
        }

        if (_parent != null)
            return _parent.TryGetClassType(name, out classInfo, out llvmType);

        classInfo = null;
        llvmType = default;
        return false;
    }

    public bool HasLocalClassType(string name)
    {
        return _classTypes.ContainsKey(name);
    }
}
