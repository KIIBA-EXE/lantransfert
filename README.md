# 🚀 KITRANSFERT

> **Transferts de fichiers ultra-rapides, locaux et distants.**  
> Plus simple qu'AirDrop, plus puissant, et cross-plateforme.

![Icon](src/LanTransfer.Desktop/Assets/icon.png)

## ✨ Fonctionnalités

- **⚡ Transfert instantané** : Glissez-déposez vos fichiers et dossiers.
- **🌐 Réseau Local (LAN)** : Découverte automatique des appareils sur le même WiFi.
- **🌍 Mode Distant** : Connectez-vous avec n'importe qui via un **Code de Partage** sécurisé.
- **🔒 Sécurisé** : Transfert direct P2P (Peer-to-Peer). Vos fichiers ne transitent pas par le cloud.
- **👤 Profil** : Choisissez votre pseudo pour être reconnu facilement.
- **💻 Cross-Plateforme** : Compatible Windows, Linux et macOS.

---

## 📥 Installation

Pas d'installation complexe requise. Téléchargez simplement l'exécutable pour votre système.

### 🪟 Windows
1. Téléchargez le fichier `LanTransfer-win-x64.exe`.
2. Double-cliquez pour lancer.
3. (Optionnel) Faites un clic droit > "Épingler à la barre des tâches".

### 🐧 Linux
1. Téléchargez le fichier `LanTransfer-linux-x64`.
2. Ouvrez un terminal dans le dossier de téléchargement.
3. Rendez le fichier exécutable et lancez-le :
   ```bash
   chmod +x LanTransfer-linux-x64
   ./LanTransfer-linux-x64
   ```
4. (Recommandé) Déplacez-le dans `/usr/local/bin` ou créez un raccourci `.desktop`.

### 🍎 macOS
1. Téléchargez :
   - Pour Mac Intel : `LanTransfer-osx-x64`
   - Pour Mac M1/M2/M3 : `LanTransfer-osx-arm64`
2. Rendez le fichier exécutable :
   ```bash
   chmod +x LanTransfer-osx-arm64  # Ou osx-x64
   ```
3. Lancez le fichier.
   *Note : Si macOS bloque l'ouverture ("Développeur non identifié"), faites Clic-Droit > Ouvrir, puis confirmez.*

---

## 🎮 Comment utiliser

### 1. Envoyer des fichiers
1. Lancez **KITRANSFERT** sur les deux appareils.
2. Si vous êtes sur le même WiFi, l'autre appareil apparaît automatiquement dans la liste de gauche.
3. **Glissez** vos fichiers/dossiers dans la zone de droite.
4. Cliquez sur le nom du destinataire dans la liste. C'est envoyé ! 🚀

### 2. Connecter un ami distant (Internet)
Si vous n'êtes pas sur le même réseau :
1. Cliquez sur **"🔗 Partager mon code"**.
2. Donnez le code à votre ami (ex: `ABC-123`).
3. Votre ami clique sur **"🎯 Entrer un code ami"** et tape le code.
4. Vous êtes connectés ! Vous pouvez transférer comme si vous étiez à côté.

---

## 🔨 Compilation (Pour les développeurs)

Si vous souhaitez modifier le code source :

### Pré-requis
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Cloner et lancer
```bash
git clone https://github.com/votre-repo/kitransfert.git
cd kitransfert
dotnet run --project src/LanTransfer.Desktop
```

### Générer les exécutables (Release)
```bash
# Linux/macOS
./build.sh

# Windows
./build.bat
```
Les fichiers seront créés dans le dossier `releases/`.

---

## 🔧 Architecture Technique

- **Frontend** : Avalonia UI (C# / XAML)
- **Backend** : .NET 8
- **Découverte** : UDP Broadcast (Port 45454)
- **Transfert** : TCP Sockets (Ports dynamiques)
- **Signalisation** : Node.js (WebSocket/HTTP) pour la mise en relation distante.

---

Made with ❤️ by Kiiba.
