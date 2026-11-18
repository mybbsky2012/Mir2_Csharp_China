using System.IO;

namespace ServerPackets;

public sealed class ObjectGuildNameChanged : Packet
{
	public uint ObjectID;

	public string GuildName;

	public override short Index => 82;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		GuildName = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(GuildName);
	}
}
