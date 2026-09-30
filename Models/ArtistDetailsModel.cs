using System.Collections.ObjectModel;

namespace Melodium.Models;

public class ArtistDetailsModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Subscribers { get; set; }
    public string? ThumbnailUrl { get; set; }
    public ObservableCollection<SongModel> TopSongs { get; } = new();
    public ObservableCollection<AlbumModel> Albums { get; } = new();
    public ObservableCollection<AlbumModel> Singles { get; } = new();
}
