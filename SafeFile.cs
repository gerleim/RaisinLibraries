using System.IO;

namespace Raisin.WPF.Base;

internal static class SafeFile
{
    /// <summary>Write text to targetPath atomically (temp file + replace).</summary>
    public static void WriteAllText(string targetPath, string contents)
    {
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tempPath = targetPath + ".tmp";
        File.WriteAllText(tempPath, contents);
        ReplaceOrMove(tempPath, targetPath);
    }

    /// <summary>Write via a StreamWriter action atomically.</summary>
    public static void WriteWithStream(string targetPath, Action<StreamWriter> writeAction)
    {
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tempPath = targetPath + ".tmp";
        using (var sw = new StreamWriter(tempPath, append: false))
            writeAction(sw);
        ReplaceOrMove(tempPath, targetPath);
    }

    /// <summary>Atomically move a completed temp file to its target path.</summary>
    public static void ReplaceOrMove(string tempPath, string targetPath)
    {
        if (File.Exists(targetPath))
            File.Replace(tempPath, targetPath, destinationBackupFileName: null);
        else
            File.Move(tempPath, targetPath);
    }
}
