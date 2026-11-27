using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>STD009: processing service names contain "Processing" (2.2.2.0.1).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std009ProcessingNameAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD009";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Processing service names contain 'Processing'",
        messageFormat: "'{0}' is a processing service and its name must contain 'Processing' (Standard 2.2.2.0.1)",
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

        if (type.TypeKind != TypeKind.Class || !Layers.InSegment(type, "Processings") || type.Name.Contains("Processing"))
        {
            return;
        }

        var location = type.Locations.FirstOrDefault();

        if (location is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name));
    }
}
