using Server.MirEnvir;
using Server.MirObjects;

namespace Server.MirDatabase
{
    public class BuffInfo
    {
        public BuffType Type { get; set; }
        public BuffStackType StackType { get; set; }
        public BuffProperty Properties { get; set; }
        public int Icon { get; set; }
        public bool Visible { get; set; }

        /// <summary>
        /// 显示名（2026-10-12 新增）。只有 Configs\BuffSystem.ini 里配过的 BUFF 才有值，
        /// 随 AddBuff 封包下发给客户端；为空时客户端回退到枚举名，原有 BUFF 行为不变。
        /// </summary>
        public string Name = string.Empty;

        public static List<BuffInfo> Load()
        {
            List<BuffInfo> info = new List<BuffInfo>
            {
                //Magics
                new BuffInfo { Type = BuffType.时间之殇, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.隐身术, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.体迅风, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.轻身步, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.血龙剑法, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.幽灵盾, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.神圣战甲术, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.风身术, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.无极真气, Properties = BuffProperty.None, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.护身气幕, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.剑气爆, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.诅咒术, Properties = BuffProperty.RemoveOnDeath | BuffProperty.Debuff, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.月影术, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.烈火身, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.气流术, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.吸血地闪, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.毒魔闪, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.天务, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.精神状态, Properties = BuffProperty.None, StackType = BuffStackType.Infinite },
                new BuffInfo { Type = BuffType.先天气功, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.深延术, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.血龙水, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.金刚不坏, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.金刚不坏秘籍, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.天上秘术, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.魔法盾, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.金刚术, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration },

                //新增技能 Buff（新增：必须在此注册，否则 AddBuff 会抛 NotImplementedException）
                new BuffInfo { Type = BuffType.万效符, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.万效符秘笈, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.影化连击, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.灭杀窗口, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.月华乱舞减准, Properties = BuffProperty.Debuff | BuffProperty.RemoveOnDeath, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.万效灵魂火符, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.万效秘灵魂火符, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },

                //Monsters
                new BuffInfo { Type = BuffType.HornedArcherBuff, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.ColdArcherBuff, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.HornedColdArcherBuff, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.GeneralMeowMeowShield, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.惩戒真言, Properties = BuffProperty.Debuff, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.御体之力, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.HornedWarriorShield, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.ChieftainSwordBuff, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.HornedCommanderShield, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.失明状态, Properties = BuffProperty.RemoveOnDeath | BuffProperty.Debuff, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.寒冰护甲, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.ReaperPriestBuff, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.至尊威严, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.伤口加深, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.死亡印记, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.RiklebitesShield, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.麻痹状态, Properties = BuffProperty.Debuff, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.绝对封锁, Properties = BuffProperty.Debuff, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.Mon564NSealing, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.防御诅咒, Properties = BuffProperty.Debuff, StackType = BuffStackType.ResetDuration, Visible = true },
                new BuffInfo { Type = BuffType.烈火焚烧, Properties = BuffProperty.Debuff, StackType = BuffStackType.ResetDuration, Visible = true },

                //Special
                new BuffInfo { Type = BuffType.游戏管理, Properties = BuffProperty.None, StackType = BuffStackType.Infinite, Visible = Settings.GameMasterEffect },
                new BuffInfo { Type = BuffType.衣钵相传, Properties = BuffProperty.None, StackType = BuffStackType.Infinite },
                new BuffInfo { Type = BuffType.火传穷薪, Properties = BuffProperty.None, StackType = BuffStackType.Infinite },
                new BuffInfo { Type = BuffType.公会特效, Properties = BuffProperty.None, StackType = BuffStackType.Infinite },
                new BuffInfo { Type = BuffType.技巧项链, Properties = BuffProperty.None, StackType = BuffStackType.Infinite },
                new BuffInfo { Type = BuffType.隐身戒指, Properties = BuffProperty.None, StackType = BuffStackType.Infinite },
                new BuffInfo { Type = BuffType.变形效果, Properties = BuffProperty.None, StackType = BuffStackType.None },
                new BuffInfo { Type = BuffType.心心相映, Properties = BuffProperty.RemoveOnExit, StackType = BuffStackType.Infinite },
                new BuffInfo { Type = BuffType.精力充沛, Properties = BuffProperty.None, StackType = BuffStackType.ResetDuration },
                new BuffInfo { Type = BuffType.Prison, Properties = BuffProperty.None, StackType = BuffStackType.None }, //???
                new BuffInfo { Type = BuffType.General, Properties = BuffProperty.None, StackType = BuffStackType.None }, //???
                new BuffInfo { Type = BuffType.新人特效, Properties = BuffProperty.RemoveOnExit, StackType = BuffStackType.Infinite }, //???
                new BuffInfo { Type = BuffType.英雄灵气, Properties = BuffProperty.None, StackType = BuffStackType.Infinite },
                new BuffInfo { Type = BuffType.暗影侵袭, Properties = BuffProperty.RemoveOnExit, StackType = BuffStackType.Infinite },
                new BuffInfo { Type = BuffType.攻击型绝技, Properties = BuffProperty.None, StackType = BuffStackType.Infinite },
                new BuffInfo { Type = BuffType.防御型绝技, Properties = BuffProperty.None, StackType = BuffStackType.Infinite },
                new BuffInfo { Type = BuffType.技能型绝技, Properties = BuffProperty.None, StackType = BuffStackType.Infinite },
                new BuffInfo { Type = BuffType.共用型绝技, Properties = BuffProperty.None, StackType = BuffStackType.Infinite },

                //Stats
                new BuffInfo { Type = BuffType.获取经验提升, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.物品掉落提升, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.金币辉煌, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.StackDuration },
                new BuffInfo { Type = BuffType.背包负重提升, Properties = BuffProperty.None, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.攻击力提升, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.魔法力提升, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.道术力提升, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.攻击速度提升, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.生命值提升, Properties = BuffProperty.None, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.法力值提升, Properties = BuffProperty.None, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.防御提升, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.魔法防御提升, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.奇异药水, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.StackDuration },
                new BuffInfo { Type = BuffType.包容万斤, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.StackDuration },
                new BuffInfo { Type = BuffType.准确命中提升, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.敏捷躲避提升, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.技能经验提升, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.ResetStatAndDuration },
                new BuffInfo { Type = BuffType.龍之祝福, Properties = BuffProperty.PauseInSafeZone, StackType = BuffStackType.StackDuration },
                new BuffInfo { Type = BuffType.安息之气, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.None },
                new BuffInfo { Type = BuffType.远古气息, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.None },
                new BuffInfo { Type = BuffType.华丽雨光, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.None, Visible = true },
                new BuffInfo { Type = BuffType.龙之特效, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.AttrStackStat, Visible = true },
                new BuffInfo { Type = BuffType.龙的特效, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.AttrStackStat, Visible = true },
                new BuffInfo { Type = BuffType.增强组队, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.AttrStackStat, Visible = true },
                new BuffInfo { Type = BuffType.内尔族的灵药, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.None },
                new BuffInfo { Type = BuffType.天灵水, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.None },
                new BuffInfo { Type = BuffType.玉清水, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.None },
                new BuffInfo { Type = BuffType.甜筒HP, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.None },
                new BuffInfo { Type = BuffType.甜筒MP, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.None },
                new BuffInfo { Type = BuffType.摩鲁的赤色药剂, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.None },
                new BuffInfo { Type = BuffType.摩鲁的青色药剂, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.None },
                new BuffInfo { Type = BuffType.摩鲁的黄色药剂, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.None },
                new BuffInfo { Type = BuffType.古代宗师祝福, Properties = BuffProperty.RemoveOnDeath, StackType = BuffStackType.None },
                new BuffInfo { Type = BuffType.黄金宗师祝福, Properties = BuffProperty.None, StackType = BuffStackType.None }

            };

            //★ 合并自定义 BUFF 配置（Configs\BuffSystem.ini）
            //  两个作用：
            //   ① 注册自定义槽位 —— Envir.GetBuffInfo 找不到会抛 NotImplementedException，
            //      所以 Custom1~Custom24 必须先在这里落地，否则一加就崩。
            //   ② 覆盖已有 BUFF 的元信息 —— 只覆盖配置里「真写了」的字段，
            //      免得只加一条属性却把 Visible/Stack 改回默认值。
            BuffConfig.Load();

            for (int i = 0; i < BuffConfig.Defs.Count; i++)
            {
                BuffDef def = BuffConfig.Defs[i];

                BuffInfo target = null;

                for (int j = 0; j < info.Count; j++)
                {
                    if (info[j].Type != def.Type) continue;

                    target = info[j];
                    break;
                }

                if (target == null)
                {
                    // 非自定义节却没匹配到已有 BUFF：说明节名写错了，加载日志里已经报过，这里跳过
                    if (!def.IsCustom) continue;

                    target = new BuffInfo { Type = def.Type, StackType = def.Stack, Properties = def.Property };
                    info.Add(target);
                }

                if (def.HasName) target.Name = def.Name;
                if (def.HasIcon) target.Icon = def.Icon;
                if (def.HasVisible) target.Visible = def.Visible;
                if (def.HasStack) target.StackType = def.Stack;
                if (def.HasProperty) target.Properties = def.Property;
            }

            return info;
        }
    }

