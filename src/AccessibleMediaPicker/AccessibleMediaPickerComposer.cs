using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace HCS.Media.AccessibleMediaPicker;

internal class AccessibleMediaPickerComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.ManifestFilters().Append<AccessibleMediaPickerManifestFilter>();
    }
}
