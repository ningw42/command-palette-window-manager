// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace WindowManager;

internal sealed partial class WindowManagerPage : ListPage
{
    private readonly IListItem[] _items;

    internal WindowManagerPage(AlmostMaximizedCommand almostMaximized)
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Window Manager";
        Name = "Open";
        _items = [
            new ListItem(almostMaximized)
            {
                Title = "Almost Maximized",
                Subtitle = "Center the last-active application at 80% of the monitor's usable width and height",
            },
        ];
    }

    public override IListItem[] GetItems() => _items;
}
