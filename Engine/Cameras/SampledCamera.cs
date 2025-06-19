using Engine.Geometry;
using System.Drawing;
using System.Numerics;
using Engine.Renderers;
using System.Collections.Concurrent;

namespace Engine.Cameras;

public class SampledCamera : Camera
{
    public uint NumberOfSamples { get; private set; }
    public Vector3[] AveragedSamples { get; private set; }

    private ConcurrentQueue<(int, int)> queue = new();
    private int[] span;

    // ReSharper disable once InconsistentNaming
    /// <inheritdoc />
    public SampledCamera(Vector3 position, Vector3 up, Vector3 front, Size imageSize, float FOV, int maxDepth)
        : base(position, up, front, imageSize, FOV, maxDepth)
    {
        AveragedSamples = new Vector3[imageSize.Width * imageSize.Height];
        span = new int[imageSize.Width * imageSize.Height];
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

        int tileSize = 4; // Tiles size must be wholly divisible by both image width and height
        
        // Initiate the work queue
        queue.Clear(); // Clear to be safe (should be empty)
        for (var y = 0; y < this.ImageSize.Height; y += tileSize)
        {
            for (var x = 0; x < this.ImageSize.Width; x += tileSize)
            {
                queue.Enqueue((x, y));
            }
        }
        
        // Start workers
        Parallel.For((long)0, Environment.ProcessorCount, _ =>
        {
            while (!queue.IsEmpty)
            {
                int x, y;
                queue.TryDequeue(out var coords);
                (x, y) = coords;
                // Cast rays in tiles of tileSize to improve data locality
                for (var v = 0; v < tileSize; v++)
                {
                    for (var u = 0; u < tileSize; u++)
                    {
                        ref Vector3 averagedSample = ref AveragedSamples[(y + v) * this.ImageSize.Width + (x + u)];
                    
                        var newSample = Vector3.One;
                        var ray = GetRayTowardsPixel((x + u), (y + v));
                        renderer.TraceRay(ray, MaxDepth, ref newSample, ref intersectionDebugInfo);
                    
                        averagedSample = ExpandAverage(averagedSample, NumberOfSamples, newSample);

                        lock (span)
                        {
                            span[(y+v) * ImageSize.Width + (x+u)] = ColorInt.Make(averagedSample);
                        }
                    }
                }
            }
        });
        
        span.CopyTo(pixels);
        NumberOfSamples++;
    }
}
