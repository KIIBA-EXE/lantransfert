# LAN Transfer

Application de transfert de fichiers en réseau local (LAN) similaire à AirDrop, compatible Windows/Linux/macOS.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey.svg)

## ✨ Fonctionnalités

- 🔍 **Découverte automatique** - Détecte automatiquement les appareils sur le réseau local via UDP Broadcast
- 📁 **Transfert de fichiers** - Envoi de fichiers de toute taille via connexion TCP fiable
- 🚀 **Gestion des gros fichiers** - Utilise des buffers de 8KB pour transférer des fichiers de plusieurs Go sans surcharger la RAM
- 📊 **Barre de progression** - Visualisez la progression du transfert en temps réel
- 🎨 **Interface moderne** - UI élégante avec thème sombre et design moderne
- 🖱️ **Drag & Drop** - Glissez-déposez vos fichiers directement dans l'application
- 🌐 **Cross-platform** - Fonctionne sur Windows, Linux et macOS

## 📥 Téléchargement

Téléchargez la dernière version pour votre système :

| Plateforme | Téléchargement |
|------------|----------------|
| Windows x64 | [LanTransfer-win-x64.exe](../../releases/latest/download/LanTransfer-win-x64.exe) |
| Linux x64 | [LanTransfer-linux-x64](../../releases/latest/download/LanTransfer-linux-x64) |
| macOS Intel | [LanTransfer-osx-x64](../../releases/latest/download/LanTransfer-osx-x64) |
| macOS Apple Silicon | [LanTransfer-osx-arm64](../../releases/latest/download/LanTransfer-osx-arm64) |

## 🚀 Installation

### Windows
1. Téléchargez `LanTransfer-win-x64.exe`
2. Double-cliquez pour lancer (pas d'installation requise)
3. Optionnel: Créez un raccourci sur le Bureau

### Linux
```bash
# Téléchargez et rendez exécutable
chmod +x LanTransfer-linux-x64
./LanTransfer-linux-x64
```

### macOS
```bash
# Téléchargez et rendez exécutable
chmod +x LanTransfer-osx-x64  # ou osx-arm64 pour M1/M2
./LanTransfer-osx-x64
```

> **Note macOS**: Vous devrez peut-être autoriser l'application dans Préférences Système > Sécurité & Confidentialité.

## 🔧 Compilation depuis les sources

### Prérequis
- .NET 8.0 SDK

### Build
```bash
# Clone le projet
git clone https://github.com/votre-username/lantransfert.git
cd lantransfert

# Build pour la plateforme actuelle
dotnet build

# Exécuter
dotnet run --project src/LanTransfer.Desktop

# Build tous les exécutables (Linux/macOS)
chmod +x build.sh
./build.sh

# Build tous les exécutables (Windows)
build.bat
```

## 📡 Architecture Technique

```
┌─────────────────────────────────────────────────────────────┐
│                     Avalonia UI (MainWindow)                 │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────┐ │
│  │   Peer List     │  │   Drop Zone     │  │  Progress   │ │
│  └─────────────────┘  └─────────────────┘  └─────────────┘ │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                    MainWindowViewModel                       │
│           (Dispatcher.UIThread pour mises à jour)           │
└─────────────────────────────────────────────────────────────┘
        │                                       │
        ▼                                       ▼
┌───────────────────┐                 ┌───────────────────────┐
│ UdpDiscoveryService│                 │   TcpTransferServer   │
│  Port UDP: 45454   │                 │   TcpTransferClient   │
│  Broadcast 2s      │                 │   Port TCP: dynamique │
└───────────────────┘                 └───────────────────────┘
```

### Gestion des buffers

Le transfert utilise des chunks de 8KB pour éviter de charger tout le fichier en mémoire :

```csharp
const int BUFFER_SIZE = 8192; // 8KB
byte[] buffer = new byte[BUFFER_SIZE];

while ((bytesRead = await fileStream.ReadAsync(buffer)) > 0)
{
    await networkStream.WriteAsync(buffer, 0, bytesRead);
    OnProgressUpdate(totalSent, fileSize);
}
```

## 🔒 Sécurité

- Aucune connexion Internet requise
- Tous les transferts restent sur le réseau local
- Validation des noms de fichiers contre les attaques path traversal
- Fichiers temporaires nettoyés en cas d'échec

## 📝 License

MIT License - voir [LICENSE](LICENSE) pour plus de détails.
