namespace Web.Data.BkgWorkers;

using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Decode Meshtastic ServiceEnvelope from msh/{root}/2/e/{canal}/{gateway}.
/// </summary>
public sealed class Meshtastic
{
    /// <summary>PortNum.TEXT_MESSAGE_APP</summary>
    public const int PortText = 1;

    private readonly byte[] psk;

    private Meshtastic(byte[] psk)
    {
        this.psk = psk;
    }

    public static Meshtastic? Create(string? pskBase64)
    {
        if (string.IsNullOrWhiteSpace(pskBase64)) return null;

        Span<byte> buffer = stackalloc byte[48];
        if (!Convert.TryFromBase64String(pskBase64, buffer, out int n)) return null;
        if (n != 16 && n != 32) return null;

        return new Meshtastic(buffer[..n].ToArray());
    }

    public sealed class Pacote
    {
        public uint From;
        public uint Id;
        public uint RxTime;
        public int PortNum;
        public string Text = "";
    }

    /// <summary>
    /// Decode ServiceEnvelope with PKI (Curve25519+AES-CCM)
    /// </summary>
    public Pacote? ReadEnvelope(ReadOnlySpan<byte> envelope)
    {
        try
        {
            ReadOnlySpan<byte> pacote = default;
            int pos = 0;
            while (NextField(envelope, ref pos, out int campo, out _, out ReadOnlySpan<byte> dados))
            {
                if (campo == 1) pacote = dados;
            }

            return pacote.IsEmpty ? null : ReadPacket(pacote);
        }
        catch (FormatException) { return null; }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private Pacote? ReadPacket(ReadOnlySpan<byte> packet)
    {
        uint from = 0, id = 0, rxTime = 0;
        bool pki = false;
        ReadOnlySpan<byte> cifrado = default;
        ReadOnlySpan<byte> dados;

        int pos = 0;
        while (NextField(packet, ref pos, out int campo, out ulong num, out dados))
        {
            switch (campo)
            {
                case 1: from = (uint)num; break;    // from    (fixed32)
                case 5: cifrado = dados; break;     // encrypted
                case 6: id = (uint)num; break;      // id      -> nonce
                case 7: rxTime = (uint)num; break;  // rx_time (epoch s)
                case 17: pki = num != 0; break;     // pki_encrypted
            }
        }

        if (cifrado.IsEmpty || pki) return null;
        if (cifrado.Length > 512) return null;

        byte[] claro = new byte[cifrado.Length];
        Decrypt(psk, from, id, cifrado, claro);

        int portnum = 0;
        ReadOnlySpan<byte> payload = default;
        pos = 0;
        while (NextField(claro, ref pos, out int campoData, out ulong numData, out ReadOnlySpan<byte> dadosData))
        {
            if (campoData == 1) portnum = (int)numData;
            else if (campoData == 2) payload = dadosData;
        }

        return new Pacote
        {
            From = from,
            Id = id,
            RxTime = rxTime,
            PortNum = portnum,
            Text = portnum == PortText ? Encoding.UTF8.GetString(payload) : "",
        };
    }

    private static void Decrypt(byte[] key, uint from, uint id, ReadOnlySpan<byte> input, Span<byte> output)
    {
        Span<byte> contador = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(contador, id);
        BinaryPrimitives.WriteUInt32LittleEndian(contador[8..], from);

        using var aes = Aes.Create();
        aes.Key = key;

        Span<byte> keystream = stackalloc byte[16];
        for (int offset = 0; offset < input.Length; offset += 16)
        {
            aes.EncryptEcb(contador, keystream, PaddingMode.None);

            int n = Math.Min(16, input.Length - offset);
            for (int i = 0; i < n; i++)
            {
                output[offset + i] = (byte)(input[offset + i] ^ keystream[i]);
            }

            for (int i = 15; i >= 12; i--)
            {
                if (++contador[i] != 0) break;
            }
        }
    }

    private static bool NextField(ReadOnlySpan<byte> buffer, scoped ref int pos, out int field, out ulong num, out ReadOnlySpan<byte> data)
    {
        field = 0; num = 0; data = default;
        if (pos >= buffer.Length) return false;

        ulong tag = ReadVarint(buffer, ref pos);
        field = (int)(tag >> 3);

        switch ((int)(tag & 7))
        {
            case 0:
                num = ReadVarint(buffer, ref pos);
                break;
            case 1:
                num = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(pos, 8));
                pos += 8;
                break;
            case 2:
                int n = (int)ReadVarint(buffer, ref pos);
                data = buffer.Slice(pos, n);
                pos += n;
                break;
            case 5:
                num = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(pos, 4));
                pos += 4;
                break;
            default:
                throw new FormatException("unsupported wiretype");
        }
        return true;
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> buffer, scoped ref int pos)
    {
        ulong resultado = 0;
        for (int shift = 0; shift <= 63; shift += 7)
        {
            if (pos >= buffer.Length) throw new FormatException("varint truncated");
            byte b = buffer[pos++];
            resultado |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return resultado;
        }
        throw new FormatException("varint too long");
    }
}
