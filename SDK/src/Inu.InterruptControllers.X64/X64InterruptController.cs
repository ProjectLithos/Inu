using Inu.Interrupts;
using Inu.Primitives;

namespace Inu.InterruptControllers.X64;

/// <summary>Coordinates PIC, Local APIC, I/O APIC, MSI, MSI-X, and x2APIC delivery behind one driver contract.</summary>
public sealed class X64InterruptController : IInterruptController
{
    private const uint ApicBaseMsr=0x1B, X2ApicEoiMsr=0x80B, X2ApicIcrMsr=0x830;
    private readonly IInterruptVectorAllocator _vectors;
    private readonly RouteState[] _routes = new RouteState[256];
    private readonly LegacyPic _pic = new();
    private ulong _localApicBase;
    private ulong _ioApicBase;
    private bool _x2Apic;
    private ulong _generation;

    /// <summary>Creates an x64 interrupt-controller stack using the shared IDT vector allocator.</summary>
    public X64InterruptController(IInterruptVectorAllocator vectors) => _vectors=vectors ?? throw new ArgumentNullException(nameof(vectors));
    /// <summary>Configures discovered APIC MMIO addresses and mode.</summary>
    public bool Configure(PhysicalAddress localApicBase, PhysicalAddress ioApicBase, bool x2Apic)
    {
        if (!x2Apic && localApicBase.Value==0) return false;
        _localApicBase=localApicBase.Value; _ioApicBase=ioApicBase.Value; _x2Apic=x2Apic;
        return _pic.Disable() && EnableLocalApic();
    }
    /// <inheritdoc />
    public InterruptControllerCapabilities GetCapabilities() => new(true,true,_ioApicBase!=0,true,true,_x2Apic,224,0,15);
    /// <inheritdoc />
    public byte AllocateVector() => _vectors.Allocate();
    /// <inheritdoc />
    public bool ReleaseVector(byte vector) => _vectors.Release(vector);
    /// <inheritdoc />
    public InterruptRouteResult Route(InterruptRouteConfiguration c)
    {
        if (c.Vector<32 || c.Priority>15) return new(false,default,"Invalid vector or priority.");
        ulong id=++_generation; var handle=new InterruptRouteHandle(id);
        _routes[c.Vector]=new(true,handle,c);
        bool ok=c.PreferredMechanism switch
        {
            InterruptDeliveryMechanism.IoApic => ProgramIoApic(c),
            InterruptDeliveryMechanism.Msi or InterruptDeliveryMechanism.MsiX => true,
            InterruptDeliveryMechanism.LegacyPic => false,
            _ => true
        };
        return ok ? new(true,handle,string.Empty) : new(false,default,"Requested delivery mechanism is unavailable.");
    }
    /// <inheritdoc />
    public bool RemoveRoute(InterruptRouteHandle handle) => Update(handle, static _=>default);
    /// <inheritdoc />
    public bool Mask(InterruptRouteHandle handle) => Mutate(handle,true,null,null);
    /// <inheritdoc />
    public bool Unmask(InterruptRouteHandle handle) => Mutate(handle,false,null,null);
    /// <inheritdoc />
    public bool SetAffinity(InterruptRouteHandle handle, InterruptAffinity affinity) => Mutate(handle,null,affinity,null);
    /// <inheritdoc />
    public bool SetPriority(InterruptRouteHandle handle, byte priority) => priority<=15 && Mutate(handle,null,null,priority);
    /// <inheritdoc />
    public bool EndOfInterrupt(byte vector)
    {
        if (_x2Apic) return NativeMethods.WriteMsr(X2ApicEoiMsr,0);
        return _localApicBase!=0 && NativeMethods.WriteMmio32(_localApicBase+0xB0,0);
    }
    /// <inheritdoc />
    public bool SendInterprocessorInterrupt(InterprocessorInterrupt interrupt)
    {
        ulong value=((ulong)interrupt.Target.ProcessorId.Value<<32) | ((ulong)interrupt.DeliveryMode<<8) | interrupt.Vector;
        if (interrupt.AssertLevel) value|=1UL<<14;
        if (_x2Apic) return NativeMethods.WriteMsr(X2ApicIcrMsr,value);
        return _localApicBase!=0 && NativeMethods.WriteMmio32(_localApicBase+0x310,(uint)(value>>32)) && NativeMethods.WriteMmio32(_localApicBase+0x300,(uint)value);
    }
    /// <inheritdoc />
    public MessageSignalledInterrupt CreateMessage(InterruptRouteHandle handle)
    {
        RouteState s=Find(handle); if (!s.Active) return default;
        ulong address=0xFEE00000UL | ((ulong)s.Configuration.Affinity.ProcessorId.Value<<12);
        uint data=s.Configuration.Vector | ((uint)InterruptDeliveryMode.Fixed<<8);
        if (s.Configuration.TriggerMode==InterruptTriggerMode.Level) data|=1U<<15;
        return new(address,data,s.Configuration.Vector);
    }
    private bool EnableLocalApic()
    {
        ulong value=NativeMethods.ReadMsr(ApicBaseMsr) | (1UL<<11);
        if (_x2Apic) value|=1UL<<10;
        return NativeMethods.WriteMsr(ApicBaseMsr,value);
    }
    private bool ProgramIoApic(InterruptRouteConfiguration c)
    {
        if (_ioApicBase==0 || c.Source.Value>=120) return false;
        uint low=c.Vector | ((uint)c.Priority<<4);
        if (c.Polarity==InterruptPolarity.ActiveLow) low|=1U<<13;
        if (c.TriggerMode==InterruptTriggerMode.Level) low|=1U<<15;
        if (c.InitiallyMasked) low|=1U<<16;
        uint high=c.Affinity.ProcessorId.Value<<24;
        uint index=0x10+(c.Source.Value*2);
        return WriteIoApic(index,low) && WriteIoApic(index+1,high);
    }
    private bool WriteIoApic(uint register,uint value) => NativeMethods.WriteMmio32(_ioApicBase,register) && NativeMethods.WriteMmio32(_ioApicBase+0x10,value);
    private bool Mutate(InterruptRouteHandle h,bool? masked,InterruptAffinity? affinity,byte? priority) => Update(h,s=>new(true,s.Handle,s.Configuration with { InitiallyMasked=masked??s.Configuration.InitiallyMasked, Affinity=affinity??s.Configuration.Affinity, Priority=priority??s.Configuration.Priority }));
    private bool Update(InterruptRouteHandle h,Func<RouteState,RouteState> transform)
    {
        for(int i=0;i<_routes.Length;i++) if(_routes[i].Active && _routes[i].Handle==h){ _routes[i]=transform(_routes[i]); return !_routes[i].Active || _routes[i].Configuration.PreferredMechanism!=InterruptDeliveryMechanism.IoApic || ProgramIoApic(_routes[i].Configuration); }
        return false;
    }
    private RouteState Find(InterruptRouteHandle h){ foreach(var s in _routes) if(s.Active&&s.Handle==h)return s; return default; }
    private readonly record struct RouteState(bool Active,InterruptRouteHandle Handle,InterruptRouteConfiguration Configuration);
}
