namespace GlyphScriptCompiler.Models;

public enum OperationKind
{
    DefaultValue,
    ParseImmediate,

    Addition,
    Subtraction,
    Multiplication,
    Division,
    Power,
    Not,
    Xor,

    Print,
    Read,

    CreateArray,
    ArrayAccess,
    ArrayElementAssignment,

    Comparison,
    LessThan,
    GreaterThan,
}
