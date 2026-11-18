using System.IO;

namespace ClientPackets;

public sealed class TeleportToNPC : Packet
{
	public uint ObjectID;

	public override short Index => 37;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
	}
}
