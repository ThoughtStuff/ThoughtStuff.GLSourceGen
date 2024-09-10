using System;
using System.Collections.Generic;
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
    private record Model(string Namespace, string ClassName, string MethodName, string VertexType, List<VertexField> VertexFields);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        DeclareGenerationAttribute(context);

        var pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "GenShaderBinding.GeneratedAttribute",
            predicate: static (syntaxNode, cancellationToken) => syntaxNode is BaseMethodDeclarationSyntax,
            transform: CreateModel
        );

        context.RegisterSourceOutput(pipeline, GenerateSource);
    }

    static Model CreateModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var containingClass = context.TargetSymbol.ContainingType;
        var methodSymbol = (IMethodSymbol)context.TargetSymbol;
        var vertexParameter = methodSymbol.Parameters.FirstOrDefault(param => param.Type.Name == "Span")?.Type;

        if (vertexParameter is not INamedTypeSymbol spanType || spanType.TypeArguments.Length != 1)
        {
            throw new InvalidOperationException("Expected a Span<T> parameter");
        }
        var vertexType = spanType.TypeArguments[0];
        // TODO: vertexType should be a struct with public fields
        var vertexFields = vertexType.GetMembers()
                                     .OfType<IFieldSymbol>()
                                     .Select(f => new VertexField(f.Name, f.Type.Name))
                                     .ToList();

        var format = SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(
            SymbolDisplayGlobalNamespaceStyle.Omitted);
        return new Model(
            // TODO: handle the case where the type is in a global namespace, nested, etc.
            Namespace: containingClass.ContainingNamespace?.ToDisplayString(format),
            ClassName: containingClass.Name,
            MethodName: context.TargetSymbol.Name,
            VertexType: vertexType.ToDisplayString(format),
            VertexFields: vertexFields);
    }

    static void GenerateSource(SourceProductionContext context, Model model)
    {
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
                    Console.WriteLine("Binding vertex buffer data");
                    // Print out Model members for debugging
                    Console.WriteLine($"Model.Namespace: {{model.Namespace}}");
                    Console.WriteLine($"Model.ClassName: {{model.ClassName}}");
                    Console.WriteLine($"Model.MethodName: {{model.MethodName}}");
                    Console.WriteLine($"Model.VertexType: {{model.VertexType}}");
                    Console.WriteLine("Model.VertexFields: {{string.Join(", ", model.VertexFields.Select(f => $"{f.Name}: {f.Type}"))}}");

                    GL.BindBuffer(GL.ARRAY_BUFFER, vertexBuffer);
                    GL.BufferData(GL.ARRAY_BUFFER, vertices, GL.STATIC_DRAW);

            """;

        var sourceBuilder = new StringBuilder(preamble);
        var vertexType = model.VertexType;
        foreach (var field in model.VertexFields)
        {
            var glslVariableName = $"a_Vertex{field.Name}";
            var location = $"{field.Name}Location";
            int size = field.Type switch
            {
                "Single" => 1,
                "Vector2" => 2,
                "Vector3" => 3,
                "Vector4" => 4,
                _ => throw new NotSupportedException($"Unsupported field type: {field.Type}")
            };
            sourceBuilder.AppendLine($$"""

                    var {{location}} = GL.GetAttribLocation(shaderProgram, "{{glslVariableName}}");
                    if ({{location}} == -1)
                        throw new InvalidOperationException($"Could not find attribute location for {{glslVariableName}}. Expected a vertex shader input variable named {{glslVariableName}}.");
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

                    Console.WriteLine("Finished");
                }
            }

            """;
        sourceBuilder.Append(closing);
        var sourceText = SourceText.From(sourceBuilder.ToString(), Encoding.UTF8);
        context.AddSource($"{model.ClassName}_{model.MethodName}.g.cs", sourceText);
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
                    }
                }
                """, Encoding.UTF8)));
    }
}
