using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using GenShaderBinding.GameApp.GameFramework;
using ThoughtStuff.GLSourceGen;

namespace GenShaderBinding.GameApp.Examples;

using static Angle;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct ColorVertex3(Vector3 position, Vector3 color)
{
    public Vector3 Position = position;
    public Vector3 Color = color;
}

[SetupVertexAttrib("Shaders/Perspective3D/ColorPassthrough_vert.glsl", typeof(ColorVertex3))]
partial class ColorVertex3ShaderBinding
{
}

sealed partial class HelloTetrahedron : IGame
{
    private float _rotationAngleX = 0f;
    private float _rotationAngleY = 0f;
    private JSObject? _shaderProgram;
    private JSObject? _modelViewLocation;
    private JSObject? _projectionLocation;
    private JSObject? _vertexBuffer;
    private readonly List<int> _vertexAttributeLocations = [];


    public string? OverlayText => "Hello, Tetrahedron";

    public Task LoadAssetsEssentialAsync(IShaderLoader shaderLoader, ITextureLoader textureLoader)
    {
        // No essential assets for this demo.
        return Task.CompletedTask;
    }

    public void InitializeScene(IShaderLoader shaderLoader)
    {
        // Load shader program from files using IShaderLoader
        _shaderProgram = shaderLoader.LoadShaderProgram("Perspective3D/ColorPassthrough_vert", "Basic/ColorPassthrough_frag");

        // Store location of the model-view and projection matrix uniforms
        _modelViewLocation = GL.GetUniformLocation(_shaderProgram, "u_ModelViewMatrix");
        _projectionLocation = GL.GetUniformLocation(_shaderProgram, "u_ProjectionMatrix");

        // Define vertices for the tetrahedron
        Span<ColorVertex3> vertices =
        [
            // Red face
            new (new(0.5f, 0.5f, 0.5f), new(1f, 0f, 0f)),
            new (new(-0.5f, -0.5f, 0.5f), new(1f, 0f, 0f)),
            new (new(-0.5f, 0.5f, -0.5f), new(1f, 0f, 0f)),
            // Green face
            new (new(0.5f, -0.5f, -0.5f), new(0f, 1f, 0f)),
            new (new(-0.5f, -0.5f, 0.5f), new(0f, 1f, 0f)),
            new (new(-0.5f, 0.5f, -0.5f), new(0f, 1f, 0f)),
            // Blue face
            new (new(0.5f, 0.5f, 0.5f), new(0f, 0f, 1f)),
            new (new(-0.5f, -0.5f, 0.5f), new(0f, 0f, 1f)),
            new (new(0.5f, -0.5f, -0.5f), new(0f, 0f, 1f)),
            // Yellow face
            new (new(0.5f, 0.5f, 0.5f), new(1f, 1f, 0f)),
            new (new(0.5f, -0.5f, -0.5f), new(1f, 1f, 0f)),
            new (new(-0.5f, 0.5f, -0.5f), new(1f, 1f, 0f))
        ];

        // Create and bind the vertex buffer
        _vertexBuffer = GL.CreateBuffer();
        ColorVertex3ShaderBinding.SetVertexData(_shaderProgram,
                                                _vertexBuffer,
                                                vertices,
                                                _vertexAttributeLocations);

        // Enable depth testing
        GL.Enable(GL.DEPTH_TEST);

        // Set the clear color to black
        GL.ClearColor(0, 0, 0, 1);
    }

    public Task LoadAssetsExtendedAsync(IShaderLoader shaderLoader, ITextureLoader textureLoader)
    {
        // No extended assets for this demo.
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Restore default settings
        GL.Disable(GL.DEPTH_TEST);

        // Disable all vertex attribute locations
        foreach (var attributeLocation in _vertexAttributeLocations)
        {
            GL.DisableVertexAttribArray(attributeLocation);
        }
        _vertexAttributeLocations.Clear();

        // Dispose of the vertex buffer
        if (_vertexBuffer is not null)
        {
            GL.DeleteBuffer(_vertexBuffer);
            _vertexBuffer.Dispose();
            _vertexBuffer = null;
        }

        // Dispose of the shader program
        if (_shaderProgram is not null)
            ShaderLoader.DisposeShaderProgram(_shaderProgram);
        _shaderProgram = null;
    }

    public void Update(TimeSpan deltaTime)
    {
        // Rotate the tetrahedron
        _rotationAngleX += (float)deltaTime.TotalSeconds * 30;
        _rotationAngleX %= 360f;
        _rotationAngleY += (float)deltaTime.TotalSeconds * 120;
        _rotationAngleY %= 360f;
    }

    public void FixedUpdate(TimeSpan deltaTime)
    {
        // Not needed for this demo
    }

    public void Render()
    {
        GL.Clear(GL.COLOR_BUFFER_BIT | GL.DEPTH_BUFFER_BIT);

        if (_shaderProgram is null || _modelViewLocation is null || _projectionLocation is null)
            return;

        GL.UseProgram(_shaderProgram);

        // Create Model-View matrix (rotation)
        var modelViewMatrix = Matrix4x4.CreateRotationY(ToRadians(_rotationAngleY)) *
                              Matrix4x4.CreateRotationX(ToRadians(_rotationAngleX)) *
                              Matrix4x4.CreateTranslation(0, 0, -2);

        // Create Projection matrix (perspective projection)
        var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
            fieldOfView: ToRadians(60),
            aspectRatio: 1.0f,           // TODO: Fix aspect ratio
            nearPlaneDistance: 0.1f,
            farPlaneDistance: 10
        );

        // Send model-view and projection matrices to the shader
        GL.UniformMatrix(_modelViewLocation, false, ref modelViewMatrix);
        GL.UniformMatrix(_projectionLocation, false, ref projectionMatrix);

        // Draw the tetrahedron
        GL.DrawArrays(GL.TRIANGLES, 0, 12); // Assuming 12 vertices for 4 triangles (tetrahedron)
    }

    public void OnKeyPress(string key, bool pressed) { }

    public void OnMouseClick(int button, bool pressed, Vector2 position) { }

    public void OnMouseMove(Vector2 position) { }

    public void OnTouchStart(IEnumerable<Vector2> touches) { }

    public void OnTouchMove(IEnumerable<Vector2> touches) { }

    public void OnTouchEnd(IEnumerable<Vector2> touches) { }
}
