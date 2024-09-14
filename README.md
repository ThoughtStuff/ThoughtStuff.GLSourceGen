# ThoughtStuff.GLSourceGen

Generates calls to

- `GL.BindBuffer`
- `GL.BufferData`
- `GL.GetAttribLocation`
- `GL.EnableVertexAttribArray`
- `GL.VertexAttribPointer`

These calls map vertex data structures to shader variables, and facilitate copying data from CPU memory to GPU memory.

## Example

Given a typical vertex structure for 2D colored vertices:

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct ColorVertex2(Vector2 position, Vector4 color)
{
    public Vector2 Position = position;
    public Vector4 Color = color;
}
```

```csharp
partial class HelloTriangle
{
    // Declare the partial function which will be implemented by the source generator.
    [GenShaderBinding.Generated]
    partial void SetBufferData(JSObject shaderProgram,
                               JSObject vertexBuffer,
                               Span<ColorVertex2> vertices,
                               List<int> vertexAttributeLocations);

    public void InitializeScene(IShaderLoader shaderLoader)
    {
        var shaderProgram = shaderLoader.LoadShaderProgram(...);

        // Define the vertices for the triangle. Assume NDC coordinates [-1 ... 1].
        Span<ColorVertex2> vertices =
        [
            new(new(0, 1), new(1, 0, 0, 1)),    // Red vertex
            new(new(-1, -1), new(0, 1, 0, 1)),  // Green vertex
            new(new(1, -1), new(0, 0, 1, 1))    // Blue vertex
        ];

        // Create a buffer for the triangle's vertex positions.
        var positionBuffer = GL.CreateBuffer();
        // Call the generated function which sets up the vertex buffer and passes the data to the GPU.
        SetBufferData(shaderProgram, positionBuffer, vertices, vertexAttributeLocations);
    }
```

The source generator will generate the following code:

```csharp
partial class HelloTriangle
{
    partial void SetBufferData(JSObject shaderProgram,
                               JSObject vertexBuffer,
                               Span<ColorVertex2> vertices,
                               List<int> vertexAttributeLocations)
    {
        // Bind the buffer to the ARRAY_BUFFER target.
        GL.BindBuffer(GL.ARRAY_BUFFER, vertexBuffer);
        // Copy the vertex data to the GPU.
        GL.BufferData(GL.ARRAY_BUFFER, vertices, GL.STATIC_DRAW);

        // Get the location of the 'a_Position' attribute variable in the shader program.
        int positionLocation = GL.GetAttribLocation(shaderProgram, "a_Position");
        // Enable the 'a_Position' attribute.
        GL.EnableVertexAttribArray(positionLocation);
        // Define the layout of the 'a_Position' attribute.
        GL.VertexAttribPointer(positionLocation,
                               size: 2, // Vector2 has 2 floats
                               type: GL.FLOAT,
                               normalized: false,
                               stride: Marshal.SizeOf<ColorVertex2>(),
                               offset: Marshal.OffsetOf<ColorVertex2>(nameof(ColorVertex2.Position)).ToInt32());

        // Get the location of the 'a_Color' attribute in the shader program.
        int colorLocation = GL.GetAttribLocation(shaderProgram, "a_Color");
        // Enable the 'a_Color' attribute.
        GL.EnableVertexAttribArray(colorLocation);
        // Define the layout of the 'a_Color' attribute.
        GL.VertexAttribPointer(colorLocation,
                               size: 4, // Vector4 has 4 floats
                               type: GL.FLOAT,
                               normalized: false,
                               stride: Marshal.SizeOf<ColorVertex2>(),
                               offset: Marshal.OffsetOf<ColorVertex2>(nameof(ColorVertex2.Color)).ToInt32());
    }
}
```
