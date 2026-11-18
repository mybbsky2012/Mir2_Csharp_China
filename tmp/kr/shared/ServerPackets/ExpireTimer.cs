using System.IO;

namespace ServerPackets;

public sealed class ExpireTimer : Packet
{
	public string Key;

	public override short Index => 268;

	protected override void ReadPacket(BinaryReader reader)
	{
		Key = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Key);
	}
}
