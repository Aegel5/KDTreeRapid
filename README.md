# KDTreeRapid

The Kd-tree is written entirely in C# without any dependencies.

## About
- Fully static. After any updates need rebuild.
- Designed for fast rebuild. Actually, building a tree has the same speed as just sorting array.
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
        List<(Node elem, double dist)> res = new();
        double rad = 5;
        kdtree.SearchSorted(elements, [10, 20], res, rad * rad, max_cnt: 1);
        Console.WriteLine($"Nearest point: {res[0].elem.X} {res[0].elem.Y}");
    }
}
```
Full example: https://github.com/Aegel5/KDTreeRapid/blob/main/KDTreeRapid_Test/Program.cs

## Benchmarks
Build 300000 points
```
| Method                 | Mean      | Error     | StdDev    | Median    |
|----------------------- |----------:|----------:|----------:|----------:|
| KDTreeRapid_BuildTree  | 110.43 ms |  2.064 ms |  2.825 ms | 109.54 ms |
| Supercluster_BuildTree | 941.21 ms | 16.483 ms | 15.418 ms | 939.52 ms |
| List_Sort              |  59.65 ms |  1.054 ms |  2.643 ms |  58.76 ms |
```
Search 3 nearest for each point
```
| Method              | Mean     | Error    | StdDev   |
|-------------------- |---------:|---------:|---------:|
| KDTreeRapid_Search  | 262.5 ms |  3.94 ms |  3.50 ms |
| Supercluster_Search | 765.3 ms | 14.52 ms | 15.54 ms |
```
