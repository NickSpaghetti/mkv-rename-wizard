using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>STD004: exposers do not communicate with brokers of any type (3.0.0.0).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std004ExposersCallNoBrokersAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD004";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Exposers must not call brokers",
        messageFormat: "'{0}' is an exposer and must not depend on broker '{1}' (Standard 3.0.0.0)",
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

        if (!IsInNamespaceSegment(type, "Exposers"))
        {
            return;
        }

        var location = type.Locations.FirstOrDefault();

        if (location is null)
        {
            return;
        }

        foreach (var parameter in type.Constructors.SelectMany(constructor => constructor.Parameters))
        {
            if (IsInNamespaceSegment(parameter.Type, "Brokers"))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name, parameter.Type.Name));
            }
        }
    }

    private static bool IsInNamespaceSegment(ISymbol symbol, string segment)
    {
        for (var ns = symbol.ContainingNamespace; ns is not null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
        {
            if (ns.Name == segment)
            {
                return true;
            }
        }

        return false;
    }
}
