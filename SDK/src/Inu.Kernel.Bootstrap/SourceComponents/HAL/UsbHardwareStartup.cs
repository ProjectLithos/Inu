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
        if (!KernelXhci.Initialize()) return false;
        if (!KernelXhci.ScanRootPorts()) return false;
        if (!UsbHub.Initialize()) return false;
        if (!UsbHub.EnumerateDownstream()) return false;
        if (!UsbHid.Initialize()) return false;
#if INU_KERNELAREA_INPUT && INU_COMPONENT_USB_HID_KEYBOARD
        if (!UsbHidKeyboard.Initialize()) return false;
#endif
#if INU_KERNELAREA_INPUT && INU_COMPONENT_USB_HID_MOUSE
        if (!UsbHidMouse.Initialize()) return false;
#endif
#if INU_COMPONENT_USB_MASS_STORAGE
        if (!UsbMassStorage.Initialize()) return false;
#endif
        if (!KernelStructuredLogging.InfoLine("usb","UsbHardwareStartup.Initialize","Selected kernel-domain USB services online.")) return false;
#endif
        return true;
    }
}
