using System.Diagnostics;
using Clone;
using Mapster;
using MemoryPack;
using Newtonsoft.Json;

A NewA()
{
    return new A()
    {
        Dict = new Dictionary<Type, int> { { typeof(A), 1 } },
        IdArr = [1, 2, 3],
        IdArr2 = [[1], [2]],
        ChildArr = [new Child() { Id = Random.Shared.Next() }],
        ChildArr2 = [[new Child() { Id = Random.Shared.Next() }]],
        Id2 = 1111,

        Ints = [1, 2, 3, 4],
        ListOfList = [[1, 1], [2, 2], [3, 3]],
        ListOfListString = [["aa", "bb"], ["cc"]],
        Meta = new() { { "a", "b" } },
        IdMap = new() { { 1, 2 } },
        MetaMeta = new() { { "meta", new() { { "meta", "value" } } } },
        Child = new Child()
        {
            Id = Random.Shared.Next()
        },
        Children = new()
        {
            { 1, new Child() { Id = Random.Shared.Next() } },
            { 2, new FChild() { Id = Random.Shared.Next(), NameF = "F" } },
            { 3, new MChild() { Id = Random.Shared.Next(), NameM = "M" } },
        },
        IdSet = new()
        {
            { "a", [1, 2, 3] }
        }
    };
}

var source = NewA();

void Measure(Action action)
{
    var sw = Stopwatch.StartNew();

    long i = GC.GetAllocatedBytesForCurrentThread();

    action();
    Console.WriteLine($"total: {sw.ElapsedMilliseconds}ms");
    Console.WriteLine($"bytes: {GC.GetAllocatedBytesForCurrentThread() - i:N0}\n\n");
}

void Dump<T>(T a, T b)
{
    string sourceStr = JsonConvert.SerializeObject(a);
    string cloneStr = JsonConvert.SerializeObject(b);
    Console.WriteLine(sourceStr);
    Console.WriteLine(cloneStr);
    Console.WriteLine(sourceStr == cloneStr);
}

Dump(source.Adapt<A>(), source.Clone());

Measure(() =>
{
    for (int i = 0; i < 1000000; i++)
    {
        MemoryPackSerializer.Deserialize<A>(MemoryPackSerializer.Serialize(source));
    }

    Console.WriteLine("memorypack");
});

Measure(() =>
{
    for (int i = 0; i < 1000000; i++)
    {
        source.Adapt<A>();
    }

    Console.WriteLine("adapt");
});

Measure(() =>
{
    for (int i = 0; i < 1000000; i++)
    {
        source.Clone();
    }

    Console.WriteLine("clone");
});

// var item = new MChild() { Id = Random.Shared.Next(), NameM = "M" };

// Console.WriteLine(JsonConvert.SerializeObject(Cloner.Make((Child)item)));

// var clone = Cloner.Make(source);


// string sourceStr = JsonConvert.SerializeObject(source);
// string cloneStr = JsonConvert.SerializeObject(clone);
// Console.WriteLine(sourceStr);
// Console.WriteLine(cloneStr);

// Console.WriteLine(sourceStr == cloneStr);

// Console.WriteLine(JsonConvert.SerializeObject(Cloner.Make((Z)new Z2() { i = 2, i2 = 3 })));
// ((Z)new Z2()).A();

[Cloneable]
public partial class Test
{
    public List<A> ints = new();
}

public partial class A
{
    public int Anthor = 0;
}

[Cloneable]
partial class Z
{
    public int i = 1;

    public virtual void A()
    {
        Console.WriteLine(111);
    }
}

[Cloneable]
partial class Z2 : Z
{
    public int i2 = 1;

    public virtual void A()
    {
        Console.WriteLine(222);
    }
}


interface If
{
}

[MemoryPackable]
[Cloneable]
public partial class A
{
    public Dictionary<Type, int> Dict;
    private int Id = 0;

    private int ID { get; }

    [CloneIgnore, JsonIgnore] public int Id2 = 0;

    public int[] IdArr;
    public int[][] IdArr2;
    public Child[] ChildArr;
    public Child[][] ChildArr2;
    public List<int> Ints;


    public Dictionary<string, HashSet<int>> IdSet;

    public List<List<int>> ListOfList;
    public List<List<string>> ListOfListString;
    public Dictionary<string, string> Meta;
    public Dictionary<string, Dictionary<string, string>> MetaMeta;


    public Dictionary<int, int> IdMap;


    public Child Child;
    public Dictionary<int, Child> Children;
}

[MemoryPackable]
[Cloneable]
public partial class Child
{
    public int Id = 0;
    public int Id2 = 0;
}


[MemoryPackable]
[Cloneable]
public partial class FChild : Child
{
    public string NameF;
}

[MemoryPackable]
[Cloneable]
public partial class MChild : Child
{
    public string NameM;
}
