using System.IO;

namespace ServerPackets;

public sealed class Chat : Packet
{
	public string Message = string.Empty;

	public ChatType Type;

	public override short Index => 30;

	public override bool Observable
	{
		get
		{
			if (Type != ChatType.WhisperIn)
			{
				return Type != ChatType.WhisperOut;
			}
			return false;
		}
	}

	protected override void ReadPacket(BinaryReader reader)
	{
		Message = reader.ReadString();
		Type = (ChatType)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Message);
		writer.Write((byte)Type);
	}
}
