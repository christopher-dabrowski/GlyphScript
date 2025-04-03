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
    : type ID '=' immediateValue
    ;

immediateValue
    : INT
    | LONG
    | FLOAT
    | DOUBLE
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
    : '📢'
    | ':loudspeaker:'
    ;

READ
    : '⌨️'
    | ':keyboard:'
    ;

STRING
    : '"' STRING_CHAR* '"'
    ;

ID
    : [a-zA-Z_] [a-zA-Z_0-9]*
    ;

DECIMAL_ATOM
    : [0-9]+ ('.' [0-9]+)
    ;

INT_ATOM
    : [0-9]+
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
