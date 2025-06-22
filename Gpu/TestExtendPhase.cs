//
// This is code to test a single GPU kernel
//

using Gpu.OpenCL;
using Gpu.Pipeline;
using Silk.NET.OpenCL;
using System.Numerics;

namespace Gpu;

public static partial class KernelTests
{
    public static void TestExtendPhase()
    {
        // Prepare input data
        const int numberOfRays = 16;

        // ClSphere[] spheres = Enumerable.Repeat(new ClSphere()
        // {
        //     Material = 3,
        //     Position = new ClFloat3 { X = 0, Y = 0, Z = 6 },
        //     Radius = 2
        // }, numberOfRays).ToArray();
        ClSphere[] spheres =
        {
            new() { Material = 3, Position = new ClFloat3 { X = 0, Y = 0, Z = 11 }, Radius = 2 },
            new() { Material = 2, Position = new ClFloat3 { X = 0, Y = 0, Z = 6 }, Radius = 2 },
            new() { Material = 1, Position = new ClFloat3 { X = 0, Y = 0, Z = -200 }, Radius = 100 },
        };

        ClTriangle[] triangles = Enumerable.Repeat(new ClTriangle()
        {
            Material = 4,
            V1 = new ClFloat3 { X = 0,  Y = 1, Z = 1 },
            V2 = new ClFloat3 { X = -1, Y = 0, Z = 0 },
            V3 = new ClFloat3 { X = 1,  Y = 0, Z = 0 },
        }, numberOfRays).ToArray();

        ClSceneInfo[] sceneInfo = { new() { NumSpheres = spheres.Length, NumTriangles = triangles.Length } };

        ClPathState[] rays = Enumerable.Repeat(
            new ClPathState
            {
                Direction = new ClFloat3 { X = 0, Y = 0, Z = 1 },
                Origin = new ClFloat3 { X = 0, Y = 0, Z = -3f },
                // T = 390,
                // ObjectId = 1
            },
            numberOfRays).ToArray();


        // Prepare OpenCL
        OpenCLManager manager = new();

        ReadOnlyBuffer<ClSceneInfo> sceneInfoBuffer = new(manager, sceneInfo);
        ReadOnlyBuffer<ClSphere> sphereBuffer = new(manager, spheres);
        ReadOnlyBuffer<ClTriangle> triangleBuffer = new(manager, triangles);
        ReadWriteBuffer<ClQueueStates> queueStates = new(manager, new[] { new ClQueueStates() }); // Set all lengths to 0
        ReadWriteBuffer<uint> extendRayQueue = new(manager, new uint[4_000_000 / sizeof(uint)]);
        ReadWriteBuffer<ClPathState> raysBuffer = new(manager, rays);

        manager.AddBuffers(sceneInfoBuffer, sphereBuffer, triangleBuffer, queueStates, extendRayQueue, raysBuffer);
        manager.AddUtilsProgram("structs.h", "structs.h");
        manager.AddUtilsProgram("random.cl", "random.cl");
        manager.AddUtilsProgram("utils.cl", "utils.cl");
        ExtendPhase phase = new(manager, "extend.cl", "extend",
            sceneInfoBuffer, sphereBuffer, triangleBuffer, queueStates, extendRayQueue, raysBuffer);

        var globalSize = new nuint[2]
        {
            (nuint)MathF.Ceiling(MathF.Sqrt(numberOfRays)),
            (nuint)MathF.Ceiling(MathF.Sqrt(numberOfRays))
        };
        var localSize = new nuint[2] { 2, 2 };

        // Execute
        phase.EnqueueExecute(manager, globalSize, localSize);
        var err = manager.Cl.Finish(manager.Queue.Id);

        if (err != (int)ErrorCodes.Success)
        {
            throw new Exception($"Error {err}: finishing queue");
        }

        ClPathState[] result = new ClPathState[1];
        // manager.EnqueueReadBufferToHost(phase.DebugBuffer, out ClFloat3[] result);
        manager.EnqueueReadBufferToHost(raysBuffer, result.AsSpan());
        for (int index = 0; index < result.Length; index++)
        {
            var item = result[index];
            Console.WriteLine($"{(index + 1).ToString(),3}: {item}");
        }
    }
}
