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
| <img width="1906" height="1018" alt="image" src="https://github.com/user-attachments/assets/ac71d8da-a9b9-4dc8-ba5b-c0c16cf42107" /> | <img width="1906" height="1018" alt="image" src="https://github.com/user-attachments/assets/6b908b17-4bc8-4736-a5e5-2fb54f0d2ba3" /> |

| Search Results | Personal Library |
|----------------|--------------|
| <img width="1906" height="1018" alt="image" src="https://github.com/user-attachments/assets/74c4fc6c-f2cb-498b-aa5f-8542d662e948" /> | <img width="1906" height="1018" alt="image" src="https://github.com/user-attachments/assets/8639cb35-da04-49de-8e50-09847734c549" /> |

## 🎚️ How to install

To install application:
1. Download .NET runtime from here https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-10.0.9-windows-x64-installer?cid=getdotnetcore
2. Download .zip file from Releases tab unzip it and run Melodium.msi.

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
