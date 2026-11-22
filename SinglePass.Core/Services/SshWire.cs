using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace SinglePass.Core.Services
{
    /// <summary>Reads the SSH wire encoding (RFC 4251): big-endian integers and length-prefixed strings.</summary>
    internal sealed class SshReader(byte[] data, int position = 0)
    {
        private int _position = position;

        public int Remaining => data.Length - _position;

        public byte ReadByte()
        {
            if (Remaining < 1)
                throw new EndOfStreamException();

            return data[_position++];
        }

        public uint ReadUInt32()
        {
            if (Remaining < 4)
                throw new EndOfStreamException();

            var value = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(_position));
            _position += 4;
            return value;
        }

        public byte[] ReadBytes()
        {
            var length = ReadUInt32();
            return ReadRaw(length);
        }

        /// <summary>Reads <paramref name="length"/> bytes that have no length prefix.</summary>
        public byte[] ReadRaw(uint length)
        {
            if (length > Remaining)
                throw new EndOfStreamException();

            var value = data.AsSpan(_position, (int)length).ToArray();
            _position += (int)length;
            return value;
        }

        public string ReadString() => Encoding.ASCII.GetString(ReadBytes());
    }

    /// <summary>Writes the SSH wire encoding (RFC 4251).</summary>
    internal sealed class SshWriter
    {
        private readonly MemoryStream _stream = new();

        public void WriteByte(byte value) => _stream.WriteByte(value);

        public void WriteUInt32(uint value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            _stream.Write(buffer);
        }

        public void WriteBytes(ReadOnlySpan<byte> value)
        {
            WriteUInt32((uint)value.Length);
            _stream.Write(value);
        }

        /// <summary>Writes bytes without a length prefix.</summary>
        public void WriteRaw(ReadOnlySpan<byte> value) => _stream.Write(value);

        public void WriteString(string value) => WriteBytes(Encoding.ASCII.GetBytes(value));

        /// <summary>Unsigned big-endian integer: no leading zeros, plus one zero byte if the top bit is set.</summary>
        public void WriteMpint(byte[] value)
        {
            var span = value.AsSpan().TrimStart((byte)0);
            if (span.Length > 0 && (span[0] & 0x80) != 0)
            {
                var padded = new byte[span.Length + 1];
                span.CopyTo(padded.AsSpan(1));
                WriteBytes(padded);
            }
            else
                WriteBytes(span);
        }

        public byte[] ToArray() => _stream.ToArray();
    }
}
