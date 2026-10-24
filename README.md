# Server Room Background

**Version 1.0.0 · for SPT 4.1.6 · by amos**

Brings the live Escape from Tarkov **server room** main menu background to SPT: a looping
panorama of a dimly lit server room behind the menu, with an animated Baofeng radio that
clicks through its menus and occasionally takes an incoming call.

It shows up as **Server Room** in the game's own background selector, next to the vanilla
backgrounds.

---

## Features

- The live scene itself (`EnvironmentUIScene_Season_LAB`), repacked for the SPT 4.1.6 client:
  geometry, textures, materials, lighting, animations and sounds are the original live data
- The original 3440×1440 panorama video, looping
- The animated radio with its button clicks and five incoming-call recordings, played through
  the game's **UI** volume
- On trader, character and other screens the camera slides to the side of the panorama, to the
  scene's own left/right camera points, instead of the vanilla random rotation
- Unlocked for every profile automatically: no profile edits, nothing written to your save
- Compatible with other background mods (uses background type 11; WTT-ContentBackport uses 6–10)

## Requirements

- SPT **4.1.6**
- Nothing else

## Installation

1. Close the game and the SPT server.
2. Extract the archive into your SPT folder (the one containing `EscapeFromTarkov.exe`).
   You should end up with:
   ```
   BepInEx/plugins/ServerRoomBackground/
   BepInEx/patchers/ServerRoomBackground/
   SPT_Runtime/user/mods/ServerRoomBackground/
   ```
3. Start the server, then the game.
4. Open **Settings → Game** and choose **Server Room** as the main menu background.

### Fika

Install the full mod on the machine that runs the SPT server, and the `BepInEx` folders on
**every** player's game. A client without the mod receives a background type it does not know.

## Uninstallation

Delete the three `ServerRoomBackground` folders listed above. Your menu falls back to a
vanilla background.

## Troubleshooting

| Problem | Fix |
|---|---|
| **Server Room** is greyed out in the list | The server was running when the mod was installed: restart the SPT server. |
| **Server Room** is missing from the list | The `BepInEx/patchers/ServerRoomBackground` folder is missing or misplaced. |
| Black or empty background | Check that the `Video` folder inside `BepInEx/plugins/ServerRoomBackground` was extracted. |

The mod writes diagnostic lines to `BepInEx/LogOutput.log` (client) and the SPT server log,
all prefixed with `[ServerRoomBackground]`. Please include them when reporting a problem.

## How it works

The SPT 4.1.6 client is several versions older than live, so the scene could not be used as is:

- The scene and every asset it references were repacked into two asset bundles with exact
  Unity 2022.3.43 type trees, and verified object by object.
- Five scripts used by the scene do not exist in the SPT client (video player, camera slide,
  radio controller, radio sound player and an animator helper). They are re-implemented in
  `ServerRoomBackground.Scripts.dll` from the data stored in the scene: field values, the
  radio's animation events and its sound list.
- One camera effect in live has four settings the older client does not have. Those settings
  are ignored; everything else uses the client's own post-processing and audio mixer.
- A BepInEx patcher adds the new background type, the plugin registers the scene with the
  menu, and a small server mod adds, unlocks and names the background.

The full source code is available separately.

## Credits

- **Battlestate Games**: the original scene, models, textures, video and sounds. All game
  assets remain the property of Battlestate Games.
- **WTT-ContentBackport** by GrooveypenguinX: the background registration follows the same
  approach, so both mods work side by side.
- **SPT team**: SPT.

## License

The code is released under the MIT License (see `LICENSE.txt`). The license covers the code
only, not the Escape from Tarkov game assets in the bundles and the video.
