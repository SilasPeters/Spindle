using Engine.BoundingBoxes;

namespace Engine.AccelerationStructures.BoundingVolumeHierarchy;

public struct OptimizedBvhNode
{
    public AxisAlignedBoundingBox boundingBox;
    public int leftFirst, count;
    public bool isLeaf() { return count > 0; }
}
