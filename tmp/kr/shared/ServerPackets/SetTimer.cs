using System.IO;

namespace ServerPackets;

public sealed class SetTimer : Packet
{
	public string Key;

	public byte Type;

	public int Seconds;

	public override short Index => 267;

	protected override void ReadPacket(BinaryReader reader)
	{
		Key = reader.ReadString();
		Type = reader.ReadByte();
		Seconds = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Key);
		writer.Write(Type);
		writer.Write(Seconds);
	}
}
