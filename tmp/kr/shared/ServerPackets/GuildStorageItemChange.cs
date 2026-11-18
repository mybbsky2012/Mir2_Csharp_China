using System.IO;

namespace ServerPackets;

public sealed class GuildStorageItemChange : Packet
{
	public int User;

	public byte Type;

	public int To;

	public int From;

	public GuildStorageItem Item;

	public override short Index => 171;

	protected override void ReadPacket(BinaryReader reader)
	{
		Type = reader.ReadByte();
		To = reader.ReadInt32();
		From = reader.ReadInt32();
		User = reader.ReadInt32();
		if (reader.ReadBoolean())
		{
			Item = new GuildStorageItem
			{
				UserId = reader.ReadInt64(),
				Item = new UserItem(reader)
			};
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Type);
		writer.Write(To);
		writer.Write(From);
		writer.Write(User);
		writer.Write(Item != null);
		if (Item != null)
		{
			writer.Write(Item.UserId);
			Item.Item.Save(writer);
		}
	}
}
