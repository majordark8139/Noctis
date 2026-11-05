using System;

namespace Noctis.Helpers;

public class Stopwatch : System.Diagnostics.Stopwatch
{
    public static new Stopwatch StartNew()
    {
        var sw = new Stopwatch();
        sw.Start();
        return sw;
    }

    public static TimeSpan GetElapsedTime(long startingTimestamp) =>
        TimeSpan.FromSeconds((double)(GetTimestamp() - startingTimestamp) / Frequency);

    public static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
        TimeSpan.FromSeconds((double)(endingTimestamp - startingTimestamp) / Frequency);
}
