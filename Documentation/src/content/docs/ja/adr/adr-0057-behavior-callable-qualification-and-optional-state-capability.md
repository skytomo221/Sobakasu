---
title: 'ADR-0057: Behavior Callable Qualification and Optional State Capability'
---

## Status

Accepted

## Context

Behavior function calls already distinguish state-independent and state-dependent callables through `behavior::name(...)`, `state.name(...)`, and `behavior::name(state, ...)`. Network sends historically used a separate bare-name syntax and binder path. This made qualification, state capability checks, and the distinction between compile-time capability and runtime arguments inconsistent.

Separately, `on interact` required a `state` capability even when its body did not access script state. Other event handlers did not require such a special rule, and state access is already checked through the event body's capability context.

ADR-0024 records the earlier bare `send name ...` syntax. ADR-0054 also contains examples written under the earlier send and `interact` rules. Those statements remain historical records and are superseded where they conflict with this decision.

## Decision

- Behavior callable references use `behavior::` or `state.` qualification.
- Network sends use the same qualification rules as ordinary behavior function calls.
- Bare `send name ...` and unqualified `send name() ...` are invalid.
- `behavior::name(state, ...)` treats `state` as a compile-time capability, not a runtime argument or network payload.
- `state.name(...)` and `behavior::name(state, ...)` are the two invocation forms for a state-dependent behavior callable.
- Event handler state capability is optional, including for `on interact`. The special rule requiring state for `interact` is removed. Access to `state` still requires the capability through the existing binding checks.
- Ordinary behavior calls and network sends share callable qualification resolution, including invocation form, capability use, callable-name extraction, and runtime argument selection.
- `FunctionSymbol` and `NetworkReceiveSymbol` remain separate. Ordinary calls continue to bind to their local-call bound nodes, while sends continue to bind to `BoundNetworkSendStatement` and use network lowering.

For example, a state-dependent receiver declared as `receive changed(state, value: i32)` can be invoked by either `state.changed(value)` or `behavior::changed(state, value)`. Only `value` is sent over the network.

## Alternatives

- Keep bare send syntax and add send-specific checks: rejected because behavior calls and sends would continue to interpret qualification and state capability differently.
- Infer state capability by inspecting handler bodies: rejected because it introduces effect inference and duplicates the existing capability checks.
- Unify function and network receiver symbols or their lowering: rejected because local calls and network entrypoints have different semantics and runtime ABIs.

## Rationale

One qualification resolver gives ordinary calls and sends the same source-level meaning without conflating their symbols, payload validation, bound nodes, or runtime behavior. Making event capability optional also gives every event the same explicit state-access rule: state is available only when declared, and the body is checked by the existing state receiver binding.

## Consequences

### Positive

- Callable qualification has one consistent meaning in local calls and network sends.
- The compile-time state capability cannot accidentally become a runtime network payload.
- State-free event handlers, including `on interact`, need no unused capability.
- Existing network receiver visibility, payload ABI, metadata, and lowering remain independent from ordinary function calls.

### Negative

- Existing bare-send programs must add `behavior::` or `state.` qualification.
- Code that used `on interact(state)` solely to satisfy the old requirement can omit the capability; code accessing state must continue to declare it.
