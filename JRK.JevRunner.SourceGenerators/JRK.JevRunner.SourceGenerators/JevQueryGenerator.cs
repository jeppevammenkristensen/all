using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace JRK.JevRunner.SourceGenerators;

/// <summary>
/// Generates request construction and named answer accessors for annotated query classes.
/// </summary>
[Generator]
public sealed class JevQueryGenerator : IIncrementalGenerator
{
    private const string QueryAttribute = "JRK.JevRunner.Annotation.JevQueryAttribute";
    private const string QuestionAttribute = "JRK.JevRunner.Annotation.IQuestionAttribute";

    private static readonly DiagnosticDescriptor InvalidType = Descriptor("JEV001", "Invalid query declaration",
        "Query '{0}' must be a non-static partial class or record class; all containing types must also be non-file-local partial classes or record classes");

    private static readonly DiagnosticDescriptor InvalidProperty = Descriptor("JEV002", "Invalid query property",
        "Query '{0}' has an invalid property contract: {1}");

    private static readonly DiagnosticDescriptor UnsupportedMapping = Descriptor("JEV003",
        "Unsupported question mapping",
        "Question property '{0}' has an unsupported mapping: {1}");

    private static readonly DiagnosticDescriptor InvalidContract = Descriptor("JEV004",
        "Invalid generated API contract",
        "Query '{0}' cannot use the request/response API: {1}");

    private static readonly DiagnosticDescriptor MemberConflict = Descriptor("JEV005", "Generated member conflict",
        "Query '{0}' already declares a member named '{1}' required by the generator");

