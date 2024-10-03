using Umbraco.Cms.Core.Manifest;

namespace HCS.Media.AccessibleMediaPicker;

internal class AccessibleMediaPickerManifestFilter : IManifestFilter
{
    public void Filter(List<PackageManifest> manifests)
    {
        var assembly = typeof(AccessibleMediaPickerManifestFilter).Assembly;

        manifests.Add(new PackageManifest
        {
            PackageName = "HCS.Media.AccessibleMediaPicker",
            Version = assembly.GetName()?.Version?.ToString(3) ?? "0.1.0",
            AllowPackageTelemetry = true,
            Scripts = new string[] {
                // List any Script files
                // Urls should start '/App_Plugins/AccessibleMediaPicker/' not '/wwwroot/AccessibleMediaPicker/', e.g.
                "/App_Plugins/HCS.Media/AccessibleMediaPicker/hcsAccessibleMediaPickerPropertyEditor.component.js",
                "/App_Plugins/HCS.Media/AccessibleMediaEntryEditor/mediaentryeditor.controller.js"
            },
            Stylesheets = new string[]
            {
                // List any Stylesheet files
                // Urls should start '/App_Plugins/AccessibleMediaPicker/' not '/wwwroot/AccessibleMediaPicker/', e.g.
                "/App_Plugins/HCS.Media/AccessibleMediaEntryEditor/styles.css"
            }
        });
    }
}
