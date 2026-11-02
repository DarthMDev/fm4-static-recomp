// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
namespace Fm4StaticRecomp.Launcher.Core.Settings;

// The launcher's window sizes, in real pixels. Another size goes through COMMAND-LINE OPTIONS.
public static class WindowSizes
{
    // The shape the game keeps its window in (src\built_in_defaults.cpp window_aspect_ratio).
    private const int AspectWidth = 16, AspectHeight = 9;

    public static readonly IReadOnlyList<(int Width, int Height)> All =
        new[] { 720, 900, 1080, 1440, 2160 }.Select(height => (height * AspectWidth / AspectHeight, height)).ToList();

    public static readonly (int Width, int Height) Default = All[0];

    public static string Label((int Width, int Height) size) => $"{size.Width}x{size.Height}";

    public static (int Width, int Height) Parse(string? label) =>
        All.Where(s => Label(s) == label).DefaultIfEmpty(Default).First();

    // Smaller than the display, whose whole height would push the title bar off it; the smallest
    // and the chosen one always stay.
    public static IReadOnlyList<(int Width, int Height)> Fitting((int Width, int Height)? display, (int Width, int Height) chosen) =>
        All.Where(s => s == Default || s == chosen || display is not { } d || (s.Width < d.Width && s.Height < d.Height)).ToList();
}
