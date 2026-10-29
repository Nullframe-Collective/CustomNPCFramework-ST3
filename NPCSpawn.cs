using Il2Cpp;
using Il2CppFishNet.Object;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;

namespace CustomNPCFramework
{
    internal static class NPCSpawn
    {
        internal sealed class Request
        {
            internal NPCInfo Info;
            internal int Team;
            internal bool Claimed;
        }

        internal sealed class State
        {
            internal NPCInfo Info;
            internal GameObject Object;
            internal int Team;
            internal bool Applied;
            internal bool MovementApplied;
        }

        private static readonly Dictionary<int, State> States = new();
        [ThreadStatic] internal static Request Current;

        internal static State Get(GameObject gameObject) => gameObject && States.TryGetValue(gameObject.GetInstanceID(), out var state) ? state : null;

        internal static void Update()
        {
            foreach (var (key, state) in States.ToArray())
            {
                if (!state.Object)
                {
                    States.Remove(key);
                    continue;
                }

                if (state.Info.PlayerMonster == null || state.MovementApplied) continue;

                PlayerMonster player = state.Object.GetComponent<PlayerMonster>();
                if (player && player.GFHPOIPBLGE != null && player.GFHPOIPBLGE.GKJOHCHABPI != null)
                {
                    Core.ApplyStatistics(state.Info, state.Object);
                    state.MovementApplied = true;
                }
            }
        }

        internal static void Clear() => States.Clear();

        internal static bool Resolve(string path, out NPCInfo info, out int team)
        {
            info = null;
            team = 0;
            if (path == null) return false;

            int slash = path.IndexOf('/');
            if (slash < 0) return false;

            string prefix = path[..slash];
            if (prefix is not ("NPCTeamA" or "NPCTeamB" or "PlayerTeamA" or "PlayerTeamB" or "CustomNPCs")) return false;

            info = Core.CheckNPCInfos(path[(slash + 1)..]);
            if (info == null) return false;

            if (prefix != "CustomNPCs" && prefix.StartsWith("Player", StringComparison.Ordinal) != (info.PlayerMonster != null))
            {
                info = null;
                return false;
            }

            team = prefix.EndsWith("B", StringComparison.Ordinal) ? 1 : 0;
            return true;
        }

        internal static State Attach(GameObject gameObject, NPCInfo info, int team)
        {
            if (!Matches(gameObject, info))
            {
                throw new InvalidOperationException("DummyNPC does not contain the selected NPC behaviour: " + info.Name);
            }

            int key = gameObject.GetInstanceID();
            if (States.TryGetValue(key, out var existing))
            {
                if (existing.Info.Id != info.Id || existing.Info.ContentHash != info.ContentHash)
                {
                    throw new InvalidOperationException("A spawned NPC cannot change its pack identity.");
                }

                existing.Team = team;

                return existing;
            }

            State state = new() { Info = info, Object = gameObject, Team = team };
            States[key] = state;

            Bot bot = gameObject.GetComponent<Bot>();
            BossBot boss = gameObject.GetComponent<BossBot>();
            if (bot)
            {
                bot.BLPBCBFEMNA = team;
            }

            if (boss)
            {
                boss.BLPBCBFEMNA = team;
            }

            Core.ApplyStatistics(info, gameObject);

            return state;
        }

        internal static bool Matches(GameObject gameObject, NPCInfo info) => info.Type switch
        {
            NPCType.Bot => gameObject.GetComponent<Bot>() != null,
            NPCType.BossBot => gameObject.GetComponent<BossBot>() != null,
            NPCType.CustardBot => gameObject.GetComponent<CustardBot>() != null,
            NPCType.PlayerMonster => gameObject.GetComponent<PlayerMonster>() != null,
            _ => false
        };

        internal static void Apply(GameObject gameObject)
        {
            State state = Get(gameObject);
            if (state?.Info == null) return;

            try
            {
                Core.SetupCustomNPC(state.Info.Name, gameObject, state.Info.Type);
                gameObject.name = state.Info.Name;

                var agent = gameObject.GetComponent<NavMeshAgent>();
                var network = gameObject.GetComponent<NetworkObject>();
                if (agent && network && network.IsSpawned)
                {
                    agent.enabled = network.IsServer;
                }
            }
            catch (Exception exception)
            {
                MelonLogger.Error("Cannot configure NPC " + state.Info.Name + ": " + exception.Message);
                throw;
            }
        }
    }
}
