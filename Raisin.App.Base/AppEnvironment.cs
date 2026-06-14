using Raisin.Core;

namespace Raisin.App.Base;

public class AppEnvironment : IAppEnvironment
{
    public string AppName { get; }

    public AppEnvironment(string appName)
    {
        AppName = appName;
    }

    public virtual string Resolve(DataCategory category) => category switch
    {
        DataCategory.AppData => AppPaths.DataDir,
        DataCategory.LocalAppData => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppName),
        DataCategory.AppLocal => AppPaths.AppDir,
        DataCategory.Temp => Path.GetTempPath(),
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };
}
