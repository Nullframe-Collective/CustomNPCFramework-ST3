# Validation — 25 October 2026

CustomNPCFramework 1.1.0 with NullServers 0.0.27. Game: Slendytubbies 3 Multiplayer 2.625, Windows x64, Unity 6000.0.82, MelonLoader 0.7.3.

## Automated checks

- 125 NPC checks passed: inherited JSON fields, descriptors, invalid data, hashes, bounded payload parsing, teams, enabled state, selected content and restoration.
- 80 NullServers addon integration checks passed, including NPC discovery, metadata, required packs, missing DLLs, changed packs, extra local content, enable controls and restoration.
- Both mod projects build with zero warnings and zero errors.

## In-game checks

- TrainingMonster appears in native sandbox NPC categories and spawns through Volume.SendOption and the native server spawner.
- The spawned native Bot is network-spawned and server-controlled; health 350, damage 12 and speed 2 were read back.
- Purple model, movement, attacks, player death and player respawn were observed. The process remained running for several minutes after spawn.
- The real FishNet writer and reader reconstructed TrainingMonster on a fresh native dummy, preserving health 350 and consuming the exact payload. Both inline length and explicitly supplied nullable length passed.
- Addons lists CustomNPCFramework 1.1.0 as installed and enabled after returning from the room.
- Returning to the menu and creating another sandbox room in the same process passed: the custom console entry, network spawn and both payload roundtrips worked again.
- A reproducible CLR access violation was isolated to Harmony's generated reverse callback for ReadPayload(..., Nullable<int>). The replacement native callback preserves the verified Windows x64 value ABI and resolves the target through method metadata.

## Remaining coverage

No connection between two separate computers was available. Join, late join and room restart with another player should be checked before public distribution. Single-process payload reconstruction exercises the real serialization path but does not replace that test.

The supplied TrainingMonster reuses a native model. Third-party bundle-based models, every boss/custard/player-monster variation, and all custom animation/ragdoll assets have not received a complete in-game test matrix. Descriptor and asset validation do not establish that an author's animations or rig work correctly.

Temporary NPCSpawnChecks and NPCPayloadChecks helpers are excluded from the release. Existing CinematicUnityExplorer warnings about its own UI bundle are unrelated to the verified NPC spawn path.
