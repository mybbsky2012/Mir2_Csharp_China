using Server.MirEnvir;

namespace Server.MirDatabase
{
    public class HeroInfo : CharacterInfo
    {
        public bool AutoPot;
        public byte Grade;
        public int HPItemIndex;
        public int MPItemIndex;
        public byte AutoHPPercent;
        public byte AutoMPPercent;
        public ushort SealCount;
        public HeroInfo(ClientPackets.NewHero p)
        {
            Name = p.Name;
            Class = p.Class;
            Gender = p.Gender;

            HP = -1;

            Inventory = new UserItem[10];

            CreationDate = Envir.Now;
        }

        public HeroInfo(BinaryReader reader, int version, int customVersion) : base(reader, version, customVersion) { }

        public override void Load(BinaryReader reader, int version, int customVersion)
        {
            Index = reader.ReadInt32();
            Name = reader.ReadString();
            Level = reader.ReadUInt16(); 
            Class = (MirClass) reader.ReadByte();
            Gender = (MirGender) reader.ReadByte();
            Hair = reader.ReadByte();

            CreationDate = DateTime.FromBinary(reader.ReadInt64());

            Deleted = reader.ReadBoolean();
            DeleteDate = DateTime.FromBinary(reader.ReadInt64());

            HP = reader.ReadInt32();
            MP = reader.ReadInt32();            

            Experience = reader.ReadInt64();

            int count = reader.ReadInt32();

            Array.Resize(ref Inventory, count);

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
                UserMagic magic = new UserMagic(reader, version, customVersion);
                if (magic.Info == null) continue;

                magic.CastTime = int.MinValue;
                Magics.Add(magic);
            }  
            
            if (version > 99)
            {
                AutoPot = reader.ReadBoolean();
                Grade = reader.ReadByte();
                HPItemIndex = reader.ReadInt32();
                MPItemIndex = reader.ReadInt32();
                AutoHPPercent = reader.ReadByte();
                AutoMPPercent = reader.ReadByte();
            }

            if (version > 101)
                SealCount = reader.ReadUInt16();

            //★英雄的持久化 BUFF（药剂/脚本给的时效属性，比如破坏强化水的 5 小时）。
            //  原来这里没有这一段，英雄喝药加的 BUFF 从来没进过库 —— 一退出游戏/一重启服务端就没了。
            //  version 111 起才有：老档（<=110）读到 SealCount 就结束，不会错位。
            if (version > 110)
            {
                count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    Buffs.Add(new Buff(reader, version, customVersion));
                }
            }
        }

        public override void Save(BinaryWriter writer)
        {
            writer.Write(Index);
            writer.Write(Name);
            writer.Write(Level);
            writer.Write((byte) Class);
            writer.Write((byte) Gender);
            writer.Write(Hair);

            writer.Write(CreationDate.ToBinary());

            writer.Write(Deleted);
            writer.Write(DeleteDate.ToBinary());

            writer.Write(HP);
            writer.Write(MP);
            writer.Write(Experience);

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

            writer.Write(Magics.Count);
            for (int i = 0; i < Magics.Count; i++)
            {
                Magics[i].Save(writer);
            }

            writer.Write(AutoPot);
            writer.Write(Grade);
            writer.Write(HPItemIndex);
            writer.Write(MPItemIndex);
            writer.Write(AutoHPPercent);
            writer.Write(AutoMPPercent);
            writer.Write(SealCount);

            //★与 HeroInfo.Load 的 version > 110 段对应，两者必须一起改（见 Envir.Version）
            //  条数按「真正写得出去的」算：Buff.Save 里 Values/Stats 是直接 .Length/.Save 的，
            //  手工 new 出来的 Buff（脚本/测试直接往列表里塞）这两项可能是 null，
            //  一抛异常整个 SaveAccounts 就废了（全服存不了档）。这里就地补空，绝不跳过条目 ——
            //  跳过会让条数和实际写入不一致，读回来直接错位。
            int buffCount = 0;
            for (int i = 0; i < (Buffs?.Count ?? 0); i++)
                if (Buffs[i] != null) buffCount++;

            writer.Write(buffCount);

            for (int i = 0; i < (Buffs?.Count ?? 0); i++)
            {
                Buff buff = Buffs[i];
                if (buff == null) continue;

                buff.Values ??= new int[0];
                buff.Stats ??= new Stats();

                buff.Save(writer);
            }
        }

        public override int ResizeInventory()
        {
            if (Inventory.Length >= 42) return Inventory.Length;
            Array.Resize(ref Inventory, Inventory.Length + 8);
            return Inventory.Length;
        }

        public ClientHeroInformation ClientInformation
        {
            get
            {
                return new ClientHeroInformation()
                {
                    Index = Index,
                    Name = Name,
                    Level = Math.Max((ushort)1, Level),
                    Class = Class,
                    Gender = Gender
                };
            }
        }
    }
}
