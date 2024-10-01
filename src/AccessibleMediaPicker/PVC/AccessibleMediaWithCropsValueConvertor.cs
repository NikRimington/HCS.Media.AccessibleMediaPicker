using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DeliveryApi;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.DeliveryApi;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors.DeliveryApi;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Infrastructure.DeliveryApi;
using Umbraco.Extensions;
using HCS.Media.AccessibleMediaPicker.Models;
using HCS.Media.AccessibleMediaPicker.PropertyEditor;
namespace HCS.Media.AccessibleMediaPicker.PVC;

public class AccessibleMediaWithCropsValueConvertor : PropertyValueConverterBase, IDeliveryApiPropertyValueConverter
{
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IPublishedSnapshotAccessor _publishedSnapshotAccessor;
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IApiMediaWithCropsBuilder _apiMediaWithCropsBuilder;

    [Obsolete($"Use constructor that takes {nameof(IApiMediaWithCropsBuilder)}, scheduled for removal in V14")]
    public AccessibleMediaWithCropsValueConvertor(
        IPublishedSnapshotAccessor publishedSnapshotAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedValueFallback publishedValueFallback,
        IJsonSerializer jsonSerializer)
        : this(
            publishedSnapshotAccessor,
            publishedUrlProvider,
            publishedValueFallback,
            jsonSerializer,
            StaticServiceProvider.Instance.GetRequiredService<IApiMediaWithCropsBuilder>()
        )
    {
    }

    [Obsolete($"Use constructor that takes {nameof(IApiMediaWithCropsBuilder)}, scheduled for removal in V14")]
    public AccessibleMediaWithCropsValueConvertor(
        IPublishedSnapshotAccessor publishedSnapshotAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedValueFallback publishedValueFallback,
        IJsonSerializer jsonSerializer,
        IApiMediaBuilder apiMediaBuilder)
        : this(
            publishedSnapshotAccessor,
            publishedUrlProvider,
            publishedValueFallback,
            jsonSerializer,
            StaticServiceProvider.Instance.GetRequiredService<IApiMediaWithCropsBuilder>())
    {
    }

    [Obsolete($"Use constructor that takes {nameof(IApiMediaWithCropsBuilder)} and no {nameof(IApiMediaBuilder)}, scheduled for removal in V14")]
    public AccessibleMediaWithCropsValueConvertor(
        IPublishedSnapshotAccessor publishedSnapshotAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedValueFallback publishedValueFallback,
        IJsonSerializer jsonSerializer,
        IApiMediaBuilder apiMediaBuilder,
        IApiMediaWithCropsBuilder apiMediaWithCropsBuilder)
        : this(publishedSnapshotAccessor, publishedUrlProvider, publishedValueFallback, jsonSerializer, apiMediaWithCropsBuilder)
    {
    }

    public AccessibleMediaWithCropsValueConvertor(
        IPublishedSnapshotAccessor publishedSnapshotAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedValueFallback publishedValueFallback,
        IJsonSerializer jsonSerializer,
        IApiMediaWithCropsBuilder apiMediaWithCropsBuilder)
    {
        _publishedSnapshotAccessor = publishedSnapshotAccessor ??
                                     throw new ArgumentNullException(nameof(publishedSnapshotAccessor));
        _publishedUrlProvider = publishedUrlProvider;
        _publishedValueFallback = publishedValueFallback;
        _jsonSerializer = jsonSerializer;
        _apiMediaWithCropsBuilder = apiMediaWithCropsBuilder;
    }

    public override bool IsConverter(IPublishedPropertyType propertyType) =>
        propertyType.EditorAlias.Equals(Constants.EditorAlias);

    public override bool? IsValue(object? value, PropertyValueLevel level)
    {
        var isValue = base.IsValue(value, level);
        if (isValue != false && level == PropertyValueLevel.Source)
        {
            // Empty JSON array is not a value
            isValue = value?.ToString() != "[]";
        }

        return isValue;
    }

    public override Type GetPropertyValueType(IPublishedPropertyType propertyType)
        => IsMultipleDataType(propertyType.DataType)
            ? typeof(IEnumerable<AccessibleMediaWithCrops>)
            : typeof(AccessibleMediaWithCrops);

    public override PropertyCacheLevel GetPropertyCacheLevel(IPublishedPropertyType propertyType) =>
        PropertyCacheLevel.Snapshot;

