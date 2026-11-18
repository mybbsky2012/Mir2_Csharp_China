using System.IO;

namespace ClientPackets;

public sealed class DropGold : Packet
{
	public uint Amount;

	public override short Index => 34;

	protected override void ReadPacket(BinaryReader reader)
	{
		Amount = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Amount);
	}
}
