namespace MilovanaEosEditor.Dsl.Commands;

/// <summary>
/// <c>[CHOICE]</c> — a branching menu. The following <c>[OPTION]</c> markers are its buttons. The
/// compiler validates that a CHOICE has at least one OPTION and assembles the action.
/// </summary>
public sealed class ChoiceCommand : MarkerCommand
{
    public override string Keyword => "CHOICE";
    public override string Summary => "Branching menu; follow with [OPTION] lines.";
    public override string CompletionSnippet => "CHOICE";

    /// <summary>Build the <c>choice</c> action from its resolved options (label, target, optional color).</summary>
    public static EosObject BuildAction(IEnumerable<(string Label, string Target, string? Color)> options)
    {
        var arr = new EosArray();
        foreach ((string label, string target, string? color) in options)
        {
            var opt = new EosObject()
                .Add("label", new EosString(label))
                .Add("commands", new EosArray().Add(EosActions.Goto(target)));
            opt.Add("color", color is null ? new EosRaw("null") : new EosString(color));
            arr.Add(opt);
        }
        return new EosObject().Add("choice", new EosObject().Add("options", arr));
    }
}
