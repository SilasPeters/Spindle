using Engine.BoundingBoxes;
using Engine.Geometry;
using System.Collections.Concurrent;
using System.Numerics;

namespace Engine.AccelerationStructures.BoundingVolumeHierarchy;

/*
 * Node contains:
 * - IBoundingBox BoundingBox
 * - int Left (if negative, it is a leaf. Ensure left+1 == right node)
 * - int first, count (indices to primitive list)
 */

public class BvhNode_ : IIntersectable
{
    public IBoundingBox BoundingBox;
    public int Left;
    public int First, Count;

    public IBoundingBox GetBoundingBox() => BoundingBox;
    public Vector3 GetCentroid() => BoundingBox.GetCentroid();

    public void Subdivide(List<BvhNode_> pool, int[] indices, List<Geometry.Geometry> primitives)
    {
        if (Count < 3) return; //TODO: Hardcoded value!
        pool.Add(new  BvhNode_()); // Add left
        pool.Add(new  BvhNode_()); // Add right
        Left = pool.Count - 2;
        Partition(indices, primitives);
        pool[Left].Subdivide(pool, indices, primitives);
        pool[Left + 1].Subdivide(pool, indices, primitives);
    }
    private void Partition(int[] indices, List<Geometry.Geometry> primitives)
    {
        // Find split axis with binned SAH
        
        
        // Quick sort indices using the split as pivot
        
        
        // Set first and count of both left and right child
        
        
        // Calculate left and right bounds with the given first and count
        
    }

    public bool TryIntersectExtended(Ray ray, Interval distanceInterval, out Intersection intersection,
        ref IntersectionDebugInfo intersectionDebugInfo, List<BvhNode_> pool, int[] indices, List<Geometry.Geometry> primitives)
    {
        // Update debug info
        intersectionDebugInfo.NumberOfTraversals++;
        
        // Check if the ray intersects the bounding box
        if (!TryIntersect(ray, distanceInterval, out intersection, ref intersectionDebugInfo))
        {
            intersection = Intersection.Undefined;
            return false;
        }

        if (Left <= 0) // The node is a leaf
        {
            // Loop through primitives
            bool intersected = false;
            float closest = distanceInterval.Max;
            intersection = Intersection.Undefined;
            
            for (int i = 0; i < Count; i++)
            {
                if (!primitives[indices[First + i]]
                        .TryIntersect(ray, new Interval(distanceInterval.Min, closest),
                            out var newIntersection, ref intersectionDebugInfo))
                    continue;
                
                intersected = true;
                closest = newIntersection.Distance;
                intersection = newIntersection;
            }

            return intersected;
        }
        else
        {
            // Traverse the left and right nodes
            bool leftIntersected = pool[Left].TryIntersectExtended(ray, distanceInterval, out var leftIntersection,
                ref intersectionDebugInfo, pool, indices, primitives);
            bool rightIntersected = pool[Left + 1].TryIntersectExtended(ray, distanceInterval, out var rightIntersection,
                ref intersectionDebugInfo, pool, indices, primitives);

            switch (leftIntersected)
            {
                // If both are missed, return false
                case false when !rightIntersected:
                    intersection = Intersection.Undefined;
                    return false;
                // if both are hit, check closest
                case true when rightIntersected:
                    intersection = leftIntersection.Distance <= rightIntersection.Distance ? leftIntersection : rightIntersection;
                    return true;
                // if one is hit, return the result
                default:
                    intersection = leftIntersected ? leftIntersection : rightIntersection;
                    return true;
            }
        }
    }
    
    public bool TryIntersect(Ray ray, Interval distanceInterval, out Intersection intersection,
        ref IntersectionDebugInfo intersectionDebugInfo)
    {
        return BoundingBox.TryIntersect(ray, distanceInterval,  out intersection, ref intersectionDebugInfo);
    }
}

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
