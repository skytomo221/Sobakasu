using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
    internal enum UdonApiGeneratedPlacement
    {
        Impl,
        Type,
        Struct,
        Enum,
        TopLevel
    }

    internal enum UdonApiGeneratedProjection
    {
        Raw,
        Maybe
    }

    internal sealed class UdonApiGeneratedMemberModel
    {
        private readonly Dictionary<int, UdonApiGeneratedProjection> _outProjections = new();

        public UdonBindingSourceMember Physical { get; }
        public string FunctionName { get; set; }
        public UdonApiGeneratedProjection ReturnProjection { get; set; }
        public string SkipReason { get; set; }
        public bool IsExplicitlyExcluded { get; set; }
        public bool HasDeclarationCollision { get; set; }
        public bool IsMutableReceiver { get; set; }
        public bool RequiresExplicitAbiSignature { get; set; }
        public string GeneratedHostTypeName { get; set; }
        public bool IsGenerated => string.IsNullOrEmpty(SkipReason);

        public UdonApiGeneratedMemberModel(UdonBindingSourceMember physical)
        {
            Physical = physical ?? throw new ArgumentNullException(nameof(physical));
            SkipReason = physical.SkipReason;
        }

        public UdonApiGeneratedProjection GetOutProjection(int parameterIndex)
        {
            return _outProjections.TryGetValue(parameterIndex, out var projection)
                ? projection
                : UdonApiGeneratedProjection.Raw;
        }

        public void SetOutProjection(
            int parameterIndex,
            UdonApiGeneratedProjection projection)
        {
            _outProjections[parameterIndex] = projection;
        }
    }

    internal sealed class UdonApiGeneratedTypeModel
    {
        private readonly List<UdonApiGeneratedMemberModel> _members = new();

        public UdonBindingSourceType Physical { get; }
        public string GeneratedNamespace { get; set; }
        public UdonApiGeneratedPlacement Placement { get; set; }
        public string WrapperName { get; set; }
        public string ModuleName { get; set; }
        public string LanguageItem { get; set; }
        public bool ShouldReExport { get; set; } = true;
        public string RelativePath { get; set; }
        public string SkipReason { get; set; }
        public bool IsExplicitlyExcluded { get; set; }
        public IReadOnlyList<UdonApiGeneratedMemberModel> Members => _members;
        public bool IsGenerated => string.IsNullOrEmpty(SkipReason);

        public UdonApiGeneratedTypeModel(UdonBindingSourceType physical)
        {
            Physical = physical ?? throw new ArgumentNullException(nameof(physical));
            SkipReason = physical.SkipReason;
        }

        public void AddMember(UdonApiGeneratedMemberModel member)
        {
            _members.Add(member ?? throw new ArgumentNullException(nameof(member)));
        }

        public void SkipGeneratedMembers(string reason)
        {
            foreach (var member in _members)
            {
                if (member.IsGenerated)
                    member.SkipReason = reason;
            }
        }
    }

    internal sealed class UdonApiGeneratedModel
    {
        public UdonBindingSourceModel Source { get; }
        public IReadOnlyList<UdonApiGeneratedTypeModel> Types { get; }
        public IReadOnlyCollection<string> UdonExposedSignatures { get; }
        public UdonBindingGenerationConfig Configuration { get; }
        public string ConfigurationPath { get; }

        public UdonApiGeneratedModel(
            IReadOnlyList<UdonApiGeneratedTypeModel> types,
            IReadOnlyCollection<string> udonExposedSignatures,
            UdonBindingGenerationConfig configuration,
            string configurationPath,
            UdonBindingSourceModel source = null)
        {
            Source = source;
            Types = types ?? throw new ArgumentNullException(nameof(types));
            UdonExposedSignatures = udonExposedSignatures ??
                throw new ArgumentNullException(nameof(udonExposedSignatures));
            Configuration = configuration ??
                throw new ArgumentNullException(nameof(configuration));
            ConfigurationPath = configurationPath ?? string.Empty;
        }
    }

    internal sealed class UdonBindingGenerationPolicy
    {
        private const string DefaultNamespace = "external";

        public UdonApiGeneratedModel Apply(
            UdonBindingSourceModel physicalModel,
            UdonBindingGenerationConfig configuration,
            string configurationPath)
        {
            if (physicalModel == null)
                throw new ArgumentNullException(nameof(physicalModel));
            configuration ??= UdonBindingGenerationConfig.CreateDefault();
            configuration.Normalize();
            configuration.ResetRuleMatches();
            configuration.receivers.ResetMatches();

            var errors = new SortedSet<string>(StringComparer.Ordinal);
            ValidateConfiguration(configuration, errors);
            var generatedTypes = new List<UdonApiGeneratedTypeModel>();
            var generatedTypesByRuntimeName = new Dictionary<string, UdonApiGeneratedTypeModel>(StringComparer.Ordinal);
            foreach (var physicalType in physicalModel.Types)
            {
                var generatedType = CreateGeneratedType(
                    physicalType,
                    configuration,
                    errors);
                generatedTypes.Add(generatedType);
                generatedTypesByRuntimeName.Add(physicalType.RuntimeName, generatedType);
            }

            var projectedOperatorSurfaces = new HashSet<string>(StringComparer.Ordinal);
            foreach (var physicalType in physicalModel.Types)
            {
                var physicalOwner = generatedTypesByRuntimeName[physicalType.RuntimeName];
                foreach (var physicalMember in physicalType.Members)
                {
                    var hostType = GetOperatorHostType(physicalMember) ?? physicalType.RuntimeName;
                    if (!generatedTypesByRuntimeName.TryGetValue(hostType, out var generatedHost))
                    {
                        if (!physicalModel.TypesByRuntimeName.TryGetValue(hostType, out var hostRecord))
                            continue;
                        var hostPhysical = new UdonBindingSourceType(hostRecord, Array.Empty<UdonBindingSourceMember>());
                        generatedHost = CreateGeneratedType(hostPhysical, configuration, errors);
                        generatedTypes.Add(generatedHost);
                        generatedTypesByRuntimeName.Add(hostType, generatedHost);
                    }

                    var member = ApplyMemberPolicy(
                        physicalModel,
                        configuration,
                        physicalMember,
                        physicalType,
                        !physicalOwner.IsGenerated,
                        physicalOwner.SkipReason,
                        errors);
                    member.GeneratedHostTypeName =
                        generatedHost.Physical.RuntimeName;
                    if (member.IsGenerated && !generatedHost.IsGenerated)
                    {
                        member.SkipReason =
                            $"Operator host type was skipped: {generatedHost.SkipReason}";
                    }
                    if (member.IsGenerated &&
                        SobakasuOperatorMapping.TryGet(physicalMember, out _, out _) &&
                        !projectedOperatorSurfaces.Add(
                            $"{generatedHost.Physical.QualifiedName}|{physicalMember.ExternSignature}"))
                    {
                        member.SkipReason =
                            "Duplicate CLR operator surface is already generated on the first-operand host.";
                    }
                    if (generatedHost.Placement == UdonApiGeneratedPlacement.TopLevel &&
                        member.IsGenerated &&
                        !SobakasuOperatorMapping.IsOperator(physicalMember) &&
                        !IsStaticMember(physicalMember))
                    {
                        member.SkipReason =
                            "Instance members cannot be published by a static-class module.";
                    }
                    generatedHost.AddMember(member);
                }
            }

            AddStaleSourceRuleErrors(configuration, errors);
            ThrowIfErrors(errors);
            return new UdonApiGeneratedModel(
                generatedTypes,
                physicalModel.UdonExposedSignatures,
                configuration,
                configurationPath,
                physicalModel);
        }

        private static UdonApiGeneratedTypeModel CreateGeneratedType(
            UdonBindingSourceType physicalType,
            UdonBindingGenerationConfig configuration,
            ISet<string> errors)
        {
            var typeExclusion = MatchTypeExclusion(configuration, physicalType);
            var namespaceExclusion = MatchNamespaceExclusion(
                configuration,
                physicalType.ClrNamespace ?? string.Empty);
            var isTypeExcluded = typeExclusion != null || namespaceExclusion != null;
            var typeRename = isTypeExcluded
                ? null
                : MatchTypeRename(configuration, physicalType);
            var namespaceRename = isTypeExcluded
                ? null
                : MatchNamespaceRename(configuration, physicalType);
            var languageItem = MatchLanguageItem(configuration, physicalType);
            var isCanonicalPrimitive = UdonBindingTypeFormatter.IsCanonicalPrimitive(physicalType.RuntimeName, out var builtinName);
            var generatedType = new UdonApiGeneratedTypeModel(physicalType)
            {
                GeneratedNamespace = ResolveNamespace(physicalType, namespaceRename),
                Placement = isCanonicalPrimitive
                  ? UdonApiGeneratedPlacement.Impl
                    : physicalType.IsStaticApiContainer
                      ? UdonApiGeneratedPlacement.TopLevel
                      : physicalType.IsEnum
                          ? UdonApiGeneratedPlacement.Enum
                          : physicalType.IsValueType
                              ? UdonApiGeneratedPlacement.Struct
                              : UdonApiGeneratedPlacement.Type,
                WrapperName = isCanonicalPrimitive
                  ? builtinName
                  : string.IsNullOrWhiteSpace(typeRename?.to)
                      ? physicalType.WrapperName
                      : typeRename.to,
                LanguageItem = languageItem?.item,
                ShouldReExport = !isCanonicalPrimitive
            };

            if (languageItem != null &&
                (generatedType.Placement == UdonApiGeneratedPlacement.TopLevel ||
                 isTypeExcluded))
            {
                errors.Add(
                    $"Language item target '{languageItem.from}' does not generate a type declaration.");
            }

            if (isTypeExcluded)
            {
                var identity = typeExclusion != null
                    ? TypeExcludeIdentity(typeExclusion)
                    : NamespaceExcludeIdentity(namespaceExclusion);
                generatedType.SkipReason = $"Explicitly excluded by '{identity}'.";
                generatedType.IsExplicitlyExcluded = true;
            }

            return generatedType;
        }

        private static string GetOperatorHostType(UdonBindingSourceMember member)
        {
            if (!SobakasuOperatorMapping.TryGet(member, out _, out var isUnary))
            {
                return null;
            }

            var parameters = member.Parameters;
            var expectedArity = isUnary ? 1 : 2;
            if (parameters.Count != expectedArity)
                return null;
            return GetRuntimeName(parameters[0].type);
        }

        internal static ExternTypeRef GetNormalReturnType(UdonBindingSourceMember member)
        {
            if (member.IsOperator)
                return member.ReturnType;
            return member.ReturnType;
        }

        private static string GetRuntimeName(ExternTypeRef type) => type?.kind == "Named" ? type.runtimeName : null;
        private static bool IsVoid(ExternTypeRef type) => type?.kind == "Named" && type.runtimeName == "System.Void";
        private static UdonBindingSourceTypeRecord GetTypeRecord(ExternTypeRef type, UdonBindingSourceModel model)
        {
            var name = type?.kind == "Named" ? type.runtimeName :
                type?.kind == "ConstructedGeneric" ? type.definition?.runtimeName : null;
            return name != null && model.TypesByRuntimeName.TryGetValue(name, out var record) ? record : null;
        }

        private static bool IsReferenceLike(ExternTypeRef type, UdonBindingSourceModel model) =>
            type?.kind == "Array" || (!IsVoid(type) && GetTypeRecord(type, model)?.shape == "Reference");

        private static UdonApiGeneratedProjection GetDefaultProjection(
            ExternTypeRef type, UdonBindingSourceModel model, UdonBindingGenerationConfig configuration)
        {
            if (!IsReferenceLike(type, model))
                return UdonApiGeneratedProjection.Raw;
            var record = GetTypeRecord(type, model);
            if (record != null && Contains(configuration.maybe.nonMaybeTypes, record.runtimeName))
            {
                configuration.MarkRuleMatched(NonMaybeTypeIdentity(record.runtimeName));
                return UdonApiGeneratedProjection.Raw;
            }
            return UdonApiGeneratedProjection.Maybe;
        }

        internal static IReadOnlyList<string> GetConfiguredRuleIdentities(
            UdonBindingGenerationConfig configuration)
        {
            var identities = new List<string>();
            foreach (var rule in configuration.renames.namespaces)
                if (rule != null) identities.Add(NamespaceRenameIdentity(rule));
            foreach (var rule in configuration.renames.types)
                if (rule != null) identities.Add(TypeRenameIdentity(rule));
            foreach (var rule in configuration.renames.members)
                if (rule != null) identities.Add(MemberRenameIdentity(rule));
            foreach (var rule in configuration.lang)
                if (rule != null) identities.Add(LanguageItemIdentity(rule));
            foreach (var path in configuration.prelude.namespaces)
                identities.Add(PreludeNamespaceIdentity(path));
            foreach (var path in configuration.prelude.types)
                identities.Add(PreludeTypeIdentity(path));
            foreach (var path in configuration.prelude.members)
                identities.Add(PreludeMemberIdentity(path));
            foreach (var type in configuration.maybe.nonMaybeTypes)
                identities.Add(NonMaybeTypeIdentity(type));
            foreach (var member in configuration.receivers.Selectors)
                identities.Add(ReceiverReadonlyIdentity(member));
            foreach (var member in configuration.maybe.nonMaybeReturns)
                identities.Add(NonMaybeReturnIdentity(member));
            foreach (var rule in configuration.maybe.nonMaybeOuts)
                if (rule != null) identities.Add(NonMaybeOutIdentity(rule.member));
            foreach (var value in configuration.excludes.namespaces)
                identities.Add(NamespaceExcludeIdentity(value));
            foreach (var value in configuration.excludes.types)
                identities.Add(TypeExcludeIdentity(value));
            foreach (var value in configuration.excludes.members)
                identities.Add(MemberExcludeIdentity(value));
            return identities;
        }

        internal static string PreludeNamespaceIdentity(string path) =>
            $"prelude.namespace:{path}";
        internal static string PreludeTypeIdentity(string path) =>
            $"prelude.type:{path}";
        internal static string PreludeMemberIdentity(string path) =>
            $"prelude.member:{path}";

        private static UdonApiGeneratedMemberModel ApplyMemberPolicy(
            UdonBindingSourceModel physicalModel,
            UdonBindingGenerationConfig configuration,
            UdonBindingSourceMember physical,
            UdonBindingSourceType physicalOwner,
            bool isTypeExcluded,
            string typeSkipReason,
            ISet<string> errors)
        {
            var generated = new UdonApiGeneratedMemberModel(physical)
            {
                FunctionName = ResolveFunctionName(physical)
            };
            var memberId = physical.ClrSignature;
            var explicitlyExcluded = MatchMemberExclusion(configuration, memberId);
            if (isTypeExcluded || explicitlyExcluded)
            {
                generated.SkipReason = explicitlyExcluded
                    ? $"Explicitly excluded by '{MemberExcludeIdentity(memberId)}'."
                    : $"Declaring type was skipped: {typeSkipReason}";
                generated.IsExplicitlyExcluded = true;
                return generated;
            }

            var rename = MatchMemberRename(configuration, memberId);
            if (rename != null && !SobakasuOperatorMapping.IsOperator(physical))
                generated.FunctionName = rename.to;

            generated.IsMutableReceiver = !physical.IsStatic &&
                physical.Kind != UdonApiMemberKind.Constructor && physicalOwner.IsValueType;
            foreach (var selector in configuration.receivers.Selectors)
            {
                if (selector.Member != physical.HostRuntimeName + "." + physical.Name ||
                    selector.Kind != physical.Kind)
                    continue;
                // Static accessors share the same Catalog kind as instance accessors.
                if (physical.IsStatic)
                {
                    errors.Add($"Readonly receiver target '{selector}' must be an instance member, not static or constructor.");
                    continue;
                }
                if (!selector.Equals(UdonBindingMemberSelector.FromPhysical(physical)))
                    continue;
                configuration.receivers.MarkMatched(selector);
                configuration.MarkRuleMatched(ReceiverReadonlyIdentity(selector));
                if (physical.IsStatic || physical.Kind == UdonApiMemberKind.Constructor)
                    errors.Add($"Readonly receiver target '{memberId}' must be an instance member, not static or constructor.");
                generated.IsMutableReceiver = false;
            }

            var normalReturnType = GetNormalReturnType(physical);
            var isWriteSurface = physical.Kind == UdonApiMemberKind.PropertySetter ||
                physical.Kind == UdonApiMemberKind.FieldSetter;
            var referenceReturn = physical.Kind != UdonApiMemberKind.Constructor &&
                !isWriteSurface && IsReferenceLike(normalReturnType, physicalModel);
            generated.ReturnProjection = referenceReturn
                ? GetDefaultProjection(normalReturnType, physicalModel, configuration)
                : UdonApiGeneratedProjection.Raw;
            if (!isWriteSurface && Contains(configuration.maybe.nonMaybeReturns, memberId))
            {
                configuration.MarkRuleMatched(NonMaybeReturnIdentity(memberId));
                if (!referenceReturn)
                    errors.Add($"NonMaybe return target '{memberId}' returns a non-reference type or has no return value.");
                generated.ReturnProjection = UdonApiGeneratedProjection.Raw;
            }

            var parameters = physical.Parameters;
            if (generated.ReturnProjection == UdonApiGeneratedProjection.Maybe &&
                HasParameterOutputs(parameters))
            {
                errors.Add(
                    $"Maybe return target '{memberId}' also has ref/out outputs. " +
                    "The current compiler can only project the complete extern result.");
            }
            for (var index = 0; index < parameters.Count; index++)
                if (parameters[index].passingMode == "Out")
                    generated.SetOutProjection(index,
                        GetDefaultProjection(parameters[index].type, physicalModel, configuration));

            var nonMaybeOut = MatchNonMaybeOut(configuration, memberId);
            if (nonMaybeOut != null)
            {
                foreach (var parameterName in nonMaybeOut.parameters)
                {
                    var index = FindParameter(parameters, parameterName);
                    if (index < 0)
                        errors.Add($"NonMaybe out target '{memberId}' has no parameter '{parameterName}'.");
                    else if (parameters[index].passingMode != "Out")
                        errors.Add($"NonMaybe out target '{memberId}' parameter '{parameterName}' is not out.");
                    else if (!IsReferenceLike(parameters[index].type, physicalModel))
                        errors.Add($"NonMaybe out target '{memberId}' parameter '{parameterName}' has non-reference type.");
                    else
                        generated.SetOutProjection(index, UdonApiGeneratedProjection.Raw);
                }
            }

            if (generated.IsGenerated)
            {
                if (physical.Kind == UdonApiMemberKind.PropertyGetter && parameters.Count > 0 ||
                    physical.Kind == UdonApiMemberKind.PropertySetter && parameters.Count > 1)
                    generated.SkipReason = "Indexed properties are not supported by the current Sobakasu extern syntax.";
                else
                    foreach (var parameter in parameters)
                        if (parameter.isParams)
                        {
                            generated.SkipReason = "params parameters are not supported in v1.";
                            break;
                        }
            }
            return generated;
        }

        private static void ValidateConfiguration(
            UdonBindingGenerationConfig configuration,
            ISet<string> errors)
        {
            if (!string.Equals(configuration.version, "5", StringComparison.Ordinal))
                errors.Add($"Unsupported configuration version '{configuration.version}'. Expected '5'.");

            var namespaceRenames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in configuration.renames.namespaces)
            {
                if (rule == null)
                {
                    errors.Add("A namespace rename is null.");
                    continue;
                }
                ValidateClrPath(rule.from, $"namespace rename from '{rule.from}'", errors);
                if (!rule.ToSpecified)
                    errors.Add($"Namespace rename '{rule.from}' omits required property 'to'.");
                else if (rule.to != null)
                    ValidateSobakasuPath(rule.to, $"namespace rename '{rule.from}'", errors);
                if (!namespaceRenames.Add(rule.from ?? string.Empty))
                    errors.Add($"Conflicting namespace renames target '{rule.from}'.");
            }

            var typeRenames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in configuration.renames.types)
            {
                if (rule == null)
                {
                    errors.Add("A type rename is null.");
                    continue;
                }
                ValidateSourceIdentity(rule.from, "type rename", errors);
                ValidateModuleIdentifier(rule.to, $"type rename '{rule.from}'", errors);
                if (!typeRenames.Add(rule.from ?? string.Empty))
                    errors.Add($"Conflicting type renames target '{rule.from}'.");
            }

            var memberRenames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in configuration.renames.members)
            {
                if (rule == null)
                {
                    errors.Add("A member rename is null.");
                    continue;
                }
                ValidateSourceIdentity(rule.from, "member rename", errors);
                if (!IsCallableIdentifier(rule.to))
                    errors.Add($"Member rename '{rule.from}' has invalid target '{rule.to}'.");
                if (!memberRenames.Add(rule.from ?? string.Empty))
                    errors.Add($"Conflicting member renames target '{rule.from}'.");
            }

            var languageItemSources = new HashSet<string>(StringComparer.Ordinal);
            var languageItemNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in configuration.lang)
            {
                if (rule == null)
                {
                    errors.Add("A language item rule is null.");
                    continue;
                }
                ValidateSourceIdentity(rule.from, "language item", errors);
                if (string.IsNullOrWhiteSpace(rule.item))
                    errors.Add($"Language item rule '{rule.from}' has an empty item.");
                if (!languageItemSources.Add(rule.from ?? string.Empty))
                    errors.Add($"Conflicting language item rules target CLR type '{rule.from}'.");
                if (!languageItemNames.Add(rule.item ?? string.Empty))
                    errors.Add($"Language item '{rule.item}' is assigned more than once.");
            }

            ValidateUniquePaths(configuration.prelude.namespaces, "prelude namespace", true, errors);
            ValidateUniquePaths(configuration.prelude.types, "prelude type", true, errors);
            ValidateUniquePreludeMembers(configuration.prelude.members, errors);
            ValidateUniqueSources(configuration.maybe.nonMaybeTypes, "nonMaybe type", errors);
            ValidateUniqueSources(configuration.maybe.nonMaybeReturns, "nonMaybe return", errors);

            var maybeOuts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in configuration.maybe.nonMaybeOuts)
            {
                if (rule == null)
                {
                    errors.Add("A nonNonMaybe out rule is null.");
                    continue;
                }
                ValidateSourceIdentity(rule.member, "nonMaybe out", errors);
                if (!maybeOuts.Add(rule.member ?? string.Empty))
                    errors.Add($"Conflicting nonNonMaybe out rules target '{rule.member}'.");
                var parameters = new HashSet<string>(StringComparer.Ordinal);
                if (rule.parameters.Length == 0)
                    errors.Add($"NonMaybe out rule '{rule.member}' has no parameters.");
                foreach (var parameter in rule.parameters)
                {
                    if (string.IsNullOrWhiteSpace(parameter))
                        errors.Add($"NonMaybe out rule '{rule.member}' has an empty parameter.");
                    else if (!parameters.Add(parameter))
                        errors.Add($"NonMaybe out rule '{rule.member}' repeats parameter '{parameter}'.");
                }
            }

            ValidateUniquePaths(configuration.excludes.namespaces, "excluded namespace", false, errors);
            ValidateUniqueSources(configuration.excludes.types, "excluded type", errors);
            ValidateUniqueSources(configuration.excludes.members, "excluded member", errors);
        }

        private static UdonBindingNamespaceRenameRule MatchNamespaceRename(
            UdonBindingGenerationConfig configuration,
            UdonBindingSourceType type)
        {
            var clrNamespace = type.ClrNamespace ?? string.Empty;
            UdonBindingNamespaceRenameRule best = null;
            foreach (var rule in configuration.renames.namespaces)
            {
                if (rule == null || !IsNamespacePrefix(rule.from, clrNamespace))
                    continue;
                configuration.MarkRuleMatched(NamespaceRenameIdentity(rule));
                if (best == null || rule.from.Length > best.from.Length)
                    best = rule;
            }
            return best;
        }

        private static UdonBindingTypeRenameRule MatchTypeRename(
            UdonBindingGenerationConfig configuration,
            UdonBindingSourceType type)
        {
            foreach (var rule in configuration.renames.types)
            {
                if (rule == null || !string.Equals(rule.from, type.QualifiedName, StringComparison.Ordinal))
                    continue;
                configuration.MarkRuleMatched(TypeRenameIdentity(rule));
                return rule;
            }
            return null;
        }

        private static UdonBindingMemberRenameRule MatchMemberRename(
            UdonBindingGenerationConfig configuration,
            string memberId)
        {
            foreach (var rule in configuration.renames.members)
            {
                if (rule == null || !string.Equals(rule.from, memberId, StringComparison.Ordinal))
                    continue;
                configuration.MarkRuleMatched(MemberRenameIdentity(rule));
                return rule;
            }
            return null;
        }

        private static UdonBindingLangRule MatchLanguageItem(
            UdonBindingGenerationConfig configuration,
            UdonBindingSourceType type)
        {
            foreach (var rule in configuration.lang)
            {
                if (rule == null ||
                    !string.Equals(rule.from, type.QualifiedName, StringComparison.Ordinal))
                {
                    continue;
                }
                configuration.MarkRuleMatched(LanguageItemIdentity(rule));
                return rule;
            }
            return null;
        }

        private static string MatchNamespaceExclusion(
            UdonBindingGenerationConfig configuration,
            string clrNamespace)
        {
            string best = null;
            foreach (var value in configuration.excludes.namespaces)
            {
                if (!IsNamespacePrefix(value, clrNamespace))
                    continue;
                configuration.MarkRuleMatched(NamespaceExcludeIdentity(value));
                if (best == null || value.Length > best.Length)
                    best = value;
            }
            return best;
        }

        private static string MatchTypeExclusion(
            UdonBindingGenerationConfig configuration,
            UdonBindingSourceType type)
        {
            foreach (var value in configuration.excludes.types)
            {
                if (!string.Equals(value, type.QualifiedName, StringComparison.Ordinal))
                    continue;
                configuration.MarkRuleMatched(TypeExcludeIdentity(value));
                return value;
            }
            return null;
        }

        private static bool MatchMemberExclusion(
            UdonBindingGenerationConfig configuration,
            string memberId)
        {
            if (!Contains(configuration.excludes.members, memberId))
                return false;
            configuration.MarkRuleMatched(MemberExcludeIdentity(memberId));
            return true;
        }

        private static UdonBindingNonMaybeOutRule MatchNonMaybeOut(
            UdonBindingGenerationConfig configuration,
            string memberId)
        {
            foreach (var rule in configuration.maybe.nonMaybeOuts)
            {
                if (rule == null || !string.Equals(rule.member, memberId, StringComparison.Ordinal))
                    continue;
                configuration.MarkRuleMatched(NonMaybeOutIdentity(memberId));
                return rule;
            }
            return null;
        }

        private static string ResolveNamespace(
            UdonBindingSourceType type,
            UdonBindingNamespaceRenameRule rule)
        {
            if (rule == null)
                return DefaultNamespace;
            var clrNamespace = type.ClrNamespace ?? string.Empty;
            var suffix = clrNamespace.Length == rule.from.Length
                ? string.Empty
                : clrNamespace[(rule.from.Length + 1)..];
            var normalizedSuffix = NormalizeNamespace(suffix);
            if (string.IsNullOrEmpty(rule.to))
                return normalizedSuffix;
            var renamedNamespace = rule.to.Replace("::", ".");
            return string.IsNullOrEmpty(normalizedSuffix)
                ? renamedNamespace
                : $"{renamedNamespace}.{normalizedSuffix}";
        }

        private static string NormalizeNamespace(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            var segments = value.Split('.');
            for (var index = 0; index < segments.Length; index++)
            {
                segments[index] = SobakasuNameUtility.ToIdentifier(
                    segments[index],
                    $"namespace_{index}");
            }
            return string.Join(".", segments);
        }

        private static string ResolveFunctionName(UdonBindingSourceMember member)
        {
            if (SobakasuOperatorMapping.TryGet(
                    member,
                    out var operatorToken,
                    out var isUnary))
            {
                return isUnary ? $"@{operatorToken}" : operatorToken;
            }

            switch (member.Kind)
            {
                case UdonApiMemberKind.Constructor:
                    return "new";
                case UdonApiMemberKind.PropertySetter:
                case UdonApiMemberKind.FieldSetter:
                    var valueType = member.Parameters.Count == 0 ? null : member.Parameters[member.Parameters.Count - 1].type;
                    if (GetRuntimeName(valueType) == "System.Boolean" &&
                        TryGetPredicateStem(member.MemberName, out var setterStem))
                    {
                        return SobakasuNameUtility.ToNormalIdentifier(
                            $"set_{setterStem}",
                            "set_value");
                    }
                    return SobakasuNameUtility.ToNormalIdentifier(
                        $"set_{member.MemberName}",
                        "set_value");
            }

            var returnType = GetNormalReturnType(member);
            if (GetRuntimeName(returnType) == "System.Boolean" &&
                !HasParameterOutputs(member.Parameters) &&
                TryGetPredicateStem(member.MemberName, out var stem))
            {
                return SobakasuNameUtility.ToNormalIdentifier(stem, "predicate") + "?";
            }
            return SobakasuNameUtility.ToNormalIdentifier(member.MemberName, "member");
        }

        private static bool TryGetPredicateStem(string name, out string stem)
        {
            stem = null;
            if (string.IsNullOrEmpty(name) || name.Length <= 2 ||
                !(name.StartsWith("Is", StringComparison.Ordinal) ||
                  name.StartsWith("is", StringComparison.Ordinal)) ||
                !char.IsUpper(name[2]))
            {
                return false;
            }
            stem = name[2..];
            return true;
        }

        private static void AddStaleSourceRuleErrors(
            UdonBindingGenerationConfig configuration,
            ISet<string> errors)
        {
            foreach (var selector in configuration.receivers.Selectors)
            {
                var count = configuration.receivers.MatchCount(selector);
                if (count > 1)
                    errors.Add($"Ambiguous readonly receiver rule '{selector}' matched multiple physical members.");
                if (count == 0)
                    errors.Add($"Configuration rule '{selector}' did not match any discovered CLR API.");
            }
            foreach (var identity in GetConfiguredRuleIdentities(configuration))
            {
                if (identity.StartsWith("prelude.", StringComparison.Ordinal))
                    continue;
                if (identity.StartsWith("receiver.readonly:", StringComparison.Ordinal))
                    continue;
                if (configuration.GetRuleMatchCount(identity) == 0)
                    errors.Add($"Configuration rule '{identity}' did not match any discovered CLR API.");
            }
        }

        private static void ValidateUniquePaths(
            IReadOnlyList<string> values,
            string location,
            bool sobakasu,
            ISet<string> errors)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (sobakasu)
                    ValidateSobakasuPath(value, location, errors);
                else
                    ValidateClrPath(value, location, errors);
                if (!seen.Add(value ?? string.Empty))
                    errors.Add($"Duplicate {location} '{value}'.");
            }
        }

        private static void ValidateUniqueSources(
            IReadOnlyList<string> values,
            string location,
            ISet<string> errors)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                ValidateSourceIdentity(value, location, errors);
                if (!seen.Add(value ?? string.Empty))
                    errors.Add($"Duplicate {location} '{value}'.");
            }
        }

        private static void ValidateUniquePreludeMembers(
            IReadOnlyList<string> values,
            ISet<string> errors)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add("prelude member has an empty Sobakasu path.");
                }
                else
                {
                    var segments = value.Split(new[] { "::" }, StringSplitOptions.None);
                    for (var index = 0; index < segments.Length - 1; index++)
                    {
                        if (!IsModuleIdentifier(segments[index]))
                        {
                            errors.Add($"prelude member has invalid Sobakasu path '{value}'.");
                            break;
                        }
                    }
                    if (segments.Length == 0 || !IsCallableIdentifier(segments[^1]))
                        errors.Add($"prelude member has invalid Sobakasu path '{value}'.");
                }
                if (!seen.Add(value ?? string.Empty))
                    errors.Add($"Duplicate prelude member '{value}'.");
            }
        }

        private static void ValidateSourceIdentity(
            string value,
            string location,
            ISet<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf(' ') >= 0)
                errors.Add($"{location} has malformed CLR identity '{value}'.");
        }

        private static void ValidateClrPath(
            string value,
            string location,
            ISet<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{location} has an empty CLR namespace path.");
                return;
            }
            foreach (var segment in value.Split('.'))
            {
                if (!IsClrIdentifier(segment))
                {
                    errors.Add($"{location} has invalid CLR namespace path '{value}'.");
                    return;
                }
            }
        }

        private static void ValidateSobakasuPath(
            string value,
            string location,
            ISet<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{location} has an empty Sobakasu path.");
                return;
            }
            foreach (var segment in value.Split(new[] { "::" }, StringSplitOptions.None))
            {
                if (!IsModuleIdentifier(segment))
                {
                    errors.Add($"{location} has invalid Sobakasu path '{value}'.");
                    return;
                }
            }
        }

        private static void ValidateModuleIdentifier(
            string value,
            string location,
            ISet<string> errors)
        {
            if (!IsModuleIdentifier(value))
                errors.Add($"{location} has invalid Sobakasu identifier '{value}'.");
        }

        private static bool IsClrIdentifier(string value)
        {
            return SobakasuIdentifierFacts.IsUasmSymbol(value);
        }

        private static bool IsModuleIdentifier(string value)
        {
            return SobakasuIdentifierFacts.IsBareIdentifier(value);
        }

        private static bool IsCallableIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            return value.EndsWith("?", StringComparison.Ordinal)
                ? value.Length > 1 && SobakasuIdentifierFacts.IsNormalIdentifier(
                    value[..^1])
                : SobakasuIdentifierFacts.IsNormalIdentifier(value);
        }

        private static bool IsStaticMember(UdonBindingSourceMember member)
        {
            return member.IsStatic;
        }

        private static bool HasParameterOutputs(IReadOnlyList<ExternParameterRecord> parameters)
        {
            foreach (var parameter in parameters)
            {
                if (parameter.passingMode == "Ref" || parameter.passingMode == "Out")
                    return true;
            }
            return false;
        }

        private static int FindParameter(
            IReadOnlyList<ExternParameterRecord> parameters,
            string name)
        {
            for (var index = 0; index < parameters.Count; index++)
            {
                if (string.Equals(parameters[index].name, name, StringComparison.Ordinal))
                    return index;
            }
            return -1;
        }

        private static bool IsNamespacePrefix(string prefix, string value)
        {
            return !string.IsNullOrEmpty(prefix) &&
                (string.Equals(prefix, value, StringComparison.Ordinal) ||
                 (value.StartsWith(prefix, StringComparison.Ordinal) &&
                  value.Length > prefix.Length &&
                  value[prefix.Length] == '.'));
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            foreach (var candidate in values)
            {
                if (string.Equals(candidate, value, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string NamespaceRenameIdentity(UdonBindingNamespaceRenameRule rule) =>
            $"rename.namespace:{rule.from}";
        private static string TypeRenameIdentity(UdonBindingTypeRenameRule rule) =>
            $"rename.type:{rule.from}";
        private static string MemberRenameIdentity(UdonBindingMemberRenameRule rule) =>
            $"rename.member:{rule.from}";
        private static string LanguageItemIdentity(UdonBindingLangRule rule) =>
            $"lang.type:{rule.from}";
        private static string ReceiverReadonlyIdentity(UdonBindingMemberSelector member) => $"receiver.readonly:{member}";
        private static string NonMaybeTypeIdentity(string type) => $"maybe.nonMaybeType:{type}";
        private static string NonMaybeReturnIdentity(string member) =>
            $"maybe.nonMaybeReturn:{member}";
        private static string NonMaybeOutIdentity(string member) =>
            $"maybe.nonMaybeOut:{member}";
        private static string NamespaceExcludeIdentity(string value) =>
            $"exclude.namespace:{value}";
        private static string TypeExcludeIdentity(string value) =>
            $"exclude.type:{value}";
        private static string MemberExcludeIdentity(string value) =>
            $"exclude.member:{value}";

        private static void ThrowIfErrors(IReadOnlyCollection<string> errors)
        {
            if (errors.Count == 0)
                return;
            throw new UdonBindingConfigurationException(
                "Udon binding generation policy validation failed:\n- " +
                string.Join("\n- ", errors));
        }
    }
}
