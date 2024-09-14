using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ThoughtStuff.GLSourceGen;

/// <summary>
/// Responsible for matching input variables in the shader source code with the
/// corresponding members of vertex structures in C#.
/// e.g. matching the 'a_Position' attribute in the shader with the 'Vertex.Position' member
/// </summary>
public static class ShaderInputMatching
{
    public static string GetInputVariableName(VariableDeclaration vertexField,
                                              IEnumerable<VariableDeclaration> glslAttributes)
    {
        var csharpWords = new HashSet<string>(NormalizeCSharpName(vertexField.Name));

        string bestMatch = null;
        int bestScore = -1;

        foreach (var glslAttribute in glslAttributes)
        {
            var glslWords = NormalizeGlslName(glslAttribute.Name).ToList();

            // Compute the number of matching words
            int score = csharpWords.Intersect(glslWords).Count();

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = glslAttribute.Name;
            }
        }

        if (bestMatch != null && bestScore > 0)
        {
            return bestMatch;
        }

        throw new Exception($"No matching shader attribute found for vertex field '{vertexField.Name}'");
    }

    private static IEnumerable<string> NormalizeCSharpName(string csharpName)
    {
        // Split PascalCase and snake_case
        var words = SplitWords(csharpName).Select(NormalizeWord);
        return words;
    }

    private static IEnumerable<string> NormalizeGlslName(string glslName)
    {
        // Remove common prefixes
        var name = glslName;
        if (name.StartsWith("a_") || name.StartsWith("v_") || name.StartsWith("u_"))
        {
            name = name.Substring(2);
        }

        // Split on underscores and camelCase
        var words = SplitWords(name).Select(NormalizeWord);
        return words;
    }

    private static IEnumerable<string> SplitWords(string name)
    {
        // Split on underscores
        var parts = name.Split('_');

        foreach (var part in parts)
        {
            // Split camelCase and PascalCase
            var matches = Regex.Matches(part, @"([A-Z]?[a-z0-9]+|[A-Z]+(?![a-z]))");
            foreach (Match match in matches)
            {
                yield return match.Value;
            }
        }
    }

    private static string NormalizeWord(string word)
    {
        word = word.ToLowerInvariant();

        return word switch
        {
            "pos" or "position" => "position",
            "nrm" or "normal" => "normal",
            "uv" or "texcoord" or "tex" or "coord" => "texcoord",
            "col" or "colour" or "color" => "color",
            "vert" or "vertex" => "vertex",
            "tangent" => "tangent",
            "binormal" or "bi" or "bi_normal" => "binormal",
            "boneweights" or "bone_weights" or "bone" or "weights" => "boneweights",
            "boneindices" or "bone_indices" or "indices" => "boneindices",
            _ => word,
        };
    }
}
