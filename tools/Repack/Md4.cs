using System;

namespace ServerRoomBackground.Repack
{
    /// <summary>MD4 (RFC 1320). Unity's MonoBehaviour ScriptIdHash = MD4(UTF8(class + namespace + assembly)); the repacked scene uses it for every script type entry.</summary>
    static class Md4
{
    public static byte[] Hash(byte[] input)
    {
        uint a = 0x67452301, b = 0xefcdab89, c = 0x98badcfe, d = 0x10325476;
        long bitLen = (long)input.Length * 8;
        int padLen = ((input.Length + 8) / 64 + 1) * 64;
        var msg = new byte[padLen]; Array.Copy(input, msg, input.Length); msg[input.Length] = 0x80;
        BitConverter.GetBytes(bitLen).CopyTo(msg, padLen - 8);
        uint R(uint x, int s) => (x << s) | (x >> (32 - s));
        for (int off = 0; off < padLen; off += 64)
        {
            var X = new uint[16]; for (int i = 0; i < 16; i++) X[i] = BitConverter.ToUInt32(msg, off + i * 4);
            uint aa = a, bb = b, cc = c, dd = d;
            uint F(uint x, uint y, uint z) => (x & y) | (~x & z); uint G(uint x, uint y, uint z) => (x & y) | (x & z) | (y & z); uint H(uint x, uint y, uint z) => x ^ y ^ z;
            int[] s1 = { 3, 7, 11, 19 }, s2 = { 3, 5, 9, 13 }, s3 = { 3, 9, 11, 15 };
            for (int i = 0; i < 16; i++) { var t = a + F(b, c, d) + X[i]; a = d; d = c; c = b; b = R(t, s1[i % 4]); }
            int[] o2 = { 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15 };
            for (int i = 0; i < 16; i++) { var t = a + G(b, c, d) + X[o2[i]] + 0x5a827999; a = d; d = c; c = b; b = R(t, s2[i % 4]); }
            int[] o3 = { 0, 8, 4, 12, 2, 10, 6, 14, 1, 9, 5, 13, 3, 11, 7, 15 };
            for (int i = 0; i < 16; i++) { var t = a + H(b, c, d) + X[o3[i]] + 0x6ed9eba1; a = d; d = c; c = b; b = R(t, s3[i % 4]); }
            a += aa; b += bb; c += cc; d += dd;
        }
        var r = new byte[16]; BitConverter.GetBytes(a).CopyTo(r, 0); BitConverter.GetBytes(b).CopyTo(r, 4); BitConverter.GetBytes(c).CopyTo(r, 8); BitConverter.GetBytes(d).CopyTo(r, 12);
        return r;
    }
}
}
