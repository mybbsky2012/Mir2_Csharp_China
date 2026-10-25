using System;
using System.Collections.Generic;
using System.Linq;
using Shared;
using S = ServerPackets;

namespace Server.MirObjects
{
    /// <summary>
    /// 一键回收：把背包里符合条件的装备换成金币。
    ///
    /// 两种匹配方式（脚本里分别用 RECYCLE / RECYCLEKEY 触发）：
    ///   A. 按套装：只收 ItemSet 等于指定枚举的那一套；
    ///   B. 按名字：只收名字里含指定关键词的装备（沃玛 / 祖玛 / 赤月这类散件，
    ///      本服没有把它们做成 ItemSet，所以只能按名字认）。
    ///
    /// 两种方式共用的规则（与脚本页文案保持一致）：
    ///   1. 只扫背包（含腰带 0~5 格与扩展格），不碰装备栏、仓库、精炼台；
    ///   2. 回收价 = 物品数据库原价（ItemInfo.Price）× 数量，不打折；
    ///   3. 绑定为「不可出售」的物品、租赁中的物品自动跳过；
    ///   4. 物品直接销毁并通知客户端刷新，不进商店的「买回」列表。
    /// </summary>
    public static class RecycleManager
    {
        /// <summary>按名字回收时认可的「装备」类型（任务物品、杂物、消耗品一律不碰）</summary>
        private static readonly HashSet<ItemType> EquipmentTypes = new HashSet<ItemType>
        {
            ItemType.武器, ItemType.盔甲, ItemType.头盔, ItemType.项链,
            ItemType.手镯, ItemType.戒指, ItemType.腰带, ItemType.靴子
        };

        /// <summary>
        /// 按套装回收：<paramref name="setKey"/> 是 ItemSet 的枚举名。
        /// 传「非套装」时表示回收所有没归入套装的装备（只认 8 类装备，药水/卷轴/任务物品不碰）。
        /// </summary>
        public static bool Recycle(PlayerObject player, string setKey)
        {
            if (player == null || string.IsNullOrEmpty(setKey)) return false;

            setKey = setKey.Trim();

            if (setKey.StartsWith("[")) setKey = setKey.Trim('[', ']');

            ItemSet set;

            if (!Enum.TryParse(setKey, true, out set) || !Enum.IsDefined(typeof(ItemSet), set))
            {
                player.ReceiveChat("回收失败：脚本里的套装名「" + setKey + "」无效", ChatType.System);
                MessageQueue.Instance.Enqueue("[一键回收] 脚本套装名无效: " + setKey);
                return false;
            }

            // 「非套装」不是一个套装，而是一大类：所有 Set=非套装 的装备
            if (set == ItemSet.非套装)
            {
                return RecycleCore(player, "非套装装备", item =>
                    item.Info.Set == ItemSet.非套装 && EquipmentTypes.Contains(item.Info.Type));
            }

            return RecycleCore(player, "【" + setKey + "】", item => item.Info.Set == set);
        }

        /// <summary>
        /// 按名字关键词回收：收背包里名字含关键词的装备（沃玛 / 祖玛 / 赤月这类散件）。
        /// <paramref name="keywordExpr"/> 支持多个关键词，用 | , 、 任一符号分隔，命中任意一个即回收。
        /// </summary>
        public static bool RecycleByKeyword(PlayerObject player, string keywordExpr)
        {
            if (player == null || string.IsNullOrEmpty(keywordExpr)) return false;

            if (keywordExpr.StartsWith("[")) keywordExpr = keywordExpr.Trim('[', ']');

            string[] words = keywordExpr
                .Split(new[] { '|', ',', '，', '、' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();

            if (words.Length == 0)
            {
                player.ReceiveChat("回收失败：脚本里的关键词「" + keywordExpr + "」无效", ChatType.System);
                MessageQueue.Instance.Enqueue("[一键回收] 脚本关键词无效: " + keywordExpr);
                return false;
            }

            string label = "名字含「" + string.Join("」「", words) + "」的装备";

            return RecycleCore(player, label, item =>
            {
                if (!EquipmentTypes.Contains(item.Info.Type)) return false;

                foreach (string word in words)
                {
                    if (item.Info.Name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }

                return false;
            });
        }

        /// <summary>脚本里可用的一键回收套装清单（给 GM 工具/调试用，与实际回收判定无关）</summary>
        public static string DescribeSets()
        {
            return string.Join("、", Enum.GetNames(typeof(ItemSet)).Where(x => x != "非套装"));
        }

        /// <summary>扫背包 → 匹配 → 折算金币 → 销毁发包。两种回收方式共用。</summary>
        private static bool RecycleCore(PlayerObject player, string label, Func<UserItem, bool> match)
        {
            UserItem[] bag = player.Info.Inventory;

            ulong totalGold = 0;
            int itemCount = 0;
            int slotCount = 0;
            List<string> detail = new List<string>();

            for (int i = 0; i < bag.Length; i++)
            {
                UserItem item = bag[i];

                if (item == null || item.Info == null) continue;
                if (!match(item)) continue;

                // ★特殊物品（麻痹/复活/护身/传送/记忆/隐身等特殊戒指、特殊项链手镯等）
                //   一律不回收：这类装备价值极高且标识就是 Info.Unique != None，误收无法挽回
                if (item.Info.Unique != SpecialItemMode.None) continue;

                // 绑定为不可出售的（任务/活动装备）不回收，避免玩家损失
                if (item.Info.Bind.HasFlag(BindMode.DontSell)) continue;
                if (item.RentalInformation != null) continue;

                totalGold += (ulong)item.Info.Price * (ulong)Math.Max(1, (int)item.Count);

                itemCount += Math.Max(1, (int)item.Count);
                slotCount++;
                detail.Add(item.Info.Name + (item.Count > 1 ? "x" + item.Count : string.Empty));

                bag[i] = null;
                player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
            }

            if (slotCount == 0)
            {
                player.ReceiveChat("背包里没有可回收的" + label, ChatType.Hint);
                return false;
            }

            if (totalGold > uint.MaxValue) totalGold = uint.MaxValue;

            player.GainGold((uint)totalGold);
            player.RefreshBagWeight();

            player.ReceiveChat(string.Format("一键回收完成：{0} {1} 件，获得 {2} 金币",
                label, itemCount, totalGold), ChatType.Hint);

            MessageQueue.Instance.Enqueue(string.Format("[一键回收] {0} 回收 {1}：{2} 件，{3} 金币（{4}）",
                player.Name, label, itemCount, totalGold,
                string.Join("、", detail.Take(20)) + (detail.Count > 20 ? "…" : string.Empty)));

            return true;
        }
    }
}
