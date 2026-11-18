using System.IO;

namespace ClientPackets;

public sealed class StartGame : Packet
{
	public int CharacterIndex;

	public override short Index => 8;

	protected override void ReadPacket(BinaryReader reader)
	{
		CharacterIndex = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(CharacterIndex);
	}
}
