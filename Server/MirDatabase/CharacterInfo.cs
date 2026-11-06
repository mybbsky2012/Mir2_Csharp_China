using System.Drawing;
﻿using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects;

namespace Server.MirDatabase
{
    public class CharacterInfo
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        public int Index;
        public string Name;
        public ushort Level;
        public MirClass Class;
        public MirGender Gender;
        public byte Hair;
        public int GuildIndex = -1;

        public string CreationIP;
        public DateTime CreationDate;

        public bool Banned;
        public string BanReason = string.Empty;
        public DateTime ExpiryDate;

        public bool ChatBanned;
        public DateTime ChatBanExpiryDate;

        public string LastIP = string.Empty;
        public DateTime LastLogoutDate;
        public DateTime LastLoginDate;

        public bool Deleted;
        public DateTime DeleteDate;

        //Marriage
        public int Married = 0;
        public DateTime MarriedDate;

        //Mentor
        public int Mentor = 0;
        public DateTime MentorDate;
        public bool IsMentor;
        public long MentorExp = 0;

        //Location
        public int CurrentMapIndex;
        public Point CurrentLocation;
        public MirDirection Direction;
        public int BindMapIndex;
        public Point BindLocation;

        public int HP, MP;
        public long Experience;

        public AttackMode AMode;
        public PetMode PMode;
        public bool AllowGroup;
        public bool AllowTrade;
        public bool AllowObserve;

        public int PKPoints;

        public bool NewDay;

        public bool Thrusting, HalfMoon, CrossHalfMoon;
        public bool DoubleSlash;
        public byte MentalState;
        public byte MentalStateLvl;

        // 大背包：默认 86 格 = 腰带 6 格(索引 0~5) + 背包 80 格(索引 6~85)。
        // 客户端背包面板固定 8 列 x 10 行 = 80 个格子，ItemSlot 直接就是从 6 到 85 的数组索引，
        // 所以 86 是客户端能完整显示的物理上限，超过 86 会出现「放了东西但看不见」的黑格。
        public UserItem[] Inventory = new UserItem[86], Equipment = new UserItem[14], Trade = new UserItem[10], QuestInventory = new UserItem[40], Refine = new UserItem[16];
        public List<ItemRentalInformation> RentedItems = new List<ItemRentalInformation>();
        public List<ItemRentalInformation> RentedItemsToRemove = new List<ItemRentalInformation>();
        public bool HasRentedItem;
        public UserItem CurrentRefine = null;
        public long CollectTime = 0;
        public List<UserMagic> Magics = new List<UserMagic>();
        public List<PetInfo> Pets = new List<PetInfo>();
        public List<Buff> Buffs = new List<Buff>();
        public List<Poison> Poisons = new List<Poison>();
        public List<MailInfo> Mail = new List<MailInfo>();
        public List<FriendInfo> Friends = new List<FriendInfo>();

        public List<UserIntelligentCreature> IntelligentCreatures = new List<UserIntelligentCreature>();
        public int PearlCount;

        public List<QuestProgressInfo> CurrentQuests = new List<QuestProgressInfo>();
        public List<int> CompletedQuests = new List<int>();

        public bool[] Flags = new bool[Globals.FlagIndexCount];

        public AccountInfo AccountInfo;
        public PlayerObject Player;
        public MountInfo Mount;

        public Dictionary<int, int> GSpurchases = new Dictionary<int, int>();
        public int[] Rank = new int[2];//dont save this in db!(and dont send it to clients :p)
        
        public int MaximumHeroCount = 1;
        public HeroInfo[] Heroes;
        public int CurrentHeroIndex;
        public bool HeroSpawned;
        public HeroBehaviour HeroBehaviour;

        public CharacterInfo() { }

        public CharacterInfo(ClientPackets.NewCharacter p, MirConnection c)
        {
            Name = p.Name;
            Class = p.Class;
            Gender = p.Gender;
            Heroes = new HeroInfo[MaximumHeroCount];

            CreationIP = c.IPAddress;
            CreationDate = Envir.Now;
        }

        public CharacterInfo(BinaryReader reader, int version, int customVersion)
        {
            Load(reader, version, customVersion);            
        }

