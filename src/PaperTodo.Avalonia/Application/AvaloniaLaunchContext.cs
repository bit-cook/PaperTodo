namespace PaperTodo.Avalonia.Application;

internal sealed class AvaloniaLaunchContext
{
    private readonly object _gate = new();
    private readonly Queue<IReadOnlyList<string>> _pending = new();
    private Action<IReadOnlyList<string>>? _receiver;
    private bool _accepting = true;

    public AvaloniaLaunchContext(IReadOnlyList<string> initialArguments)
    {
        InitialArguments = initialArguments.ToArray();
    }

    public IReadOnlyList<string> InitialArguments { get; }

    public void EnqueueForwardedArguments(IReadOnlyList<string> arguments)
    {
        Action<IReadOnlyList<string>>? receiver;
        var copy = arguments.ToArray();
        lock (_gate)
        {
            if (!_accepting)
            {
                return;
            }

            receiver = _receiver;
            if (receiver is null)
            {
                _pending.Enqueue(copy);
                return;
            }
        }

        receiver(copy);
    }

    public void AttachReceiver(Action<IReadOnlyList<string>> receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);

        IReadOnlyList<string>[] pending;
        lock (_gate)
        {
            if (!_accepting)
            {
                return;
            }

            if (_receiver is not null)
            {
                throw new InvalidOperationException("A forwarded-command receiver is already attached.");
            }

            _receiver = receiver;
            pending = _pending.ToArray();
            _pending.Clear();
        }

        foreach (var arguments in pending)
        {
            receiver(arguments);
        }
    }

    public void DetachReceiver(Action<IReadOnlyList<string>> receiver)
    {
        lock (_gate)
        {
            if (_receiver == receiver)
            {
                _receiver = null;
            }
        }
    }

    public void StopAcceptingCommands()
    {
        lock (_gate)
        {
            _accepting = false;
            _receiver = null;
            _pending.Clear();
        }
    }
}
