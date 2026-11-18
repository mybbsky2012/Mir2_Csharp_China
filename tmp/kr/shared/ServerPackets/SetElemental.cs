using System.IO;

namespace ServerPackets;

public sealed class SetElemental : Packet
{
	public uint ObjectID;

	public bool Enabled;

	public bool Casted;

	public uint Value;

	public uint ElementType;

	public uint ExpLast;

	public override short Index => 215;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Enabled = reader.ReadBoolean();
		Casted = reader.ReadBoolean();
		Value = reader.ReadUInt32();
		ElementType = reader.ReadUInt32();
		ExpLast = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Enabled);
		writer.Write(Casted);
		writer.Write(Value);
		writer.Write(ElementType);
		writer.Write(ExpLast);
	}
}
