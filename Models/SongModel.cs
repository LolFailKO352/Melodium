using System;

namespace Melodium.Models
{
    public class SongModel
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string? ArtistId { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? Duration { get; set; }
        public string? SetVideoId { get; set; }
        public bool CanEdit { get; set; }
    }
}
