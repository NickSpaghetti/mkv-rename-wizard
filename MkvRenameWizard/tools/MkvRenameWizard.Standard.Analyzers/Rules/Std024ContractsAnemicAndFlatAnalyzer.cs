using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

/// <summary>
/// STD024: a contract (a non-exception model used in a service or exposer signature) is anemic and flat: it declares
/// no methods and no hand-written constructors, and no property holds another local model directly, though it may hold a collection of one (2.3.2.2.0). Primary
/// constructors are allowed by project decision.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Std024ContractsAnemicAndFlatAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STD024";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Contracts are anemic and flat",
        messageFormat: "Contract '{0}' {1} (Standard 2.3.2.2.0)",
        category: "Standard",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(start =>
        {
            var contracts = new ContractSet();

            start.RegisterSymbolAction(
                symbol => contracts.Collect((IMethodSymbol)symbol.Symbol),
                SymbolKind.Method);

            start.RegisterCompilationEndAction(end => contracts.Report(end));
        });
    }

    private sealed class ContractSet
    {
        private readonly System.Collections.Generic.HashSet<INamedTypeSymbol> _contracts =
            new(SymbolEqualityComparer.Default);

        // Symbol actions run concurrently, so the set is guarded.
        private readonly object _gate = new();

        public void Collect(IMethodSymbol method)
        {
            if (!Layers.InSegment(method.ContainingType, "Services") && !Layers.InSegment(method.ContainingType, "Exposers"))
            {
                return;
            }

            foreach (var typeSymbol in method.Parameters.Select(parameter => parameter.Type).Append(method.ReturnType))
            {
                foreach (var named in Unwrap(typeSymbol))
                {
                    if (IsContractCandidate(named))
                    {
                        lock (_gate)
                        {
                            _contracts.Add(named);
                        }
                    }
                }
            }
        }

        public void Report(CompilationAnalysisContext context)
        {
            INamedTypeSymbol[] snapshot;

            lock (_gate)
            {
                snapshot = _contracts.ToArray();
            }

            foreach (var contract in snapshot)
            {
                var location = contract.Locations.FirstOrDefault();

                if (location is null)
                {
                    continue;
                }

                var problem = Describe(contract);

                if (problem is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, location, contract.Name, problem));
                }
            }
        }

        private static string? Describe(INamedTypeSymbol contract)
        {
            var methods = contract.GetMembers().OfType<IMethodSymbol>()
                .Where(method => method.MethodKind == MethodKind.Ordinary && !method.IsImplicitlyDeclared);

            if (methods.Any())
            {
                return "declares a method";
            }

            // Primary constructors (records, and classes with a primary constructor) are allowed: they declare no
            // constructor syntax. A hand-written constructor is a routine and is not allowed.
            var handWritten = contract.Constructors.Any(constructor => constructor.DeclaringSyntaxReferences
                .Any(reference => reference.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.ConstructorDeclarationSyntax));

            if (handWritten)
            {
                return "declares a constructor";
            }

            // A collection of a flat model is allowed; a model held directly is not (2.3.2.2.0). Static members
            // describe the type rather than its data, so they are not part of the contract.
            var nested = contract.GetMembers().OfType<IPropertySymbol>()
                .Where(property => !property.IsStatic && !IsCollection(property.Type))
                .SelectMany(property => Unwrap(property.Type))
                .FirstOrDefault(IsLocalModel);

            return nested is null ? null : $"holds the nested model '{nested.Name}'";
        }

        private static bool IsCollection(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol)
            {
                return true;
            }

            if (type.SpecialType == SpecialType.System_String)
            {
                return false;
            }

            var enumerable = SpecialType.System_Collections_Generic_IEnumerable_T;

            return type.OriginalDefinition.SpecialType == enumerable
                || type.AllInterfaces.Any(interfaceType => interfaceType.OriginalDefinition.SpecialType == enumerable);
        }

        private static bool IsContractCandidate(INamedTypeSymbol named) =>
            named.TypeKind is TypeKind.Class or TypeKind.Struct
            && named.DeclaringSyntaxReferences.Length > 0
            && !Layers.InSegment(named, "Services")
            && !Layers.InSegment(named, "Exposers")
            && !Layers.InSegment(named, "Brokers")
            && !IsException(named);

        private static bool IsLocalModel(INamedTypeSymbol named) =>
            named.TypeKind is TypeKind.Class or TypeKind.Struct
            && named.DeclaringSyntaxReferences.Length > 0
            && !IsException(named);

        private static bool IsException(INamedTypeSymbol named)
        {
            for (var type = named; type is not null; type = type.BaseType)
            {
                if (type.Name == "Exception" && type.ContainingNamespace?.Name == "System")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Yields the named types inside a signature, looking through generic arguments and arrays.</summary>
        private static System.Collections.Generic.IEnumerable<INamedTypeSymbol> Unwrap(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                foreach (var inner in Unwrap(array.ElementType))
                {
                    yield return inner;
                }

                yield break;
            }

            if (type is not INamedTypeSymbol named)
            {
                yield break;
            }

            yield return named;

            foreach (var argument in named.TypeArguments)
            {
                foreach (var inner in Unwrap(argument))
                {
                    yield return inner;
                }
            }
        }
    }
}
