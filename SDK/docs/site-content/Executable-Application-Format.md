# Inu executable/application format

Inu 0.0.173 stabilises and hardens the canonical application package contract while keeping end-user filenames familiar.

## Default visible extensions

- **`.exe`** — packaged Inu application. This is an Inu package container identified by its `NOAP` magic; the extension alone never makes data executable.
- **`.nexe`** — architecture-specific native executable image stored inside a package. x64 uses the validated PE32+/ELF64 loader; PE32+ is the preferred NativeAOT/LLD payload.
- **`.dll`** — dynamic/shared userland library.
- **`.lib`** — static/import development library.

An OS may override file associations for presentation, but the package ABI, magic and loader validation are unchanged.

## Package header and canonical layout

The binary package starts with a 192-byte little-endian header (`NOAP`, format 1.0) containing architecture, syscall ABI, ABI major/minor, flags, package size, native-image range, entry-point RVA, dependency/capability/resource tables, a UTF-8 string table, resource-data range, identity, name, version, publisher and minimum-SDK references.

0.0.173 requires a bounded canonical order: header, dependency table, capability table, resource table, string table, native image, then resource data. Package bytes and table counts are bounded, every range is overflow/bounds checked, unknown architecture/personality/flags are rejected, and resource data must terminate at the package boundary.

## Metadata

A package identifies application ID, display name, application version, publisher, architecture (`x86_64`, `arm64`, `riscv64`), ABI major/minor, syscall personality (`inu`, `linux`, `windows-nt`), native image, semantic entry-point RVA, dependencies, requested capabilities and resources. The process runtime preserves hashes of package ID, name and version in its process snapshot for diagnostics without retaining mutable package pointers.

The application version is independent of the syscall ABI version. Native Inu applications use ABI **2.0**. Linux and Windows-NT compatibility personalities use **1.0**. Unsupported major/minor combinations are rejected before process address-space allocation.

## Dependencies

Dependencies are explicit ID + version-constraint records. Runtime loaders/package managers must resolve declared dependencies rather than performing unrestricted directory searches. Until an explicit resolver satisfies them, the kernel loader fails closed with `DependenciesUnavailable` before allocating a process address space.

## Capabilities

The package declares capabilities it **may request**. Declaration is not authority. Inu security policy/broker decides which process-scoped capability handles are actually granted. Until a broker satisfies declared capability requirements, direct kernel loading fails closed with `CapabilitiesUnavailable`; it never turns package text into privilege automatically.

## Resources

Resources are package records with name, offset, length and flags. Counts and flags are validated, and resource byte ranges must reside inside the declared resource-data tail. Mutable application data belongs in the user's application-data area rather than in the installed `.exe`.

## Entry point and security

The package stores an entry-point RVA, never a fixed virtual address. The process loader unwraps the `.nexe`, validates the native executable, verifies the package RVA against the image entry point when supplied, then creates the private user address space. W^X, NX, guard-page, privilege-ring, syscall and user-pointer validation remain authoritative.

## Packaging tool

`Inu.ApplicationPacker` consumes `Inu.Application.json` and emits the canonical `.exe` binary package. The manifest supports identity/version/publisher, architecture, ABI, syscall personality, entry-point RVA, native image, dependency records, requested capability records and resource files.

```json
{
  "id": "com.example.editor",
  "name": "Editor",
  "version": "1.0.0",
  "publisher": "Example",
  "architecture": "x86_64",
  "syscallAbi": "inu",
  "abiMajor": 2,
  "abiMinor": 0,
  "entryPointRva": 4096,
  "nativeImage": "bin/x64/Editor.nexe",
  "dependencies": [{ "id": "Inu.UI", "version": "1.x" }],
  "requiredCapabilities": [{ "name": "graphics.window", "rights": 65 }],
  "resources": [{ "name": "icon", "path": "resources/icon.png", "flags": 1 }]
}
```
