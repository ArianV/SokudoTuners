# Sokudo Tuners

A car tuning mod for Schedule I. Nitro and performance upgrades with an arcade feel, built to make driving actually fun.

This is the successor to my older mod, VNitro. I'm rebuilding it from scratch with a lot more planned.

> **Work in progress.** Single-player only for now.

## Features

- **Nitro:** hold Left Shift while driving for a strong forward boost.

## Planned

- Nitro meter with a UI bar, FOV kick and exhaust effects
- Engine, tire, suspension and brake upgrade tiers
- A tuning menu where upgrades cost in-game cash
- Upgrades saved per car

## Install

1. Switch Schedule I to the **alternate** (Mono) beta branch: right-click the game in Steam → Properties → Betas. The mod is built for the Mono version and won't load on the default IL2CPP branch.
2. Install [MelonLoader](https://github.com/LavaGang/MelonLoader.Installer/releases/latest) **0.7.3** and launch the game once.
3. Drop `SokudoTuners.dll` into `Schedule I\Mods`.

## Building from source

Needs the .NET SDK (any recent version) and the game installed with MelonLoader.

```powershell
.\build.ps1
```

This builds the DLL and copies it straight into the game's `Mods` folder. If your game isn't at `F:\SteamLibrary\steamapps\common\Schedule I`, either change `GamePath` in `SokudoTuners.csproj` or pass it in:

```powershell
dotnet build -p:GamePath="C:\Path\To\Schedule I"
```
