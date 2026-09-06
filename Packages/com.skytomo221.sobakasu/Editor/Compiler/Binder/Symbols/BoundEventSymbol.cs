using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundEventSymbol : Symbol
    {
        public override SymbolKind Kind => SymbolKind.Event;
        public string SourceName { get; }
        public string UdonName { get; }
        public TypeSymbol ReturnType { get; }
        public IReadOnlyList<ParameterSymbol> Parameters { get; }
        public EventCategory Category { get; }
        public string Requirement { get; }
        public EventSupportLevel SupportLevel { get; }
        public TextSpan SourceSpan { get; }
        public string ReturnValueStorageName { get; }

        public BoundEventSymbol(
            string sourceName,
            string udonName,
            TypeSymbol returnType,
            IReadOnlyList<ParameterSymbol> parameters,
            EventCategory category,
            string requirement,
            EventSupportLevel supportLevel,
            TextSpan sourceSpan,
            string returnValueStorageName)
            : base(sourceName)
        {
            SourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
            UdonName = udonName ?? throw new ArgumentNullException(nameof(udonName));
            ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Category = category;
            Requirement = requirement;
            SupportLevel = supportLevel;
            SourceSpan = sourceSpan;
            ReturnValueStorageName = returnValueStorageName;
        }
    }
}
