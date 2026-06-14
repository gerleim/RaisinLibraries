namespace Raisin.App.Base;

public interface IManagedPaths
{
    DataCategory Category { get; }
    IReadOnlyList<string> ManagedNames { get; }
}
