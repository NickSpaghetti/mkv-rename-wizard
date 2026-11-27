using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>STD010: a processing service interacts with exactly one foundation (2.2.2.2).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std010ProcessingOneFoundationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD010";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Processing services depend on exactly one foundation",
        messageFormat: "'{0}' is a processing service and must depend on exactly one foundation, found {1} (Standard 2.2.2.2)",
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

        if (type.TypeKind != TypeKind.Class || !Layers.InSegment(type, "Processings"))
        {
            return;
        }

        var foundationCount = Layers.ConstructorParameters(type).Count(parameter => Layers.InSegment(parameter.Type, "Foundations"));

        if (foundationCount == 1)
        {
            return;
        }

        var location = type.Locations.FirstOrDefault();

        if (location is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name, foundationCount));
    }
}
