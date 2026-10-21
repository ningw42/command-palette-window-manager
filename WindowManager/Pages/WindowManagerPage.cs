// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace WindowManager;

internal sealed partial class WindowManagerPage : ListPage
{
    private readonly IListItem[] _items;

    internal WindowManagerPage(
        AlmostMaximizedCommand almostMaximized, MaximizeCommand maximize, MinimizeCommand minimize,
        IncreaseSizeCommand increaseSize, DecreaseSizeCommand decreaseSize, CenterCommand center,
        SetWindowSizePage setWindowSize)
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Window Manager";
        Name = "Open";
        _items = [
            new ListItem(maximize)
            {
                Title = "Maximize",
                Subtitle = "Maximize and activate the last-active application on its monitor",
            },
            new ListItem(minimize)
            {
                Title = "Minimize",
                Subtitle = "Minimize the last-active application",
            },
            new ListItem(center)
            {
                Title = "Center",
                Subtitle = "Center without resizing; leave maximized windows unchanged",
            },
            new ListItem(almostMaximized)
            {
                Title = "Almost Maximized",
                Subtitle = "Center the last-active application at 80% of the monitor's usable width and height",
            },
            new ListItem(setWindowSize)
            {
                Title = "Set Window Size",
                Subtitle = "Enter 0–100: minimize, resize both dimensions, or maximize",
            },
            new ListItem(increaseSize)
            {
                Title = "Increase by 5%",
                Subtitle = "Grow both dimensions by 5% of the work area; maximize and activate at 100%",
            },
            new ListItem(decreaseSize)
            {
                Title = "Decrease by 5%",
                Subtitle = "Shrink both dimensions by 5% of the work area, down to a minimum of 10%",
            },
        ];
    }

    public override IListItem[] GetItems() => _items;
}
