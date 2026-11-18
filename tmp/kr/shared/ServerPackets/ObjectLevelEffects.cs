using System.IO;

namespace ServerPackets;

public sealed class ObjectLevelEffects : Packet
{
	public uint ObjectID;

	public LevelEffects LevelEffects;

	public override short Index => 219;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		LevelEffects = (LevelEffects)reader.ReadUInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write((ushort)LevelEffects);
	}
}
