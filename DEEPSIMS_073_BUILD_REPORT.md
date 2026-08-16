# Deep Sims 0.7.3 build report

## Scope

- Module: `DeepSim-erenshor` only.
- Version: `0.7.3`.
- Repair: social retrieval routing, manual banter fallback, visible-thread continuation source, and witnessed session seeds.

## Verification

- `tests/RUN_DETERMINISTIC_TESTS.ps1`: PASS, 222 PASS / 0 FAIL.
- Included new deterministic coverage for Windblade opinion routing, direct personal preference no-retrieval, late-join witness denial, and witnessed-seed eligibility.

## Build inputs

- Installed game assembly is used by the module build script.
- Lunaris developer references are supplied from the configured local references path.
- `Assembly-CSharp.dll` SHA-256: `b840cb8076ed0553f7dc3beb4042aba653917882f763181ec0d2c13c26c17847`.
- Installed `ErenshorDeepSims.dll` SHA-256: `3b269d4855f89781497c1046c4324f19b2b02bca4acca6477f7aecc900578d3f`.
- Install target: `D:\SteamLibrary\steamapps\common\Erenshor\plugins\ErenshorDeepSims.dll`.

## Pending live acceptance

Run the supplied live checklist after installation. The acceptance criterion is a party exchange that is observably connected, not only passing routing tests.
