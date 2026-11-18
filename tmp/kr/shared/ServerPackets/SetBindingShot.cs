using System.IO;

namespace ServerPackets;

public sealed class SetBindingShot : Packet
{
	public uint ObjectID;

	public bool Enabled;

	public long Value;

	public override short Index => 220;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Enabled = reader.ReadBoolean();
		Value = reader.ReadInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Enabled);
		writer.Write(Value);
	}
}
