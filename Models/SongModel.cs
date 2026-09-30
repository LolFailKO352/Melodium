using System;
using System.Collections.Generic;
using System.Text;

namespace Melodium.Models
{
    public class SongModel
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string? ArtistId { get; set; }
        public string? ThumbnailUrl { get; set; }
    }
}
