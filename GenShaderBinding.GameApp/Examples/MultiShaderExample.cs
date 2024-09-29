using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using GenShaderBinding.GameApp.GameFramework;
using ThoughtStuff.GLSourceGen;

namespace GenShaderBinding.GameApp.Examples;

using static Angle;

// Demonstrates using multiple shaders with different vertex formats in the same scene.
public sealed partial class MultiShaderExample : IGame
{
    // GL Resources for the textured quad
    private JSObject? _shaderProgramTextured;
    private JSObject? _lowResTextureId;
    private JSObject? _highResTextureId;
    private JSObject? _vertexBufferTextured;
    private readonly List<int> _vertexAttributeLocationsTextured = [];

    // GL Resources for the tetrahedron
    private JSObject? _shaderProgramPerspective;
    private JSObject? _modelViewLocation;
    private JSObject? _projectionLocation;
    private JSObject? _vertexBufferTetrahedron;
    private readonly List<int> _vertexAttributeLocationsPerspective = [];

    // "Game" state
    private float _rotationAngleX = 0f;
    private float _rotationAngleY = 0f;


    public string? OverlayText => "Multi-Shader Example";

    [SetupVertexAttrib("Shaders/Basic/TextureUnlit_vert.glsl")]
    partial void BindVertexBufferData(JSObject shaderProgram,
                                      JSObject vertexBuffer,
                                      Span<TextureVertex2> vertices,
                                      List<int> vertexAttributeLocations);

    private void SetupVertexLayoutTextured()
    {
        var shaderProgram = _shaderProgramTextured!;
        GL.BindBuffer(GL.ARRAY_BUFFER, _vertexBufferTextured!);
        var PositionLocation = GL.GetAttribLocation(shaderProgram, "a_VertexPosition");
        if (PositionLocation == -1)
            throw new InvalidOperationException($"Could not find shader attribute location for a_VertexPosition.");
        GL.EnableVertexAttribArray(PositionLocation);
        // vertexAttributeLocations.Add(PositionLocation);
        GL.VertexAttribPointer(PositionLocation,
                               size: 2,
                               type: GL.FLOAT,
                               normalized: false,
                               stride: Marshal.SizeOf<GenShaderBinding.GameApp.Examples.TextureVertex2>(),
                               offset: Marshal.OffsetOf<GenShaderBinding.GameApp.Examples.TextureVertex2>(nameof(GenShaderBinding.GameApp.Examples.TextureVertex2.Position)).ToInt32());


        var TextureCoordLocation = GL.GetAttribLocation(shaderProgram, "a_TextureCoord");
        if (TextureCoordLocation == -1)
            throw new InvalidOperationException($"Could not find shader attribute location for a_TextureCoord.");
        GL.EnableVertexAttribArray(TextureCoordLocation);
        // vertexAttributeLocations.Add(TextureCoordLocation);
        GL.VertexAttribPointer(TextureCoordLocation,
                               size: 2,
                               type: GL.FLOAT,
                               normalized: false,
                               stride: Marshal.SizeOf<GenShaderBinding.GameApp.Examples.TextureVertex2>(),
                               offset: Marshal.OffsetOf<GenShaderBinding.GameApp.Examples.TextureVertex2>(nameof(GenShaderBinding.GameApp.Examples.TextureVertex2.TextureCoord)).ToInt32());
    }

    [SetupVertexAttrib("Shaders/Perspective3D/ColorPassthrough_vert.glsl")]
    partial void BindVertexBufferData(JSObject shaderProgram,
                                      JSObject vertexBuffer,
                                      Span<ColorVertex3> vertices,
                                      List<int> vertexAttributeLocations);

    private void SetupVertexLayoutPerspective()
    {
        var shaderProgram = _shaderProgramPerspective!;
        GL.BindBuffer(GL.ARRAY_BUFFER, _vertexBufferTetrahedron!);
        var PositionLocation = GL.GetAttribLocation(shaderProgram, "a_VertexPosition");
        if (PositionLocation == -1)
            throw new InvalidOperationException($"Could not find shader attribute location for a_VertexPosition.");
        GL.EnableVertexAttribArray(PositionLocation);
        // vertexAttributeLocations.Add(PositionLocation);
        GL.VertexAttribPointer(PositionLocation,
                               size: 3,
                               type: GL.FLOAT,
                               normalized: false,
                               stride: Marshal.SizeOf<GenShaderBinding.GameApp.Examples.ColorVertex3>(),
                               offset: Marshal.OffsetOf<GenShaderBinding.GameApp.Examples.ColorVertex3>(nameof(GenShaderBinding.GameApp.Examples.ColorVertex3.Position)).ToInt32());


        var ColorLocation = GL.GetAttribLocation(shaderProgram, "a_VertexColor");
        if (ColorLocation == -1)
            throw new InvalidOperationException($"Could not find shader attribute location for a_VertexColor.");
        GL.EnableVertexAttribArray(ColorLocation);
        // vertexAttributeLocations.Add(ColorLocation);
        GL.VertexAttribPointer(ColorLocation,
                               size: 3,
                               type: GL.FLOAT,
                               normalized: false,
                               stride: Marshal.SizeOf<GenShaderBinding.GameApp.Examples.ColorVertex3>(),
                               offset: Marshal.OffsetOf<GenShaderBinding.GameApp.Examples.ColorVertex3>(nameof(GenShaderBinding.GameApp.Examples.ColorVertex3.Color)).ToInt32());
    }

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

