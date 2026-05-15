namespace Sodalis.Core;

public sealed class ModuleRegistry
{
    public List<IModule> Modules { get; } = new();
}
