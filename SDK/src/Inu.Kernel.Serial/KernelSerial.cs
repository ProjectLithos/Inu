using System;
using Inu.Kernel.Console;
using Inu.Kernel.Drivers;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Pci;
using Inu.Kernel.Virtio;

namespace Inu.Kernel.Serial;

/// <summary>Owns early 16550 serial debugging and attaches PCI UART and VirtIO-console mirrors after bus discovery.</summary>
public static unsafe class KernelSerial
{
    private const UInt16 LegacyCom1 = 0x03F8;
    private const UInt32 DefaultBaudRate = 115200U;
    private const UInt32 TransmitPollLimit = 1000000U;
    private static Boolean _initialized;
    private static Boolean _legacy16550;
    private static UInt32 _pciDiscovered;
    private static UInt32 _pciOnline;
    private static UInt32 _virtioConsoles;
    private static Boolean _hasPciMirror;
    private static PciLocation _pciMirrorLocation;
    private static Byte _pciMirrorBar;
    private static PciBarType _pciMirrorBarType;
    private static UInt16 _pciMirrorIoBase;
    private static KernelDeviceHandle _virtioMirror;
    private static UInt64 _transmitFailures;

    /// <summary>Initializes the legacy 16550-compatible COM1 UART for earliest-possible kernel diagnostics.</summary>
    public static Boolean InitializeEarly16550()
    {
        if (!Native.InitializeSerial()) return false;
        _legacy16550 = true;
        return true;
    }

    /// <summary>Discovers PCI UARTs, attaches started VirtIO consoles, and mirrors KernelConsole output to available secondary transports.</summary>
    public static Boolean Initialize()
    {
        if (_initialized) return true;
        if (!_legacy16550 && !InitializeEarly16550()) return false;
        if (!KernelPci.IsInitialized() || !KernelVirtio.IsInitialized()) return false;
        _pciDiscovered = 0U; _pciOnline = 0U; _virtioConsoles = 0U; _hasPciMirror = false; _virtioMirror = default; _transmitFailures = 0UL;
        DiscoverPciUarts();
        DiscoverVirtioConsoles();
        if ((_hasPciMirror || _virtioMirror.Value != 0U) && !KernelConsole.SetSecondarySerialWriter(&WriteSecondaryByte)) return false;
        _initialized = true;
        return true;
    }

    /// <summary>Gets whether the complete serial facility has been initialized.</summary>
    public static Boolean IsInitialized() => _initialized;

    /// <summary>Gets current 16550, PCI-UART, and VirtIO-console capability information.</summary>
    public static KernelSerialCapabilities GetCapabilities() => new(_initialized, _legacy16550, _pciDiscovered, _pciOnline, _virtioConsoles, _hasPciMirror || _virtioMirror.Value != 0U, _transmitFailures);

    /// <summary>Attempts a non-blocking read from the legacy COM1 UART.</summary>
    public static Boolean TryReadLegacyByte(out Byte value)
    {
        value = 0;
        if (!_legacy16550 || !Native.ReadPort8((UInt16)(LegacyCom1 + 5), out Byte status) || (status & 0x01) == 0) return false;
        return Native.ReadPort8(LegacyCom1, out value);
    }

    private static Boolean WriteSecondaryByte(Byte value)
    {
        Boolean attempted = false;
        if (_hasPciMirror)
        {
            attempted = true;
            if (!WritePciByte(value)) _transmitFailures++;
        }
        if (_virtioMirror.Value != 0U)
        {
            attempted = true;
            Byte local = value;
            if (!KernelVirtio.WriteConsole(_virtioMirror, &local, 1U)) _transmitFailures++;
        }
        _ = attempted;
        return true;
    }

    private static Boolean DiscoverPciUarts()
    {
        UInt32 count = KernelPci.GetDeviceCount();
        for (UInt32 index=0U; index<count; index++)
        {
            if (!KernelPci.TryGetDevice(index, out PciDeviceInfo device) || !SerialMath.IsPciSerialController(device.ClassCode)) continue;
            _pciDiscovered++;
            if (!SerialMath.Is16550CompatibleProgrammingInterface(device.ClassCode)) continue;
            if (!TryInitializePciUart(device, out Byte barIndex, out PciBarType barType, out UInt16 ioBase)) continue;
            _pciOnline++;
            if (_hasPciMirror) continue;
            _hasPciMirror = true; _pciMirrorLocation = device.Location; _pciMirrorBar = barIndex; _pciMirrorBarType = barType; _pciMirrorIoBase = ioBase;
        }
        return true;
    }

