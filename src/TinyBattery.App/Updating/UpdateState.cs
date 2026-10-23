namespace TinyBattery.App.Updating;

public sealed class UpdateState
{
    private int _availableCount;

    public int AvailableCount => Volatile.Read(ref _availableCount);

    public event EventHandler? Changed;

    public void Set(int count)
    {
        count = Math.Max(0, count);
        if (Interlocked.Exchange(ref _availableCount, count) == count)
        {
            return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
