# Documentation Authoring

Public SDK declarations use normal XML documentation plus Inu-specific metadata.

```csharp
/// <summary>Halts the current processor.</summary>
/// <remarks>This operation normally does not return.</remarks>
/// <returns>False only when the architecture implementation rejects the operation.</returns>
/// <nova.when>Use after final kernel output when no scheduler is active.</nova.when>
/// <nova.depends>The selected architecture CPU implementation.</nova.depends>
/// <example>return CPU.Halt();</example>
public static bool Halt()
```

The generator reads public declarations from `src`, records project-reference dependencies, writes the offline site beneath `Artifacts/Documentation/site`, and writes `Artifacts/Documentation/PublicApiAudit.json`. `Build-InuDocumentation.bat -Strict` fails when a configured public API is missing required summary, usage, dependency, return, or example documentation.
