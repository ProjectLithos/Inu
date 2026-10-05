using System;
using Inu.Kernel.Console;
using Inu.Kernel.Contracts;
using Inu.Kernel.Platform.X64;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Runtime.NativeAot;
using Inu.Runtime.Conformance;
using Inu.Kernel.Acpi;
using Inu.Kernel.TimerDispatch;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.InterruptBroker;
using Inu.Kernel.Time;
using Inu.Kernel.Smp;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Protection;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Ps2;
using Inu.Kernel.Processes;
using Inu.Kernel.Drivers;
using Inu.Kernel.Storage;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;
using Inu.Kernel.Nvme;
using Inu.Kernel.Ahci;
using Inu.Kernel.Virtio;
using Inu.Kernel.Virtio.Gpu;
using Inu.Kernel.Graphics;
using Inu.Kernel.Gui;
using Inu.Kernel.Audio;
using Inu.Kernel.E1000;
using Inu.Kernel.Rtl8168;
using Inu.Bus.Usb;
using Inu.Usb.Xhci;
using Inu.Usb.Hid;
using Inu.Usb.MassStorage;
using Inu.Usb.Hub;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Bootstrap;

public static unsafe partial class Kernel
{
private static void BootTrace(Char value) => Native.WriteSerial((Byte)value);

private static void EmitKMainStageBody() { BootTrace('N');BootTrace('O');BootTrace('M');BootTrace('N');BootTrace('G');BootTrace(':');BootTrace('B');BootTrace('O');BootTrace('D');BootTrace('Y');BootTrace('\n'); }

private static void EmitKMainStageConsole() { BootTrace('N');BootTrace('O');BootTrace('M');BootTrace('N');BootTrace('G');BootTrace(':');BootTrace('C');BootTrace('O');BootTrace('N');BootTrace('S');BootTrace('O');BootTrace('L');BootTrace('E');BootTrace('\n'); }

private static void EmitKMainStageString() { BootTrace('N');BootTrace('O');BootTrace('M');BootTrace('N');BootTrace('G');BootTrace(':');BootTrace('S');BootTrace('T');BootTrace('R');BootTrace('I');BootTrace('N');BootTrace('G');BootTrace('\n'); }

private static void EmitKMainStageLogging() { BootTrace('N');BootTrace('O');BootTrace('M');BootTrace('N');BootTrace('G');BootTrace(':');BootTrace('L');BootTrace('O');BootTrace('G');BootTrace('I');BootTrace('N');BootTrace('I');BootTrace('T');BootTrace('\n'); }

private static void EmitKMainStageBoot() { BootTrace('N');BootTrace('O');BootTrace('M');BootTrace('N');BootTrace('G');BootTrace(':');BootTrace('B');BootTrace('O');BootTrace('O');BootTrace('T');BootTrace('\n'); }

private static void EmitKMainFailureConsole() { BootTrace('N');BootTrace('O');BootTrace('M');BootTrace('N');BootTrace('G');BootTrace(':');BootTrace('F');BootTrace('A');BootTrace('I');BootTrace('L');BootTrace(':');BootTrace('C');BootTrace('O');BootTrace('N');BootTrace('S');BootTrace('O');BootTrace('L');BootTrace('E');BootTrace('\n'); }

private static void EmitKMainFailureString() { BootTrace('N');BootTrace('O');BootTrace('M');BootTrace('N');BootTrace('G');BootTrace(':');BootTrace('F');BootTrace('A');BootTrace('I');BootTrace('L');BootTrace(':');BootTrace('S');BootTrace('T');BootTrace('R');BootTrace('I');BootTrace('N');BootTrace('G');BootTrace('\n'); }

private static void EmitKMainFailureLogging() { BootTrace('N');BootTrace('O');BootTrace('M');BootTrace('N');BootTrace('G');BootTrace(':');BootTrace('F');BootTrace('A');BootTrace('I');BootTrace('L');BootTrace(':');BootTrace('L');BootTrace('O');BootTrace('G');BootTrace('\n'); }

private static void EmitKMainFailurePanic() { BootTrace('N');BootTrace('O');BootTrace('M');BootTrace('N');BootTrace('G');BootTrace(':');BootTrace('F');BootTrace('A');BootTrace('I');BootTrace('L');BootTrace(':');BootTrace('P');BootTrace('A');BootTrace('N');BootTrace('I');BootTrace('C');BootTrace('\n'); }
}
