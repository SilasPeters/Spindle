using Engine.AccelerationStructures.BoundingVolumeHierarchy;
using Engine.BoundingBoxes;
using Engine.Geometry;
using Engine.Scenes;
using System.Numerics;

namespace Engine.Strategies.BVH;

/// <summary>
/// Optimized version of the SplitDirection Stratgey.
/// Uses the template provided in the slides of the
/// Advanced Graphics course (lecture 3 and 4)
/// </summary>
public class OptimizedBvhStrategy : IBvhStrategy
{
    private OptimizedBvhNode[] pool;
    private List<Geometry.Geometry> primitives;
    private int[] indices;
    private int N;
    private int nodesUsed;
    private int splitBins;

    public OptimizedBvhStrategy(int splitBins = 4)
    {
        this.splitBins = splitBins;
    }

    public void Build(Scene scene)
    {
        // Set number of primitives
        N = scene.Objects.Count;
        primitives = scene.Objects;
        
        // Initialize node pool and indices array
        pool = new OptimizedBvhNode[N * 2 - 1];
        indices = new int[N];
        for (int i = 0; i < N; i++) indices[i] = i;
        
        // Assign starting values to root node
        pool[0].leftFirst = 0;
        pool[0].count = N;
        nodesUsed = 1;
        UpdateBounds(ref pool[0]);
        Subdivide(ref pool[0]);
    }

    public bool TryIntersect(Ray ray, Interval distanceInterval, out Intersection intersection,
        ref IntersectionDebugInfo intersectionDebugInfo) => IntersectNode(ray, distanceInterval, out intersection, ref intersectionDebugInfo, pool[0]);

    private bool IntersectNode(Ray ray, Interval distanceInterval, out Intersection intersection,
        ref IntersectionDebugInfo intersectionDebugInfo, OptimizedBvhNode node)
    {
        intersectionDebugInfo.NumberOfTraversals++;
        
        // Check if the ray collides with the node
        if (!node.boundingBox.TryIntersect(ray, distanceInterval, out intersection, ref intersectionDebugInfo)) return false;

        // Check closest collision with primitives if node is a leaf
        if (node.isLeaf())
        {
            bool intersected = false;
            float closest = distanceInterval.Max;
            intersection = Intersection.Undefined;

            for (int i = 0; i < node.count; i++)
            {
                if (!primitives[indices[node.leftFirst + i]]
                        .TryIntersect(ray, new Interval(distanceInterval.Min, closest), out var newIntersection,
                            ref intersectionDebugInfo))
                    continue;

                intersected = true;
                closest = newIntersection.Distance;
                intersection = newIntersection;
            }

            return intersected;
        }
        
        // Collide with children
        bool leftIntersected = IntersectNode(ray, distanceInterval, out var leftIntersection, ref intersectionDebugInfo, pool[node.leftFirst]);
        bool rightIntersected = IntersectNode(ray, distanceInterval, out var rightIntersection, ref intersectionDebugInfo, pool[node.leftFirst + 1]);

        switch (leftIntersected)
        {
            case false when !rightIntersected:
                intersection = Intersection.Undefined;
                return false;
            case true when !rightIntersected:
                intersection = leftIntersection;
                return true;
            case false when rightIntersected:
                intersection = rightIntersection;
                return true;
            default:
                intersection = leftIntersection.Distance <= rightIntersection.Distance ? leftIntersection : rightIntersection;
                return true;
        }
    }
    
    private void Subdivide(ref OptimizedBvhNode node)
    {
        //Use Binned SAH to determine split axis
        int axis = 0;
        float splitPoint = 0;
        float splitCost = FindBestSplitPlane(node, ref axis, ref splitPoint);
        
        // Terminate if splitting the node doesn't result in a better cost
        if (splitCost >= node.boundingBox.GetArea() * node.count) return;
        
        // Apply sorting on the indices list of the primitives
        int i = node.leftFirst;
        int j = i + node.count - 1;
        while (i <= j)
        {
            if (primitives[indices[i]].GetCentroid().AxisByInt(axis) < splitPoint)
                i++;
            else
            {
                (indices[i], indices[j]) = (indices[j], indices[i]);
                j--;
            }
        }

        // Check if a split is empty
        int leftCount = i - node.leftFirst;
        if (leftCount == 0 || leftCount == node.count) return;
        // Create the child nodes
        pool[nodesUsed].leftFirst = node.leftFirst;
        pool[nodesUsed].count = leftCount;
        pool[nodesUsed + 1].leftFirst = i;
        pool[nodesUsed + 1].count = node.count - leftCount;
        node.leftFirst = nodesUsed;
        node.count = 0; // Reset count so we can use it as Leaf flag
        nodesUsed += 2;
        UpdateBounds(ref pool[node.leftFirst]);
        UpdateBounds(ref pool[node.leftFirst + 1]);
        
        // Recursive call to children
        Subdivide(ref pool[node.leftFirst]);
        Subdivide(ref pool[node.leftFirst + 1]);
    }

    private void UpdateBounds(ref OptimizedBvhNode node)
    {
        node.boundingBox = AxisAlignedBoundingBox.Empty();
        for (int i = 0; i < node.count; i++)
            node.boundingBox.Add((AxisAlignedBoundingBox)primitives[indices[node.leftFirst + i]].GetBoundingBox());
    }

    private float FindBestSplitPlane(OptimizedBvhNode node, ref int axis, ref float splitPoint)
    {
        float bestCost =  float.PositiveInfinity;
        for (int a = 0; a < 3; a++)
        {
            float boundsMin = float.PositiveInfinity;
            float boundsMax = -float.PositiveInfinity;
            for (int i = 0; i < node.count; i++)
            {
                Geometry.Geometry prim = primitives[indices[node.leftFirst + i]];
                boundsMin = Math.Min(boundsMin, prim.GetCentroid().AxisByInt(a));
                boundsMax = Math.Max(boundsMax, prim.GetCentroid().AxisByInt(a));
            }
            if (boundsMin == boundsMax) continue;
            float scale = (boundsMax - boundsMin) / splitBins;
            for (int i = 1; i < splitBins; i++)
            {
                float candidatePoint = boundsMin + i * scale;
                float cost = CalculateSah(node, a,  candidatePoint);
                if (cost < bestCost)
                {
                    splitPoint = candidatePoint;
                    axis = a;
                    bestCost = cost;
                }
            }
        }

        return bestCost;
    }
    
    private float CalculateSah(OptimizedBvhNode node, int axis, float point)
    {
        AxisAlignedBoundingBox leftBox =  AxisAlignedBoundingBox.Empty();
        AxisAlignedBoundingBox rightBox = AxisAlignedBoundingBox.Empty();
        int leftCount = 0, rightCount = 0;
        for (int i = 0; i < node.count; i++)
        {
            Geometry.Geometry prim =  primitives[indices[node.leftFirst + i]];
            if (prim.GetCentroid().AxisByInt(axis) < point)
            {
                leftCount++;
                leftBox.Add((AxisAlignedBoundingBox)prim.GetBoundingBox());
            }
            else
            {
                rightCount++;
                rightBox.Add((AxisAlignedBoundingBox)prim.GetBoundingBox());
            }
        }
        float cost = leftCount * leftBox.GetArea() +  rightCount * rightBox.GetArea();
        return cost > 0 ? cost :  float.PositiveInfinity;
    }
}
