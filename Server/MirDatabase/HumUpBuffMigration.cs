using System.Collections.Generic;
using Server.MirEnvir;

namespace Server.MirDatabase
{
    /// <summary>
    /// 羽化 BUFF 编码迁移（2026-11-12）：旧的自定义槽位 → 专用枚举。
    ///
    ///   旧：Custom1(230) = 纯标记，Custom3~Custom12(232~241) = 一重~十重
    ///   新：羽化登仙(159) = 纯标记，羽化一重~十重(160~169)
    ///
    /// ★只改 buff.Info 指针，绝不 new Buff：
    ///   Buff.Type 来自 Info，新建对象必须手工搬 Stats/ExpireTime/Values/Data/ObjectID，
    ///   漏一个就是「属性白板」——而 ApplyBuffConfigStats 对「非自定义类型 + 已存在」是直接 return 的，
    ///   登录重放不会补回来，等于永久少属性。改指针则这些字段一个都不会动。
    ///
    /// 幂等：新编码再跑一遍不会命中任何分支，重复执行安全。
    ///
    /// 这里的计数只服务于「升级工具出报告」——服务端读档时本来就会自动迁移，
    /// 计数为 0 说明这一份存档已经是新编码了。
    /// </summary>
    public static class HumUpBuffMigration
    {
        /// <summary>本次加载里被改写的 BUFF 条数（0 = 无需迁移）。</summary>
        public static int BuffsRemapped;

        /// <summary>被改写的角色/英雄数。</summary>
        public static int OwnersTouched;

        /// <summary>迁移后按类型去重掉的重复条数。</summary>
        public static int DuplicatesRemoved;

        /// <summary>明细（最多保留 300 条，防止大服刷爆内存）。</summary>
        public static readonly List<string> Details = new List<string>();

        private const int MaxDetails = 300;

        public static void Reset()
        {
            BuffsRemapped = 0;
            OwnersTouched = 0;
            DuplicatesRemoved = 0;
            Details.Clear();
        }

        /// <summary>是否还有旧编码待迁移（读档后调用）。</summary>
        public static bool Pending
        {
            get { return BuffsRemapped > 0 || DuplicatesRemoved > 0; }
        }

        public static void Apply(List<Buff> buffs, string owner)
        {
            if (buffs == null || buffs.Count == 0) return;

            int touched = 0;

            for (int i = 0; i < buffs.Count; i++)
            {
                Buff buff = buffs[i];
                if (buff == null) continue;

                int type = (int)buff.Type;

                // 旧：Custom1(230) → 羽化登仙(159，零属性)；232~241 → 羽化一重~十重。
                //★纯标记绝不能映射到「一重」：一重在 ini 里是 +5DC/+5MC/+5SC/+3AC/+3MAC/+100HP/+100MP，
                //  映射成一重等于给老号凭空加属性。
                BuffType target = BuffConfig.MapLegacyHumUp((BuffType)type);

                if ((int)target == type) continue;

                try
                {
                    buff.Info = Envir.Main.GetBuffInfo(target);
                }
                catch (System.NotImplementedException)
                {
                    // BUFF 定义还没灌进来（工具里裸读存档、或 ini 缺节）：
                    // 保留原样，等真正加载时再迁，别在这里把读档打断
                    continue;
                }

                buff.UnknownType = false;

                touched++;
                BuffsRemapped++;

                if (Details.Count < MaxDetails)
                    Details.Add(string.Format("{0}: {1} → {2}", owner, type, (int)target));
            }

            if (touched > 0) OwnersTouched++;

            // 去重：同一类型只留一条，否则客户端 BUFF 栏会画两个相同图标
            HashSet<int> seen = new HashSet<int>();

            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                Buff buff = buffs[i];
                if (buff == null) continue;

                int type = (int)buff.Type;
                if (!CharacterInfo.IsHumUpBuffType(type)) continue;

                if (!seen.Add(type))
                {
                    buffs.RemoveAt(i);
                    DuplicatesRemoved++;
                }
            }
        }

        /// <summary>一行摘要，给日志/工具用。</summary>
        public static string Describe()
        {
            if (!Pending) return "羽化 BUFF 编码：无需迁移（已是 159~169）";

            return string.Format("羽化 BUFF 编码：迁移 {0} 条 / {1} 个角色{2}",
                BuffsRemapped, OwnersTouched,
                DuplicatesRemoved > 0 ? string.Format("，去重 {0} 条", DuplicatesRemoved) : "");
        }
    }
}
