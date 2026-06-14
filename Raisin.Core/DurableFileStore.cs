namespace Raisin.Core;

public abstract class DurableFileStore
{
    protected readonly string FilePath;
    protected readonly object Sync = new();

    protected DurableFileStore(string filePath)
    {
        FilePath = filePath;
    }

    protected void LoadFromDisk()
    {
        if (File.Exists(FilePath))
            ReadFile();
    }

    protected abstract void ReadFile();

    protected abstract void WriteFile();
}
