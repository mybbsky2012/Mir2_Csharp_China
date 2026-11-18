using System;
using System.IO;

public class UserItem
{
	public ulong UniqueID;

	public int ItemIndex;

	public ItemInfo Info;

	public ushort CurrentDura;

	public ushort MaxDura;

	public ushort Count = 1;

	public ushort GemCount;

	public RefinedValue RefinedValue;

	public byte RefineAdded;

	public int RefineSuccessChance;

	public bool DuraChanged;

	public int SoulBoundId = -1;

	public bool Identified;

	public bool Cursed;

	public int WeddingRing = -1;

	public UserItem[] Slots = new UserItem[0];

	public DateTime BuybackExpiryDate;

	public ExpireInfo ExpireInfo;

	public RentalInformation RentalInformation;

	public SealedInfo SealedInfo;

	public bool IsShopItem;

	public Awake Awake = new Awake();

	public Stats AddedStats;

	public bool IsAdded
	{
		get
		{
			if (AddedStats.Count <= 0)
			{
				return Slots.Length > Info.Slots;
			}
			return true;
		}
	}

	public int Weight
	{
		get
		{
			if (Info.Type != ItemType.护身符 && Info.Type != ItemType.鱼饵)
			{
				return Info.Weight * Count;
			}
			return Info.Weight;
		}
	}

	public string FriendlyName
	{
		get
		{
			if (Count <= 1)
			{
				return Info.FriendlyName;
			}
			return $"{Info.FriendlyName} ({Count})";
		}
	}

	public bool GMMade { get; set; }

	public ushort Image
	{
		get
		{
			if (Info.Type == ItemType.护身符 && Info.StackSize > 0)
			{
				switch (Info.Shape)
				{
				case 0:
					if (Count >= 300)
					{
						return 3662;
					}
					if (Count >= 200)
					{
						return 3661;
					}
					_ = Count;
					_ = 100;
					return 3660;
				case 1:
					if (Count >= 150)
					{
						return 3675;
					}
					if (Count >= 100)
					{
						return 2960;
					}
					if (Count >= 50)
					{
						return 3674;
					}
					return 3673;
				case 2:
					if (Count >= 150)
					{
						return 3672;
					}
					if (Count >= 100)
					{
						return 2961;
					}
					if (Count >= 50)
					{
						return 3671;
					}
					return 3670;
				}
			}
			return Info.Image;
		}
	}

	public UserItem(ItemInfo info)
	{
		SoulBoundId = -1;
		ItemIndex = info.Index;
		Info = info;
		AddedStats = new Stats();
		SetSlotSize();
	}

	public UserItem(BinaryReader reader, int version = int.MaxValue, int customVersion = int.MaxValue)
	{
		UniqueID = reader.ReadUInt64();
		ItemIndex = reader.ReadInt32();
		CurrentDura = reader.ReadUInt16();
		MaxDura = reader.ReadUInt16();
		if (version <= 84)
		{
			Count = (ushort)reader.ReadUInt32();
		}
		else
		{
			Count = reader.ReadUInt16();
		}
		if (version <= 84)
		{
			AddedStats = new Stats();
			AddedStats[Stat.MaxAC] = reader.ReadByte();
			AddedStats[Stat.MaxMAC] = reader.ReadByte();
			AddedStats[Stat.MaxDC] = reader.ReadByte();
			AddedStats[Stat.MaxMC] = reader.ReadByte();
			AddedStats[Stat.MaxSC] = reader.ReadByte();
			AddedStats[Stat.准确] = reader.ReadByte();
			AddedStats[Stat.敏捷] = reader.ReadByte();
			AddedStats[Stat.HP] = reader.ReadByte();
			AddedStats[Stat.MP] = reader.ReadByte();
			AddedStats[Stat.攻击速度] = reader.ReadSByte();
			AddedStats[Stat.幸运] = reader.ReadSByte();
		}
		SoulBoundId = reader.ReadInt32();
		byte b = reader.ReadByte();
		Identified = (b & 1) == 1;
		Cursed = (b & 2) == 2;
		if (version <= 84)
		{
			AddedStats[Stat.强度] = reader.ReadByte();
			AddedStats[Stat.魔法躲避] = reader.ReadByte();
			AddedStats[Stat.毒物躲避] = reader.ReadByte();
			AddedStats[Stat.生命恢复] = reader.ReadByte();
			AddedStats[Stat.法力恢复] = reader.ReadByte();
			AddedStats[Stat.中毒恢复] = reader.ReadByte();
			AddedStats[Stat.暴击倍率] = reader.ReadByte();
			AddedStats[Stat.暴击伤害] = reader.ReadByte();
			AddedStats[Stat.冰冻伤害] = reader.ReadByte();
			AddedStats[Stat.毒素伤害] = reader.ReadByte();
		}
		int num = reader.ReadInt32();
		SetSlotSize(num);
		for (int i = 0; i < num; i++)
		{
			if (!reader.ReadBoolean())
			{
				UserItem userItem = new UserItem(reader, version, customVersion);
				Slots[i] = userItem;
			}
		}
		if (version <= 84)
		{
			GemCount = (ushort)reader.ReadUInt32();
		}
		else
		{
			GemCount = reader.ReadUInt16();
		}
		if (version > 84)
		{
			AddedStats = new Stats(reader, version, customVersion);
		}
		Awake = new Awake(reader);
		RefinedValue = (RefinedValue)reader.ReadByte();
		RefineAdded = reader.ReadByte();
		if (version > 85)
		{
			RefineSuccessChance = reader.ReadInt32();
		}
		WeddingRing = reader.ReadInt32();
		if (version < 65)
		{
			return;
		}
		if (reader.ReadBoolean())
		{
			ExpireInfo = new ExpireInfo(reader, version, customVersion);
		}
		if (version < 76)
		{
			return;
		}
		if (reader.ReadBoolean())
		{
			RentalInformation = new RentalInformation(reader, version, customVersion);
		}
		if (version < 83)
		{
			return;
		}
		IsShopItem = reader.ReadBoolean();
		if (version >= 92)
		{
			if (reader.ReadBoolean())
			{
				SealedInfo = new SealedInfo(reader, version, customVersion);
			}
			if (version > 107)
			{
				GMMade = reader.ReadBoolean();
			}
		}
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(ItemIndex);
		writer.Write(CurrentDura);
		writer.Write(MaxDura);
		writer.Write(Count);
		writer.Write(SoulBoundId);
		byte b = 0;
		if (Identified)
		{
			b = (byte)(b | 1u);
		}
		if (Cursed)
		{
			b = (byte)(b | 2u);
		}
		writer.Write(b);
		writer.Write(Slots.Length);
		for (int i = 0; i < Slots.Length; i++)
		{
			writer.Write(Slots[i] == null);
			if (Slots[i] != null)
			{
				Slots[i].Save(writer);
			}
		}
		writer.Write(GemCount);
		AddedStats.Save(writer);
		Awake.Save(writer);
		writer.Write((byte)RefinedValue);
		writer.Write(RefineAdded);
		writer.Write(RefineSuccessChance);
		writer.Write(WeddingRing);
		writer.Write(ExpireInfo != null);
		ExpireInfo?.Save(writer);
		writer.Write(RentalInformation != null);
		RentalInformation?.Save(writer);
		writer.Write(IsShopItem);
		writer.Write(SealedInfo != null);
		SealedInfo?.Save(writer);
		writer.Write(GMMade);
	}

