using System.IO;

namespace ClientPackets;

public sealed class MailCost : Packet
{
	public uint Gold;

	public ulong[] ItemsIdx = new ulong[5];

	public bool Stamped;

	public override short Index => 120;

	protected override void ReadPacket(BinaryReader reader)
	{
		Gold = reader.ReadUInt32();
		for (int i = 0; i < 5; i++)
		{
			ItemsIdx[i] = reader.ReadUInt64();
		}
		Stamped = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Gold);
		for (int i = 0; i < 5; i++)
		{
			writer.Write(ItemsIdx[i]);
		}
		writer.Write(Stamped);
	}
}
