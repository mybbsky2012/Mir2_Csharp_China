using System.IO;

namespace ServerPackets;

public sealed class Struck : Packet
{
	public uint AttackerID;

	public override short Index => 71;

	protected override void ReadPacket(BinaryReader reader)
	{
		AttackerID = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(AttackerID);
	}
}
