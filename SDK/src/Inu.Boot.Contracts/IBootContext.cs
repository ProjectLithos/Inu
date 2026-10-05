namespace Inu.Boot.Contracts;

/// <summary>Marks one independently selectable boot capability.</summary>
/// <nova.when>Use as the common boundary for boot information without requiring unrelated boot data.</nova.when>
/// <nova.depends>Concrete capability interfaces derive from this marker and are selected independently by the OS author.</nova.depends>
public interface IBootContext
{
}
