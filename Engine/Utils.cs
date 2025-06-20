using System.Numerics;

namespace Engine;
    
public class Utils
{
    public static float Pi => 3.1415926535897932385f;
    public static float Infinity => float.PositiveInfinity;

    public static float DegreesToRadians(float degrees)
    {
        return degrees * Pi / 180f;
    }

    public static float RandomFloat()
    {
        return Random.Shared.NextSingle();
    }

    public static float RandomFloat(float min, float max)
    {
        return min + (max - min) * RandomFloat();
    }

    public static int RgbToInt(Vector3 rgb)
    {
        rgb *= 255;
        int i = (int) rgb.X;
        i = (i << 8) + (int) rgb.Y;
        i = (i << 8) + (int) rgb.Z;

        return i;
    }
    
    public static Vector3 IntToRgb(int value)
    {
        var red =   ( value >> 16 ) & 255;
        var green = ( value >> 8  ) & 255;
        var blue =  ( value >> 0  ) & 255;
        
        return new Vector3(red, green, blue); 
    }

    public static Vector3 RandomVector()
    {
        var random = new Random();

        return new Vector3(random.NextSingle(), random.NextSingle(), random.NextSingle());
    }
    
    public static Vector3 RandomVector(float min, float max)
    {
        return new Vector3(Utils.RandomFloat(min, max), Utils.RandomFloat(min, max), Utils.RandomFloat(min, max));
    }
    
    public static Vector3 RandomVectorNormalized()
    {
        // Randomly create a vector, if it lies within the unit sphere, we normalize it.
        while (true)
        {
            var p = RandomVector(-1, 1);
            var lengthSquared = p.LengthSquared();
            if (1e-80 < lengthSquared && lengthSquared <= 1)
            {
                return p / (float) Math.Sqrt(lengthSquared);
            }
        }
    }
    
    public static Vector3 RandomVectorHemisphere(Vector3 normal)
    {
        var onNormalizedSphere = RandomVectorNormalized();

        if (Vector3.Dot(onNormalizedSphere, normal) > 0f)
            return onNormalizedSphere;
        
        return -onNormalizedSphere;
    }
}
