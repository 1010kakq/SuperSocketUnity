using System;
using System.Buffers;
using System.Text;
using System.Buffers.Text;

namespace SuperSocket.ProtoBase
{
    /// <summary>
    /// Provides utility extension methods for working with sequences and buffers.
    /// </summary>
    public static class Extensions
    {
        private static readonly ReadOnlyMemory<byte> CRLF = Encoding.ASCII.GetBytes("\r\n").AsMemory();

        /// <summary>
        /// Reads a string from the sequence reader using UTF-8 encoding.
        /// </summary>
        /// <param name="reader">The sequence reader.</param>
        /// <param name="length">The length of the string to read. If 0, reads the remaining length.</param>
        /// <returns>The decoded string.</returns>
        public static string ReadString(ref this SequenceReader<byte> reader, long length = 0)
        {
            return ReadString(ref reader, Encoding.UTF8, length);
        }

        /// <summary>
        /// Reads a string from the sequence reader using the specified encoding.
        /// </summary>
        /// <param name="reader">The sequence reader.</param>
        /// <param name="encoding">The encoding to use for decoding the string.</param>
        /// <param name="length">The length of the string to read. If 0, reads the remaining length.</param>
        /// <returns>The decoded string.</returns>
        public static string ReadString(ref this SequenceReader<byte> reader, Encoding encoding, long length = 0)
        {
            if (length == 0)
                length = reader.Remaining;

            var seq = reader.Sequence.Slice(reader.Consumed, length);

            try
            {
                return seq.GetString(encoding);
            }
            finally
            {
                reader.Advance(length);
            }
        }

        /// <summary>
        /// Attempts to read a 16-bit unsigned integer in big-endian format from the sequence reader.
        /// </summary>
        /// <param name="reader">The sequence reader.</param>
        /// <param name="value">The read value.</param>
        /// <returns><c>true</c> if the value was successfully read; otherwise, <c>false</c>.</returns>
        public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out ushort value)
        {
            value = 0;

            if (reader.Remaining < 2)
                return false;

            if (!reader.TryRead(out byte h))
                return false;

            if (!reader.TryRead(out byte l))
                return false;

            value = (ushort)((h << 8) | l);
            return true;
        }

