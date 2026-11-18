using System.IO;

namespace ServerPackets;

public sealed class ChatItemStats : Packet
{
	public ulong ChatItemId;

	public UserItem Stats;

	public override short Index => 163;

	protected override void ReadPacket(BinaryReader reader)
	{
		ChatItemId = reader.ReadUInt64();
		Stats = new UserItem(reader);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ChatItemId);
		if (Stats != null)
		{
			Stats.Save(writer);
		}
	}
}
