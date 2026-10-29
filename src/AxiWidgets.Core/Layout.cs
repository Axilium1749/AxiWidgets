namespace AxiWidgets.Core;

public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}
public record SnapResult(Rect Bounds, double? GuideX, double? GuideY);

// All input uses the same coordinate space. UI supplies physical pixels and scales parameters.
public sealed class SnapEngine
{
    private double? heldX, heldY;
    public void Reset() { heldX = heldY = null; }
    public SnapResult Snap(Rect moving, IEnumerable<Rect> neighbors, Rect work, double scale = 1)
    {
        var x = new List<(double Position, double Guide)>();
        var y = new List<(double Position, double Guide)>();
        double margin = 8 * scale, gap = 12 * scale;
        x.Add((work.X + margin, work.X + margin));
        x.Add((work.Right - margin - moving.Width, work.Right - margin));
        y.Add((work.Y + margin, work.Y + margin));
        y.Add((work.Bottom - margin - moving.Height, work.Bottom - margin));
        foreach (var n in neighbors)
        {
            x.AddRange([(n.X,n.X),(n.Right-moving.Width,n.Right),
                (n.X+(n.Width-moving.Width)/2,n.X+n.Width/2),
                (n.X-gap-moving.Width,n.X-gap),(n.Right+gap,n.Right+gap)]);
            y.AddRange([(n.Y,n.Y),(n.Bottom-moving.Height,n.Bottom),
                (n.Y+(n.Height-moving.Height)/2,n.Y+n.Height/2),
                (n.Y-gap-moving.Height,n.Y-gap),(n.Bottom+gap,n.Bottom+gap)]);
        }
        var sx = Choose(moving.X, x, heldX, 12*scale);
        var sy = Choose(moving.Y, y, heldY, 12*scale);
        heldX = sx?.Position; heldY = sy?.Position;
        var bounds = Clamp(moving with { X = sx?.Position ?? moving.X, Y = sy?.Position ?? moving.Y }, work, margin);
        return new(bounds, bounds.X == sx?.Position ? sx?.Guide : null, bounds.Y == sy?.Position ? sy?.Guide : null);
    }
    private static (double Position, double Guide)? Choose(double value, List<(double Position,double Guide)> candidates, double? held, double threshold)
    {
        // Hysteresis retains a snap until the pointer exceeds a wider release radius.
        if (held.HasValue && Math.Abs(value-held.Value) <= threshold*1.5)
            foreach (var c in candidates) if (c.Position == held.Value) return c;
        var eligible = candidates.Where(c => Math.Abs(c.Position-value) <= threshold)
            .OrderBy(c => Math.Abs(c.Position-value)).ThenBy(c => c.Position).ToArray();
        return eligible.Length == 0 ? null : eligible[0];
    }
    public static Rect Clamp(Rect r, Rect work, double margin = 8) => r with
    {
        X = Math.Clamp(r.X,work.X+margin,Math.Max(work.X+margin,work.Right-r.Width-margin)),
        Y = Math.Clamp(r.Y,work.Y+margin,Math.Max(work.Y+margin,work.Bottom-r.Height-margin))
    };
}

public sealed class ClockState
{
    public string Kind {get;set;}="Clock";
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Monitor { get; set; } = "";
    public double X { get; set; } = 48;
    public double Y { get; set; } = 48;
    public bool Acrylic { get; set; } = true;

    public bool Seconds { get; set; }
    public bool Date { get; set; } = true;
}
public static class ClockSchedule
{
    public static TimeSpan Next(DateTime now, bool seconds)
    {
        long period = seconds ? TimeSpan.TicksPerSecond : TimeSpan.TicksPerMinute;
        return TimeSpan.FromTicks(period - now.Ticks % period);
    }
}


