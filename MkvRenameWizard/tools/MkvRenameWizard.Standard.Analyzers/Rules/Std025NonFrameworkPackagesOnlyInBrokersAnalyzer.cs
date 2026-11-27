using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>
/// STD025: non-framework packages are referenced only from brokers. The Standard routes third-party libraries
/// through brokers (1.8); services and exposers call the base library only.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std025NonFrameworkPackagesOnlyInBrokersAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD025";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Third-party packages are used only from brokers",
        messageFormat: "'{0}' uses '{1}', a package outside the base library; wrap it in a broker (Standard 1.8)",
        category: "Standard",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IdentifierName);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var identifier = (IdentifierNameSyntax)context.Node;

        if (identifier.Ancestors().OfType<UsingDirectiveSyntax>().Any())
        {
            return;
        }

        var typeDeclaration = identifier.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        var enclosing = typeDeclaration is null ? null : context.SemanticModel.GetDeclaredSymbol(typeDeclaration);

        if (!Layers.InSegment(enclosing, "Services") && !Layers.InSegment(enclosing, "Exposers"))
        {
            return;
        }

        var assembly = context.SemanticModel.GetSymbolInfo(identifier).Symbol?.ContainingAssembly;

        if (assembly is null
            || SymbolEqualityComparer.Default.Equals(assembly, context.Compilation.Assembly)
            || assembly.Name == "MkvRenameWizard.Core"
            || Layers.IsFramework(assembly))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), enclosing?.Name ?? "?", assembly.Name));
    }
}
