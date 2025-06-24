using Engine.Scenes;
using Gpu.Cameras;
using Gpu.OpenCL;
using Silk.NET.OpenCL;

namespace Gpu.Pipeline;

public class WavefrontPipeline
{
    public OpenCLManager Manager { get; private set; }
    public OpenCLCamera  Camera  { get; private set; }

    public nuint[]                        GlobalSize           { get; set; }
    public nuint[]                        LocalSize            { get; set; }
    public ClSceneBuffers                 SceneBuffers         { get; private set; }
    public ReadWriteBuffer<uint>          RandomStatesBuffer   { get; private set; }
    public LogicPhase                     LogicPhase           { get; private set; }
    public ShadeDiffusePhase              ShadeDiffusePhase    { get; private set; }
    public ShadeReflectivePhase           ShadeReflectivePhase { get; private set; }
    public ExtendPhase                    ExtendPhase          { get; private set; }
    public ShadowPhase                    ShadowPhase          { get; private set; }
    public ReadWriteBuffer<int>           ImageBuffer          { get; private set; }
    public ReadWriteBuffer<uint>          ExtendRayQueue       { get; private set; }
    public ReadWriteBuffer<uint>          ShadeDiffuseQueue    { get; private set; }
    public ReadWriteBuffer<uint>          ShadeReflectiveQueue { get; private set; }
    public ReadWriteBuffer<uint>          ShadowRayQueue       { get; private set; }
    public ReadWriteBuffer<ClQueueStates> QueueStates          { get; private set; }
    public Buffer                         PathStates           { get; private set; }
    public Buffer                         DebugBuffer          { get; private set; }

    private readonly ClQueueStates[] _queueStatesResult;

    public WavefrontPipeline(
        Scene scene, 
        OpenCLCamera camera)
    {
        Manager = new OpenCLManager();
        Camera = camera;
        SceneBuffers = BufferConverter.ConvertSceneToBuffers(Manager, scene, camera);

        GlobalSize = [(nuint)camera.ImageSize.Width, (nuint)camera.ImageSize.Height];
        LocalSize = [32, 32]; // TODO: let OpenCL decide by passing NULL, NULL (Or just NULL?)

        // Find number of rays, used for calculating buffer sizes
        int numOfRays = camera.ImageSize.Width * camera.ImageSize.Height;

        // Create buffer with random seeds
        Random random = new(20283497); // TODO: supply custom seed
        uint[] randomStates = Enumerable.Range(0, numOfRays).Select(_ => (uint) random.Next()).ToArray();
        RandomStatesBuffer = new ReadWriteBuffer<uint>(Manager, randomStates);

        // Add structs and functions that will be bound with every program
        Manager.AddUtilsProgram("structs.h", "structs.h");
        Manager.AddUtilsProgram("random.cl", "random.cl");
        Manager.AddUtilsProgram("utils.cl", "utils.cl");

        // Prepare output buffers
        ImageBuffer = new ReadWriteBuffer<int>(Manager, new int[numOfRays]);
        _queueStatesResult = new ClQueueStates[1];

        // Queue of 4 MB can hold 1_000_000 path state indices
        QueueStates = new ReadWriteBuffer<ClQueueStates>(Manager, new[] { new ClQueueStates() });
        ExtendRayQueue = new ReadWriteBuffer<uint>(Manager,       new uint[4_000_000 / sizeof(uint)]);
        ShadeDiffuseQueue = new ReadWriteBuffer<uint>(Manager,    new uint[4_000_000 / sizeof(uint)]);
        ShadeReflectiveQueue = new ReadWriteBuffer<uint>(Manager, new uint[4_000_000 / sizeof(uint)]);
        ShadowRayQueue = new ReadWriteBuffer<uint>(Manager,       new uint[4_000_000 / sizeof(uint)]);

        PathStates = new ReadWriteBuffer<ClPathState>(Manager, new ClPathState[numOfRays]);
        DebugBuffer = new ReadWriteBuffer<ClFloat3>(Manager, new ClFloat3[numOfRays]);

        // Add buffers to the manager
        Manager.AddBuffers(
            SceneBuffers.SceneInfo,
            SceneBuffers.Spheres,
            SceneBuffers.Triangles,
            SceneBuffers.Materials,
            RandomStatesBuffer,
            ImageBuffer,
            DebugBuffer,
            QueueStates,
            ExtendRayQueue,
            ShadeDiffuseQueue,
            ShadowRayQueue,
            PathStates);

        // Define pipeline dataflow (connect pipes)

        ShadeDiffusePhase = new ShadeDiffusePhase(
            Manager, "shade_diffuse.cl", "shade_diffuse",
            QueueStates,
            ShadeDiffuseQueue,
            ExtendRayQueue,
            RandomStatesBuffer,
            PathStates,
            SceneBuffers.Spheres);

        ShadeReflectivePhase = new ShadeReflectivePhase(
            Manager, "shade_reflective.cl", "shade_reflective",
            QueueStates,
            ShadeReflectiveQueue,
            ExtendRayQueue,
            ShadowRayQueue,
            RandomStatesBuffer,
            PathStates,
            SceneBuffers.Spheres);

        ExtendPhase = new ExtendPhase(
            Manager, "extend.cl", "extend",
            SceneBuffers.SceneInfo,
            SceneBuffers.Spheres,
            SceneBuffers.Triangles,
            QueueStates,
            ExtendRayQueue,
            PathStates);

        ShadowPhase = new ShadowPhase(
            Manager, "shadow.cl", "shadow",
            SceneBuffers.SceneInfo,
            QueueStates,
            ShadowRayQueue,
            PathStates,
            SceneBuffers.Spheres,
            SceneBuffers.Triangles);

        LogicPhase = new LogicPhase(
            Manager, "logic.cl", "logic",
            QueueStates,
            ExtendRayQueue,
            ShadeDiffuseQueue,
            ShadeReflectiveQueue,
            ShadowRayQueue,
            PathStates,
            SceneBuffers.Materials,
            SceneBuffers.SceneInfo,
            SceneBuffers.Spheres,
            SceneBuffers.Triangles,
            SceneBuffers.PrimaryRays,
            ImageBuffer);
    }

