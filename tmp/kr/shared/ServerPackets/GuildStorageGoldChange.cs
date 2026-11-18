using System.IO;

namespace ServerPackets;

public sealed class GuildStorageGoldChange : Packet
{
	public uint Amount;

	public byte Type;

	public string Name = string.Empty;

	public override short Index => 170;

	protected override void ReadPacket(BinaryReader reader)
	{
		Amount = reader.ReadUInt32();
		Type = reader.ReadByte();
		Name = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Amount);
		writer.Write(Type);
		writer.Write(Name);
	}
}
