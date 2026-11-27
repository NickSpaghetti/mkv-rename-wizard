using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>STD022: no Utils, Helpers, or Common components (0.2.0.0.1.0).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std022NoUtilityNamesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD022";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "No utility-named components",
        messageFormat: "'{0}' has a utility name, which the Standard forbids (0.2.0.0.1.0)",
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

        if (!Layers.IsUtilityName(type.Name))
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
