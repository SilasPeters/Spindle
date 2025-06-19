using Engine.Geometry;
using System.Numerics;

namespace Engine.BoundingBoxes;

public interface IBoundingBox : IIntersectable
{
    public IBoundingBox Combine(List<IBoundingBox> boxes);
    public Interval     AxisByInt(int axis);
    public Vector3      GetLowerBound();
    public Vector3      GetUpperBound();
    public Vector3      GetExtent();
    public float        GetArea();
}
