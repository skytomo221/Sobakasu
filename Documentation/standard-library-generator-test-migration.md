# Standard Library Generator test migration baseline

This file freezes the `tmp2` regression-test assets before the standalone Standard Library Generator migration.

The migration must preserve every behavior covered by these suites. A suite may be rewritten to use Catalog v2 fixtures, but it must not be deleted merely because the production generator no longer uses reflection. Any removed or merged test case must have an explicit replacement and rationale. Completion requires `unmapped = 0`.

| Suite | Baseline blob | Baseline size | Migration status |
| --- | --- | ---: | --- |
| `UdonBindingGeneratorConfigurationTests.cs` | `6503c2bdd7937dfd2033030cd18ca87d929ace6a` | 41,387 | MIGRATE / preserve assertions |
| `UdonBindingGeneratorCoverageTests.cs` | `7027d71f052639e3e091f694135296fbac35f928` | 9,266 | MIGRATE / preserve assertions |
| `UdonBindingGeneratorFormattingTests.cs` | `c3dd03ac9a04d65e8ad6032d55060f373a340944` | 7,707 | KEEP reflection-adapter tests; add Catalog v2 projection tests |
| `UdonBindingGeneratorNestedTypeTests.cs` | `1babfba5ba3c23e843de605d08953f572df3fc8e` | 3,257 | MIGRATE; preserve canonical CLR `+` identity regression coverage |
| `UdonBindingGeneratorOperatorTests.cs` | `71c0f18dcd46593ccac3c6f0aa3d52fc824be2cf` | 6,116 | MIGRATE / preserve assertions |
| `UdonBindingGeneratorRenderingTests.cs` | `50bcd359a1433ee73f2c4e0c00d33034e4716cdc` | existing large suite | MIGRATE / preserve assertions |
| `UdonBindingGeneratorReportTests.cs` | `7ebc099dcd05ff773ffd3bb29e863689f6127a4e` | existing suite | MIGRATE / preserve assertions |

## Responsibility mapping

- Reflection discovery remains only as the Catalog-generation/test adapter.
- Catalog v2 remains the persisted physical CLR/Udon snapshot and preserves canonical nested CLR identity (`Outer+Inner`).
- Standard Library generation consumes Catalog v2 through a generator-local source model; publication policy is not persisted in the Catalog.
- EditorWindow and standalone CLI are adapters over the same production generation implementation.
- CLI generation must not fall back to `UdonApiDiscovery`, loaded Unity assemblies, or `UdonExposedNodeCache`.

## Completion accounting

The final implementation report must account for the baseline as:

```text
existing cases = kept + rewritten_for_v2 + merged + justified_obsolete
unmapped = 0
```

`justified_obsolete` requires either a removed language/API behavior or an identified replacement test that covers the same invariant. A schema or architecture change by itself is not sufficient justification.
