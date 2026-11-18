using System.IO;

namespace ServerPackets;

public sealed class MountUpdate : Packet
{
	public long ObjectID;

	public short MountType;

	public bool RidingMount;

	public override short Index => 196;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadInt64();
		MountType = reader.ReadInt16();
		RidingMount = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(MountType);
		writer.Write(RidingMount);
	}
}
