using System.Buffers.Binary;
using System.Text;

namespace CustomNPCFramework
{
    internal static class NPCPayload
    {
        private const uint Magic = 0x3143504E;
        private const int TailSize = 38;

        internal static byte[] Encode(string id, string hash, int team)
        {
            if (id == null || id.Length is < 3 or > 128 || id.Any(character => character > 127) ||
                hash == null || hash.Length != 64 || team is < 0 or > 1)
            {
                throw new ArgumentException("Invalid NPC spawn identity.");
            }

            byte[] name = Encoding.ASCII.GetBytes(id);
            byte[] result = new byte[name.Length + TailSize];
            name.CopyTo(result, 0);
            Convert.FromHexString(hash).CopyTo(result, name.Length);
            result[^6] = (byte)team;
            result[^5] = (byte)name.Length;
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(result.Length - 4), Magic);

            return result;
        }

        internal static bool TryRead(ReadOnlySpan<byte> payload, out string id, out string hash, out int team)
        {
            id = hash = null;
            team = 0;

            if (payload.Length < TailSize + 3 || BinaryPrimitives.ReadUInt32LittleEndian(payload[^4..]) != Magic)
            {
                return false;
            }

            int length = payload[^5];
            team = payload[^6];

            if (length is < 3 or > 128 || team > 1 || payload.Length < length + TailSize) return false;

            ReadOnlySpan<byte> name = payload.Slice(payload.Length - length - TailSize, length);

            foreach (byte character in name)
            {
                if (!(character is >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9' or
                    (byte)':' or (byte)'-' or (byte)'_' or (byte)'.')) return false;
            }

            id = Encoding.ASCII.GetString(name);
            if (!id.Contains(':')) return false;

            hash = Convert.ToHexString(payload.Slice(payload.Length - TailSize, 32));

            return true;
        }
    }
}
