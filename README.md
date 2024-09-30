# ThoughtStuff.GLSourceGen

`ThoughtStuff.GLSourceGen` is a source generator that automates OpenGL/WebGL calls for mapping vertex data structures to shaders.
Specifically, it generates calls to:

- `GL.BindBuffer`
- `GL.BufferData`
- `GL.GetAttribLocation`
- `GL.EnableVertexAttribArray`
- `GL.VertexAttribPointer`

### Benefits of Source Generation

- **Compile-Time Generation**: Code is generated during compilation, eliminating without using reflection.
- **Performance**: No reflection means faster execution and lower memory usage.
- **AOT Compatibility**: Fully supports Ahead-of-Time (AOT) compilation for platforms like WebAssembly.

## Get Started

- Add Package Reference
    ```sh
    dotnet add package ThoughtStuff.GLSourceGen
    ```
- Add shaders as `AdditionalFiles` in csproj
    ```xml
    <ItemGroup>
        <!-- Include shader files as Additional Files so they can be used by source generation -->
        <AdditionalFiles Include="Shaders\**\*.glsl" />
    </ItemGroup>
    ```

## Example

Given a typical vertex structure for 2D colored vertices with `Position` and `Color`:

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct ColorVertex2(Vector2 position, Vector4 color)
{
    public Vector2 Position = position;
    public Vector4 Color = color;
}
```

And shader with `a_VertexPosition` and `a_VertexColor` input attribute variables.
(The source generator will parse the shader to identify attribute names that match the vertex structure fields.)

```glsl
attribute vec4 a_VertexPosition;
attribute vec4 a_VertexColor;

varying mediump vec4 v_Color;

void main(void) {
    gl_Position = a_VertexPosition;
    v_Color = a_VertexColor;
}
```

We can leverage the `SetupVertexAttrib` attribute to generate the necessary OpenGL calls:

```csharp
// Declare the partial class which will be implemented by the source generator.
[SetupVertexAttrib("Shaders/Basic_vert.glsl", typeof(ColorVertex2))]
partial class ColorVertex2ShaderBinding
{
    // While not strictly necessary, these method declarations can help the IDE with code completion

    // Call SetVertexData during initialization to set up the vertex buffer and pass the data to the GPU.
    internal static partial void SetVertexData(JSObject shaderProgram,
                                               JSObject vertexBuffer,
                                               Span<ColorVertex2> vertices,
                                               List<int> vertexAttributeLocations);

    // Call EnableVertexBuffer during rendering if the vertex buffer needs to be re-enabled,
    // or if you are switching between multiple vertex buffers.
    internal static void EnableVertexBuffer(JSObject vertexBuffer,
                                            List<int>? vertexAttributeLocations = null);
}
```

In the main program, we can now use the generated code to set up the vertex buffer:
```csharp
partial class HelloTriangle
{
    public void InitializeScene(IShaderLoader shaderLoader)
    {
        var shaderProgram = shaderLoader.LoadShaderProgram("Shaders/Basic_vert.glsl", ...);

        // Define the vertices for the triangle. Assume NDC coordinates [-1 ... 1].
        Span<ColorVertex2> vertices =
        [
            new ColorVertex2(new(0, 1), new(1, 0, 0, 1)),    // Red vertex
            new ColorVertex2(new(-1, -1), new(0, 1, 0, 1)),  // Green vertex
            new ColorVertex2(new(1, -1), new(0, 0, 1, 1))    // Blue vertex
        ];

        // Create a buffer for the triangle's vertices.
        var vertexBuffer = GL.CreateBuffer();
        // Call the generated function which sets up the vertex buffer and passes the data to the GPU.
        ColorVertex2ShaderBinding.SetBufferData(shaderProgram, vertexBuffer, vertices, vertexAttributeLocations);
    }

