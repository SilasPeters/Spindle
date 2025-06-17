//
// This is code to test a single GPU kernel
//

using Gpu.Cameras;
using Gpu.OpenCL;
using Gpu.Pipeline;
using Silk.NET.OpenCL;
using System.Drawing;
using System.Numerics;

namespace Gpu;

public static partial class KernelTests
{
    public static void TestGeneratePhase()
    {
        // Prepare input data
        const int windowWidth = 4;
        const int windowHeight = 4;
        const int numberOfRays = windowWidth * windowHeight;

        OpenCLCamera camera = new(
            new Vector3(0, 0, -4),
            Vector3.UnitY,
            new Vector3(0, 0, 1),
            new Size(windowWidth, windowHeight),
            60,
            20);

        ClSceneInfo[] sceneInfo = {
            new()
            {
                CameraPosition = ClFloat3.FromVector3(camera.Position),
                FrustumTopLeft = ClFloat3.FromVector3(camera.FrustumTopLeft),
                FrustumHorizontalStep = ClFloat3.FromVector3(camera.FrustumHorizontal),
                FrustumVerticalStep = ClFloat3.FromVector3(camera.FrustumVertical),
                NumSpheres = 40,
                NumTriangles = 20
            }
        };

        // Prepare OpenCL
        OpenCLManager manager = new();

        ReadOnlyBuffer<ClSceneInfo> sceneInfoBuffer = new(manager, sceneInfo);
        ReadWriteBuffer<ClQueueStates> queueStates = new(manager, new[] { new ClQueueStates() { NewRayLength = numberOfRays} }); // Set all lengths to 0
        ReadWriteBuffer<uint> newRayQueue = new(manager, Enumerable.Range(0, numberOfRays).Select(i => (uint)i).ToArray());
        ReadWriteBuffer<uint> extendRayQueue = new(manager, new uint[4_000_000 / sizeof(uint)]);

        manager.AddBuffers(sceneInfoBuffer);
        manager.AddUtilsProgram("structs.h", "structs.h");
        manager.AddUtilsProgram("random.cl", "random.cl");
        manager.AddUtilsProgram("utils.cl", "utils.cl");
        GeneratePhase phase = new(manager, "generate.cl", "generate",
            sceneInfoBuffer, queueStates, newRayQueue, extendRayQueue, numberOfRays);

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

        // manager.ReadBufferToHost(phase.DebugBuffer, out ClFloat3[] result);
        manager.ReadBufferToHost(phase.PathStates, out ClPathState[] result);
        for (int index = 0; index < result.Length; index++)
        {
            var item = result[index];
            Console.WriteLine($"{(index + 1).ToString(),3}: {item}");
        }
    }
}
