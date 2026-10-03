using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Melodium.Models;

public enum HomeItemType
{
    Song,
    Playlist,
    Album,
    Artist
}

public partial class HomeItemModel : ObservableObject
{
    public HomeItemType Type { get; set; } = HomeItemType.Song;
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }

    public bool IsSong => Type == HomeItemType.Song;
    public bool IsPlaylist => Type == HomeItemType.Playlist;
    public bool IsAlbum => Type == HomeItemType.Album;
    public bool IsArtist => Type == HomeItemType.Artist;

    public string TypeName => Type switch
    {
        HomeItemType.Playlist => "Playlist",
        HomeItemType.Album => "Album",
        HomeItemType.Artist => "Interpret",
        _ => "Skladba"
    };

    public string TypeGlyph => Type switch
    {
        HomeItemType.Playlist => "\uE8B7",
        HomeItemType.Album => "\uE93C",
        HomeItemType.Artist => "\uE77B",
        _ => "\uE8D6"
    };

    public SongModel? Song { get; set; }
    public PlaylistModel? Playlist { get; set; }
    public AlbumModel? Album { get; set; }
    public ArtistModel? Artist { get; set; }
}

public partial class HomeSectionModel : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Strapline { get; set; }

    public ObservableCollection<HomeItemModel> Items { get; set; } = new();
}
