# Melodium (Unofficial MAUI Client)


A modern, fast, and unofficial YouTube Music client built on the [.NET MAUI](https://dotnet.microsoft.com/en-us/apps/maui) platform. This application brings the best of YouTube Music straight to your desktop using a native interface, improved music recommendation algorithms, and deep system integration.

## ✨ Features

- **🎵 Native Playback**: Smooth and high-quality background music playback using the libVLCsharp audio API.
- **🧠 Smart Tailored Recommendations**: The app includes a unique algorithm that analyzes your personal library (saved tracks, artists, albums, and playlists) and automatically generates personalized radio and top picks tailored just for you on the home screen.
- **🎨 Modern UI/UX**: Native design inspired by Windows 11 / WinUI 3. Fully responsive layout that adapts to the window size, with support for automatic switching between Light and Dark mode.
- **🌍 Full Localization**: Built-in translation engine that translates the entire application on the fly for non-native users.
- **⚙️ System Integration**: The app runs in the system tray menu with quick playback controls (Play/Pause), allowing you to control music without having to open the app window.
- **🔍 Search and Explore**: Search for any tracks, artists, albums, or community playlists directly from Melodium.
- **📚 Library Management**: Easily browse your saved songs, favorite albums, and artists.

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

- **Framework**: [.NET MAUI](https://dotnet.microsoft.com/apps/maui)
- **Architecture**: MVVM (Model-View-ViewModel) using [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- **Media**: [libVLCsharp](https://github.com/videolan/libvlcsharp)
- **Data and API**: 
  - [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode)
  - Custom Melodium API integration

## 🤝 Contributing
Suggestions for improvements, bug reports, or pull requests are welcome! Check out the [Issues](https://github.com/your_name/Melodium/issues) tab.

## 📄 License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for more details.

*Disclaimer: This is an unofficial, community-created project. It is not sponsored, endorsed, or otherwise affiliated with Google LLC or YouTube.*
