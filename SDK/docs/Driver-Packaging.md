# Driver packaging

Inu driver packages use `Inu.Driver.json` schema version 3 as the authoritative package manifest. Every distributable kernel or userland driver identifies itself with a stable package ID and records its driver kind, package version, target architecture, minimum Inu SDK/runtime version, SDK API version, Driver ABI version, supported device IDs, dependencies, requested capabilities/permissions, and signing metadata.

## Required manifest fields

- `id` is the stable package identifier and is separate from the display `name`.
- `ids` records hardware/device identifiers understood by the driver. Bus-specific convenience fields may also be present.
- `architecture` is `any`, `x64`, or `arm64`.
- `minimumInuVersion`, `sdkApiVersion`, and `driverAbiVersion` make compatibility explicit before a driver is accepted.
- `dependencies` lists driver/package dependencies.
- `capabilities` declares what the driver needs; `permissions` is the package policy grant ceiling. A capability must also be present in permissions.
- `signing` records `unsigned`, `development`, `signed`, `trusted`, or `revoked` state plus algorithm, signer ID, and digest when signatures are used. Trust-chain enforcement can be tightened later without changing the package format.

## Validation and packaging

Run `Validate-InuDriverPackage.bat <path-to-Inu.Driver.json>` to validate a manifest. Optional target architecture, Inu version, SDK API version, and Driver ABI version arguments can enforce compatibility. Revoked packages are rejected. Signed/trusted packages must carry signing metadata.

Run `Pack-InuDriver.bat -ProjectDirectory <driver-project>` after building the driver. The command validates the manifest and creates a `.nodrv` archive containing the manifest and build payload beneath `Artifacts\Drivers`.

Kath's Driver Development Centre creates schema-v3 manifests automatically. Visual Studio kernel-driver and userland-driver templates now include the same manifest from project creation, so a new driver is package-aware from its first build.
