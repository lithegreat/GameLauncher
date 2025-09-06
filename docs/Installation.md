English | [简体中文](./docs/Installation.zh-CN.md) 

# 📥 Installation Instructions

Before installing GameLauncher, you need to install the application signing certificate:

1. **Download Certificate Files**:
   - Download the latest version from the [Releases](https://github.com/lithegreat/GameLauncher/releases) page
   - Download the release package containing both `.msix` installer and `.cer` certificate file

2. **Install Certificate**:
   - Right-click on the downloaded `.cer` certificate file
   - Select "Install Certificate"
   - In the Certificate Import Wizard, select "Local Machine"
   - Choose "Place all certificates in the following store"
   - Click "Browse" and select "Trusted Root Certification Authorities"
   - Complete the certificate installation

3. **Install Application**:
   - Double-click the `.msix` file to start installation
   - If the certificate is installed correctly, the application will install normally
   - If you encounter security warnings, the certificate was not installed correctly - repeat step 2
