namespace Inu.Boot.Contracts;

/// <summary>Identifies a boot protocol only when an OS component explicitly needs protocol identity.</summary>
/// <nova.when>Use for code whose behaviour genuinely depends on the entry protocol rather than on a narrower boot capability.</nova.when>
/// <nova.depends>No boot protocol is required unless the OS selects a component that consumes this value.</nova.depends>
public enum BootProtocol
{
    Unknown = 0,
    Uefi = 1,
    Limine = 2,
    Multiboot2 = 3
}
