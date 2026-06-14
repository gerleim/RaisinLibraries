namespace Raisin.Core;

public abstract class DeferredFileStore : DurableFileStore
{
    private readonly Timer _flushTimer;
    private bool _dirty;

    protected DeferredFileStore(string filePath) : base(filePath)
    {
        _flushTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    protected void ScheduleFlush()
    {
        _dirty = true;
        _flushTimer.Change(200, Timeout.Infinite);
    }

    protected void SaveNow()
    {
        _dirty = false;
        _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);
        WriteFile();
    }

    public void Flush()
    {
        lock (Sync)
        {
            if (!_dirty) return;
            _dirty = false;
            WriteFile();
        }
    }
}