	public int GetTotal(Stat type)
	{
		return AddedStats[type] + Info.Stats[type];
	}

	public uint Price()
	{
		if (Info == null)
		{
			return 0u;
		}
		uint num = Info.Price;
		if (Info.Durability > 0)
		{
			float num2 = (float)Info.Price / 2f / (float)(int)Info.Durability;
			num = (uint)((float)(int)MaxDura * num2);
			num2 = ((MaxDura <= 0) ? 0f : ((float)(int)CurrentDura / (float)(int)MaxDura));
			num = (uint)Math.Floor((float)num / 2f + (float)num / 2f * num2 + (float)Info.Price / 2f);
		}
		num = (uint)((float)num * ((float)AddedStats.Count * 0.1f + 1f));
		return num * Count;
	}

	public uint RepairPrice()
	{
		if (Info == null || Info.Durability == 0)
		{
			return 0u;
		}
		uint num = Info.Price;
		if (Info.Durability > 0)
		{
			num = (uint)Math.Floor((float)(int)MaxDura * ((float)Info.Price / 2f / (float)(int)Info.Durability) + (float)Info.Price / 2f);
			num = (uint)((float)num * ((float)AddedStats.Count * 0.1f + 1f));
		}
		uint num2 = num * Count - Price();
		if (RentalInformation == null)
		{
			return num2;
		}
		return num2 * 2;
	}

	public uint Quality()
	{
		return (uint)(AddedStats.Count + Awake.GetAwakeLevel() + 1);
	}

	public uint AwakeningPrice()
	{
		if (Info == null)
		{
			return 0u;
		}
		return (uint)((ulong)(1500u * (1 + Awake.GetAwakeLevel() * 2)) * (ulong)Info.Grade);
	}

	public uint DisassemblePrice()
	{
		if (Info == null)
		{
			return 0u;
		}
		return (uint)((float)(1500u * (uint)Info.Grade) * ((float)(AddedStats.Count + Awake.GetAwakeLevel()) * 0.1f + 1f));
	}

	public uint DowngradePrice()
	{
		if (Info == null)
		{
			return 0u;
		}
		return (uint)((ulong)(3000u * (1 + (Awake.GetAwakeLevel() + 1) * 2)) * (ulong)Info.Grade);
	}

	public uint ResetPrice()
	{
		if (Info == null)
		{
			return 0u;
		}
		return (uint)((float)(3000u * (uint)Info.Grade) * ((float)AddedStats.Count * 0.2f + 1f));
	}

	public void SetSlotSize(int? size = null)
	{
		if (!size.HasValue)
		{
			switch (Info.Type)
			{
			case ItemType.坐骑:
				if (Info.Shape < 7)
				{
					size = 4;
				}
				else if (Info.Shape < 13)
				{
					size = 5;
				}
				break;
			case ItemType.武器:
				if (Info.Shape == 49 || Info.Shape == 50)
				{
					size = 5;
				}
				break;
			}
		}
		if ((size.HasValue || Info != null) && (!size.HasValue || size != Slots.Length) && (size.HasValue || Info == null || Info.Slots != Slots.Length))
		{
			Array.Resize(ref Slots, size ?? Info.Slots);
		}
	}

	public UserItem Clone()
	{
		return new UserItem(Info)
		{
			UniqueID = UniqueID,
			CurrentDura = CurrentDura,
			MaxDura = MaxDura,
			Count = Count,
			GemCount = GemCount,
			DuraChanged = DuraChanged,
			SoulBoundId = SoulBoundId,
			Identified = Identified,
			Cursed = Cursed,
			Slots = Slots,
			AddedStats = new Stats(AddedStats),
			Awake = Awake,
			RefineAdded = RefineAdded,
			ExpireInfo = ExpireInfo,
			RentalInformation = RentalInformation,
			SealedInfo = SealedInfo,
			IsShopItem = IsShopItem,
			GMMade = GMMade
		};
	}
}
