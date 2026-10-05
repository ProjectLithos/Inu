using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Drivers;

public static unsafe partial class KernelDrivers
{
    public static Boolean InstallInterruptBroker(delegate*<KernelDriverInterruptRequest*,KernelDriverInterruptHandle*,Boolean> request,delegate*<KernelDriverInterruptHandle,Boolean> release)
    { if(!_initialized||request==null||release==null)return false;_interruptRequestBroker=(UInt64)(void*)request;_interruptReleaseBroker=(UInt64)(void*)release;return true; }
    public static Boolean TryRequestInterrupt(KernelDriverInterruptRequest request,out KernelDriverInterruptHandle handle)
    { handle=default;if(_interruptRequestBroker==0UL||_interruptReleaseBroker==0UL||!KernelDriverMath.IsValidInterruptRequest(request)||!TryDevice(request.Device,out DeviceRecord* device)||device->InterruptCount>=InterruptsPerDevice)return false;if(device->BoundDriver!=0U){DriverRecord* driver=Driver((Int32)device->BoundDriver-1);if(driver->DeclaredCapabilities!=0UL&&!HasAnyInterruptGrant(device))return false;}delegate*<KernelDriverInterruptRequest*,KernelDriverInterruptHandle*,Boolean> broker=(delegate*<KernelDriverInterruptRequest*,KernelDriverInterruptHandle*,Boolean>)(void*)_interruptRequestBroker;KernelDriverInterruptHandle result=default;if(!broker(&request,&result)||result.Value==0UL)return false;device->InterruptHandle[device->InterruptCount++]=result.Value;handle=result;return true; }
    public static Boolean ReleaseInterrupt(KernelDriverInterruptHandle handle)
    { if(_interruptReleaseBroker==0UL||handle.Value==0UL)return false;for(Int32 i=0;i<(Int32)_deviceCapacity;i++){DeviceRecord* d=Device(i);if(d->Used==0)continue;for(Int32 n=0;n<d->InterruptCount;n++)if(d->InterruptHandle[n]==handle.Value){delegate*<KernelDriverInterruptHandle,Boolean> broker=(delegate*<KernelDriverInterruptHandle,Boolean>)(void*)_interruptReleaseBroker;if(!broker(handle))return false;for(Int32 q=n+1;q<d->InterruptCount;q++)d->InterruptHandle[q-1]=d->InterruptHandle[q];d->InterruptCount--;d->InterruptHandle[d->InterruptCount]=0UL;return true;}}return false; }
    public static Boolean DispatchInterrupt(KernelDeviceHandle device,UInt64 driverCookie)
    {
        if(KernelFaultInjection.ShouldInject(KernelFaultKind.DroppedInterrupt,"interrupt",out _))return true;
        if(KernelFaultInjection.TryGetInterruptDelay("interrupt",out UInt64 delayNanoseconds)){KernelHardwareSimulation.TryAdvanceTime(delayNanoseconds);return true;}
        if(KernelFaultInjection.ShouldInject(KernelFaultKind.DeviceReset,"device",out _)){ResetDevice(device);return true;}
        if(!TryBound(device,out DeviceRecord* d,out DriverRecord* r,out KernelDriverDeviceContext context)||r->Interrupt==0UL)return false;
        if(KernelFaultInjection.ShouldFailDriverRuntime("driver")){MarkFailed(d,device,context.Driver,KernelDriverFailureCode.DeviceFault,KernelDriverLifecycleStage.Fail);return false;}
        delegate*<KernelDriverDeviceContext*,UInt64,Boolean> handler=(delegate*<KernelDriverDeviceContext*,UInt64,Boolean>)(void*)r->Interrupt;return handler(&context,driverCookie);
    }

    private static Boolean HasAnyInterruptGrant(DeviceRecord* d){UInt64 mask=(UInt64)(KernelDriverCapability.Interrupt|KernelDriverCapability.Msi|KernelDriverCapability.MsiX);for(Int32 i=0;i<d->GrantCount;i++)if((d->GrantCapability[i]&mask)!=0UL)return true;return false;}

