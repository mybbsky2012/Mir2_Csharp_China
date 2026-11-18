using System.IO;

namespace ServerPackets;

public sealed class ItemRentalLock : Packet
{
	public bool Success;

	public bool GoldLocked;

	public bool ItemLocked;

	public override short Index => 260;

	protected override void ReadPacket(BinaryReader reader)
	{
		Success = reader.ReadBoolean();
		GoldLocked = reader.ReadBoolean();
		ItemLocked = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Success);
		writer.Write(GoldLocked);
		writer.Write(ItemLocked);
	}
}
