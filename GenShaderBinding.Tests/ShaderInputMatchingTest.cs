using GenShaderBinding.SourceGeneration;

namespace GenShaderBinding.Tests;

public class ShaderInputMatchingTest
{
    [Theory(DisplayName = "Matching GLSL variable names")]
    [InlineData("Position", new[] { "a_Position", "a_Normal" }, "a_Position")]
    [InlineData("Normal", new[] { "a_Position", "a_Normal" }, "a_Normal")]
    [InlineData("TexCoord", new[] { "a_Position", "a_TexCoord" }, "a_TexCoord")]
    [InlineData("Position", new[] { "position", "normal" }, "position")]
    [InlineData("VertexPosition", new[] { "vertex_position", "vertex_normal" }, "vertex_position")]
    [InlineData("Color1", new[] { "a_Color0", "a_Color1" }, "a_Color1")]
    [InlineData("Color", new[] { "color", "normal" }, "color")]
    [InlineData("Position", new[] { "pos", "normal" }, "pos")]
    [InlineData("TexCoord", new[] { "uv", "a_Position" }, "uv")]
    [InlineData("Normal", new[] { "nrm", "a_TexCoord" }, "nrm")]
    [InlineData("VertexPosition", new[] { "a_VertexPosition", "a_Normal" }, "a_VertexPosition")]
    [InlineData("VertexNormal", new[] { "a_VertexNormal", "a_Tangent" }, "a_VertexNormal")]
    [InlineData("Color", new[] { "a_DiffuseColor", "a_SpecularColor" }, "a_DiffuseColor")]
    // [InlineData("Color", new[] { "color0", "color1" }, "color0")]
    [InlineData("Binormal", new[] { "bi_normal", "tangent" }, "bi_normal")]
    [InlineData("BoneWeights", new[] { "bone_weights", "bone_indices" }, "bone_weights")]
    public void MatchingNames(string vertexFieldName,
                              string[] glslAttributeNames,
                              string expectedGlslName)
    {
        var vertexField = new VariableDeclaration(vertexFieldName, "Vector3");
        var glslAttributes = glslAttributeNames.Select(name => new VariableDeclaration(name, "vec3"));

        var result = ShaderInputMatching.GetInputVariableName(vertexField, glslAttributes);

        result.Should().Be(expectedGlslName);
    }
}
