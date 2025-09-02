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
var elements = Generate(1000);
KDTreeRapid<double, Node> kdtree = new();
var span = CollectionsMarshal.AsSpan(elements);
kdtree.BuildInPlace(span, 2);
List<(Node elem, double dist)> res = new();
double rad = 5;
kdtree.SearchSorted(span, new double[]{10,20}, res, rad*rad, 10);
```