        /// <summary>
        /// Attempts to read a 32-bit unsigned integer in big-endian format from the sequence reader.
        /// </summary>
        /// <param name="reader">The sequence reader.</param>
        /// <param name="value">The read value.</param>
        /// <returns><c>true</c> if the value was successfully read; otherwise, <c>false</c>.</returns>
        public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out uint value)
        {
            value = 0;

            if (reader.Remaining < 4)
                return false;

            if (!reader.TryRead(out byte b0))
                return false;

            if (!reader.TryRead(out byte b1))
                return false;

            if (!reader.TryRead(out byte b2))
                return false;

            if (!reader.TryRead(out byte b3))
                return false;

            value = (uint)((b0 << 24) | (b1 << 16) | (b2 << 8) | b3);
            return true;
        }

        /// <summary>
        /// Attempts to read a 64-bit unsigned integer in big-endian format from the sequence reader.
        /// </summary>
        /// <param name="reader">The sequence reader.</param>
        /// <param name="value">The read value.</param>
        /// <returns><c>true</c> if the value was successfully read; otherwise, <c>false</c>.</returns>
        public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out ulong value)
        {
            value = 0;

            if (reader.Remaining < 8)
                return false;

            if (!reader.TryRead(out byte b0))
                return false;

            if (!reader.TryRead(out byte b1))
                return false;

            if (!reader.TryRead(out byte b2))
                return false;

            if (!reader.TryRead(out byte b3))
                return false;

            if (!reader.TryRead(out byte b4))
                return false;

            if (!reader.TryRead(out byte b5))
                return false;

            if (!reader.TryRead(out byte b6))
                return false;

            if (!reader.TryRead(out byte b7))
                return false;

            value = ((ulong)b0 << 56) | ((ulong)b1 << 48) | ((ulong)b2 << 40) | ((ulong)b3 << 32) |
                    ((ulong)b4 << 24) | ((ulong)b5 << 16) | ((ulong)b6 << 8) | b7;
            return true;
        }

        /// <summary>
        /// Attempts to read a 16-bit unsigned integer in little-endian format from the sequence reader.
        /// </summary>
        /// <param name="reader">The sequence reader.</param>
        /// <param name="value">The read value.</param>
        /// <returns><c>true</c> if the value was successfully read; otherwise, <c>false</c>.</returns>
        public static bool TryReadLittleEndian(ref this SequenceReader<byte> reader, out ushort value)
        {
            value = 0;

            if (reader.Remaining < 2)
                return false;

            if (!reader.TryRead(out byte l))
                return false;

            if (!reader.TryRead(out byte h))
                return false;

            value = (ushort)((h << 8) | l);
            return true;
        }

        /// <summary>
        /// Attempts to read a 32-bit unsigned integer in little-endian format from the sequence reader.
        /// </summary>
        /// <param name="reader">The sequence reader.</param>
        /// <param name="value">The read value.</param>
        /// <returns><c>true</c> if the value was successfully read; otherwise, <c>false</c>.</returns>
        public static bool TryReadLittleEndian(ref this SequenceReader<byte> reader, out uint value)
        {
            value = 0;

            if (reader.Remaining < 4)
                return false;

            if (!reader.TryRead(out byte b0))
                return false;

            if (!reader.TryRead(out byte b1))
                return false;

            if (!reader.TryRead(out byte b2))
                return false;

            if (!reader.TryRead(out byte b3))
                return false;

            value = (uint)((b3 << 24) | (b2 << 16) | (b1 << 8) | b0);
            return true;
        }

        /// <summary>
        /// Attempts to read a 64-bit unsigned integer in little-endian format from the sequence reader.
        /// </summary>
        /// <param name="reader">The sequence reader.</param>
        /// <param name="value">The read value.</param>
        /// <returns><c>true</c> if the value was successfully read; otherwise, <c>false</c>.</returns>
        public static bool TryReadLittleEndian(ref this SequenceReader<byte> reader, out ulong value)
        {
            value = 0;

            if (reader.Remaining < 8)
                return false;

            if (!reader.TryRead(out byte b0))
                return false;

            if (!reader.TryRead(out byte b1))
                return false;

            if (!reader.TryRead(out byte b2))
                return false;

            if (!reader.TryRead(out byte b3))
                return false;

            if (!reader.TryRead(out byte b4))
                return false;

            if (!reader.TryRead(out byte b5))
                return false;

            if (!reader.TryRead(out byte b6))
                return false;

            if (!reader.TryRead(out byte b7))
                return false;

            value = ((ulong)b7 << 56) | ((ulong)b6 << 48) | ((ulong)b5 << 40) | ((ulong)b4 << 32) |
                    ((ulong)b3 << 24) | ((ulong)b2 << 16) | ((ulong)b1 << 8) | b0;
            return true;
        }

        /// <summary>
        /// Converts a read-only sequence of bytes to a string using the specified encoding.
        /// </summary>
        /// <param name="buffer">The read-only sequence of bytes.</param>
        /// <param name="encoding">The encoding to use for decoding the string.</param>
        /// <returns>The decoded string.</returns>
        public static string GetString(this ReadOnlySequence<byte> buffer, Encoding encoding)
        {
            if (buffer.IsSingleSegment)
            {
                return encoding.GetString(buffer.First.Span);
            }

            if (encoding.IsSingleByte)
            {
                return string.Create((int)buffer.Length, buffer, (span, sequence) =>
                {
                    foreach (var segment in sequence)
                    {
                        var count = encoding.GetChars(segment.Span, span);
                        span = span.Slice(count);
                    }
                });
            }

            var sb = new StringBuilder();
            var decoder = encoding.GetDecoder();

            foreach (var piece in buffer)
            {
                var charBuff = (new char[piece.Length]).AsSpan();
                var len = decoder.GetChars(piece.Span, charBuff, false);
                sb.Append(new string(len == charBuff.Length ? charBuff : charBuff.Slice(0, len)));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Writes the specified text to the buffer writer using the specified encoding.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="text">The text to write.</param>
        /// <param name="encoding">The encoding to use for encoding the text.</param>
        /// <returns>The total number of bytes written to the buffer writer.</returns>
        public static int Write(this IBufferWriter<byte> writer, ReadOnlySpan<char> text, Encoding encoding)
        {
            var encoder = encoding.GetEncoder();
            var completed = false;
            var totalBytes = 0;

            var minSpanSizeHint = encoding.GetMaxByteCount(1);

            while (!completed)
            {
                var span = writer.GetSpan(minSpanSizeHint);

                encoder.Convert(text, span, false, out int charsUsed, out int bytesUsed, out completed);

                if (charsUsed > 0)
                    text = text.Slice(charsUsed);

                totalBytes += bytesUsed;
                writer.Advance(bytesUsed);
            }

            return totalBytes;
        }

        /// <summary>
        /// Writes a CRLF (Carriage Return + Line Feed) sequence to the buffer writer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <returns>The total number of bytes written to the buffer writer.</returns>
        public static int WriteCrLf(this IBufferWriter<byte> writer)
        {
            writer.Write(CRLF.Span);
            return CRLF.Length;
        }

        /// <summary>
        /// Writes a 16-bit unsigned integer in little-endian format to the buffer writer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <returns>The number of bytes written.</returns>
        public static int WriteLittleEndian(this IBufferWriter<byte> writer, ushort value)
        {
            var span = writer.GetSpan(2);
            span[0] = (byte)(value & 0xFF);
            span[1] = (byte)((value >> 8) & 0xFF);
            writer.Advance(2);
            return 2;
        }

        /// <summary>
        /// Writes a 32-bit unsigned integer in little-endian format to the buffer writer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <returns>The number of bytes written.</returns>
        public static int WriteLittleEndian(this IBufferWriter<byte> writer, uint value)
        {
            var span = writer.GetSpan(4);
            span[0] = (byte)(value & 0xFF);
            span[1] = (byte)((value >> 8) & 0xFF);
            span[2] = (byte)((value >> 16) & 0xFF);
            span[3] = (byte)((value >> 24) & 0xFF);
            writer.Advance(4);
            return 4;
        }

        /// <summary>
        /// Writes a 32-bit signed integer in little-endian format to the buffer writer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <returns>The number of bytes written.</returns>
        public static int WriteLittleEndian(this IBufferWriter<byte> writer, int value)
        {
            return WriteLittleEndian(writer, (uint)value);
        }

        /// <summary>
        /// Writes a 64-bit unsigned integer in little-endian format to the buffer writer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <returns>The number of bytes written.</returns>
        public static int WriteLittleEndian(this IBufferWriter<byte> writer, ulong value)
        {
            var span = writer.GetSpan(8);
            span[0] = (byte)(value & 0xFF);
            span[1] = (byte)((value >> 8) & 0xFF);
            span[2] = (byte)((value >> 16) & 0xFF);
            span[3] = (byte)((value >> 24) & 0xFF);
            span[4] = (byte)((value >> 32) & 0xFF);
            span[5] = (byte)((value >> 40) & 0xFF);
            span[6] = (byte)((value >> 48) & 0xFF);
            span[7] = (byte)((value >> 56) & 0xFF);
            writer.Advance(8);
            return 8;
        }

        /// <summary>
        /// Writes a 64-bit signed integer in little-endian format to the buffer writer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <returns>The number of bytes written.</returns>
        public static int WriteLittleEndian(this IBufferWriter<byte> writer, long value)
        {
            return WriteLittleEndian(writer, (ulong)value);
        }

        /// <summary>
        /// Writes a byte array to the buffer writer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="data">The byte array to write.</param>
        /// <returns>The number of bytes written.</returns>
        public static int Write(this IBufferWriter<byte> writer, byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;
                
            writer.Write(data.AsSpan());
            return data.Length;
        }

        /// <summary>
        /// Writes a byte array to the buffer writer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="data">The byte array to write.</param>
        /// <param name="offset">The offset in the array.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <returns>The number of bytes written.</returns>
        public static int Write(this IBufferWriter<byte> writer, byte[] data, int offset, int count)
        {
            if (data == null || count <= 0)
                return 0;
                
            writer.Write(data.AsSpan(offset, count));
            return count;
        }

        /// <summary>
        /// Attempts to read a 16-bit signed integer in little-endian format from the sequence reader.
        /// </summary>
        /// <param name="reader">The sequence reader.</param>
        /// <param name="value">The read value.</param>
        /// <returns><c>true</c> if the value was successfully read; otherwise, <c>false</c>.</returns>
        public static bool TryReadLittleEndian(ref this SequenceReader<byte> reader, out short value)
        {
            if (TryReadLittleEndian(ref reader, out ushort uvalue))
            {
                value = (short)uvalue;
                return true;
            }
            value = 0;
            return false;
        }

        /// <summary>
        /// Attempts to read a 32-bit signed integer in little-endian format from the sequence reader.
        /// </summary>
        /// <param name="reader">The sequence reader.</param>
        /// <param name="value">The read value.</param>
        /// <returns><c>true</c> if the value was successfully read; otherwise, <c>false</c>.</returns>
        public static bool TryReadLittleEndian(ref this SequenceReader<byte> reader, out int value)
        {
            if (TryReadLittleEndian(ref reader, out uint uvalue))
            {
                value = (int)uvalue;
                return true;
            }
            value = 0;
            return false;
        }

        /// <summary>
        /// Attempts to read a 64-bit signed integer in little-endian format from the sequence reader.
        /// </summary>
        /// <param name="reader">The sequence reader.</param>
        /// <param name="value">The read value.</param>
        /// <returns><c>true</c> if the value was successfully read; otherwise, <c>false</c>.</returns>
        public static bool TryReadLittleEndian(ref this SequenceReader<byte> reader, out long value)
        {
            if (TryReadLittleEndian(ref reader, out ulong uvalue))
            {
                value = (long)uvalue;
                return true;
            }
            value = 0;
            return false;
        }

        /// <summary>
        /// Attempts to read a byte array from the sequence reader.
        /// </summary>
        /// <param name="reader">The sequence reader.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <param name="data">The read data.</param>
        /// <returns><c>true</c> if the data was successfully read; otherwise, <c>false</c>.</returns>
        public static bool TryReadBytes(ref this SequenceReader<byte> reader, int count, out byte[] data)
        {
            data = null;
            
            if (reader.Remaining < count)
                return false;
                
            data = new byte[count];
            var span = data.AsSpan();
            
            if (!reader.TryCopyTo(span))
                return false;
                
            reader.Advance(count);
            return true;
        }
    }
}
