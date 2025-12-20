%{
using System;
%}

%%

[0-9]+      { return (int)Tokens.NUMBER; }
"+"         { return (int)Tokens.PLUS; }
"-"         { return (int)Tokens.MINUS; }
"*"         { return (int)Tokens.TIMES; }
"/"         { return (int)Tokens.DIVIDE; }
[ \t\r\n]+  { /* skip whitespace */ }
.           { throw new Exception("Unexpected character: " + yytext); }
