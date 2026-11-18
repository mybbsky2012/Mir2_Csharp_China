using System.IO;

namespace ClientPackets;

public sealed class SetAutoPotValue : Packet
{
	public Stat Stat;

	public uint Value;

	public override short Index => 61;

	protected override void ReadPacket(BinaryReader reader)
	{
		Stat = (Stat)reader.ReadByte();
		Value = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Stat);
		writer.Write(Value);
	}
}
