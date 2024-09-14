using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ThoughtStuff.GLSourceGen;

public static class ShaderParsing
{
    public static List<VariableDeclaration> ExtractAttributesFromSource(string shaderSource)
    {
        // Remove comments from the shader source
        var withoutComments = RemoveComments(shaderSource);

        // Regex to capture attribute variable declarations
        var regex = new Regex(@"attribute\s+(?<type>\w+)\s+(?<name>\w+)\s*;",
                              RegexOptions.Compiled | RegexOptions.Multiline);
        var matches = regex.Matches(withoutComments);

        // Return attribute names and types
        return matches.Cast<Match>()
                      .Select(match => new VariableDeclaration(
                          Name: match.Groups["name"].Value,
                          Type: match.Groups["type"].Value))
                      .ToList();
    }

    static string RemoveComments(string shaderSource)
    {
        var result = new StringBuilder();
        bool inSingleLineComment = false;
        bool inMultiLineComment = false;
        int i = 0;

        while (i < shaderSource.Length)
        {
            if (!inSingleLineComment && !inMultiLineComment)
            {
                // Check for single-line comment start
                if (i + 1 < shaderSource.Length && shaderSource[i] == '/' && shaderSource[i + 1] == '/')
                {
                    inSingleLineComment = true;
                    i += 2;
                }
                // Check for multi-line comment start
                else if (i + 1 < shaderSource.Length && shaderSource[i] == '/' && shaderSource[i + 1] == '*')
                {
                    inMultiLineComment = true;
                    i += 2;
                }
                else
                {
                    result.Append(shaderSource[i]);
                    i++;
                }
            }
            else if (inSingleLineComment)
            {
                // End of single-line comment
                if (shaderSource[i] == '\n')
                {
                    inSingleLineComment = false;
                    result.Append('\n');
                }
                i++;
            }
            else if (inMultiLineComment)
            {
                // End of multi-line comment
                if (i + 1 < shaderSource.Length && shaderSource[i] == '*' && shaderSource[i + 1] == '/')
                {
                    inMultiLineComment = false;
                    i += 2;
                }
                else
                {
                    i++;
                }
            }
        }
        return result.ToString();
    }
}
