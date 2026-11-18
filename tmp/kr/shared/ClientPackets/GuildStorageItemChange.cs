using System.IO;

namespace ClientPackets;

public sealed class GuildStorageItemChange : Packet
{
	public byte Type;

	public int From;

	public int To;

	public override short Index => 82;

	protected override void ReadPacket(BinaryReader reader)
	{
		Type = reader.ReadByte();
		From = reader.ReadInt32();
		To = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Type);
		writer.Write(From);
		writer.Write(To);
	}
}
