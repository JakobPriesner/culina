---
name: code-simplicity
description: The default standard for all culina-v2 code — write the smallest, plainest thing that works, prefer clarity over cleverness or micro-performance, and extract anything that has to happen in more than one place. Use when writing any new code, when a file or method is growing, when tempted to add an abstraction or an optimisation, and when reviewing a change for unnecessary complexity.
---

# Simple and small

**The simplest code that is correct wins.** Not the fastest, not the most
extensible, not the most clever. Simple code is the code a person reads once
and understands, and it is the code that is still cheap to change in a year.

This applies to every language and layer in this repository, and it overrides
any stylistic preference in the other skills — if a convention here would force
something convoluted, the convention is the thing that bends.

## What "simple" means concretely

- **A method does one thing, and its name says which.** If the name needs
  "And" or a comment to explain a second responsibility, split it.
- **Short bodies.** A method beyond roughly 30 lines, or with more than two
  levels of nesting, is a method with a hidden method inside it. Use early
  returns and guard clauses instead of `else` ladders.
- **Few parameters, few fields, few branches.** More than about five
  constructor parameters usually means the class is two classes.
- **Plain constructs first**: a `foreach` over a clever LINQ chain when the
  chain would need a reader to pause; a straightforward `if` over a nested
  ternary; a named local over a comment explaining an expression.
- **Delete rather than accommodate.** Dead code, commented-out code, a flag
  nothing sets, a parameter every caller passes the same value for — remove it.
  Git remembers.

## Clarity beats performance

Write the obvious version. Optimise only when a measurement says this code is
the problem — a profiler, a trace, a metric, a reproducible benchmark.

- No object pooling, no `Span`/`stackalloc` gymnastics, no hand-rolled caching,
  no manual struct layouts, no "avoid the allocation" rewrites, unless the
  telemetry names that path.
- When an optimisation is genuinely justified, it gets a comment with **the
  measurement that justified it**, so the next person knows whether it still
  applies.
- Correctness and readability are never traded for a saved allocation. A slow
  correct endpoint can be made fast; a fast subtle one has to be rewritten.

## Don't over-engineer

Build what is needed now, for what is known now.

- No interface with exactly one implementation "for testability" when the
  concrete type is already injectable and fake-able — the ports in
  `Application/Abstractions/` exist because a technology boundary is really
  there, not as a reflex.
- No generic base class, no plugin/strategy registry, no configuration knob, no
  event bus added for a second case that does not exist yet.
- No layer of indirection whose only job is to forward a call.
- No "framework" inside the app. Two similar things are two things; three make
  a pattern worth naming.

The rule of thumb: an abstraction must remove more code and more concepts than
it introduces. If it only moves them, skip it.

## But do extract anything that repeats

The other half of the rule, and just as important: **if something has to be
done in more than one place, it lives in exactly one place.** Copy-paste is how
two call sites quietly drift apart, and how a fix lands in one of them.

When you find yourself writing the same thing a second time, extract it —
before the third copy exists:

| Repeated thing | Where it goes |
| --- | --- |
| Same mapping between two shapes | one static extension mapper (`dotnet-layer-mapping`) |
| Same validation or invariant | a domain method or value object, not each handler |
| Same error construction | a `<Domain>Errors` member |
| Same query/SQL shape | one repository method |
| Same cross-cutting request behaviour | middleware (`dotnet-middleware`) |
| Same markup or interaction | one Svelte component (`sveltekit-components-and-pages`) |
| Same fetch/ETag/error handling | the API client wrapper (`frontend-api-client`) |
| Same test setup | one builder or fixture (`dotnet-testing`) |
| Same constant, route, header name, or magic string | one named constant, referenced everywhere |

Extraction rules so the cure is not worse than the disease:

- Extract to the **nearest shared place**, not to a global `Common`/`Utils`/
  `Helpers` bucket. Those become dumping grounds nobody can prune.
- Give it a name from the domain (`HouseholdMembershipPolicy`,
  `formatServings`), not from its mechanism (`Helper`, `Manager`, `Util`).
- Extract **duplicated meaning**, not duplicated characters. Two blocks that
  look alike but answer to different rules will need to change separately;
  merging them creates a parameterised knot. When in doubt, wait for the third
  occurrence.

## Reviewing for complexity

Ask, in order:

1. Can this be done with less code that reads as well? Do that instead.
2. Is anything here duplicated from somewhere else in the repo? Extract it.
3. Is any abstraction, option, or parameter here unused today? Remove it.
4. Does any comment explain *what* the code does? Rename until it does not, and
   keep the comments that explain *why*.
5. Would a new contributor understand this file without asking? If not, it is
   not finished.

## Checklist

- [ ] The change is the smallest one that solves the problem as asked.
- [ ] No new abstraction, generic, or configuration knob without a present-day
      second use.
- [ ] Nothing added here duplicates something that already exists; anything
      that will exist twice was extracted to one place with a domain name.
- [ ] No performance trick without a measurement in a comment.
- [ ] Methods are short, flat, single-purpose, and named for what they do.
