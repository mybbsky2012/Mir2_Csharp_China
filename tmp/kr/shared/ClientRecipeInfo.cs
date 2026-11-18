using System.Collections.Generic;
using System.IO;

public class ClientRecipeInfo
{
	public uint Gold;

	public byte Chance;

	public UserItem Item;

	public List<UserItem> Tools = new List<UserItem>();

	public List<UserItem> Ingredients = new List<UserItem>();

	public ClientRecipeInfo()
	{
	}

	public ClientRecipeInfo(BinaryReader reader)
	{
		Gold = reader.ReadUInt32();
		Chance = reader.ReadByte();
		Item = new UserItem(reader);
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Tools.Add(new UserItem(reader));
		}
		num = reader.ReadInt32();
		for (int j = 0; j < num; j++)
		{
			Ingredients.Add(new UserItem(reader));
		}
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(Gold);
		writer.Write(Chance);
		Item.Save(writer);
		writer.Write(Tools.Count);
		foreach (UserItem tool in Tools)
		{
			tool.Save(writer);
		}
		writer.Write(Ingredients.Count);
		foreach (UserItem ingredient in Ingredients)
		{
			ingredient.Save(writer);
		}
	}
}
