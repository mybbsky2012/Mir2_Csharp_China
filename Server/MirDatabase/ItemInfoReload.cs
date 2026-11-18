using System;
using System.Collections.Generic;

namespace Server.MirDatabase
{
    /// <summary>
    /// 物品库热重载的「就地同步」实现（11-13）。
    ///
    /// 旧实现（Envir.ReloadItems）把 ItemInfoList 里的条目整个替换成编辑器实例：
    /// <code>ItemInfoList[idx] = src;</code>
    /// 但运行期的物品全部直接持有 ItemInfo 实例的引用 —— UserItem.Info 是**字段**不是查表，
    /// 背包 / 装备 / 仓库 / 地上 ItemObject.Info / NPC 商店 Goods / 掉落表 都一样。
    /// 换掉列表里的对象 = 存量物品仍指着旧对象，属性一点没变，玩家只有重启服务器才看得到改动。
    ///
    /// 这里改成「按 Index 配对新旧两条，把字段值就地覆盖到运行期那条」，引用地址不变，
    /// 于是所有持有者立刻看到新数据；新条目照旧追加，库里删掉的照旧移除。
    /// 抽成静态方法是为了能脱离 Envir 实例做单元验证（Tools/ItemReloadTest）。
    /// </summary>
    public static class ItemInfoReload
    {
        /// <summary>
        /// 把 source（DB/编辑器新读入的列表）同步进 runtime（运行服列表）。
        /// </summary>
        /// <param name="changed">输出：真正发生字段变化的运行期条目（在线客户端只推送这些）</param>
        /// <returns>新增 + 删除 + 修改 的件数</returns>
        public static void Sync(List<ItemInfo> runtime, List<ItemInfo> source, List<ItemInfo> changed,
                                out int added, out int removed, out int updated)
        {
            added = 0;
            removed = 0;
            updated = 0;

            if (runtime == null) return;
            if (source == null) source = new List<ItemInfo>();
            if (ReferenceEquals(runtime, source)) return;   // 同一份列表：边遍历边改会抛异常，且本就无需同步

            // Index → 运行期条目。物品库几千条，逐条 FindIndex 是 O(n²)，这里先建字典（O(n)）。
            Dictionary<int, ItemInfo> lookup = new Dictionary<int, ItemInfo>(runtime.Count);
            for (int i = 0; i < runtime.Count; i++)
            {
                ItemInfo info = runtime[i];
                if (info == null) continue;
                lookup[info.Index] = info;
            }

            HashSet<int> sourceIds = new HashSet<int>();

            for (int i = 0; i < source.Count; i++)
            {
                ItemInfo src = source[i];
                if (src == null) continue;

                sourceIds.Add(src.Index);

                ItemInfo dst;
                if (!lookup.TryGetValue(src.Index, out dst) || dst == null)
                {
                    // 库里新增的条目：运行期还不能和编辑器共享同一个实例
                    //（否则下次在编辑器里改这件东西，线上会"隔空"跟着变，且绕过重载流程）
                    ItemInfo runtimeCopy = new ItemInfo();
                    runtimeCopy.CopyFrom(src);
                    runtimeCopy.Index = src.Index;      // CopyFrom 有意跳过身份字段

                    runtime.Add(runtimeCopy);
                    lookup[src.Index] = runtimeCopy;
                    added++;
                    continue;   // 新增不进 changed：客户端本来就没这条，没有"旧数据要刷新"
                }

                if (ReferenceEquals(dst, src)) continue;   // 已经是同一个实例，无需同步

                if (dst.CopyFrom(src))
                {
                    updated++;
                    if (changed != null) changed.Add(dst);
                }
            }

            // 库里已删除的条目：从运行服列表移除（存量物品的引用仍然有效，只是不再出现在物品库）
            for (int i = runtime.Count - 1; i >= 0; i--)
            {
                ItemInfo info = runtime[i];
                if (info == null) { runtime.RemoveAt(i); continue; }
                if (sourceIds.Contains(info.Index)) continue;

                runtime.RemoveAt(i);
                removed++;
            }
        }
    }
}
