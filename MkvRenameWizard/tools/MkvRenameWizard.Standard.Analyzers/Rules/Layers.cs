using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MkvRenameWizard.Standard.Analyzers.Rules;

internal static class Layers
{
    private static readonly string[] CategorySuffixes =
    {
        "DependencyValidationException",
        "ValidationException",
        "DependencyException",
        "ServiceException",
    };

    private static readonly string[] UtilityNameSuffixes =
    {
        "Utils",
        "Util",
        "Utilities",
        "Helper",
        "Helpers",
        "Common",
        "Commons",
    };

    /// <summary>The category markers a layer catches by (Models/Categories).</summary>
    public static bool IsMarker(ISymbol type) =>
        type.Name is "IFailureCategory" or "IValidationCategory" or "IDependencyCategory" or "IServiceCategory";

    /// <summary>True when any containing namespace of the symbol has the given name, so layers are read from folders and namespaces (1.3).</summary>
    public static bool InSegment(ISymbol? symbol, string segment)
    {
        for (var ns = symbol?.ContainingNamespace; ns is not null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
        {
            if (ns.Name == segment)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Heuristic for categorized exceptions: the name ends with a category suffix. A "Failed" prefix marks a
    /// localized exception that happens to end the same way, such as FailedStudentServiceException.
    /// </summary>
    public static bool IsCategorized(string typeName) =>
        !typeName.StartsWith("Failed", StringComparison.Ordinal)
        && CategorySuffixes.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal));

    public static bool IsUtilityName(string typeName) =>
        UtilityNameSuffixes.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal));

    /// <summary>Framework assemblies are the base library; anything else is a package the Standard routes through brokers (1.8).</summary>
    public static bool IsFramework(IAssemblySymbol assembly)
    {
        var name = assembly.Name;

        return name == "System"
            || name.StartsWith("System.", StringComparison.Ordinal)
            || name == "mscorlib"
            || name == "netstandard"
            || name.StartsWith("Microsoft.CSharp", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.VisualBasic", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Win32", StringComparison.Ordinal);
    }

    /// <summary>The constructor with the most parameters, which is the primary constructor when one exists.</summary>
    public static ImmutableArray<IParameterSymbol> ConstructorParameters(INamedTypeSymbol type)
    {
        var constructor = type.Constructors
            .Where(candidate => !candidate.IsStatic)
            .OrderByDescending(candidate => candidate.Parameters.Length)
            .FirstOrDefault();

        return constructor is null ? ImmutableArray<IParameterSymbol>.Empty : constructor.Parameters;
    }
}
