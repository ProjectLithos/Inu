# Inu command-line SDK

The canonical `inu` executable is the sole authority for user-facing SDK commands.

## Commands

- `inu build [--root <path>] [--force-rebuild]` — build Inu and Kath.
- `inu toolchain ensure [--root <path>]` — verify the private Inu toolchain required by the build.
- `inu version` — print the Inu executable version. `inu --version` and `inu -v` are aliases.
- `inu help` — print this command surface. `inu --help` and `inu -h` are aliases.

No other command is part of the supported Inu command API. Historical commands such as `new`, `run`, `debug`, `test`, `pack`, and `doctor` must not be documented unless they are implemented by the canonical executable.

## API rule

Documentation must be generated from the actual supported surface. C# `public` visibility is not, by itself, an SDK API declaration.
