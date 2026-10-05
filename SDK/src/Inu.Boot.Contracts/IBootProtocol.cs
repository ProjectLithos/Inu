namespace Inu.Boot.Contracts;

/// <summary>Provides the protocol used to enter the current boot environment.</summary>
/// <nova.when>Use only when a component must distinguish UEFI, Limine, Multiboot2, or another entry protocol.</nova.when>
/// <nova.depends>IBootContext and a boot provider that deliberately exposes protocol identity.</nova.depends>
public interface IBootProtocol : IBootContext
{
    /// <summary>Gets the protocol used to enter the boot environment.</summary>
    /// <nova.when>Read only when protocol identity itself changes component behaviour.</nova.when>
    /// <nova.depends>The selected IBootProtocol provider.</nova.depends>
    BootProtocol Protocol { get; }
}

/// <summary>Stores one boot-protocol capability value.</summary>
/// <nova.when>Use when a boot provider deliberately hands protocol identity to another component.</nova.when>
/// <nova.depends>IBootProtocol.</nova.depends>
public readonly struct BootProtocolContext : IBootProtocol
{
    /// <summary>Creates a protocol capability.</summary>
    /// <nova.when>Use at a boot-provider boundary when protocol identity has been selected as part of the OS architecture.</nova.when>
    /// <nova.depends>A valid BootProtocol value.</nova.depends>
    /// <returns>A boot-protocol capability value.</returns>
    /// <example><code>BootProtocolContext context = new(BootProtocol.Uefi);</code></example>
    public BootProtocolContext(BootProtocol protocol) => Protocol = protocol;

    /// <summary>Gets the selected boot protocol.</summary>
    /// <nova.when>Read from a consumer that explicitly depends on IBootProtocol.</nova.when>
    /// <nova.depends>The value supplied to the constructor.</nova.depends>
    public BootProtocol Protocol { get; }
}
