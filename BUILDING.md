# Building Server Room Background

## Projects (`src/`)

| Project | Target | Output |
|---|---|---|
| `ServerRoomBackground` | netstandard2.1 | BepInEx plugin: registers the background, loads the bundles, runtime fixes |
| `ServerRoomBackground.Scripts` | netstandard2.1 | The 5 re-implemented live scripts the scene uses (the bundles reference this assembly by name) |
| `ServerRoomBackground.Prepatch` | netstandard2.0 | BepInEx patcher: adds `EEnvironmentUIType.ServerRoomEnvironmentUiType = 11` |
| `ServerRoomBackground.Server` | net10.0 | SPT server mod: customization entry, unlock, locale |

```
dotnet build ServerRoomBackground.slnx -c Release -p:SPTPath="C:\path\to\SPT"
```

`SPTPath` must point to an SPT 4.1.6 install; it is used for reference assemblies only.
Building the server mod needs the .NET 10 SDK.

## Asset bundles (`tools/`)

The bundles are generated from a local copy of the live game; no game files are in this repository.

1. **Strip** (`tools/Strip`): copies of SPT's `Assembly-CSharp` / `Assembly-CSharp-firstpass`
   reduced to the scene's classes and their serialized fields, renamed `EFTAsm` / `EFTFirstpass`
   (Unity reserves the original names, and the full assembly crashes the editor). Set
   `EFT_MANAGED` to SPT's `EscapeFromTarkov_Data/Managed` folder.
2. **Oracle** (`tools/Oracle`): a Unity **2022.3.43f1** project. Put the stripped assemblies,
   the other SPT managed dlls the classes need, and `ServerRoomBackground.Scripts.dll` into
   `Assets/Plugins`, then run `MenuOracle.Run` in batch mode. It builds `Out/oracle_*.bundle`,
   which hold exact type trees for every class listed in `classes.txt`.
3. **Repack** (`tools/Repack`):
   ```
   Repack <live EscapeFromTarkov_Data> <oracle Out folder> <output folder>
   ```
   It reads the live scene and everything it references, without writing to the live install.
   It parses every object with its exact type tree, and fails if any object does not consume its
   exact byte size. It writes `serverroom_assets.bundle` and `serverroom_scene.bundle`.
4. **TestPlayer** (`tools/TestPlayer`): an optional Unity player that loads the bundles,
   reports missing scripts and takes screenshots.

The video comes from the live game's
`StreamingAssets/Video/EnvironmentUIBg/Season_BD/main_menu-server's_room(animate_exp).webm`.
In the mod it goes in `BepInEx/plugins/ServerRoomBackground/Video/EnvironmentUIBg/Season_BD/`.
