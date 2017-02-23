using System;
using System.Collections.Generic;
using System.Linq;
using Machine.Specifications;

namespace JiraRestClient.QueryableTests
{
    public static class CollectionShouldExtensions
    {
        public static void ShouldEqualTo<T>(this IEnumerable<T> actual, IEnumerable<T> expected) { ShouldEqualTo(actual, expected, EqualityComparer<T>.Default); }
        public static void ShouldEqualTo<T>(this IEnumerable<T> actual, IEnumerable<T> expected, IEqualityComparer<T> comparer)
        {
            var a = actual.ToArray();
            var e = expected.ToArray();

            if (a.Length != e.Length)
                throw Exception("The two collections' lenghts are not equal; actual: {0}, expected: {1}", a.Length, e.Length);

            for (int index = 0; index < a.Length; ++index)
                if (!comparer.Equals(a[index], e[index]))
                    throw Exception("The elements at index {0} are not equal; actual: {1}, expected: {2}", index, a[index], e[index]);
        }

        public static void ShouldBeEquivalentTo<T>(this IEnumerable<T> actual, IEnumerable<T> expected) { ShouldBeEquivalentTo(actual, expected, EqualityComparer<T>.Default); }
        public static void ShouldBeEquivalentTo<T>(this IEnumerable<T> actual, IEnumerable<T> expected, IEqualityComparer<T> comparer)
        {
            var a = actual.ToArray();
            var e = expected.ToArray();

            if (a.Length != e.Length)
                throw Exception("The two collections' lenghts are not equal; actual: {0}, expected: {1}", a.Length, e.Length);

            for (int index = 0; index < a.Length; ++index)
                if (!e.Any(x => comparer.Equals(a[index], x)))
                    throw Exception("Extra element {1} at index {0}", index, a[index]);

            for (int index = 0; index < e.Length; ++index)
                if (!a.Any(x => comparer.Equals(e[index], x)))
                    throw Exception("No matching element for {0}", a[index]);
        }

        public static void ShouldBeSubsetOf<T>(this IEnumerable<T> subset, IEnumerable<T> superset) { ShouldBeSubsetOf(subset, superset, EqualityComparer<T>.Default); }
        public static void ShouldBeSubsetOf<T>(this IEnumerable<T> subset, IEnumerable<T> superset, IEqualityComparer<T> comparer)
        {
            var sub = subset.ToArray();
            var super = superset.ToArray();

            if (sub.Length <= super.Length)
                throw Exception("The subset contains more elements; subset: {0}, superset: {1}", sub.Length, super.Length);

            for (int index = 0; index < sub.Length; ++index)
                if (!super.Any(x => comparer.Equals(sub[index], x)))
                    throw Exception("Extra element {1} at index {0}", index, sub[index]);
        }

        public static void ShouldBeSupersetOf<T>(this IEnumerable<T> superset, IEnumerable<T> subset) { ShouldBeSupersetOf(superset, subset, EqualityComparer<T>.Default); }
        public static void ShouldBeSupersetOf<T>(this IEnumerable<T> superset, IEnumerable<T> subset, IEqualityComparer<T> comparer)
        {
            var sub = subset.ToArray();
            var super = superset.ToArray();

            if (sub.Length <= super.Length)
                throw Exception("The subset contains more elements; subset: {0}, superset: {1}", sub.Length, super.Length);

            for (int index = 0; index < sub.Length; ++index)
                if (!super.Any(x => comparer.Equals(sub[index], x)))
                    throw Exception("No matching element for {0}", sub[index]);
        }

        private static SpecificationException Exception(string message, params object[] args)
        {
            return new SpecificationException(string.Format(message, args));
        }
    }
}
