# KDTreeRapid

The Kd-tree is written entirely in C# without any dependencies.

## About
- Fully static. After any updates need rebuild.
- Designed for fast rebuild. Actually, building the tree has the same speed as just sorting an array.
- Fast search. All functions do minimum allocations, thus no pressure for GC.

## Features
- Find nearest and radius search.
- Custom user search using lambda `Visit`

## Example
```csharp
class Node : IKDTreeElement<double> {
    public double X;
    public double Y;
    public double GetForDimension(int dim_index) {
        return dim_index == 0 ? X : Y;
    }
}
internal class Program {
    static void Main(string[] args) {
        List<Node> elements = Generate(1000);
        KDTreeRapid<double, Node> kdtree = new();
        kdtree.BuildInPlace(elements, dimensions:2);
        var res = kdtree.SearchSorted(elements, [10, 20], max_cnt: 1);
        Console.WriteLine($"Nearest point: {res[0].element.X} {res[0].element.Y}");
    }
}
```
Full example: https://github.com/Aegel5/KDTreeRapid/blob/main/KDTreeRapid_Test/Program.cs

## Benchmarks
Build 300000 points
```
| Method                 | Mean      | StdDev    |
|----------------------- |----------:|----------:|
| KDTreeRapid_BuildTree  | 125.75 ms |  6.381 ms |
| Supercluster_BuildTree | 999.82 ms | 29.974 ms |
| List_Sort              |  66.13 ms |  2.099 ms |
```
Search 3 nearest for each point
```
| Method              | Mean     | StdDev   |
|-------------------- |---------:|---------:|
| KDTreeRapid_Search  | 255.2 ms |  2.62 ms |
| Supercluster_Search | 722.9 ms | 13.99 ms |
```
