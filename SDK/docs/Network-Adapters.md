# Inu network-adapter drivers

Inu 0.23.0 extends the heap-backed networking stack with three built-in PCI/PCIe Ethernet families.

## VirtIO-net

`Inu.Kernel.Virtio` remains the first virtual NIC. It uses the modern VirtIO PCI transport, PMM-backed split virtqueues, MAC and MTU feature negotiation, transmit and receive queues, and a polling fallback when MSI/MSI-X delivery is not installed. `KernelVirtio.PollAll()` services all started VirtIO network receive queues without exposing transport details to the IPv4 stack.

## Intel E1000 / E1000e

`Inu.Kernel.E1000` matches supported Intel 8254x/8257x-era E1000 and E1000e PCI IDs, maps BAR0, enables PCI memory/bus-master access, resets the controller, reads the receive address registers, and creates PMM-backed receive/transmit descriptor rings. Every started controller registers its own `KernelNetworking` interface. Receive and transmit use DMA buffers and the normal contextual network callbacks. MSI and MSI-X capability presence is reported in `E1000DeviceInfo`; interrupt delivery remains transport-neutral through the common driver framework. I219/I225-family IDs are intentionally not claimed by this driver.

## Realtek RTL8168 / RTL8111

`Inu.Kernel.Rtl8168` supports the RTL8168/RTL8111 PCIe family and RTL8169-compatible device ID, maps the controller MMIO BAR, enables bus mastering, performs the chip reset, reads the station MAC address, programs PMM-backed Rx/Tx descriptor rings, and registers one `KernelNetworking` interface per controller. The driver supports transmit, receive polling, interface enable/disable, and MSI capability discovery.

## Adapter-neutral networking

None of the IP/ARP/UDP/TCP code knows which adapter carries a packet. Drivers deliver received Ethernet frames to `KernelNetworking.QueueReceivedFrame`, and outbound packets arrive through the same contextual transmit callback contract used by VirtIO-net.
