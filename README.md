# OpenEDM (Enterprise Document Management)

OpenEDM is an ultra-lightweight, stateless Product Data Management (PDM) system that integrates directly into the Windows Explorer Context Menu. It is designed to bring check-in/check-out version control and granular file locking to standard network file shares, including strict WORM (Write-Once, Read-Many) and Append-Only NAS environments like TrueNAS.

## Key Features
- **Stateless Sidecar Locking:** Uses tiny `.openedmlock` sidecar files to reserve files. No heavy central database required.
- **WORM/Append-Only Compliant:** Built-in conflict resolution automatically catches "File Exists" errors and intelligently segments filenames with `_v2`, `_v3` suffixes.
- **Encrypted Audit Logs:** 100% of Check-In, Check-Out, and Undo actions are logged into AES-encrypted JSON files.
- **WPF Dashboard:** Ships with an Admin Utility (OpenEDM Audit Reader) to track file turn-around times, analyze lock heatmaps, and forcefully purge stale locks.
- **GPO & Enterprise Ready:** Deploys as an MSI. Configuration is managed via a central text file allowing instant IT rollout via Group Policy.

## Installation
Deploy the `OpenEDMShellExtension.msi` manually or via Group Policy. 

## Configuration
OpenEDM is highly configurable. Upon installation, use the **OpenEDM Audit Reader > Settings Tab** to generate your master configuration file, which is saved to:
`C:\ProgramData\OpenEDMShellExtension\openedm-config.txt`

### Sample Configuration
```ini
[SourceDrives]
Z
H

[OpenEDMPath]
A:\{USERNAME}

[AuditLogPath]
A:\OpenEDM_Admin_Logs

[LockPath]
A:\OpenEDM_Locks

[OpenEDMCleanup]
True
```
- **SourceDrives:** The network drives where OpenEDM is allowed to operate.
- **OpenEDMPath:** The local "WIP" (Work In Progress) directory where users edit files.
- **AuditLogPath & LockPath:** Where encrypted logs and lock sidecars are stored.
- **OpenEDMCleanup:** If true, automatically purges the local WIP directory when a Check-In succeeds.

## Using OpenEDM (Public User Guide)

### 1. Syncing Projects (Check-Out)
Right-click any folder on your configured `SourceDrives` and select **Sync Project to WIP**. 
- OpenEDM will copy the files into your local `OpenEDMPath` workspace and flag them as Read-Only.
- A hidden `.sourcepath.txt` is created to remember where the files came from.

### 2. Acquiring Locks
Inside your local workspace, right-click the file you want to edit and select **Acquire Lock (Edit)**.
- A `.openedmlock` sidecar is instantly dropped into the `LockPath` directory on the server, reserving the file under your Windows Username.
- The local Read-Only flag is removed so you can work.

### 3. Checking In
When finished, right-click the file and select **Check-In Changes**.
- The UI allows you to add commit notes and handles bulk check-ins.
- If your server blocks overwrites, OpenEDM will visually flag the file and prompt you to save it as a new revision (e.g., `_v2`).
- Upon success, the `.openedmlock` is deleted and the event is written to the encrypted Audit Log.

## Building from Source
Run the included PowerShell script to compile the C# assemblies, encrypt payloads, and package the MSI via WiX Toolset:
```powershell
.\installer\build-msi.ps1
```
