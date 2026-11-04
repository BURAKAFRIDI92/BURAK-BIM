# Prebuilt bridges (filled by the release build)

From 1.3.0, the release build (GitHub Actions, `.github/workflows/build.yml`) compiles every bridge against each product year's own API reference assemblies. It puts them here:

```
prebuilt/
  revit/2022 ... 2027/        BurakBim.Bridge.Revit.dll  (net48 for 2022-2024, net8 for 2025-2026, net10 for 2027)
  autocad/2022 ... 2027/      BurakBim.Bridge.AutoCAD.dll (AutoCAD, Civil 3D, Plant 3D, Map 3D, MEP, Advance Steel ...)
  navisworks/2022 ... 2027/   BurakBim.Bridge.Navisworks.dll
  _shared/net48/              Roslyn C# scripting, shared by all .NET Framework bridges
  _shared/netcore/            Roslyn C# scripting, shared by all .NET 8 / .NET 10 bridges
```

The installer copies the bridge for every version it finds, with full C# scripting. It needs no internet and no build tools.

If a version has no prebuilt bridge, the installer compiles that one bridge on the PC. It uses the bundled offline compiler (`tools/roslyn`) against the installed product's own DLLs. Only if that also fails does it fall back to a .NET SDK.

This folder is empty in the source repository. Autodesk DLLs are never stored here: the build references them only at compile time.
