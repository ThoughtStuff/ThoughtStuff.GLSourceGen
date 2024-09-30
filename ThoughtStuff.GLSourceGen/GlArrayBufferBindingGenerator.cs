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

namespace ThoughtStuff.GLSourceGen;

[Generator]
public class GlArrayBufferBindingGenerator : IIncrementalGenerator
{
    const string Namespace = "ThoughtStuff.GLSourceGen";
    const string GeneratedAttributeName = "SetupVertexAttribAttribute";
    const string SetVertexDataMethodName = "SetVertexData";
    const string GeneratedAttributeFullName = $"{Namespace}.{GeneratedAttributeName}";
    const string ShaderExtension = ".glsl";

    private record Model(string ShaderPath,
                         string Namespace,
                         string ClassName,
                         string MethodName,
                         string VertexTypeFullName,
                         string VertexTypeShortName,
                         List<VariableDeclaration> VertexFields,
                         Location Location);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register post-initialization output for the GeneratedAttribute
        DeclareGenerationAttribute(context);

        // Pipeline for shader files
        var shaderFiles = context.AdditionalTextsProvider.Where(IsShaderFile);
        var shaderSourcePipeline = shaderFiles
            .Select((text, cancellationToken) =>
                (path: text.Path, content: text.GetText(cancellationToken)?.ToString()))
            .Select((tuple, cancellationToken) =>
            {
                var (path, shaderSource) = tuple;
                path = ToProjectRelativePath(path);
                return new KeyValuePair<string, string>(path, shaderSource);
            })
            .Collect();

