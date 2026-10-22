using System.Drawing.Imaging;
using System.IO;
using TinyBattery.Abstractions.Themes;
using TinyBattery.App.Rendering;

namespace TinyBattery.App;

public static class IconExport
{
    public static bool TryRun(string[] args)
    {
        var index = Array.IndexOf(args, "--render-icons");
        if (index < 0)
        {
            return false;
        }

        if (index + 1 >= args.Length)
        {
            throw new ArgumentException("--render-icons requires an output directory.");
        }

        var directory = Path.GetFullPath(args[index + 1]);
        Directory.CreateDirectory(directory);

        var renderer = new ClassicBatteryIconRenderer();
        var states = new (string Name, BatteryIconState State)[]
        {
            ("unknown", new BatteryIconState(null)),
            ("0", new BatteryIconState(0)),
            ("10", new BatteryIconState(10)),
            ("19", new BatteryIconState(19)),
            ("20", new BatteryIconState(20)),
            ("50", new BatteryIconState(50)),
            ("59", new BatteryIconState(59)),
            ("60", new BatteryIconState(60)),
            ("95", new BatteryIconState(95)),
            ("100", new BatteryIconState(100)),
        };

        foreach (var (name, state) in states)
        {
            foreach (var size in new[] { 16, 20, 24, 32 })
            {
                using var bitmap = renderer.RenderBitmap(state, size);
                bitmap.Save(
                    Path.Combine(directory, $"battery-{name}-{size}.png"),
                    ImageFormat.Png);
            }

            using var fullSize = renderer.RenderBitmap(state, 32);
            fullSize.Save(
                Path.Combine(directory, $"battery-{name}.png"),
                ImageFormat.Png);
        }

        using (var provider = new DefaultTrayIconProvider())
        {
            foreach (var size in new[] { 16, 32 })
            {
                var defaultIcon = provider.GetDefaultIcon(size);
                using var defaultBitmap = defaultIcon.ToBitmap();
                defaultBitmap.Save(
                    Path.Combine(directory, $"battery-default-{size}.png"),
                    ImageFormat.Png);
                if (size == 32)
                {
                    defaultBitmap.Save(
                        Path.Combine(directory, "battery-default.png"),
                        ImageFormat.Png);
                }
            }
        }

        return true;
    }
}
