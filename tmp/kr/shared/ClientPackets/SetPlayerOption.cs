using System.IO;

namespace ClientPackets;

public sealed class SetPlayerOption : Packet
{
	public PlayerOptionType Option;

	public bool Value;

	public override short Index => 144;

	protected override void ReadPacket(BinaryReader reader)
	{
		Option = (PlayerOptionType)reader.ReadByte();
		Value = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Option);
		writer.Write(Value);
	}
}
