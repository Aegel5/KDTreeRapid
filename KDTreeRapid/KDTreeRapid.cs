using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace KDTreeRapid;

public interface IKDTreeElement<T> where T: INumber<T> {
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
    static bool More(TElement a, TElement b, int dim) {
        return a.GetForDimension(dim) > b.GetForDimension(dim);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Sort(Span<TElement> a, int i, int j, int dim) {
        if (a[i].GetForDimension(dim) > a[j].GetForDimension(dim)) {
            (a[i], a[j]) = (a[j], a[i]);
        }
    }
    static Random rnd = new();
    static void SelectInplace(Span<TElement> a, int rank, int dim) {

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
    public void BuildInPlace(Span<TElement> elements, int dimensions) {
        BuildRecursive(elements, dimensions, 0);
    }

    public void BuildInPlace(List<TElement> elements, int dimensions) {
        BuildInPlace(CollectionsMarshal.AsSpan(elements), dimensions);
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
        public required Func<TElement, double, bool> Visit;
        public Func<double>? WorstDistL2 = null;
        public required T[] point;
        public int restriction_max_cnt = int.MaxValue;
        public double restriction_radiusL2 = double.MaxValue;

    }

    bool SearchRecursive(
        Span<TElement> elements,
        SearchContext ctx,
        int depth,
        T parent_value,
        bool fromLeft
        ) {
        if (elements.IsEmpty) 
            return true;
        int median_i = elements.Length / 2;
        var node = elements[median_i];
        var point = ctx.point;
        double distL2 = DistanceL2(point, node);

        // check sorting. just for convenience. can be deleted for optimize
        {
            if (depth > 0) {
                var value_as_parent = node.GetForDimension((depth - 1) % point.Length);
                if (fromLeft
                ? value_as_parent > parent_value
                : value_as_parent < parent_value) {
                    throw new Exception("Bad sorting");
                }
            }
        }

        if (distL2 <= ctx.restriction_radiusL2) {
            if (!ctx.Visit(node, distL2))
                return false; // больше не заинтересованы в поиске!
        }
        if (elements.Length == 1) 
            return true;
        int axis = depth % point.Length;
        var value = node.GetForDimension(axis);
        bool toLeft = point[axis] < value; // искомая точка лежит левее
        double diff = ToDouble(point[axis] - value);
        diff *= diff;
        var best = toLeft ? Left(elements, median_i) : Right(elements, median_i);
        if (!SearchRecursive(best, ctx, depth + 1, value, toLeft))
            return false;
        if (diff <= ctx.restriction_radiusL2) { // возможно есть и в другой стороне! Проверим худший случай когда она лежит по оси.
            if(ctx.WorstDistL2 == null || diff < ctx.WorstDistL2()) { // только если есть пустое место либо если правая сторона заведомо не хуже чем худшая уже известная точка
                var other = !toLeft ? Left(elements, median_i) : Right(elements, median_i);
                if (!SearchRecursive(other, ctx, depth + 1, value, !toLeft))
                    return false;
            }

        }
        return true;
    }

    public void Search(
    List<TElement> elements,
    SearchContext ctx) {
        Search(CollectionsMarshal.AsSpan(elements), ctx);
    }

    public void Search(
        Span<TElement> elements,
        SearchContext ctx) 
    {
        if(ctx.restriction_radiusL2 == double.MaxValue && ctx.restriction_max_cnt == int.MaxValue) {
            throw new Exception("Either restriction_radiusL2 or restriction_max_cnt or both must be specified");
        }
        if(ctx.restriction_max_cnt != int.MaxValue && ctx.WorstDistL2 == null) {
            throw new Exception("Must specify WorstDistL2 when have restriction max_cnt");
        }
        SearchRecursive(elements, ctx, 0, default, default);
    }

    public List<(TElement element, double distL2)> SearchSorted(
        List<TElement> elements,
        T[] point,
        double radius = double.MaxValue,
        int max_cnt = int.MaxValue,
        List<(TElement, double)>? result = null) {

        return SearchSorted(CollectionsMarshal.AsSpan(elements), point, radius, max_cnt, result);
    }

    // Возвращает индексы ближайших объектов в отсортированном порядке.
    public List<(TElement element, double distL2)> SearchSorted(
        Span<TElement> elements,
        T[] point,
        double radius = double.MaxValue,
        int max_cnt = int.MaxValue,
        List<(TElement element, double distL2)>? result = null
        ) {

        if (result == null) {
            result = new();
        } else {
            result.Clear();
        }
        SearchContext ctx = new SearchContext {
            restriction_radiusL2 = radius*radius,
            restriction_max_cnt = max_cnt,
            point = point,
            Visit = (x, dist) => {
                int i = 0;
                int _cnt = result.Count;
                if (result.Count < max_cnt) result.Add(default);
                for (i = _cnt; i > 0; --i) {
                    if (result[i - 1].distL2 > dist) {
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
            WorstDistL2 = () => {
                return (result.Count < max_cnt || result.Count == 0)  
                ? double.MaxValue 
                : result[^1].distL2;
            }
        };
        Search(elements, ctx);
        return result;
    }
}
