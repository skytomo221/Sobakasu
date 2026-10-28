using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
    [Serializable]
    internal sealed class UdonApiSkipRecord
    {
        public string full_name;
        public string declaring_type;
        public string surface_type;
        public string clr_declaring_type;
        public string member_kind;
        public string signature;
        public string extern_signature;
        public string reason;
        public bool is_udon_exposed;
        public List<string> surface_types = new();
        public List<string> generated_surface_types = new();
        public List<string> reasons = new();
        public List<UdonApiSurfaceFailureRecord> surface_failures = new();
    }

    [Serializable]
    internal sealed class UdonApiSurfaceFailureRecord { public string surface_type; public string reason; }
    [Serializable]
    internal sealed class UdonApiPhysicalRecord
    {
        public string extern_signature;
        public string physical_full_name;
        public string clr_declaring_type;
        public string member_kind;
        public string signature;
        public List<string> surface_types = new();
        public List<string> generated_surface_types = new();
        public bool is_udon_exposed;
        public bool is_covered;
        public List<string> reasons = new();
        public List<UdonApiSurfaceFailureRecord> surface_failures = new();
    }
    [Serializable]
    internal sealed class UdonApiSkipReasonCount { public string reason; public int count; }
    [Serializable]
    internal sealed class UdonApiGeneratedTypeRecord
    {
        public string clr_declaring_type;
        public string sobakasu_namespace;
        public string placement;
        public string generated_file;
    }
    [Serializable]
    internal sealed class UdonApiGenerationReport
    {
        public string configuration_path;
        public string configuration_version;
        public int types_discovered, types_generated, types_skipped;
        public int members_discovered, members_generated, members_skipped;
        public int member_surfaces_discovered, member_surfaces_generated, member_surfaces_skipped;
        public int udon_signatures_discovered, udon_signatures_exposed, udon_signatures_covered, udon_signatures_unsupported;
        public double udon_api_coverage_percent;
        public int udon_exposed_unmatched_signatures_count;
        public List<string> udon_exposed_unmatched_signatures = new();
        public List<UdonApiPhysicalRecord> udon_api = new();
        public List<string> physical_modeled_signatures = new();
        public List<string> physical_covered_signatures = new();
        public List<string> physical_unsupported_signatures = new();
        public List<string> generated_public_surfaces = new();
        public List<string> skipped_public_surfaces = new();
        public List<string> reconciled_catalog_signatures = new();
        public int physical_api_total, physical_api_covered, physical_api_unsupported;
        public int physical_declared_api_total, physical_declared_api_covered, physical_declared_api_unsupported;
        public double physical_declared_api_coverage_percent;
        public List<string> physical_declared_signatures = new();
        public List<string> physical_declared_covered_signatures = new();
        public List<string> physical_declared_unsupported_signatures = new();
        public int catalog_declared_members, catalog_capability_signatures, catalog_unmodeled_signatures;
        public int catalog_exposed_declared_members, catalog_unexposed_declared_members;
        public int udon_exposed_signature_total;
        public List<string> non_extern_udon_signatures = new();
        public string physical_api_metric_definition = "Distinct actual exposed extern ABI signatures, including capabilities and unmodeled externs, independent of catalog record layout. Non-extern graph/type/constant/event node definitions remain in udon_exposed_signature_total and non_extern_udon_signatures. Covered means a generated surface or compiler-supported capability. physical_declared_api_* counts declaration-owned CLR APIs without projected host aliases or capabilities. Legacy udon_signatures_* counts projected member bindings only.";
        public List<UdonApiSkipRecord> skipped_types = new();
        public List<UdonApiSkipRecord> skipped_members = new();
        public List<UdonApiSkipReasonCount> skip_reasons = new();
        public List<UdonApiSkipReasonCount> type_skip_reasons = new();
        public List<UdonApiSkipReasonCount> surface_skip_reasons = new();
        public List<UdonApiSkipReasonCount> udon_unsupported_reasons = new();
        public int rules_configured, rules_matched;
        public List<string> unmatched_rules = new();
        public int explicit_exclusions, declaration_collisions, raw_return_count, maybe_return_count, raw_out_count, maybe_out_count;
        public int impl_type_count, top_level_static_type_count, namespaces_generated, namespace_rules_configured, namespace_rules_matched;
        public List<string> unmatched_namespace_rules = new();
        public List<UdonApiGeneratedTypeRecord> generated_types = new();
    }
}
