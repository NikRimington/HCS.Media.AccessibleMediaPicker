using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models.Editors;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core;
using Umbraco.Extensions;
using static Umbraco.Cms.Core.Constants;
using Newtonsoft.Json;

namespace HCS.Media.AccessibleMediaPicker.PropertyEditor;

// TODO: Check this file

/// <summary>
///     Represents a media picker property editor.
/// </summary>
[DataEditor(
    Constants.EditorAlias,
    ValueType = ValueTypes.Json,
    ValueEditorIsReusable = true)]
public class AccessibleMediaPickerPropertyEditor : DataEditor
{
    private readonly IIOHelper _ioHelper;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AccessibleMediaPickerPropertyEditor" /> class.
    /// </summary>
    public AccessibleMediaPickerPropertyEditor(
        IDataValueEditorFactory dataValueEditorFactory,
        IIOHelper ioHelper)
        : base(dataValueEditorFactory)
    {
        _ioHelper = ioHelper;
        SupportsReadOnly = true;
    }

    public override IPropertyIndexValueFactory PropertyIndexValueFactory { get; } = new NoopPropertyIndexValueFactory();

    /// <inheritdoc />
    protected override IConfigurationEditor CreateConfigurationEditor() =>
        new MediaPicker3ConfigurationEditor(_ioHelper);

    /// <inheritdoc />
    protected override IDataValueEditor CreateValueEditor() =>
        DataValueEditorFactory.Create<AccessibleMediaPickerPropertyValueEditor>(Attribute!);



    internal class AccessibleMediaPickerPropertyValueEditor : DataValueEditor, IDataValueReference
    {
        private readonly IDataTypeService _dataTypeService;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ITemporaryMediaService _temporaryMediaService;


        public AccessibleMediaPickerPropertyValueEditor(
            ILocalizedTextService localizedTextService,
            IShortStringHelper shortStringHelper,
            IJsonSerializer jsonSerializer,
            IIOHelper ioHelper,
            DataEditorAttribute attribute,
            IDataTypeService dataTypeService,
            ITemporaryMediaService temporaryMediaService)
            : base(localizedTextService, shortStringHelper, jsonSerializer, ioHelper, attribute)
        {
            _jsonSerializer = jsonSerializer;
            _dataTypeService = dataTypeService;
            _temporaryMediaService = temporaryMediaService;
        }

        /// <remarks>
        ///     Note: no FromEditor() and ToEditor() methods
        ///     We do not want to transform the way the data is stored in the DB and would like to keep a raw JSON string
        /// </remarks>
        public IEnumerable<UmbracoEntityReference> GetReferences(object? value)
        {
            foreach (AccessibleMediaWithCropsDto dto in Deserialize(_jsonSerializer, value))
            {
                yield return new UmbracoEntityReference(Udi.Create(UdiEntityType.Media, dto.MediaKey));
            }
        }

        public override async Task<object> ToEditor(IProperty property, string? culture = null, string? segment = null)
        {
            var value = property.GetValue(culture, segment);

            var dtos = Deserialize(_jsonSerializer, value).ToList();

            IDataType? dataType = await _dataTypeService.GetAsync(property.PropertyType.DataTypeKey);
            if (dataType?.ConfigurationObject != null)
            {
                MediaPicker3Configuration? configuration = dataType.ConfigurationAs<MediaPicker3Configuration>();

                foreach (AccessibleMediaWithCropsDto dto in dtos)
                {
                    dto.ApplyConfiguration(configuration);
                }
            }

            return dtos;
        }

        public override object? FromEditor(ContentPropertyData editorValue, object? currentValue)
        {
            if (editorValue.Value is JArray dtos)
            {
                if (editorValue.DataTypeConfiguration is MediaPicker3Configuration configuration)
                {
                    dtos = PersistTempMedia(dtos, configuration);
                }

                // Clean up redundant/default data
                foreach (JObject? dto in dtos.Values<JObject>())
                {
                    AccessibleMediaWithCropsDto.Prune(dto);
                }

                return dtos.ToString(Formatting.None);
            }

            return base.FromEditor(editorValue, currentValue);
        }

        internal static IEnumerable<AccessibleMediaWithCropsDto> Deserialize(IJsonSerializer jsonSerializer, object? value)
        {
            var rawJson = value is string str ? str : value?.ToString();
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                yield break;
            }

