# shared/

Source compiled into more than one mod in this tree. There is no shared assembly: Deep Sims,
Erenshor PvP, and Erenshor Nemesis are separately loaded BepInEx plugins that find each other only
through reflection. Anything here is compiled into each of them independently.

## Rules

- **mscorlib only.** No Unity, BepInEx, or mod-specific references. A file here must compile inside
  all three assemblies.
- **C# 5.** The shipped builds use the .NET Framework `csc`, so no string interpolation, `nameof`,
  null-conditional operators, expression-bodied members, or out-variable declarations.
- **Optional at build time.** Each `BUILD_AND_INSTALL.ps1` includes `shared/*.cs` when the directory
  exists and defines `SHARED_CONTRACTS`. A standalone copy of one mod still builds without it, so
  every call site must sit behind `#if SHARED_CONTRACTS`.
- **Namespace `ErenshorSharedContracts`,** internal types only. Nothing here is part of any mod's
  public surface.

## PvpContractConformance.cs

Pins all three mods to one outcome-classification table.

PvP owns classification (`ErenshorPvpApi.ClassifyOutcome`), but Nemesis and Deep Sims each carry a
local mirror for pre-v2 PvP builds. Those mirrors are unreachable in a normal install, which is
exactly what makes them prone to silent drift. This file gives every implementation the same table
and the same row-shape checks, so a mirror that falls out of step fails that mod's self-test:

| Mod | Test entry point | Command |
| --- | --- | --- |
| Erenshor PvP | `ErenshorPvpApi.RunSelfTests` | `/epvp selftest` |
| Erenshor Nemesis | `NemesisDirector.SelfTest` | `/enemesis selftest` |
| Deep Sims | `PvpEventBridge.RunSelfTests` | `/dsguardtest` |

Nemesis runs it twice: once against its local mirror, once against the live path that reflects into
PvP when installed.

**Adding a new termination reason:** add it to `Cases` here first, then implement it in
`ErenshorPvpApi.ClassifyOutcome` and both mirrors. Anything not listed must classify as `invalid`,
so an unrecognised failure is never mistaken for a real fight result.
