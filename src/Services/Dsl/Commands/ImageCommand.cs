namespace MilovanaEosEditor.Dsl.Commands;

/// <summary>
/// <c>[IMAGE: bucket/file.jpg]</c> — show an image, resolved to a locator via <c>asset-map.json</c>.
/// The special payload <c>hold</c> keeps the previous image (emits nothing). Locator resolution (and the
/// "no locator" error) happens at build time where the asset catalog is available.
/// </summary>
public sealed class ImageCommand : MarkerCommand
{
    public const string Hold = "hold";

    public override string Keyword => "IMAGE";
    public override string Summary => "Show an image by bucket/filename (or 'hold' to keep the current one).";
    public override string CompletionSnippet => "IMAGE: ";
    public override PayloadSpec Payload => PayloadSpec.Of(PayloadKind.ImageRef, required: true,
        "bucket/filename.jpg — resolved via asset-map.json. Use 'hold' to keep the current image.");

    public static EosObject BuildAction(string locator) =>
        new EosObject().Add("image", new EosObject().Add("locator", new EosString(locator)));
}
