using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>
/// STD005: an exposer component depends on exactly one component, and it is a service (3.0.2, 3.1.1.2.2).
/// A component is a top-level class in Exposers that implements an interface declared in the project.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std005ExposersDependOnOneServiceAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD005";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Exposers depend on exactly one service",
        messageFormat: "'{0}' is an exposer and must depend on exactly one component, which must be a service; found {1} (Standard 3.0.2, 3.1.1.2.2)",
        category: "Standard",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || type.ContainingType is not null
            || !Layers.InSegment(type, "Exposers") || !type.AllInterfaces.Any(contract => contract.DeclaringSyntaxReferences.Length > 0))
        {
            return;
        }

        var components = Layers.ConstructorParameters(type)
            .Where(parameter => Layers.InSegment(parameter.Type, "Services")
                || Layers.InSegment(parameter.Type, "Brokers")
                || Layers.InSegment(parameter.Type, "Exposers"))
            .ToArray();

        if (components.Length == 1 && Layers.InSegment(components[0].Type, "Services"))
        {
            return;
        }

        var location = type.Locations.FirstOrDefault();

        if (location is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name, components.Length));
    }
}
