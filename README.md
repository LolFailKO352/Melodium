# Melodium (Unofficial YouTube Music Client)

A modern, fast, and unofficial YouTube Music desktop client built on **WinUI 3** and the **Windows App SDK** (.NET 10). This application brings the best of YouTube Music straight to your Windows desktop with native performance, fluent UI design, personalized recommendation algorithms, and deep system integration.

## ✨ Features

- **🎵 Native Playback**: Smooth, high-performance background music playback using native Windows Media Foundation audio APIs.
- **🧠 Smart Tailored Recommendations**: Unique algorithm analyzing your personal library (saved tracks, artists, albums, and playlists) to automatically generate personalized radio and top picks.
- **🎨 Modern WinUI 3 Design**: Native Windows 11 Fluent design with mica/acrylic materials, dark/light theme support, and responsive layouts.
- **🌍 Full Localization**: Built-in translation engine supporting multiple languages.
- **⚙️ System Tray Integration**: Minimizes to the system tray with quick playback controls and notification toasts.
- **🔍 Search and Explore**: Instant search for tracks, artists, albums, or community playlists.
- **📚 Personal Library**: Full access to your liked songs, playlists, and artists.

## 📸 Screenshots

| Home Screen | Player and Queue |
|-------------|----------------|
| <img width="2560" height="1392" alt="obrazek" src="https://github.com/user-attachments/assets/ab3c1e1e-8754-497c-b91d-ca02c484dcc9" /> | <img width="2560" height="1392" alt="obrazek" src="https://github.com/user-attachments/assets/d9558d9d-d79d-4e9f-ad79-c343392e1e21" /> |

| Player with karaoke lyrics | Discover Page |
|----------------------------|---------------|
| <img width="2560" height="1392" alt="obrazek" src="https://github.com/user-attachments/assets/e64875d5-f143-4dbe-b80d-50302b9922a9" /> | <img width="2560" height="1392" alt="obrazek" src="https://github.com/user-attachments/assets/3a0e50f3-8105-4fc1-a7cc-52788fb6c3f2" /> |

| Search Results | Personal Library |
|----------------|--------------|
| <img width="2560" height="1392" alt="obrazek" src="https://github.com/user-attachments/assets/42083021-bd90-4f05-aedb-1b2a1db11f52" /> | <img width="2560" height="1392" alt="obrazek" src="https://github.com/user-attachments/assets/5295ebb1-20d5-4e3c-9e3f-65eefa50729f" /> |


## 🔐 How Login Works

The application uses a secure built-in browser window (WebView) to allow you to log in directly via Google/YouTube. After a successful login, the app securely retrieves "session cookies" in the background. Thanks to them, it gains access to your personal library and can generate personalized recommendations. This approach fully bypasses the need for an official (and often paid) API key.

## 🛠️ Technologies
 
- **Framework**: [WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/) & [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/) (.NET 10)
- **Architecture**: MVVM (Model-View-ViewModel) using [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- **Media**: Windows Media Foundation (`Windows.Media.Playback.MediaPlayer`)
- **Installer**: WiX Toolset v4 MSI package
- **Data and API**: 
  - [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode)
  - Custom Melodium YouTube Music API integration

## 🤝 Contributing
Suggestions for improvements, bug reports, or pull requests are welcome! Check out the [Issues](https://github.com/your_name/Melodium/issues) tab.

## 📄 License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for more details.

*Disclaimer: This is an unofficial, community-created project. It is not sponsored, endorsed, or otherwise affiliated with Google LLC or YouTube.*
