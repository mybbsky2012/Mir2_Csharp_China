using System.IO;

namespace ServerPackets;

public sealed class ItemRentalRequest : Packet
{
	public string Name;

	public bool Renting;

	public override short Index => 253;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
		Renting = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
		writer.Write(Renting);
	}
}
