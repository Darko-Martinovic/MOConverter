using System;
using System.Collections.Generic;

namespace Converter.Extension
{
    public static class ExceptionExtension
    {
        /// <summary>
        /// When working with SMO, when an exception arises, 
        /// usually the most inner one contains information that you need. 
        /// In order to collect all exception text, you have to travel through exception hierarchy.
        /// </summary>
        public static IEnumerable<TSource> CollectThemAll<TSource>(
        this TSource source,
        Func<TSource, TSource> nextItem,
        Func<TSource, bool> canContinue)
        {
            for (var current = source; canContinue(current); current = nextItem(current))
            {
                yield return current;
            }
        }

        public static IEnumerable<TSource> CollectThemAll<TSource>(
            this TSource source,
            Func<TSource, TSource> nextItem)
            where TSource : class
        {
            return CollectThemAll(source, nextItem, s => s != null);
        }
    }

}
