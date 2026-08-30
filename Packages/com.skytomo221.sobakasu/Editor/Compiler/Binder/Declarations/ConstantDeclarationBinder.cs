using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class ConstantDeclarationBinder : BinderComponent
  {
    internal ConstantDeclarationBinder(BindingSession session) : base(session)
    {
    }
  
    internal void CollectConstantDeclarations(IReadOnlyList<MemberSyntax> members)
    {
      if (Session.Modules.CurrentModule == null)
        return;
      var moduleConstants = Session.Constants.ModuleConstants[Session.Modules.CurrentModule];
      foreach (var member in members)
      {
        if (member is not ConstDeclarationSyntax syntax)
          continue;
        var name = syntax.Identifier.Text ?? string.Empty;
        if (moduleConstants.ContainsKey(name))
        {
          Session.Diagnostics.ReportDuplicateConstant(syntax.Identifier.Span, name);
          continue;
        }
  
        if (Session.Modules.Symbols[Session.Modules.CurrentModule].LookupDeclared(name) != null)
        {
          Session.Diagnostics.ReportTopLevelDeclarationNameConflict(syntax.Identifier.Span, name, "declaration");
          continue;
        }
  
        var symbol = new ConstantSymbol(name, syntax.PubKeyword != null, Session.Modules.CurrentModule.LogicalName, syntax.Identifier.Span);
        moduleConstants.Add(name, symbol);
        Session.Modules.VisibleConstants[name] = symbol;
        Session.Constants.SyntaxBySymbol.Add(symbol, syntax);
        Session.Constants.ModulesBySymbol.Add(symbol, Session.Modules.CurrentModule);
        Session.Constants.BindingStates.Add(symbol, ConstantBindingState.Unbound);
        Session.Constants.DeclarationOrder.Add(symbol);
        Session.CallableDeclarationBinder.RegisterModuleDeclaration(name, symbol, symbol.IsPublic);
      }
    }
  
    internal bool IsSupportedConstantType(TypeSymbol type)
    {
      if (type == null || type == TypeSymbol.Error || type.IsAggregate || type.TypeKind == TypeKind.Array)
      {
        return false;
      }
  
      return type.TypeKind is TypeKind.I8 or TypeKind.U8 or TypeKind.I16 or TypeKind.U16 or TypeKind.I32 or TypeKind.U32 or TypeKind.I64 or TypeKind.U64 or TypeKind.F32 or TypeKind.F64 or TypeKind.Char or TypeKind.String or TypeKind.Bool or TypeKind.Named;
    }
  }
}
