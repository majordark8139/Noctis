using System;

namespace Noctis.Helpers;

public class Stopwatch : System.Diagnostics.Stopwatch
{
    public static TimeSpan GetElapsedTime(long startingTimestamp) =>
        TimeSpan.FromSeconds((double)(GetTimestamp() - startingTimestamp) / Frequency);

    public static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
        TimeSpan.FromSeconds((double)(endingTimestamp - startingTimestamp) / Frequency);
}
