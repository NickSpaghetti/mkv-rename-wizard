using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>STD017: foundations throw no native exception types; local and categorized types are mapped (2.1.3.2.1).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std017FoundationsThrowCategorizedAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD017";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Foundations do not throw native exceptions",
        messageFormat: "Foundation throws native exception '{0}'; map it to a local exception and category (Standard 2.1.3.2.1)",
        category: "Standard",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeThrowStatement, SyntaxKind.ThrowStatement);
        context.RegisterSyntaxNodeAction(AnalyzeThrowExpression, SyntaxKind.ThrowExpression);
    }

    private static void AnalyzeThrowStatement(SyntaxNodeAnalysisContext context)
    {
        var throwStatement = (ThrowStatementSyntax)context.Node;

        if (throwStatement.Expression is { } expression)
        {
            Check(context, expression);
        }
    }

    private static void AnalyzeThrowExpression(SyntaxNodeAnalysisContext context)
    {
        var throwExpression = (ThrowExpressionSyntax)context.Node;

        Check(context, throwExpression.Expression);
    }

    private static void Check(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        if (expression is not ObjectCreationExpressionSyntax creation)
        {
            return;
        }

        var typeDeclaration = creation.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        var enclosing = typeDeclaration is null ? null : context.SemanticModel.GetDeclaredSymbol(typeDeclaration);

        if (!Layers.InSegment(enclosing, "Foundations"))
        {
            return;
        }

        var thrown = context.SemanticModel.GetTypeInfo(creation).Type;

        // A local type is allowed: validation throws localized exceptions that TryCatch maps (2.1.3.1.2.1).
        // Only native exception types must not leave a foundation.
        if (thrown is null || IsLocal(thrown))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.GetLocation(), thrown.Name));
    }

    private static bool IsLocal(ITypeSymbol type) => type.DeclaringSyntaxReferences.Length > 0;
}
