using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Server;

namespace ConfigFormUiTest;

/// <summary>
/// 只读验证「服务器设置 → 版本信息」页新增的三个开关（离线挂机免损耗 ×2 + 假人系统）。
/// 不保存配置、不改任何文件，只截图 + 做布局断言。
/// 用法：ConfigFormUiTest.exe "E:\...\Mir2Server" "D:\...\out.png"
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // 隔离目录往返测试：抄一份 Configs 出来，走「改勾选 → 保存 → 重新读」全链路，
        // 绝不碰真实服务端的配置文件。
        if (args.Length >= 3 && args[0] == "--roundtrip")
            return RoundTrip(args[1], args[2]);

        string dir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        string outPath = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "configform_ui.png");

        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 2; }
        Directory.SetCurrentDirectory(dir);

        Settings.Load();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var form = new ConfigForm();
        form.Show();
        Application.DoEvents();
        Thread.Sleep(300);
        Application.DoEvents();

        int fail = 0;
        void Check(string name, bool ok, string detail = "")
        {
            Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (detail.Length > 0 ? "  → " + detail : ""));
            if (!ok) fail++;
        }

        // 找控件
        CheckBox cbGold = Find<CheckBox>(form, "GoldDirectCheckBox");
        CheckBox cbAfk = Find<CheckBox>(form, "OfflineAfkEnabledCheckBox");
        CheckBox cbCons = Find<CheckBox>(form, "OfflineAfkNoConsumableCheckBox");
        CheckBox cbEquip = Find<CheckBox>(form, "OfflineAfkNoEquipCheckBox");
        CheckBox cbFake = Find<CheckBox>(form, "FakePlayerEnabledCheckBox");
        GroupBox gb = Find<GroupBox>(form, "versionFuncGroupBox");

        CheckBox[] added = { cbAfk, cbCons, cbEquip, cbFake };

        Check("四个新开关都已创建", cbGold != null && gb != null && Array.TrueForAll(added, c => c != null));
        if (cbGold == null || gb == null || !Array.TrueForAll(added, c => c != null)) return 1;

        // 1) 都挂在「版本功能设置」分组里（和金币直接入包同一块）
        Check("四个开关都在版本功能设置分组内", Array.TrueForAll(added, c => c.Parent == gb));

        // 2) 位置：金币直接入包 → 允许离线挂机 → 两项免损耗 → 假人系统
        Check("允许离线挂机 位于金币直接入包下方", cbAfk.Top > cbGold.Top,
              $"金币 y={cbGold.Top} / 允许挂机 y={cbAfk.Top}");
        Check("不扣消耗品持久 位于允许离线挂机下方", cbCons.Top > cbAfk.Top,
              $"{cbAfk.Top} → {cbCons.Top}");
        Check("不扣装备耐久 位于上一项下方", cbEquip.Top > cbCons.Top,
              $"{cbCons.Top} → {cbEquip.Top}");
        Check("启用假人系统 位于最下方", cbFake.Top > cbEquip.Top,
              $"{cbEquip.Top} → {cbFake.Top}");
        Check("四个开关左对齐（同一列）", Array.TrueForAll(added, c => c.Left == cbGold.Left),
              $"金币 x={cbGold.Left} / 新增 x={string.Join(",", Array.ConvertAll(added, c => c.Left))}");

        // 3) 不超出分组边界
        int rightMost = 0, bottomMost = 0;
        foreach (CheckBox c in added) { rightMost = Math.Max(rightMost, c.Right); bottomMost = Math.Max(bottomMost, c.Bottom); }
        Check("四个开关都没超出分组右边界", rightMost < gb.ClientSize.Width,
              $"分组宽={gb.ClientSize.Width}，最宽={rightMost}");
        Check("四个开关都没超出分组下边界", bottomMost < gb.ClientSize.Height,
              $"底部 y={bottomMost} / 分组高={gb.ClientSize.Height}");

        // 4) 不与分组里任何已有控件重叠（含右列倍率标签/输入框）
        int overlaps = 0;
        string overlapDetail = "";
        var all = new List<Control>();
        foreach (Control c in gb.Controls) all.Add(c);
        foreach (Control a in all)
        {
            if (Array.IndexOf(added, a) >= 0) continue;
            foreach (CheckBox nb in added)
            {
                if (!a.Visible || !nb.Visible) continue;
                if (a.Bounds.IntersectsWith(nb.Bounds))
                {
                    overlaps++;
                    overlapDetail += $"[{a.Name} ↔ {nb.Name}] ";
                }
            }
        }
        Check("新开关不与右列控件重叠", overlaps == 0, overlapDetail.Length > 0 ? overlapDetail : "无重叠");

        // 5) 勾选状态与 Settings 一致
        Check("允许离线挂机 勾选态 = Settings", cbAfk.Checked == Settings.OfflineAfkEnabled,
              $"界面={cbAfk.Checked} 配置={Settings.OfflineAfkEnabled}");
        Check("离线挂机·消耗品 勾选态 = Settings",
              cbCons.Checked == Settings.OfflineAfkNoConsumableDura,
              $"界面={cbCons.Checked} 配置={Settings.OfflineAfkNoConsumableDura}");
        Check("离线挂机·装备 勾选态 = Settings",
              cbEquip.Checked == Settings.OfflineAfkNoEquipDura,
              $"界面={cbEquip.Checked} 配置={Settings.OfflineAfkNoEquipDura}");
        Check("假人系统 勾选态 = Settings",
              cbFake.Checked == Settings.FakePlayerEnabled,
              $"界面={cbFake.Checked} 配置={Settings.FakePlayerEnabled}");

        // 5b) 总开关联动：关掉「允许离线挂机」时两个子项置灰（勾选值保留）
        bool consChecked = cbCons.Checked, equipChecked = cbEquip.Checked;
        cbAfk.Checked = false;
        Application.DoEvents();
        Check("6) 关掉总开关 → 两个免损耗子项置灰",
              !cbCons.Enabled && !cbEquip.Enabled,
              $"消耗品 Enabled={cbCons.Enabled} 装备 Enabled={cbEquip.Enabled}");
        Check("7) 置灰不改变子项勾选值", cbCons.Checked == consChecked && cbEquip.Checked == equipChecked);

        cbAfk.Checked = true;
        Application.DoEvents();
        Check("8) 重新打开总开关 → 子项恢复可用",
              cbCons.Enabled && cbEquip.Enabled,
              $"消耗品 Enabled={cbCons.Enabled} 装备 Enabled={cbEquip.Enabled}");

        // 6) 整页不越界（分组下沿不能压到「服务器版本信息」框）
        GroupBox gbVer = Find<GroupBox>(form, "groupBox1");
        if (gbVer != null)
        {
            Check("版本功能分组不压住服务器版本信息框", gb.Bottom <= gbVer.Top,
                  $"功能框底={gb.Bottom} / 版本框顶={gbVer.Top}");
        }

        // 截图
        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
        bmp.Save(outPath, ImageFormat.Png);
        Console.WriteLine("截图: " + outPath);

        // 只截「版本信息」页局部（放大看布局）
        Rectangle pageRect = form.RectangleToScreen(new Rectangle(gb.Left - 6, gb.Top - 8, gb.Width + 12, gb.Height + 16));
        pageRect = new Rectangle(pageRect.Left - form.Left, pageRect.Top - form.Top, pageRect.Width, pageRect.Height);
        pageRect.Intersect(new Rectangle(0, 0, form.Width, form.Height));
        if (pageRect.Width > 0 && pageRect.Height > 0)
        {
            using var clone = bmp.Clone(pageRect, bmp.PixelFormat);
            clone.Save(Path.ChangeExtension(outPath, ".group.png"), ImageFormat.Png);
            Console.WriteLine("分组截图: " + Path.ChangeExtension(outPath, ".group.png"));
        }

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "全部通过" : $"{fail} 项失败");
        return fail == 0 ? 0 : 1;
    }

    /// <summary>
    /// 隔离目录往返测试：复制 Configs → 构造窗体 → 改勾选 → Save() → Settings.Save() →
    /// 重新 Settings.Load() 校验落盘值。全程只操作 workDir，不碰真实服务端。
    /// </summary>
    private static int RoundTrip(string srcServerDir, string workDir)
    {
        int fail = 0;
        void Check(string name, bool ok, string detail = "")
        {
            Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (detail.Length > 0 ? "  → " + detail : ""));
            if (!ok) fail++;
        }

        string srcConfigs = Path.Combine(srcServerDir, "Configs");
        if (!Directory.Exists(srcConfigs)) { Console.WriteLine("源 Configs 不存在: " + srcConfigs); return 2; }

        string dstConfigs = Path.Combine(workDir, "Configs");
        if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
        Directory.CreateDirectory(dstConfigs);

        foreach (string f in Directory.GetFiles(srcConfigs, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(srcConfigs, f);
            string target = Path.Combine(dstConfigs, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(f, target, true);
        }

        Directory.SetCurrentDirectory(workDir);

        Settings.Load();

        bool cfgAfkBefore = Settings.OfflineAfkEnabled;
        bool cfgConsBefore = Settings.OfflineAfkNoConsumableDura;
        bool cfgEquipBefore = Settings.OfflineAfkNoEquipDura;
        bool cfgFakeBefore = Settings.FakePlayerEnabled;

        Console.WriteLine($"[初始] 允许挂机={cfgAfkBefore} 消耗品={cfgConsBefore} 装备={cfgEquipBefore} 假人={cfgFakeBefore}");

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var form = new ConfigForm();
        form.Show();
        Application.DoEvents();

        CheckBox cbAfk = Find<CheckBox>(form, "OfflineAfkEnabledCheckBox");
        CheckBox cbCons = Find<CheckBox>(form, "OfflineAfkNoConsumableCheckBox");
        CheckBox cbEquip = Find<CheckBox>(form, "OfflineAfkNoEquipCheckBox");
        CheckBox cbFake = Find<CheckBox>(form, "FakePlayerEnabledCheckBox");
        if (cbAfk == null || cbCons == null || cbEquip == null || cbFake == null) { Console.WriteLine("控件缺失"); return 1; }

        Check("1) 窗体初值与配置一致",
              cbAfk.Checked == cfgAfkBefore && cbCons.Checked == cfgConsBefore &&
              cbEquip.Checked == cfgEquipBefore && cbFake.Checked == cfgFakeBefore,
              $"界面=({cbAfk.Checked},{cbCons.Checked},{cbEquip.Checked},{cbFake.Checked})");

        // 总开关关着时子项会被置灰，但勾选值仍然是配置里的值
        Check("2) 总开关与子项可用性联动正确",
              cbCons.Enabled == cbAfk.Checked && cbEquip.Enabled == cbAfk.Checked,
              $"总开关={cbAfk.Checked} 子项可用=({cbCons.Enabled},{cbEquip.Enabled})");

        // ── 四个开关全部取反（子项先确保可用，否则 Checked 改不动也没关系，值仍会写） ──
        cbAfk.Checked = !cbAfk.Checked;
        cbCons.Checked = !cbCons.Checked;
        cbEquip.Checked = !cbEquip.Checked;
        cbFake.Checked = !cbFake.Checked;

        bool expectAfk = cbAfk.Checked;
        bool expectCons = cbCons.Checked;
        bool expectEquip = cbEquip.Checked;
        bool expectFake = cbFake.Checked;
        bool changedFake = expectFake != cfgFakeBefore;

        Server.MirObjects.FakePlayerManager.RestartPending = false;

        form.Save();            // ← 界面 → Settings
        Settings.Save();        // ← Settings → Configs\Setup.ini

        Check("3) 保存后 Settings 内存值 = 界面值",
              Settings.OfflineAfkEnabled == expectAfk &&
              Settings.OfflineAfkNoConsumableDura == expectCons &&
              Settings.OfflineAfkNoEquipDura == expectEquip &&
              Settings.FakePlayerEnabled == expectFake,
              $"内存=({Settings.OfflineAfkEnabled},{Settings.OfflineAfkNoConsumableDura},{Settings.OfflineAfkNoEquipDura},{Settings.FakePlayerEnabled})");

        if (changedFake)
            Check("4) 假人开关变化 → 置热切换标记", Server.MirObjects.FakePlayerManager.RestartPending);
        else
            Check("4) 假人开关未变化 → 不置热切换标记", !Server.MirObjects.FakePlayerManager.RestartPending);

        // ── 重新从文件读一遍，确认真的落盘 ──
        Settings.Load();
        Check("5) 重新读取 Setup.ini 后四个开关保持不变",
              Settings.OfflineAfkEnabled == expectAfk &&
              Settings.OfflineAfkNoConsumableDura == expectCons &&
              Settings.OfflineAfkNoEquipDura == expectEquip &&
              Settings.FakePlayerEnabled == expectFake,
              $"读回=({Settings.OfflineAfkEnabled},{Settings.OfflineAfkNoConsumableDura},{Settings.OfflineAfkNoEquipDura},{Settings.FakePlayerEnabled})");

        string ini = Path.Combine(dstConfigs, "Setup.ini");
        string text = File.Exists(ini) ? File.ReadAllText(ini) : "";
        string offSec = Section(text, "OfflineAFK");
        Check("6) Setup.ini 里 OfflineAFK 段三个键都写进去了",
              offSec != null && offSec.Contains("Enabled") && offSec.Contains("NoConsumableDura") && offSec.Contains("NoEquipDura"),
              offSec == null ? "缺段" : offSec.Replace("\r\n", " | ").Replace("\n", " | "));
        string fakeSec = Section(text, "FakePlayer");
        Check("7) Setup.ini 里 FakePlayer.Enabled 已写",
              fakeSec != null && fakeSec.Contains("Enabled"),
              fakeSec == null ? "缺段" : "Ok");

        // ── 关掉总开关后再保存：Enabled 必须落 false；子项按界面原样保存（尊重用户配置，
        //     只是界面上被置灰；功能上因为挂机本身不会发生，免扣永远不会生效） ──
        cbAfk.Checked = false;
        cbCons.Checked = true;      // 故意勾着，验证不会被偷偷改掉
        cbEquip.Checked = true;
        form.Save();
        Settings.Save();
        Settings.Load();
        Check("8) 总开关关掉后 Enabled=False 落盘，子项勾选值不被偷偷改写",
              !Settings.OfflineAfkEnabled &&
              Settings.OfflineAfkNoConsumableDura && Settings.OfflineAfkNoEquipDura,
              $"读回 Enabled={Settings.OfflineAfkEnabled} 消耗品={Settings.OfflineAfkNoConsumableDura} 装备={Settings.OfflineAfkNoEquipDura}");

        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "全部通过" : $"{fail} 项失败");
        return fail == 0 ? 0 : 1;
    }

    /// <summary>取 ini 里某个 [段] 的原始文本块。</summary>
    private static string Section(string text, string name)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        bool inside = false;
        var sb = new System.Text.StringBuilder();
        foreach (string line in lines)
        {
            string t = line.Trim();
            if (t.StartsWith("[") && t.EndsWith("]"))
            {
                inside = string.Equals(t.Substring(1, t.Length - 2).Trim(), name, StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (inside) sb.AppendLine(line);
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    private static T Find<T>(Control root, string name) where T : Control
    {
        foreach (Control c in root.Controls)
        {
            if (c is T t && c.Name == name) return t;

            T inner = Find<T>(c, name);
            if (inner != null) return inner;
        }

        return null;
    }
}
