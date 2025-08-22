namespace AllureReportProcessor.Utils;

public static class TimeUtils
{
    public static string ConvertMillisecondsToReadable(long milliseconds)
    {
        if (milliseconds < 1000)
            return $"{milliseconds} ms";

        var seconds = Math.Round(milliseconds / 1000.0, 1);

        if (seconds < 60)
            return $"{seconds} s";

        var minutes = (int)Math.Floor(seconds / 60);
        var remainingSeconds = (int)Math.Round(seconds % 60);

        if (remainingSeconds == 0)
            return $"{minutes} m";

        return $"{minutes} m {remainingSeconds} s";
    }
}