        public virtual void Load(BinaryReader reader, int version, int customVersion)
        {
            Index = reader.ReadInt32();
            Name = reader.ReadString();

            if (version < 62)
            {
                Level = (ushort)reader.ReadByte();
            }
            else
            {
                Level = reader.ReadUInt16();
            }

            Class = (MirClass)reader.ReadByte();
            Gender = (MirGender)reader.ReadByte();
            Hair = reader.ReadByte();

            CreationIP = reader.ReadString();
            CreationDate = DateTime.FromBinary(reader.ReadInt64());

            Banned = reader.ReadBoolean();
            BanReason = reader.ReadString();
            ExpiryDate = DateTime.FromBinary(reader.ReadInt64());

            LastIP = reader.ReadString();
            LastLogoutDate = DateTime.FromBinary(reader.ReadInt64());

            if (version > 81)
            {
                LastLoginDate = DateTime.FromBinary(reader.ReadInt64());
            }

            Deleted = reader.ReadBoolean();
            DeleteDate = DateTime.FromBinary(reader.ReadInt64());

            CurrentMapIndex = reader.ReadInt32();
            CurrentLocation = new Point(reader.ReadInt32(), reader.ReadInt32());
            Direction = (MirDirection)reader.ReadByte();
            BindMapIndex = reader.ReadInt32();
            BindLocation = new Point(reader.ReadInt32(), reader.ReadInt32());

            if (version <= 84)
            {
                HP = reader.ReadUInt16();
                MP = reader.ReadUInt16();
            }
            else
            {
                HP = reader.ReadInt32();
                MP = reader.ReadInt32();
            }

            Experience = reader.ReadInt64();

            AMode = (AttackMode)reader.ReadByte();
            PMode = (PetMode)reader.ReadByte();

            if (version > 34)
            {
                PKPoints = reader.ReadInt32();
            }

            int count = reader.ReadInt32();

            Array.Resize(ref Inventory, count);

            // 大背包：旧存档（46 / 54 / ... / 82 格）登录时在尾部补齐到 86 格。
            // 只做尾部扩容，已有物品的索引位置完全不变，不会丢失或错位。
            if (Inventory.Length < 86)
                Array.Resize(ref Inventory, 86);

            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadBoolean()) continue;
                UserItem item = new UserItem(reader, version, customVersion);
                if (Envir.BindItem(item) && i < Inventory.Length)
                {
                    Inventory[i] = item;
                }
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadBoolean()) continue;
                UserItem item = new UserItem(reader, version, customVersion);
                if (Envir.BindItem(item) && i < Equipment.Length)
                {
                    Equipment[i] = item;
                }
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadBoolean()) continue;
                UserItem item = new UserItem(reader, version, customVersion);
                if (Envir.BindItem(item) && i < QuestInventory.Length)
                {
                    QuestInventory[i] = item;
                }
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                UserMagic magic = new UserMagic(reader, version, customVersion);
                if (magic.Info == null) continue;

                magic.CastTime = int.MinValue;
                Magics.Add(magic);
            }

            Thrusting = reader.ReadBoolean();
            HalfMoon = reader.ReadBoolean();
            CrossHalfMoon = reader.ReadBoolean();
            DoubleSlash = reader.ReadBoolean();

            MentalState = reader.ReadByte();

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                Pets.Add(new PetInfo(reader, version, customVersion));
            }

            AllowGroup = reader.ReadBoolean();

            for (int i = 0; i < Globals.FlagIndexCount; i++)
            {
                Flags[i] = reader.ReadBoolean();
            }

            GuildIndex = reader.ReadInt32();

            AllowTrade = reader.ReadBoolean();
            if (version > 104)
                AllowObserve = reader.ReadBoolean();

            count = reader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                QuestProgressInfo quest = new QuestProgressInfo(reader, version, customVersion);
                if (Envir.BindQuest(quest))
                {
                    CurrentQuests.Add(quest);
                }
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                Buff buff = new Buff(reader, version, customVersion);

                Buffs.Add(buff);
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                Mail.Add(new MailInfo(reader, version, customVersion));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                UserIntelligentCreature creature = new UserIntelligentCreature(reader, version, customVersion);
                if (creature.Info == null) continue;
                IntelligentCreatures.Add(creature);
            }

            if (version == 45)
            {
                var old1 = (IntelligentCreatureType)reader.ReadByte();
                var old2 = reader.ReadBoolean();
            }

            PearlCount = reader.ReadInt32();

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                CompletedQuests.Add(reader.ReadInt32());
            }

            if (reader.ReadBoolean())
            {
                CurrentRefine = new UserItem(reader, version, customVersion);
            }

            if (CurrentRefine != null)
            {
                Envir.BindItem(CurrentRefine);
            }

            CollectTime = reader.ReadInt64();
            CollectTime += Envir.Time;

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                Friends.Add(new FriendInfo(reader, version, customVersion));
            }

            if (version > 75)
            {
                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    RentedItems.Add(new ItemRentalInformation(reader, version, customVersion));
                }

                HasRentedItem = reader.ReadBoolean();
            }

            Married = reader.ReadInt32();
            MarriedDate = DateTime.FromBinary(reader.ReadInt64());
            Mentor = reader.ReadInt32();
            MentorDate = DateTime.FromBinary(reader.ReadInt64());
            IsMentor = reader.ReadBoolean();
            MentorExp = reader.ReadInt64();

            if (version >= 63)
            {
                int logCount = reader.ReadInt32();

                for (int i = 0; i < logCount; i++)
                {
                    GSpurchases.Add(reader.ReadInt32(), reader.ReadInt32());
                }
            }

            if (version > 98)
            {
                MaximumHeroCount = reader.ReadInt32();
                Heroes = new HeroInfo[MaximumHeroCount];
                if (version > 102)
                {
                    for (int i = 0; i < MaximumHeroCount; i++)
                    {
                        int heroIndex = reader.ReadInt32();
                        if (heroIndex > 0)
                            Heroes[i] = Envir.GetHeroInfo(heroIndex);
                    }
                }
                else
                {
                    for (int i = 0; i < MaximumHeroCount; i++)
                        Heroes[i] = new HeroInfo(reader, version, customVersion);
                }

                if (version < 104) reader.ReadInt32();
                CurrentHeroIndex = reader.ReadInt32();
                HeroSpawned = reader.ReadBoolean();
            }
            else Heroes = new HeroInfo[MaximumHeroCount];

            if (version > 100)
                HeroBehaviour = (HeroBehaviour)reader.ReadByte();
        }

        public virtual void Save(BinaryWriter writer)
        {
            writer.Write(Index);
            writer.Write(Name);
            writer.Write(Level);
            writer.Write((byte) Class);
            writer.Write((byte) Gender);
            writer.Write(Hair);

            writer.Write(CreationIP);
            writer.Write(CreationDate.ToBinary());

            writer.Write(Banned);
            writer.Write(BanReason);
            writer.Write(ExpiryDate.ToBinary());

            writer.Write(LastIP);
            writer.Write(LastLogoutDate.ToBinary());
            writer.Write(LastLoginDate.ToBinary());

            writer.Write(Deleted);
            writer.Write(DeleteDate.ToBinary());

            writer.Write(CurrentMapIndex);
            writer.Write(CurrentLocation.X);
            writer.Write(CurrentLocation.Y);
            writer.Write((byte)Direction);
            writer.Write(BindMapIndex);
            writer.Write(BindLocation.X);
            writer.Write(BindLocation.Y);

            writer.Write(HP);
            writer.Write(MP);
            writer.Write(Experience);

            writer.Write((byte) AMode);
            writer.Write((byte) PMode);

            writer.Write(PKPoints);

            writer.Write(Inventory.Length);
            for (int i = 0; i < Inventory.Length; i++)
            {
                writer.Write(Inventory[i] != null);
                if (Inventory[i] == null) continue;

                Inventory[i].Save(writer);
            }

            writer.Write(Equipment.Length);
            for (int i = 0; i < Equipment.Length; i++)
            {
                writer.Write(Equipment[i] != null);
                if (Equipment[i] == null) continue;

                Equipment[i].Save(writer);
            }

            writer.Write(QuestInventory.Length);
            for (int i = 0; i < QuestInventory.Length; i++)
            {
                writer.Write(QuestInventory[i] != null);
                if (QuestInventory[i] == null) continue;

                QuestInventory[i].Save(writer);
            }

            writer.Write(Magics.Count);
            for (int i = 0; i < Magics.Count; i++)
            {
                Magics[i].Save(writer);
            }

            writer.Write(Thrusting);
            writer.Write(HalfMoon);
            writer.Write(CrossHalfMoon);
            writer.Write(DoubleSlash);
            writer.Write(MentalState);

            writer.Write(Pets.Count);
            for (int i = 0; i < Pets.Count; i++)
            {
                Pets[i].Save(writer);
            }

            writer.Write(AllowGroup);

            for (int i = 0; i < Flags.Length; i++)
            {
                writer.Write(Flags[i]);
            }

            writer.Write(GuildIndex);

            writer.Write(AllowTrade);
            writer.Write(AllowObserve);

            writer.Write(CurrentQuests.Count);
            for (int i = 0; i < CurrentQuests.Count; i++)
            {
                CurrentQuests[i].Save(writer);
            }

            writer.Write(Buffs.Count);
            for (int i = 0; i < Buffs.Count; i++)
            {
                Buffs[i].Save(writer);
            }

            writer.Write(Mail.Count);
            for (int i = 0; i < Mail.Count; i++)
            {
                Mail[i].Save(writer);
            }

            writer.Write(IntelligentCreatures.Count);
            for (int i = 0; i < IntelligentCreatures.Count; i++)
            {
                IntelligentCreatures[i].Save(writer);
            }

            writer.Write(PearlCount);

            writer.Write(CompletedQuests.Count);
            for (int i = 0; i < CompletedQuests.Count; i++)
            {
                writer.Write(CompletedQuests[i]);
            }


            writer.Write(CurrentRefine != null);
            if (CurrentRefine != null)
            {
                CurrentRefine.Save(writer);
            }

            if ((CollectTime - Envir.Time) < 0)
            {
                CollectTime = 0;
            }
            else
            {
                CollectTime -= Envir.Time;
            }

            writer.Write(CollectTime);

            //★11-06：这里原来写着
            //      writer.Write(Friends.Count);
            //      for (...) { if (Friends[i].Info == null) continue; Friends[i].Save(writer); }
            //  数量写的是全部，循环却跳过 Info==null 的条目 —— 好友一旦被归档（不在 CharacterList
            //  里，Info 懒加载查不到人返回 null），就会「声明 N 条、实际只写 N-1 条」，
            //  后续所有字段整体错位。存档没有长度前缀，读回来直接 EndOfStreamException，
            //  而且同账号后面的角色跟着一起坏 —— 表现就是「重启后角色全没了 / 服起不来」。
            //
            //  FriendInfo.Save 只写 Index/Blocked/Memo，根本不碰 Info，所以悬空条目照写即可：
            //  数量与条目天然一致，角色以后被 @RESTOREPLAYER 恢复（Index 不变）时好友关系还能自动接回。
            writer.Write(Friends.Count);
            for (int i = 0; i < Friends.Count; i++)
            {
                Friends[i].Save(writer);
            }

            writer.Write(RentedItems.Count);
            foreach (var rentedItemInformation in RentedItems)
            {
                rentedItemInformation.Save(writer);
            }

            writer.Write(HasRentedItem);

            writer.Write(Married);
            writer.Write(MarriedDate.ToBinary());
            writer.Write(Mentor);
            writer.Write(MentorDate.ToBinary());
            writer.Write(IsMentor);
            writer.Write(MentorExp);

            writer.Write(GSpurchases.Count);

            foreach (var item in GSpurchases)
            {
                writer.Write(item.Key);
                writer.Write(item.Value);
            }

            writer.Write(MaximumHeroCount);
            for (int i = 0; i < Heroes.Length; i++)
                writer.Write(Heroes[i] != null ? Heroes[i].Index : 0);            
            writer.Write(CurrentHeroIndex);
            writer.Write(HeroSpawned);
            writer.Write((byte)HeroBehaviour);
        }

        public SelectInfo ToSelectInfo()
        {
            return new SelectInfo
                {
                    Index = Index,
                    Name = Name,
                    Level = Level,
                    Class = Class,
                    Gender = Gender,
                    LastAccess = LastLogoutDate
                };
        }

        public bool CheckHasIntelligentCreature(IntelligentCreatureType petType)
        {
            for (int i = 0; i < IntelligentCreatures.Count; i++)
            {
                if (IntelligentCreatures[i].PetType == petType) return true;
            }

            return false;
        }
        public virtual int ResizeInventory()
        {
            if (Inventory.Length >= 86) return Inventory.Length;

            if (Inventory.Length == 46)
            {
                Array.Resize(ref Inventory, Inventory.Length + 8);
            }
            else
            {
                Array.Resize(ref Inventory, Inventory.Length + 4);
            }

            return Inventory.Length;
        }
    }

    public class PetInfo
    {
        public int MonsterIndex;
        public int HP;
        public uint Experience;
        public byte Level, MaxPetLevel;

        public long TameTime;

        public PetInfo(MonsterObject ob)
        {
            MonsterIndex = ob.Info.Index;
            HP = ob.HP;
            Experience = ob.PetExperience;
            Level = ob.PetLevel;
            MaxPetLevel = ob.MaxPetLevel;
        }

        public PetInfo(BinaryReader reader, int version, int customVersion)
        {
            MonsterIndex = reader.ReadInt32();
            if (MonsterIndex == 271) MonsterIndex = 275;

            if (version <= 84)
            {
                HP = (int)reader.ReadUInt32();
            }
            else
            {
                HP = reader.ReadInt32();
            }

            Experience = reader.ReadUInt32();
            Level = reader.ReadByte();
            MaxPetLevel = reader.ReadByte();
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(MonsterIndex);
            writer.Write(HP);
            writer.Write(Experience);
            writer.Write(Level);
            writer.Write(MaxPetLevel);
        }
    }

    public class MountInfo
    {
        public HumanObject Player;
        public short MountType = -1;

        public bool CanRide
        {
            get { return HasMount && Slots[(int)MountSlot.Saddle] != null; }
        }
        public bool CanMapRide
        {
            get { return HasMount && !Player.CurrentMap.Info.NoMount; }
        }
        public bool CanDungeonRide
        {
            get { return HasMount && CanMapRide && (!Player.CurrentMap.Info.NeedBridle || Slots[(int)MountSlot.Reins] != null); }
        }
        public bool CanAttack
        {
            get { return HasMount && Slots[(int)MountSlot.Bells] != null || !RidingMount; }
        }
        public bool SlowLoyalty
        {
            get { return HasMount && Slots[(int)MountSlot.Ribbon] != null; }
        }

        public bool HasMount
        {
            get { return Player.Info.Equipment[(int)EquipmentSlot.坐骑] != null; }
        }

        private bool RidingMount
        {
            get { return Player.RidingMount; }
            set { Player.RidingMount = value; }
        }

        public UserItem[] Slots
        {
            get { return Player.Info.Equipment[(int)EquipmentSlot.坐骑].Slots; }
        }


        public MountInfo(HumanObject ob)
        {
            Player = ob;
        }
    }

    public class FriendInfo
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        public int Index;

        private CharacterInfo _Info;
        public CharacterInfo Info
        {
            get 
            {
                if (_Info == null)
                {
                    _Info = Envir.GetCharacterInfo(Index);
                }

                return _Info;
            }
        }

        public bool Blocked;
        public string Memo;

        public FriendInfo(CharacterInfo info, bool blocked) 
        {
            Index = info.Index;
            Blocked = blocked;
            Memo = "";
        }

        public FriendInfo(BinaryReader reader, int version, int customVersion)
        {
            Index = reader.ReadInt32();
            Blocked = reader.ReadBoolean();
            Memo = reader.ReadString();
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(Index);
            writer.Write(Blocked);
            // Memo 若为 null，BinaryWriter 会写 -1，读回来 ReadString 会抛 IOException → 整个账号库读不完
            writer.Write(Memo ?? string.Empty);
        }

        public ClientFriend CreateClientFriend()
        {
            return new ClientFriend()
            {
                Index = Index,
                Name = Info.Name,
                Blocked = Blocked,
                Memo = Memo,
                Online = Info.Player != null && Info.Player.Node != null
            };
        }
    }
}
