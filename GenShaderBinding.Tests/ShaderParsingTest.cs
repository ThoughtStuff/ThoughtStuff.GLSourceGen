using ThoughtStuff.GLSourceGen;
using static ThoughtStuff.GLSourceGen.ShaderParsing;

namespace GenShaderBinding.Tests;

public class ShaderParsingTest
{
    [Fact(DisplayName = "One Liner")]
    public void OneLine()
    {
        // Arrange
        var shaderSource = "attribute vec3 a_Position;";

        // Act
        var result = ExtractAttributesFromSource(shaderSource);

        // Assert
        result.Should().BeEquivalentTo(new List<VariableDeclaration>
        {
            new("a_Position", "vec3")
        });
    }

    [Fact(DisplayName = "Extract single attribute with correct type and name")]
    public void Extract_SingleAttribute_CorrectTypeAndName()
    {
        // Arrange
        var shaderSource = @"
            attribute vec3 a_Position;

            void main(void) {
                gl_Position = vec4(a_Position, 1.0);
            }";

        // Act
        var result = ExtractAttributesFromSource(shaderSource);

        // Assert
        result.Should().BeEquivalentTo(new List<VariableDeclaration>
        {
            new("a_Position", "vec3")
        });
    }

    [Fact(DisplayName = "Extract multiple attributes with correct types and names")]
    public void Extract_MultipleAttributes_CorrectTypesAndNames()
    {
        // Arrange
        var shaderSource = @"
            #version 100

            // Comments in the shader
            attribute vec4 a_VertexPosition; 
            attribute vec4 a_VertexColor; 
            attribute vec2 a_TextureCoord;
            attribute float point_size;
            // Weird white space:
                attribute        
                mat4             
                a_ModelViewMatrix 
                ;

            varying mediump vec4 v_Color;

            void main(void) {
                gl_Position = a_VertexPosition;
                v_Color = a_VertexColor;
            }";

        // Act
        var result = ExtractAttributesFromSource(shaderSource);

        // Assert
        result.Should().BeEquivalentTo(new List<VariableDeclaration>
        {
            new("a_VertexPosition", "vec4"),
            new("a_VertexColor", "vec4"),
            new("a_TextureCoord", "vec2"),
            new("point_size", "float"),
            new("a_ModelViewMatrix", "mat4")
        });
    }

    [Fact(DisplayName = "Do not extract attributes from comments")]
    public void NotFromComment()
    {
        // Arrange
        var shaderSource = @"
            #version 100

            // attribute vec3 a_Position;
            attribute vec4 p;
            const float PI = 3.14159;
            int attribute_count = 3;
            // these are all commented:
            /*
            attribute vec4 a_Color;
            attribute vec2 a_TexCoord;
            */
            /* attribute vec3 a_Normal; */

            /* diabolical case
            // */
            attribute vec4 q;

            void main(void) {
                gl_Position = p;
            }";

        // Act
        var result = ExtractAttributesFromSource(shaderSource);

        // Assert
        result.Should().BeEquivalentTo(new List<VariableDeclaration>
        {
            new("p", "vec4"),
            new("q", "vec4"),
        });
    }
}
