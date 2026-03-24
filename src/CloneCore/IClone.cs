using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Clone;

public interface IClone<T> where T : class, new()
{
    void Clone0(T target);
}

public static class CloneExtensions
{
    private static readonly Dictionary<Type, Func<object, object>> Cache = new();

    public static T Clone<T>(this T? t) where T : class, IClone<T>, new()
    {
        if (t == null)
        {
            return null!;
        }

        Type typ = t.GetType();

        if (Cache.TryGetValue(typ, out var method))
        {
            return (T)method(t);
        }

        bool itf = typ.GetInterfaces().Any(x => x == typeof(IClone<T>));
        var methodInfo = typ.GetMethods().FirstOrDefault(x => x.DeclaringType == typ && x.Name == "Clone0");

        if (!itf || methodInfo is null)
        {
            throw new Exception($"{typ} isn't cloneable object");
        }

        var param = Expression.Parameter(typeof(object));
        var target = Expression.Variable(typ, "target");
        var block = Expression.Block([target],
            Expression.Assign(target, Expression.New(typ)),
            Expression.Call(Expression.Convert(param, typ), methodInfo, Expression.Convert(target, typ)),
            target
        );
        method = Expression.Lambda<Func<object, object>>(block, param).Compile();

        Cache[typ] = method;

        return (T)method(t);
    }
}
