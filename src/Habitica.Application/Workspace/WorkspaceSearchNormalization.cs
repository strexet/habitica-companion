using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;

namespace Habitica.Application.Workspace;

internal static partial class WorkspaceSearchNormalization
{
    // .NET 8's browser ICU rejects FormKC. Use the browser's native NFKC implementation,
    // matching the Worker evaluator without changing the persisted query or its hash.
    internal static string Normalize(string value) => OperatingSystem.IsBrowser()
        ? NormalizeInBrowser(value, "NFKC")
        : value.Normalize(NormalizationForm.FormKC);

    [SupportedOSPlatform("browser")]
    [JSImport("globalThis.String.prototype.normalize.call")]
    private static partial string NormalizeInBrowser(string value, string form);
}
