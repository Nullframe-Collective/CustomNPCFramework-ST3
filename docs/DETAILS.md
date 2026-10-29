# CustomNPCFramework 1.1.0

Framework for custom Bot, BossBot, CustardBot and PlayerMonster content in Slendytubbies 3 Multiplayer 2.625 (Unity 6000 / MelonLoader 0.7.3 / FishNet).

## Installation and test NPC

Copy `Mods/CustomNPCFramework.dll` and the supplied `Mods/NullServers.dll` into the game. For a multiplayer test, both players need these DLLs and the same required NPC packs. NullServers 0.0.27 adds this framework to ADDONS and Required Addons. DLL version strings are informational; API and pack identity are checked.

Copy the example `UserData/CustomNPCs/TrainingMonster` directory into the game. Start a NullFrame sandbox room, open the normal sandbox console and choose TrainingMonster in either NPC team category. It is a purple Newborn at 1.35 scale, with 350 health, 12 damage and movement speed 2. It reuses the native `SUR/Newborn_Bot` model, so this test pack needs only npc.json. Remove the example directory before restarting if you no longer want the test content.

The diagnostic NPCSpawnChecks/NPCProbe DLL is a development helper and is not part of the release.

## Pack layout

```
UserData/CustomNPCs/MyNPC/
    npc.json
    bundle
    icon.png       (optional)
```

The existing JSON fields remain supported, including fields inherited from NPCData. Use one of Bot, BossBot, CustardBot or PlayerMonster, and a unique lowercase `Id` such as `author:monster`. Older packs without Id receive a deterministic `legacy:` identifier derived from Name. Name must also be unique, because it identifies bundle assets and sandbox options.

The main prefab inside bundle is named exactly Name. Bot additionally needs `Name(Dead)` or `Name(Ragdoll)`, depending on UseRagdoll; BossBot needs `Name(Dead)`. The dummy prefab must have the selected native behaviour, a root NetworkObject and a Model child. Models keep the native AI/network components on the dummy. Trigger colliders named `Hitbox <positive multiplier>` provide custom hitboxes. Use a dot for fractional multipliers.

`UseDummyModel: true` keeps the native model, death behaviour and animations instead of loading a bundle. Optional ModelScale (default 1) and ModelColor (Unity HTML colour, such as #A050FF) style either model. The sample demonstrates this path.

A pack hash covers the exact npc.json bytes and, for a bundle-based pack, the bundle bytes. Folder location and icon do not affect the hash. Distribute the same files to host and clients. Invalid descriptors are logged individually; they do not stop discovery of other packs. Missing/invalid required assets prevent compatibility approval.

## Spawning and networking

The current console path is `Volume.SendOption -> Volume.FMLPLLMJAPP -> CPPBDGGKHCB.FONDGACBGAB(string, ...) -> FONDGACBGAB(GameObject, ...) -> ServerManager.Spawn`. The old GFKAJFNHJCD patch has been removed. Native console positioning, limits, ownership, team configuration, RPC validation and NullServers anti-crash checks remain in this path.

The string spawner substitutes only registered custom paths (`NPCTeamA/Name`, `NPCTeamB/Name`, `PlayerTeamA/Name`, `PlayerTeamB/Name` or `CustomNPCs/Id`). It resolves the native dummy and configures the created instance immediately before ServerManager.Spawn. Ordinary paths and prefabs use the original implementation.

NPC identity and pack hash travel in FishNet's length-delimited spawn payload. Native behaviour payload bytes remain intact. Identity is reconstructed before client start callbacks and included whenever FishNet serializes a spawn, including for a later joining observer. Native team/target string arrays are not used as NPC identifiers.

NullServers selects the host's catalog for the room. Extra local packs stay on disk and are restored to the catalog when leaving. A missing DLL, missing pack or changed pack is reported through the existing addon UI and Discord download action. A disabled framework cannot register or spawn custom content.

CustomNPCFramework does not require a compile-time reference to NullServers. Without NullServers the spawn payload still requires the same framework/content on peers; pre-join addon notices are provided by NullServers.

## Public API

- Existing Core.NPCInfos, Core.LoadedBundles, Core.CheckNPCInfos, Core.SpawnCustomNPC, Core.SetupCustomNPC and NPCInfo fields are retained.
- SpawnCustomNPC(info, prefix, position, rotation) is a host-side spawn request through the native spawner. It does not grant clients authority to spawn objects.
- NPCCatalog.ApiVersion = 1, Enabled, IsReady and Items expose discovery and controls.
- NPCCatalog.UseContent(string[] ids) returns an IDisposable room selection. Dispose restores the prior selection.
- Core.IsInitialized, AssetsReady and Error expose readiness to NullServers.

## Build and checks

Set GamePath in Directory.Build.props (or pass -p:GamePath=...). Build with `dotnet build -c Release -p:SkipModInstall=true` to avoid installing into the game. The asset-bundle helper is referenced relative to GamePath/MelonLoader/net6.

`dotnet run --project tests/NPCChecks.csproj -c Release` exercises descriptor validation, hash sensitivity, bounded payload parsing and room catalog selection. Runtime and two-computer test coverage is recorded in the release validation notes.

The payload reader uses MelonLoader's native hook API with the Windows x64 Nullable<int> layout. It resolves the target through generated method metadata, without a hardcoded address. Patching this generated boxed-value signature with Harmony caused a reproducible CLR crash after spawn; the native callback preserves the value ABI.
