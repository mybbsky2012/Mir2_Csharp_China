using System.IO;

namespace ServerPackets;

public sealed class NPCSRepair : Packet
{
	public float Rate;

	public override short Index => 103;

	protected override void ReadPacket(BinaryReader reader)
	{
		Rate = reader.ReadSingle();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Rate);
	}
}
