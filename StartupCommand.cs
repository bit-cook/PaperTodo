namespace PaperTodo;

public enum StartupCommandKind
{
    None,
    Show,
    Hide,
    Toggle,
    NewTodo,
    NewNote,
    Exit
}

public sealed class StartupCommand
{
    public StartupCommand(StartupCommandKind kind)
    {
        Kind = kind;
    }

    public StartupCommandKind Kind { get; }

    public bool CreatesPaper => Kind is StartupCommandKind.NewTodo or StartupCommandKind.NewNote;

    public static StartupCommand Parse(
        IReadOnlyList<string> args,
        StartupCommandKind defaultWhenEmpty = StartupCommandKind.None)
    {
        foreach (var rawArgument in args)
        {
            var command = Normalize(rawArgument ?? "");
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            return new StartupCommand(ParseKind(command));
        }

        return new StartupCommand(defaultWhenEmpty);
    }

    private static StartupCommandKind ParseKind(string command)
    {
        return command switch
        {
            "show" or "open" => StartupCommandKind.Show,
            "hide" => StartupCommandKind.Hide,
            "toggle" => StartupCommandKind.Toggle,
            "new-todo" or "todo" => StartupCommandKind.NewTodo,
            "new-note" or "note" or "paper" => StartupCommandKind.NewNote,
            "exit" or "quit" => StartupCommandKind.Exit,
            _ => StartupCommandKind.None
        };
    }

    private static string Normalize(string arg)
    {
        return arg.Trim().TrimStart('-', '/').ToLowerInvariant();
    }
}
