using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>
/// STD019: a catch that throws passes the caught exception into the thrown value, so the original stays as the inner
/// exception (2.2.3.1.1, 2.3.3.0.2). A bare "throw;" is not affected.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std019RethrowKeepsInnerAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD019";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "A rethrow keeps the caught exception as the inner exception",
        messageFormat: "Throw inside catch of '{0}' does not pass it as the inner exception (Standard 2.2.3.1.1, 2.3.3.0.2)",
        category: "Standard",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ThrowStatement);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var throwStatement = (ThrowStatementSyntax)context.Node;

        if (throwStatement.Expression is null)
        {
            return;
        }

        var catchClause = throwStatement.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();
        var caughtName = catchClause?.Declaration?.Identifier.ValueText;

        if (caughtName is null)
        {
            return;
        }

        var thrown = throwStatement.Expression;

        if (thrown is IdentifierNameSyntax local)
        {
            thrown = InitializerOf(catchClause!, local.Identifier.ValueText) ?? thrown;
        }

        var passesCaught = thrown
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => identifier.Identifier.ValueText == caughtName);

        if (passesCaught)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, throwStatement.GetLocation(), caughtName));
    }

    /// <summary>The initializer of a local declared inside the catch block, so a value built earlier is still checked.</summary>
    private static ExpressionSyntax? InitializerOf(CatchClauseSyntax catchClause, string name) =>
        catchClause.Block
            .DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(declarator => declarator.Identifier.ValueText == name)
            ?.Initializer?.Value;
}
