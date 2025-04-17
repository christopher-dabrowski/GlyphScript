// $antlr-format alignTrailingComments true, columnLimit 300, minEmptyLines 1, maxEmptyLinesToKeep 1, reflowComments false, useTab false
// $antlr-format allowShortRulesOnASingleLine false, allowShortBlocksOnASingleLine true, alignSemicolons hanging, alignColons hanging

grammar GlyphScript;

program
    : (statement? NEWLINE)* EOF
    ;

statement
    : declaration
    | print
    | assignment
    | read
    ;

print
    : WRITE ID
    ;

read
    : READ ID
    ;

assignment
    : ID '=' immediateValue
    ;

declaration
    : defaultDeclaration
    | initializingDeclaration
    ;

defaultDeclaration
    : type ID
    ;

initializingDeclaration
    : type ID '=' immediateValue
    ;

immediateValue
    : INT_LITERAL
    | LONG_LITERAL
    | FLOAT_LITERAL
    | DOUBLE_LITERAL
    ;

type
    : INT
    | LONG
    | FLOAT
    | DOUBLE
    ;

COMMENT
    : '//' ~[\r\n]* -> skip
    ;

MULTILINE_COMMENT
    : '/*' .*? '*/' -> skip
    ;

LONG
    : INT_SYMBOL INT_SYMBOL
    ;

INT
    : INT_SYMBOL
    ;

DOUBLE
    : FLOAT_SYMBOL FLOAT_SYMBOL
    ;

FLOAT
    : FLOAT_SYMBOL
    ;

WRITE
    : LOUDSPEAKER_EMOJI
    ;

READ
    : KEYBOARD_EMOJI
    ;

STRING
    : '"' STRING_CHAR* '"'
    ;

ID
    : [a-zA-Z_] [a-zA-Z_0-9]*
    ;

LONG_LITERAL
    : [0-9]+ [lL]
    ;

INT_LITERAL
    : [0-9]+
    ;

DOUBLE_LITERAL
    : [0-9]+ ('.' [0-9]+)? [dD]
    ;

FLOAT_LITERAL
    : [0-9]+ ('.' [0-9]+)? [fF]?
    ;

ADDITION_SYMBOL
    : '+'
    | PLUS_EMOJI
    ;

SUBTRACTION_SYMBOL
    : '-'
    | MINUS_EMOJI
    ;

MULTIPLICATION_SYMBOL
    : '*'
    | ASTERISK_EMOJI
    | MULTIPLICATION_EMOJI
    ;

DIVISION_SYMBOL
    : '/'
    | DIVISION_EMOJI
    ;

NEWLINE
    : '\r'? '\n'
    ;

WHITE_SPACE
    : (' ' | '\t')+ -> skip
    ;

fragment STRING_CHAR
    : ~[\\'\n\r\t$]
    ;

fragment LOUDSPEAKER_EMOJI
    : '📢'
    | ':loudspeaker:'
    ;

fragment KEYBOARD_EMOJI
    : '⌨️'
    | ':keyboard:'
    ;

fragment PLUS_EMOJI
    : '➕'
    | ':heavy_plus_sign:'
    ;

fragment MINUS_EMOJI
    : '➖'
    | ':heavy_minus_sign:'
    ;

fragment ASTERISK_EMOJI
    : '*️⃣'
    | ':asterisk:'
    ;

fragment MULTIPLICATION_EMOJI
    : '✖️'
    | ':heavy_multiplication_x:'
    ;

fragment DIVISION_EMOJI
    : '➗'
    | ':heavy_division_sign:'
    ;

fragment INT_SYMBOL
    : '🔢'
    | ':1234:'
    ;

fragment FLOAT_SYMBOL
    : '🔷'
    | '🔹'
    | ':large_blue_diamond:'
    | ':small_blue_diamond:'
    ;
