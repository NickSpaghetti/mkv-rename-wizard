using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>STD023: no local base components, so inheritance stays within framework or external types (0.2.0.0.0, 0.2.0.0.1.1).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std023NoLocalBaseComponentsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD023";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "No local base components",
        messageFormat: "'{0}' derives from local type '{1}'; base components must be native or external (Standard 0.2.0.0.1.1)",
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
        var baseType = type.BaseType;

        if (type.TypeKind != TypeKind.Class || baseType is null || baseType.DeclaringSyntaxReferences.Length == 0)
        {
            return;
        }

        var location = type.Locations.FirstOrDefault();

        if (location is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name, baseType.Name));
    }
}
