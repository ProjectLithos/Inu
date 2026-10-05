# Inu package-manager format

Inu 0.20.0 standardises installable software as an ordinary **ZIP archive**. The package format is defined by the archive contents and `Inu.Package.json`, not by inventing a proprietary archive extension.

## Container

A Inu package keeps the normal `.zip` extension. A ZIP is installable only when it contains a valid `Inu.Package.json` at the archive root. Arbitrary ZIP files remain ordinary archives.

```text
Calculator-1.0.0.zip
├── Inu.Package.json
└── payload/
    └── Calculator.exe
```

The package verifier rejects absolute paths, `..` path traversal, duplicate normalized entries, undeclared payload files, missing payload files, length mismatches, and SHA-256 mismatches.

## Package classes

One manifest schema covers five package classes:

- `Application` — contains a Inu `.exe` application package.
- `Driver` — contains a `.nodrv` driver artifact produced by the existing driver packer.
- `Library` — contains one or more `.dll` or `.lib` files.
- `Service` — contains a Inu `.exe` and service registration metadata.
- `KernelExtension` — privileged package class; the 0.20.0 policy requires its package signing state to be `signed` or `trusted`.

This keeps driver packaging and the 0.19.x executable format independent while allowing both to be distributed through one package manager.

Applications continue to use `.exe` packages containing `.nexe` native images; libraries continue to use `.dll` and `.lib`.

## Required root manifest

The manifest format identifier is `inu-package-v1`, schema version 1. It records package identity, semantic version, package class, supported architectures, minimum Inu/SDK ABI requirements, dependencies, requested capabilities, payload hashes, installation metadata, and signing metadata.

Example:

```json
{
  "format": "inu-package-v1",
  "schemaVersion": 1,
  "id": "org.example.calculator",
  "name": "Calculator",
  "version": "1.0.0",
  "type": "Application",
  "publisher": "Example",
  "architectures": ["x64"],
  "requires": {
    "minimumInuVersion": "0.1.0",
    "sdkApiVersion": "1.0",
    "abiVersion": "1.0"
  },
  "dependencies": [],
  "capabilities": ["graphics.window", "input.keyboard"],
  "files": [],
  "install": { "entry": "payload/Calculator.exe" },
  "signing": { "state": "unsigned" }
}
```

`Inu.PackagePacker` regenerates the `files` table from the payload directory when building the final ZIP, so file length and SHA-256 data are authoritative for the produced package.

## Dependencies

Dependencies identify another package and a version constraint. The 0.20.0 manager supports `*`, exact versions, and space-separated comparison constraints such as `>=1.2.0 <2.0.0`. Required dependencies must already be installed before a package can commit. Optional dependencies do not block installation.

Before uninstalling a package, the package database is checked for reverse required dependencies.

## Capabilities

Capabilities are declarations, not grants. The package manifest describes what the installed payload may request. The Inu security/capability authority remains responsible for granting actual handles and rights at runtime.

## Transactional installation

`Inu.PackageManager install` performs:

1. ZIP structure and manifest validation.
2. file hash verification.
3. package-class policy validation.
4. dependency resolution against the installed package database.
5. extraction into a transaction staging directory.
6. an atomic directory move into the installed-package store on the same volume.
7. an atomic package-database update.

If staging or database commit fails, the staged install is removed rather than leaving a partial package behind.

Installed state is recorded under:

```text
/System/Packages/
├── database.json
├── installed/
└── transactions/
```

The package database is authoritative for installed package identity, version, type, dependency ownership, and install path.

## Tools

```text
Pack-InuPackage.bat <Inu.Package.json> <payload-directory> <output.zip>

Inu-Package.bat verify <package.zip>
Inu-Package.bat inspect <package.zip>
Inu-Package.bat install <package.zip> [--root <system-root>]
Inu-Package.bat uninstall <package-id> [--root <system-root>]
Inu-Package.bat list [--root <system-root>]
```

The tools deliberately keep `.zip` visible to the user. Inu package identity comes from the root manifest and verification rules, not from the filename extension.
