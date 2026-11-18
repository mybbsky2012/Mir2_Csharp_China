using System.IO;

namespace ServerPackets;

public sealed class UpdateRentalItem : Packet
{
	public bool HasData;

	public UserItem LoanItem;

	public override short Index => 258;

	protected override void ReadPacket(BinaryReader reader)
	{
		HasData = reader.ReadBoolean();
		if (HasData)
		{
			LoanItem = new UserItem(reader);
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(LoanItem != null);
		if (LoanItem != null)
		{
			LoanItem.Save(writer);
		}
	}
}
