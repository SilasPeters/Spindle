using Silk.NET.OpenCL;

namespace Gpu;

public class CommandQueue
{
    public nint Id { get; private set; }

    public unsafe CommandQueue(OpenCLManager manager)
    {
        Id = manager.Cl.CreateCommandQueue(manager.Context.Id, manager.Context.Device.Id, CommandQueueProperties.None, null);

        if (Id == IntPtr.Zero)
        {
            manager.Cleanup();
            throw new Exception("Failed to create Command Queue");
        }
    }

    public unsafe void EnqueueKernel(
        OpenCLManager manager, 
        nint kernelId,
        nuint[] globalSize,
        nuint[] localSize,
        uint dimensions = 2)
    {
        var err = manager.Cl.EnqueueNdrangeKernel(
            Id, 
            kernelId, 
            dimensions,
            (nuint*) null, 
            globalSize, 
            localSize,
            0, 
            (nint*) null, 
            (nint*) null);

        if (err != (int) ErrorCodes.Success)
        {
            throw new Exception($"Error {err}: Kernel could not be queued");
        }
    }

    public void FinishQueue(OpenCLManager manager)
    {
        int err = manager.Cl.Finish(Id);
        
        if (err != (int)ErrorCodes.Success)
        {
            throw new Exception($"Error {err}: finishing queue");
        }
    }
}
