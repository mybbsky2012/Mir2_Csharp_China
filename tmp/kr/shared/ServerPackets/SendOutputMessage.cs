using System.IO;

namespace ServerPackets;

public sealed class SendOutputMessage : Packet
{
	public string Message;

	public OutputMessageType Type;

	public override short Index => 221;

	protected override void ReadPacket(BinaryReader reader)
	{
		Message = reader.ReadString();
		Type = (OutputMessageType)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Message);
		writer.Write((byte)Type);
	}
}