    public void Render()
    {
        GL.Clear(GL.COLOR_BUFFER_BIT);
        // If necessary:
        // ColorVertex2ShaderBinding.EnableVertexBuffer(vertexBuffer);
        // GL.UseProgram(shaderProgram);
        GL.DrawArrays(GL.TRIANGLES, 0, 3);
    }
```

The source generator will generate the following code:

```csharp

partial class ColorVertex2ShaderBinding
{
    // Private "cache" fields for attribute locations, strides, and offsets
    private static int _ColorVertex2_Position_location;
    private static int _ColorVertex2_Position_stride;
    private static int _ColorVertex2_Position_offset;
    private static int _ColorVertex2_Color_location;
    private static int _ColorVertex2_Color_stride;
    private static int _ColorVertex2_Color_offset;
    private static bool _ColorVertex2_vertexLayoutInitialized;

    private static void _InitVertexLayoutFields_ColorVertex2(JSObject shaderProgram)
    {
        if (_ColorVertex2_vertexLayoutInitialized)
            return;

        // Cache the attribute locations, strides, and offsets
        _ColorVertex2_Position_location = GL.GetAttribLocation(shaderProgram, "a_VertexPosition");
        if (_ColorVertex2_Position_location == -1)
            throw new InvalidOperationException($"Could not find shader attribute location for a_VertexPosition.");
        _ColorVertex2_Position_stride = Marshal.SizeOf<GenShaderBinding.GameApp.Examples.ColorVertex2>();
        _ColorVertex2_Position_offset = Marshal.OffsetOf<GenShaderBinding.GameApp.Examples.ColorVertex2>(nameof(GenShaderBinding.GameApp.Examples.ColorVertex2.Position)).ToInt32();

        _ColorVertex2_Color_location = GL.GetAttribLocation(shaderProgram, "a_VertexColor");
        if (_ColorVertex2_Color_location == -1)
            throw new InvalidOperationException($"Could not find shader attribute location for a_VertexColor.");
        _ColorVertex2_Color_stride = Marshal.SizeOf<GenShaderBinding.GameApp.Examples.ColorVertex2>();
        _ColorVertex2_Color_offset = Marshal.OffsetOf<GenShaderBinding.GameApp.Examples.ColorVertex2>(nameof(GenShaderBinding.GameApp.Examples.ColorVertex2.Color)).ToInt32();

        _ColorVertex2_vertexLayoutInitialized = true;
    }

    internal static void EnableVertexBuffer(JSObject vertexBuffer,
                                                          List<int>? vertexAttributeLocations = null)
    {
        if (!_ColorVertex2_vertexLayoutInitialized)
            throw new InvalidOperationException("Vertex layout fields have not been initialized.");

        // Bind the vertex buffer
        GL.BindBuffer(GL.ARRAY_BUFFER, vertexBuffer);

        // Enable the vertex attributes
        vertexAttributeLocations?.Add(_ColorVertex2_Position_location);
        GL.VertexAttribPointer(_ColorVertex2_Position_location,
                            size: 2,
                            type: GL.FLOAT,
                            normalized: false,
                            stride: _ColorVertex2_Position_stride,
                            offset: _ColorVertex2_Position_offset);
        GL.EnableVertexAttribArray(_ColorVertex2_Position_location);

        vertexAttributeLocations?.Add(_ColorVertex2_Color_location);
        GL.VertexAttribPointer(_ColorVertex2_Color_location,
                            size: 4,
                            type: GL.FLOAT,
                            normalized: false,
                            stride: _ColorVertex2_Color_stride,
                            offset: _ColorVertex2_Color_offset);
        GL.EnableVertexAttribArray(_ColorVertex2_Color_location);

    }

    internal static partial void SetVertexData(JSObject shaderProgram,
                                                    JSObject vertexBuffer,
                                                    Span<GenShaderBinding.GameApp.Examples.ColorVertex2> vertices,
                                                    List<int> vertexAttributeLocations)
    {
        // Initialize the vertex layout fields
        _InitVertexLayoutFields_ColorVertex2(shaderProgram);

        // Enable the vertex buffer and attributes
        EnableVertexBuffer(vertexBuffer, vertexAttributeLocations);

        // Upload the vertex data to the GPU
        GL.BufferData(GL.ARRAY_BUFFER, vertices, GL.STATIC_DRAW);
    }
}
```
