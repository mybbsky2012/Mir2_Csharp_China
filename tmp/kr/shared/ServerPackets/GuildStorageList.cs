using System.IO;

namespace ServerPackets;

public sealed class GuildStorageList : Packet
{
	public GuildStorageItem[] Items;

	public override short Index => 172;

	protected override void ReadPacket(BinaryReader reader)
	{
		Items = new GuildStorageItem[reader.ReadInt32()];
		for (int i = 0; i < Items.Length; i++)
		{
			if (reader.ReadBoolean())
			{
				Items[i] = new GuildStorageItem(reader);
			}
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Items.Length);
		for (int i = 0; i < Items.Length; i++)
		{
			writer.Write(Items[i] != null);
			if (Items[i] != null)
			{
				Items[i].Save(writer);
			}
		}
	}
}
