using KDTreeRapid;
using Supercluster.KDTree;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace KDTree_Test; 
internal class Program {

    static Random rnd2 = new Random(123);

    static Func<double[], double[], double> L2Norm = (x, y) =>
    {
        double dist = 0f;
        for (int i = 0; i < x.Length; i++) {
            dist += (x[i] - y[i]) * (x[i] - y[i]);
        }

        return dist;
    };

    class Node : IKDTreeElement<double> {
        public double x;
        public double y;
        public double[] arr;
        public int Index { get; set; }

        public double GetForDimension(int dim_index) {
            //if (dim_index == 0) return x;
            //if (dim_index == 1) return y;
            return arr[dim_index];
        }
    }
    static List<Node> Generate(int N) {
        List<Node> res = new();
        for (int i = 0; i < N; i++) {
            res.Add(new());
            res[^1].arr = new double[10];
            for (int j = 0; j < 10; j++) {
                res[^1].arr[j] = rnd2.NextDouble() * 10;
            }
        }
        return res;
    }
    static void TestCorrect() {

        List<(Node elem, double dist)> res = new();
        foreach (var DIM in new[] { 2, 5, 10 }) {
            for (int i = 0; i < 1000; i++) {
                var elements = Generate(1000);
                KDTreeRapid<double, Node> kdtree = new();
                var span = CollectionsMarshal.AsSpan(elements);
                kdtree.BuildInPlace(span, DIM);
                kdtree.Check(span, DIM);
                var p = new double[DIM];
                for(int j = 0; j < p.Length; j++) {
                    p[j] = rnd2.NextDouble() * 10;
                }
                double rad = rnd2.NextDouble() * 100;
                int max_cnt = rnd2.Next(0, elements.Count);
                kdtree.SearchSorted(span, p, res, rad, max_cnt);

                var need = elements.Where(x => kdtree.DistanceL2(p, x) <= rad).OrderBy(x => kdtree.DistanceL2(p, x)).Take(max_cnt);
                if (!need.SequenceEqual(res.Select(x => x.elem))) {
                    throw new Exception("bad");
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
        {
            var timer = Stopwatch.StartNew();
            double sign = 0;
            rnd2 = new Random(seed);
            for (int i = 0; i < N; i++) {
                var elements = Generate(CNT);
                elements.Sort((x, y) => x.x.CompareTo(y.x));
                foreach (var elem in elements) {
                    sign += elem.GetForDimension(1);
                }
            }
            Console.WriteLine($"SORTING {timer.Elapsed.TotalSeconds} {sign}");
        }
        foreach (var DIM in new []{ 2, 3, 5 }) {
            Console.WriteLine($"DIMINSIONS {DIM}");
            {
                var timer = Stopwatch.StartNew();
                double build_time = 0;
                rnd2 = new Random(seed);
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
                        kdtree.SearchSorted(span, elem.arr.Take(DIM).ToArray(), res, double.MaxValue, 3);
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
                rnd2 = new Random(seed);
                for (int i = 0; i < N; i++) {
                    var elements = Generate(CNT);
                    var span = CollectionsMarshal.AsSpan(elements);
                    var timer_build = Stopwatch.StartNew();
                    KDTree<double, Node> kdtree = new(dimensions: DIM,
                        points: elements.Select(x => x.arr.Take(DIM).ToArray()).ToArray(),
                        nodes: elements.ToArray(),
                        metric: L2Norm);
                    build_time += timer_build.Elapsed.TotalSeconds;
                    foreach (var elem in elements) {
                        // для каждой точки найдем 3 ближайшие
                        var res = kdtree.NearestNeighbors(elem.arr.Take(DIM).ToArray(), 3);
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
        TestSpeed();
    }
}
