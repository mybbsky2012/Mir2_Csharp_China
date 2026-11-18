using System.IO;

namespace ClientPackets;

public sealed class RequestChatItem : Packet
{
	public ulong ChatItemID;

	public override short Index => 75;

	protected override void ReadPacket(BinaryReader reader)
	{
		ChatItemID = reader.ReadUInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ChatItemID);
	}
}
