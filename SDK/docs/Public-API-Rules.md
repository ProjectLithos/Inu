# Inu public API rules

Inu public SDK APIs are a compatibility contract.

Every public type and member in an assembly listed under `publicAssemblies` in `docs/Inu.Documentation.json` must provide:

- an XML `<summary>` that states what the item does;
- a `<nova.when>` section explaining when the SDK user should use it;
- dependency information through `<nova.depends>` or project references;
- `<returns>` for every value-returning public method;
- an `<example>` for public methods when strict example validation is enabled.

Public methods and procedures must return `bool` or a value. Public `void` methods are rejected by source-policy tests.

`Build-InuDocumentation.bat` always writes `Artifacts/Documentation/PublicApiAudit.json`. Run the strict audit with:

```bat
Build-InuDocumentation.bat -Strict
```

Strict mode exits with failure when any required documentation field is missing. Generated HTML and audit files belong beneath `Artifacts` and must never be committed.
