using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>STD008: brokers call no other brokers and no services (1.2.7).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std008BrokersCallNoBrokersOrServicesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD008";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Brokers call no brokers or services",
        messageFormat: "Broker '{0}' depends on '{1}', which is a broker or a service (Standard 1.2.7)",
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

        foreach (var parameter in Layers.ConstructorParameters(type))
        {
            if (!Layers.InSegment(parameter.Type, "Brokers") && !Layers.InSegment(parameter.Type, "Services"))
            {
                continue;
            }

            var location = type.Locations.FirstOrDefault();

            if (location is null)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name, parameter.Type.Name));
        }
    }
}