    private static void MarkFailed(DeviceRecord* d,KernelDeviceHandle device,KernelDriverHandle driver,KernelDriverFailureCode failure,KernelDriverLifecycleStage stage)
    { KernelDeviceState previous=(KernelDeviceState)d->State;d->State=(Byte)KernelDeviceState.Failed;d->FailureCode=(UInt32)failure;ReleaseAllInterrupts(d);RevokeAllCapabilities(d);if(driver.Value!=0U){DriverRecord* r=Driver((Int32)driver.Value-1);r->State=(Byte)KernelDriverState.Failed;if(r->Fail!=0UL){KernelDriverDeviceContext context=new(device,driver,Identifier(d));delegate*<KernelDriverDeviceContext*,KernelDriverFailureCode,Boolean> fail=(delegate*<KernelDriverDeviceContext*,KernelDriverFailureCode,Boolean>)(void*)r->Fail;fail(&context,failure);}}EmitLifecycle(device,driver,stage,previous,KernelDeviceState.Failed,failure); }
    private static void EmitLifecycle(KernelDeviceHandle device,KernelDriverHandle driver,KernelDriverLifecycleStage stage,KernelDeviceState previous,KernelDeviceState current,KernelDriverFailureCode failure)
    { if(TryDevice(device,out DeviceRecord* d))EmitDeviceStateEvent(KernelDeviceStateEventKind.StateChanged,device,d,driver,previous,current,failure);if(_lifecycleSink==0UL)return;KernelDriverLifecycleEvent e=new(device,driver,stage,previous,current,failure,_lifecycleSequence++);delegate*<KernelDriverLifecycleEvent*,Boolean> sink=(delegate*<KernelDriverLifecycleEvent*,Boolean>)(void*)_lifecycleSink;sink(&e); }
    private static void EmitDeviceStateEvent(KernelDeviceStateEventKind kind,KernelDeviceHandle device,DeviceRecord* d,KernelDriverHandle driver,KernelDeviceState previous,KernelDeviceState current,KernelDriverFailureCode failure)
    { if(d==null)return;EmitDeviceStateEvent(kind,device,d,driver,previous,current,failure,Identifier(d),new KernelDeviceHandle(d->Parent)); }
    private static void EmitDeviceStateEvent(KernelDeviceStateEventKind kind,KernelDeviceHandle device,DeviceRecord* d,KernelDriverHandle driver,KernelDeviceState previous,KernelDeviceState current,KernelDriverFailureCode failure,KernelDeviceIdentifier identifier,KernelDeviceHandle parent)
    { if(!_initialized||_deviceEvents==null)return;if(_deviceEventCount==DeviceEventCapacity){_deviceEventHead=(_deviceEventHead+1U)%DeviceEventCapacity;_deviceEventCount--;_deviceEventsDropped++;}DeviceEventRecord* e=_deviceEvents+_deviceEventTail;Clear((Byte*)e,sizeof(DeviceEventRecord));e->Sequence=_deviceEventSequence++;e->Kind=(Byte)kind;e->Handle=device.Value;e->Parent=parent.Value;e->Driver=driver.Value;e->PreviousState=(Byte)previous;e->CurrentState=(Byte)current;e->Failure=(UInt32)failure;e->Bus=(Byte)identifier.Bus;e->Vendor=identifier.VendorId;e->Device=identifier.DeviceId;e->SubsystemVendor=identifier.SubsystemVendorId;e->Subsystem=identifier.SubsystemId;e->ClassCode=identifier.ClassCode;e->Revision=identifier.Revision;e->Location=identifier.Location;_deviceEventTail=(_deviceEventTail+1U)%DeviceEventCapacity;_deviceEventCount++; }
    private static void ReleaseAllInterrupts(DeviceRecord* d)
    { if(d==null||d->InterruptCount==0||_interruptReleaseBroker==0UL){if(d!=null)d->InterruptCount=0;return;}delegate*<KernelDriverInterruptHandle,Boolean> broker=(delegate*<KernelDriverInterruptHandle,Boolean>)(void*)_interruptReleaseBroker;while(d->InterruptCount>0){Int32 i=d->InterruptCount-1;KernelDriverInterruptHandle h=new(d->InterruptHandle[i]);if(h.Value!=0UL)broker(h);d->InterruptHandle[i]=0UL;d->InterruptCount--;} }
    private static void UnlinkFromParent(KernelDeviceHandle handle,DeviceRecord* d)
    { if(d->Parent==0U)return;DeviceRecord* p=Device((Int32)d->Parent-1);UInt32 cur=p->FirstChild,prev=0U;while(cur!=0U){DeviceRecord* c=Device((Int32)cur-1);if(cur==handle.Value){if(prev==0U)p->FirstChild=c->NextSibling;else Device((Int32)prev-1)->NextSibling=c->NextSibling;return;}prev=cur;cur=c->NextSibling;} }
}
