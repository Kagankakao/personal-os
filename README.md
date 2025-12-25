# 🖥️ KeganOS - Personal Productivity Dashboard

<p align="center">
  <img width="636" height="795" alt="image" src="https://github.com/user-attachments/assets/480a9f67-2afe-4bc7-bd6d-54d7a3d0e623" />
</p>

> **A dark-themed productivity command center** that integrates with KEGOMODORO Pomodoro timer and Pixe.la habit tracking to help you stay focused and visualize your progress.

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Windows-0078D6?style=flat-square&logo=windows)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)

---

## ✨ Features

### 🎯 Focus Management
- **KEGOMODORO Integration** - Launch your Pomodoro timer directly from the dashboard
- **25/5 Work Sessions** - Proven time-boxing technique for deep work
- **Journal Tracking** - Log your thoughts and accomplishments

### 📊 Visual Progress
- **Pixe.la Heatmap** - GitHub-style contribution graph for your focus hours
- **Real-time Stats** - Today, Week, and Total hours at a glance
- **Click-to-Navigate** - Click the heatmap to view detailed stats on Pixe.la

### 👤 Multi-Profile Support
- **Profile Selection** - Switch between different user profiles
- **Per-user Settings** - Each profile has its own journal and Pixe.la graph
- **Auto Registration** - New users are automatically registered on Pixe.la

### 🎨 Beautiful Dark UI
- **Terminal Aesthetic** - Hacker-style interface with ASCII-inspired design
- **Custom Title Bar** - Seamless integration with Windows
- **WebView2 Rendering** - Modern Edge-based heatmap display

---

## 🏗️ Architecture

```
KeganOS/
├── Core/                    # Domain layer (interfaces & models)
│   ├── Interfaces/          # Service contracts
│   └── Models/              # Domain entities
├── Infrastructure/          # Implementation layer
│   ├── AI/                  # Gemini AI integration
│   ├── Data/                # SQLite database
│   └── Services/            # Service implementations
└── Views/                   # WPF UI layer
```

### Key Principles
- ✅ **SOLID Architecture** - Interface-based dependency injection
- ✅ **Clean Separation** - Core has no dependencies on Infrastructure
- ✅ **Serilog Logging** - Comprehensive logging in all components
- ✅ **Error Handling** - Try-catch patterns with user feedback

---

## 🚀 Getting Started

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Python 3.10+](https://www.python.org/downloads/) (for KEGOMODORO)
- Windows 10/11 (WPF requirement)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/personal-os.git
   cd personal-os
   ```

2. **Install Python dependencies** (for KEGOMODORO)
   ```bash
   cd personal-os/kegomodoro
   pip install -r requirements.txt
   ```

3. **Build and run KeganOS**
   ```bash
   cd personal-os/KeganOS
   dotnet run
   ```

4. **Create your profile**
   - Click "Create Profile"
   - Enter your display name and symbol
   - Select or create a journal file
   - Your Pixe.la account is auto-created!

---

## 📁 Project Structure

| Component | Description |
|-----------|-------------|
| `KeganOS/` | WPF dashboard application (.NET 9) |
| `kegomodoro/` | Python Pomodoro timer with Pixe.la integration |

---

## 🔧 Configuration

### Pixe.la Integration
KeganOS automatically:
- Registers new users on [Pixe.la](https://pixe.la)
- Creates a "focus" graph for tracking hours
- Displays your heatmap with dark theme

### Journal Files
- Located in `kegomodoro/dependencies/texts/`
- Plain text format with date + time entries
- Auto-parsed for stats display

---

## 📸 Screenshots

<details>
<summary>Click to expand</summary>

### Profile Selection
Select or create user profiles with custom symbols.

### Main Dashboard
Dark-themed command center with timer and stats.

### Pixe.la Heatmap
Interactive contribution graph showing your focus history.

</details>

---

## 🛣️ Roadmap

- [x] Profile management with SQLite
- [x] Pixe.la heatmap integration
- [x] KEGOMODORO launcher
- [x] WebView2 for modern rendering
- [ ] Gemini AI motivational messages
- [ ] KEGOMODORO settings from dashboard
- [ ] Unit test coverage
- [ ] PyInstaller bundle for faster startup

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- [Pixe.la](https://pixe.la) - Habit tracking API
- [Serilog](https://serilog.net) - Structured logging
- [WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) - Modern web rendering

---

<p align="center">
  Made with ❤️ for productivity enthusiasts
</p>
