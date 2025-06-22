using Gpu.OpenCL;

namespace Gpu.Pipeline;

public class LogicPhase : Phase
{
    public Buffer DebugBuffer { get; private set; }

    /// <summary>
    /// Accumulates radiance contributions and queues new ray generations.
    /// </summary>
    /// <param name="manager">OpenCLManager used for buffer creation</param>
    /// <param name="path">Path to OpenCL program</param>
    /// <param name="kernel">Kernel to execute</param>
    /// <param name="shadowRaysBuffer">Buffer of shadow rays to trace</param>
    /// <param name="pathStatesBuffer">The buffer containing the previous intersection tests</param>
    /// <param name="sceneInfoBuffer">Buffer that contains scene info</param>
    /// <param name="spheresBuffer">Buffer that contains spheres used for intersection calculations</param>
    /// <param name="trianglesBuffer">Buffer that contains triangles used for intersection calculations</param>
    public LogicPhase(
        OpenCLManager manager, 
        string path,
        string kernel,
        Buffer queueStates,
        Buffer extendRayQueue,
        Buffer shadeDiffuseQueue,
        Buffer shadeReflectiveQueue,
        Buffer shadowRayQueue,
        Buffer pathStatesBuffer,
        Buffer materialsBuffer,
        Buffer sceneInfoBuffer,
        Buffer spheresBuffer,
        Buffer trianglesBuffer,
        Buffer primaryRaysBuffer,
        Buffer imageBuffer)
    {
        DebugBuffer = new ReadWriteBuffer<ClFloat3>(manager, new ClFloat3[pathStatesBuffer.GetLength()]);

        manager.AddProgram(path, "logic.cl")
            .AddBuffers(DebugBuffer)
            .AddKernel("logic.cl",
                kernel,
                queueStates,
                extendRayQueue,
                shadeDiffuseQueue,
                shadeReflectiveQueue,
                shadowRayQueue,
                pathStatesBuffer,
                materialsBuffer,
                sceneInfoBuffer, 
                spheresBuffer,
                trianglesBuffer,
                imageBuffer,
                primaryRaysBuffer,
                DebugBuffer);

        KernelId = manager.GetKernelId(kernel);
    }
}
