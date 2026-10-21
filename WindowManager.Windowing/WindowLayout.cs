namespace WindowManager.Windowing;

internal static class WindowLayout
{
    internal static WindowRectangle AlmostMaximized(WindowRectangle workArea)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workArea.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workArea.Height);

        // Integer arithmetic keeps odd-sized work areas deterministic (opposite margins differ by at most one pixel).
        var width = Math.Max(1, (int)((long)workArea.Width * 4 / 5));
        var height = Math.Max(1, (int)((long)workArea.Height * 4 / 5));
        return new WindowRectangle(
            checked(workArea.X + (workArea.Width - width) / 2),
            checked(workArea.Y + (workArea.Height - height) / 2),
            width,
            height);
    }

    internal static WindowRectangle IncludeInvisibleFrame(
        WindowRectangle desiredVisibleBounds, WindowRectangle outerBounds, WindowRectangle visibleBounds)
    {
        var left = Math.Max(0, visibleBounds.X - outerBounds.X);
        var top = Math.Max(0, visibleBounds.Y - outerBounds.Y);
        var right = Math.Max(0, outerBounds.X + outerBounds.Width - visibleBounds.X - visibleBounds.Width);
        var bottom = Math.Max(0, outerBounds.Y + outerBounds.Height - visibleBounds.Y - visibleBounds.Height);
        return new WindowRectangle(
            checked(desiredVisibleBounds.X - left),
            checked(desiredVisibleBounds.Y - top),
            checked(desiredVisibleBounds.Width + left + right),
            checked(desiredVisibleBounds.Height + top + bottom));
    }
}
