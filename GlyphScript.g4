// $antlr-format alignTrailingComments true, columnLimit 150, minEmptyLines 1, maxEmptyLinesToKeep 1, reflowComments false, useTab false
// $antlr-format allowShortRulesOnASingleLine false, allowShortBlocksOnASingleLine true, alignSemicolons hanging, alignColons hanging

grammar GlyphScript;

program
    : (statement? NEWLINE)*
    ;

statement
    : WRITE IDENTIFIER   # write
    | IDENTIFIER '=' INT # assign
    | READ IDENTIFIER    # read
    ;

WRITE
    : 'write'
    ;

READ
    : 'read'
    ;

STRING
    : '"' STRING_CHAR* '"'
    ;

IDENTIFIER
    : [a-zA-Z_] [a-zA-Z_0-9]*
    ;

DECIMAL
    : [0-9]+ ('.' [0-9]+)
    ;

INT
    : [0-9]+
    ;

NEWLINE
    : '\r'? '\n'
    ;

WHITE_SPACE
    : (' ' | '\t')+ -> skip
    ;

COMMENT
    : '//' ~[\r\n]* -> skip
    ;

fragment STRING_CHAR
    : ~[\\'\n\r\t$]
    ;
