# Publishing BURAK BIM 1.3.0 (installer ZIP, Setup.exe, ZIP, .mcpb)

The release binaries are built by GitHub Actions on Windows, from your BURAK-BIM repository. The build creates:

- the self-contained server;
- the prebuilt bridges for Revit, AutoCAD and Navisworks 2022–2027;
- the offline compiler;
- `BURAK-BIM-1.3.0-Complete.zip` (`BURAK-BIM-Installer.exe`, `BURAK-BIM-Uninstaller.exe`, guide, checksums);
- `BURAK-BIM-Setup-1.3.0.exe`, `BURAK-BIM-1.3.0.zip` and `BURAK-BIM-1.3.0.mcpb`.

## Steps

1. Copy the contents of this folder over your local clone of `BURAKAFRIDI92/BURAK-BIM`, so it replaces the 1.2.0 files. Delete the old `prebuilt/revit/2024` and `prebuilt/server` folders: the build now creates fresh ones.
2. Commit and push to `main`:
   ```powershell
   git add -A
   git commit -m "BURAK BIM 1.3.0"
   git push
   ```
3. On GitHub, open **Actions > build**, wait for the green check (about 15 minutes), and download the **BURAK-BIM** artifact. Then:
   - The job summary lists every prebuilt bridge (OK / FAILED).
   - If something failed, download the **build-logs** artifact and send the log to your developer (or to Claude).
4. To publish a release, tag the commit:
   ```powershell
   git tag v1.3.0
   git push origin v1.3.0
   ```
   The workflow attaches Setup.exe, the ZIP and the .mcpb to a new GitHub Release, with `docs/RELEASE_NOTES.md` as the text.

## Test on a real PC before announcing
Follow the "1.3 additions" list at the top of [TESTING.md](TESTING.md). Most important: install with the network unplugged, check every Revit version you have, and check that each AI app calls `burak_status`.

## Removing the Windows "unknown publisher" warning (code signing)
Every new, unsigned .exe triggers Windows SmartScreen, whatever it contains. To remove the warning:
1. Get an OV or EV code-signing certificate. Options:
   - SignPath.io: free for open-source projects;
   - Certum Open Source Code Signing: low cost;
   - Microsoft Azure Trusted Signing;
   - DigiCert or Sectigo.
2. On GitHub, add repository secrets: `SIGN_PFX_BASE64` (the .pfx file, base64-encoded) and `SIGN_PFX_PASSWORD`.
3. Re-run the workflow. The server exe, the bridge DLLs and Setup.exe are then signed and timestamped.
4. If an antivirus product ever flags a file, submit it as a false positive. For Microsoft Defender use https://www.microsoft.com/wdsi/filesubmission. A signed release is cleared quickly.
