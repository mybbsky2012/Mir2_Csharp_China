using System.IO;

namespace ServerPackets;

public sealed class ChangePMode : Packet
{
	public PetMode Mode;

	public override short Index => 61;

	protected override void ReadPacket(BinaryReader reader)
	{
		Mode = (PetMode)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Mode);
	}
}
