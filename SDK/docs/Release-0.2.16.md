# Inu / Kath 0.2.16

Provider-boundary decomposition pass.

- Adds canonical selectable definitions for FatFs, firmware framebuffer, and simple framebuffer providers.
- Adds the required graphics display-registry definition.
- Adds clock-core plus HPET and invariant-TSC candidate definitions without falsely declaring the still-coupled clock sources selectable.
- Makes firmware framebuffer registration source-selectable with `INU_COMPONENT_GRAPHICS_FIRMWARE_FRAMEBUFFER`; UEFI defaults preserve prior behaviour.
- Keeps filesystem choice as OS-author policy: FatFs is selectable but is not silently imposed as a generated default.
