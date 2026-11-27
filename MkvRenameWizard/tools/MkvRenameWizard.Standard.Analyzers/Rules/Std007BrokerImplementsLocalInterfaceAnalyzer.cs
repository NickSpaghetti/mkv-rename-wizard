using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>STD007: a broker implements a local interface named I{Name} (1.2.0).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std007BrokerImplementsLocalInterfaceAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD007";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Brokers implement a local interface",
        messageFormat: "Broker '{0}' must implement a local interface named 'I{0}' (Standard 1.2.0)",
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

        if (type.TypeKind != TypeKind.Class || !Layers.InSegment(type, "Brokers"))
        {
            return;
        }

        var expected = "I" + type.Name;

        if (type.AllInterfaces.Any(contract => contract.Name == expected))
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
