using System;

namespace Inu.Kernel.Networking;

/// <summary>Registry for optional DHCP and DNS protocol codecs.</summary>
public static unsafe class KernelNetworkProtocolServices
{
    private static delegate*<Byte*,UInt32,UInt32,KernelMacAddress,UInt32*,Boolean> _buildDhcpDiscover;
    private static delegate*<Byte*,UInt32,UInt32,KernelIpv4Address*,KernelIpv4Address*,Boolean> _parseDhcpOffer;
    private static delegate*<Byte*,UInt32,UInt16,String,KernelDnsRecordType,UInt32*,Boolean> _buildDnsQuery;
    private static delegate*<Byte*,UInt32,UInt16,KernelIpv4Address*,Boolean> _parseDnsA;
    private static delegate*<Byte*,UInt32,UInt16,KernelIpv6Address*,Boolean> _parseDnsAaaa;

    public static Boolean HasDhcpCodec => _buildDhcpDiscover!=null&&_parseDhcpOffer!=null;
    public static Boolean HasDnsCodec => _buildDnsQuery!=null&&_parseDnsA!=null&&_parseDnsAaaa!=null;

    public static Boolean RegisterDhcpCodec(
        delegate*<Byte*,UInt32,UInt32,KernelMacAddress,UInt32*,Boolean> buildDiscover,
        delegate*<Byte*,UInt32,UInt32,KernelIpv4Address*,KernelIpv4Address*,Boolean> parseOffer)
    {
        if(buildDiscover==null||parseOffer==null||_buildDhcpDiscover!=null)return false;
        _buildDhcpDiscover=buildDiscover;_parseDhcpOffer=parseOffer;return true;
    }

    public static Boolean RegisterDnsCodec(
        delegate*<Byte*,UInt32,UInt16,String,KernelDnsRecordType,UInt32*,Boolean> buildQuery,
        delegate*<Byte*,UInt32,UInt16,KernelIpv4Address*,Boolean> parseA,
        delegate*<Byte*,UInt32,UInt16,KernelIpv6Address*,Boolean> parseAaaa)
    {
        if(buildQuery==null||parseA==null||parseAaaa==null||_buildDnsQuery!=null)return false;
        _buildDnsQuery=buildQuery;_parseDnsA=parseA;_parseDnsAaaa=parseAaaa;return true;
    }

    internal static Boolean BuildDhcpDiscover(Byte* buffer,UInt32 capacity,UInt32 transactionId,KernelMacAddress mac,out UInt32 length){length=0;if(_buildDhcpDiscover==null)return false;UInt32 value=0;if(!_buildDhcpDiscover(buffer,capacity,transactionId,mac,&value))return false;length=value;return true;}
    internal static Boolean TryParseDhcpOffer(Byte* packet,UInt32 length,UInt32 transactionId,out KernelIpv4Address offered,out KernelIpv4Address server){offered=default;server=default;if(_parseDhcpOffer==null)return false;KernelIpv4Address o=default,s=default;if(!_parseDhcpOffer(packet,length,transactionId,&o,&s))return false;offered=o;server=s;return true;}
    internal static Boolean BuildDnsQuery(Byte* buffer,UInt32 capacity,UInt16 transactionId,String host,KernelDnsRecordType type,out UInt32 length){length=0;if(_buildDnsQuery==null)return false;UInt32 value=0;if(!_buildDnsQuery(buffer,capacity,transactionId,host,type,&value))return false;length=value;return true;}
    internal static Boolean TryParseDnsAResponse(Byte* packet,UInt32 length,UInt16 transactionId,out KernelIpv4Address address){address=default;if(_parseDnsA==null)return false;KernelIpv4Address value=default;if(!_parseDnsA(packet,length,transactionId,&value))return false;address=value;return true;}
    internal static Boolean TryParseDnsAaaaResponse(Byte* packet,UInt32 length,UInt16 transactionId,out KernelIpv6Address address){address=default;if(_parseDnsAaaa==null)return false;KernelIpv6Address value=default;if(!_parseDnsAaaa(packet,length,transactionId,&value))return false;address=value;return true;}
}
