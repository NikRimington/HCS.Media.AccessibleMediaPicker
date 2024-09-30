using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;

namespace HCS.Media.AccessibleMediaPicker.Models;

public class AccessibleMediaWithCrops : MediaWithCrops
{
    public bool IsDecorative { get; set; }
    public string? DefaultAltDescription { get; }
    public IEnumerable<CropDescription> CropDescriptions { get; }

    public AccessibleMediaWithCrops(IPublishedContent content, IPublishedValueFallback publishedValueFallback, ImageCropperValue localCrops,
        bool isDecorative,
        string? defaultAltDescription, IEnumerable<CropDescription> cropDescriptions) : base(content, publishedValueFallback, localCrops)
    {
        IsDecorative = isDecorative;
        DefaultAltDescription = defaultAltDescription;
        CropDescriptions = cropDescriptions;
    }

    public string GetDescription(string? cropAlias = null)
    {
        if (!IsDecorative)
        {
            if (!string.IsNullOrWhiteSpace(cropAlias))
            {
                var cropDescription = CropDescriptions.FirstOrDefault(c => c.Alias.Equals(cropAlias));

                return (string.IsNullOrWhiteSpace(cropDescription?.AltDescription) ? DefaultAltDescription : cropDescription.AltDescription) ?? string.Empty;
            }
            else
            {
                return DefaultAltDescription ?? string.Empty;
            }
        }

        return string.Empty;
    }
}

public class AccessibleMediaWithCrops<T> : AccessibleMediaWithCrops where T : IPublishedContent
{
    public AccessibleMediaWithCrops(T content, IPublishedValueFallback publishedValueFallback, ImageCropperValue localCrops, bool isDecorative,
        string? defaultAltDescription, IEnumerable<CropDescription> cropDescriptions) : base(content, publishedValueFallback, localCrops, isDecorative, defaultAltDescription, cropDescriptions)
    {
    }
}
