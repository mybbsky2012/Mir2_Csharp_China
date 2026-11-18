using System.IO;

public class GuildStorageItem
{
	public UserItem Item;

	public long UserId;

	public GuildStorageItem()
	{
	}

	public GuildStorageItem(BinaryReader reader)
	{
		Item = new UserItem(reader);
		UserId = reader.ReadInt64();
	}

	public void Save(BinaryWriter writer)
	{
		Item.Save(writer);
		writer.Write(UserId);
	}
}
