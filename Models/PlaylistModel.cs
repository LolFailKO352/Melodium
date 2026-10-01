using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Melodium.Models
{
    public partial class PlaylistModel : ObservableObject
    {
        public string Id { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Title { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string? ThumbnailUrl { get; set; }

        [ObservableProperty]
        public partial int SongCount { get; set; }

        [ObservableProperty]
        public partial string Creator { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string? Description { get; set; }

        [ObservableProperty]
        public partial bool CanEdit { get; set; }

        [ObservableProperty]
        public partial bool IsCollaborative { get; set; }
    }
}
