using System.IO;

namespace ClientPackets;

public sealed class CallNPC : Packet
{
	public uint ObjectID;

	public string Key = string.Empty;

	public override short Index => 47;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Key = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Key);
	}
}
