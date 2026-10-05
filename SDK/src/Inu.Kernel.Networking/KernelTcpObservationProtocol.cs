using System;
namespace Inu.Kernel.Networking;

/// <summary>Selectable minimal TCP observation component used by the current socket state machine.</summary>
public static unsafe class KernelTcpObservationProtocol
{
    public static Boolean Register()=>KernelIpv4TransportServices.RegisterTcpObservation(&Receive);

    private static Boolean Receive(KernelNetworkInterfaceHandle networkInterface,KernelNetworkInterfaceInfo info,KernelIpv4Address source,KernelIpv4Address destination,Byte* payload,UInt32 length)
    {
        _=networkInterface;_=info;
        if(length<20)return false;
        UInt16 sourcePort=KernelNetworkMath.ReadUInt16Network(payload);UInt16 destinationPort=KernelNetworkMath.ReadUInt16Network(payload+2);
        UInt32 header=(UInt32)(payload[12]>>4)*4U;if(header<20||header>length)return false;
        return KernelSocketServices.ObserveTcp(new(source,sourcePort),new(destination,destinationPort),payload[13]);
    }
}
