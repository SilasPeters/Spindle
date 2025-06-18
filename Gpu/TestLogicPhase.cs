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
    public static void TestLogicPhase()
    {
        // Prepare input data
        const int numberOfRays = 16;

        ClMaterial[] materials = {
            new()
            {
                Type = MaterialType.Diffuse,
                ColorTimesAlbedo = new ClFloat3 { X = 20 * .78f, Y = 30 * .78f, Z = 40 * .78f },
            },
            new()
            {
                Type = MaterialType.Diffuse,
                ColorTimesAlbedo = new ClFloat3 { X = 25 * .69f, Y = 35 * .69f, Z = 45 * .69f },
            },
            new()
            {
                Type = MaterialType.Reflective,
                ColorTimesAlbedo = new ClFloat3 { X = 100, Y = 101, Z = 102 },
            }
        };

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

        ClPathState[] pathStates = Enumerable.Repeat(
            new ClPathState
            {
                Direction = new ClFloat3 { X = 0, Y = 0, Z = 1 },
                Origin = new ClFloat3 { X = 0, Y = 0, Z = 0.5f },
                T = 390,
                ObjectId = 1,
                SampleCount = 69420
            },
            numberOfRays).ToArray();


        // Prepare OpenCL
        OpenCLManager manager = new();

        ReadWriteBuffer<ClQueueStates> queueStates = new(manager, new[] { new ClQueueStates() }); // Set all lengths to 0
        ReadWriteBuffer<uint> shadeDiffuseQueue = new(manager, new uint[4_000_000 / sizeof(uint)]);
        ReadWriteBuffer<uint> shadeReflectiveQueue = new(manager, new uint[4_000_000 / sizeof(uint)]);
        ReadWriteBuffer<uint> extendRayQueue = new(manager, new uint[4_000_000 / sizeof(uint)]);
        ReadWriteBuffer<ClPathState> pathStatesBuffer = new(manager, pathStates);
        ReadOnlyBuffer<ClMaterial> materialsBuffer = new(manager, materials);
        ReadOnlyBuffer<ClSceneInfo> sceneInfoBuffer = new(manager, sceneInfo);
        ReadOnlyBuffer<ClSphere> sphereBuffer = new(manager, spheres);
        ReadOnlyBuffer<ClTriangle> triangleBuffer = new(manager, triangles);
        ReadOnlyBuffer<ClPathState> primaryRayBuffer = new(manager, pathStates);
        ReadWriteBuffer<uint> imageBuffer = new(manager, new uint[numberOfRays]);

        manager.AddBuffers(queueStates, shadeDiffuseQueue, shadeReflectiveQueue, extendRayQueue, pathStatesBuffer, materialsBuffer, sceneInfoBuffer, sphereBuffer, triangleBuffer, imageBuffer);
        manager.AddUtilsProgram("structs.h", "structs.h");
        manager.AddUtilsProgram("random.cl", "random.cl");
        manager.AddUtilsProgram("utils.cl", "utils.cl");
        LogicPhase phase = new(manager, "logic.cl", "logic",
            queueStates, shadeDiffuseQueue, shadeReflectiveQueue, extendRayQueue, pathStatesBuffer, materialsBuffer, sceneInfoBuffer, sphereBuffer, triangleBuffer, primaryRayBuffer, imageBuffer);

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

        manager.ReadBufferToHost(phase.DebugBuffer, out ClFloat3[] result);
        // manager.ReadBufferToHost(imageBuffer, out uint[] result);
        for (int index = 0; index < result.Length; index++)
        {
            var item = result[index];
            Console.WriteLine($"{(index + 1).ToString(),3}: {item}");
        }
    }
}
