using System.IO;

namespace ClientPackets;

public sealed class GuildBuffUpdate : Packet
{
	public byte Action;

	public int Id;

	public override short Index => 128;

	protected override void ReadPacket(BinaryReader reader)
	{
		Action = reader.ReadByte();
		Id = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Action);
		writer.Write(Id);
	}
}
