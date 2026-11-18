using System.IO;

namespace ServerPackets;

public sealed class ObjectSneaking : Packet
{
	public uint ObjectID;

	public bool SneakingActive;

	public override short Index => 218;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		SneakingActive = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(SneakingActive);
	}
}
