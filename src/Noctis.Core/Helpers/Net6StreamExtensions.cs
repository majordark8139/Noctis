using System;
using System.IO;

namespace System.IO;

public static class Net6StreamExtensions
{
    public static void ReadExactly(this Stream stream, Span<byte> buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer.Slice(totalRead));
            if (read == 0)
                throw new EndOfStreamException();
            totalRead += read;
        }
    }

    public static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0)
                throw new EndOfStreamException();
            totalRead += read;
        }
    }

    public static int ReadAtLeast(this Stream stream, Span<byte> buffer, int minimumBytes, bool throwOnEndOfStream = true)
    {
        var totalRead = 0;
        while (totalRead < minimumBytes)
        {
            var read = stream.Read(buffer.Slice(totalRead));
            if (read == 0)
            {
                if (throwOnEndOfStream)
                    throw new EndOfStreamException();
                return totalRead;
            }
            totalRead += read;
        }
        return totalRead;
    }
}
