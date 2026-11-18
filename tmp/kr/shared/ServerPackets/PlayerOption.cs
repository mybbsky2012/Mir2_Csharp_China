using System.IO;

namespace ServerPackets;

public sealed class PlayerOption : Packet
{
	public PlayerOptionType Option;

	public bool Allowed;

	public bool Value;

	public override short Index => 274;

	protected override void ReadPacket(BinaryReader reader)
	{
		Option = (PlayerOptionType)reader.ReadByte();
		Allowed = reader.ReadBoolean();
		Value = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Option);
		writer.Write(Allowed);
		writer.Write(Value);
	}
}
