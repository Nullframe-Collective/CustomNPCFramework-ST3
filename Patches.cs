using HarmonyLib;
using Il2Cpp;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Managing.Server;
using Il2CppFishNet.Object;
using Il2CppFishNet.Serializing;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomNPCFramework
{
    [HarmonyPatch(typeof(CPPBDGGKHCB), nameof(CPPBDGGKHCB.FONDGACBGAB),
        [typeof(string), typeof(Vector3), typeof(Quaternion), typeof(NetworkConnection)])]
    public static class NetworkSpawnPathPatch
    {
        internal static bool Prefix(string __0, Vector3 __1, Quaternion __2, NetworkConnection __3, ref NetworkObject __result)
        {
            if (!NPCSpawn.Resolve(__0, out NPCInfo info, out int team)) return true;

            if (!info.UseDummyModel)
            {
                Core.EnsureBundle(info);
            }

            var prefab = Resources.Load<GameObject>(info.Data.DummyNPC);

            if (!prefab || !prefab.GetComponent<NetworkObject>() || !NPCSpawn.Matches(prefab, info) || !prefab.transform.Find("Model"))
            {
                MelonLogger.Error("Invalid DummyNPC prefab for " + info.Name + ": " + info.Data.DummyNPC);
                __result = null;

                return false;
            }

            NPCSpawn.Request previous = NPCSpawn.Current;
            NPCSpawn.Current = new NPCSpawn.Request { Info = info, Team = team };

            try
            {
                __result = CPPBDGGKHCB.FONDGACBGAB(prefab, __1, __2, __3);
            }
            finally
            {
                NPCSpawn.Current = previous;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(ServerManager), nameof(ServerManager.Spawn),
        [typeof(GameObject), typeof(NetworkConnection), typeof(Scene)])]
    public static class ServerSpawnPatch
    {
        internal static void Prefix(GameObject go)
        {
            NPCSpawn.Request request = NPCSpawn.Current;
            if (request == null || request.Claimed || !go) return;

            request.Claimed = true;
            NPCSpawn.Attach(go, request.Info, request.Team);
            NPCSpawn.Apply(go);
        }

        internal static void Postfix(GameObject go)
        {
            if (go)
            {
                NPCSpawn.Apply(go);
            }
        }
    }

    [HarmonyPatch(typeof(ManagedObjects), nameof(ManagedObjects.WritePayload))]
    public static class NPCWritePayloadPatch
    {
        internal static void Prefix(PooledWriter writer, out int __state) => __state = writer.Position;

        internal static void Postfix(NetworkObject nob, PooledWriter writer, int __state)
        {
            NPCSpawn.State state = nob ? NPCSpawn.Get(nob.gameObject) : null;
            if (state?.Info == null) return;

            int end = writer.Position;
            if (end < __state + 4) throw new InvalidDataException("Invalid FishNet spawn payload framing.");

            byte[] trailer = NPCPayload.Encode(state.Info.Id, state.Info.ContentHash, state.Team);
            writer.WriteUInt8Array(trailer, 0, trailer.Length);

            var buffer = writer.GetBuffer();
            uint length = checked((uint)(writer.Position - __state - 4));
            for (int index = 0; index < 4; index++)
            {
                buffer[__state + index] = (byte)(length >> (index * 8));
            }
        }
    }

    [HarmonyPatch(typeof(Volume), nameof(Volume.Start))]
    public static class VolumeStartPatch
    {
        public static void Postfix(Volume __instance) => MelonCoroutines.Start(Core.AddNPCInfos(__instance));
    }
}
