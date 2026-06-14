using System.Diagnostics;

namespace Raisin.Core;

public static class ParentProcessWatcher
{
    public static void StartIfRequested(string[] args, Action onParentExited)
    {
        int? parentPid = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--parent-pid" && i + 1 < args.Length && int.TryParse(args[i + 1], out var pid))
            {
                parentPid = pid;
                break;
            }
        }

        if (parentPid is null)
            return;

        try
        {
            var parent = Process.GetProcessById(parentPid.Value);
            parent.EnableRaisingEvents = true;
            parent.Exited += (_, _) => onParentExited();
        }
        catch
        {
            // Parent already exited or PID invalid — shut down immediately
            onParentExited();
        }
    }
}
