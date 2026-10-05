# Public API baseline

Inu 0.0.77 defines the first compatibility baseline for end-user SDK assemblies.

A public API must have a summary, usage guidance, dependency and restriction information, documented return semantics, supported-architecture metadata and earliest boot-stage metadata. Public methods must include a compilable example. Implementation details that do not form part of the end-user contract must remain `internal`.

`Build-InuDocumentation.bat -Strict` validates the documentation contract. The generated `Inu.ApiCompatibility.json` records stable identities and signature hashes for compatibility comparison in subsequent releases.
