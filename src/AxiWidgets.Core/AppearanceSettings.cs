namespace AxiWidgets.Core;

public sealed class AppearanceSettings
{
    public bool Blur { get; set; } = true;
    public double Transparency { get; set; } = 56;
    public void Normalize() => Transparency = double.IsFinite(Transparency)?Math.Clamp(Transparency,0,95):56;
    [System.Text.Json.Serialization.JsonIgnore]
    public byte TintAlpha => (byte)Math.Round(255*(1-Math.Clamp(double.IsFinite(Transparency)?Transparency:56,0,95)/100));
}