    /// <summary>
    /// Registers semantic discovery using the exact JevQuery attribute metadata name.
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var queries = context.SyntaxProvider.ForAttributeWithMetadataName(QueryAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (attributeContext, _) => attributeContext);
        context.RegisterSourceOutput(queries, static (productionContext, query) => Generate(productionContext, query));
    }

    private static DiagnosticDescriptor Descriptor(string id, string title, string message) =>
        new DiagnosticDescriptor(id, title, message, "JevQuery", DiagnosticSeverity.Error, true);

    /// <summary>
    /// Validates the query and its question mappings before emitting a complete partial declaration.
    /// </summary>
    private static void Generate(SourceProductionContext context, GeneratorAttributeSyntaxContext query)
    {
        var type = (INamedTypeSymbol) query.TargetSymbol;
        var compilation = query.SemanticModel.Compilation;
        var location = query.TargetNode.GetLocation();
        var containers = new Stack<INamedTypeSymbol>();
        for (var current = type; current != null; current = current.ContainingType)
        {
            if (current.TypeKind != TypeKind.Class || current.IsFileLocal ||
                (SymbolEqualityComparer.Default.Equals(current, type) && current.IsStatic) ||
                current.DeclaringSyntaxReferences.Any(reference =>
                    reference.GetSyntax(context.CancellationToken) is not TypeDeclarationSyntax declaration ||
                    !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidType, location, type.Name));
                return;
            }

            containers.Push(current);
        }

        var valid = true;
        var properties = Properties(type).ToArray();
        foreach (var name in new[] {"State", "JevModel"})
        {
            var property = properties.FirstOrDefault(candidate => candidate.Name == name);
            if (property == null || !Readable(property, type, compilation))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidProperty, location, type.Name,
                    $"'{name}' must be a readable, accessible instance property"));
                valid = false;
            }
        }

        var marker = compilation.GetTypeByMetadataName(QuestionAttribute);
        var questions = new List<QuestionMapping>();
        foreach (var property in properties)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var attributes = property.Type.GetAttributes().Where(attribute =>
                attribute.AttributeClass != null && marker != null &&
                attribute.AttributeClass.AllInterfaces.Any(@interface =>
                    SymbolEqualityComparer.Default.Equals(@interface, marker))).ToArray();
            if (attributes.Length == 0)
                continue;

            var propertyLocation = property.Locations.FirstOrDefault() ?? location;
            var instructions = property.Type is INamedTypeSymbol questionType
                ? Properties(questionType).FirstOrDefault(candidate => candidate.Name == "Instructions")
                : null;
            if (!Readable(property, type, compilation) || instructions == null ||
                !Readable(instructions, type, compilation))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidProperty, propertyLocation, type.Name,
                    $"'{property.Name}' and its 'Instructions' must be readable, accessible instance properties"));
                valid = false;
                continue;
            }

            var attributeName = attributes.Length == 1 ? attributes[0].AttributeClass!.Name : "";
            const string suffix = "QuestionAttribute";
            var prefix = attributeName.EndsWith(suffix, StringComparison.Ordinal)
                ? attributeName.Substring(0, attributeName.Length - suffix.Length)
                : "";
            var builder = compilation.GetTypeByMetadataName($"JRK.JevRunner.Requests.{prefix}QuestionBuilder");
            var answer = compilation.GetTypeByMetadataName($"JRK.JevRunner.Responses.{prefix}Answer");
            var response = compilation.GetTypeByMetadataName("JRK.JevRunner.Response");
            var accessor = "GetRequired" + prefix + "Answer";
            if (prefix.Length == 0 || builder == null || answer == null || response == null ||
                !response.GetMembers(accessor).OfType<IMethodSymbol>().Any(method =>
                    !method.IsStatic && method.Arity == 0 && method.Parameters.Length == 1 &&
                    method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                    SymbolEqualityComparer.Default.Equals(method.ReturnType, answer) &&
                    compilation.IsSymbolAccessibleWithin(method, type)))
            {
                context.ReportDiagnostic(Diagnostic.Create(UnsupportedMapping, propertyLocation, property.Name,
                    "expected one {Prefix}QuestionAttribute marker, a Requests.{Prefix}QuestionBuilder, " +
                    "a Responses.{Prefix}Answer, and Response.GetRequired{Prefix}Answer(string)"));
                valid = false;
                continue;
            }

            questions.Add(new QuestionMapping(property, builder, answer, accessor));
        }

        foreach (var name in new[] {"GetRequest"}.Concat(questions.Select(question =>
                     "Get" + question.Property.Name + "Answer")))
        {
            if (type.GetMembers(name).Length == 0)
                continue;
            context.ReportDiagnostic(Diagnostic.Create(MemberConflict, location, type.Name, name));
            valid = false;
        }

        if (!valid)
            return;

        var source = Emit(type, containers, questions);
        var parseOptions = (CSharpParseOptions) query.TargetNode.SyntaxTree.Options;
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), parseOptions,
            cancellationToken: context.CancellationToken);
        // Binding the emitted tree checks implicit conversions, constructor overloads, accessibility,
        // builder constraints, and the request extension API without guessing their signatures.
        var errors = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree)
            .GetDiagnostics(cancellationToken: context.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0)
        {
            foreach (var error in errors)
                context.ReportDiagnostic(Diagnostic.Create(InvalidContract, location, type.Name, error.GetMessage()));
            return;
        }

        // UTF-16 hex encoding is injective, including namespace, nesting, and generic arity.
        var identity = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var hint = "JevQuery_" + string.Concat(identity.Select(character => ((int) character).ToString("X4"))) +
                   ".g.cs";
        context.AddSource(hint, SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>
    /// Enumerates visible property names most-derived first, respecting member hiding.
    /// </summary>
    private static IEnumerable<IPropertySymbol> Properties(INamedTypeSymbol type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var group in current.GetMembers().GroupBy(member => member.Name))
            {
                if (!names.Add(group.Key))
                    continue;
                foreach (var property in group.OfType<IPropertySymbol>())
                    yield return property;
            }
        }
    }

    private static bool Readable(IPropertySymbol property, INamedTypeSymbol within, Compilation compilation) =>
        !property.IsStatic && !property.IsIndexer && property.ExplicitInterfaceImplementations.Length == 0 &&
        property.GetMethod != null && compilation.IsSymbolAccessibleWithin(property.GetMethod, within);

    private static string Identifier(string name) => "@" + name;

    /// <summary>
    /// Reopens namespaces and generic containing classes and emits documented ordinary methods.
    /// </summary>
    private static string Emit(INamedTypeSymbol type, Stack<INamedTypeSymbol> containers,
        List<QuestionMapping> questions)
    {
        var source = new StringBuilder("// <auto-generated/>\n#nullable enable\n");
        var namespaceSymbol = type.ContainingNamespace;
        if (!namespaceSymbol.IsGlobalNamespace)
        {
            var parts = new Stack<string>();
            for (var current = namespaceSymbol; !current.IsGlobalNamespace; current = current.ContainingNamespace)
                parts.Push(Identifier(current.Name));
            source.Append("namespace ").Append(string.Join(".", parts)).AppendLine(" {");
        }

        foreach (var container in containers)
        {
            source.Append(container.IsStatic ? "static " : "").Append("partial ")
                .Append(container.IsRecord ? "record class " : "class ").Append(Identifier(container.Name));
            if (container.TypeParameters.Length != 0)
                source.Append('<')
                    .Append(string.Join(", ", container.TypeParameters.Select(parameter => Identifier(parameter.Name))))
                    .Append('>');
            source.AppendLine(" {");
        }

        source.AppendLine(
                "/// <summary>Creates a request from this query's state, model, and question instructions.</summary>")
            .AppendLine("/// <returns>A new request containing each annotated question.</returns>")
            .AppendLine("public global::JRK.JevRunner.Request GetRequest() {")
            .AppendLine("var request = new global::JRK.JevRunner.Request(this.@State, this.@JevModel);");
        foreach (var question in questions)
        {
            var property = Identifier(question.Property.Name);
            source.Append("global::JRK.JevRunner.Requests.RequestExtensions.AddQuestion(request, new ")
                .Append(question.Builder.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append("(nameof(this.").Append(property).Append("), this.").Append(property)
                .AppendLine(".@Instructions));");
        }

        source.AppendLine("return request;\n}");
        foreach (var question in questions)
        {
            source.AppendLine(
                    "/// <summary>Returns the required typed answer for this query's named question.</summary>")
                .AppendLine("/// <param name=\"response\">The response containing the named answer.</param>")
                .AppendLine(
                    "/// <returns>The answer; the response lookup throws if it is missing or has another type.</returns>")
                .Append("public ").Append(question.Answer.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append(' ').Append(Identifier("Get" + question.Property.Name + "Answer"))
                .AppendLine("(global::JRK.JevRunner.Response response) {")
                .Append("return response.").Append(Identifier(question.Accessor)).Append("(nameof(this.")
                .Append(Identifier(question.Property.Name)).AppendLine("));\n}");
        }

        foreach (var _ in containers)
            source.AppendLine("}");
        if (!namespaceSymbol.IsGlobalNamespace)
            source.AppendLine("}");
        return source.ToString();
    }

    private sealed class QuestionMapping
    {
        public QuestionMapping(IPropertySymbol property, INamedTypeSymbol builder, INamedTypeSymbol answer,
            string accessor)
        {
            Property = property;
            Builder = builder;
            Answer = answer;
            Accessor = accessor;
        }

        public IPropertySymbol Property { get; }
        public INamedTypeSymbol Builder { get; }
        public INamedTypeSymbol Answer { get; }
        public string Accessor { get; }
    }
}