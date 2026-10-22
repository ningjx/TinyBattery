namespace TinyBattery.Plugin.Logitech;

internal static class VoltageToPercent
{
    // Solaar's estimate_battery_level_percentage table, highest first.
    private static readonly (int Millivolts, int Percent)[] Table =
    [
        (4186, 100),
        (4067, 90),
        (3989, 80),
        (3922, 70),
        (3859, 60),
        (3811, 50),
        (3778, 40),
        (3751, 30),
        (3717, 20),
        (3671, 10),
        (3646, 5),
        (3579, 2),
        (3500, 0),
    ];

    public static int Estimate(int millivolts)
    {
        if (millivolts >= Table[0].Millivolts)
        {
            return Table[0].Percent;
        }

        if (millivolts <= Table[^1].Millivolts)
        {
            return Table[^1].Percent;
        }

        for (var index = 0; index < Table.Length - 1; index++)
        {
            var (highMillivolts, highPercent) = Table[index];
            var (lowMillivolts, lowPercent) = Table[index + 1];
            if (millivolts < lowMillivolts || millivolts > highMillivolts)
            {
                continue;
            }

            var percent = lowPercent
                + (highPercent - lowPercent)
                * (millivolts - lowMillivolts)
                / (double)(highMillivolts - lowMillivolts);
            return (int)Math.Round(percent, MidpointRounding.ToEven);
        }

        return 0;
    }
}
