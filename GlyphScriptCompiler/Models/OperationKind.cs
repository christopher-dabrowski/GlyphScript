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

    Print,
    Read
}
