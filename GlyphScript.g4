// $antlr-format alignTrailingComments true, columnLimit 300, minEmptyLines 1, maxEmptyLinesToKeep 1, reflowComments false, useTab false
// $antlr-format allowShortRulesOnASingleLine false, allowShortBlocksOnASingleLine true, alignSemicolons hanging, alignColons hanging

grammar GlyphScript;

program
    : (statement? NEWLINE)*? statement? EOF
    ;

statement
    : declaration
    | print
    | assignment
    | read
    | ifStatement
    | whileStatement
    | block
    ;

expression
    : '(' expression ')'                                              # parenthesisExp
    | NOT_SYMBOL expression                                           # notExpr
    | <assoc = right> expression POWER_SYMBOL expression              # powerExp
    | expression (MULTIPLICATION_SYMBOL | DIVISION_SYMBOL) expression # mulDivExp
    | expression (ADDITION_SYMBOL | SUBTRACTION_SYMBOL) expression    # addSubExp
    | expression XOR_SYMBOL expression                                # xorExp
    | expression '[' expression ']'                                   # arrayAccessExp
    | immediateValue                                                  # valueExp
    | ID                                                              # idAtomExp
    | expression EQUALITY_SYMBOL expression                           # comparisonExpr
    | expression LESS_THAN_SYMBOL expression                          # lessThanExpr
    | expression GREATER_THAN_SYMBOL expression                       # greaterThanExpr
    ;

ifStatement
    : IF expression block NEWLINE? (ELSE block)?
    ;

whileStatement
    : WHILE expression block
    ;

block
    : BEGIN NEWLINE (statement NEWLINE)* END
    ;

print
    : WRITE expression
    ;

read
    : READ ID
    ;

assignment
    : ID '=' expression
    | ID '[' expression ']' '=' expression
    ;

declaration
    : defaultDeclaration
    | initializingDeclaration
    ;

defaultDeclaration
    : type ID
    ;

initializingDeclaration
    : type ID '=' expression
    ;

immediateValue
    : INT_LITERAL
    | LONG_LITERAL
    | FLOAT_LITERAL
    | DOUBLE_LITERAL
    | STRING_LITERAL
    | TRUE_LITERAL
    | FALSE_LITERAL
    | arrayLiteral
    ;

type
    : INT
    | LONG
    | FLOAT
    | DOUBLE
    | STRING_TYPE
    | BOOLEAN_TYPE
    | arrayOfType
    ;

arrayOfType
    : ARRAY_TYPE type
    ;

arrayLiteral
    : '[' expressionList? ']'
    ;

expressionList
    : expression (',' expression)*
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

STRING_TYPE
    : LETTERS_SYMBOL
    ;

ARRAY_TYPE
    : PACKAGE_EMOJI
    ;

FLOAT
    : FLOAT_SYMBOL
    ;

BOOLEAN_TYPE
    : OK_EMOJI
    ;

WRITE
    : LOUDSPEAKER_EMOJI
    ;

READ
    : KEYBOARD_EMOJI
    ;

STRING_LITERAL
    : '"' STRING_CHAR* '"'
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

TRUE_LITERAL
    : CHECK_MARK_EMOJI
    | 'true'
    ;

FALSE_LITERAL
    : X_EMOJI
    | 'false'
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

POWER_SYMBOL
    : '^'
    | RED_TRIANGLE_EMOJI
    ;

DIVISION_SYMBOL
    : '/'
    | DIVISION_EMOJI
    ;

NOT_SYMBOL
    : NO_ENTRY_SIGN_EMOJI
    ;

XOR_SYMBOL
    : CROSSED_SWORDS_EMOJI
    ;

EQUALITY_SYMBOL
    : BALANCE_SCALE_EMOJI
    ;

LESS_THAN_SYMBOL
    : ARROW_DOWN_EMOJI
    ;

GREATER_THAN_SYMBOL
    : ARROW_UP_EMOJI
    ;

BEGIN
    : OPEN_BOOK_EMOJI
    ;

END
    : CLOSED_BOOK_EMOJI
    ;

IF
    : THINKING_EMOJI
    ;

ELSE
    : UPSIDE_DOWN_FACE_EMOJI
    ;

WHILE
    : ARROWS_COUNTERCLOCKWISE
    ;

ID
    : [a-zA-Z_] [a-zA-Z_0-9]*
    ;

NEWLINE
    : '\r'? '\n'
    ;

WHITE_SPACE
    : (' ' | '\t')+ -> skip
    ;

fragment STRING_CHAR
    : ~[\\"\n\r\t$]
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

fragment RED_TRIANGLE_EMOJI
    : '🔺'
    | ':small_red_triangle:'
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

fragment LETTERS_SYMBOL
    : '🔤'
    | ':abc:'
    | '🔡'
    | ':abcd:'
    | '🔠'
    | ':capital_abcd:'
    ;

fragment OK_EMOJI
    : '🆗'
    | ':ok:'
    ;

fragment CHECK_MARK_EMOJI
    : '✅'
    | ':white_check_mark:'
    | '✔️'
    | ':heavy_check_mark:'
    ;

fragment X_EMOJI
    : '❌'
    | ':x:'
    ;

fragment CROSSED_SWORDS_EMOJI
    : '⚔️'
    | ':crossed_swords:'
    ;

fragment NO_ENTRY_SIGN_EMOJI
    : '🚫'
    | ':no_entry_sign:'
    ;

fragment PACKAGE_EMOJI
    : '📦'
    | ':package:'
    ;

fragment BALANCE_SCALE_EMOJI
    : '⚖️'
    | ':balance_scale:'
    ;

fragment ARROW_DOWN_EMOJI
    : '⬇️'
    | ':arrow_down:'
    ;

fragment ARROW_UP_EMOJI
    : '⬆️'
    | ':arrow_up:'
    ;

fragment OPEN_BOOK_EMOJI
    : '📖'
    | ':open_book:'
    ;

fragment CLOSED_BOOK_EMOJI
    : '📕'
    | ':closed_book:'
    ;

fragment THINKING_EMOJI
    : '🤔'
    | ':thinking:'
    ;

fragment UPSIDE_DOWN_FACE_EMOJI
    : '🙃'
    | ':upside_down_face:'
    ;

fragment ARROWS_COUNTERCLOCKWISE
    : '🔄'
    | ':arrows_counterclockwise:'
    ;
