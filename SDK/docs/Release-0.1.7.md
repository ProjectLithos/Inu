# Kath & Inu 0.1.7

- Fixes the clean FullSource bootstrap so an already-extracted tree cannot keep a stale root `TODO.md` merely because its `VERSION` already matches the available FullSource ZIP.
- When a matching same-version FullSource ZIP is available, `Build.ps1` compares its root `TODO.md` with the installed root copy and restores the ZIP copy when they differ.
- Keeps `TODO.md` as the single authoritative backlog; no second roadmap copy is introduced.
