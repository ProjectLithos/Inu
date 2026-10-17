using System;
using Inu.Kernel.Console;
#if INU_KERNELAREA_USB
using Inu.Bus.Usb;
using Inu.Usb.Xhci;
using Inu.Usb.Hid;
using Inu.Usb.MassStorage;
using Inu.Usb.Hub;
#endif

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Starts only USB host, hub, HID and storage capabilities selected by the OS architecture.</summary>
public static class UsbHardwareStartup
{
    public static Boolean Initialize()
    {
#if INU_KERNELAREA_USB
        Boolean usbKeyboardSelected = HasConfiguredInput("usb-hid-keyboard");
        Boolean usbMouseSelected = HasConfiguredInput("usb-hid-mouse");
        Boolean xhciSelected = HasConfiguredDriver("usb-xhci") || usbKeyboardSelected || usbMouseSelected;
        if (!xhciSelected) return true;
        if (!KernelXhci.Initialize()) return false;
        XhciCapabilities xhciCapabilities = KernelXhci.GetCapabilities();
        if (!KernelStructuredLogging.InfoLine("usb-detail","UsbHardwareStartup.Initialize",xhciCapabilities.InterruptConfiguredControllers == xhciCapabilities.RunningControllers ? "xHCI runtime interrupt routes are configured; bootstrap enumeration remains synchronous until runtime interrupt delivery is enabled." : "xHCI explicit polling fallback is active for at least one controller.")) return false;

        // Register every selected USB class/protocol driver before the host controller
        // enumerates root ports. Dynamic USB interfaces are bound as they are created;
        // enumeration must therefore never run ahead of the drivers that own them.
        if (!UsbHub.Initialize()) return false;
        if (!UsbHid.Initialize()) return false;
#if INU_KERNELAREA_INPUT && INU_COMPONENT_USB_HID_KEYBOARD
        if (usbKeyboardSelected && !UsbHidKeyboard.Initialize()) return false;
#endif
#if INU_KERNELAREA_INPUT && INU_COMPONENT_USB_HID_MOUSE
        if (usbMouseSelected && !UsbHidMouse.Initialize()) return false;
#endif
#if INU_COMPONENT_USB_MASS_STORAGE
        if (!UsbMassStorage.Initialize()) return false;
#endif
        if (!KernelXhci.ScanRootPorts()) return false;
        if (!UsbHub.EnumerateDownstream()) return false;
        if (!KernelStructuredLogging.InfoLine("usb","UsbHardwareStartup.Initialize","Selected kernel-domain USB services online.")) return false;
#endif
        return true;
    }

    /// <summary>Enables runtime xHCI interrupt delivery after the kernel interrupt dispatcher has enabled maskable interrupts.</summary>
    public static Boolean EnableHardwareInterrupts()
    {
#if INU_KERNELAREA_USB
        if (!KernelXhci.EnableInterruptDelivery()) return false;
        XhciCapabilities xhci = KernelXhci.GetCapabilities();
        if (!KernelStructuredLogging.InfoLine("usb-detail","UsbHardwareStartup.EnableHardwareInterrupts",xhci.PollingFallbackControllers == 0U ? "xHCI broker-managed hardware interrupt delivery active." : "xHCI explicit polling fallback remains active.")) return false;
#endif
        return true;
    }
#if INU_KERNELAREA_USB
    private static Boolean HasConfiguredInput(String value)
    {
        String[] configured = Inu.Kernel.Bootstrap.GeneratedConfiguration.Input();
        for (Int32 i = 0; i < configured.Length; i++) if (configured[i] == value) return true;
        return false;
    }

    private static Boolean HasConfiguredDriver(String value)
    {
        String[] configured = Inu.Kernel.Bootstrap.GeneratedConfiguration.Drivers();
        for (Int32 i = 0; i < configured.Length; i++) if (configured[i] == value) return true;
        return false;
    }
#endif

}
