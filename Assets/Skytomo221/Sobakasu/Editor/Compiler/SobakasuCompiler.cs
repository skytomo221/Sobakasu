namespace Skytomo221.Sobakasu.Compiler
{
  public static class SobakasuCompiler
  {
    public readonly struct CompileResult
    {
      public readonly bool Success;
      public readonly string Uasm;
      public readonly string ErrorText;

      public CompileResult(bool success, string uasm, string errorText)
      {
        Success = success;
        Uasm = uasm;
        ErrorText = errorText;
      }

      public static CompileResult Ok(string uasm) => new(true, uasm, "");
      public static CompileResult Fail(string errorText) => new(false, "", errorText);
    }

    public static CompileResult CompileToUasm(string sourceText)
    {
      var parser = new SobakasuParser(sourceText);
      var syntax = parser.ParseCompilationUnit();
      var binder = new Binder();
      var bound = binder.Bind(syntax);
      var lowerer = new Lowerer();
      var ir = lowerer.Lower(bound);
      var gen = new UdonAssemblyCodeGen();
      var uasm = gen.Generate(ir);

      return CompileResult.Ok(uasm);
    }
  }
}
