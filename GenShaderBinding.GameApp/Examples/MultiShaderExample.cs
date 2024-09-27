using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using GenShaderBinding.GameApp.GameFramework;
using ThoughtStuff.GLSourceGen;

namespace GenShaderBinding.GameApp.Examples;

// Demonstrates using multiple shaders with different vertex formats in the same scene.
public sealed partial class MultiShaderExample: IGame
{
    private JSObject? _shaderProgramTextured;
    private JSObject? _lowResTextureId;
    private JSObject? _highResTextureId;
    private JSObject? _vertexBufferTextured;
    private readonly List<int> _vertexAttributeLocations = [];


    public string? OverlayText => "Multi-Shader Example";

    [SetupVertexAttrib("Shaders/Basic/TextureUnlit_vert.glsl")]
    partial void BindVertexBufferData(JSObject shaderProgram,
                                      JSObject vertexBuffer,
                                      Span<TextureVertex2> vertices,
                                      List<int> vertexAttributeLocations);

    public async Task LoadAssetsEssentialAsync(IShaderLoader shaderLoader, ITextureLoader textureLoader)
    {
        // Load the shader program
        _shaderProgramTextured = shaderLoader.LoadShaderProgram("Basic/TextureUnlit_vert",
                                                        "Basic/TextureUnlit_frag");

        // Load the low-res texture
        string texturePath = "/textures/webgl-logo-lowres.png";

        // Load and bind texture
        var textureId = await textureLoader.LoadTexture(texturePath,
                                                        mipMapping: false,
                                                        nearestNeighborMagnification: false);
        GL.ActiveTexture(GL.TEXTURE0);
        GL.BindTexture(GL.TEXTURE_2D, textureId);
        var textureUniformLoc = GL.GetUniformLocation(_shaderProgramTextured, "u_Texture");
        GL.Uniform1i(textureUniformLoc, 0);

        _lowResTextureId = textureId;
    }

    public void InitializeScene(IShaderLoader shaderLoader)
    {
        if (_shaderProgramTextured is null)
            throw new InvalidOperationException("Shader program not loaded.");

        // Define the vertices for the quad. Assume NDC coordinates [-1 ... 1].
        Span<TextureVertex2> vertices =
        [
            new(new(-1, 1), new(0, 0)),
            new(new(-1, -1), new(0, 1)),
            new(new(0, 1), new(1, 0)),
            new(new(0, -1), new(1, 1))
        ];
        // Create a buffer for the quad's vertices
        _vertexBufferTextured = GL.CreateBuffer();
        BindVertexBufferData(_shaderProgramTextured, _vertexBufferTextured, vertices, _vertexAttributeLocations);

        // Enable alpha blending for the textures which have an alpha channel
        GL.Enable(GL.BLEND);
        GL.BlendFunc(GL.SRC_ALPHA, GL.ONE_MINUS_SRC_ALPHA);

        // Set the clear color to cornflower blue
        GL.ClearColor(0.392f, 0.584f, 0.929f, 1.0f);
    }

    public async Task LoadAssetsExtendedAsync(IShaderLoader shaderLoader, ITextureLoader textureLoader)
    {
        // Load the high-res texture
        string texturePath = "/textures/webgl-logo.png";

        // Load and bind texture
        var textureId = await textureLoader.LoadTexture(texturePath);
        GL.ActiveTexture(GL.TEXTURE0);
        GL.BindTexture(GL.TEXTURE_2D, textureId);
        _highResTextureId = textureId;

        // Delete the low-res texture
        if (_lowResTextureId is not null)
        {
            GL.DeleteTexture(_lowResTextureId);
            _lowResTextureId.Dispose();
            _lowResTextureId = null;
        }
    }

    public void Render()
    {
        GL.Clear(GL.COLOR_BUFFER_BIT);
        if (_vertexBufferTextured is not null)
        {
            GL.DrawArrays(GL.TRIANGLE_STRIP, 0, 4);
        }
    }

    public void Dispose()
    {
        // Restore default settings
        GL.Disable(GL.BLEND);

        // Disable all vertex attribute locations
        foreach (var attributeLocation in _vertexAttributeLocations)
        {
            GL.DisableVertexAttribArray(attributeLocation);
        }
        _vertexAttributeLocations.Clear();

        // Delete the position buffer
        if (_vertexBufferTextured is not null)
        {
            GL.DeleteBuffer(_vertexBufferTextured);
            _vertexBufferTextured.Dispose();
            _vertexBufferTextured = null;
        }

        // Delete the low-res texture if it hasn't been deleted already
        if (_lowResTextureId is not null)
        {
            GL.DeleteTexture(_lowResTextureId);
            _lowResTextureId.Dispose();
            _lowResTextureId = null;
        }

        // Delete the high-res texture if it hasn't been deleted already
        if (_highResTextureId is not null)
        {
            GL.DeleteTexture(_highResTextureId);
            _highResTextureId.Dispose();
            _highResTextureId = null;
        }

        // Delete the shader program
        if (_shaderProgramTextured is not null)
            ShaderLoader.DisposeShaderProgram(_shaderProgramTextured);
        _shaderProgramTextured = null;
    }

    public void Update(TimeSpan deltaTime) { }
    public void FixedUpdate(TimeSpan deltaTime) { }
    public void OnKeyPress(string key, bool pressed) { }
    public void OnMouseClick(int button, bool pressed, Vector2 position) { }
    public void OnMouseMove(Vector2 position) { }
    public void OnTouchEnd(IEnumerable<Vector2> touches) { }
    public void OnTouchMove(IEnumerable<Vector2> touches) { }
    public void OnTouchStart(IEnumerable<Vector2> touches) { }
}
