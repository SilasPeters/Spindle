using System.Drawing;
using Engine.Cameras;
using System.Numerics;

namespace Renderer.Display;

/// <summary>
/// Represents a camera and his texture, a single unit to be displayed
/// </summary>
public class CameraSlot
{
    public Camera        Camera        { get; init; }
    public OpenGLTexture Texture       { get; init; } // TODO: not every display manager uses OpenGL
    public Rectangle     DisplayRegion { get; private set; }
    public float[]       Vertices      { get; private set; }

    public CameraSlot(Camera camera, OpenGLTexture texture, Rectangle displayRegion)
    {
        Camera = camera;
        Texture = texture;
        DisplayRegion = displayRegion;
    }

    public void Update(Point point, Size cameraSlotSize, Size displaySize)
    {
        Texture.SetSize(cameraSlotSize);
        Camera.SetImageSize(cameraSlotSize);
        DisplayRegion = new Rectangle(point, cameraSlotSize);
        
        Vector2 topLeft = screenCoordinatesToOpenGlCoordinates(new Vector2(DisplayRegion.Left, DisplayRegion.Top));
        Vector2 bottomRight = screenCoordinatesToOpenGlCoordinates(new Vector2(DisplayRegion.Right, DisplayRegion.Bottom));

        Vertices =
        [
                //          aPosition        |    aTexCoords
                bottomRight.X, bottomRight.Y, 0.0f,  1.0f, 1.0f,
                // 1.0f,        -1.0f,        0.0f,  1.0f, 1.0f,
                bottomRight.X, topLeft.Y,     0.0f,  1.0f, 0.0f,
                // 1.0f,       1.0f,          0.0f,  1.0f, 0.0f,
                topLeft.X,     topLeft.Y,     0.0f,  0.0f, 0.0f,
                // -1.0f,      1.0f,          0.0f,  0.0f, 0.0f,
                topLeft.X,     bottomRight.Y, 0.0f,  0.0f, 1.0f
                // -1.0f,      -1.0f,         0.0f,  0.0f, 1.0f
        ];

        return;
        
        Vector2 screenCoordinatesToOpenGlCoordinates(Vector2 screenPosition)
        {
            Vector2 displaySpace = new(displaySize.Width, displaySize.Height);
            return (screenPosition / displaySpace * 2 - Vector2.One) * new Vector2(1, -1); // The last vector2 flips the image to meet OpenGL standards
        }
    }

    public void Deconstruct(out Camera camera, out OpenGLTexture texture, out Rectangle displayRegion, out float[] vertices)
    {
        camera = Camera;
        texture = Texture;
        displayRegion = DisplayRegion;
        vertices = Vertices;
    }
}
