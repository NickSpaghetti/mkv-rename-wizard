using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>
/// STD018: above the foundations, a catch names only categories, or framework cancellation (2.3.3.0.2, 2.4.3.1,
/// 3.0.3). A catch-all must throw a Service category, unless its filter already names categories. Foundations are
/// not checked: they map native exceptions to localized ones (2.1.3.2.1).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std018CatchCategoriesOnlyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD018";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Catches above the foundations name only categories",
        messageFormat: "{0} (Standard 2.3.3.0.2, 2.4.3.1, 3.0.3)",
        category: "Standard",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CatchClause);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;

        var typeDeclaration = catchClause.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        var enclosing = typeDeclaration is null ? null : context.SemanticModel.GetDeclaredSymbol(typeDeclaration);

        if (enclosing is null || Layers.InSegment(enclosing, "Foundations") || !IsAboveFoundations(enclosing))
        {
            return;
        }

        var declared = catchClause.Declaration is null
            ? null
            : context.SemanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
        var isCatchAll = declared is null || IsSystemException(declared);

        var named = new System.Collections.Generic.List<INamedTypeSymbol>();

        if (declared is not null && !isCatchAll)
        {
            named.Add(declared);
        }

        if (catchClause.Filter is { } filter)
        {
            foreach (var node in filter.FilterExpression.DescendantNodesAndSelf().OfType<NameSyntax>())
            {
                if (context.SemanticModel.GetSymbolInfo(node).Symbol is INamedTypeSymbol type)
                {
                    named.Add(type);
                }
            }
        }

        foreach (var type in named.Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>())
        {
            if (IsAllowed(type))
            {
                continue;
            }

            Report(context, catchClause, $"'{enclosing.Name}' catches '{type.Name}', which is neither a category nor cancellation");
            return;
        }

        if (!Layers.InSegment(enclosing, "Exposers") && CatchesLowerCategory(named)
            && RewrapViolation(context, catchClause, enclosing) is { } rewrapProblem)
        {
            Report(context, catchClause, $"'{enclosing.Name}' {rewrapProblem} (Standard 2.2.3.1.1, 2.3.3.0.2)");
            return;
        }

        if (isCatchAll && !catchClause.Filter.IsCategoryFiltered(named) && !ThrowsServiceCategory(context, catchClause))
        {
            Report(context, catchClause, $"'{enclosing.Name}' has a catch-all that does not throw a Service category");
        }
    }

    private static bool IsSystemException(INamedTypeSymbol type) =>
        type.Name == "Exception" && type.ContainingNamespace?.Name == "System";

    private static bool IsAllowed(INamedTypeSymbol type)
    {
        if (Layers.IsMarker(type))
        {
            return true;
        }

        if (type.DeclaringSyntaxReferences.Length > 0)
        {
            return Layers.IsCategorized(type.Name);
        }

        return type.Name is "OperationCanceledException" or "TaskCanceledException";
    }

    /// <summary>A catch that receives a lower validation, dependency, or service category.</summary>
    private static bool CatchesLowerCategory(System.Collections.Generic.IEnumerable<INamedTypeSymbol> named) =>
        named.Any(type => type.Name is "IValidationCategory" or "IDependencyCategory" or "IServiceCategory");

    /// <summary>
    /// What a lower category may become: a bare rethrow passes it through, and a throw must create this layer's own
    /// category. Recovery (a catch that does not throw) is not a rewrap and is not checked.
    /// </summary>
    private static string? RewrapViolation(SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause, INamedTypeSymbol enclosing)
    {
        var ownBase = enclosing.Name.EndsWith("Service", System.StringComparison.Ordinal)
            ? enclosing.Name.Substring(0, enclosing.Name.Length - "Service".Length)
            : enclosing.Name;

        foreach (var throwStatement in catchClause.Block.DescendantNodes().OfType<ThrowStatementSyntax>())
        {
            if (throwStatement.Expression is null)
            {
                return "rethrows a lower category unchanged";
            }

            var creations = throwStatement.Expression.DescendantNodesAndSelf().OfType<ObjectCreationExpressionSyntax>();

            var isOwn = creations.Any(creation =>
                context.SemanticModel.GetTypeInfo(creation).Type is INamedTypeSymbol thrown
                && thrown.Name.StartsWith(ownBase, System.StringComparison.Ordinal)
                && Layers.InSegment(thrown, "Layers"));

            if (!isOwn)
            {
                return "throws a category that is not its own, instead of rewrapping the lower one";
            }
        }

        return null;
    }

    private static bool ThrowsServiceCategory(SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause)
    {
        foreach (var creation in catchClause.Block.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (creation.Ancestors().OfType<ThrowStatementSyntax>().Any()
                && context.SemanticModel.GetTypeInfo(creation).Type is { } thrown
                && thrown.DeclaringSyntaxReferences.Length > 0
                && thrown.Name.EndsWith("ServiceException", System.StringComparison.Ordinal)
                && Layers.IsCategorized(thrown.Name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAboveFoundations(INamedTypeSymbol type) =>
        Layers.InSegment(type, "Orchestrations")
        || Layers.InSegment(type, "Coordinations")
        || Layers.InSegment(type, "Managements")
        || Layers.InSegment(type, "Aggregations")
        || Layers.InSegment(type, "Exposers");

    private static void Report(SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause, string message) =>
        context.ReportDiagnostic(Diagnostic.Create(Rule, catchClause.GetLocation(), message));
}

internal static class CatchFilterExtensions
{
    /// <summary>A filter that names at least one category and no localized type handles a failure by category.</summary>
    public static bool IsCategoryFiltered(this CatchFilterClauseSyntax? filter, System.Collections.Generic.IEnumerable<INamedTypeSymbol> named) =>
        filter is not null && named.Any(type => Layers.IsMarker(type)
            || (type.DeclaringSyntaxReferences.Length > 0 && Layers.IsCategorized(type.Name)));
}
