using System;

namespace Melodium.Models
{
    public class AlbumModel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string ArtistName { get; set; } = string.Empty;
        public string Artist { get => ArtistName; set => ArtistName = value; }
        public int ReleaseYear { get; set; }
        public string Year { get => ReleaseYear > 0 ? ReleaseYear.ToString() : string.Empty; set { if (int.TryParse(value, out int y)) ReleaseYear = y; } }
    }
}
