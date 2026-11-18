using System.IO;

namespace ClientPackets;

public sealed class GuildStorageGoldChange : Packet
{
	public byte Type;

	public uint Amount;

	public override short Index => 81;

	protected override void ReadPacket(BinaryReader reader)
	{
		Type = reader.ReadByte();
		Amount = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Type);
		writer.Write(Amount);
	}
}
