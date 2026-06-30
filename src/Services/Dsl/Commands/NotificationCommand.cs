namespace MilovanaEosEditor.Dsl.Commands;

/// <summary>
/// <c>[NOTIFICATION (id=, target=): label]</c> — a persistent button overlay that jumps to
/// <c>target</c>. Notifications are auto-scoped to the page that declares them: the compiler injects a
/// <c>notification.remove</c> when navigating to a page that does not re-declare the same id.
/// </summary>
public sealed class NotificationCommand : MarkerCommand
{
    public override string Keyword => "NOTIFICATION";
    public override string Summary => "Persistent button overlay that jumps to a page.";
    public override string CompletionSnippet => "NOTIFICATION (id=, target=): ";

    public override IReadOnlyList<ParameterSpec> Parameters { get; } = new[]
    {
        ParameterSpec.String("id", required: true, "Unique id (used to remove the notification later)."),
        ParameterSpec.String("target", required: true, "Destination page key."),
    };

    public override PayloadSpec Payload => PayloadSpec.Of(PayloadKind.Label, required: true, "Button label text.");

    public static EosObject BuildAction(string id, string label, string target) =>
        new EosObject().Add("notification.create", new EosObject()
            .Add("id", new EosString(id))
            .Add("buttonLabel", new EosString(label))
            .Add("buttonCommands", new EosArray().Add(EosActions.Goto(target))));
}
