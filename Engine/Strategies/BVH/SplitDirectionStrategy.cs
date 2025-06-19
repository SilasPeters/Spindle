using Engine.AccelerationStructures.BoundingVolumeHierarchy;
using Engine.BoundingBoxes;
using Engine.Geometry;
using Engine.Scenes;
using System.Numerics;

namespace Engine.Strategies.BVH;
public class SplitDirectionStrategy : IBvhStrategy
{
    public int NumOfPrimitives { get; set; }
    private BvhNode root;

    public SplitDirectionStrategy(int numOfPrimitives)
    {
        NumOfPrimitives = numOfPrimitives;
    }
    
    public void Build(Scene scene)
    {
        // Create the root bounding box.
        root = new BvhNode
        {
            IsLeaf = false, BoundingBox = scene.GetBoundingBox(), Primitives = new List<IIntersectable>(scene.Objects)
        };

        var random = new Random();
        PopulateChildren(random);
    }

    public bool TryIntersect(Ray ray, Interval distanceInterval, out Intersection intersection,
        ref IntersectionDebugInfo intersectionDebugInfo) =>
        root.TryIntersect(ray, distanceInterval, out intersection, ref intersectionDebugInfo);

    private BvhNode PopulateChildren(Random random)
    {
        Stack<BvhNode> stack = new();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var parent = stack.Pop();

            // Base case
            if (parent.Primitives.Count <= NumOfPrimitives)
            {
                parent.IsLeaf = true;
                continue;
            }

            int axisToSplit = random.Next(0, 3);

            // Find the split intervals
            (Interval fst, _) = parent.BoundingBox.AxisByInt(axisToSplit).Split();

            var primsFst = new List<IIntersectable>();
            var primsSnd = new List<IIntersectable>();

            // Divide the primitives based on their position in 3D space.
            foreach (IIntersectable primitive in parent.Primitives)
            {
                // Check if it falls inside the first or second box and append accordingly.
                if (fst.Surrounds(primitive.GetCentroid().AxisByInt(axisToSplit)))
                {
                    primsFst.Add(primitive);
                    continue;
                }

                primsSnd.Add(primitive);
            }

            var boxFst = new AxisAlignedBoundingBox(primsFst.Select(i => i.GetBoundingBox()));
            var boxSnd = new AxisAlignedBoundingBox(primsSnd.Select(i => i.GetBoundingBox()));

            var left = new BvhNode { IsLeaf = false, Primitives = primsFst, BoundingBox = boxFst };

            var right = new BvhNode { IsLeaf = false, Primitives = primsSnd, BoundingBox = boxSnd };

            parent.Left = left;
            parent.Right = right;

            stack.Push(left);
            stack.Push(right);
        }

        return root;
    }
}
