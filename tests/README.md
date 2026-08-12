# Deterministic Deep Sims regression suite

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\RUN_DETERMINISTIC_TESTS.ps1
```

The runner compiles the grounding, memory, telemetry, prompt, and sanitization paths into a temporary executable with small test-only framework/Unity stubs. It does not start Erenshor, contact Ollama, install a DLL, or retain test files. A non-zero exit code means at least one named regression failed.

Inside Erenshor, `/dsguardtest` also runs this deterministic suite in addition to its existing guard/lifecycle checks. The in-game command prints one concise PASS/FAIL line per test plus a summary.
