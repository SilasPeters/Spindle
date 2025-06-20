using Gpu.OpenCL;

namespace Gpu.Pipeline;

public class ShadeReflectivePhase : Phase
{
    public Buffer DebugBuffer { get; private set; }

    /// <summary>
    /// Calculates extension rays and shadow rays to trace in phases 2 and 4.
    /// Writes to three buffers, pathStates (phase 2) and shadowRays (phase 4).
    /// </summary>
    /// <param name="manager">OpenCLManager used for buffer creation</param>
    /// <param name="path">Path to OpenCL program</param>
    /// <param name="kernel">Kernel to execute</param>
    /// <param name="materials">A constant buffer containing the materials in the scene</param>
    /// <param name="randomStates">The current seeds or states for the random number generator</param>
    /// <param name="pathStates">Takes ray buffer that is used in phase 2 for intersection calculation</param>
    public ShadeReflectivePhase(
        OpenCLManager manager,
        string path,
        string kernel,
        Buffer queueStates,
        Buffer shadeReflectiveQueue,
        Buffer extendRayQueue,
        Buffer shadowRayQueue,
        Buffer randomStates,
        Buffer pathStates,
        Buffer spheres)
    {
        DebugBuffer = new ReadWriteBuffer<ClFloat3>(manager, new ClFloat3[pathStates.GetLength()]);

        manager.AddProgram(path, "shade_reflective.cl")
            .AddBuffers(DebugBuffer)
            .AddKernel(
                "shade_reflective.cl",
                kernel,
                queueStates,
                shadeReflectiveQueue,
                extendRayQueue,
                shadowRayQueue,
                randomStates,
                pathStates,
                spheres,
                DebugBuffer);

        KernelId = manager.GetKernelId(kernel);
    }
}
