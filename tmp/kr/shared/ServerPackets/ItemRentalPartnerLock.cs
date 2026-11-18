using System.IO;

namespace ServerPackets;

public sealed class ItemRentalPartnerLock : Packet
{
	public bool GoldLocked;

	public bool ItemLocked;

	public override short Index => 261;

	protected override void ReadPacket(BinaryReader reader)
	{
		GoldLocked = reader.ReadBoolean();
		ItemLocked = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(GoldLocked);
		writer.Write(ItemLocked);
	}
}
