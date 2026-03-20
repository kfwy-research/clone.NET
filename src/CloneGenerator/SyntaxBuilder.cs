using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CloneGenerator;

public abstract class SyntaxBuilder
{
    protected SyntaxBuilder(string indent)
    {
        Indent = indent;
    }

    protected string Indent;
    protected StringBuilder _sb = new();
    protected List<SyntaxBuilder> _builders = new();

    protected abstract void CreateBegin();
    protected abstract void CreateEnd();

    public string Build()
    {
        var childSb = new StringBuilder();

        try
        {
            foreach (var builder in _builders)
            {
                childSb.Append(builder.Build());
            }
        }
        catch (UnhandledCloneTypeException ex)
        {
            childSb.AppendLine(SymbolDisplay.FormatLiteral(ex.Message, false));
        }

        CreateBegin();
        _sb.Append(childSb);
        CreateEnd();

        return _sb.ToString();
    }
}

public class NamespaceBuilder : SyntaxBuilder
{
    private string? _ns;

    public NamespaceBuilder(string? ns) : base(string.Empty)
    {
        _ns = ns;
    }


    public ClassBuilder CreateClass(INamedTypeSymbol symbol, Compilation compilation)
    {
        var builder = new ClassBuilder(symbol, compilation, _ns, _ns is null ? string.Empty : "    ");
        _builders.Add(builder);
        return builder;
    }

    protected override void CreateBegin()
    {
        _sb.AppendLine("using Clone;");
        _sb.AppendLine();

        if (_ns is null)
        {
            return;
        }

        _sb.AppendLine($"namespace {_ns}");
        _sb.AppendLine($"{Indent}{{");
    }

    protected override void CreateEnd()
    {
        if (_ns is null)
        {
            return;
        }

        _sb.AppendLine("}");
    }
}

public class ClassBuilder : SyntaxBuilder
{
    private readonly INamedTypeSymbol _symbol;
    private readonly Compilation _compilation;
    private readonly string? _ns;

    public ClassBuilder(INamedTypeSymbol symbol, Compilation compilation, string? ns, string indent) : base(indent)
    {
        _symbol = symbol;
        _compilation = compilation;
        _ns = ns;
    }


    public void CreateField(SourceProductionContext ctx, ISymbol syntax, Compilation compilation)
    {
        _builders.Add(new FieldBuilder(ctx, syntax, compilation, Indent + "    "));
    }

    public void CreateProperty(IPropertySymbol symbol)
    {
        // TODO
    }

    public string GetFullName()
    {
        return _symbol.Name;
    }

    protected override void CreateBegin()
    {
        string clazzName = _symbol.Name;
        string clonePrefix = "";

        if (_symbol.BaseType?.GetAttributes()
            .Any(ad => ad.AttributeClass?.ToDisplayString() == "Clone.CloneableAttribute") is true)
        {
            clonePrefix = " override";
        }
        else
        {
            clonePrefix = " virtual";
        }

        _sb.AppendLine($"{Indent}partial class {clazzName}: IClone<{clazzName}>");
        _sb.AppendLine($"{Indent}{{");

        _sb.AppendLine($"{Indent}    public{clonePrefix} {clazzName} Clone()");
        _sb.AppendLine($"{Indent}    {{");
        _sb.AppendLine($"{Indent}        {clazzName} target = new ();");
        _sb.AppendLine($"{Indent}        Clone0(target);");
        _sb.AppendLine($"{Indent}        return target;");
        _sb.AppendLine($"{Indent}    }}\n");

        _sb.AppendLine($"{Indent}    public virtual void Clone0({clazzName} target)");
        _sb.AppendLine($"{Indent}    {{");

        if (_symbol.BaseType is not null &&
            _symbol.BaseType.GetAttributes().Any(x => x.ToString() is "Clone.CloneableAttribute"))
        {
            _sb.AppendLine($"{Indent}        base.Clone0(target);");
        }
    }

    protected override void CreateEnd()
    {
        _sb.AppendLine($"{Indent}    }}");
        _sb.AppendLine($"{Indent}}}");
    }
}

class FieldBuilder : SyntaxBuilder
{
    private readonly SourceProductionContext _ctx;
    private readonly ISymbol _symbol;
    private readonly Compilation _compilation;
    private int _version;
    private int _id;
    private static int _idStore;

    public FieldBuilder(SourceProductionContext ctx, ISymbol symbol, Compilation compilation, string indent) :
        base(indent)
    {
        _ctx = ctx;
        _symbol = symbol;
        _compilation = compilation;
        _id = ++_idStore;
    }