    public void Execute(in Span<int> result)
    {
        // Performs one iteration of the Wavefront implementation
        // TODO: we could let the logic kernel call other kernels for less IO

        // NOTE: Setting warpSize to something like 32 ensures that only multiples of 32 are dequeued and processed,
        // ensuring high occupancy. However, it was difficult to not run the logic kernel over already enqueued pixels.
        // Thus we set it to 1 for now, so in some scenarios some rays are checked twice.
        const uint warpSize = 1u; // TODO: let OpenCL decide what's best based on GPU

        Span<ClQueueStates> queueStatesResultSpan = _queueStatesResult.AsSpan();

        // Logic phase
        LogicPhase.EnqueueExecute(Manager, GlobalSize, LocalSize);

        // Manager.EnqueueReadBufferToHost(GeneratePhase.PathStates, out ClPathState[] pathStates0);
        // Manager.EnqueueReadBufferToHost(ExtendRayQueue, out uint[] extendRayQueue0);
        // Manager.EnqueueReadBufferToHost(ShadeDiffuseQueue, out uint[] shadeQueue0);
        // Manager.EnqueueReadBufferToHost(ShadowRayQueue, out uint[] shadowRayQueue0);
        // Manager.EnqueueReadBufferToHost(GeneratePhase.DebugBuffer, out ClFloat3[] generateDebug0);
        // Manager.EnqueueReadBufferToHost(ExtendPhase.DebugBuffer, out ClFloat3[] extendDebug0);
        // Manager.EnqueueReadBufferToHost(LogicPhase.DebugBuffer, out ClFloat3[] logicDebug0);
        // Manager.EnqueueReadBufferToHost(ShadeDiffusePhase.DebugBuffer, out ClFloat3[] shadeDebug0);

        // Shade diffuse phase, shade reflective phase, and shadow phase in parallel. They do not write to shared data.
        Manager.EnqueueReadBufferToHost(QueueStates, in queueStatesResultSpan);
        Manager.FinishQueue();

        // Based on states of queues, set kernel size (let no thread be idle)
        uint queuedShadeDiffuseCount = queueStatesResultSpan[0].ShadeDiffuseLength;
        if (queuedShadeDiffuseCount > 0)
        {
            uint workItems;
            uint localSize;
            if (queuedShadeDiffuseCount < warpSize)
            {
                workItems = queuedShadeDiffuseCount;
                localSize = queuedShadeDiffuseCount;
            }
            else // Find the biggest multiple of warpSize
            {
                workItems = queuedShadeDiffuseCount / warpSize * warpSize;
                localSize = warpSize;
            }
            ShadeDiffusePhase.EnqueueExecute(Manager, new nuint[] { workItems }, new nuint[] { localSize }, dimensions: 1);
        }

        // Manager.EnqueueReadBufferToHost(GeneratePhase.PathStates, out ClPathState[] pathStates2);
        // Manager.EnqueueReadBufferToHost(ExtendRayQueue, out uint[] extendRayQueue2);
        // Manager.EnqueueReadBufferToHost(ShadeDiffuseQueue, out uint[] shadeQueue2);
        // Manager.EnqueueReadBufferToHost(ShadowRayQueue, out uint[] shadowRayQueue2);
        // Manager.EnqueueReadBufferToHost(GeneratePhase.DebugBuffer, out ClFloat3[] generateDebug2);
        // Manager.EnqueueReadBufferToHost(ExtendPhase.DebugBuffer, out ClFloat3[] extendDebug2);
        // Manager.EnqueueReadBufferToHost(LogicPhase.DebugBuffer, out ClFloat3[] logicDebug2);
        // Manager.EnqueueReadBufferToHost(ShadeDiffusePhase.DebugBuffer, out ClFloat3[] shadeDebug2);

        // Shade reflective phase
        // Based on states of queues, set kernel size (let no thread be idle)
        uint queuedShadeReflectiveCount = queueStatesResultSpan[0].ShadeReflectiveLength;
        if (queuedShadeReflectiveCount > 0)
        {
            uint workItems;
            uint localSize;
            if (queuedShadeReflectiveCount < warpSize)
            {
                workItems = queuedShadeReflectiveCount;
                localSize = queuedShadeReflectiveCount;
            }
            else // Find the biggest multiple of warpSize
            {
                workItems = queuedShadeReflectiveCount / warpSize * warpSize;
                localSize = warpSize;
            }
            ShadeReflectivePhase.EnqueueExecute(Manager, new nuint[] { workItems }, new nuint[] { localSize }, dimensions: 1);
        }

        // Manager.EnqueueReadBufferToHost(GeneratePhase.PathStates, out ClPathState[] pathStates3);
        // Manager.EnqueueReadBufferToHost(ExtendRayQueue, out uint[] extendRayQueue3);
        // Manager.EnqueueReadBufferToHost(ShadeDiffuseQueue, out uint[] shadeQueue3);
        // Manager.EnqueueReadBufferToHost(ShadowRayQueue, out uint[] shadowRayQueue3);
        // Manager.EnqueueReadBufferToHost(GeneratePhase.DebugBuffer, out ClFloat3[] generateDebug3);
        // Manager.EnqueueReadBufferToHost(ExtendPhase.DebugBuffer, out ClFloat3[] extendDebug3);
        // Manager.EnqueueReadBufferToHost(LogicPhase.DebugBuffer, out ClFloat3[] logicDebug3);
        // Manager.EnqueueReadBufferToHost(ShadeDiffusePhase.DebugBuffer, out ClFloat3[] shadeDebug3);

        // Shadow phase
        // Based on states of queues, set kernel size (let no thread be idle)
        uint queuedShadowRayCount = queueStatesResultSpan[0].ShadowRayLength;
        if (queuedShadowRayCount > 0)
        {
            uint workItems;
            uint localSize;
            if (queuedShadowRayCount < warpSize)
            {
                workItems = queuedShadowRayCount;
                localSize = queuedShadowRayCount;
            }
            else // Find the biggest multiple of warpSize
            {
                workItems = queuedShadowRayCount / warpSize * warpSize;
                localSize = warpSize;
            }
            ShadowPhase.EnqueueExecute(Manager, new nuint[] { workItems }, new nuint[] { localSize }, dimensions: 1);
        }

        // Manager.EnqueueReadBufferToHost(SceneBuffers.Materials, out ClMaterial[] materials);
        // Manager.EnqueueReadBufferToHost(SceneBuffers.SceneInfo, out ClSceneInfo[] sceneInfos);
        // Manager.EnqueueReadBufferToHost(SceneBuffers.Spheres, out ClSphere[] spheres);
        // Manager.EnqueueReadBufferToHost(GeneratePhase.PathStates, out ClPathState[] pathStates);
        // Manager.EnqueueReadBufferToHost(QueueStates, out ClQueueStates[] queueStatesAfterwards);
        // Manager.EnqueueReadBufferToHost(ExtendRayQueue, out uint[] extendRayQueue);
        // Manager.EnqueueReadBufferToHost(ShadeDiffuseQueue, out uint[] shadeQueue);
        // Manager.EnqueueReadBufferToHost(ShadowRayQueue, out uint[] shadowRayQueue);
        // Manager.EnqueueReadBufferToHost(GeneratePhase.DebugBuffer, out ClFloat3[] generateDebug);
        // Manager.EnqueueReadBufferToHost(ExtendPhase.DebugBuffer, out ClFloat3[] extendDebug);
        // Manager.EnqueueReadBufferToHost(LogicPhase.DebugBuffer, out ClFloat3[] logicDebug);
        // Manager.EnqueueReadBufferToHost(ShadeDiffusePhase.DebugBuffer, out ClFloat3[] shadeDebug);


        // Extend phase
        Manager.EnqueueReadBufferToHost(QueueStates, in queueStatesResultSpan);
        Manager.FinishQueue();
        // Based on states of queues, set kernel size (let no thread be idle)
        uint queuedExtendRayCount = queueStatesResultSpan[0].ExtendRayLength;
        if (queuedExtendRayCount > 0)
        {
            uint workItems;
            uint localSize;
            if (queuedExtendRayCount < warpSize)
            {
                workItems = queuedExtendRayCount;
                localSize = queuedExtendRayCount;
            }
            else // Find the biggest multiple of warpSize
            {
                workItems = queuedExtendRayCount / warpSize * warpSize;
                localSize = warpSize;
            }
            ExtendPhase.EnqueueExecute(Manager, new nuint[] { workItems }, new nuint[] { localSize }, dimensions: 1);
        }

        // Manager.EnqueueReadBufferToHost(GeneratePhase.PathStates, out ClPathState[] pathStates4);
        // Manager.EnqueueReadBufferToHost(ExtendRayQueue, out uint[] extendRayQueue4);
        // Manager.EnqueueReadBufferToHost(ShadeDiffuseQueue, out uint[] shadeQueue4);
        // Manager.EnqueueReadBufferToHost(ShadowRayQueue, out uint[] shadowRayQueue4);
        // Manager.EnqueueReadBufferToHost(GeneratePhase.DebugBuffer, out ClFloat3[] generateDebug3);
        // Manager.EnqueueReadBufferToHost(ExtendPhase.DebugBuffer, out ClFloat3[] extendDebug3);
        // Manager.EnqueueReadBufferToHost(LogicPhase.DebugBuffer, out ClFloat3[] logicDebug3);
        // Manager.EnqueueReadBufferToHost(ShadeDiffusePhase.DebugBuffer, out ClFloat3[] shadeDebug3);


        // Display current state
        Manager.EnqueueReadBufferToHost(ImageBuffer, in result);
        Manager.FinishQueue();
    }
}
