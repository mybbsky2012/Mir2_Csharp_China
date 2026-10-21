using System;
using System.Collections.Generic;
using System.Linq;
using Shared;
using S = ServerPackets;

namespace Server.MirObjects
{
    /// <summary>
    /// 一键回收：按「套装」把背包里的装备换成金币。
    ///
    /// 规则（与脚本页文案保持一致）：
    ///   1. 只扫背包（含腰带 0~5 格与扩展格），不碰装备栏、仓库、精炼台；
    ///   2. 只收脚本里点名的那个套装（ItemSet），其它套装一律不动；
    ///   3. 回收价 = 物品数据库原价（ItemInfo.Price）× 数量，不打折；
    ///   4. 绑定为「不可出售」的物品、租赁中的物品自动跳过；
    ///   5. 物品直接销毁并通知客户端刷新，不进商店的「买回」列表。
    ///
    /// 由脚本命令 RECYCLE &lt;套装名&gt; 调用，套装名就是 ItemSet 的枚举名。
    /// </summary>
    public static class RecycleManager
    {
        /// <summary>
        /// 回收玩家背包里属于 <paramref name="setKey"/> 套装的全部装备。
        /// </summary>
        /// <returns>是否真的回收了东西（一件都没收到时返回 false 并给出提示）</returns>
        public static bool Recycle(PlayerObject player, string setKey)
        {
            if (player == null || string.IsNullOrEmpty(setKey)) return false;

            setKey = setKey.Trim();

            if (setKey.StartsWith("[")) setKey = setKey.Trim('[', ']');

            ItemSet set;

            if (!Enum.TryParse(setKey, true, out set) || !Enum.IsDefined(typeof(ItemSet), set) || set == ItemSet.非套装)
            {
                player.ReceiveChat("回收失败：脚本里的套装名「" + setKey + "」无效", ChatType.System);
                MessageQueue.Instance.Enqueue("[一键回收] 脚本套装名无效: " + setKey);
                return false;
            }

            UserItem[] bag = player.Info.Inventory;

            ulong totalGold = 0;
            int itemCount = 0;
            int slotCount = 0;
            List<string> detail = new List<string>();

            for (int i = 0; i < bag.Length; i++)
            {
                UserItem item = bag[i];

                if (item == null || item.Info == null) continue;
                if (item.Info.Set != set) continue;

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
                player.ReceiveChat("背包里没有可回收的【" + setKey + "】装备", ChatType.Hint);
                return false;
            }

            if (totalGold > uint.MaxValue) totalGold = uint.MaxValue;

            player.GainGold((uint)totalGold);
            player.RefreshBagWeight();

            player.ReceiveChat(string.Format("一键回收完成：【{0}】{1} 件，获得 {2} 金币",
                setKey, itemCount, totalGold), ChatType.Hint);

            MessageQueue.Instance.Enqueue(string.Format("[一键回收] {0} 回收套装 {1}：{2} 件，{3} 金币（{4}）",
                player.Name, setKey, itemCount, totalGold,
                string.Join("、", detail.Take(20)) + (detail.Count > 20 ? "…" : string.Empty)));

            return true;
        }

        /// <summary>脚本里可用的一键回收套装清单（给 GM 工具/调试用，与实际回收判定无关）</summary>
        public static string DescribeSets()
        {
            return string.Join("、", Enum.GetNames(typeof(ItemSet)).Where(x => x != "非套装"));
        }
    }
}
