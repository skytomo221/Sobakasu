# ADR-0045: External Nominal Type Declarations and Udon Exposure Catalog

## Status

Accepted

## Context

`impl T = extern Runtime.T` combines two independent concerns: declaring a Sobakasu type identity and providing an implementation container.  The Standard Library Generator consequently used that legacy syntax for normal CLR reference types.  In addition, the default extern catalog was built from a namespace-prefix allowlist, so Udon-exposed APIs in other namespaces could not be represented even when their CLR and Udon metadata existed.

Sobakasu needs to represent an exposed runtime type without granting source aggregate semantics, construction, fields, enum variants, arrays, or arbitrary members.  It must also index Udon-exposed type and member metadata independently from the generator policy that decides which public bindings to emit.

## Decision

Introduce the canonical external nominal type declaration:

```sobakasu
type Foo = extern Runtime.Foo;
pub type Foo = extern Runtime.Foo;
```

It creates a distinct Sobakasu `TypeSymbol` whose runtime identity is the bound CLR/Udon type.  It reuses the existing external-binding metadata (`RuntimeQualifiedName`, `RuntimeClrType`, `IsExternalBinding`, and ABI mapping) and does not introduce a new `TypeKind`.  The declaration alone grants no aggregate layout, constructor, implicit construction, field, variant, array-construction, or member capability.  Members remain available only through separately declared and Binder-resolved extern bindings, normally in `impl Foo`.

`struct T = extern Runtime.T` and `enum T = extern Runtime.T` continue to represent stronger source aggregate semantics.  Canonical primitive external bindings continue to use `impl` as specified by ADR-0041, and static API containers remain top-level modules.

`impl T = extern Runtime.T` remains accepted with its existing semantics for source compatibility.  New generated reference-type bindings use `type T = extern Runtime.T` followed, when needed, by `impl T`.  The legacy `impl = extern` syntax is planned for future removal, but this decision adds no deprecation diagnostic, migration, or source rewrite.

The `type` declaration family is deliberately extensible to a future transparent alias syntax, `type Foo = Bar`.  This ADR implements only the `= extern` external nominal form; it does not add alias resolution or equate `Foo` and `Bar`.

`ReflectionExternCatalogBuilder.BuildDefaultCatalog()` discovers public and nested-public types in loaded CLR assemblies, retains those accepted by `UdonExposedNodeCache.IsTypeExposed`, and indexes only Udon-exposed members.  It has no namespace allowlist.  `BuildCatalog(namespacePrefixes)` remains the explicit namespace-limited operation used by tests and callers that request that scope.  The Binder consumes the built catalog and never performs its own assembly scan.

The Standard Library Generator selects placement independently of catalog discovery: canonical primitives use `Impl`, static containers use `TopLevel`, CLR enums use `Enum`, CLR value types use `Struct`, and other CLR types use the new `Type` placement.  A `Type` declaration is emitted even with no generated members; members are emitted in a separate ordinary `impl` block.  Reports identify that placement as `type`.

Parser and Binder resolve the declaration before IR lowering.  IR, optimizer, and UASM backend consume the existing resolved external type and extern member symbols without type-declaration-specific work.

## Alternatives

1. Add missing namespaces or individual CLR types to the default allowlist. This cannot track future Udon APIs and confuses discovery with generator policy.
2. Treat `type = extern` as a generator-only fallback. This would leave handwritten source unsupported and create parallel type semantics.
3. Make every public CLR member available once a type is indexed. Public CLR metadata does not imply Udon exposure.
4. Resolve CLR types by scanning assemblies in the Binder. This violates the catalog and compiler-phase responsibility boundary.

## Rationale

Separating nominal identity, runtime ABI identity, and source capabilities lets a Udon-exposed type exist in Sobakasu without inventing operations that Udon does not provide.  Discovery by Udon exposure gives the catalog a stable runtime-index role, while the generator remains responsible for its public API policy.  Reusing the established `TypeSymbol` and resolved extern-call pipeline keeps semantic resolution in the Binder and preserves the backend boundary from ADR-0003.

## Consequences

### Positive

* Udon-exposed APIs outside historical namespace prefixes can be represented and generated without per-type exceptions.
* Source can declare opaque runtime identities and add only explicitly bound members.
* Generated reference APIs use a canonical declaration form while legacy source remains compatible.
* The catalog, generator policy, and compiler phases have distinct responsibilities.

### Negative

* Parser, declaration collection, module visibility, language-item handling, generator rendering, and report schema require coordinated updates.
* A bare external nominal type deliberately cannot be constructed or inspected without further declarations.
* The future transparent type-alias form requires a later semantic decision and implementation.
