using MilovanaEosEditor.Dsl.Commands;

namespace MilovanaEosEditor.Dsl;

/// <summary>
/// The single catalogue of known marker commands. The parser, validator, colorizer, and completion
/// engine all consult this — so supporting a new marker is just adding a <see cref="MarkerCommand"/>
/// subclass to <see cref="All"/>. Keywords are matched case-insensitively (upper-cased on lookup).
/// </summary>
public sealed class CommandRegistry
{
    /// <summary>Process-wide shared instance (the command set is stateless).</summary>
    public static readonly CommandRegistry Default = new();

    private readonly Dictionary<string, MarkerCommand> _byKeyword;

    public CommandRegistry() : this(DefaultCommands()) { }

    public CommandRegistry(IEnumerable<MarkerCommand> commands)
    {
        All = commands.ToList();
        _byKeyword = All.ToDictionary(c => c.Keyword, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<MarkerCommand> All { get; }

    public bool IsKnown(string keyword) => _byKeyword.ContainsKey(keyword);

    public MarkerCommand? Find(string keyword) => _byKeyword.GetValueOrDefault(keyword);

    private static IEnumerable<MarkerCommand> DefaultCommands() => new MarkerCommand[]
    {
        new PageCommand(),
        new ImageCommand(),
        new SayCommand(),
        new MetronomeCommand(),
        new PauseCommand(),
        new AudioCommand(),
        new NotificationCommand(),
        new ChoiceCommand(),
        new OptionCommand(),
        new GotoCommand(),
        new EndCommand(),
    };
}
