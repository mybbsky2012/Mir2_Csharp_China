using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ServerPackets;

public class UserInformation : Packet
{
	public uint ObjectID;

	public uint RealId;

	public string Name = string.Empty;

	public string GuildName = string.Empty;

	public string GuildRank = string.Empty;

	public Color NameColour;

	public MirClass Class;

	public MirGender Gender;

	public ushort Level;

	public Point Location;

	public MirDirection Direction;

	public byte Hair;

	public int HP;

	public int MP;

	public long Experience;

	public long MaxExperience;

	public LevelEffects LevelEffects;

	public bool HasHero;

	public HeroBehaviour HeroBehaviour;

	public UserItem[] Inventory;

	public UserItem[] Equipment;

	public UserItem[] QuestInventory;

	public uint Gold;

	public uint Credit;

	public bool HasExpandedStorage;

	public DateTime ExpandedStorageExpiryTime;

	public List<ClientMagic> Magics = new List<ClientMagic>();

	public List<ClientIntelligentCreature> IntelligentCreatures = new List<ClientIntelligentCreature>();

	public IntelligentCreatureType SummonedCreatureType = IntelligentCreatureType.None;

	public bool CreatureSummoned;

	public bool AllowObserve;

	public bool Observer;

	public override short Index => 21;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		RealId = reader.ReadUInt32();
		Name = reader.ReadString();
		GuildName = reader.ReadString();
		GuildRank = reader.ReadString();
		NameColour = Color.FromArgb(reader.ReadInt32());
		Class = (MirClass)reader.ReadByte();
		Gender = (MirGender)reader.ReadByte();
		Level = reader.ReadUInt16();
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		Direction = (MirDirection)reader.ReadByte();
		Hair = reader.ReadByte();
		HP = reader.ReadInt32();
		MP = reader.ReadInt32();
		Experience = reader.ReadInt64();
		MaxExperience = reader.ReadInt64();
		LevelEffects = (LevelEffects)reader.ReadUInt16();
		HasHero = reader.ReadBoolean();
		HeroBehaviour = (HeroBehaviour)reader.ReadByte();
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
		if (reader.ReadBoolean())
		{
			QuestInventory = new UserItem[reader.ReadInt32()];
			for (int k = 0; k < QuestInventory.Length; k++)
			{
				if (reader.ReadBoolean())
				{
					QuestInventory[k] = new UserItem(reader);
				}
			}
		}
		Gold = reader.ReadUInt32();
		Credit = reader.ReadUInt32();
		HasExpandedStorage = reader.ReadBoolean();
		ExpandedStorageExpiryTime = DateTime.FromBinary(reader.ReadInt64());
		int num = reader.ReadInt32();
		for (int l = 0; l < num; l++)
		{
			Magics.Add(new ClientMagic(reader));
		}
		num = reader.ReadInt32();
		for (int m = 0; m < num; m++)
		{
			IntelligentCreatures.Add(new ClientIntelligentCreature(reader));
		}
		SummonedCreatureType = (IntelligentCreatureType)reader.ReadByte();
		CreatureSummoned = reader.ReadBoolean();
		AllowObserve = reader.ReadBoolean();
		Observer = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(RealId);
		writer.Write(Name);
		writer.Write(GuildName);
		writer.Write(GuildRank);
		writer.Write(NameColour.ToArgb());
		writer.Write((byte)Class);
		writer.Write((byte)Gender);
		writer.Write(Level);
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write((byte)Direction);
		writer.Write(Hair);
		writer.Write(HP);
		writer.Write(MP);
		writer.Write(Experience);
		writer.Write(MaxExperience);
		writer.Write((ushort)LevelEffects);
		writer.Write(HasHero);
		writer.Write((byte)HeroBehaviour);
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
		writer.Write(QuestInventory != null);
		if (QuestInventory != null)
		{
			writer.Write(QuestInventory.Length);
			for (int k = 0; k < QuestInventory.Length; k++)
			{
				writer.Write(QuestInventory[k] != null);
				if (QuestInventory[k] != null)
				{
					QuestInventory[k].Save(writer);
				}
			}
		}
		writer.Write(Gold);
		writer.Write(Credit);
		writer.Write(HasExpandedStorage);
		writer.Write(ExpandedStorageExpiryTime.ToBinary());
		writer.Write(Magics.Count);
		for (int l = 0; l < Magics.Count; l++)
		{
			Magics[l].Save(writer);
		}
		writer.Write(IntelligentCreatures.Count);
		for (int m = 0; m < IntelligentCreatures.Count; m++)
		{
			IntelligentCreatures[m].Save(writer);
		}
		writer.Write((byte)SummonedCreatureType);
		writer.Write(CreatureSummoned);
		writer.Write(AllowObserve);
		writer.Write(Observer);
	}
}
