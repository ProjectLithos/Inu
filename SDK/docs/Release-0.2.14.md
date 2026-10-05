# Inu SDK 0.2.14

- Promoted USB enumeration, storage partition discovery, and keyboard scan-code decoding from candidate seams to selectable source components.
- Added dependency-resolved service registries so parent subsystems no longer name the concrete optional implementation.
- Moved keyboard decode/modifier/layout/pressed-key state out of `KernelPs2` into `KernelKeyboardDecoder`; PS/2 remains the hardware transport owner.
- Moved MBR/GPT discovery into `KernelPartitionDiscovery`; storage exposes explicit block-device and volume-registry contracts.
- Moved USB enumeration state-machine logic into `KernelUsbEnumeration`; the USB bus exposes explicit host/device-registry services.
- Added default selection symbols `INU_COMPONENT_USB_ENUMERATION`, `INU_COMPONENT_STORAGE_PARTITION_DISCOVERY`, and `INU_COMPONENT_INPUT_KEYBOARD_DECODER`.
- Existing facade APIs remain stable; omitting a selectable component makes the corresponding operation unavailable rather than introducing a concrete dependency into the parent subsystem.
