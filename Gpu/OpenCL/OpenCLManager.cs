using Engine.Cameras;
using Engine.Scenes;
using Gpu.OpenCL;
using Silk.NET.OpenCL;

namespace Gpu;

public class OpenCLManager
{
    public CL Cl { get; private set; }
    public nint Platform { get; private set; }
    public Context Context { get; private set; }
    public CommandQueue Queue { get; private set; }
    public List<Kernel> Kernels;
    public List<ClProgram> Programs;
    public List<ClProgram> UtilsPrograms;
    public Memory Memory;
    public nuint[] GlobalSize { get; private set; }
    public nuint[] LocalSize { get; private set; }


    public unsafe OpenCLManager()
    {
        Cl = CL.GetApi();
        Context = new Context(this);
        Programs = new List<ClProgram>();
        UtilsPrograms = new List<ClProgram>();
        Kernels = new List<Kernel>();
        Queue = new CommandQueue(this);
        Memory = new Memory();
    }

    public unsafe OpenCLManager AddUtilsProgram(string path, string name)
    {
        var utilsProgram = new ClProgram(this, path, name);

        Cl.CompileProgram(
            utilsProgram.Id,
            1,
            Context.Device.Id,
            "",
            0,
            null,
            new string[] {},
            null,
            null);
        
        UtilsPrograms.Add(new ClProgram(this, path, name));
        return this;
    }
    
    public unsafe OpenCLManager AddProgram(string path, string name)
    {
        var program = new ClProgram(this, path, name);
        var headerIds = UtilsPrograms.Select(p => p.Id).ToArray();
        var headerNames = UtilsPrograms.Select(p => p.Name).ToArray();
        fixed (
            nint* utilsPointer = headerIds)
        {
            var errNum = Cl.CompileProgram(
                program.Id,
                1,
                Context.Device.Id,
                "",
                (uint)UtilsPrograms.Count,
                utilsPointer,
                headerNames,
                null,
                null);

            if (errNum != (int) ErrorCodes.Success)
            {
                Console.WriteLine("Error code: " + errNum);
                _ = Cl.GetProgramBuildInfo(program.Id, Context.Device.Id, ProgramBuildInfo.BuildLog, 0, null, out nuint buildLogSize);
                byte[] log = new byte[buildLogSize / sizeof(byte)];
                fixed (void* pValue = log)
                {
                    Cl.GetProgramBuildInfo(program.Id,  Context.Device.Id, ProgramBuildInfo.BuildLog, buildLogSize, pValue, null);
                }
                string? build_log = System.Text.Encoding.UTF8.GetString(log);

                Console.WriteLine("Error in kernel: ");
                Console.WriteLine("=============== OpenCL Program Build Info ================");
                Console.WriteLine(build_log);
                Console.WriteLine("==========================================================");

                Cl.ReleaseProgram(program.Id);
                throw new Exception($"Error COMPILING program {name}");
            }

            var final = Cl.LinkProgram(
                Context.Id,
                1,
                Context.Device.Id,
                "",
                1,
                program.Id,
                null,
                null,
                null);

            if (final == IntPtr.Zero)
            {
                Cleanup();
                throw new Exception("Failed to link program");
            }
            
            Programs.Add(new ClProgram(final, name));
            
        }

        return this;
    }

    public OpenCLManager AddKernel(string programName, string name, params Buffer[] arguments)
    {
        var program = Programs.Find(p => p.Name == programName);

        if (program == null)
        {
            throw new Exception("Could not find program with name: " + programName);
        }
        var kernel = new Kernel(this, program, name);
        kernel.SetArguments(this, arguments);
        Kernels.Add(kernel);

        return this;
    }
    
    public OpenCLManager AddBuffers(params Buffer[] buffers)
    {
        Memory.AddBuffers(buffers);
        return this;
    }

    public OpenCLManager AddBuffer(Buffer buffer)
    {
        Memory.AddBuffer(buffer);
        return this;
    }

    public OpenCLManager SetWorkSize(nuint[] global, nuint[] local)
    {
        GlobalSize = global;
        LocalSize = local;
        return this;
    }

    public unsafe void ReadBufferToHost<T>(Buffer buffer, in Span<T> output) where T : unmanaged
    {
        if ((nuint)(output.Length * sizeof(T)) != buffer.GetSize())
            throw new Exception("Output buffer not of same size as buffer to be read from GPU.");

        fixed (void* pValue = output)
        {
            // Read the output buffer back to the Host
            var err = Cl.EnqueueReadBuffer(Queue.Id, buffer.Id, true, 0, buffer.GetSize(), pValue, 0, null, null);
            
            if (err != (int)ErrorCodes.Success)
            {
                throw new Exception($"Error {err}: enqueuing read buffer");
            }
            err = Cl.Finish(Queue.Id);
            
            if (err != (int)ErrorCodes.Success)
            {
                throw new Exception($"Error {err}: finishing queue");
            }
        }
    }
    
    public void Cleanup()
    {
        if (Memory != null)
        {
            Memory.Buffers.ForEach(b => Cl.ReleaseMemObject(b.Id));
        }
        
        Cl.ReleaseCommandQueue(Queue.Id);
        Kernels.ForEach(k => Cl.ReleaseKernel(k.Id));
        Programs.ForEach(p => Cl.ReleaseProgram(p.Id));
        UtilsPrograms.ForEach(p => Cl.ReleaseProgram(p.Id));

        Cl.ReleaseContext(Context.Id);
    }

    public nint GetKernelId(string kernel)
    {
        return this.Kernels
            .Where(k => k.Name == kernel)
            .Select(k => k.Id)
            .First();
    }
}