    public override object? ConvertIntermediateToObject(IPublishedElement owner, IPublishedPropertyType propertyType, PropertyCacheLevel referenceCacheLevel, object? inter, bool preview)
    {
        var isMultiple = IsMultipleDataType(propertyType.DataType);
        if (string.IsNullOrEmpty(inter?.ToString()))
        {
            // Short-circuit on empty value
            return isMultiple ? Enumerable.Empty<AccessibleMediaWithCrops>() : null;
        }

        var mediaItems = new List<AccessibleMediaWithCrops>();
        IEnumerable<AccessibleMediaPickerPropertyEditor.AccessibleMediaPickerPropertyValueEditor.AccessibleMediaWithCropsDto> dtos =
            AccessibleMediaPickerPropertyEditor.AccessibleMediaPickerPropertyValueEditor.Deserialize(_jsonSerializer, inter);
        MediaPicker3Configuration? configuration = propertyType.DataType.ConfigurationAs<MediaPicker3Configuration>();
        IPublishedSnapshot publishedSnapshot = _publishedSnapshotAccessor.GetRequiredPublishedSnapshot();
        foreach (AccessibleMediaPickerPropertyEditor.AccessibleMediaPickerPropertyValueEditor.AccessibleMediaWithCropsDto dto in dtos)
        {
            IPublishedContent? mediaItem = publishedSnapshot.Media?.GetById(preview, dto.MediaKey);
            if (mediaItem != null)
            {
                var localCrops = new ImageCropperValue
                {
                    Crops = dto.Crops,
                    FocalPoint = dto.FocalPoint,
                    Src = mediaItem.Url(_publishedUrlProvider),
                };

                var accessibleCrops = dto.Crops?.Where(c => c.Alias != null)
                    .Select(c => new CropDescription(c.Alias ?? string.Empty, c.CropAlt)).ToList() ?? [];

                localCrops.ApplyConfiguration(configuration);
                //TODO: Handle accessiblity elements

                // TODO: This should be optimized/cached, as calling Activator.CreateInstance is slow
                Type mediaWithCropsType = typeof(AccessibleMediaWithCrops<>).MakeGenericType(mediaItem.GetType());
                var mediaWithCrops = (AccessibleMediaWithCrops)Activator.CreateInstance(mediaWithCropsType, mediaItem, _publishedValueFallback, localCrops, dto.IsDecorative, dto.DefaultAlt, accessibleCrops)!;

                mediaItems.Add(mediaWithCrops);

                if (!isMultiple)
                {
                    // Short-circuit on single item
                    break;
                }
            }
        }

        return isMultiple ? mediaItems : mediaItems.FirstOrDefault();
    }

    public PropertyCacheLevel GetDeliveryApiPropertyCacheLevel(IPublishedPropertyType propertyType) => PropertyCacheLevel.Elements;

    public PropertyCacheLevel GetDeliveryApiPropertyCacheLevelForExpansion(IPublishedPropertyType propertyType) => PropertyCacheLevel.Snapshot;

    public Type GetDeliveryApiPropertyValueType(IPublishedPropertyType propertyType) => typeof(IEnumerable<IApiMediaWithCrops>);

    public object? ConvertIntermediateToDeliveryApiObject(IPublishedElement owner, IPublishedPropertyType propertyType, PropertyCacheLevel referenceCacheLevel, object? inter, bool preview, bool expanding)
    {
        var isMultiple = IsMultipleDataType(propertyType.DataType);

        IApiMediaWithCrops ToApiMedia(AccessibleMediaWithCrops media) => _apiMediaWithCropsBuilder.Build(media);

        // NOTE: eventually we might implement this explicitly instead of piggybacking on the default object conversion. however, this only happens once per cache rebuild,
        // and the performance gain from an explicit implementation is negligible, so... at least for the time being this will do just fine.
        var converted = ConvertIntermediateToObject(owner, propertyType, referenceCacheLevel, inter, preview);
        if (isMultiple && converted is IEnumerable<AccessibleMediaWithCrops> mediasWithCrops)
        {
            return mediasWithCrops.Select(ToApiMedia).ToArray();
        }
        if (isMultiple == false && converted is AccessibleMediaWithCrops mediaWithCrops)
        {
            return new[] { ToApiMedia(mediaWithCrops) };
        }

        return Array.Empty<IApiMediaWithCrops>();
    }

    private bool IsMultipleDataType(PublishedDataType dataType) =>
        dataType.ConfigurationAs<MediaPicker3Configuration>()?.Multiple ?? false;
}
