using System;
using System.Collections.Generic;

namespace Melodium.Models;

public class PlaybackSessionModel
{
    public SongModel? CurrentSong { get; set; }
    public double PositionSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public List<SongModel> Queue { get; set; } = new();
    public int QueueIndex { get; set; } = -1;
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}
