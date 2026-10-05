using System;

namespace Inu.Kernel.Acpi;

/// <summary>Registration and dispatch boundary for optional ACPI fixed-feature power support.</summary>
public static unsafe class KernelAcpiPowerServices
{
    private static Byte _registered;
    private static Byte _ready;
    private static delegate*<Boolean> _initialize;
    private static delegate*<AcpiPowerCapabilities*,Boolean> _getCapabilities;
    private static delegate*<Boolean*,Boolean> _consumePowerButton;
    private static delegate*<UInt32,Boolean> _isSleepStateAvailable;
    private static delegate*<Boolean> _hasResumeVector;
    private static delegate*<UInt32,Boolean> _configureResumeVector;
    private static delegate*<UInt32,Boolean> _sleep;
    private static delegate*<Boolean> _reboot;
    private static delegate*<Boolean> _shutdown;

    public static Boolean Register(
        delegate*<Boolean> initialize,
        delegate*<AcpiPowerCapabilities*,Boolean> getCapabilities,
        delegate*<Boolean*,Boolean> consumePowerButton,
        delegate*<UInt32,Boolean> isSleepStateAvailable,
        delegate*<Boolean> hasResumeVector,
        delegate*<UInt32,Boolean> configureResumeVector,
        delegate*<UInt32,Boolean> sleep,
        delegate*<Boolean> reboot,
        delegate*<Boolean> shutdown)
    {
        if (_registered != 0 || initialize == null || getCapabilities == null || consumePowerButton == null || isSleepStateAvailable == null || hasResumeVector == null || configureResumeVector == null || sleep == null || reboot == null || shutdown == null) return false;
        _initialize = initialize; _getCapabilities = getCapabilities; _consumePowerButton = consumePowerButton;
        _isSleepStateAvailable = isSleepStateAvailable; _hasResumeVector = hasResumeVector; _configureResumeVector = configureResumeVector;
        _sleep = sleep; _reboot = reboot; _shutdown = shutdown; _registered = 1; return true;
    }

    public static Boolean Initialize() { if (_ready != 0) return true; if (_registered == 0 || !_initialize()) return false; _ready = 1; return true; }
    public static Boolean IsAvailable() => _ready != 0;
    public static AcpiPowerCapabilities GetCapabilities() { AcpiPowerCapabilities value = default; if (_ready != 0) _getCapabilities(&value); return value; }
    public static Boolean TryConsumePowerButton(out Boolean pressed) { pressed = false; if (_ready == 0) return false; Boolean local = false; if (!_consumePowerButton(&local)) return false; pressed = local; return true; }
    public static Boolean IsSleepStateAvailable(UInt32 state) => _ready != 0 && _isSleepStateAvailable(state);
    public static Boolean HasResumeVector() => _ready != 0 && _hasResumeVector();
    public static Boolean ConfigureResumeVector(UInt32 physicalAddress) => _ready != 0 && _configureResumeVector(physicalAddress);
    public static Boolean Sleep(UInt32 state) => _ready != 0 && _sleep(state);
    public static Boolean Reboot() => _ready != 0 && _reboot();
    public static Boolean Shutdown() => _ready != 0 && _shutdown();
}
