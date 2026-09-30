using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Melodium.Models
{
    public partial class LyricLineModel : ObservableObject
    {
        public TimeSpan Timestamp { get; set; }
        public string Text { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsActive { get; set; }
    }
}
