using System.IO;

namespace ClientPackets;

public sealed class AddMemo : Packet
{
	public int CharacterIndex;

	public string Memo;

	public override short Index => 127;

	protected override void ReadPacket(BinaryReader reader)
	{
		CharacterIndex = reader.ReadInt32();
		Memo = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(CharacterIndex);
		writer.Write(Memo);
	}
}
