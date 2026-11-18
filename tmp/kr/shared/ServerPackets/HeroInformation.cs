using System.IO;

namespace ServerPackets;

public sealed class HeroInformation : UserInformation
{
	public bool AutoPot;

	public byte AutoHPPercent;

	public byte AutoMPPercent;

	public int HPItemIndex;

	public int MPItemIndex;

	public override short Index => 176;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Name = reader.ReadString();
		Class = (MirClass)reader.ReadByte();
		Gender = (MirGender)reader.ReadByte();
		Level = reader.ReadUInt16();
		Hair = reader.ReadByte();
		HP = reader.ReadInt32();
		MP = reader.ReadInt32();
		Experience = reader.ReadInt64();
		MaxExperience = reader.ReadInt64();
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
		if (reader.ReadBoolean())
		{
			Equipment = new UserItem[reader.ReadInt32()];
			for (int j = 0; j < Equipment.Length; j++)
			{
				if (reader.ReadBoolean())
				{
					Equipment[j] = new UserItem(reader);
				}
			}
		}
		int num = reader.ReadInt32();
		for (int k = 0; k < num; k++)
		{
			Magics.Add(new ClientMagic(reader));
		}
		AutoPot = reader.ReadBoolean();
		AutoHPPercent = reader.ReadByte();
		AutoMPPercent = reader.ReadByte();
		HPItemIndex = reader.ReadInt32();
		MPItemIndex = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Name);
		writer.Write((byte)Class);
		writer.Write((byte)Gender);
		writer.Write(Level);
		writer.Write(Hair);
		writer.Write(HP);
		writer.Write(MP);
		writer.Write(Experience);
		writer.Write(MaxExperience);
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
		if (Equipment != null)
		{
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
		writer.Write(Magics.Count);
		for (int k = 0; k < Magics.Count; k++)
		{
			Magics[k].Save(writer);
		}
		writer.Write(AutoPot);
		writer.Write(AutoHPPercent);
		writer.Write(AutoMPPercent);
		writer.Write(HPItemIndex);
		writer.Write(MPItemIndex);
	}
}
