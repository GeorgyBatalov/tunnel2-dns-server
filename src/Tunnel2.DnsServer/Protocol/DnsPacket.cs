using System.Net;
using System.Text;

namespace Tunnel2.DnsServer.Protocol;

/// <summary>
/// Simple DNS packet parser and builder for A and TXT records.
/// </summary>
public class DnsPacket
{
    public ushort TransactionId { get; set; }
    public ushort Flags { get; set; }
    public List<DnsQuestion> Questions { get; set; } = new();
    public List<DnsResourceRecord> Answers { get; set; } = new();

    /// <summary>
    /// Parses a DNS query packet from raw bytes.
    /// </summary>
    public static DnsPacket Parse(byte[] data)
    {
        if (data.Length < 12)
        {
            throw new ArgumentException("DNS packet too short");
        }

        DnsPacket packet = new DnsPacket
        {
            TransactionId = (ushort)((data[0] << 8) | data[1]),
            Flags = (ushort)((data[2] << 8) | data[3])
        };

        ushort questionCount = (ushort)((data[4] << 8) | data[5]);
        ushort answerCount = (ushort)((data[6] << 8) | data[7]);
        int offset = 12;

        // Parse questions
        for (int i = 0; i < questionCount; i++)
        {
            string name = ReadName(data, ref offset);
            ushort type = (ushort)((data[offset] << 8) | data[offset + 1]);
            ushort classCode = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
            offset += 4;

            packet.Questions.Add(new DnsQuestion
            {
                Name = name,
                Type = type,
                Class = classCode
            });
        }

        // Parse answers
        for (int i = 0; i < answerCount; i++)
        {
            string name = ReadName(data, ref offset);
            ushort type = (ushort)((data[offset] << 8) | data[offset + 1]);
            ushort classCode = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
            uint ttl = (uint)((data[offset + 4] << 24) | (data[offset + 5] << 16) |
                             (data[offset + 6] << 8) | data[offset + 7]);
            ushort dataLength = (ushort)((data[offset + 8] << 8) | data[offset + 9]);
            offset += 10;

            object? resourceData = null;
            if (type == 1 && dataLength == 4) // A record
            {
                resourceData = $"{data[offset]}.{data[offset + 1]}.{data[offset + 2]}.{data[offset + 3]}";
            }
            else if (type == 16) // TXT record
            {
                byte textLength = data[offset];
                resourceData = System.Text.Encoding.ASCII.GetString(data, offset + 1, textLength);
            }

            offset += dataLength;

            packet.Answers.Add(new DnsResourceRecord
            {
                Name = name,
                Type = type,
                Class = classCode,
                Ttl = ttl,
                Data = resourceData
            });
        }

        return packet;
    }

