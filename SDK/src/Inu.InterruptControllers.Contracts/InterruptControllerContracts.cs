using Inu.Interrupts;
using Inu.Primitives;

namespace Inu.InterruptControllers;

/// <summary>Identifies the delivery technology hidden behind a routed interrupt.</summary>
public enum InterruptDeliveryMechanism : byte
{
    /// <summary>Legacy dual 8259 programmable interrupt controllers.</summary>
    LegacyPic,
    /// <summary>I/O APIC redirection-table delivery.</summary>
    IoApic,
    /// <summary>PCI message-signalled interrupt delivery.</summary>
    Msi,
    /// <summary>PCI-X/PCIe MSI-X table delivery.</summary>
    MsiX,
    /// <summary>Processor-local APIC delivery.</summary>
    LocalApic,
    /// <summary>Extended x2APIC MSR-based delivery.</summary>
    X2Apic
}
/// <summary>Defines the electrical polarity of an interrupt source.</summary>
public enum InterruptPolarity : byte
{
    /// <summary>Uses the polarity defined by the interrupt bus or firmware.</summary>
    Conforms,
    /// <summary>The interrupt is asserted at a high electrical level.</summary>
    ActiveHigh,
    /// <summary>The interrupt is asserted at a low electrical level.</summary>
    ActiveLow
}
/// <summary>Defines whether an interrupt is edge or level triggered.</summary>
public enum InterruptTriggerMode : byte
{
    /// <summary>Uses the trigger mode defined by the interrupt bus or firmware.</summary>
    Conforms,
    /// <summary>The interrupt is triggered by an edge transition.</summary>
    Edge,
    /// <summary>The interrupt remains asserted at a level until serviced.</summary>
    Level
}
/// <summary>Defines the Local APIC delivery mode.</summary>
public enum InterruptDeliveryMode : byte
{
    /// <summary>Delivers the vector directly to the selected processor.</summary>
    Fixed,
    /// <summary>Delivers the vector to the lowest-priority selected processor.</summary>
    LowestPriority,
    /// <summary>Delivers a non-maskable interrupt.</summary>
    NonMaskable,
    /// <summary>Delivers an INIT interprocessor interrupt.</summary>
    Init,
    /// <summary>Delivers a startup interprocessor interrupt.</summary>
    Startup
}
/// <summary>Represents an architecture-independent hardware interrupt source.</summary>
public readonly record struct InterruptSource(uint Value);
/// <summary>Represents an opaque route handle used by drivers.</summary>
public readonly record struct InterruptRouteHandle(ulong Value);
/// <summary>Describes one processor-affinity target.</summary>
public readonly record struct InterruptAffinity(ProcessorId ProcessorId);
/// <summary>Describes routing policy without exposing PIC, APIC, MSI, or MSI-X details.</summary>
public readonly record struct InterruptRouteConfiguration(
    InterruptSource Source,
    byte Vector,
    InterruptPolarity Polarity,
    InterruptTriggerMode TriggerMode,
    InterruptAffinity Affinity,
    byte Priority,
    bool InitiallyMasked,
    InterruptDeliveryMechanism PreferredMechanism);
/// <summary>Reports a route operation.</summary>
public readonly record struct InterruptRouteResult(bool Succeeded, InterruptRouteHandle Handle, string Error);
/// <summary>Describes one interprocessor interrupt.</summary>
public readonly record struct InterprocessorInterrupt(
    byte Vector,
    InterruptAffinity Target,
    InterruptDeliveryMode DeliveryMode,
    bool AssertLevel);
/// <summary>Provides a transport-neutral MSI/MSI-X message.</summary>
public readonly record struct MessageSignalledInterrupt(ulong Address, uint Data, byte Vector);
/// <summary>Provides controller capabilities discovered for the current machine.</summary>
public readonly record struct InterruptControllerCapabilities(
    bool LegacyPic,
    bool LocalApic,
    bool IoApic,
    bool Msi,
    bool MsiX,
    bool X2Apic,
    uint MaximumRoutes,
    byte MinimumPriority,
    byte MaximumPriority);

/// <summary>Controls interrupt delivery while keeping routing technology private from drivers.</summary>
public interface IInterruptController
{
    /// <summary>Gets the capabilities of the active controller stack.</summary>
    InterruptControllerCapabilities GetCapabilities();
    /// <summary>Allocates a vector suitable for a device route.</summary>
    byte AllocateVector();
    /// <summary>Releases a vector after every route using it has been removed.</summary>
    bool ReleaseVector(byte vector);
    /// <summary>Creates or replaces one hardware interrupt route.</summary>
    InterruptRouteResult Route(InterruptRouteConfiguration configuration);
    /// <summary>Removes a route.</summary>
    bool RemoveRoute(InterruptRouteHandle handle);
    /// <summary>Masks a routed interrupt.</summary>
    bool Mask(InterruptRouteHandle handle);
    /// <summary>Unmasks a routed interrupt.</summary>
    bool Unmask(InterruptRouteHandle handle);
    /// <summary>Changes the processor affinity of a route.</summary>
    bool SetAffinity(InterruptRouteHandle handle, InterruptAffinity affinity);
    /// <summary>Changes the logical priority of a route.</summary>
    bool SetPriority(InterruptRouteHandle handle, byte priority);
    /// <summary>Signals end-of-interrupt for a delivered vector.</summary>
    bool EndOfInterrupt(byte vector);
    /// <summary>Sends an interprocessor interrupt.</summary>
    bool SendInterprocessorInterrupt(InterprocessorInterrupt interrupt);
    /// <summary>Builds an MSI or MSI-X message for a route.</summary>
    MessageSignalledInterrupt CreateMessage(InterruptRouteHandle handle);
}

/// <summary>Provides early legacy PIC masking and disablement.</summary>
public interface ILegacyPic
{
    /// <summary>Masks every legacy IRQ line.</summary>
    bool MaskAll();
    /// <summary>Disables legacy PIC delivery after APIC takeover.</summary>
    bool Disable();
    /// <summary>Sends a legacy end-of-interrupt.</summary>
    bool EndOfInterrupt(byte irq);
}
