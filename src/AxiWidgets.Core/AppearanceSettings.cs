using System.Globalization;
using System.Text.Json.Serialization;

namespace AxiWidgets.Core;

public sealed class AppearanceSettings
{
    public bool Blur { get; set; } = true;
    public string TintColor { get; set; } = "#1E1E22";
    public double TintOpacity { get; set; } = 15;
    public void Normalize()
    {
        Blur=true;
        TintOpacity=double.IsFinite(TintOpacity)?Math.Clamp(TintOpacity,0,100):15;
        TintColor=TryColor(TintColor,out var rgb)?$"#{rgb:X6}":"#1E1E22";
    }
    public static bool TryColor(string? value,out uint rgb)
    {
        rgb=0;
        return value is { Length: 7 } && value[0]=='#' &&
            uint.TryParse(value.AsSpan(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out rgb);
    }
    [JsonIgnore] public uint TintRgb=>TryColor(TintColor,out var rgb)?rgb:0x1E1E22;
    [JsonIgnore] public byte TintAlpha=>(byte)Math.Round(255*Math.Clamp(TintOpacity,0,100)/100);
}
