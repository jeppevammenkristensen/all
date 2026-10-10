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

        var definitions = new[]
        {
            (Interface: compilation.GetTypeByMetadataName("JRK.JevRunner.Annotation.INoulQuestionDefinition`1"),
                Builder: "JRK.JevRunner.Requests.NoulQuestionBuilder", Answer: "JRK.JevRunner.Responses.NoulAnswer",
                Accessor: "GetRequiredNoulAnswer", Configuration: ConfigurationKind.Noul),
            (Interface: compilation.GetTypeByMetadataName("JRK.JevRunner.Annotation.IChoiceQuestionDefinition`1"),
                Builder: "JRK.JevRunner.Requests.ChoiceQuestionBuilder", Answer: "JRK.JevRunner.Responses.ChoiceAnswer",
                Accessor: "GetRequiredChoiceAnswer", Configuration: ConfigurationKind.Choice),
            (Interface: compilation.GetTypeByMetadataName("JRK.JevRunner.Annotation.IScoreQuestionDefinition`1"),
                Builder: "JRK.JevRunner.Requests.ScoreQuestionBuilder", Answer: "JRK.JevRunner.Responses.ScoreAnswer",
                Accessor: "GetRequiredScoreAnswer", Configuration: ConfigurationKind.Score)
        };
        var questions = new List<QuestionMapping>();
        foreach (var property in properties)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var matches = QuestionInterfaces(property.Type).SelectMany(@interface => definitions
                .Where(definition => definition.Interface != null && SymbolEqualityComparer.Default.Equals(
                    @interface.OriginalDefinition, definition.Interface))
                .Select(definition => (Interface: @interface, Definition: definition))).ToArray();
            if (matches.Length == 0)
                continue;

            var propertyLocation = property.Locations.FirstOrDefault() ?? location;
            if (matches.Length != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(UnsupportedMapping, propertyLocation, property.Name,
                    "expected exactly one supported question definition interface construction"));
                valid = false;
                continue;
            }

            if (!Readable(property, type, compilation))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidProperty, propertyLocation, type.Name,
                    $"'{property.Name}' must be a readable, accessible instance property"));
                valid = false;
                continue;
            }

            var match = matches[0];
            // Invalid concrete implementations already produce compiler errors; report the missing
            // interface member at the query too, without relying on concrete member names/accessibility.
            var missing = property.Type is INamedTypeSymbol concrete && concrete.TypeKind != TypeKind.Interface
                ? match.Interface.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(member =>
                    concrete.FindImplementationForInterfaceMember(member) is not IPropertySymbol implementation ||
                    implementation.GetMethod == null ||
                    (implementation.ExplicitInterfaceImplementations.Length == 0 &&
                     implementation.GetMethod.DeclaredAccessibility != Accessibility.Public))
                : null;
            if (missing != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidProperty, propertyLocation, type.Name,
                    $"'{property.Name}' must implement '{missing.Name}' from its question definition interface"));
                valid = false;
                continue;
            }

            var builder = compilation.GetTypeByMetadataName(match.Definition.Builder);
            var answer = compilation.GetTypeByMetadataName(match.Definition.Answer);
            var response = compilation.GetTypeByMetadataName("JRK.JevRunner.Response");
            var accessor = match.Definition.Accessor;
            if (builder == null || answer == null || response == null ||
                !response.GetMembers(accessor).OfType<IMethodSymbol>().Any(method =>
                    !method.IsStatic && method.Arity == 0 && method.Parameters.Length == 1 &&
                    method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                    SymbolEqualityComparer.Default.Equals(method.ReturnType, answer) &&
                    compilation.IsSymbolAccessibleWithin(method, type)))
            {
                context.ReportDiagnostic(Diagnostic.Create(UnsupportedMapping, propertyLocation, property.Name,
                    $"expected {match.Definition.Builder}, {match.Definition.Answer}, and Response.{accessor}(string)"));
                valid = false;
                continue;
            }

            questions.Add(new QuestionMapping(property, match.Interface, builder, answer, accessor,
                match.Definition.Configuration));
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

    /// <summary>Enumerates distinct interface constructions, including the type itself and generic constraints.</summary>
    private static IEnumerable<INamedTypeSymbol> QuestionInterfaces(ITypeSymbol type)
    {
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var pending = new Stack<ITypeSymbol>();
        pending.Push(type);
        while (pending.Count != 0)
        {
            var current = pending.Pop();
            if (!seen.Add(current))
                continue;
            if (current is INamedTypeSymbol named)
            {
                if (named.TypeKind == TypeKind.Interface)
                    yield return named;
                foreach (var inherited in named.AllInterfaces)
                    pending.Push(inherited);
            }
            else if (current is ITypeParameterSymbol parameter)
            {
                foreach (var constraint in parameter.ConstraintTypes)
                    pending.Push(constraint);
            }
        }
    }

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

        var localNames = new HashSet<string>(containers.SelectMany(container => container.TypeParameters)
            .Select(parameter => parameter.Name), StringComparer.Ordinal);
        var request = LocalName("request", localNames);
        source.AppendLine(
                "/// <summary>Creates a request from this query's state, model, instructions, and ordered question configuration.</summary>")
            .AppendLine("/// <returns>A new request containing each interface-defined question.</returns>")
            .AppendLine("public global::JRK.JevRunner.Request GetRequest() {")
            .Append("var ").Append(request)
            .AppendLine(" = new global::JRK.JevRunner.Request(this.@State, this.@JevModel);");
        for (var index = 0; index < questions.Count; index++)
        {
            var question = questions[index];
            var property = Identifier(question.Property.Name);
            var definition = LocalName("__jevDefinition" + index, localNames);
            source.Append("var ").Append(definition).Append(" = (")
                .Append(question.Interface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append(")this.").Append(property).AppendLine(";");
            var constructor = new StringBuilder("new ")
                .Append(question.Builder.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append("(nameof(this.").Append(property).Append("), ").Append(definition)
                .Append(".@Instructions)").ToString();
            var builder = LocalName("__jevQuestion" + index, localNames);
            source.Append("var ").Append(builder).Append(" = ").Append(constructor).AppendLine(";");
            if (question.Configuration == ConfigurationKind.Noul)
            {
                var yes = LocalName("__jevYes" + index, localNames);
                var no = LocalName("__jevNo" + index, localNames);
                source.Append("var ").Append(yes).Append(" = ").Append(definition).AppendLine(".@Yes;")
                    .Append("var ").Append(no).Append(" = ").Append(definition).AppendLine(".@No;")
                    .Append("if (").Append(yes).Append(" != null && ").Append(no).AppendLine(" != null) {")
                    .Append(builder).Append(".@AddYesNo(").Append(yes).Append(", ").Append(no).AppendLine(");")
                    .AppendLine("}");
                source.Append("global::JRK.JevRunner.Requests.RequestExtensions.AddQuestion(")
                    .Append(request).Append(", ").Append(builder).AppendLine(");");
                continue;
            }

            var items = LocalName("__jevItems" + index, localNames);
            var item = LocalName("__jevItem" + index, localNames);
            var choice = question.Configuration == ConfigurationKind.Choice;
            source.Append("global::System.Collections.Generic.IEnumerable<")
                .Append(choice ? "global::JRK.JevRunner.Requests.ChoiceCriteria" : "global::System.String")
                .Append("> ").Append(items).Append(" = ").Append(definition)
                .Append(choice ? ".@Choices" : ".@Criterias").AppendLine(";")
                .Append("foreach (var ").Append(item).Append(" in ").Append(items).AppendLine(") {")
                .Append(builder).Append(choice ? ".@AddChoice(" : ".@AddCriteria(").Append(item)
                .Append(choice ? ".@Choice, " + item + ".@Instruction" : "").AppendLine(");\n}")
                .Append("global::JRK.JevRunner.Requests.RequestExtensions.AddQuestion(")
                .Append(request).Append(", ").Append(builder).AppendLine(");");
        }

        source.Append("return ").Append(request).AppendLine(";\n}");
        var response = LocalName("response", localNames);
        foreach (var question in questions)
        {
            source.AppendLine(
                    "/// <summary>Returns the required typed answer for this query's named question.</summary>")
                .Append("/// <param name=\"").Append(response)
                .AppendLine("\">The response containing the named answer.</param>")
                .AppendLine(
                    "/// <returns>The answer; the response lookup throws if it is missing or has another type.</returns>")
                .Append("public ").Append(question.Answer.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append(' ').Append(Identifier("Get" + question.Property.Name + "Answer"))
                .Append("(global::JRK.JevRunner.Response ").Append(response).AppendLine(") {")
                .Append("return ").Append(response).Append('.').Append(Identifier(question.Accessor))
                .Append("(nameof(this.")
                .Append(Identifier(question.Property.Name)).AppendLine("));\n}");
        }

        foreach (var _ in containers)
            source.AppendLine("}");
        if (!namespaceSymbol.IsGlobalNamespace)
            source.AppendLine("}");
        return source.ToString();
    }

    /// <summary>
    /// Allocates a generated local or parameter name without shadowing containing type parameters.
    /// </summary>
    private static string LocalName(string preferred, HashSet<string> names)
    {
        var name = preferred;
        while (!names.Add(name))
            name += "_";
        return name;
    }

    /// <summary>
    /// Identifies the criteria or choice configuration required before a question is added.
    /// </summary>
    private enum ConfigurationKind
    {
        Noul,
        Choice,
        Score
    }

    /// <summary>
    /// Couples a query property with its builder, typed response lookup, and configuration contract.
    /// </summary>
    private sealed class QuestionMapping
    {
        public QuestionMapping(IPropertySymbol property, INamedTypeSymbol @interface, INamedTypeSymbol builder,
            INamedTypeSymbol answer,
            string accessor, ConfigurationKind configuration)
        {
            Property = property;
            Interface = @interface;
            Builder = builder;
            Answer = answer;
            Accessor = accessor;
            Configuration = configuration;
        }

        public IPropertySymbol Property { get; }
        public INamedTypeSymbol Interface { get; }
        public INamedTypeSymbol Builder { get; }
        public INamedTypeSymbol Answer { get; }
        public string Accessor { get; }
        public ConfigurationKind Configuration { get; }
    }
}
