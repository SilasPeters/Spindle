using System.Numerics;
using System.Runtime.InteropServices;

namespace Gpu.OpenCL;

[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct ClFloat3
{
    public float X; // 4 bytes
    public float Y; // 4 bytes
    public float Z; // 4 bytes
    // 4 bytes empty

    public static ClFloat3 FromVector3(Vector3 v) => new() { X = v.X, Y = v.Y, Z = v.Z };

    public Vector3 ToVector3() => new(X, Y, Z);

    public override string ToString() => $"<{X}, {Y}, {Z}>";
}

[StructLayout(LayoutKind.Sequential, Size = 64)]
public struct ClTriangle
{
    public ClFloat3 V1; // 16 bytes
    public ClFloat3 V2; // 16 bytes
    public ClFloat3 V3; // 16 bytes
    public uint Material; // 4 bytes
    // 12 empty bytes

    public override string ToString() =>
        $"ClTriangle (V1: {V1}, V2: {V2}, V3: {V3}, Material: {Material})";
}

[StructLayout(LayoutKind.Sequential, Size = 32)]
public struct ClSphere
{
    public ClFloat3 Position; // 16 bytes
    public float Radius; // 4 bytes
    public uint Material; // 4 bytes
    // 8 bytes empty

    public override string ToString() =>
        $"ClSphere: (Pos: {Position}, Radius: {Radius}, Material: {Material}>";
}

[StructLayout(LayoutKind.Sequential, Size = 96)]
public struct ClPathState
{
    public ClFloat3 Direction; // 16 bytes
    public ClFloat3 Origin; // 16 bytes
    public ClFloat3 AccumulatedLuminance; // 16 bytes
    public ClFloat3 LatestLuminanceSample; // 16 bytes
    public ClFloat3 AveragedSamples; // 16 bytes
    public float T; // 4 bytes
    public uint MaterialId; // 4 bytes (is luxury data, but we have bytes unused anyway)
    public uint ObjectId; // 4 bytes
    public float SampleCount; // 4 bytes

    public override string ToString() =>
        $"ClPathState: (Origin {Origin}, Dir {Direction}, T: {T}, ObjectId: {ObjectId}, " +
        $"AccumulatedLuminance: {AccumulatedLuminance}, LatestLuminanceSample: {LatestLuminanceSample}, " +
        $"MaterialId: {MaterialId}, SampleCount: {SampleCount})";
}

[StructLayout(LayoutKind.Sequential, Size = 32)]
public struct ClSceneInfo
{
    public ClFloat3 CameraPosition; // 16 bytes
    public int NumSpheres; // 4 bytes
    public int NumTriangles; // 4 bytes
    // 8 bytes unused

    public override string ToString() =>
        $"ClSceneInfo: (Spheres: {NumSpheres}, Triangles: {NumTriangles}, CameraPosition: {CameraPosition}";
}


[StructLayout(LayoutKind.Sequential, Size = 32)]
public struct ClMaterial
{
    public ClFloat3 ColorTimesAlbedo; // 16 bytes
    public MaterialType Type; // 4 bytes
    // 12 empty bytes

    public override string ToString() => $"ClMaterial: Type: {Type}, ColorTimesAlbedo: {ColorTimesAlbedo})";
}

public enum MaterialType : uint // 4 bytes
{
    Diffuse = 1,
    Reflective = 2
}

[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct ClQueueStates
{
    public uint ExtendRayLength; // 4 bytes
    public uint ShadeDiffuseLength; // 4 bytes
    public uint ShadeReflectiveLength; // 4 bytes
    public uint ShadowRayLength; // 4 bytes

    /// <inheritdoc />
    public override string ToString() =>
        $"{nameof(ExtendRayLength)}: {ExtendRayLength}, " +
        $"{nameof(ShadeDiffuseLength)}: {ShadeDiffuseLength}, " +
        $"{nameof(ShadowRayLength)}: {ShadowRayLength}";
}
