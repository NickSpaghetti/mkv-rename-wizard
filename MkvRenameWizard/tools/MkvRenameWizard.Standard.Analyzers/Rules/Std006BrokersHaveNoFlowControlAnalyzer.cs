using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>STD006: brokers have no flow control and no exception handling (1.2.1, 1.2.2).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std006BrokersHaveNoFlowControlAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD006";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Brokers must not contain flow control",
        messageFormat: "Broker contains '{0}', which is flow control or exception handling (Standard 1.2.1, 1.2.2)",
        category: "Standard",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.IfStatement,
            SyntaxKind.SwitchStatement,
            SyntaxKind.SwitchExpression,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement,
            SyntaxKind.TryStatement,
            SyntaxKind.ConditionalExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;
        var typeDeclaration = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();

        if (typeDeclaration is null)
        {
            return;
        }

        var type = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);

        if (!Layers.InSegment(type, "Brokers"))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation(), node.Kind().ToString()));
    }
}