        // Pipeline for classes with the GeneratedAttribute
        var attributePipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: GeneratedAttributeFullName,
            predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
            transform: CreateModel
        );

        // Combine the pipelines and register the source output generator function
        var pipelines = attributePipeline.Combine(shaderSourcePipeline);
        context.RegisterSourceOutput(pipelines, GenerateSource);
    }

    private static bool IsShaderFile(AdditionalText file)
    {
        return Path.GetExtension(file.Path)
                   .Equals(ShaderExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static string ToProjectRelativePath(string path)
    {
        // TODO: Convert path to relative to the project root
        path = path.Replace("\\", "/");
        return path;
    }

    static Model CreateModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        // Get the shader path and vertex type from the attribute
        var shaderPath = context.Attributes[0].ConstructorArguments[0].Value as string
            ?? throw new UsageException("ShaderPath argument is required");

        var vertexType = context.Attributes[0].ConstructorArguments[1].Value as ITypeSymbol
            ?? throw new UsageException("VertexType argument is required");

        var classSymbol = (INamedTypeSymbol)context.TargetSymbol;

        // Get the namespace and class name
        var containingNamespace = classSymbol.ContainingNamespace;
        var className = classSymbol.Name;

        // Get the vertex fields
        var vertexFields = vertexType.GetMembers()
                                     .OfType<IFieldSymbol>()
                                     .Select(f => new VariableDeclaration(f.Name, f.Type.Name))
                                     .ToList();

        var fullyQualified = SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(
            SymbolDisplayGlobalNamespaceStyle.Omitted);
        var minimalName = SymbolDisplayFormat.MinimallyQualifiedFormat;

        return new Model(
            ShaderPath: shaderPath,
            Namespace: containingNamespace?.ToDisplayString(fullyQualified),
            ClassName: className,
            MethodName: SetVertexDataMethodName,
            VertexTypeFullName: vertexType.ToDisplayString(fullyQualified),
            VertexTypeShortName: vertexType.ToDisplayString(minimalName),
            VertexFields: vertexFields,
            Location: context.TargetSymbol.Locations.FirstOrDefault());
    }

    private static void CheckParameterList(ImmutableArray<IParameterSymbol> parameters)
    {
        const string expectedParameterList = "(JSObject shaderProgram, JSObject vertexBuffer, Span<CustomVertexType> vertices, List<int> vertexAttributeLocations)";
        if (parameters.Length != 4)
        {
            throw new UsageException($"Expected 4 parameters: {expectedParameterList}");
        }
        var isMatching = parameters[0].Type.Name == "JSObject" &&
                         parameters[1].Type.Name == "JSObject" &&
                         parameters[2].Type.Name == "Span" &&
                         parameters[3].Type.Name == "List";
        if (!isMatching)
        {
            throw new UsageException($"Expected parameter types to match: {expectedParameterList}");
        }
        var locationsParameter = parameters[3].Type;
        if (locationsParameter is not INamedTypeSymbol listType
            || listType.TypeArguments.Length != 1
            || listType.TypeArguments[0].Name != "Int32")
        {
            throw new UsageException("Expected 4th parameter to be a List<int>");
        }
    }

    private static void GenerateSource(SourceProductionContext context,
                                       (Model model, ImmutableArray<KeyValuePair<string, string>> shaderSources) input)
    {
        // Convert the exceptions to a diagnostic error
        try
        {
            GenerateSourceCore(context, input);
        }
        catch (UsageException usageException)
        {
            ExceptionToError(context, input.model.Location, usageException);
        }
        catch (Exception internalException)
        {
            ExceptionToError(context, input.model.Location, internalException);
        }
    }

    // Record type to hold the field names
    private record FieldNames(string LocationVarName, string StrideVarName, string OffsetVarName);

    // Method to generate unique field names
    private static FieldNames GenerateFieldNames(string vertexTypeShortName, string fieldName)
    {
        var uniqueFieldIdentifier = $"{vertexTypeShortName}_{fieldName}";
        var locationVarName = $"_{uniqueFieldIdentifier}_location";
        var strideVarName = $"_{uniqueFieldIdentifier}_stride";
        var offsetVarName = $"_{uniqueFieldIdentifier}_offset";
        return new FieldNames(locationVarName, strideVarName, offsetVarName);
    }

    private static void GenerateSourceCore(SourceProductionContext context,
                                        (Model model, ImmutableArray<KeyValuePair<string, string>> shaderSources) input)
    {
        var (model, shaderSourcesArray) = input;
        var shaderSources = shaderSourcesArray.ToDictionary(pair => pair.Key, pair => pair.Value);

        // TODO: var shaderSource = shaderSources[model.ShaderPath];
        var shaderSource = shaderSources.First(kvp => kvp.Key.Contains(model.ShaderPath)).Value;
        var shaderAttributeVariables =
            ShaderParsing.ExtractAttributesFromSource(shaderSource);

        var vertexTypeFullName = model.VertexTypeFullName;
        var vertexTypeShortName = model.VertexTypeShortName;

        // Start building the generated code
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine($$"""
        using System.Runtime.InteropServices;
        using System.Runtime.InteropServices.JavaScript;

        namespace {{model.Namespace}};

        partial class {{model.ClassName}}
        {
            // Private "cache" fields for attribute locations, strides, and offsets
        """);

        // Declare private fields with unique names
        foreach (var field in model.VertexFields)
        {
            var fieldNames = GenerateFieldNames(vertexTypeShortName, field.Name);
            sourceBuilder.AppendLine($"    private static int {fieldNames.LocationVarName};");
            sourceBuilder.AppendLine($"    private static int {fieldNames.StrideVarName};");
            sourceBuilder.AppendLine($"    private static int {fieldNames.OffsetVarName};");
        }

        // Generate a unique bool flag for the vertex type
        var vertexLayoutInitializedFlag = $"_{vertexTypeShortName}_vertexLayoutInitialized";
        sourceBuilder.AppendLine($"    private static bool {vertexLayoutInitializedFlag};");

        // Generate a private method to initialize the vertex layout fields
        var initMethodName = $"_InitVertexLayoutFields_{vertexTypeShortName}";
        sourceBuilder.AppendLine($$"""

            private static void {{initMethodName}}(JSObject shaderProgram)
            {
                if ({{vertexLayoutInitializedFlag}})
                    return;

                // Initialize the fields here
        """);

        foreach (var field in model.VertexFields)
        {
            var fieldNames = GenerateFieldNames(vertexTypeShortName, field.Name);

            // Match C# field name to GLSL variable name
            var glslVariableName = ShaderInputMatching.GetInputVariableName(field, shaderAttributeVariables);
            shaderAttributeVariables.Remove(shaderAttributeVariables.First(v => v.Name == glslVariableName));

            // Assign values to the private member variables
            sourceBuilder.AppendLine($$"""
                    {{fieldNames.LocationVarName}} = GL.GetAttribLocation(shaderProgram, "{{glslVariableName}}");
                    if ({{fieldNames.LocationVarName}} == -1)
                        throw new InvalidOperationException($"Could not find shader attribute location for {{glslVariableName}}.");
                    {{fieldNames.StrideVarName}} = Marshal.SizeOf<{{vertexTypeFullName}}>();
                    {{fieldNames.OffsetVarName}} = Marshal.OffsetOf<{{vertexTypeFullName}}>(nameof({{vertexTypeFullName}}.{{field.Name}})).ToInt32();

            """);
        }

        // Set the initialized flag to true
        sourceBuilder.AppendLine($$"""
                {{vertexLayoutInitializedFlag}} = true;
            }
        """);

        // Begin the partial method implementation
        sourceBuilder.AppendLine($$"""

            internal static partial void {{model.MethodName}}(JSObject shaderProgram,
                                                              JSObject vertexBuffer,
                                                              Span<{{vertexTypeFullName}}> vertices,
                                                              List<int> vertexAttributeLocations)
            {
                GL.BindBuffer(GL.ARRAY_BUFFER, vertexBuffer);

                // Initialize the vertex layout fields
                {{initMethodName}}(shaderProgram);

        """);

        foreach (var field in model.VertexFields)
        {
            var fieldNames = GenerateFieldNames(vertexTypeShortName, field.Name);

            int size = field.Type switch
            {
                "Single" => 1,
                "Vector2" => 2,
                "Vector3" => 3,
                "Vector4" => 4,
                _ => throw new UsageException($"Unsupported field type in {vertexTypeFullName}: {field.Type}")
            };

            // Use the initialized member variables
            sourceBuilder.AppendLine($$"""
                    vertexAttributeLocations.Add({{fieldNames.LocationVarName}});
                    GL.VertexAttribPointer({{fieldNames.LocationVarName}},
                                           size: {{size}},
                                           type: GL.FLOAT,
                                           normalized: false,
                                           stride: {{fieldNames.StrideVarName}},
                                           offset: {{fieldNames.OffsetVarName}});
                    GL.EnableVertexAttribArray({{fieldNames.LocationVarName}});

            """);
        }

        // Close the method and class definitions
        sourceBuilder.Append($$"""
                GL.BufferData(GL.ARRAY_BUFFER, vertices, GL.STATIC_DRAW);
            }
        }
        """);

        var sourceText = SourceText.From(sourceBuilder.ToString(), Encoding.UTF8);
        string fileNameHint = $"{model.ClassName}_{model.MethodName}_{vertexTypeShortName}.g.cs";
        context.AddSource(fileNameHint, sourceText);

        // For troubleshooting, uncomment to write the generated source to a file in the obj directory
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
        var objDir = @"C:\Source\GenShaderBinding\GenShaderBinding.GameApp\obj\Debug\net8.0";
        File.WriteAllText(Path.Combine(objDir, fileNameHint), sourceText.ToString());
#pragma warning restore RS1035 // Do not use APIs banned for analyzers
    }

    private static void ExceptionToError(SourceProductionContext context, Location location, UsageException ex)
    {
        DiagnosticDescriptor descriptor = new(
            id: "TSGL001",
            title: "GL Source Generation Error",
            messageFormat: ex.Message,
            category: "ThoughtStuff.GLSourceGen",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor, location);
        context.ReportDiagnostic(diagnostic);
    }

    private static void ExceptionToError(SourceProductionContext context, Location location, Exception ex)
    {
        DiagnosticDescriptor descriptor = new(
            id: "TSGL999",
            title: "Internal GL Source Generation Error",
            messageFormat: "An internal error in GL source generation occurred: {0}",
            category: "Internal",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor, location, ex.Message);
        context.ReportDiagnostic(diagnostic);
    }

    private static void DeclareGenerationAttribute(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext =>
            postInitializationContext.AddSource($"{GeneratedAttributeName}.g.cs", SourceText.From($$"""
            using System;

            namespace {{Namespace}}
            {
                [AttributeUsage(AttributeTargets.Class)]
                internal sealed class {{GeneratedAttributeName}} : Attribute
                {
                    public string ShaderPath { get; }
                    public Type VertexType { get; }

                    public {{GeneratedAttributeName}}(string shaderPath, Type vertexType)
                    {
                        ShaderPath = shaderPath;
                        VertexType = vertexType;
                    }
                }
            }
            """, Encoding.UTF8)));
    }
}
