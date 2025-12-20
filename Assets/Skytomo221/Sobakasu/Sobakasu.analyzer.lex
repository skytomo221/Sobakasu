// ---------------------------
// 宣言部
// ---------------------------

// 出力するクラスのnamespace
%namespace GPPGCalculator2

// 生成するクラスがpartialクラスになる
// そうすることで、実装部分を*.yではなく、*.csに書けるようになるので便利
%partial

// 出力するクラス名
%parsertype CalculatorParser

// 出力するクラスのアクセシビリティ
%visibility internal

// トークンに使用するenumの型名
%tokentype Token

%union { 
    public double real;
    public string text;
}

%token <text> VARIABLE
%token <real> CONSTANT

%start main


%%
// ---------------------------
// ルール部
// ---------------------------

main            : assignment                    { m_result = $1.real; }
                ;

assignment      : additive                      { $$.real = $1.real; }
                | VARIABLE '=' assignment       {
                                                    m_variables[$1] = $3.real;
                                                    $$.real = $3.real;
                                                }
                ;

additive        : multiplicative                { $$.real = $1.real;           }
                | additive '+' multiplicative   { $$.real = $1.real + $3.real; }
                | additive '-' multiplicative   { $$.real = $1.real - $3.real; }
                ;

multiplicative  : primary                       { $$.real = $1.real;            }
                | multiplicative '*' primary    { $$.real = $1.real * $3.real;  }
                | multiplicative '/' primary    { $$.real = $1.real / $3.real;  }
                ; 

primary         : VARIABLE                      {
                                                    double v;
                                                    if (m_variables.TryGetValue($1, out v))
                                                    {
                                                        $$.real = v;
                                                    }
                                                    else
                                                    {
                                                        $$.real = 0;
                                                    }
                                                }
                | CONSTANT                      { $$.real = $1; }
                | '(' additive ')'              { $$.real = $2.real; }
                ;


%%
// ---------------------------
// コード部
// ---------------------------

