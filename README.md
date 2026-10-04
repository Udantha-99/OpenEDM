# OpenEDM (Enterprise Document Management)

An enterprise-grade, WORM-compliant Windows Shell Extension and Audit Dashboard designed to solve network concurrency, file-locking collisions, and strict append-only storage constraints over SMB.

## Features
- **Granular File Locking:** Implements sidecar `.openedmlock` files that exist alongside server documents, preventing users from overwriting each other's work without modifying the original document metadata.
- **WORM Compliance Abstracted:** Safely maps volatile sidecar files (locks and states) to a separate, standard NTFS network share while leaving the primary engineering dataset fully locked down under strict TrueNAS WORM (Write-Once-Read-Many) ACLs.
- **Out-of-Process Shell Extension:** The Context Menu UI safely delegates to a standalone `OpenEDMHelper.exe` process, fully protecting the `explorer.exe` COM thread from network timeouts and RPC deadlocks.
- **Encrypted Global Audit Logging:** Generates unique JSON event payloads encrypted with Hybrid RSA-2048 and AES-256, allowing compliance teams to safely read logs without exposing PII or intellectual property in plain text.

## Components
1. **OpenEDMShellExtension.dll:** The SharpShell context menu handler.
2. **OpenEDMHelper.exe:** The UI and network operations processor.
3. **OpenEDMAuditReader.exe:** A WPF administrative dashboard for reviewing cryptographically secured session logs.

## Deployment
Use the included `build-msi.ps1` to compile the solution and generate a WiX `.msi` file suitable for global GPO deployment.

```bash
.\installer\build-msi.ps1
```

*Note: Ensure you generate your own RSA keypair using `GenerateKeys.ps1` before deployment.*
