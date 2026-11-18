using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Server;
using Server.MirObjects;

namespace VirtualRowTest
{
    /// <summary>
    /// 验证在线列表虚拟模式的取行回调：返回的 ListViewItem 子项数必须等于列数。
    /// 以前兜底行是 `new ListViewItem(string.Empty)`（只有 1 个子项），6 列控件下
    /// WinForms 会抛「处于 VirtualMode 时…需要每个 ListView 列的列表视图子项」。
    /// 直接调真实的 SMain 私有方法，不依赖画图/服务端启动。
    /// </summary>
    internal static class Program
    {
        private static int _pass, _fail;

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();

            SMain form;
            try
            {
                form = new SMain();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SKIP] 无法构造 SMain（需要完整的服务端环境）：" + ex.Message);
                return;
            }

            var lvField = typeof(SMain).GetField("PlayersOnlineListView",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var snapField = typeof(SMain).GetField("_onlineSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var retrieve = typeof(SMain).GetMethod("PlayersOnlineListView_RetrieveVirtualItem",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var create = typeof(SMain).GetMethod("CreateListView",
                BindingFlags.Instance | BindingFlags.NonPublic);

            ListView lv = (ListView)lvField.GetValue(form);
            int columns = lv.Columns.Count;

            Console.WriteLine("在线列表列数 = " + columns + "，VirtualMode = " + lv.VirtualMode);
            Check("控件已开启虚拟模式", lv.VirtualMode);
            Check("列数为 6（序号/名称/等级/职业/性别/状态）", columns == 6);

            // ---------- 1. 空快照 + 行数 3：越界兜底行 ----------
            snapField.SetValue(form, Activator.CreateInstance(snapField.FieldType));
            SetSize(lv, 3);

            for (int i = 0; i < 3; i++)
            {
                ListViewItem item = Retrieve(form, retrieve, lv, i);
                Check(string.Format("空快照取第 {0} 行 → 子项 {1} 个（应 = {2}）", i,
                    item?.SubItems.Count ?? -1, columns),
                    item != null && item.SubItems.Count == columns);
            }

            // ---------- 2. 严重越界（模拟快照比行数小） ----------
            foreach (int bad in new[] { -1, 99, int.MaxValue })
            {
                ListViewItem item = Retrieve(form, retrieve, lv, bad);
                Check(string.Format("越界索引 {0} → 子项 {1} 个", bad, item?.SubItems.Count ?? -1),
                    item != null && item.SubItems.Count == columns);
            }

            // ---------- 3. 快照里有行，但玩家的 Info 正被下线线程置空 ----------
            IList snapshot = (IList)Activator.CreateInstance(snapField.FieldType);

            // 3a. 直接传 null（CreateListView 最极端的入口）
            ListViewItem nullItem = (ListViewItem)create.Invoke(form, new object[] { null });
            Check(string.Format("CreateListView(null) → 子项 {0} 个", nullItem?.SubItems.Count ?? -1),
                nullItem != null && nullItem.SubItems.Count == columns);

            // 3b. 未初始化的 PlayerObject（Info 为 null，等价于「下线中」那一刻）
            PlayerObject ghost = (PlayerObject)FormatterServices.GetUninitializedObject(typeof(PlayerObject));
            ListViewItem ghostItem = (ListViewItem)create.Invoke(form, new object[] { ghost });
            Check(string.Format("Info 为 null 的玩家 → 子项 {0} 个", ghostItem?.SubItems.Count ?? -1),
                ghostItem != null && ghostItem.SubItems.Count == columns);

            snapshot.Add(ghost);
            snapField.SetValue(form, snapshot);
            SetSize(lv, 1);

            ListViewItem row0 = Retrieve(form, retrieve, lv, 0);
            Check(string.Format("快照里这一行 Info 为空 → 子项 {0} 个", row0?.SubItems.Count ?? -1),
                row0 != null && row0.SubItems.Count == columns);

            // ---------- 4. 正常行不受影响 ----------
            ListViewItem normal = new ListViewItem("1");
            for (int i = 1; i < columns; i++) normal.SubItems.Add("c" + i);

            var ensure = typeof(SMain).GetMethod("EnsureItemColumns",
                BindingFlags.Instance | BindingFlags.NonPublic);

            ListViewItem kept = (ListViewItem)ensure.Invoke(form, new object[] { normal });
            Check("已满列的行原样返回", ReferenceEquals(kept, normal) && kept.SubItems.Count == columns);

            Console.WriteLine();
            Console.WriteLine(string.Format("==== 通过 {0} 项，失败 {1} 项 ====", _pass, _fail));
            Environment.ExitCode = _fail == 0 ? 0 : 1;
        }

        private static void SetSize(ListView lv, int size)
        {
            // 先缩小再放大，避免缩小行数时控件对旧行发起回调
            if (lv.VirtualListSize > size) lv.VirtualListSize = 0;
            lv.VirtualListSize = size;
        }

        private static ListViewItem Retrieve(SMain form, MethodInfo m, ListView lv, int index)
        {
            var args = new RetrieveVirtualItemEventArgs(index);

            try
            {
                m.Invoke(form, new object[] { lv, args });
            }
            catch (TargetInvocationException ex)
            {
                Console.WriteLine("   [异常] " + (ex.InnerException ?? ex).Message);
                return null;
            }

            return args.Item;
        }

        private static void Check(string name, bool ok)
        {
            Console.WriteLine((ok ? "  [OK]   " : "  [FAIL] ") + name);
            if (ok) _pass++; else _fail++;
        }
    }
}
