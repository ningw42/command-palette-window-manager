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

    internal static WindowRectangle Center(WindowRectangle visibleBounds, WindowRectangle workArea)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(visibleBounds.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(visibleBounds.Height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workArea.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workArea.Height);

        // Even oversized windows retain their dimensions; opposite margins differ by at most one pixel.
        return visibleBounds with
        {
            X = checked((int)(workArea.X + ((long)workArea.Width - visibleBounds.Width) / 2)),
            Y = checked((int)(workArea.Y + ((long)workArea.Height - visibleBounds.Height) / 2)),
        };
    }

    internal static WindowRectangle ResizeByFivePercent(
        WindowRectangle visibleBounds, WindowRectangle workArea, bool increase)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(visibleBounds.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(visibleBounds.Height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workArea.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workArea.Height);

        var width = ResizeDimension(visibleBounds.Width, workArea.Width, increase);
        var height = ResizeDimension(visibleBounds.Height, workArea.Height, increase);
        // Keep the current center, shifting only as much as needed to fit inside the work area.
        var x = Math.Clamp((long)visibleBounds.X + (visibleBounds.Width - (long)width) / 2,
            workArea.X, (long)workArea.X + workArea.Width - width);
        var y = Math.Clamp((long)visibleBounds.Y + (visibleBounds.Height - (long)height) / 2,
            workArea.Y, (long)workArea.Y + workArea.Height - height);
        return new WindowRectangle(checked((int)x), checked((int)y), width, height);
    }

    private static int ResizeDimension(int current, int available, bool increase)
    {
        // Round 5% to the nearest physical pixel, but never to zero. Round the 10% floor up.
        var step = Math.Max(1, ((long)available + 10) / 20);
        var minimum = ((long)available + 9) / 10;
        return (int)Math.Clamp(current + (increase ? step : -step), minimum, available);
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
