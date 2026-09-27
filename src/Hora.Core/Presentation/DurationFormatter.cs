namespace Hora.Core.Presentation;

public static class DurationFormatter
{
    public static string Format(long totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        var h = totalSeconds / 3600;
        var m = (totalSeconds % 3600) / 60;
        if (h > 0)
        {
            return $"{h} sa {m} dk";
        }

        if (m > 0)
        {
            return $"{m} dk";
        }

        return $"{totalSeconds} sn";
    }
}
