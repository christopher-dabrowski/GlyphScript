// $antlr-format alignTrailingComments true, columnLimit 150, minEmptyLines 1, maxEmptyLinesToKeep 1, reflowComments false, useTab false
// $antlr-format allowShortRulesOnASingleLine false, allowShortBlocksOnASingleLine true, alignSemicolons hanging, alignColons hanging

grammar GlyphScript;

program
    : (statement? NEWLINE)* EOF
    ;

statement
    : WRITE ID   # write
    | ID '=' INT # assign
    | READ ID    # read
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
