namespace HCS.Media.AccessibleMediaPicker.Models;

public class CropDescription
{
    public string Alias { get; set; }
    public string? AltDescription { get; set; }

    public CropDescription(string alias, string? altDescription)
    {
        Alias = alias;
        AltDescription = altDescription;
    }
}
