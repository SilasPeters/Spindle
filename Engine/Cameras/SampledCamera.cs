using Engine.Geometry;
using System.Drawing;
using System.Numerics;
using Engine.Renderers;

namespace Engine.Cameras;

public class SampledCamera : Camera
{
    public uint NumberOfSamples { get; private set; }
    public Vector3[] AveragedSamples { get; private set; }

    // ReSharper disable once InconsistentNaming
    /// <inheritdoc />
    public SampledCamera(Vector3 position, Vector3 up, Vector3 front, Size imageSize, float FOV, int maxDepth)
        : base(position, up, front, imageSize, FOV, maxDepth)
    {
        AveragedSamples = new Vector3[imageSize.Width * imageSize.Height];
        OnTransform += () => NumberOfSamples = 0;
    }

    /// <inheritdoc />
    public override void SetImageSize(Size size)
    {
        base.SetImageSize(size);
        AveragedSamples = new Vector3[size.Width * size.Height];
        NumberOfSamples = 0;
    }

    public override void RenderShot(IRenderer renderer, in Span<int> pixels)
    {
        IntersectionDebugInfo intersectionDebugInfo = new();

        // Cast rays in tiles to improve data locality
        for (var j = 0; j < this.ImageSize.Height; j++)
        {
            for (var i = 0; i < this.ImageSize.Width; i++)
            {
                ref Vector3 averagedSample = ref AveragedSamples[j * this.ImageSize.Width + i];

                var newSample = Vector3.One;
                var ray = GetRayTowardsPixel(i, j);
                renderer.TraceRay(ray, MaxDepth, ref newSample, ref intersectionDebugInfo);

                averagedSample = ExpandAverage(averagedSample, NumberOfSamples, newSample);

                pixels[j * ImageSize.Width + i] = ColorInt.Make(averagedSample);
            }
        }

        NumberOfSamples++;
    }
}
