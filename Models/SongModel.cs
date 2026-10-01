using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Melodium.Models
{
    public partial class SongModel : ObservableObject
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string? ArtistId { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? Duration { get; set; }
        public string? SetVideoId { get; set; }

        [ObservableProperty]
        public partial bool CanEdit { get; set; }
    }
}

