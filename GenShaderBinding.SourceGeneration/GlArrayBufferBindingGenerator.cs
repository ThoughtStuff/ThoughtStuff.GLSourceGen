using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GenShaderBinding.SourceGeneration;

[Generator]
public class GlArrayBufferBindingGenerator : IIncrementalGenerator
{
    private record VertexField(string Name, string Type);
    private record Model(string ShaderPath, string Namespace, string ClassName, string MethodName, string VertexType, List<VertexField> VertexFields);

    // TODO: convert exceptions to build errors

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        DeclareGenerationAttribute(context);

        var shaderFiles =
            context.AdditionalTextsProvider.Where(
                file => file.Path.EndsWith("_vert.glsl", StringComparison.OrdinalIgnoreCase));
        // file.Path.Contains("Basic") &&
        // file.Path.EndsWith("ColorPassthrough_vert.glsl", StringComparison.OrdinalIgnoreCase));
        var shaderSourcePipeline = shaderFiles.Select((text, cancellationToken) =>
        (path: text.Path, content: text.GetText(cancellationToken)?.ToString()))
        .Select((tuple, cancellationToken) =>
        {
            var (path, content) = tuple;
            // TODO: Convert path to relative to the project root
            path = path.Replace("\\", "/");
            var glslVariables = ShaderParsing.ExtractAttributesFromSource(content);
            return new KeyValuePair<string, string>(path, content);
        })
        .Collect();

