using System;
using System.Linq;

namespace Rebus.Internals;

static class ByteArrayExtensions
{
    public static int GetInt32(this byte[] bytes) => bytes.Length switch
    {
        0 => 0,
        1 => bytes[0],
        2 => 256 * bytes[0] + bytes[1],
        3 => 256 * 256 * bytes[0] + 256 * bytes[1] + bytes[2],
        4 => BitConverter.ToInt32(bytes, startIndex: 0),
        _ => throw new FormatException(
            $"Could not turn the byte array with {string.Join(" ", bytes.Select(b => b.ToString("X")))} into an int32")
    };
}