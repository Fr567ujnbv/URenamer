using System;
using System.IO;
using System.Text;

namespace URenamer
{
	internal class FileReader : BinaryReader
	{
		internal FileReader(Stream input, EndianType endianType) : base(input, Encoding.UTF8, true)
		{
			Stream = input;
			EndianType = endianType;
		}

		internal bool TryReadStringNullTerm(out string result) => TryReadStringNullTerm(m_buffer.Length, out result);

		internal bool TryReadStringNullTerm(int length, out string result)
		{
			length = (int)Math.Min(Stream.Length, length);
			for (int i = 0; i < length; i++)
			{
				byte bt = ReadByte();
				if (bt == 0)
				{
					result = Encoding.UTF8.GetString(m_buffer, 0, i);
					return true;
				}
				m_buffer[i] = bt;
			}
			result = null;
			return false;
		}

		public Stream Stream { get; }
		public EndianType EndianType { get; }


		protected const int BufferSize = 4096;

		private readonly byte[] m_buffer = new byte[BufferSize];

		internal bool TryReadInt32(out int result)
		{
			int offset = 0;
			int count = 4;
			while (count > 0)
			{
				int read = Read(m_buffer, offset, count);
				if (read == 0)
				{
					result = 0;
					return false;
					//throw new Exception($"End of stream. Read {offset}, expected {count} bytes");
				}
				offset += read;
				count -= read;
			}
			result = EndianType == EndianType.LittleEndian ?
				(m_buffer[0] << 0) | (m_buffer[1] << 8) | (m_buffer[2] << 16) | (m_buffer[3] << 24) :
				(m_buffer[3] << 0) | (m_buffer[2] << 8) | (m_buffer[1] << 16) | (m_buffer[0] << 24);
			return true;
		}

		internal bool TryReadUInt64(out ulong result)
		{
			int offset = 0;
			int count = 8;
			while (count > 0)
			{
				int read = Read(m_buffer, offset, count);
				if (read == 0)
				{
					result = 0;
					return false;
					//throw new Exception($"End of stream. Read {offset}, expected {count} bytes");
				}
				offset += read;
				count -= read;
			}
			if (EndianType == EndianType.LittleEndian)
			{
				uint value1 = unchecked((uint)((m_buffer[0] << 0) | (m_buffer[1] << 8) | (m_buffer[2] << 16) | (m_buffer[3] << 24)));
				uint value2 = unchecked((uint)((m_buffer[4] << 0) | (m_buffer[5] << 8) | (m_buffer[6] << 16) | (m_buffer[7] << 24)));
				result = ((ulong)value1 << 0) | ((ulong)value2 << 32);
			}
			else
			{
				uint value1 = unchecked((uint)((m_buffer[7] << 0) | (m_buffer[6] << 8) | (m_buffer[5] << 16) | (m_buffer[4] << 24)));
				uint value2 = unchecked((uint)((m_buffer[3] << 0) | (m_buffer[2] << 8) | (m_buffer[1] << 16) | (m_buffer[0] << 24)));
				result = ((ulong)value1 << 0) | ((ulong)value2 << 32);
			}
			return true;
		}

		internal bool TryReadHash128(out Guid guid)
		{
			if (TryReadUInt32(out uint Data0) && TryReadUInt32(out uint Data1) && TryReadUInt32(out uint Data2) && TryReadUInt32(out uint Data3))
			{
				StringBuilder sb = new StringBuilder(32, 32);
				try
				{
					sb.Append(Data0.ToString("x8"));
					sb.Append(Data1.ToString("x8"));
					sb.Append(Data2.ToString("x8"));
					sb.Append(Data3.ToString("x8"));
					guid = new Guid(sb.ToString());
					if (!guid.Equals(new Guid()))
					{
#warning TODO: Need to test if bit order is correct
					}
					return true;
				}
				finally
				{
					sb.Clear();
				}
			}
			guid = Guid.Empty;
			return false;
			throw new NotImplementedException();
		}

		internal bool TryReadUInt16(out ushort result)
		{
			int offset = 0;
			int count = 2;
			while (count > 0)
			{
				int read = Read(m_buffer, offset, count);
				if (read == 0)
				{
					result = 0;
					return false;
					//throw new Exception($"End of stream. Read {offset}, expected {count} bytes");
				}
				offset += read;
				count -= read;
			}
			result = EndianType == EndianType.LittleEndian ?
				unchecked((ushort)((m_buffer[0] << 0) | (m_buffer[1] << 8))) :
				unchecked((ushort)((m_buffer[1] << 0) | (m_buffer[0] << 8)));
			return true;
		}

		internal bool TryReadUInt32(out uint result)
		{
			int offset = 0;
			int count = 4;
			while (count > 0)
			{
				int read = Read(m_buffer, offset, count);
				if (read == 0)
				{
					result = 0;
					return false;
					//throw new Exception($"End of stream. Read {offset}, expected {count} bytes");
				}
				offset += read;
				count -= read;
			}
			result = EndianType == EndianType.LittleEndian ?
				unchecked((uint)((m_buffer[0] << 0) | (m_buffer[1] << 8) | (m_buffer[2] << 16) | (m_buffer[3] << 24))) :
				unchecked((uint)((m_buffer[3] << 0) | (m_buffer[2] << 8) | (m_buffer[1] << 16) | (m_buffer[0] << 24)));
			return true;
		}
	}
}