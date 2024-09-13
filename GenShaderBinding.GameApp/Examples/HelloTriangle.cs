using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using GenShaderBinding.GameApp.GameFramework;

namespace GenShaderBinding.GameApp.Examples;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct ColorVertex2(Vector2 position, Vector4 color)
{
    public Vector2 Position = position;
    public Vector4 Color = color;
}

sealed partial class HelloTriangle : IGame
{
    private JSObject? _shaderProgram;
    private JSObject? _positionBuffer;
    private JSObject? _colorBuffer;
    private readonly List<int> _vertexAttributeLocations = [];


    public string? OverlayText => "Hello, Triangle";

    [Generated("Shaders/Basic/ColorPassthrough_vert.glsl")]
    partial void SetBufferData(JSObject shaderProgram,
                               JSObject vertexBuffer,
                               Span<ColorVertex2> vertices,
                               List<int> vertexAttributeLocations);

    public void InitializeScene(IShaderLoader shaderLoader)
    {
        // Load the shader program
        _shaderProgram = shaderLoader.LoadShaderProgram("Basic/ColorPassthrough_vert", "Basic/ColorPassthrough_frag");

        // Define the vertices for the triangle. Assume NDC coordinates [-1 ... 1].
        Span<ColorVertex2> vertices =
        [
            new(new(0, 1), new(1, 0, 0, 1)),    // Red
            new(new(-1, -1), new(0, 1, 0, 1)),  // Green
            new(new(1, -1), new(0, 0, 1, 1))    // Blue
        ];
        // Create a buffer for the triangle's vertex positions.
        _positionBuffer = GL.CreateBuffer();
        SetBufferData(_shaderProgram, _positionBuffer, vertices, _vertexAttributeLocations);

        // Set the clear color to cornflower blue
        GL.ClearColor(0.392f, 0.584f, 0.929f, 1.0f);
    }

    public void Dispose()
    {
        // Disable all vertex attribute locations
        foreach (var attributeLocation in _vertexAttributeLocations)
        {
            GL.DisableVertexAttribArray(attributeLocation);
        }
        _vertexAttributeLocations.Clear();

        // Delete the position buffer
        if (_positionBuffer is not null)
        {
            GL.DeleteBuffer(_positionBuffer);
            _positionBuffer.Dispose();
            _positionBuffer = null;
        }

        // Delete the color buffer
        if (_colorBuffer is not null)
        {
            GL.DeleteBuffer(_colorBuffer);
            _colorBuffer.Dispose();
            _colorBuffer = null;
        }

        // Delete the shader program
        if (_shaderProgram is not null)
            ShaderLoader.DisposeShaderProgram(_shaderProgram);
        _shaderProgram = null;
    }

    public void Render()
    {
        GL.Clear(GL.COLOR_BUFFER_BIT);
        if (_positionBuffer is not null)
        {
            GL.DrawArrays(GL.TRIANGLES, 0, 3);
        }
    }

    public Task LoadAssetsEssentialAsync(IShaderLoader shaderLoader, ITextureLoader textureLoader) => Task.CompletedTask;
    public Task LoadAssetsExtendedAsync(IShaderLoader shaderLoader, ITextureLoader textureLoader) => Task.CompletedTask;
    public void Update(TimeSpan deltaTime) { }
    public void FixedUpdate(TimeSpan deltaTime) { }
    public void OnKeyPress(string key, bool pressed) { }
    public void OnMouseClick(int button, bool pressed, Vector2 position) { }
    public void OnMouseMove(Vector2 position) { }
    public void OnTouchEnd(IEnumerable<Vector2> touches) { }
    public void OnTouchMove(IEnumerable<Vector2> touches) { }
    public void OnTouchStart(IEnumerable<Vector2> touches) { }
}
