using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BindingSession
  {
    internal BindingSession(
        SobakasuCompilationEnvironment environment,
        DiagnosticBag diagnostics)
    {
      Environment = environment ?? throw new ArgumentNullException(nameof(environment));
      Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

      Pipeline = new BindingPipeline(this);
      ModuleBindingPhase = new ModuleBindingPhase(this);
      TypeDeclarationBindingPhase = new TypeDeclarationBindingPhase(this);
      LanguageItemBindingPhase = new LanguageItemBindingPhase(this);
      CallableDeclarationBindingPhase = new CallableDeclarationBindingPhase(this);
      ConstantBindingPhase = new ConstantBindingPhase(this);
      StateBindingPhase = new StateBindingPhase(this);
      BodyBindingPhase = new BodyBindingPhase(this);
      ValidationPhase = new ValidationPhase(this);
      ModuleResolver = new ModuleResolver(this);
      ImportResolver = new ImportResolver(this);
      PreludeResolver = new PreludeResolver(this);
      VisibilityResolver = new VisibilityResolver(this);
      AggregateDeclarationBinder = new AggregateDeclarationBinder(this);
      CallableDeclarationBinder = new CallableDeclarationBinder(this);
      ExternDeclarationBinder = new ExternDeclarationBinder(this);
      ConstantDeclarationBinder = new ConstantDeclarationBinder(this);
      StateDeclarationBinder = new StateDeclarationBinder(this);
      BodyBinder = new BodyBinder(this);
      EventDeclarationBinder = new EventDeclarationBinder(this);
      ReceiveDeclarationBinder = new ReceiveDeclarationBinder(this);
      BlockBinder = new BlockBinder(this);
      StatementBinder = new StatementBinder(this);
      NetworkSendBinder = new NetworkSendBinder(this);
      LoopBinder = new LoopBinder(this);
      ReturnBinder = new ReturnBinder(this);
      LocalDeclarationBinder = new LocalDeclarationBinder(this);
      ExpressionBinder = new ExpressionBinder(this);
      AggregateExpressionBinder = new AggregateExpressionBinder(this);
      ConditionalBinder = new ConditionalBinder(this);
      AssignmentExpressionBinder = new AssignmentExpressionBinder(this);
      OperatorExpressionBinder = new OperatorExpressionBinder(this);
      NameExpressionBinder = new NameExpressionBinder(this);
      MemberAccessBinder = new MemberAccessBinder(this);
      CallExpressionBinder = new CallExpressionBinder(this);
      TypeResolver = new TypeResolver(this);
      NameResolver = new NameResolver(this);
      MemberResolver = new MemberResolver(this);
      OverloadResolver = new OverloadResolver(this);
      OperatorResolver = new OperatorResolver(this);
      ConversionClassifier = new ConversionClassifier(this);
      GenericInference = new GenericInference(this);
      GenericSubstitution = new GenericSubstitution();
      GenericInstantiation = new GenericInstantiation(this);
      ExternResolver = new ExternResolver(this);
      ConstantEvaluator = new ConstantEvaluator(this);
      ConstantDependencyAnalyzer = new ConstantDependencyAnalyzer(this);
      AggregateDependencyValidator = new AggregateDependencyValidator(this);
      ConstructedTypeValidator = new ConstructedTypeValidator(this);
      RecursiveFunctionValidator = new RecursiveFunctionValidator(this);
      BinderSyntaxFacts = new BinderSyntaxFacts(this);
    }

    internal SobakasuCompilationEnvironment Environment { get; }
    internal DiagnosticBag Diagnostics { get; }
    internal ModuleBindingState Modules { get; } = new();
    internal DeclarationBindingState Declarations { get; } = new();
    internal CallableBindingState Callables { get; } = new();
    internal ConstantBindingStateStore Constants { get; } = new();
    internal GenericBindingState Generics { get; } = new();
    internal LanguageItemBindingState LanguageItems { get; } = new();
    internal BodyBindingContext Body { get; set; } = new();

    internal BindingPipeline Pipeline { get; }
    internal ModuleBindingPhase ModuleBindingPhase { get; }
    internal TypeDeclarationBindingPhase TypeDeclarationBindingPhase { get; }
    internal LanguageItemBindingPhase LanguageItemBindingPhase { get; }
    internal CallableDeclarationBindingPhase CallableDeclarationBindingPhase { get; }
    internal ConstantBindingPhase ConstantBindingPhase { get; }
    internal StateBindingPhase StateBindingPhase { get; }
    internal BodyBindingPhase BodyBindingPhase { get; }
    internal ValidationPhase ValidationPhase { get; }
    internal ModuleResolver ModuleResolver { get; }
    internal ImportResolver ImportResolver { get; }
    internal PreludeResolver PreludeResolver { get; }
    internal VisibilityResolver VisibilityResolver { get; }
    internal AggregateDeclarationBinder AggregateDeclarationBinder { get; }
    internal CallableDeclarationBinder CallableDeclarationBinder { get; }
    internal ExternDeclarationBinder ExternDeclarationBinder { get; }
    internal ConstantDeclarationBinder ConstantDeclarationBinder { get; }
    internal StateDeclarationBinder StateDeclarationBinder { get; }
    internal BodyBinder BodyBinder { get; }
    internal EventDeclarationBinder EventDeclarationBinder { get; }
    internal ReceiveDeclarationBinder ReceiveDeclarationBinder { get; }
    internal BlockBinder BlockBinder { get; }
    internal StatementBinder StatementBinder { get; }
    internal NetworkSendBinder NetworkSendBinder { get; }
    internal LoopBinder LoopBinder { get; }
    internal ReturnBinder ReturnBinder { get; }
    internal LocalDeclarationBinder LocalDeclarationBinder { get; }
    internal ExpressionBinder ExpressionBinder { get; }
    internal AggregateExpressionBinder AggregateExpressionBinder { get; }
    internal ConditionalBinder ConditionalBinder { get; }
    internal AssignmentExpressionBinder AssignmentExpressionBinder { get; }
    internal OperatorExpressionBinder OperatorExpressionBinder { get; }
    internal NameExpressionBinder NameExpressionBinder { get; }
    internal MemberAccessBinder MemberAccessBinder { get; }
    internal CallExpressionBinder CallExpressionBinder { get; }
    internal TypeResolver TypeResolver { get; }
    internal NameResolver NameResolver { get; }
    internal MemberResolver MemberResolver { get; }
    internal OverloadResolver OverloadResolver { get; }
    internal OperatorResolver OperatorResolver { get; }
    internal ConversionClassifier ConversionClassifier { get; }
    internal GenericInference GenericInference { get; }
    internal GenericSubstitution GenericSubstitution { get; }
    internal GenericInstantiation GenericInstantiation { get; }
    internal ExternResolver ExternResolver { get; }
    internal ConstantEvaluator ConstantEvaluator { get; }
    internal ConstantDependencyAnalyzer ConstantDependencyAnalyzer { get; }
    internal AggregateDependencyValidator AggregateDependencyValidator { get; }
    internal ConstructedTypeValidator ConstructedTypeValidator { get; }
    internal RecursiveFunctionValidator RecursiveFunctionValidator { get; }
    internal BinderSyntaxFacts BinderSyntaxFacts { get; }
  }
}
