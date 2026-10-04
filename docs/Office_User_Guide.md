# OpenEDM - Office User Guide & Policies

Welcome to OpenEDM! This tool makes it incredibly easy to safely edit files on our company network without worrying about accidentally overwriting your coworkers' changes or violating our WORM (Write-Once, Read-Many) server constraints.

Think of OpenEDM like a library: if you want to write in a book, you have to "check it out" first. When you are done, you "check it in" so others can read it.

---

## 🛑 Core Office Policies
Before using OpenEDM, please review our strict data governance policies:
1. **Never edit files directly on the Z: or H: drive.** All modifications MUST be done in your personal A:\ drive (WIP Workspace).
2. **Leave meaningful notes.** When checking in a file, "updated" is not a valid note. Summarize exactly what you changed (e.g., "Updated dimensions on bracket housing").
3. **Do not hoard locks.** If you are going on vacation or finishing for the week, check in your files or release your locks. *IT Administrators will forcefully purge active locks older than 72 hours.*
4. **Never manually delete lock files.** Do not attempt to bypass the lock system. Let the software manage it.

---

## Step 1: Getting Your Files (Sync to WIP)
Before you can edit any files, you need to download a safe copy to your personal workspace.

1. Open **Windows Explorer** and navigate to our office server.
2. Find the project folder you want to work on.
3. **Right-click** on the folder and click **Sync Project to WIP**.
4. OpenEDM is now copying those files into your personal A:\ drive workspace. 

*(Note: The files in your workspace are initially "Read-Only" to prevent accidental edits before you lock them).*

---

## Step 2: Unlocking a File (Acquire Lock)
You must officially claim a file before making edits so your coworkers know you are working on it.

1. Open your **WIP Workspace** (A:\).
2. **Right-click** on the specific file you want to edit and click **Acquire Lock (Edit)**.
3. OpenEDM will securely lock the file on the server under your name.

You can now open the file in AutoCAD, Word, Excel, or any other program, and edit it normally!

---

## Step 3: Saving Your Work (Check-In Changes)
When you are completely finished with your edits:

1. **Right-click** the file in your WIP folder and click **Check-In Changes**.
2. A window will pop up showing the file you are about to check in.
3. Type a brief explanation into the **Notes** box (Remember Policy #2!).
4. Click the blue **Check In** button.
5. OpenEDM will upload your file and release the lock so others can use it.

---

## Handling Conflicts (The "Exists on Server" Warning)
Because our server uses a strict Append-Only (WORM) architecture, you cannot overwrite old files. 

If you try to Check-In a file and the original file is still sitting on the server, the Check-In screen will show a red warning: **"⚠ Exists on server"**. 
- Don't panic! OpenEDM will automatically suggest a new name for your file by adding _v2 (Version 2). 
- Leave it as _v2 (or increment it) and click **Check In**.

---

## Reverting Changes (Release Lock)
If you made some edits but decided you want to scrap them and start over:

1. **Right-click** the file in your WIP folder.
2. Click **Release Lock (Undo)**.
3. OpenEDM will unlock the file on the server and undo your local changes, reverting your file back to the exact way it looked on the server.
