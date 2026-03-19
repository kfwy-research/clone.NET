using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CloneGenerator;

public static class Helper
{
    public static IEnumerable<ISymbol> GetAllMembers(this INamedTypeSymbol symbol, bool withoutOverride = true)
    {
        // Iterate Parent -> Derived
        if (symbol.BaseType != null)
        {
            foreach (var item in GetAllMembers(symbol.BaseType))
            {
                // override item already iterated in parent type
                if (!withoutOverride || !item.IsOverride)
                {
                    yield return item;
                }
            }
        }

        foreach (var item in symbol.GetMembers())
        {
            if (!withoutOverride || !item.IsOverride)
            {
                yield return item;
            }
        }
    }

    public static bool IsNoBackingFieldGetterSetter(this PropertyDeclarationSyntax property)
    {
        return property.AccessorList?.Accessors.Count(x => x.Body is null) == 2;
    }

    public static void ThrowUnhandled(SourceProductionContext ctx,ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault();
        ctx.ReportDiagnostic(Diagnostic.Create(Errors.UnkownTypeError,
            location,
            symbol.Name));
    }
}