    protected override void CreateBegin()
    {
        ITypeSymbol typeSymbol = null;
        switch (_symbol)
        {
            case IFieldSymbol s:
                typeSymbol = s.Type;
                break;
            case IPropertySymbol s:
                typeSymbol = s.Type;
                foreach (var reference in _symbol.DeclaringSyntaxReferences)
                {
                    if (reference.GetSyntax() is PropertyDeclarationSyntax node &&
                        !node.IsNoBackingFieldGetterSetter())
                    {
                        // not { set; get; } like
                        return;
                    }
                }

                break;
            default:
                Helper.ThrowUnhandled(_ctx, _symbol);
                break;
        }

        _sb.AppendLine();
        GenerateExpression(typeSymbol!, $"target.{_symbol.Name}", _symbol.Name, Indent);
    }

    private string GenerateExpression(ITypeSymbol symbol, string left, string right, string Indent)
    {
        TypedConstantKind kind = SyntaxHelper.GetTypedConstantKind(symbol, _compilation);
        string ver = $"_{_id}_{_version}";
        string rt = left;

        bool noLeft = left.Length == 0;
        if (left.Length == 0)
        {
            left = $"{symbol} r{ver}";
            rt = $"r{ver}";
        }

        var parentVar = noLeft ? "null" : left;
        var comment = noLeft ? "//" : "";

        switch (kind)
        {
            case TypedConstantKind.Error:
                _sb.AppendLine(
                    $"{Indent}    throw new Exception(\"error type {symbol} {right}\");");
                break;
            case TypedConstantKind.Type:
            {
                var type = (INamedTypeSymbol)symbol;
                if (type.IsGenericType)
                {
                    switch (type.ConstructUnboundGenericType().ToString())
                    {
                        case "System.Collections.Generic.List<>":
                        {
                            var argSymbol = type.TypeArguments.First();

                            _sb.AppendLine($$"""
                                             {{Indent}}    {{type}} r{{ver}} = {{parentVar}};
                                             {{Indent}}    if ({{right}} is null) {
                                             {{Indent}}        {{comment}} {{parentVar}} = null;
                                             {{Indent}}    }
                                             {{Indent}}    else {
                                             {{Indent}}        if (r{{ver}} is null) r{{ver}} = new ({{right}}.Count);
                                             {{Indent}}        {{type}} src{{ver}} = {{right}};
                                             {{Indent}}        foreach({{argSymbol}} itm{{ver}} in src{{ver}})
                                             {{Indent}}        {
                                             """);
                            _version++;

                            var rvar = GenerateExpression(argSymbol, string.Empty,
                                $"itm{ver}", Indent + "        ");

                            // int i = 2;
                            // int[][] a = new int[][i];
                            _sb.AppendLine($$"""{{Indent}}            r{{ver}}.Add({{rvar}});""");
                            if (!noLeft)
                            {
                                _sb.AppendLine($$"""{{Indent}}            {{left}} = r{{ver}};""");
                            }

                            _sb.AppendLine($$"""
                                             {{Indent}}        }
                                             {{Indent}}    }
                                             """);
                            break;
                        }
                        case "System.Collections.Generic.Dictionary<,>":
                        {
                            _sb.AppendLine($$"""
                                             {{Indent}}    {{type}} r{{ver}} = {{parentVar}};
                                             {{Indent}}    if ({{right}} is null) {
                                             {{Indent}}        {{comment}}{{parentVar}} = null;
                                             {{Indent}}    }
                                             {{Indent}}    else {
                                             {{Indent}}        if (r{{ver}} is null) r{{ver}} = new ({{right}}.Count);
                                             """);

                            if (!noLeft)
                            {
                                _sb.AppendLine($$"""{{Indent}}        {{left}} = r{{ver}};""");
                            }

                            _sb.AppendLine($$"""
                                             {{Indent}}        {{type}} src{{ver}} = {{right}};
                                             {{Indent}}        foreach(var kv{{ver}} in  src{{ver}})
                                             {{Indent}}        {
                                             """);
                            _version++;
                            var rtk = GenerateExpression(type.TypeArguments.First(), string.Empty,
                                $"kv{ver}.Key", Indent + "        ");

                            _version++;
                            var rtv = GenerateExpression(type.TypeArguments.Last(), string.Empty,
                                $"kv{ver}.Value", Indent + "        ");

                            _sb.AppendLine($$"""
                                             {{Indent}}            r{{ver}}.Add({{rtk}}, {{rtv}});
                                             {{Indent}}        }
                                             {{Indent}}    }
                                             """);
                            break;
                        }
                        case "System.Collections.Generic.HashSet<>":
                        {
                            _sb.AppendLine($$"""
                                             {{Indent}}    {{type}} r{{ver}} = {{parentVar}};
                                             {{Indent}}    if ({{right}} is null) {
                                             {{Indent}}        {{comment}}{{parentVar}} = null;
                                             {{Indent}}    }
                                             {{Indent}}    else {
                                             {{Indent}}        if (r{{ver}} is null) r{{ver}} = new ();
                                             """);

                            if (!noLeft)
                            {
                                _sb.AppendLine($$"""{{Indent}}        {{left}} = r{{ver}};""");
                            }

                            _sb.AppendLine($$"""
                                             {{Indent}}        {{type}} src{{ver}} = {{right}};
                                             {{Indent}}        foreach(var itm{{ver}} in  src{{ver}})
                                             {{Indent}}        {
                                             """);
                            _version++;
                            var rtv = GenerateExpression(type.TypeArguments.Last(), string.Empty,
                                $"itm{ver}", Indent + "        ");

                            _sb.AppendLine($$"""
                                             {{Indent}}            r{{ver}}.Add({{rtv}});
                                             {{Indent}}        }
                                             {{Indent}}    }
                                             """);
                            break;
                        }
                        default:
                        {
                            Helper.ThrowUnhandled(_ctx, _symbol);
                            break;
                        }
                    }
                }
                else
                {
                    if (!type.GetAttributes().Any(x => x.ToString() == "Clone.CloneableAttribute"))
                    {
                        Helper.ThrowUnhandled(_ctx, _symbol);
                    }

                    _sb.AppendLine($"{Indent}    {left} =  new ();");
                    _sb.AppendLine($"{Indent}    {right}.Clone0({rt});");
                }
            }
                break;
            case TypedConstantKind.Array:
            {
                var type = (IArrayTypeSymbol)symbol;
                var s = type.ToString();
                var newSyntax = s = s.Remove(s.IndexOf('[')) + $"[{right}.Length" + s.Substring(s.IndexOf('[') + 1);
                _sb.AppendLine($$"""
                                 {{Indent}}    {{type}} r{{ver}} = {{parentVar}};
                                 {{Indent}}    if ({{right}} is null) {
                                 {{Indent}}        {{comment}}{{parentVar}} = null;
                                 {{Indent}}    }
                                 {{Indent}}    else {
                                 {{Indent}}        if (r{{ver}} is null) r{{ver}} = new {{newSyntax}};
                                 """);

                if (SyntaxHelper.GetTypedConstantKind(type.ElementType, _compilation) is TypedConstantKind.Primitive)
                {
                    _sb.AppendLine($$"""
                                     {{Indent}}        {{right}}.CopyTo(r{{ver}}.AsSpan());
                                     {{Indent}}        {{comment}} {{parentVar}} = r{{ver}};
                                     {{Indent}}    }
                                     """);
                }
                else
                {
                    _sb.AppendLine($"{Indent}        for(int i{ver} = 0; i{ver} < {right}.Length; i{ver}++)");
                    _sb.AppendLine($"{Indent}        {{");
                    _version++;
                    var rtv = GenerateExpression(type.ElementType, string.Empty,
                        $"{right}[i{ver}]", Indent + "        ");

                    _sb.AppendLine($$"""
                                     {{Indent}}            r{{ver}}[i{{ver}}] = {{rtv}};
                                     {{Indent}}        }
                                     {{Indent}}        {{comment}} {{parentVar}} = r{{ver}};
                                     {{Indent}}    }
                                     """);
                }

                break;
            }
            case TypedConstantKind.Enum:
            case TypedConstantKind.Primitive:
                _sb.AppendLine($"{Indent}    {left} = {right};");

                break;
            default:
            {
                Helper.ThrowUnhandled(_ctx, _symbol);
                break;
            }
        }

        return rt;
    }


