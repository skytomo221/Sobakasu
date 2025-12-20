using System;
using System.Collections.Generic;
using System.Text;

namespace Sobakasu.SobakasuCompiler
{
    internal partial class SobakasuCompilerScanner
    {

        void GetNumber()
        {
            yylval.s = yytext;
            yylval.n = int.Parse(yytext);
        }

		public override void yyerror(string format, params object[] args)
		{
			base.yyerror(format, args);
			Console.WriteLine(format, args);
			Console.WriteLine();
		}
    }
}
