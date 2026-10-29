using HarmonyLib;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
using Il2CppFishNet.Serializing;
using Il2CppInterop.Common;
using MelonLoader;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace CustomNPCFramework
{
    internal static class NPCPayloadReader
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ReadPayload(IntPtr instance, IntPtr connection, IntPtr networkObject,
            IntPtr reader, ulong payloadLength, IntPtr method);

        private static MelonLoader.NativeUtils.NativeHook<ReadPayload> _hook;
        private static ReadPayload _original;
        private static readonly ReadPayload Callback = Read;

        internal static void Initialize()
        {
            if (_original != null) return;
            if (IntPtr.Size != 8) throw new PlatformNotSupportedException("NPC spawn payloads require the Windows x64 game.");

            var method = AccessTools.Method(typeof(ManagedObjects), nameof(ManagedObjects.ReadPayload),
                [typeof(NetworkConnection), typeof(NetworkObject), typeof(PooledReader), typeof(Il2CppSystem.Nullable<int>)]);

            var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);

            IntPtr methodInfo = (IntPtr)field.GetValue(null);
            IntPtr target = Marshal.ReadIntPtr(methodInfo);

            _hook = new(target, Marshal.GetFunctionPointerForDelegate(Callback));
            _hook.Attach();
            _original = _hook.Trampoline;
        }

        private static void Read(IntPtr instance, IntPtr connection, IntPtr networkObject,
            IntPtr readerPointer, ulong payloadLength, IntPtr method)
        {
            int end = -1;
            PooledReader reader = null;

            try
            {
                if (networkObject != IntPtr.Zero && readerPointer != IntPtr.Zero)
                {
                    reader = new PooledReader(readerPointer);
                    int start = reader.Position;
                    int length;

                    if ((payloadLength & 0xFF) != 0)
                    {
                        length = unchecked((int)(payloadLength >> 32));
                    }
                    else
                    {
                        length = reader.Remaining >= 4 ?
                            BinaryPrimitives.ReadInt32LittleEndian(reader.GetBuffer().AsSpan().Slice(start, 4)) : -1;
                        start += 4;
                    }

                    if (length >= 0 && length <= reader.Remaining - (start - reader.Position) &&
                        NPCPayload.TryRead(reader.GetBuffer().AsSpan().Slice(start, length), out string id, out string hash, out int team))
                    {
                        end = start + length;
                        NPCInfo info = Core.CheckNPCInfos(id);
                        if (info == null || info.ContentHash != hash)
                        {
                            throw new InvalidDataException("Missing or different required NPC pack: " + id);
                        }

                        var spawned = new NetworkObject(networkObject);
                        NPCSpawn.Attach(spawned.gameObject, info, team);
                        NPCSpawn.Apply(spawned.gameObject);
                    }
                }
            }
            catch (Exception exception)
            {
                MelonLogger.Error("Cannot read NPC spawn payload: " + exception);
            }

            _original(instance, connection, networkObject, readerPointer, payloadLength, method);
            if (end >= 0 && reader != null)
            {
                reader.Position = end;
            }
        }
    }
}