    public class Buff
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        private Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

        public BuffInfo Info;
        public MapObject Caster;
        public uint ObjectID;
        public long ExpireTime;

        public long LastTime, NextTime;

        public Stats Stats;

        public int[] Values;

        public bool FlagForRemoval;
        public bool Paused;

        public BuffType Type
        {
            get { return Info.Type; }
        }

        public BuffStackType StackType
        {
            get { return Info.StackType; }
        }

        public BuffProperty Properties
        {
            get { return Info.Properties; }
        }

        public Buff(BuffType type)
        {
            Info = Envir.GetBuffInfo(type);
            Stats = new Stats();
            Data = new Dictionary<string, object>();
        }

        public Buff(BinaryReader reader, int version, int customVersion)
        {
            var type = (BuffType)reader.ReadByte();

            Info = Envir.GetBuffInfo(type);

            Caster = null;

            if (version < 88)
            {
                var visible = reader.ReadBoolean();
            }

            ObjectID = reader.ReadUInt32();
            ExpireTime = reader.ReadInt64();

            if (version <= 84)
            {
                Values = new int[reader.ReadInt32()];

                for (int i = 0; i < Values.Length; i++)
                {
                    Values[i] = reader.ReadInt32();
                }

                if (version < 88)
                {
                    var infinite = reader.ReadBoolean();
                }

                Stats = new Stats();
                Data = new Dictionary<string, object>();
            }
            else
            {
                if (version < 88)
                {
                    var stackable = reader.ReadBoolean();
                }

                Values = new int[0];
                Stats = new Stats(reader, version, customVersion);
                Data = new Dictionary<string, object>();

                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    var key = reader.ReadString();
                    var length = reader.ReadInt32();

                    var array = new byte[length];

                    for (int j = 0; j < array.Length; j++)
                    {
                        array[j] = reader.ReadByte();
                    }

                    Data[key] = Functions.DeserializeFromBytes(array);
                }

                if (version > 86)
                {
                    count = reader.ReadInt32();

                    Values = new int[count];

                    for (int i = 0; i < count; i++)
                    {
                        Values[i] = reader.ReadInt32();
                    }
                }
            }
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(ObjectID);
            writer.Write(ExpireTime);

