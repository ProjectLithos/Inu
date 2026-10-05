using Inu.Architecture;
using Inu.Architecture.X64.Descriptors;
using Inu.Architecture.X64.Interrupts;
using Inu.InterruptControllers;
using Inu.InterruptControllers.X64;
using Inu.Interrupts;
using Inu.Primitives;

namespace Inu.Kernel.Sample;

/// <summary>Initialises and demonstrates the public x64 descriptor, interrupt, and controller facilities.</summary>
internal static unsafe class PlatformInitialization
{
    private const int EmergencyStackSize = 4096;

    internal static bool Initialize(IExceptionDiagnosticSink diagnostics)
    {
        byte* gdtStorage = stackalloc byte[64];
        byte* tssStorage = stackalloc byte[104];
        byte* idtStorage = stackalloc byte[4096];
        byte* ringZeroStack = stackalloc byte[EmergencyStackSize];
        byte* doubleFaultStack = stackalloc byte[EmergencyStackSize];
        byte* nmiStack = stackalloc byte[EmergencyStackSize];
        byte* machineCheckStack = stackalloc byte[EmergencyStackSize];

        ProcessorId processor = new(0);
        SegmentSelector kernelCode = SegmentSelector.Create(1, DescriptorPrivilegeLevel.Kernel);
        SegmentSelector kernelData = SegmentSelector.Create(2, DescriptorPrivilegeLevel.Kernel);
        SegmentSelector userData = SegmentSelector.Create(3, DescriptorPrivilegeLevel.User);
        SegmentSelector userCode = SegmentSelector.Create(4, DescriptorPrivilegeLevel.User);
        SegmentSelector taskState = SegmentSelector.Create(5, DescriptorPrivilegeLevel.Kernel);

        X64TaskStateSegment tss = new();
        TaskStateSegmentConfiguration tssConfiguration = new(
            processor,
            ToAddress(tssStorage),
            104,
            ToAddress(ringZeroStack + EmergencyStackSize),
            ToAddress(doubleFaultStack + EmergencyStackSize),
            ToAddress(nmiStack + EmergencyStackSize),
            ToAddress(machineCheckStack + EmergencyStackSize),
            IoPermissionBitmapPolicy.Disabled,
            default,
            0);
        if (!tss.Configure(tssConfiguration)) return false;

        X64GlobalDescriptorTable gdt = new();
        GlobalDescriptorTableConfiguration gdtConfiguration = new(
            processor,
            ToAddress(gdtStorage),
            64,
            kernelCode,
            kernelData,
            userCode,
            userData,
            taskState);
        if (!gdt.Configure(gdtConfiguration, tss)) return false;
        if (!gdt.Install()) return false;
        if (!tss.Install(taskState)) return false;

        X64InterruptDescriptorTable idt = new();
        InterruptDescriptorTableConfiguration idtConfiguration = new(
            processor,
            ToAddress(idtStorage),
            4096,
            kernelCode,
            1,
            2,
            3);
        if (!idt.Configure(idtConfiguration)) return false;
        EssentialExceptionHandlers exceptions = new(idt, diagnostics);
        if (!exceptions.RegisterAll()) return false;
        if (!idt.Install()) return false;

        return DemonstrateInterruptControllerContracts();
    }

    private static bool DemonstrateInterruptControllerContracts()
    {
        X64InterruptVectorAllocator vectors = new();
        X64InterruptController controller = new(vectors);
        InterruptControllerCapabilities capabilities = controller.GetCapabilities();
        if (!capabilities.Msi || !capabilities.MsiX) return false;

        byte vector = controller.AllocateVector();
        if (vector == 0) return false;
        InterruptRouteConfiguration configuration = new(
            new InterruptSource(0),
            vector,
            InterruptPolarity.Conforms,
            InterruptTriggerMode.Edge,
            new InterruptAffinity(new ProcessorId(0)),
            8,
            true,
            InterruptDeliveryMechanism.Msi);
        InterruptRouteResult route = controller.Route(configuration);
        if (!route.Succeeded) return false;
        if (controller.CreateMessage(route.Handle).Vector != vector) return false;
        if (!controller.SetAffinity(route.Handle, new InterruptAffinity(new ProcessorId(0)))) return false;
        if (!controller.SetPriority(route.Handle, 8)) return false;
        if (!controller.Unmask(route.Handle)) return false;
        if (!controller.Mask(route.Handle)) return false;
        if (!controller.RemoveRoute(route.Handle)) return false;
        return controller.ReleaseVector(vector);
    }

    private static Address ToAddress(byte* pointer) => new((ulong)(nuint)pointer);
}
