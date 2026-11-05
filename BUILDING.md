# Building RaidPad against a local SPT installation

RaidPad is a single SDK-style project (`net472`) that compiles against the game's
managed assemblies, the SPT plugin assemblies and BepInEx. None of those binaries
are part of this repository.

The repository is called `RaidPadContinued`; the mod itself is still RaidPad - plugin
GUID `com.thablackunicorn.raidpad`, `RaidPad.dll`, folder `BepInEx\plugins\RaidPad`.

Every button binding of the current release is documented in [`docs/BINDS.md`](docs/BINDS.md),
with a one-page version as [`docs/cheatsheet.html`](docs/cheatsheet.html) (or the rendered
[`docs/cheatsheet.png`](docs/cheatsheet.png)).

## Requirements

* Windows with
  * the .NET SDK (8.0 or newer) — the project is SDK-style
  * Visual Studio 2022 Build Tools with the .NET Framework 4.7.2 targeting pack
    (the SDK alone does not ship the .NET Framework reference assemblies)
* SPT installation with BepInEx 5 and the `spt` plugin folder
  (client 0.16.9.x / SPT 4.1.x is what the current sources target)

## Path configuration

The project derives every reference path from `SPTGamePath`:

```xml
<SPTGamePath Condition="'$(SPTGamePath)' == ''">D:\Games\SPT 4.1.6 essential</SPTGamePath>
<EftManaged>$(SPTGamePath)\EscapeFromTarkov_Data\Managed</EftManaged>
<SptPlugins>$(SPTGamePath)\BepInEx\plugins\spt</SptPlugins>
<BepInExCore>$(SPTGamePath)\BepInEx\core</BepInExCore>
```

Override per build:

```
dotnet build RaidPad\RaidPad.csproj -c Release /p:SPTGamePath="D:\Games\SPT 4.1.6 essential"
```

## SharpDX

`SharpDX.dll` and `SharpDX.XInput.dll` come from the `SharpDX.XInput` 4.2.0
package reference. The old `..\libs\` hint paths pointed at files that were never
committed and have been removed.

## Generated PluginInfo.g.cs

`ModVersion` in the project file is written to `PluginInfo.g.cs` by the
`GeneratePluginInfo` target. Because the file only exists after the first build,
the project lists it explicitly (`<Compile Remove>` + `<Compile Include>`);
without that a clean clone fails with `CS0117: "PluginInfo" contains no
definition for "Version"`.

## Build

```
powershell -ExecutionPolicy Bypass -File build-spt.ps1
```

Options: `-SptGamePath "D:\…"`, `-Package` (writes `build\RaidPad-<version>.zip`
with the release layout), `-SkipDeploy`. Deployment is skipped while
`EscapeFromTarkov.exe` is running.

## Install layout

```
BepInEx\plugins\RaidPad\
├── RaidPad.dll
├── SharpDX.dll
├── SharpDX.XInput.dll
└── assets\Default.json, assets\images\*.png
```

`assets\sounds\` is optional — the mod logs
`Sound asset directory not found, skipping audio load` when it is missing and
continues without sound.
