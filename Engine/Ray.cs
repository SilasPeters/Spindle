using System.Numerics;
namespace Engine;

public class Ray
{
    public Vector3 Origin { get; private set; }
    public Vector3 Direction { get; private set; }
    public Vector3 Reciprocal { get; private set; }
    
    public Ray(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Direction = direction;
        Reciprocal = new Vector3(1 / direction.X, 1 / direction.Y, 1 / direction.Z);
    }

    public Vector3 At(float t)
    {
        return Origin + t * Direction;
    }
}
