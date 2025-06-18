using Engine.Cameras;
using Engine.Renderers;
using Gpu.Pipeline;
using System.Drawing;
using System.Numerics;

namespace Gpu.Cameras;

public class OpenCLCamera : Camera
{
    public WavefrontPipeline? Pipeline { get; private set; }

    public OpenCLCamera(Vector3 position, Vector3 up, Vector3 front, Size imageSize, float FOV, int maxDepth)
        : base(position, up, front, imageSize, FOV, maxDepth)
    {
    }

    public override void RenderShot(IRenderer renderer, in Span<int> pixels)
    {
        Pipeline ??= new WavefrontPipeline(renderer.Scene, this);

        Pipeline.Execute(in pixels);
    }
}
