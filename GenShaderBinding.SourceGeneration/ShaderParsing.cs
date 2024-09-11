using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GenShaderBinding.SourceGeneration;

public record GlslAttribute(string Name, string Type);

public static class ShaderParsing
{
    public static List<GlslAttribute> ExtractAttributesFromSource(string shaderSource)
    {
        // Remove single-line comments
        var withoutComments = Regex.Replace(shaderSource, @"//.*$", string.Empty, RegexOptions.Multiline);

        // Regex to capture attributes
        var regex = new Regex(@"attribute\s+(?<type>\w+)\s+(?<name>\w+)\s*;", RegexOptions.Compiled | RegexOptions.Multiline);
        var matches = regex.Matches(withoutComments);

        // Cast MatchCollection to IEnumerable<Match> to use LINQ methods
        return matches.Cast<Match>()
                      .Select(match => new GlslAttribute(
                          Name: match.Groups["name"].Value,
                          Type: match.Groups["type"].Value))
                      .ToList();
    }
}