            Stats.Save(writer);

            writer.Write(Data.Count);

            foreach (KeyValuePair<string, object> pair in Data)
            {
                var bytes = Functions.SerializeToBytes(pair.Value);

                writer.Write(pair.Key);
                writer.Write(bytes.Length);

                for (int i = 0; i < bytes.Length; i++)
                {
                    writer.Write(bytes[i]);
                }
            }

            writer.Write(Values.Length);

            for (int i = 0; i < Values.Length; i++)
            {
                writer.Write(Values[i]);
            }
        }

        public T Get<T>(string key)
        {
            if (!Data.TryGetValue(key, out object result))
            {
                return default;
            }

            return (T)result;
        }

        public void Set(string key, object val)
        {
            Data[key] = val;
        }

        public ClientBuff ToClientBuff()
        {
            return new ClientBuff
            {
                Type = Type,
                Caster = Caster?.Name ?? "",
                ObjectID = ObjectID,
                Visible = Info.Visible,
                Infinite = StackType == BuffStackType.Infinite,
                Paused = Paused,
                ExpireTime = ExpireTime,
                Stats = new Stats(Stats),
                Values = Values,
                //★ 自定义 BUFF 的名字与图标随封包下发（Info 已经在 BuffInfo.Load 里被配置覆盖过）
                Name = Info.Name ?? string.Empty,
                Icon = Info.Icon
            };
        }
    }
}
