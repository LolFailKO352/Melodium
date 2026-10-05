using CommunityToolkit.Mvvm.ComponentModel;

namespace Melodium.Models;

public partial class MoodFilterModel : ObservableObject
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? Params { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
