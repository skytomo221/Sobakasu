# Standard Library Generator test migration baseline

This file freezes the `tmp2` regression-test assets before the standalone Standard Library Generator migration and records the migration result.

The migration must preserve every behavior covered by these suites. A suite may be rewritten to use Catalog v2 fixtures, but it must not be deleted merely because the production generator no longer uses reflection. Any removed or merged test case must have an explicit replacement and rationale. Completion requires `unmapped = 0`.

| Suite | Baseline blob | Migration result |
| --- | --- | --- |
| `UdonBindingGeneratorConfigurationTests.cs` | `6503c2bdd7937dfd2033030cd18ca87d929ace6a` | KEEP — file unchanged |
| `UdonBindingGeneratorCoverageTests.cs` | `7027d71f052639e3e091f694135296fbac35f928` | KEEP — file unchanged |
| `UdonBindingGeneratorFormattingTests.cs` | `c3dd03ac9a04d65e8ad6032d55060f373a340944` | KEEP — file unchanged; reflection fixture now reaches the production generator through the migration adapter |
| `UdonBindingGeneratorNestedTypeTests.cs` | `1babfba5ba3c23e843de605d08953f572df3fc8e` | KEEP — file unchanged; canonical CLR `+` regression coverage retained |
| `UdonBindingGeneratorOperatorTests.cs` | `71c0f18dcd46593ccac3c6f0aa3d52fc824be2cf` | KEEP — file unchanged |
| `UdonBindingGeneratorRenderingTests.cs` | `50bcd359a1433ee73f2c4e0c00d33034e4716cdc` | KEEP — file unchanged |
| `UdonBindingGeneratorReportTests.cs` | `7ebc099dcd05ff773ffd3bb29e863689f6127a4e` | KEEP — file unchanged |

The retained suites continue to use their existing reflection fixtures, but `UdonBindingGeneratorMigrationAdapter` projects those fixtures into `UdonBindingSourceModel` and then invokes the same production policy/renderer/generator core used by Catalog-driven generation. This preserves the original assertions without keeping a second generator implementation in tests.

Additional standalone coverage was added for Catalog v2 parsing/projection, nested runtime identity, metadata-closure types, effective static API containers, missing/old catalogs, and duplicate JSON properties. Black-box CLI tests execute the standalone process without Unity and cover successful generation with relative paths, invalid and missing Catalog input, invalid configuration, missing additions, invalid/duplicate options, and success/failure exit codes. CI also executes the CLI against the checked-in Catalog using temporary output paths.

## Responsibility mapping

- Reflection discovery remains only as the Catalog-generation/test-fixture adapter.
- Catalog v2 remains the persisted physical CLR/Udon snapshot and preserves canonical nested CLR identity (`Outer+Inner`).
- Standard Library generation consumes Catalog v2 through a generator-local source model; publication policy is not persisted in the Catalog.
- EditorWindow and standalone CLI are adapters over the same production generation implementation.
- CLI generation does not fall back to `UdonApiDiscovery`, loaded Unity assemblies, or `UdonExposedNodeCache`.

## Completion accounting

```text
existing baseline suites: 7
kept unchanged:          7
rewritten_for_v2:        0
merged:                  0
justified_obsolete:      0
unmapped:                0
```

No baseline test suite or test case was intentionally removed. New Catalog/CLI tests are additive and are not counted as replacements for the baseline suites.