    protected override void CreateEnd()
    {
    }
}

public class UnhandledCloneTypeException : Exception
{
    internal UnhandledCloneTypeException(string s) : base(s)
    {
    }
}

public static class SyntaxHelper
{
    internal static TypedConstantKind GetTypedConstantKind(ITypeSymbol type, Compilation compilation)
    {
        if (type.Name == "Type")
        {
            return TypedConstantKind.Primitive;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_Byte:
            case SpecialType.System_UInt16:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Char:
            case SpecialType.System_String:
            case SpecialType.System_Object:
                return TypedConstantKind.Primitive;
            default:
                switch (type.TypeKind)
                {
                    case TypeKind.Array:
                        return TypedConstantKind.Array;
                    case TypeKind.Enum:
                        return TypedConstantKind.Enum;
                    case TypeKind.Error:
                        return TypedConstantKind.Error;
                }

                return TypedConstantKind.Type;
            // throw new Exception("wtf is this type");

            // default:
            //     switch (type.TypeKind)
            //     {
            //         case TypeKind.Array:
            //             return TypedConstantKind.Array;
            //         case TypeKind.Enum:
            //             return TypedConstantKind.Enum;
            //         case TypeKind.Error:
            //             return TypedConstantKind.Error;
            //     }
            //
            //     if (compilation != null &&
            //         compilation.IsSystemTypeReference(type))
            //     {
            //         return TypedConstantKind.Type;
            //     }
            //
            //     return TypedConstantKind.Error;
        }
    }
}
