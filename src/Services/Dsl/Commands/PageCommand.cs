namespace MilovanaEosEditor.Dsl.Commands;

/// <summary><c>[PAGE: key]</c> — starts a new logical page. The first page must be <c>start</c>.
/// Page-structure checks (empty key, duplicate key, missing <c>start</c>) live in the compiler since
/// they need the whole script; emission is the page itself, not an action.</summary>
public sealed class PageCommand : MarkerCommand
{
    public override string Keyword => "PAGE";
    public override string Summary => "Start a new page (the first page must be 'start').";
    public override string CompletionSnippet => "PAGE: ";
    public override PayloadSpec Payload => PayloadSpec.Of(PayloadKind.PageKey, required: true, "The page key (letters, numbers, hyphen).");
}
