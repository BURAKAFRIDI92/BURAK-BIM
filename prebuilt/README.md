# Prebuilt fallbacks

The installer **builds everything on your PC** against your installed Autodesk versions. That build includes full C# scripting.
The installer uses the files in this folder only when that build is impossible, for example when there is no internet access to fetch the .NET SDK.

| Folder | What | Limits |
|---|---|---|
| `server/` | Portable server (`dotnet BurakBim.Server.dll`, needs the .NET 8 runtime) | No UI-automation or screenshot tools |
| `revit/2024/` | Revit 2024 bridge, compiled against the Revit 2024 API | No `script_run` (C#). Every other Revit tool works |

As soon as the installer can build normally, it replaces these files.