            if (!rawJson.DetectIsJson())
            {
                // Old comma seperated UDI format
                foreach (var udiStr in rawJson.Split(CharArrays.Comma))
                {
                    if (UdiParser.TryParse(udiStr, out Udi? udi) && udi is GuidUdi guidUdi)
                    {
                        yield return new AccessibleMediaWithCropsDto
                        {
                            Key = Guid.NewGuid(),
                            MediaKey = guidUdi.Guid,
                            Crops = Enumerable.Empty<AccessibleImageCropperCrop>(),
                            FocalPoint = new ImageCropperValue.ImageCropperFocalPoint { Left = 0.5m, Top = 0.5m },
                        };
                    }
                }
            }
            else
            {
                IEnumerable<AccessibleMediaWithCropsDto>? dtos =
                    jsonSerializer.Deserialize<IEnumerable<AccessibleMediaWithCropsDto>>(rawJson);
                if (dtos is not null)
                {
                    // New JSON format
                    foreach (AccessibleMediaWithCropsDto dto in dtos)
                    {
                        yield return dto;
                    }
                }
            }
        }

        private JArray PersistTempMedia(JArray jArray, MediaPicker3Configuration mediaPicker3Configuration)
        {
            var result = new JArray();
            foreach (JObject? dto in jArray.Values<JObject>())
            {
                if (dto is null)
                {
                    continue;
                }

                if (!dto.TryGetValue("tmpLocation", out JToken? temporaryLocation))
                {
                    // If it does not have a temporary path, it can be an already saved image or not-yet uploaded temp-image, check for media-key
                    if (dto.TryGetValue("mediaKey", out _))
                    {
                        result.Add(dto);
                    }

                    continue;
                }

                var temporaryLocationString = temporaryLocation.Value<string>();
                if (temporaryLocationString is null)
                {
                    continue;
                }
                JToken? mediaTypeAlias = dto.GetValue("mediaTypeAlias");
                IMedia mediaFile = _temporaryMediaService.Save(temporaryLocationString, mediaPicker3Configuration.StartNodeId, mediaTypeAlias?.Value<string>());
                AccessibleMediaWithCropsDto? mediaDto = _jsonSerializer.Deserialize<AccessibleMediaWithCropsDto>(dto.ToString());
                if (mediaDto is null)
                {
                    continue;
                }

                mediaDto.MediaKey = mediaFile.GetUdi().Guid;
                result.Add(JObject.Parse(_jsonSerializer.Serialize(mediaDto)));
            }

            return result;
        }

        /// <summary>
        ///     Model/DTO that represents the JSON that the MediaPicker3 stores.
        /// </summary>
        [DataContract]
        internal class AccessibleMediaWithCropsDto
        {
            [DataMember(Name = "key")]
            public Guid Key { get; set; }

            [DataMember(Name = "mediaKey")]
            public Guid MediaKey { get; set; }

            [DataMember(Name = "crops")]
            public IEnumerable<AccessibleImageCropperCrop>? Crops { get; set; }

            [DataMember(Name = "focalPoint")]
            public ImageCropperValue.ImageCropperFocalPoint? FocalPoint { get; set; }

            [DataMember(Name = "isDecorative")]
            public bool IsDecorative { get; set; }

            [DataMember(Name = "defaultAlt")]
            public string? DefaultAlt { get; set; }

            /// <summary>
            ///     Removes redundant crop data/default focal point.
            /// </summary>
            /// <param name="value">The media with crops DTO.</param>
            /// <remarks>
            ///     Because the DTO uses the same JSON keys as the image cropper value for crops and focal point, we can re-use the
            ///     prune method.
            /// </remarks>
            public static void Prune(JObject? value)
            {
                // TODO: cannot access internal method Prune
                //ImageCropperValue.Prune(value)
            }

            /// <summary>
            ///     Applies the configuration to ensure only valid crops are kept and have the correct width/height.
            /// </summary>
            /// <param name="configuration">The configuration.</param>
            public void ApplyConfiguration(MediaPicker3Configuration? configuration)
            {
                var crops = new List<AccessibleImageCropperCrop>();

                MediaPicker3Configuration.CropConfiguration[]? configuredCrops = configuration?.Crops;
                if (configuredCrops != null)
                {
                    foreach (MediaPicker3Configuration.CropConfiguration configuredCrop in configuredCrops)
                    {
                        AccessibleImageCropperCrop? crop =
                            Crops?.FirstOrDefault(x => x.Alias == configuredCrop.Alias);

                        crops.Add(new AccessibleImageCropperCrop
                        {
                            CropAlt = crop?.CropAlt,
                            Alias = configuredCrop.Alias,
                            Width = configuredCrop.Width,
                            Height = configuredCrop.Height,
                            Coordinates = crop?.Coordinates,
                        });
                    }
                }

                Crops = crops;

                if (configuration?.EnableLocalFocalPoint == false)
                {
                    FocalPoint = null;
                }
            }
        }
    }

    [DataContract(Name = "imageCropData")]
    public class AccessibleImageCropperCrop : ImageCropperValue.ImageCropperCrop
    {
        [DataMember(Name = "alt")]
        [JsonProperty("alt")]
        public string? CropAlt { get; set; }
    }
}


