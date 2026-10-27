using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Server.MirDatabase;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.MirObjects
{
    /// <summary>
    /// 「0 持久残留」物品清理（10-27）。
    ///
    /// 背景：物品定义 ItemInfo.Durability 被改过之后，已经生成的实例不会回填，
    /// 于是会出现「持久 0/0 却永远占着格子」的死物品。服务端原本只有两处会删物品
    /// （HumanObject.RemoveStone、PlayerObject.UseItem 的 Shape 20/21 扣到 0），
    /// 其它任何物品持久归零都不会自己消失。
    ///
    /// 策略（安全第一）：
    ///  ★自动清理（登录 / 召唤英雄）只动「0 持久必然用不了」的白名单：
    ///     守护石 / 气血石 / 魔血石、卷轴 Shape 20(回城石) 21(随机传送石)
    ///  ★祝福油、修复油这类「0 持久照样能用」的物品绝不自动删，
    ///    只在 @清理废品 的清单里列出来给 GM 看；
    ///  ★只扫背包，不动身上装备；
    ///  ★每次删除都追加写 Logs\ZeroDuraClean.log。
    /// </summary>
    public static class ZeroDuraCleaner
    {
        public sealed class Entry
        {
            public HumanObject Owner;
            public int Slot;
            public UserItem Item;
            public bool Safe;
            public bool IsHero;

            public string Describe()
            {
                return string.Format("{0}#{1} {2} 持久 {3}/{4} uid={5}{6}",
                    IsHero ? "英雄背包" : "背包",
                    Slot,
                    Item.Info != null ? Item.Info.Name : "?",
                    Item.CurrentDura,
                    Item.MaxDura,
                    Item.UniqueID,
                    Safe ? " [安全]" : " [需人工]");
            }
        }

        /// <summary>「定义有持久、实例却已经 0 持久」——才算残留。定义本来没持久的药水/书/材料不算。</summary>
        public static bool IsZeroDura(UserItem item)
        {
            if (item == null || item.Info == null) return false;
            if (item.Info.Durability <= 0) return false;
            return item.CurrentDura == 0;
        }

        /// <summary>0 持久之后必然失效、可以放心删的物品。</summary>
        public static bool IsSafeZeroDura(UserItem item)
        {
            if (!IsZeroDura(item)) return false;

            switch (item.Info.Type)
            {
                case ItemType.守护石:
                case ItemType.气血石:
                case ItemType.魔血石:
                    return true;
                case ItemType.卷轴:
                    return item.Info.Shape == 20 || item.Info.Shape == 21;
            }

            return false;
        }

        /// <summary>
        /// 「整理背包」时判定：0 持久就可以直接扔掉的**可使用类**物品。
        /// 注意**不含装备**（武器/盔甲/首饰/坐骑）—— 那些 0 持久只是坏了，修理还能用，绝不能删。
        /// </summary>
        public static bool IsDisposableZeroDura(UserItem item)
        {
            if (!IsZeroDura(item)) return false;

            switch (item.Info.Type)
            {
                case ItemType.守护石:
                case ItemType.气血石:
                case ItemType.魔血石:
                case ItemType.卷轴:
                case ItemType.特殊消耗品:
                    return true;
            }

            return false;
        }

        /// <summary>扫描一个角色（玩家或英雄）的背包，返回所有 0 持久物品。</summary>
        public static List<Entry> Scan(HumanObject ob, bool isHero = false)
        {
            List<Entry> list = new List<Entry>();
            if (ob == null || ob.Info == null || ob.Info.Inventory == null) return list;

            for (int i = 0; i < ob.Info.Inventory.Length; i++)
            {
                UserItem item = ob.Info.Inventory[i];
                if (!IsZeroDura(item)) continue;

                list.Add(new Entry
                {
                    Owner = ob,
                    Slot = i,
                    Item = item,
                    Safe = IsSafeZeroDura(item),
                    IsHero = isHero,
                });
            }

            return list;
        }

        public static List<Entry> OnlySafe(List<Entry> list)
        {
            List<Entry> result = new List<Entry>();
            if (list == null) return result;

            for (int i = 0; i < list.Count; i++)
                if (list[i].Safe) result.Add(list[i]);

            return result;
        }

        /// <summary>真正删除。逐件写日志；notify=true 时给客户端补 DeleteItem（登录时背包快照还没发，不需要）。</summary>
        public static int Delete(List<Entry> entries, bool notify, string reason)
        {
            if (entries == null || entries.Count == 0) return 0;

            int removed = 0;
            Dictionary<HumanObject, int> byOwner = new Dictionary<HumanObject, int>();

            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e == null || e.Item == null || e.Owner == null) continue;

                HumanObject ob = e.Owner;
                if (ob.Info == null || ob.Info.Inventory == null) continue;
                if (e.Slot < 0 || e.Slot >= ob.Info.Inventory.Length) continue;
                // 期间被卖掉 / 挪走 / 换过物品：按引用校验，避免误删
                if (!ReferenceEquals(ob.Info.Inventory[e.Slot], e.Item)) continue;

                ob.Info.Inventory[e.Slot] = null;

                if (notify)
                    ob.Enqueue(new S.DeleteItem { UniqueID = e.Item.UniqueID, Count = e.Item.Count });

                WriteLog(string.Format("{0}\t角色={1}\t{2}\t持久={3}/{4}\tuid={5}\t原因={6}",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ob.Name,
                    e.Describe(),
                    e.Item.CurrentDura,
                    e.Item.MaxDura,
                    e.Item.UniqueID,
                    reason));

                if (!byOwner.ContainsKey(ob)) byOwner[ob] = 0;
                byOwner[ob]++;
                removed++;
            }

            foreach (KeyValuePair<HumanObject, int> pair in byOwner)
            {
                HumanObject ob = pair.Key;
                int count = pair.Value;

                ob.RefreshBagWeight();

                if (ob is HeroObject hero && hero.Owner != null)
                    hero.Owner.ReceiveChat(string.Format("[英雄: {0}] 清理了 {1} 件持久为 0 的物品", hero.Name, count), ChatType.Hint);
                else
                    ob.ReceiveChat(string.Format("清理了 {0} 件持久为 0 的物品", count), ChatType.Hint);
            }

            return removed;
        }

        /// <summary>
        /// 整理背包时调用：背包（含腰带）里 0 持久的**可使用类**物品直接删掉。
        /// 比自动清理宽（连 0 持久的祝福油/修复油也清），但**绝不动装备** —— 坏掉的装备还能修。
        /// </summary>
        public static int CleanAllZeroDura(HumanObject ob, string reason)
        {
            List<Entry> list = Scan(ob);
            if (list.Count == 0) return 0;

            List<Entry> kill = new List<Entry>();
            for (int i = 0; i < list.Count; i++)
                if (IsDisposableZeroDura(list[i].Item)) kill.Add(list[i]);

            if (kill.Count == 0) return 0;

            WriteList("整理背包时清理", kill);
            return Delete(kill, true, reason);
        }

        /// <summary>
        /// 登录 / 召唤英雄时的自动清理。只动安全白名单。
        /// 玩家登录时传 notify=false（背包快照还没下发）；英雄召唤时传 true（ClientHero 背包已经下发过）。
        /// </summary>
        public static int AutoClean(HumanObject ob, bool isHero, bool notify)
        {
            if (!Settings.CleanZeroDuraOnLogin) return 0;

            List<Entry> safe = OnlySafe(Scan(ob, isHero));
            if (safe.Count == 0) return 0;

            return Delete(safe, notify, isHero ? "自动清理(召唤英雄)" : "自动清理(登录)");
        }

        /// <summary>把清单写进日志文件，GM 事后能对着查。</summary>
        public static void WriteList(string title, List<Entry> list)
        {
            if (list == null || list.Count == 0) return;

            WriteLog(string.Format("---- {0} {1} 共 {2} 件 ----", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), title, list.Count));
            for (int i = 0; i < list.Count; i++)
                WriteLog("    " + list[i].Describe());
        }

        private static readonly object LogLock = new object();

        public static void WriteLog(string line)
        {
            try
            {
                lock (LogLock)
                {
                    string dir = Path.Combine(".", "Logs");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.AppendAllText(Path.Combine(dir, "ZeroDuraClean.log"), line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // 日志失败不能影响游戏逻辑
            }
        }
    }
}
