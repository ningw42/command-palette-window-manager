using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using WindowManager.Windowing;

namespace WindowManager;

internal sealed partial class SetWindowSizePage : DynamicListPage
{
    private readonly WindowController _windows;

    internal SetWindowSizePage(WindowController windows)
    {
        _windows = windows;
        Id = "ningw.WindowManager.SetWindowSize";
        Title = "Set Window Size";
        Name = "Open";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        PlaceholderText = "Enter 0–100";
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Title = WindowSizePercentage.InvalidInputMessage,
            Subtitle = "0: minimize · 1–99: width and height percentage · 100: maximize",
            Icon = Icon,
        };
    }

    // DynamicListPage sets SearchText before calling this method. Typing never mutates a window.
    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        if (!WindowSizePercentage.TryParse(SearchText, out var percentage)) return [];

        return [new ListItem(new SetWindowSizeCommand(_windows, percentage, () => SearchText))
        {
            Title = percentage switch
            {
                0 => "Minimize (0%)",
                100 => "Maximize (100%)",
                _ => $"Set width and height to {percentage}%",
            },
            Subtitle = percentage switch
            {
                0 => "Minimize the last-active application",
                100 => "Maximize the last-active application; may activate the window",
                _ => "Percentage of the monitor's work area; application size limits may apply",
            },
        }];
    }
}
