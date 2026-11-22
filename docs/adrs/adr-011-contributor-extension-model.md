# ADR-011 — A module declares what it owns; the kernel discovers it

**Status:** Accepted · **Date:** 2026-07-27 · **Phase:** B2.6

---

## Context

B2.5's audit found the permission and feature engines closed to extension. Adding a module meant
editing the kernel and editing `Program.cs` — so every new vertical touched the two files most
likely to be touched by something else, and the composition root grew a line per module forever.

---

## Decision

**A module implements `IPlatformModule` and declares what it owns through contributor interfaces. The
kernel scans for both.**

- A seam is a public interface named `I…Contributor` under a `Diaspora.` namespace.
  `ContributorSeams` finds them without being told what they are.
- **Six seams are built by the kernel before the container exists** — permissions, features,
  policies, observability, endpoints, database — and must therefore have public parameterless
  constructors. Every other contributor is resolved from DI like anything else.
- Assemblies are found from the dependency context rather than `AppDomain.GetAssemblies()`, because a
  module whose types nothing has touched yet is the normal case at startup.
- Test assemblies are excluded by the `Tests` suffix, after a phantom test contributor collided with
  a real one in the integration suite.

**Adding a module is a project reference. Nothing else.**

---

## Consequences

### Good

- **Thirteen modules have landed with zero kernel edits and zero `Program.cs` edits.**
- The model composes more than once per module: B8 → B8.1 → B8.2 registered three permission
  contributors, three endpoint contributors, three telemetry sources and three route prefixes on one
  `IPlatformModule` at one order slot.
- It works in both directions at once. B14 discovers seams from other assemblies while being
  discovered by the kernel through the same mechanism, so a vertical can tune its own recommendation
  surface without B14 knowing it exists.

### Costs, accepted

- **Every seam declares `Module` separately.** A small duplication the kernel chose over a base
  interface every module would have to know about; `ContributorSeams.DeclaredModule` reads it by
  convention.
- **The reflective catalogue sweep executes module code at startup.** Every call is wrapped, and the
  sweep can be switched off without silencing the other validators.
- **Everything scanned is scoped, including the registry.** A singleton registry over scoped seams is
  a captive dependency the host's provider validation refuses to start on. B4 found it first, then
  B6, B7, B8, B9, B10 and B11.
- Two modules claiming one name, or one feature gate, is a startup failure — which is correct, and
  which means landing a vertical must delete its placeholder entries from the provisional catalogue
  or every request that resolves the catalogue fails.

### Rejected alternatives

**A registration method per module in the composition root.** Rejected: that is the file being
removed.

**A marker base class for contributors.** Rejected: it forces every module to reference a kernel type
it otherwise would not need, for one property.

**A hard-coded list of seams in the kernel.** Rejected, with evidence: the architecture suite did
exactly that for database contributors and was still reading seven two verticals later — so
Marketplace and Housing were exempt from a platform rule by omission, silently, for four phases.

---

## Related

- ADR-018 — reference-data seeder discovery, the same reasoning applied to catalogues
- ADR-016 — extending a module rather than adding one
- [Module Extension Guide](../platform/module-extension-guide.md)
