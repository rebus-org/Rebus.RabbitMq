using System;

namespace Rebus.Internals;

static class ArrayExtensions
{
    public static T[] Truncate<T>(this T[] array, int size)
    {
        // no need to do anything
        if (array.Length < size) return array;

        // return truncated array
        var truncatedCopy = new T[size];
        Array.Copy(array, truncatedCopy, size);
        return truncatedCopy;
    }
}