using System.IO;

namespace ServerPackets;

public sealed class UserSlotsRefresh : Packet
{
	public UserItem[] Inventory;

	public UserItem[] Equipment;

	public override short Index => 22;

	protected override void ReadPacket(BinaryReader reader)
	{
		if (reader.ReadBoolean())
		{
			Inventory = new UserItem[reader.ReadInt32()];
			for (int i = 0; i < Inventory.Length; i++)
			{
				if (reader.ReadBoolean())
				{
					Inventory[i] = new UserItem(reader);
				}
			}
		}
		if (!reader.ReadBoolean())
		{
			return;
		}
		Equipment = new UserItem[reader.ReadInt32()];
		for (int j = 0; j < Equipment.Length; j++)
		{
			if (reader.ReadBoolean())
			{
				Equipment[j] = new UserItem(reader);
			}
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Inventory != null);
		if (Inventory != null)
		{
			writer.Write(Inventory.Length);
			for (int i = 0; i < Inventory.Length; i++)
			{
				writer.Write(Inventory[i] != null);
				if (Inventory[i] != null)
				{
					Inventory[i].Save(writer);
				}
			}
		}
		writer.Write(Equipment != null);
		if (Equipment == null)
		{
			return;
		}
		writer.Write(Equipment.Length);
		for (int j = 0; j < Equipment.Length; j++)
		{
			writer.Write(Equipment[j] != null);
			if (Equipment[j] != null)
			{
				Equipment[j].Save(writer);
			}
		}
	}
}
