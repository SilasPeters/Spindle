using Engine.Cameras;
using Engine.Geometry;
using Engine.Scenes;
using System.Numerics;

namespace Gpu.OpenCL;

public class BufferConverter
{
    /// <summary>
    /// This method converts the scene originally designed for the CPU path tracer so that it can be used in the GPU path tracer
    /// </summary>
    public static ClSceneBuffers ConvertSceneToBuffers(OpenCLManager manager, Scene scene, Camera camera)
    {
        int imageWidth = camera.ImageSize.Width;
        int imageHeight = camera.ImageSize.Height;

        var sceneInfo = new ClSceneInfo
        {
            NumSpheres = scene.Objects.OfType<Sphere>().Count(),
            NumTriangles = scene.Objects.OfType<Triangle>().Count(),
            CameraPosition = ClFloat3.FromVector3(camera.Position), // TODO no longer used?
        };
        
        // Gather all used materials
        List<ClMaterial> materials = new();
        Dictionary<Engine.Materials.Material, uint> materialIndex = new();
        foreach (var obj in scene.Objects)
        {
            if (materialIndex.ContainsKey(obj.Material)) continue;

            ClMaterial material;

            switch (obj.Material)
            {
                case Engine.Materials.Diffuse diffuseMat:
                    material.Type = MaterialType.Diffuse;
                    material.ColorTimesAlbedo = ClFloat3.FromVector3(diffuseMat.Color * diffuseMat.Albedo);
                    break;
                case Engine.Materials.Reflective reflectiveMat:
                    material.Type = MaterialType.Reflective;
                    material.ColorTimesAlbedo = ClFloat3.FromVector3(reflectiveMat.Color  * reflectiveMat.Albedo);
                    break;
                default:
                    throw new Exception("Material type not yet supported: " + obj.Material.GetType().Name);
            }

            materialIndex.Add(obj.Material, (uint)materials.Count);
            materials.Add(material);
        }

        // Process all triangles
        var triangles = new List<ClTriangle>();
        foreach (var t in scene.Objects.OfType<Triangle>())
        {
            triangles.Add(new ClTriangle
            {
                V1 = ClFloat3.FromVector3(t.Vertex1),
                V2 = ClFloat3.FromVector3(t.Vertex2),
                V3 = ClFloat3.FromVector3(t.Vertex3),
                // Normal = ClFloat3.FromVector3(t._normal),
                Material = materialIndex[t.Material],
            });
        }

        var spheres = new List<ClSphere>();

        foreach (var s in scene.Objects.OfType<Sphere>())
        {
            spheres.Add(new ClSphere
            {
                Position = ClFloat3.FromVector3(s.Position),
                Radius = s.Radius,
                Material = materialIndex[s.Material],
            });
        }


        // Precompute all primary rays
        ClPathState[] primaryRays = new ClPathState[imageWidth * imageHeight];
        for (int y = 0; y < imageHeight; y++) for (int x = 0; x < imageWidth; x++)
        {
            int i = x + y * imageWidth;
            Vector3 camToPixel = camera.FrustumTopLeft
                                 + ((float)x / (imageWidth - 1)) * camera.FrustumHorizontal
                                 - ((float)y / (imageHeight - 1)) * camera.FrustumVertical;

            primaryRays[i] = new ClPathState()
            {
                Origin = ClFloat3.FromVector3(camera.Position),
                Direction = ClFloat3.FromVector3(Vector3.Normalize(camToPixel)),
                AccumulatedLuminance = ClFloat3.FromVector3(new Vector3(1, 1, 1)),
                LatestLuminanceSample = ClFloat3.FromVector3(new Vector3(-1, -1, -1)), // Marker for that we have never checked it yet
            };
        }

        return new ClSceneBuffers
        {
            SceneInfo = new ReadWriteBuffer<ClSceneInfo>(manager, new [] { sceneInfo }),
            Spheres = new ReadWriteBuffer<ClSphere>(manager, spheres.ToArray()),
            Triangles = new ReadWriteBuffer<ClTriangle>(manager, triangles.ToArray()),
            Materials = new ReadOnlyBuffer<ClMaterial>(manager, materials.ToArray()),
            PrimaryRays = new ReadOnlyBuffer<ClPathState>(manager,  primaryRays.ToArray()),
        };
    }
}

public struct ClSceneBuffers
{
    public Buffer SceneInfo { get; set; }
    public Buffer Triangles { get; set; }
    public Buffer Spheres { get; set; }
    public Buffer Materials { get; set; }
    public Buffer PrimaryRays { get; set; }
}
