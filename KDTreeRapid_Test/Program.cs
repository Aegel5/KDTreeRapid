using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using KDTreeRapid;
using Supercluster.KDTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;


namespace KDTree_Test;

class Node : IKDTreeElement<double> {
    //public double x;
    //public double y;
    public double[] point = new double[10];
    public int Index { get; set; }

    public double GetForDimension(int dim_index) {
        //if (dim_index == 0) return x;
        //if (dim_index == 1) return y;
        return point[dim_index];
    }
}

class Program {

    static Random rnd = new Random(123);

    public static List<Node> Generate(int N, Random? r = null) {
        List<Node> res = new();
        for (int i = 0; i < N; i++) {
            res.Add(new());
            for (int j = 0; j < res[^1].point.Length; j++) {
                res[^1].point[j] = (r ?? rnd).NextDouble() * 10;
            }
        }
        return res;
    }
    static void TestCorrect() {

        List<(Node elem, double dist)> res = new();
        foreach (var DIM in new[] { 2, 5, 10 }) {
            for (int i = 0; i < 100; i++) {
                var elements = Generate(1000);
                KDTreeRapid<double, Node> kdtree = new();
                var span = CollectionsMarshal.AsSpan(elements);
                kdtree.BuildInPlace(span, DIM);
                kdtree.Check(span, DIM);

                for (int k = 0; k < 10; k++) {
                    var p = new double[DIM];
                    for (int j = 0; j < p.Length; j++) {
                        p[j] = rnd.NextDouble() * 10;
                    }
                    double rad = rnd.NextDouble() * 100;
                    int max_cnt = rnd.Next(0, elements.Count);
                    kdtree.SearchSorted(span, p, res, rad, max_cnt);

                    var need = elements.Where(x => kdtree.DistanceL2(p, x) <= rad).OrderBy(x => kdtree.DistanceL2(p, x)).Take(max_cnt);
                    if (!need.SequenceEqual(res.Select(x => x.elem))) {
                        throw new Exception("bad");
                    }
                }
            }
        }
        Console.WriteLine("TEST OK");
    }
    static void TestSpeed() {
        int N = 300;
        int CNT = 10000;
        //int seed = rnd2.Next();
        int seed = 11122;

        foreach (var DIM in new []{ 2, 3, 5 }) {
            Console.WriteLine($"DIMENSIONS {DIM}");
            {
                var timer = Stopwatch.StartNew();
                double build_time = 0;
                rnd = new Random(seed);
                double sign = 0;
                List<(Node elem, double dist)> res = new();
                for (int i = 0; i < N; i++) {
                    var elements = Generate(CNT);
                    KDTreeRapid<double, Node> kdtree = new();
                    var span = CollectionsMarshal.AsSpan(elements);
                    var timer_build = Stopwatch.StartNew();
                    kdtree.BuildInPlace(span, DIM);
                    build_time += timer_build.Elapsed.TotalSeconds;
                    foreach (var elem in elements) {
                        // для каждой точки найдем 3 ближайшие
                        kdtree.SearchSorted(span, elem.point.Take(DIM).ToArray(), res, double.MaxValue, 3);
                        int cnt = 1;
                        foreach (var r in res) {
                            sign += r.elem.GetForDimension(0) * cnt++;
                            
                        }
                    }
                }
                Console.WriteLine($"KDTreeRapid {timer.Elapsed.TotalSeconds} {sign} {build_time}");
            }
            {
                var timer = Stopwatch.StartNew();
                double sign = 0;
                double build_time = 0;
                rnd = new Random(seed);
                for (int i = 0; i < N; i++) {
                    var elements = Generate(CNT);
                    var span = CollectionsMarshal.AsSpan(elements);
                    var timer_build = Stopwatch.StartNew();
                    KDTree<double, Node> kdtree = new(dimensions: DIM,
                        points: elements.Select(x => x.point.Take(DIM).ToArray()).ToArray(),
                        nodes: elements.ToArray(),
                        metric: (x, y) => {
                            double dist = 0;
                            for (int i = 0; i < x.Length; i++) {
                                dist += (x[i] - y[i]) * (x[i] - y[i]);
                            }
                            return dist;
                        });
                    build_time += timer_build.Elapsed.TotalSeconds;
                    foreach (var elem in elements) {
                        // для каждой точки найдем 3 ближайшие
                        var res = kdtree.NearestNeighbors(elem.point.Take(DIM).ToArray(), 3);
                        int cnt = 1;
                        foreach (var r in res) {
                            sign += r.Item2.GetForDimension(0) * cnt++;
                        }
                    }
                }
                Console.WriteLine($"Supercluster {timer.Elapsed.TotalSeconds} {sign} {build_time}");
            }
        }
    }
    static void Main(string[] args) {
        TestCorrect();
        BenchmarkRunner.Run( typeof(Program).Assembly);
        TestSpeed();
    }
}

public class MyBenchmarks {

    static List<Node> elements;

    [IterationSetup]
    public void Setup() {
        elements = Program.Generate(300000, new Random(999));
    }

    [Benchmark]
    public void KDTreeRapid_BuildTree() {
        KDTreeRapid<double, Node> kdtree = new();
        var span = CollectionsMarshal.AsSpan(elements);
        kdtree.BuildInPlace(span, 2);
    }

    [Benchmark]
    public void Supercluster_BuildTree() {
        KDTree<double, Node> kdtree = new(dimensions: 2,
            points: elements.Select(x => x.point.Take(2).ToArray()).ToArray(),
            nodes: elements.ToArray(),
            metric: (x, y) => {
                double dist = 0;
                for (int i = 0; i < x.Length; i++) {
                    dist += (x[i] - y[i]) * (x[i] - y[i]);
                }
                return dist;
            });
    }

    [Benchmark]
    public void List_Sort() {
        elements.Sort((x, y) => x.GetForDimension(0).CompareTo(y.GetForDimension(0)));
    }
}

public class Benchmark_Search {

    List<Node> elements;
    KDTreeRapid<double, Node> kdtree;
    KDTree<double, Node> kdtree2;

    [GlobalSetup]
    public void Setup() {
        elements = Program.Generate(300000, new Random(999));

        kdtree = new();
        kdtree.BuildInPlace(CollectionsMarshal.AsSpan(elements), 2);

        kdtree2 = new(dimensions: 2,
    points: elements.Select(x => x.point.Take(2).ToArray()).ToArray(),
    nodes: elements.ToArray(),
    metric: (x, y) => {
        double dist = 0;
        for (int i = 0; i < x.Length; i++) {
            dist += (x[i] - y[i]) * (x[i] - y[i]);
        }
        return dist;
    });
    }

    [Benchmark]
    public void KDTreeRapid_Search() {
        List<(Node elem, double dist)> res = new();
        foreach (var elem in elements) {
            // для каждой точки найдем 3 ближайшие
            kdtree.SearchSorted(
                CollectionsMarshal.AsSpan(elements), elem.point.Take(2).ToArray(), res, double.MaxValue, 3);
        }
    }

    [Benchmark]
    public void Supercluster_Search() {
        foreach (var elem in elements) {
            // для каждой точки найдем 3 ближайшие
            var res = kdtree2.NearestNeighbors(elem.point.Take(2).ToArray(), 3);
        }
    }
}
