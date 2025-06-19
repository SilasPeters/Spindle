using System.Diagnostics;

namespace Engine;

[DebuggerDisplay("[{Min}, {Max}]")]
public struct Interval
{
    public float Min;
    public float Max;

    public Interval()
    {
        Min = -Utils.Infinity;
        Max = Utils.Infinity;
    }

    public Interval(float min, float max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>
    /// Creates a new interval that encapsulates existing ones.
    /// </summary>
    /// <param name="intervals">The intervals that need to be enclosed</param>
    public Interval(IEnumerable<Interval> intervals)
    {
        Min = intervals.Min(i => i.Min);
        Max = intervals.Max(i => i.Max);
    }

    public static Interval Empty()
    {
        return new Interval(Utils.Infinity, -Utils.Infinity);
    }
    
    public static Interval Universe()
    {
        return new Interval(-Utils.Infinity, Utils.Infinity);
    }

    public float Size =>  Max - Min;
    public float Middle => Min + Size / 2;

    public bool Contains(float x)
    {
        return Min <= x && x <= Max;
    }

    /// <summary>
    /// Method that can expand an interval by some delta.
    /// This is mainly used when calculating edge cases of the bounding boxes.
    /// </summary>
    /// <param name="delta">Amount to expand the interval with</param>
    /// <returns>An interval expanded with the delta parameter</returns>
    public Interval Expand(float delta)
    {
        var padding = delta / 2;
        return new Interval(Min - padding, Max + padding);
    }

    public (Interval, Interval) Split()
    {
        float center = (Min + Max) / 2;
        
        return new(
            new Interval(Min, center), 
            new Interval(center, Max));
    }
    
    public bool Surrounds(float x)
    {
        return Min < x && x < Max;
    }

    public float Clamp(float x)
    { 
        return x < Min ? Min : x > Max ? Max : x;
    }

    public void Grow(Interval other)
    {
        Min = Math.Min(Min, other.Min);
        Max = Math.Max(Max, other.Max);
    }
}
