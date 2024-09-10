using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GenShaderBinding.SourceGeneration;

[Generator]
public class GlArrayBufferBindingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext =>
            postInitializationContext.AddSource("myGeneratedFile.cs", SourceText.From("""
                using System;
                namespace GenShaderBinding
                {
                    [AttributeUsage(AttributeTargets.Method)]
                    internal sealed class GeneratedAttribute : Attribute
                    {
                    }
                }
                """, Encoding.UTF8)));

        var pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "GenShaderBinding.GeneratedAttribute",
            predicate: static (syntaxNode, cancellationToken) => syntaxNode is BaseMethodDeclarationSyntax,
            transform: static (context, cancellationToken) =>
            {
                var containingClass = context.TargetSymbol.ContainingType;
                var format = SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(
                    SymbolDisplayGlobalNamespaceStyle.Omitted);
                return new Model(
                    // TODO: handle the case where the type is in a global namespace, nested, etc.
                    Namespace: containingClass.ContainingNamespace?.ToDisplayString(format),
                    ClassName: containingClass.Name,
                    MethodName: context.TargetSymbol.Name);
            }
        );

        context.RegisterSourceOutput(pipeline, static (context, model) =>
        {
            var sourceText = SourceText.From($$"""
                using System.Runtime.InteropServices;
                using System.Runtime.InteropServices.JavaScript;

                namespace {{model.Namespace}};
                partial class {{model.ClassName}}
                {
                    partial void {{model.MethodName}}(JSObject shaderProgram, JSObject vertexBuffer, Span<ColorVertex3> vertices)
                    {
                        Console.WriteLine("Binding vertex buffer data");

                        GL.BindBuffer(GL.ARRAY_BUFFER, vertexBuffer);
                        GL.BufferData(GL.ARRAY_BUFFER, vertices, GL.STATIC_DRAW);

                        var positionLocation = GL.GetAttribLocation(shaderProgram, "a_VertexPosition");
                        GL.EnableVertexAttribArray(positionLocation);
                        // _vertexAttributeLocations.Add(positionLocation);
                        GL.VertexAttribPointer(positionLocation,
                                            size: 3,
                                            type: GL.FLOAT,
                                            normalized: false,
                                            stride: Marshal.SizeOf<ColorVertex3>(),
                                            offset: Marshal.OffsetOf<ColorVertex3>(nameof(ColorVertex3.Position)).ToInt32());

                        var colorLocation = GL.GetAttribLocation(shaderProgram, "a_VertexColor");
                        GL.EnableVertexAttribArray(colorLocation);
                        // _vertexAttributeLocations.Add(colorLocation);
                        GL.VertexAttribPointer(colorLocation,
                                            size: 3,
                                            type: GL.FLOAT,
                                            normalized: false,
                                            stride: Marshal.SizeOf<ColorVertex3>(),
                                            offset: Marshal.OffsetOf<ColorVertex3>(nameof(ColorVertex3.Color)).ToInt32());

                        Console.WriteLine("Finished");
                    }
                }
                """, Encoding.UTF8);

            context.AddSource($"{model.ClassName}_{model.MethodName}.g.cs", sourceText);
        });
    }

    private record Model(string Namespace, string ClassName, string MethodName);
}
