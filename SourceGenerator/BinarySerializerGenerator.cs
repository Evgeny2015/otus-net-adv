using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace BinarySerializerGenerator
{
    [Generator]
    public class BinarySerializerGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Find all classes with the GenerateBinarySerializerAttribute
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax classDecl &&
                                                  classDecl.AttributeLists.Count > 0,
                    transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
                .Where(classDecl => classDecl != null);

            // Combine with compilation
            var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

            // Register the source generator
            context.RegisterSourceOutput(compilationAndClasses,
                static (spc, source) => Execute(source.Left, source.Right, spc));
        }

        private static void Execute(Compilation compilation,
            ImmutableArray<ClassDeclarationSyntax> classes,
            SourceProductionContext context)
        {
            // Always generate the attribute source
            string attributeSource = GenerateAttributeSource();
            context.AddSource("GenerateBinarySerializerAttribute.g.cs", SourceText.From(attributeSource, Encoding.UTF8));

            // Report diagnostic for debugging
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "BSG001",
                    "Generator running",
                    $"Found {classes.Length} candidate classes",
                    "Debug",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true),
                Location.None));

            if (classes.IsDefaultOrEmpty)
                return;

            // Get the attribute symbol (it should exist now since we just added it)
            var attributeSymbol = compilation.GetTypeByMetadataName("BinarySerializerGenerator.GenerateBinarySerializerAttribute");

            // Process each candidate class
            foreach (var classDeclaration in classes)
            {
                var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                if (classSymbol == null)
                    continue;

                // Report diagnostic for each class
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "BSG002",
                        "Processing class",
                        $"Processing class: {classSymbol.Name}",
                        "Debug",
                        DiagnosticSeverity.Info,
                        isEnabledByDefault: true),
                    Location.None));

                // Check if the class has any attribute named GenerateBinarySerializerAttribute
                bool hasAttribute = false;
                foreach (var attr in classSymbol.GetAttributes())
                {
                    var attrClass = attr.AttributeClass;
                    if (attrClass == null)
                        continue;

                    // Check if the attribute name matches (allowing for different namespaces)
                    if (attrClass.Name == "GenerateBinarySerializerAttribute" ||
                        (attributeSymbol != null && attrClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))
                    {
                        hasAttribute = true;
                        context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "BSG003",
                                "Found attribute",
                                $"Found attribute on {classSymbol.Name}",
                                "Debug",
                                DiagnosticSeverity.Info,
                                isEnabledByDefault: true),
                            Location.None));
                        break;
                    }
                }

                if (!hasAttribute)
                    continue;

                // Generate source code for this class
                string source = GenerateSerializerClass(classSymbol, classDeclaration);
                context.AddSource($"{classSymbol.Name}_BinarySerializer.g.cs", SourceText.From(source, Encoding.UTF8));

                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "BSG004",
                        "Generated serializer",
                        $"Generated serializer for {classSymbol.Name}",
                        "Debug",
                        DiagnosticSeverity.Info,
                        isEnabledByDefault: true),
                    Location.None));
            }
        }

        private static string GenerateAttributeSource()
        {
            return @"// <auto-generated/>
#pragma warning disable

namespace BinarySerializerGenerator
{
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GenerateBinarySerializerAttribute : System.Attribute
    {
        public GenerateBinarySerializerAttribute()
        {
        }
    }
}";
        }

        private static string GenerateSerializerClass(INamedTypeSymbol classSymbol, ClassDeclarationSyntax classDeclaration)
        {
            var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
            var className = classSymbol.Name;

            // Get public properties with basic types
            var properties = classSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                           p.GetMethod != null && !p.GetMethod.IsStatic)
                .Where(p => IsSupportedType(p.Type))
                .ToList();

            // Build the source code
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#pragma warning disable");
            sb.AppendLine();
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {className}");
            sb.AppendLine("    {");
            sb.AppendLine("        public void SerializeToBinary(System.IO.Stream stream)");
            sb.AppendLine("        {");
            sb.AppendLine("            using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))");
            sb.AppendLine("            {");

            foreach (var property in properties)
            {
                var propertyName = property.Name;
                var typeName = property.Type.ToDisplayString();

                if (typeName == "int" || typeName == "System.Int32")
                {
                    sb.AppendLine($"                writer.Write(this.{propertyName});");
                }
                else if (typeName == "string" || typeName == "System.String")
                {
                    sb.AppendLine($"                writer.Write(this.{propertyName} ?? string.Empty);");
                }
                else if (typeName == "DateTime" || typeName == "System.DateTime")
                {
                    sb.AppendLine($"                writer.Write(this.{propertyName}.Ticks);");
                }
                else if (typeName == "bool" || typeName == "System.Boolean")
                {
                    sb.AppendLine($"                writer.Write(this.{propertyName});");
                }
                else if (typeName == "long" || typeName == "System.Int64")
                {
                    sb.AppendLine($"                writer.Write(this.{propertyName});");
                }
                else if (typeName == "double" || typeName == "System.Double")
                {
                    sb.AppendLine($"                writer.Write(this.{propertyName});");
                }
                else if (typeName == "float" || typeName == "System.Single")
                {
                    sb.AppendLine($"                writer.Write(this.{propertyName});");
                }
                else if (typeName == "byte" || typeName == "System.Byte")
                {
                    sb.AppendLine($"                writer.Write(this.{propertyName});");
                }
                else if (typeName == "short" || typeName == "System.Int16")
                {
                    sb.AppendLine($"                writer.Write(this.{propertyName});");
                }
                else if (typeName == "decimal" || typeName == "System.Decimal")
                {
                    sb.AppendLine($"                writer.Write(this.{propertyName});");
                }
            }

            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static bool IsSupportedType(ITypeSymbol typeSymbol)
        {
            var typeName = typeSymbol.ToDisplayString();
            var supportedTypes = new HashSet<string>
            {
                "int", "System.Int32",
                "string", "System.String",
                "DateTime", "System.DateTime",
                "bool", "System.Boolean",
                "long", "System.Int64",
                "double", "System.Double",
                "float", "System.Single",
                "byte", "System.Byte",
                "short", "System.Int16",
                "decimal", "System.Decimal"
            };

            return supportedTypes.Contains(typeName);
        }
    }
}