        var attributePipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "GenShaderBinding.GeneratedAttribute",
            predicate: static (syntaxNode, cancellationToken) => syntaxNode is BaseMethodDeclarationSyntax,
            transform: CreateModel
        );

        context.RegisterSourceOutput(
            attributePipeline.Combine(shaderSourcePipeline),
            GenerateSource);
    }

    static Model CreateModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var shaderPath = context.Attributes[0].ConstructorArguments
            // .FirstOrDefault(pair => pair.Key == "ShaderPath")
            .First()
            .Value as string
            ?? throw new InvalidOperationException("ShaderPath argument is required");
        var containingClass = context.TargetSymbol.ContainingType;
        var methodSymbol = (IMethodSymbol)context.TargetSymbol;
        // Verify parameters are as expected
        var parameters = methodSymbol.Parameters;
        CheckParameterList(parameters);
        var vertexParameter = parameters[2].Type;
        if (vertexParameter is not INamedTypeSymbol spanType || spanType.TypeArguments.Length != 1)
        {
            throw new InvalidOperationException("Expected 3rd parameter to be a Span<CustomVertexType>");
        }
        var vertexType = spanType.TypeArguments[0];
        if (vertexType.TypeKind != TypeKind.Struct)
        {
            throw new InvalidOperationException("Vertex Type must be a struct type");
        }
        var vertexFields = vertexType.GetMembers()
                                     .OfType<IFieldSymbol>()
                                     .Select(f => new VertexField(f.Name, f.Type.Name))
                                     .ToList();

        var format = SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(
            SymbolDisplayGlobalNamespaceStyle.Omitted);
        return new Model(
            ShaderPath: shaderPath,
            // TODO: handle the case where the type is in a global namespace, nested, etc.
            Namespace: containingClass.ContainingNamespace?.ToDisplayString(format),
            ClassName: containingClass.Name,
            MethodName: context.TargetSymbol.Name,
            VertexType: vertexType.ToDisplayString(format),
            VertexFields: vertexFields);
    }

    private static void CheckParameterList(ImmutableArray<IParameterSymbol> parameters)
    {
        const string expectedParameterList = "(JSObject shaderProgram, JSObject vertexBuffer, Span<CustomVertexType> vertices, List<int> vertexAttributeLocations)";
        if (parameters.Length != 4)
        {
            throw new InvalidOperationException($"Expected 4 parameters: {expectedParameterList}");
        }
        var isMatching = parameters[0].Type.Name == "JSObject" &&
                         parameters[1].Type.Name == "JSObject" &&
                         parameters[2].Type.Name == "Span" &&
                         parameters[3].Type.Name == "List";
        if (!isMatching)
        {
            throw new InvalidOperationException($"Expected parameter types to match: {expectedParameterList}");
        }
        var locationsParameter = parameters[3].Type;
        if (locationsParameter is not INamedTypeSymbol listType
            || listType.TypeArguments.Length != 1
            || listType.TypeArguments[0].Name != "Int32")
        {
            throw new InvalidOperationException("Expected 4th parameter to be a List<int>");
        }
    }

    private static void GenerateSource(SourceProductionContext context,
                                       (Model model, ImmutableArray<KeyValuePair<string, string>> shaderSources) input)
    {
        var (model, shaderSourcesArray) = input;
        var shaderSources = shaderSourcesArray.ToDictionary(pair => pair.Key, pair => pair.Value);

        // TODO: var shaderSource = shaderSources[model.ShaderPath];
        var shaderSource = shaderSources.First(kvp => kvp.Key.Contains(model.ShaderPath)).Value;
        var shaderAttributeVariables =
            ShaderParsing.ExtractAttributesFromSource(shaderSource);
        var shaderVariableNames = shaderAttributeVariables.Select(a => a.Name).ToList();

        var preamble = $$"""

            using System.Runtime.InteropServices;
            using System.Runtime.InteropServices.JavaScript;

            namespace {{model.Namespace}};
            partial class {{model.ClassName}}
            {
                partial void {{model.MethodName}}(JSObject shaderProgram,
                                                  JSObject vertexBuffer,
                                                  Span<{{model.VertexType}}> vertices,
                                                  List<int> vertexAttributeLocations)
                {
                    Console.WriteLine(@"Shader variable names: {{string.Join(", ", shaderVariableNames)}}");
                    Console.WriteLine("Binding vertex buffer data");
                    // Print out Model members for debugging
                    // Console.WriteLine("- Namespace: {{model.Namespace}}");
                    // Console.WriteLine("- ClassName: {{model.ClassName}}");
                    // Console.WriteLine("- MethodName: {{model.MethodName}}");
                    Console.WriteLine("- VertexType: {{model.VertexType}}");
                    Console.WriteLine("- VertexFields: {{string.Join(", ", model.VertexFields.Select(f => $"{f.Name}: {f.Type}"))}}");

                    GL.BindBuffer(GL.ARRAY_BUFFER, vertexBuffer);
                    GL.BufferData(GL.ARRAY_BUFFER, vertices, GL.STATIC_DRAW);

            """;

        var sourceBuilder = new StringBuilder(preamble);
        var vertexType = model.VertexType;
        foreach (var field in model.VertexFields)
        {
            // Match C# field name to GLSL variable name
            var glslVariableName = shaderVariableNames.FirstOrDefault(v => v.Contains(field.Name))
                                   ?? throw new InvalidOperationException($"Could not find GLSL variable name for {field.Name}");
            // TODO: verify the types are compatible
            shaderVariableNames.Remove(glslVariableName);
            var location = $"{field.Name}Location";
            int size = field.Type switch
            {
                "Single" => 1,
                "Vector2" => 2,
                "Vector3" => 3,
                "Vector4" => 4,
                _ => throw new NotSupportedException($"Unsupported field type in {vertexType}: {field.Type}")
            };
            sourceBuilder.AppendLine($$"""

                    var {{location}} = GL.GetAttribLocation(shaderProgram, "{{glslVariableName}}");
                    if ({{location}} == -1)
                        throw new InvalidOperationException($"Could not find attribute location for {{glslVariableName}}.");
                    GL.EnableVertexAttribArray({{location}});
                    vertexAttributeLocations.Add({{location}});
                    GL.VertexAttribPointer({{location}},
                                           size: {{size}},
                                           type: GL.FLOAT,
                                           normalized: false,
                                           stride: Marshal.SizeOf<{{vertexType}}>(),
                                           offset: Marshal.OffsetOf<{{vertexType}}>(nameof({{vertexType}}.{{field.Name}})).ToInt32());

                """);
        }

        var closing = $$"""
                }
            }

            """;
        sourceBuilder.Append(closing);
        var sourceText = SourceText.From(sourceBuilder.ToString(), Encoding.UTF8);
        context.AddSource($"{model.ClassName}_{model.MethodName}_{model.VertexType}.g.cs", sourceText);
    }

    private static void DeclareGenerationAttribute(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext =>
            postInitializationContext.AddSource("GeneratedAttribute.g.cs", SourceText.From("""
                using System;
                namespace GenShaderBinding
                {
                    [AttributeUsage(AttributeTargets.Method)]
                    internal sealed class GeneratedAttribute : Attribute
                    {
                        public string ShaderPath { get; }

                        public GeneratedAttribute(string shaderPath)
                        {
                            ShaderPath = shaderPath;
                        }
                    }
                }
                """, Encoding.UTF8)));
    }
}
