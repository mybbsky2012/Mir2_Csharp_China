using System.IO;

namespace ServerPackets;

public sealed class NPCRepair : Packet
{
	public float Rate;

	public override short Index => 102;

	protected override void ReadPacket(BinaryReader reader)
	{
		Rate = reader.ReadSingle();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Rate);
	}
}
