using System.Drawing;
using System.Numerics;
using Engine.Renderers;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Renderer.Display;

// ReSharper disable once InconsistentNaming
public class OpenGLDisplay : IDisplay
{
    /// <inheritdoc />
    public IRenderer Renderer { get; set; }

    /// <inheritdoc />
    public CameraManager CameraManager { get; set; }

    /// <inheritdoc />
    public Size DisplaySize => CameraManager.DisplaySize;

    private IWindow _window;

    private GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private uint _shaderProgram;
    private uint _textureId;

    private bool _hasFocus;
    private readonly float _movementStep;
    private readonly float _rotationStep;
    private readonly float _zoomStep;

    public OpenGLDisplay(IRenderer renderer,
        CameraManager cameraManager,
        float movementStep = .3f,
        float rotationStep = 10f,
        float zoomStep = 1.2f)
    {
        _movementStep = movementStep;
        _rotationStep = rotationStep;
        _zoomStep = zoomStep;
        Renderer = renderer;
        CameraManager = cameraManager;

        WindowOptions options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(DisplaySize.Width, DisplaySize.Height),
            Title = "Spindle, ask for parental advice before usage"
        };
        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Resize += OnResize;
        _window.FocusChanged += hasFocus => _hasFocus = hasFocus;
    }

    /// <inheritdoc />
    public void Show(params string[] args) => _window.Run(); // Blocking call


    private void KeyDown(IKeyboard keyboard, Key key, int keyCode)
    {
        if (!_hasFocus) return; // Only process any input when focussed

        switch (key)
        {
            // Functions
            case Key.Escape: _window.Close(); break;

            // Movement
            case Key.W: CameraManager.MoveFocussedCameraForward(_movementStep); break;
            case Key.S: CameraManager.MoveFocussedCameraForward(-_movementStep); break;
            case Key.D: CameraManager.MoveFocussedCameraHorizontally(_movementStep); break;
            case Key.A: CameraManager.MoveFocussedCameraHorizontally(-_movementStep); break;
            case Key.E: CameraManager.MoveFocussedCameraVertically(_movementStep); break;
            case Key.Q: CameraManager.MoveFocussedCameraVertically(-_movementStep); break;

            // Rotation
            case Key.Up: CameraManager.RotateFocussedCameraVertically(_rotationStep); break;
            case Key.Down: CameraManager.RotateFocussedCameraVertically(-_rotationStep); break;
            case Key.Right: CameraManager.RotateFocussedCameraHorizontally(_rotationStep); break;
            case Key.Left: CameraManager.RotateFocussedCameraHorizontally(-_rotationStep); break;

            // Zoom
            case Key.I: CameraManager.ZoomFocussedCamera(_zoomStep); break;
            case Key.O: CameraManager.ZoomFocussedCamera(1f/_zoomStep); break;

            // Manage cameras
            case Key.BackSlash: CameraManager.CycleThroughLayout(); break;

            case Key.H: CameraManager.IncreaseNumberOfVisibleCameras(); break;
            case Key.L: CameraManager.DecreaseNumberOfVisibleCameras(); break;

            case Key.J: CameraManager.CycleFocusThroughCameras(1); break;
            case Key.K: CameraManager.CycleFocusThroughCameras(-1); break;

            case Key.C: CameraManager.AddBasicCamera(Vector3.Zero, 10, 6); break; // TODO: hardcoded ints
            case Key.X: CameraManager.RemoveCurrentCamera(); break;
        }
    }

    private void OnLoad()
    {
        _gl = _window.CreateOpenGL(); // Store reference to our OpenGL API instance

        // Listen to input
        IInputContext input = _window.CreateInput();
        foreach (IKeyboard keyboard in input.Keyboards)
            keyboard.KeyDown += KeyDown;

        _gl.ClearColor(Color.Black); // Define the clear color

        // Prepare a Vertex Array Object and bind it as a Vertex Array (prepare for use)
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        // DEFINING SHADERS
        // The data defined above, now loaded into the buffers, must be processed by the GPU.
        // How, is defined in shaders. The two most common shaders are vertex shaders and fragment shaders.
        // Vertex shaders process the data representing vertices, to manipulate said vertices. This can scale, translate
        // or rate objects. A good example is defining a height map. After a vertex shader is complete, all vertices are
        // processed into triangles.
        // Now the fragment shader processes what every pixel should display, based on said triangles. Here, texturing
        // can be done.
        const string vertexCode = @"
#version 330 core

layout (location = 0) in vec3 aPosition;
// Add a new input attribute for the texture coordinates
layout (location = 1) in vec2 aTextureCoord;

// Add an output variable to pass the texture coordinate to the fragment shader
// This variable stores the data that we want to be received by the fragment
out vec2 frag_texCoords;

void main()
{
    gl_Position = vec4(aPosition, 1.0);
    // Assign the texture coordinates without any modification to be received in the fragment
    frag_texCoords = aTextureCoord;
}";

        const string fragmentCode = @"
#version 330 core

// Receive the input from the vertex shader in an attribute
in vec2 frag_texCoords;
uniform sampler2D uTexture;

out vec4 out_color;

void main()
{
    // This will allow us to see the texture coordinates in action!
    // out_color = vec4(frag_texCoords.x, frag_texCoords.y, 0, 1.0);
    out_color = texture(uTexture, frag_texCoords);
}";

        // Note that the shaders form a pipeline. Any custom `in`s can only be defined in the vertex shader.
        // VAOs can be filled with any data you want, and thus other `in`s may make sense.
        // More on this later.

        // Create shader as vertex shader
        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader); // Create a shader in memory
        _gl.ShaderSource(vertexShader, vertexCode); // Set the source code of the shader

        // Compile
        _gl.CompileShader(vertexShader);
        _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
        if (vStatus != (int) GLEnum.True)
            throw new Exception("Vertex shader failed to compile: " + _gl.GetShaderInfoLog(vertexShader));

        // Create shader as fragment shader
        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, fragmentCode);

        // Compile
        _gl.CompileShader(fragmentShader);
        _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
        if (fStatus != (int) GLEnum.True)
            throw new Exception("Fragment shader failed to compile: " + _gl.GetShaderInfoLog(fragmentShader));


        // Now that we compiled both shaders and have pointers to them, we must create a shader program.
        // We link the code of all shaders into a final program.
        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vertexShader);
        _gl.AttachShader(_shaderProgram, fragmentShader);

        _gl.LinkProgram(_shaderProgram);

        _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out int lStatus);
        if (lStatus != (int) GLEnum.True)
            throw new Exception("Program failed to link: " + _gl.GetProgramInfoLog(_shaderProgram));

        // Now that we have linked the program, we can free a bit of GPU memory again.
        // This removes the individual shader programs again and deletes them.
        _gl.DetachShader(_shaderProgram, vertexShader);
        _gl.DetachShader(_shaderProgram, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);


        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();
        _textureId = _gl.GenTexture();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.UseProgram(_shaderProgram); // We dont use multiple shaders, so we can call UseProgram once
    }

    private void OnRender(double deltaTime) // TODO: to see immediate results, render every texture in a different frame
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        //TODO: This stays the same and can be pre-calc'd before this method
        Span<uint> indices = stackalloc uint[]
        {
            0u, 1u, 3u,
            1u, 2u, 3u
        };
        
        foreach (var (camera, texture, _, vertices) in CameraManager.GetDisplayedCameraSlots())
        {
            camera.RenderShot(Renderer, texture.Pixels);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo); // Bind as an Array Buffer (VBO?)
            _gl.BufferData<float>(
                BufferTargetARB.ArrayBuffer,
                (nuint) (vertices.Length * sizeof(float)), // Length of the array
                vertices,                                  // The buffer to read from
                BufferUsageARB.DynamicDraw);               // How we plan to use it, optimizing GPU

            // Optional step: Element Buffer Objects:
            // Allow deduplicating VAOs by giving EBOs filled with indices.
            // Basically, make up all triangles based on all points, where the indices
            // indicate what triangle belongs to what points. This way, a point can
            // be reused for multiple triangles: less data to store and process!
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo); // Bind as an EBO?
            _gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, (nuint) (indices.Length * sizeof(uint)), indices, BufferUsageARB.DynamicDraw);

            // Define VAO structure: starting from 0 we contain Vector3's taking up 3 floats, which we do not want to normalize.
            // We increase the stride with 2 to make 5, so that the texture coordinates can be included as well. Note that these are not included as vertices, since they're not part of the attributes defined here.
            const uint positionLoc = 0; // Same value as the `aPosition` `in` in the vertex shader. We now ensure the data is
                                        // in the right place, as expected by the shader. Note: the position can be dynamic by using _gl.GetAttribLocation("aPosition");
            _gl.EnableVertexAttribArray(positionLoc);
            unsafe
            {
                _gl.VertexAttribPointer(positionLoc, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), (void*) 0);
            }

            // Define texture coordinates structure
            const uint texCoordLoc = 1; // 1 after the first value (the vertex coordinates)
            _gl.EnableVertexAttribArray(texCoordLoc);
            unsafe
            {
                _gl.VertexAttribPointer(texCoordLoc, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), (void*)(3 * sizeof(float))); // Last value is actually the index from where we start to read
            }



            // CREATE AND FILL TEXTURE

            _gl.BindTexture(TextureTarget.Texture2D, _textureId);
            // Fill the texture 'slot'
            _gl.TexImage2D<int>(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint) texture.Width,
                (uint) texture.Height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, texture.Pixels); // todo: these now inclde transparancy, but we will never use that. Switch to bytes

            // Define attributes to how the texture should be rendered
            // _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)TextureWrapMode.Repeat);
            // _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)TextureWrapMode.Repeat);
            _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)TextureMagFilter.Linear);

            // Assign the current texture to the texture uniform, such that the vertex shader gets this texture as input
            int location = _gl.GetUniformLocation(_shaderProgram, "uTexture"); // We use this method instead of a hardcoded 0 or 1 like we did earlier when defining layouts, because this time we chose not to define layouts and let GLSL figure things out.
            _gl.Uniform1(location, 0); // We bind texture unit 0 (defined earlier) to the uniform we found in the line above. Result: shader gets texture data
            
            // Rebind array and texture for final draw
            _gl.BindVertexArray(_vao);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, _textureId);
            unsafe
            {
                _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*) 0); // Draws an EBO
            }

            // _gl.DrawArrays(PrimitiveType.Triangles, 0, 6); // Draws a VBO
            // TODO: SwapBuffers might be nice?
        }
    }

    private void OnResize(Vector2D<int> newSize)
    {
        _gl.Viewport( 0, 0, (uint) newSize.X, (uint) newSize.Y );
        CameraManager.ResizeDisplay(newSize);
        // TODO: use cancallation token to stop rendering during resize
    }
}