    private static Boolean TryInitializePciUart(PciDeviceInfo device, out Byte selectedBar, out PciBarType selectedType, out UInt16 ioBase)
    {
        selectedBar=0; selectedType=PciBarType.None; ioBase=0;
        for (Byte barIndex=0; barIndex<6; barIndex++)
        {
            if (!KernelPci.TryGetBar(device.Location, barIndex, out PciBarInfo bar) || bar.Length < 8UL) continue;
            if (bar.Type == PciBarType.Io)
            {
                if (bar.Address == 0UL || bar.Address > UInt16.MaxValue) continue;
                if (!EnablePciDecode(device.Location, true, false)) return false;
                UInt16 basePort=(UInt16)bar.Address;
                if (!ConfigureUart(device.Location, barIndex, PciBarType.Io, basePort)) return false;
                selectedBar=barIndex; selectedType=PciBarType.Io; ioBase=basePort; return true;
            }
            if (bar.Type == PciBarType.Memory32 || bar.Type == PciBarType.Memory64)
            {
                if (!EnablePciDecode(device.Location, false, true)) return false;
                if (!ConfigureUart(device.Location, barIndex, bar.Type, 0)) return false;
                selectedBar=barIndex; selectedType=bar.Type; return true;
            }
        }
        return false;
    }

    private static Boolean ConfigureUart(PciLocation location, Byte barIndex, PciBarType barType, UInt16 ioBase)
    {
        if (!SerialMath.TryCalculateDivisor(DefaultBaudRate, out UInt16 divisor)) return false;
        if (!WriteRegister(location,barIndex,barType,ioBase,1,0x00)) return false;
        if (!WriteRegister(location,barIndex,barType,ioBase,3,0x80)) return false;
        if (!WriteRegister(location,barIndex,barType,ioBase,0,(Byte)(divisor & 0xFF))) return false;
        if (!WriteRegister(location,barIndex,barType,ioBase,1,(Byte)(divisor >> 8))) return false;
        if (!WriteRegister(location,barIndex,barType,ioBase,3,0x03)) return false;
        if (!WriteRegister(location,barIndex,barType,ioBase,2,0xC7)) return false;
        return WriteRegister(location,barIndex,barType,ioBase,4,0x0B);
    }

    private static Boolean DiscoverVirtioConsoles()
    {
        UInt32 count=KernelVirtio.GetDeviceCount();
        for(UInt32 index=0U;index<count;index++)
        {
            if(!KernelVirtio.TryGetDevice(index,out VirtioDeviceInfo info)||info.Type!=VirtioDeviceType.Console||!info.Started)continue;
            _virtioConsoles++;
            if(_virtioMirror.Value==0U)_virtioMirror=info.Device;
        }
        return true;
    }

    private static Boolean WritePciByte(Byte value)
    {
        for(UInt32 attempt=0U;attempt<TransmitPollLimit;attempt++)
        {
            if(!ReadRegister(_pciMirrorLocation,_pciMirrorBar,_pciMirrorBarType,_pciMirrorIoBase,5,out Byte status))return false;
            if((status&0x20)!=0)return WriteRegister(_pciMirrorLocation,_pciMirrorBar,_pciMirrorBarType,_pciMirrorIoBase,0,value);
        }
        return false;
    }

    private static Boolean EnablePciDecode(PciLocation location, Boolean io, Boolean memory)
    {
        if(!KernelPci.TryRead16(location,0x04,out UInt16 command))return false;
        UInt16 next=command;if(io)next=(UInt16)(next|0x0001);if(memory)next=(UInt16)(next|0x0002);
        return next==command||KernelPci.TryWrite16(location,0x04,next);
    }

    private static Boolean WriteRegister(PciLocation location,Byte barIndex,PciBarType barType,UInt16 ioBase,Byte register,Byte value)
    {
        if(barType==PciBarType.Io)return Native.WritePort8((UInt16)(ioBase+register),value);
        if(!KernelPci.TryMapBar(location,barIndex,out _,out UInt64 address))return false;
        *((Byte*)(nuint)(address+register))=value;return true;
    }

    private static Boolean ReadRegister(PciLocation location,Byte barIndex,PciBarType barType,UInt16 ioBase,Byte register,out Byte value)
    {
        value=0;if(barType==PciBarType.Io)return Native.ReadPort8((UInt16)(ioBase+register),out value);
        if(!KernelPci.TryMapBar(location,barIndex,out _,out UInt64 address))return false;
        value=*((Byte*)(nuint)(address+register));return true;
    }
}
