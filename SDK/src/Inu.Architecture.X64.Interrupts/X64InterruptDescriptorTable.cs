using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Inu.Interrupts;

namespace Inu.Architecture.X64.Interrupts;

/// <summary>Builds and dispatches a complete processor-local x64 IDT.</summary>
public sealed unsafe class X64InterruptDescriptorTable : IInterruptDescriptorTable
{
    private const uint EntryCount = 256;
    private const uint EntrySize = 16;
    private const uint RequiredBytes = EntryCount * EntrySize;
    private static X64InterruptDescriptorTable? installed;
    private readonly InterruptHandler?[] handlers = new InterruptHandler?[EntryCount];
    private readonly InterruptRegistrationOptions[] options = new InterruptRegistrationOptions[EntryCount];
    private readonly ulong[] generations = new ulong[EntryCount];
    private InterruptDescriptorTableConfiguration configuration;
    private bool configured;

    /// <inheritdoc />
    public bool Configure(InterruptDescriptorTableConfiguration value)
    {
        if (value.TableAddress.Value == 0 || value.TableCapacity < RequiredBytes || value.KernelCodeSelector.Value == 0) return false;
        if (!IsIst(value.DoubleFaultInterruptStack) || !IsIst(value.NonMaskableInterruptStack) || !IsIst(value.MachineCheckInterruptStack)) return false;
        configuration = value;
        for (int vector = 0; vector < EntryCount; vector++)
        {
            byte ist = GetDefaultIst((byte)vector, value);
            WriteGate((byte)vector, NativeMethods.GetInterruptStub((byte)vector), value.KernelCodeSelector.Value,
                InterruptGateType.Interrupt, DescriptorPrivilegeLevel.Kernel, ist);
            options[vector] = new InterruptRegistrationOptions(DescriptorPrivilegeLevel.Kernel, InterruptGateType.Interrupt, ist, false);
        }
        configured = true;
        return true;
    }

    /// <inheritdoc />
    public bool Install()
    {
        if (!configured) return false;
        installed = this;
        delegate* unmanaged[Cdecl]<InterruptContext*, int> dispatcher = &DispatchNative;
        if (!NativeMethods.SetInterruptDispatcher((ulong)dispatcher)) return false;
        return NativeMethods.LoadInterruptDescriptorTable(configuration.TableAddress.Value, (ushort)(RequiredBytes - 1));
    }

    /// <inheritdoc />
    public InterruptRegistrationResult Register(byte vector, InterruptHandler handler, InterruptRegistrationOptions value)
    {
        if (!configured) return Failed("The IDT is not configured.");
        if (handler is null) return Failed("The handler is required.");
        if (!IsIst(value.InterruptStackTable)) return Failed("IST must be between zero and seven.");
        if (handlers[vector] is not null && !value.ReplaceExisting) return Failed("The vector already has a handler.");
        generations[vector]++;
        handlers[vector] = handler;
        options[vector] = value;
        WriteGate(vector, NativeMethods.GetInterruptStub(vector), configuration.KernelCodeSelector.Value,
            value.GateType, value.PrivilegeLevel, value.InterruptStackTable);
        return new InterruptRegistrationResult(true, new InterruptRegistrationHandle(vector, generations[vector]), string.Empty);
    }

    /// <inheritdoc />
    public bool Remove(InterruptRegistrationHandle handle)
    {
        byte vector = handle.Vector;
        if (handlers[vector] is null || generations[vector] != handle.Generation) return false;
        handlers[vector] = null;
        generations[vector]++;
        byte ist = GetDefaultIst(vector, configuration);
        options[vector] = new InterruptRegistrationOptions(DescriptorPrivilegeLevel.Kernel, InterruptGateType.Interrupt, ist, false);
        WriteGate(vector, NativeMethods.GetInterruptStub(vector), configuration.KernelCodeSelector.Value,
            InterruptGateType.Interrupt, DescriptorPrivilegeLevel.Kernel, ist);
        return true;
    }

    /// <inheritdoc />
    public bool IsRegistered(byte vector) => handlers[vector] is not null;

    /// <inheritdoc />
    public InterruptResult Dispatch(ref InterruptContext context)
    {
        if (context.Vector > byte.MaxValue) return InterruptResult.Fatal;
        InterruptHandler? handler = handlers[(byte)context.Vector];
        if (handler is null) return InterruptResult.Fatal;
        InterruptResult result = handler(ref context);
        return result == InterruptResult.Unhandled && context.Vector < 32 ? InterruptResult.Fatal : result;
    }

    /// <summary>Stops the current processor through the native terminal halt loop.</summary>
    public bool StopProcessor() => NativeMethods.StopProcessor();

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int DispatchNative(InterruptContext* context)
    {
        if (installed is null || context is null) return (int)InterruptResult.Fatal;
        return (int)installed.Dispatch(ref *context);
    }

    private static bool IsIst(byte value) => value <= 7;

    private static byte GetDefaultIst(byte vector, InterruptDescriptorTableConfiguration value) => vector switch
    {
        (byte)CpuExceptionVector.DoubleFault => value.DoubleFaultInterruptStack,
        (byte)CpuExceptionVector.NonMaskableInterrupt => value.NonMaskableInterruptStack,
        (byte)CpuExceptionVector.MachineCheck => value.MachineCheckInterruptStack,
        _ => 0
    };

    private static InterruptRegistrationResult Failed(string error) =>
        new(false, new InterruptRegistrationHandle(0, 0), error);

    private void WriteGate(byte vector, ulong target, ushort selector, InterruptGateType gateType,
        DescriptorPrivilegeLevel privilegeLevel, byte ist)
    {
        byte* entry = (byte*)configuration.TableAddress.Value + (vector * EntrySize);
        *(ushort*)(entry + 0) = (ushort)target;
        *(ushort*)(entry + 2) = selector;
        entry[4] = (byte)(ist & 0x7);
        entry[5] = (byte)(0x80 | (((byte)privilegeLevel & 0x3) << 5) | ((byte)gateType & 0xF));
        *(ushort*)(entry + 6) = (ushort)(target >> 16);
        *(uint*)(entry + 8) = (uint)(target >> 32);
        *(uint*)(entry + 12) = 0;
        NativeMethods.SetInterruptStackSwitch(vector, ist != 0);
    }
}
