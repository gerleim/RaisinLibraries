namespace Raisin.App.Base;

public interface IAppEnvironment
{
    string AppName { get; }
    string Resolve(DataCategory category);
}