    /// <summary>
    /// Builds a DNS query packet (for testing).
    /// </summary>
    public byte[] BuildQuery()
    {
        using MemoryStream stream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(stream);

        // Header
        writer.Write((byte)(TransactionId >> 8));
        writer.Write((byte)(TransactionId & 0xFF));
        writer.Write((byte)(Flags >> 8));
        writer.Write((byte)(Flags & 0xFF));
        writer.Write((byte)(Questions.Count >> 8));
        writer.Write((byte)(Questions.Count & 0xFF));
        writer.Write((ushort)0); // Answer RRs (0 for query)
        writer.Write((ushort)0); // Authority RRs
        writer.Write((ushort)0); // Additional RRs

        // Questions
        foreach (DnsQuestion question in Questions)
        {
            WriteName(writer, question.Name);
            writer.Write((byte)(question.Type >> 8));
            writer.Write((byte)(question.Type & 0xFF));
            writer.Write((byte)(question.Class >> 8));
            writer.Write((byte)(question.Class & 0xFF));
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Builds a DNS response packet.
    /// </summary>
    public byte[] BuildResponse()
    {
        using MemoryStream stream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(stream);

        // Header
        writer.Write((byte)(TransactionId >> 8));
        writer.Write((byte)(TransactionId & 0xFF));
        writer.Write((byte)(Flags >> 8));
        writer.Write((byte)(Flags & 0xFF));
        writer.Write((byte)(Questions.Count >> 8));
        writer.Write((byte)(Questions.Count & 0xFF));
        writer.Write((byte)(Answers.Count >> 8));
        writer.Write((byte)(Answers.Count & 0xFF));
        writer.Write((ushort)0); // Authority RRs
        writer.Write((ushort)0); // Additional RRs

        // Questions
        foreach (DnsQuestion question in Questions)
        {
            WriteName(writer, question.Name);
            writer.Write((byte)(question.Type >> 8));
            writer.Write((byte)(question.Type & 0xFF));
            writer.Write((byte)(question.Class >> 8));
            writer.Write((byte)(question.Class & 0xFF));
        }

        // Answers
        foreach (DnsResourceRecord answer in Answers)
        {
            WriteName(writer, answer.Name);
            writer.Write((byte)(answer.Type >> 8));
            writer.Write((byte)(answer.Type & 0xFF));
            writer.Write((byte)(answer.Class >> 8));
            writer.Write((byte)(answer.Class & 0xFF));
            writer.Write((byte)(answer.Ttl >> 24));
            writer.Write((byte)((answer.Ttl >> 16) & 0xFF));
            writer.Write((byte)((answer.Ttl >> 8) & 0xFF));
            writer.Write((byte)(answer.Ttl & 0xFF));

            byte[] data = answer.GetData();
            writer.Write((byte)(data.Length >> 8));
            writer.Write((byte)(data.Length & 0xFF));
            writer.Write(data);
        }

        return stream.ToArray();
    }

    private static string ReadName(byte[] data, ref int offset)
    {
        StringBuilder name = new StringBuilder();
        bool jumped = false;
        int originalOffset = offset;
        int maxJumps = 5;
        int jumps = 0;

        while (true)
        {
            if (offset >= data.Length)
            {
                break;
            }

            byte length = data[offset];

            if (length == 0)
            {
                offset++;
                break;
            }

            if ((length & 0xC0) == 0xC0)
            {
                if (!jumped)
                {
                    originalOffset = offset + 2;
                }

                if (jumps++ > maxJumps)
                {
                    throw new InvalidOperationException("Too many pointer jumps in DNS name");
                }

                int pointer = ((length & 0x3F) << 8) | data[offset + 1];
                offset = pointer;
                jumped = true;
                continue;
            }

            offset++;
            if (name.Length > 0)
            {
                name.Append('.');
            }

            for (int i = 0; i < length; i++)
            {
                name.Append((char)data[offset++]);
            }
        }

        if (jumped)
        {
            offset = originalOffset;
        }

        return name.ToString();
    }

    private static void WriteName(BinaryWriter writer, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            writer.Write((byte)0);
            return;
        }

        string[] labels = name.Split('.');
        foreach (string label in labels)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(label);
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }
        writer.Write((byte)0);
    }
}

/// <summary>
/// Represents a DNS question.
/// </summary>
public class DnsQuestion
{
    public string Name { get; set; } = string.Empty;
    public ushort Type { get; set; }
    public ushort Class { get; set; }
}

/// <summary>
/// Represents a DNS resource record.
/// </summary>
public class DnsResourceRecord
{
    public string Name { get; set; } = string.Empty;
    public ushort Type { get; set; }
    public ushort Class { get; set; } = 1; // IN
    public uint Ttl { get; set; }
    public object? Data { get; set; }

    public byte[] GetData()
    {
        if (Type == 1) // A record
        {
            if (Data is string ipString)
            {
                IPAddress address = IPAddress.Parse(ipString);
                return address.GetAddressBytes();
            }
        }
        else if (Type == 16) // TXT record
        {
            if (Data is string text)
            {
                byte[] textBytes = Encoding.ASCII.GetBytes(text);
                byte[] result = new byte[textBytes.Length + 1];
                result[0] = (byte)textBytes.Length;
                Array.Copy(textBytes, 0, result, 1, textBytes.Length);
                return result;
            }
        }

        return Array.Empty<byte>();
    }
}
