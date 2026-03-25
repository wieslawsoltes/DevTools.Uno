namespace DevTools.Uno.Diagnostics.Internal;

internal sealed class DisposableAction(Action? action) : IDisposable
{
    private Action? _action = action;

    public void Dispose()
    {
        Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}
