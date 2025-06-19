using Engine.BoundingBoxes;
using Engine.Geometry;
using System.Collections.Concurrent;
using System.Numerics;

namespace Engine.AccelerationStructures.BoundingVolumeHierarchy;
public class BvhNode : IIntersectable
{
    public IBoundingBox BoundingBox;

    public bool IsLeaf;
    public BvhNode Left, Right;
    public List<IIntersectable> Primitives;
    
    public IBoundingBox GetBoundingBox() => BoundingBox;

    /// <inheritdoc />
    public Vector3 GetCentroid() => BoundingBox.GetCentroid();

    public bool TryIntersect(Ray ray, Interval distanceInterval, out Intersection intersection, ref IntersectionDebugInfo intersectionDebugInfo)
    {
        intersectionDebugInfo.NumberOfTraversals++;

        // If we intersect with the bounding box, we need to check the children
        if (BoundingBox.TryIntersect(ray, distanceInterval, out var boxIntersection, ref intersectionDebugInfo))
        {
            // If we are a leaf we intersect the primitive
            if (IsLeaf)
            {
                return TryIntersectPrimitives(ray, distanceInterval, Primitives, out intersection, ref intersectionDebugInfo);
            }

            // We do intersect with both boxes, thus we recurse the one that is closest to us.
            bool leftIntersected =
                Left.TryIntersect(ray, distanceInterval, out var leftIntersection, ref intersectionDebugInfo);
            bool rightIntersected =
                Right.TryIntersect(ray, distanceInterval, out var rightIntersection, ref intersectionDebugInfo);

            if (!leftIntersected && !rightIntersected)
            {
                intersection = Intersection.Undefined;
                return false;
            }

            if (leftIntersected && !rightIntersected)
            {
                intersection = leftIntersection; 
                return true;
            }
            
            if (rightIntersected && !leftIntersected)
            {
                intersection = rightIntersection; 
                return true;
            }

            // We intersect with both boxes
            if (leftIntersection.Distance <= rightIntersection.Distance)
            {
                intersection = leftIntersection;
                return true;
            }

            intersection = rightIntersection;
            return true;
        }

        intersection = Intersection.Undefined;
        return false;
    }

    private bool TryIntersectPrimitives(Ray ray, Interval distanceInterval, List<IIntersectable> primitives, out Intersection intersection, ref IntersectionDebugInfo intersectionDebugInfo)
    {
        var intersected = false;
        // Current closest intersection, currently infinite for we have no intersection.
        var closest = distanceInterval.Max;

        var storedIntersection = Intersection.Undefined;
        
        // Loop over all the geometry in the scene to determine what the ray hits.
        foreach (var intersectable in Primitives)
        {
            
            // If we don't intersect, we continue checking the remaining objects.
            if (!intersectable.TryIntersect(ray, new Interval(distanceInterval.Min, closest), out var newIntersection, ref intersectionDebugInfo))
                continue;
            
            // When we do hit, we set the closest to the new intersection (intersection2)
            intersected = true;
            closest = newIntersection.Distance;
            storedIntersection = newIntersection;
        }

        intersection = storedIntersection;
        return intersected;
    }
}
