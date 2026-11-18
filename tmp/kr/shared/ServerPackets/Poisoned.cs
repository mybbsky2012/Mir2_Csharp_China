using System.IO;

namespace ServerPackets;

public sealed class Poisoned : Packet
{
	public PoisonType Poison;

	public override short Index => 94;

	protected override void ReadPacket(BinaryReader reader)
	{
		Poison = (PoisonType)reader.ReadUInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((ushort)Poison);
	}
}
