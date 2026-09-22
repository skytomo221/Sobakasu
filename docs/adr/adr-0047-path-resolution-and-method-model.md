# ADR-0047: Path Resolution and Method Model

## Status

Accepted

## Context

ADR-0016 introduced implicit `self` for impl functions and used `static fn` for
associated functions. It also permitted the same `.` syntax to be parsed before
the Binder inferred whether its receiver was a type, module, or value. That
model makes source intent ambiguous and leaks CLR static/instance terminology
into Sobakasu source semantics. ADR-0029 and ADR-0030 depend on a stable method
and extern-resolution model, so this boundary must be explicit before extending
the language further.

## Decision

`::` is Sobakasu's path-resolution separator. A Sobakasu path resolves names
from modules, namespaces, types, enum variants, associated functions,
associated constants, and nested types. `.` is Sobakasu value member access
and instance method invocation.

```sobakasu
use core::string;
Foo::new(1);
Result::Ok(value);
foo.update();
self.value;
```

The Parser represents `::` as a first-class `DoubleColonToken` and creates
`PathExpressionSyntax`. `MemberAccessExpressionSyntax` continues to represent
only `.` access. The Binder resolves paths as module/type-associated access and
value members separately; it does not infer an associated call from a `.`
receiver that happens to be a type. Enum variants are path items.

`static` is removed from the Sobakasu source language. It remains a reserved,
removed keyword solely so the Parser can issue a migration diagnostic. Inside
an `impl`, a function without a receiver is an associated function and a
function whose first parameter is the dedicated receiver parameter `self` is
an instance method.

```sobakasu
impl Foo {
    fn new(value: i32) -> Foo { Foo { value } }
    fn update(self, value: i32) { self.value = value; }
}
```

`self` has no type annotation, may occur only once, and must be the first impl
parameter. It is a dedicated syntax node and a distinct `FunctionSymbol`
receiver parameter, not an ordinary parameter name and not a Binder-synthesized
binding. `FunctionSymbol.HasReceiver`, `IsInstanceMethod`, and
`IsAssociatedFunction` express Sobakasu method semantics. CLR/Udon
`MethodSymbol.IsStatic` remains external metadata and is not the source method
classification.

This source-expression rule does not apply to the external identity on the
right side of `extern`. CLR/Udon qualified names are a distinct grammar and
namespace, not Sobakasu paths: they retain their CLR spelling with `.` (and
the catalog's canonical nested-type spelling when applicable). An `extern`
target is parsed and bound as an external identity; it is never parsed as a
Sobakasu `::` path and converted later.

```sobakasu
use unity::GameObject;

let object = GameObject::find("Foo");

pub type GameObject = extern UnityEngine.GameObject;

impl GameObject {
    pub fn find(name: string) -> Maybe<Self>
        = maybe extern UnityEngine.GameObject.Find(name)

    pub fn set_active(self, active: bool)
        = extern self.SetActive(active)
}
```

Here `unity::GameObject` and `GameObject::find` are Sobakasu symbol paths,
`self.SetActive` is value-member access, and `UnityEngine.GameObject.Find` is
the CLR/Udon ABI identity. The standard-library generator emits these distinct
forms and generated library files are regenerated through the generator.

This ADR supersedes the implicit-self and `static fn` portions of ADR-0016 and
the corresponding method references in ADR-0029 and ADR-0030. Their overload
selection, declarative extern binding, and Binder-to-backend responsibility
boundaries remain in effect.

## Alternatives

### Continue to infer type access from `.`

This preserves ambiguous source syntax and makes module/type access a Binder
special case, so it is rejected.

### Keep `static fn` as a compatibility spelling

It retains two source models for the same concept and delays migration, so it
is rejected.

### Treat `self` as an untyped ordinary parameter

This distributes nullable parameter-type handling through semantic stages and
does not enforce receiver position structurally, so it is rejected.

## Rationale

Separating path and value-member syntax lets the Parser retain user intent and
lets the Binder enforce a small, predictable call model. Keeping external
identities separate prevents a Sobakasu namespace rename or path rule from
changing a CLR/Udon catalog lookup. Explicit receiver syntax also makes source,
symbol, lowering, generated bindings, and diagnostics agree without conflating
Sobakasu concepts with CLR metadata.

## Consequences

### Positive

* Module/type paths, enum variants, associated items, and nested types share a
  single source model.
* Instance calls always visibly have a value receiver.
* Binder and lowering consume explicit receiver metadata.
* Generated bindings no longer emit removed `static` syntax or implicit self.
* CLR/Udon catalog lookup receives its canonical external identity directly,
  independently of Sobakasu path spelling.

### Negative

* Existing dotted paths and `static fn` declarations require migration.
* Parser, Binder, generator, generated standard library, and tests must change
  together.
