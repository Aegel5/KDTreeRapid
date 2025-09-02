using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using System.Xml.Linq;

namespace KDTreeRapid;

public interface IKDTreeElement<T> where T: INumber<T> {
    int Index { get; set; }
    T GetForDimension(int dim_index);
}

public class KDTreeRapid<T, TElement> 
    where T: INumber<T> 
    where TElement : IKDTreeElement<T> {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Less(TElement a, TElement b, int dim) {
        return a.GetForDimension(dim) < b.GetForDimension(dim);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool More(IKDTreeElement<T> a, IKDTreeElement<T> b, int dim) {
        return a.GetForDimension(dim) > b.GetForDimension(dim);
    }
    Random rnd = new();
    void SelectInplace(Span<TElement> a, int rank, int dim) {

        // Numerical Recipes: select
        // http://en.wikipedia.org/wiki/Selection_algorithm

        int low = 0;
        int high = a.Length - 1;

        while (true) {
            if (high <= low + 1) {
                if (high == low + 1 && Less(a[high], a[low], dim)) {
                    (a[low], a[high]) = (a[high], a[low]);
                }

                return;
            }

            // choose pivot: random or just middle
            int middle = (low + high) >> 1;
            //int middle = rnd.Next(low, high + 1);

            (a[middle], a[low + 1]) = (a[low + 1], a[middle]);

            if (More(a[low], a[high], dim)) {
                (a[low], a[high]) = (a[high], a[low]);
            }

            if (More(a[low + 1], a[high], dim)) {
                (a[low + 1], a[high]) = (a[high], a[low + 1]);
            }

            if (More(a[low], a[low + 1], dim)) {
                (a[low], a[low + 1]) = (a[low + 1], a[low]);
            }

            int begin = low + 1;
            int end = high;
            T pivot = a[begin].GetForDimension(dim);
            var pivot_el = a[begin];

            while (true) {
                do {
                    begin++;
                }
                while (a[begin].GetForDimension(dim) < pivot);

                do {
                    end--;
                }
                while (a[end].GetForDimension(dim) > pivot);

                if (end < begin) {
                    break;
                }

                (a[begin], a[end]) = (a[end], a[begin]);
            }

            a[low + 1] = a[end];
            a[end] = pivot_el;

            if (end >= rank) {
                high = end - 1;
            }

            if (end <= rank) {
                low = begin;
            }
        }
    }
    void CheckRecursive(
    Span<TElement> elements,
    int dim_cnt,
    int depth
    ) {
        if (elements.Length <= 1)
            return;
        int axis = depth % dim_cnt;
        int median_i = elements.Length / 2;
        var node = elements[median_i];
        for(int i = 0; i < median_i; i++) {
            if (elements[i].GetForDimension(axis) > node.GetForDimension(axis)) {
                throw new Exception("bad tree");
            }
        }
        for (int i = median_i+1; i < elements.Length; i++) {
            if (elements[i].GetForDimension(axis) < node.GetForDimension(axis)) {
                throw new Exception("bad tree");
            }
        }
        CheckRecursive(Left(elements, median_i), dim_cnt, depth + 1);
        CheckRecursive(Right(elements, median_i), dim_cnt, depth + 1);
    }
    public void Check(Span<TElement> elements, int dim_cnt) {
        CheckRecursive(elements, dim_cnt, 0);
    }
    public void BuildInPlace(Span<TElement> elements, int dim_cnt) {
        BuildRecursive(elements, dim_cnt, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    Span<TElement> Right(Span<TElement> elements, int median_i) {
        return elements.Slice(median_i + 1, elements.Length - 1 - median_i);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    Span<TElement> Left(Span<TElement> elements, int median_i) {
        return elements.Slice(0, median_i);
    }

    void BuildRecursive(
        Span<TElement> elements,
        int dim_cnt,
        int depth
        ) {
        if (elements.Length <= 1)
            return;
        int axis = depth % dim_cnt;
        int median_i = elements.Length / 2;
        SelectInplace(elements, median_i, axis);
        BuildRecursive(Left(elements, median_i), dim_cnt, depth + 1);
        BuildRecursive(Right(elements, median_i), dim_cnt, depth + 1);
    }

    public double DistanceL2(T[] a, TElement b) {
        double sum = 0;
        for (int i = 0; i < a.Length; i++) {
            double diff = ToDouble(a[i] - b.GetForDimension(i));
            sum += diff * diff;
        }
        return sum;
    }

    static double ToDouble(T val) {
        return double.CreateSaturating(val);
    }

    public class SearchContext {
        public Func<TElement, double, bool>? visit;
        public Func<double>? worstDistL2 = null;
        public T[] point;
        public int restriction_max_cnt = int.MaxValue;
        public double restriction_radiusL2 = double.MaxValue;

    }

    bool SearchRecursive(
        Span<TElement> elements,
        SearchContext ctx,
        int depth
        ) {
        if (elements.IsEmpty) 
            return true; 
        int median_i = elements.Length / 2;
        var node = elements[median_i];
        double distL2 = DistanceL2(ctx.point, node);
        var point = ctx.point;
        if (distL2 <= ctx.restriction_radiusL2) {
            if (!ctx.visit(node, distL2))
                return false; // больше не заинтересованы в поиске!
        }
        if (elements.Length == 1) 
            return true;
        int axis = depth % point.Length;
        bool toLeft = point[axis] < node.GetForDimension(axis); // искомая точка лежит левее
        double diff = ToDouble(point[axis] - node.GetForDimension(axis));
        diff *= diff;
        var best = toLeft ? Left(elements, median_i) : Right(elements, median_i);
        if (!SearchRecursive(best, ctx, depth + 1))
            return false;
        if (diff <= ctx.restriction_radiusL2) { // возможно есть и в другой стороне! Проверим худший случай когда она лежит по оси.
            if(ctx.worstDistL2 == null || diff < ctx.worstDistL2()) { // только если есть пустое место либо если правая сторона заведомо не хуже чем худшая уже известная точка
                var other = !toLeft ? Left(elements, median_i) : Right(elements, median_i);
                if (!SearchRecursive(other, ctx, depth + 1))
                    return false;
            }

        }
        return true;
    }

    public void Search(
        Span<TElement> elements,
        SearchContext ctx) 
    {
        if(ctx.restriction_radiusL2 == double.MaxValue && ctx.restriction_max_cnt == int.MaxValue) {
            throw new Exception("Either restriction_radiusL2 or restriction_max_cnt must be specified");
        }
        if(ctx.visit == null) {
            throw new Exception("Must specify visit");
        }
        if(ctx.restriction_max_cnt != int.MaxValue && ctx.worstDistL2 == null) {
            throw new Exception("Must specify worstDistL2");
        }
        SearchRecursive(elements, ctx, 0);
    }

    // Возвращает индексы ближайших объектов в отсортированном порядке.
    public void SearchSorted(
        Span<TElement> elements,
        T[] point,
        List<(TElement elem, double dist)> result,
        double radiusL2 = double.MaxValue,
        int max_cnt = 10
        ) {
        result.Clear();
        SearchContext ctx = new SearchContext {
            restriction_radiusL2 = radiusL2,
            restriction_max_cnt = max_cnt,
            point = point,
            visit = (x, dist) => {
                int i = 0;
                int _cnt = result.Count;
                if (result.Count < max_cnt) result.Add(default);
                for (i = _cnt; i > 0; --i) {
                    if (result[i - 1].dist > dist) {
                        if (i < max_cnt) {
                            result[i] = result[i - 1];
                        }
                    } else
                        break;
                }
                if (i < max_cnt) {
                    result[i] = (x,dist);
                }
                return true;
            },
            worstDistL2 = () => {
                return (result.Count < max_cnt || result.Count == 0)  
                ? double.MaxValue 
                : result[^1].dist;
            }
        };
        Search(elements, ctx);
    }
}
