namespace Inu.Boot.Contracts;

/// <summary>Provides a boot-time framebuffer when one was deliberately selected and supplied.</summary>
/// <nova.when>Use for a component that consumes firmware- or loader-provided framebuffer information.</nova.when>
/// <nova.depends>IBootContext and a selected framebuffer provider.</nova.depends>
public interface IBootFramebuffer : IBootContext
{
    /// <summary>Attempts to retrieve a usable boot framebuffer.</summary>
    /// <nova.when>Use before constructing a framebuffer-backed console or display bootstrap.</nova.when>
    /// <nova.depends>The selected boot framebuffer provider.</nova.depends>
    /// <returns><see langword="true"/> when a valid framebuffer is available.</returns>
    /// <example><code>bool available = boot.TryGetFramebuffer(out Framebuffer framebuffer);</code></example>
    bool TryGetFramebuffer(out Framebuffer framebuffer);
}

/// <summary>Stores one independently selectable framebuffer capability.</summary>
/// <nova.when>Use when a boot provider needs to hand a captured framebuffer to a consumer.</nova.when>
/// <nova.depends>Framebuffer and IBootFramebuffer.</nova.depends>
public readonly struct BootFramebufferContext : IBootFramebuffer
{
    private readonly Framebuffer _framebuffer;

    /// <summary>Creates a framebuffer capability.</summary>
    /// <nova.when>Use after a boot provider has captured framebuffer metadata.</nova.when>
    /// <nova.depends>A framebuffer value whose lifetime remains valid for the consumer.</nova.depends>
    /// <returns>A framebuffer boot capability.</returns>
    /// <example><code>BootFramebufferContext context = new(framebuffer);</code></example>
    public BootFramebufferContext(Framebuffer framebuffer) => _framebuffer = framebuffer;

    /// <summary>Attempts to retrieve the stored framebuffer.</summary>
    /// <nova.when>Use before accessing boot-provided display memory.</nova.when>
    /// <nova.depends>The framebuffer supplied to the constructor.</nova.depends>
    /// <returns><see langword="true"/> when the framebuffer is structurally usable.</returns>
    /// <example><code>bool available = context.TryGetFramebuffer(out Framebuffer framebuffer);</code></example>
    public bool TryGetFramebuffer(out Framebuffer framebuffer)
    {
        framebuffer = _framebuffer;
        return framebuffer.IsAvailable();
    }
}
