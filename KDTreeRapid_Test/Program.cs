using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using KDTreeRapid;
using Supercluster.KDTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace KDTree_Test;

class Node : IKDTreeElement<double> {
    public double[] point = new double[10];
    public double GetForDimension(int dim_index) {
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

        void CheckRecursive(
            Span<Node> elements,
            int dim_cnt,
            int depth = 0
            ) {
            if (elements.Length <= 1)
                return;
            int axis = depth % dim_cnt;
            int median_i = elements.Length / 2;
            var node = elements[median_i];
            for (int i = 0; i < median_i; i++) {
                if (elements[i].GetForDimension(axis) > node.GetForDimension(axis)) {
                    throw new Exception("bad tree");
                }
            }
            for (int i = median_i + 1; i < elements.Length; i++) {
                if (elements[i].GetForDimension(axis) < node.GetForDimension(axis)) {
                    throw new Exception("bad tree");
                }
            }
            CheckRecursive(elements.Slice(0, median_i), dim_cnt, depth + 1);
            CheckRecursive(elements.Slice(median_i + 1, elements.Length - 1 - median_i), dim_cnt, depth + 1);
        }

        foreach (var DIM in new[] { 2, 5, 10 }) {
            for (int i = 0; i < 100; i++) {
                var elements = Generate(1000);
                KDTreeRapid<double, Node> kdtree = new();
                kdtree.BuildInPlace(elements, DIM);
                CheckRecursive(CollectionsMarshal.AsSpan(elements), DIM);

                for (int k = 0; k < 10; k++) {
                    var p = new double[DIM];
                    for (int j = 0; j < p.Length; j++) {
                        p[j] = rnd.NextDouble() * 10;
                    }
                    double rad = rnd.NextDouble() * 100;
                    int max_cnt = rnd.Next(0, elements.Count);
                    var res = kdtree.SearchSorted(elements, p, rad, max_cnt);

                    var need = elements.Where(x => kdtree.DistanceL2(p, x) <= rad).OrderBy(x => kdtree.DistanceL2(p, x)).Take(max_cnt);
                    if (!need.SequenceEqual(res.Select(x => x.element))) {
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
                for (int i = 0; i < N; i++) {
                    var elements = Generate(CNT);
                    KDTreeRapid<double, Node> kdtree = new();
                    var timer_build = Stopwatch.StartNew();
                    kdtree.BuildInPlace(elements, DIM);
                    build_time += timer_build.Elapsed.TotalSeconds;
                    foreach (var elem in elements) {
                        // для каждой точки найдем 3 ближайшие
                        var res = kdtree.SearchSorted(elements, elem.point.Take(DIM).ToArray(), double.MaxValue, 3);
                        int cnt = 1;
                        foreach (var r in res) {
                            sign += r.element.GetForDimension(0) * cnt++;
                            
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
        BenchmarkRunner.Run( typeof(Program).Assembly, new Config());
        TestSpeed();
    }
}
public class CustomLogger : ILogger {
    
    public string Id => "Mylogger";

    public int Priority => 123;

    public void Write(LogKind logKind, string text) {
        if (logKind == LogKind.Statistic || logKind == LogKind.Error) {
            ConsoleLogger.Default.Write(logKind, text);
        }
    }

    public void WriteLine(LogKind logKind, string text) {
        Write(logKind, text + Environment.NewLine);
    }

    public void WriteLine() {
        Console.WriteLine();
    }

    public void Flush() {
        // Implement any necessary flush logic for your custom logger
    }
}
public class Config : ManualConfig {
    public Config() {
        AddLogger(new CustomLogger());
        //AddLogger(ConsoleLogger.Default);
        AddColumn(TargetMethodColumn.Method);
        AddColumn(StatisticColumn.Mean);     
        AddColumn(StatisticColumn.StdDev); 
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
        kdtree.BuildInPlace(elements, 2);
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
        kdtree.BuildInPlace(elements, 2);

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
        List<(Node, double)> result = new();
        foreach (var elem in elements) {
            // для каждой точки найдем 3 ближайшие
            var res = kdtree.SearchSorted(elements, elem.point.Take(2).ToArray(), max_cnt:3, result: result);
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
