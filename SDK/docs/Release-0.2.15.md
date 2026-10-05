# Inu SDK 0.2.15

- Split USB HID into a generic transport plus independently owned keyboard and mouse protocol components.
- Added `UsbHidKeyboardServices` and `UsbHidMouseServices` dependency registries so the transport never names the concrete decoder implementations.
- Promoted USB HID keyboard, USB HID mouse, and USB mass-storage transport to language-neutral selectable component definitions.
- Added default selection symbols `INU_COMPONENT_USB_HID_KEYBOARD`, `INU_COMPONENT_USB_HID_MOUSE`, and `INU_COMPONENT_USB_MASS_STORAGE`.
- Bootstrap and generated HAL startup now initialize only the selected HID protocol components and mass-storage transport.
- Corrected Kath project generation and Visual Studio configuration generation so existing selectable component defaults are emitted consistently from selected work areas.
- Existing `UsbHid` facade APIs remain stable; when a decoder is omitted, its handler/translation service is unavailable while generic HID transport remains valid.
