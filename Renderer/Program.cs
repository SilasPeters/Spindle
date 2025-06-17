using System.Drawing;
using System.Numerics;
using Engine.Cameras;
using Engine.Geometry;
using Engine.Lighting;
using Engine.Materials;
using Engine.MeshImporters;
using Engine.Renderers;
using Engine.Scenes;
using Engine.Strategies.BVH;
using Gpu.Cameras;
using Renderer.Display;

const int windowWidth = 32 * 30;
const int windowHeight = 32 * 20;
const int maxDepth = 20;
const float fov = 65f;

Console.WriteLine("Starting render");

var cameraManager = new CameraManager(new Size(windowWidth, windowHeight), CameraLayout.Matrix);
cameraManager.AddCamera(
    new SampledCamera( // CPU with BVH
    //new OpenCLCamera( // GPU
        new Vector3(0, 0, -4),
        Vector3.UnitY, 
        new Vector3(0, 0, 1),
        new Size(windowWidth, windowHeight),
        fov, 
        maxDepth)
    );

// cameraManager.AddBasicCamera(new Vector3(0, 3.5f, -15f), maxDepth, fov);
// cameraManager.AddCamera(new BasicCamera(new Vector3(1, 1, 3), Vector3.UnitY, new Vector3(-1, 0, -3), new Size(), fov, maxDepth, samples));
// cameraManager.AddCamera(new IntersectionTestsCamera(new Vector3(1, 1, 3), Vector3.UnitY, new Vector3(-1, 0, -3), new Size(), fov, maxDepth, samples,
//     displayedIntersectionsRange: 100));
// cameraManager.AddCamera(new TraversalStepsCamera(new Vector3(1, 1, 3), Vector3.UnitY, new Vector3(-1, 0, -3), new Size(), fov, maxDepth, samples,
//     displayedTraversalStepsRange: 200));

var matGround = new Diffuse(0.5f, new Vector3(0.5f, 0.8f, 0f));
var matDarkBlue = new Diffuse(0.5f, new Vector3(.1f, .2f, .5f));
var matTriangle = new Diffuse(0.3f, new Vector3(0.5f, 1.0f, 0.5f));
var matReflect = new Reflective(1f, new Vector3(1,1,1), 0f);
var matBadReflect = new Reflective(.6f, new Vector3(.4f,.6f,1), 0f);
var matBrightYellow = new Diffuse(.9f, new Vector3(0.8f, 0.8f, 0.0f));
var matRed = new Diffuse(.8f, new Vector3(0.8f, 0.0f, 0.0f));
var matBlack = new Diffuse(0f, new Vector3(0,0,0));
var matKitchenWhite = new Diffuse(.8f, new Vector3(0.8f, .8f, .8f));

var groundOrb = new Sphere(new Vector3(0, -100.5f, 1f), matGround, 100f);
var orbCentre = new Sphere(new Vector3(0f, 0, 0), matKitchenWhite, 1.5f);
var orbRight = new Sphere(new Vector3(0f,  2f, 0f), matRed, 0.5f);
var orbUp = new Sphere(new Vector3(-1.6f,  4f, 1.2f), matBrightYellow, 2f);
var orbMirror = new Sphere(new Vector3(4f,  2f, 3.2f), matReflect, 2.5f);
var orbLeft = new Sphere(new Vector3(-1.5f, -0.2f, -1f), matDarkBlue, .5f);
var orbSmall = new Sphere(new Vector3(1.45f, 0f, -1f), matBlack, .25f);
var orbBadMirror = new Sphere(new Vector3(1f,  -6.4f, -1.4f), matBadReflect, 6f);
var tri = new Triangle(
    new Vector3(-2.5f, 10f, 2f),
    new Vector3(0f, 13f, 1.2f),
    new Vector3(2.5f, 11f, 3f),
    matTriangle);

var objects = new List<Geometry> { orbBadMirror, orbLeft, orbSmall, orbCentre, orbRight, groundOrb, orbUp, orbMirror, tri };
var lights = new List<LightSource> { new Spotlight(Vector3.One, Vector3.One) };

var cuteDragonImporter = new ObjMeshImporter("Assets/cute_dragon.obj", new Vector3(0, 0, 0), matRed);
var teaPotImporter1 = new ObjMeshImporter("Assets/teapot.obj", new Vector3(-7, -2, 0), matKitchenWhite);
var teaPotImporter2 = new ObjMeshImporter("Assets/teapot.obj", new Vector3(7, -2, 0), matKitchenWhite);
var teaPotImporter3 = new ObjMeshImporter("Assets/teapot.obj", new Vector3(0, 8, 20), matKitchenWhite);
var teaPotImporter4 = new ObjMeshImporter("Assets/teapot.obj", new Vector3(-20, 40, 80), matKitchenWhite);
//var scene = new Scene(objects, lights); // Naive approach
var scene = new BvhScene(new SplitDirectionStrategy(1), objects, lights); // Uses BVH

Console.WriteLine("Done creating acceleration structure");

var renderer = new PathTracingRenderer(scene);

// IDisplay display = new PhotoDisplay(renderer, camera);
IDisplay display = args.Length > 0
    ? new PhotoDisplay(renderer, cameraManager)
    : new OpenGLDisplay(renderer, cameraManager);

display.Show(args);
