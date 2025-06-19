using Engine.AccelerationStructures.BoundingVolumeHierarchy;
using Engine.Geometry;
using Engine.Scenes;

namespace Engine.Strategies.BVH;

public interface IBvhStrategy
{
    public void Build(Scene scene);
    public bool TryIntersect(Ray ray, Interval distanceInterval, out Intersection intersection, ref IntersectionDebugInfo intersectionDebugInfo);
}
