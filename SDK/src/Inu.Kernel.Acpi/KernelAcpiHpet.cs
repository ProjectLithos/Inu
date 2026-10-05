using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Acpi;

/// <summary>Provides the HPET platform-driver view.</summary>
public static class KernelAcpiHpet
{
    /// <summary>Gets the firmware HPET description.</summary>
    public static Boolean TryGetDevice(out AcpiHpetInfo value) => KernelAcpi.TryGetHpet(out value);
}