        // Define the vertices for the texture-mapped quad. Assume NDC coordinates [-1 ... 1].
        Span<TextureVertex2> quadVertices =
        [
            new(new(-1, 1), new(0, 0)),
            new(new(-1, -1), new(0, 1)),
            new(new(0, 1), new(1, 0)),
            new(new(0, -1), new(1, 1))
        ];
        // Create a buffer for the quad's vertices
        _vertexBufferTextured = GL.CreateBuffer();
        BindVertexBufferData(_shaderProgramTextured, _vertexBufferTextured, quadVertices, _vertexAttributeLocationsTextured);

        // Enable alpha blending for the textures which have an alpha channel
        GL.Enable(GL.BLEND);
        GL.BlendFunc(GL.SRC_ALPHA, GL.ONE_MINUS_SRC_ALPHA);

        // -----------------------------------------------------------

        // Load shader program from files using IShaderLoader
        _shaderProgramPerspective = shaderLoader.LoadShaderProgram("Perspective3D/ColorPassthrough_vert",
                                                        "Basic/ColorPassthrough_frag");

        // Store location of the model-view and projection matrix uniforms
        _modelViewLocation = GL.GetUniformLocation(_shaderProgramPerspective, "u_ModelViewMatrix");
        _projectionLocation = GL.GetUniformLocation(_shaderProgramPerspective, "u_ProjectionMatrix");

        // Define vertices for the tetrahedron
        Span<ColorVertex3> tetrahedronVertices =
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
        _vertexBufferTetrahedron = GL.CreateBuffer();
        BindVertexBufferData(_shaderProgramPerspective, _vertexBufferTetrahedron, tetrahedronVertices, _vertexAttributeLocationsPerspective);

        // Enable depth testing
        GL.Enable(GL.DEPTH_TEST);

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
        GL.Clear(GL.COLOR_BUFFER_BIT | GL.DEPTH_BUFFER_BIT);

        if (_vertexBufferTextured is not null)
        {
            // Shader
            GL.UseProgram(_shaderProgramTextured);
            // Use VBO + Vertex layout
            SetupVertexLayoutTextured();
            // Draw
            GL.DrawArrays(GL.TRIANGLE_STRIP, 0, 4);
        }

        ////////////////////

        if (_vertexBufferTetrahedron is not null && _modelViewLocation is not null && _projectionLocation is not null)
        {
            // Shader
            GL.UseProgram(_shaderProgramPerspective);
            // Setup vertex layout
            SetupVertexLayoutPerspective();

            // Create Model-View matrix (rotation)
            var modelViewMatrix = Matrix4x4.CreateRotationY(ToRadians(_rotationAngleY)) *
                                  Matrix4x4.CreateRotationX(ToRadians(_rotationAngleX)) *
                                  Matrix4x4.CreateTranslation(1, 0, -2);
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
    }

    public void Dispose()
    {
        // Restore default settings
        GL.Disable(GL.BLEND);

        // Disable all vertex attribute locations
        foreach (var attributeLocation in _vertexAttributeLocationsTextured)
        {
            GL.DisableVertexAttribArray(attributeLocation);
        }
        _vertexAttributeLocationsTextured.Clear();

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


        //////////////

        // Restore default settings
        GL.Disable(GL.DEPTH_TEST);

        // Disable all vertex attribute locations
        foreach (var attributeLocation in _vertexAttributeLocationsPerspective)
        {
            GL.DisableVertexAttribArray(attributeLocation);
        }
        _vertexAttributeLocationsPerspective.Clear();

        // Dispose of the vertex buffer
        if (_vertexBufferTetrahedron is not null)
        {
            GL.DeleteBuffer(_vertexBufferTetrahedron);
            _vertexBufferTetrahedron.Dispose();
            _vertexBufferTetrahedron = null;
        }

        // Dispose of the shader program
        if (_shaderProgramPerspective is not null)
            ShaderLoader.DisposeShaderProgram(_shaderProgramPerspective);
        _shaderProgramPerspective = null;

    }

    public void Update(TimeSpan deltaTime)
    {
        // Rotate the tetrahedron
        _rotationAngleX += (float)deltaTime.TotalSeconds * 30;
        _rotationAngleX %= 360f;
        _rotationAngleY += (float)deltaTime.TotalSeconds * 120;
        _rotationAngleY %= 360f;
    }

    public void FixedUpdate(TimeSpan deltaTime) { }
    public void OnKeyPress(string key, bool pressed) { }
    public void OnMouseClick(int button, bool pressed, Vector2 position) { }
    public void OnMouseMove(Vector2 position) { }
    public void OnTouchEnd(IEnumerable<Vector2> touches) { }
    public void OnTouchMove(IEnumerable<Vector2> touches) { }
    public void OnTouchStart(IEnumerable<Vector2> touches) { }
}
