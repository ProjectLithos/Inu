# Public API quality

Inu treats its public SDK surface as a versioned compatibility contract.

## Audit report

Every documentation build creates `Artifacts/Documentation/PublicApiAudit.json`. The report records the public assemblies and public items discovered from the source tree, the number with complete usage documentation, and every missing field.

## Required information

Each public item must explain what it does, when to use it, what it depends upon and how it is used. Value-returning methods must document their return value. Method examples become mandatory when strict example validation is enabled.

## Strict validation

Run `Build-InuDocumentation.bat -Strict` before publishing an SDK release. Strict mode turns every audit finding into a build failure, preventing undocumented public API additions from entering a release.
