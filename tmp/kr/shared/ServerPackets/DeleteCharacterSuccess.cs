using System.IO;

namespace ServerPackets;

public sealed class DeleteCharacterSuccess : Packet
{
	public int CharacterIndex;

	public override short Index => 13;

	protected override void ReadPacket(BinaryReader reader)
	{
		CharacterIndex = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(CharacterIndex);
	}
}
