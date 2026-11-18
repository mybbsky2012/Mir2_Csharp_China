using System.IO;

namespace ServerPackets;

public sealed class GroupMembersMap : Packet
{
	public string PlayerName = string.Empty;

	public string PlayerMap = string.Empty;

	public override short Index => 272;

	protected override void ReadPacket(BinaryReader reader)
	{
		PlayerName = reader.ReadString();
		PlayerMap = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(PlayerName);
		writer.Write(PlayerMap);
	}
}
