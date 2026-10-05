# Inu 0.2.24

## Networking socket component boundary

- Promoted the kernel socket layer to a selectable source component.
- Added `KernelSocketServices` as the neutral registry/dispatch contract between the networking core/API/packet stack and the concrete socket implementation.
- `KernelNetworking` no longer initializes or queries `KernelSockets` directly.
- `KernelNetworkApi` and inbound UDP/TCP dispatch no longer name the concrete socket implementation.
- Standard generated Networking configurations select `INU_COMPONENT_NETWORK_SOCKET_SERVICE` by default; an OS author may omit it to build networking without kernel sockets.
- Existing public network socket facade calls remain available through `KernelNetworkApi`; when the socket component is omitted they report unavailable rather than requiring the concrete source.

## DHCP and DNS codec split

- Retired the combined `KernelDhcpDns` concrete class.
- Added independently selectable DHCP and DNS codecs registered through `KernelNetworkProtocolServices`.
- `KernelNetworkApi` is now the stable facade for DHCP/DNS operations and reports DNS capability according to the selected codec.
- Standard Networking configurations select both codecs by default, but either can be omitted independently.
