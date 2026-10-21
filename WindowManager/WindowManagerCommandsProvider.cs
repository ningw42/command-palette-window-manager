// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using WindowManager.Windowing;

namespace WindowManager;

public partial class WindowManagerCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly SettingsManager _settingsManager = new();

    internal WindowManagerCommandsProvider(WindowController windows)
    {
        Id = "ningw.WindowManager";
        DisplayName = "Window Manager";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        var almostMaximized = new AlmostMaximizedCommand(windows);
        var maximize = new MaximizeCommand(windows);
        var minimize = new MinimizeCommand(windows);
        _commands = [
            new CommandItem(maximize)
            {
                Title = "Maximize",
                Subtitle = "Window Manager · Maximize and activate the last-active application on its monitor",
            },
            new CommandItem(minimize)
            {
                Title = "Minimize",
                Subtitle = "Window Manager · Minimize the last-active application",
            },
            new CommandItem(almostMaximized)
            {
                Title = "Almost Maximized",
                Subtitle = "Window Manager · Center at 80% of the monitor's usable width and height",
            },
            new CommandItem(new WindowManagerPage(almostMaximized, maximize, minimize))
            {
                Title = DisplayName,
                MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
            },
        ];

        Settings = _settingsManager.Settings;
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
