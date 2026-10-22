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

## 📸 Screenshots (please do not question my music taste, I know my library is wierd but it's not all my fault, I am trying to play music that most people can listen to if I am with someone)

| Home Screen | Player and Queue |
|-------------|------------------|
| <img width="1266" height="793" alt="obrazek" src="https://github.com/user-attachments/assets/73b55cbe-d0e2-4055-bba9-df7222e5c7ae" /> | <img width="1266" height="793" alt="obrazek" src="https://github.com/user-attachments/assets/1c3bf74e-2cfc-4072-9104-da855399e5eb" /> |

| Player with karaoke lyrics | Discover Page |
|----------------------------|---------------|
| <img width="1266" height="793" alt="obrazek" src="https://github.com/user-attachments/assets/c0b2acaa-21ec-41b6-9955-5e1cb1ff41a8" /> | <img width="1920" height="1152" alt="obrazek" src="https://github.com/user-attachments/assets/67cc6b2c-84e1-4f31-a1a4-6ee7f10bd3ba" /> |

| Search Results | Personal Library |
|----------------|------------------|
| <img width="1920" height="1152" alt="obrazek" src="https://github.com/user-attachments/assets/27c78fed-db23-4fb5-abd1-d9b0d1bfd198" /> | <img width="1920" height="1152" alt="obrazek" src="https://github.com/user-attachments/assets/b2f2b4d3-0de7-4b14-bf78-f4ad8f0caa41" /> |


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
Suggestions for improvements, bug reports, or pull requests are welcome! Check out the [Issues](https://github.com/LolFailKO352/Melodium/issues) tab.

## ⬇️ Requirements
.NET 10 Runtime
https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-10.0.13-windows-x64-installer

MS Edge WebView2 (if you are not a tech-savvy person, you should already have it)
https://developer.microsoft.com/cs-cz/microsoft-edge/webview2?form=MA13LH&cs=3715420047#download

## 📄 License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for more details.

*Disclaimer: This is an unofficial, community-created project. It is not sponsored, endorsed, or otherwise affiliated with Google LLC or YouTube.*
