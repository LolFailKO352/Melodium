using System;

namespace Melodium.Models
{
    public class ArtistModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int SongCount { get; set; }
        public string Subscribers { get; set; } = string.Empty;
    }
}
