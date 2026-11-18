using System.Diagnostics;
using System.Text;
using Server.MirEnvir;

namespace Mir2DbUpgrade;

/// <summary>
/// Mir2 数据库升级工具（独立 GUI）。
///
/// 为什么读写都在子进程：Server.MirADB / Server.MirDB 是二进制顺序流，只能靠服务端自己的
/// Load/Save 代码解释。GUI 主进程只做文件编排与校验，真正加载交给 --worker 子进程，
/// 既绕开 Envir 静态单例只能初始化一次的限制，也不会让一次异常把界面带崩。
/// </summary>
public class MainForm : Form
{
    private static readonly int TargetVersion = Envir.Version;   // const，编译期内联

    private TextBox _dirBox;
    private Label _verLabel;
    private Button _browseBtn, _precheckBtn, _upgradeBtn, _rollbackBtn, _openBackupBtn;
    private RichTextBox _log;
    private ProgressBar _progress;
    private ToolStripStatusLabel _statusLabel;

    private string _serverDir;
    private bool _busy;

    public MainForm(string initialDir = null)
    {
        Text = "Mir2 数据库升级工具";
        ClientSize = new Size(940, 720);
        MinimumSize = new Size(820, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = Color.FromArgb(248, 248, 250);

        BuildUi();

        WriteLog($"本工具目标版本：v{TargetVersion}（编译时内置，能读 v{Envir.MinVersion} 以上）", Color.Gray);
        WriteLog("选择服务端目录后先点「预检」；预检只读，不改任何文件。", Color.Gray);

        string dir = initialDir ?? LoadLastDir();
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            _dirBox.Text = dir;
    }

    // ============================ 界面 ============================

    private void BuildUi()
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 10, 12, 8) };
        Controls.Add(host);

        // ---- ① 目录 ----
        var gbDir = new GroupBox
        {
            Text = " ① 服务端目录 ",
            Left = 0, Top = 0, Width = host.Width - 24, Height = 104,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        _dirBox = new TextBox
        {
            Left = 14, Top = 30, Width = gbDir.Width - 130, Height = 28,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Consolas", 10F),
        };
        _dirBox.TextChanged += (_, _) => RefreshVersion();

        _browseBtn = new Button
        {
            Text = "浏览…",
            Left = gbDir.Width - 108, Top = 29, Width = 92, Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.System,
        };
        _browseBtn.Click += (_, _) => BrowseFolder();

        _verLabel = new Label
        {
            Left = 16, Top = 66, Width = gbDir.Width - 32, Height = 26,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Text = "当前库：—",
            ForeColor = Color.DimGray,
        };

        gbDir.Controls.Add(_dirBox);
        gbDir.Controls.Add(_browseBtn);
        gbDir.Controls.Add(_verLabel);

        // ---- ② 操作 ----
        var gbOps = new GroupBox
        {
            Text = " ② 操作 ",
            Left = 0, Top = 114, Width = host.Width - 24, Height = 108,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        _precheckBtn = MakeButton("预检（只读）", 14, 30, 138);
        _upgradeBtn = MakeButton("执行升级", 164, 30, 150);
        _rollbackBtn = MakeButton("回滚到备份", 326, 30, 138);
        _openBackupBtn = MakeButton("打开备份目录", 476, 30, 138);

        _upgradeBtn.BackColor = Color.FromArgb(232, 242, 255);
        _upgradeBtn.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);

        _precheckBtn.Click += (_, _) => DoPrecheck();
        _upgradeBtn.Click += (_, _) => DoUpgrade();
        _rollbackBtn.Click += (_, _) => DoRollback();
        _openBackupBtn.Click += (_, _) => OpenBackupFolder();

        _progress = new ProgressBar
        {
            Left = 640, Top = 36, Width = 148, Height = 18,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Visible = false,
        };

        var hint = new Label
        {
            Left = 16, Top = 74, Width = gbOps.Width - 32, Height = 24,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Text = "流程：自动备份 → 重写为新版本 → 重新加载逐条校验 → 不一致自动还原",
            ForeColor = Color.DimGray,
        };

        gbOps.Controls.AddRange(new Control[] { _precheckBtn, _upgradeBtn, _rollbackBtn, _openBackupBtn, _progress, hint });

        // ---- ③ 日志 ----
        var gbLog = new GroupBox
        {
            Text = " ③ 日志 ",
            Left = 0, Top = 232, Width = host.Width - 24, Height = host.Height - 232,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };

        _log = new RichTextBox
        {
            Left = 12, Top = 24, Width = gbLog.Width - 24, Height = gbLog.Height - 36,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ReadOnly = true,
            BackColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = new Font("Microsoft YaHei UI", 9F),
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            DetectUrls = false,
        };
        gbLog.Controls.Add(_log);

        host.Controls.Add(gbDir);
        host.Controls.Add(gbOps);
        host.Controls.Add(gbLog);

        var strip = new StatusStrip { SizingGrip = false, BackColor = Color.FromArgb(240, 240, 244) };
        _statusLabel = new ToolStripStatusLabel("就绪") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        strip.Items.Add(_statusLabel);
        Controls.Add(strip);
    }

    private static Button MakeButton(string text, int left, int top, int width)
    {
        return new Button
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = 34,
            FlatStyle = FlatStyle.System,
        };
    }

    // ============================ 目录 / 版本 ============================

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "选择服务端目录（含 Server.MirDB 与 Server.MirADB）",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        string guess = _dirBox.Text.Trim();
        if (guess.Length > 0 && Directory.Exists(guess)) dlg.SelectedPath = guess;

        if (dlg.ShowDialog(this) == DialogResult.OK)
            _dirBox.Text = dlg.SelectedPath;
    }

    private void RefreshVersion()
    {
        string dir = _dirBox.Text.Trim();
        _serverDir = null;

        if (dir.Length == 0 || !Directory.Exists(dir))
        {
            _verLabel.Text = "当前库：—";
            _verLabel.ForeColor = Color.DimGray;
            return;
        }

        string db = Path.Combine(dir, "Server.MirDB");
        string adb = Path.Combine(dir, "Server.MirADB");
        if (!File.Exists(db) || !File.Exists(adb))
        {
            _verLabel.Text = "当前库：该目录下没有 Server.MirDB / Server.MirADB";
            _verLabel.ForeColor = Color.Firebrick;
            return;
        }

        _serverDir = dir;
        SaveLastDir(dir);

        int vDb = Worker.PeekVersion(db);
        int vAdb = Worker.PeekVersion(adb);
        long sDb = Worker.SizeOf(db);
        long sAdb = Worker.SizeOf(adb);

        string state;
        Color color;
        if (vDb > TargetVersion || vAdb > TargetVersion)
        {
            state = $"；比工具新，工具最高支持 v{TargetVersion}";
            color = Color.Firebrick;
        }
        else if (vDb == TargetVersion && vAdb == TargetVersion)
        {
            state = "；已是最新，无需升级";
            color = Color.SeaGreen;
        }
        else
        {
            state = $"；升级目标 v{TargetVersion}";
            color = Color.FromArgb(30, 90, 170);
        }

        _verLabel.Text = $"MirDB v{vDb}（{sDb:N0} 字节）    MirADB v{vAdb}（{sAdb:N0} 字节）{state}";
        _verLabel.ForeColor = color;
    }

    private bool EnsureReady()
    {
        if (_busy) return false;

        string dir = _dirBox.Text.Trim();
        if (dir.Length == 0 || !Directory.Exists(dir))
        {
            Warn("请先选择服务端目录。");
            return false;
        }

        if (!File.Exists(Path.Combine(dir, "Server.MirDB")) || !File.Exists(Path.Combine(dir, "Server.MirADB")))
        {
            Warn("该目录下没有 Server.MirDB 或 Server.MirADB。");
            return false;
        }

        _serverDir = dir;
        return true;
    }

    private bool CheckNotRunning()
    {
        string proc = UpgradeRunner.DetectServerProcess();
        if (proc == null) return true;

        Warn($"检测到服务端进程「{proc}」正在运行。\n\n请先关闭服务端再操作 —— 边跑边改库会丢数据。");
        return false;
    }

    // ============================ 预检 ============================

    private async void DoPrecheck()
    {
        if (!EnsureReady()) return;
        if (!CheckNotRunning()) return;

        SetBusy(true, "预检中…");
        WriteLog("");
        WriteLog("──────── 预检（只读，不改任何文件）────────", Color.FromArgb(30, 90, 170));

        string dir = _serverDir;
        try
        {
            var r = await Task.Run(() => UpgradeRunner.Precheck(dir, LogFromBackground));

            WriteLog("");
            if (r.Success)
            {
                WriteLog("预检完成：库可以被正确解析，可以执行升级。", Color.SeaGreen);
                SetStatus($"预检完成（快照 {r.SnapshotRows:N0} 行）");
            }
            else
            {
                WriteLog("预检未通过：" + r.Message, Color.Firebrick);
                SetStatus("预检失败");
            }
        }
        catch (Exception ex)
        {
            WriteLog("!! 预检异常: " + ex.Message, Color.Firebrick);
            SetStatus("预检异常");
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ============================ 升级 ============================

    private async void DoUpgrade()
    {
        if (!EnsureReady()) return;
        if (!CheckNotRunning()) return;

        int vDb = Worker.PeekVersion(Path.Combine(_serverDir, "Server.MirDB"));
        int vAdb = Worker.PeekVersion(Path.Combine(_serverDir, "Server.MirADB"));

        // 版本号相同 ≠ 数据已是最新：羽化 BUFF 编码迁移没改二进制布局，
        // Envir.Version 还是 111，只能靠读一遍看有没有旧编码。所以这里不再直接拦，
        // 交给 UpgradeRunner 先只读检查，再决定要不要备份重写。
        bool sameVersion = vDb == TargetVersion && vAdb == TargetVersion;

        if (vDb > TargetVersion || vAdb > TargetVersion)
        {
            Warn($"库版本（v{Math.Max(vDb, vAdb)}）比本工具（v{TargetVersion}）还新，工具读不了。");
            return;
        }

        if (sameVersion)
        {
            var r0 = MessageBox.Show(this,
                $"数据库版本已经是 v{TargetVersion}。\n\n" +
                $"工具会先只读检查存档里的羽化 BUFF 编码（旧的 230 / 232~241 → 159~169）：\n" +
                "  · 已经是新编码 → 什么都不改，直接结束\n" +
                "  · 发现旧编码 → 先完整备份，再原地改写并逐条校验\n\n" +
                $"目录：{_serverDir}\n\n继续吗？",
                "检查数据编码", MessageBoxButtons.OKCancel, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);
            if (r0 != DialogResult.OK) return;
        }
        else if (!ConfirmUpgrade(vDb, vAdb)) return;

        SetBusy(true, "升级中…");
        WriteLog("");
        WriteLog("──────── 开始升级 ────────", Color.FromArgb(30, 90, 170));

        string dir = _serverDir;
        UpgradeRunner.Result result = null;

        try
        {
            result = await Task.Run(() => UpgradeRunner.Run(dir, LogFromBackground));

            WriteLog("");
            if (result.Success)
            {
                WriteLog($"完成：{result.Message}（{result.Seconds:F1} 秒）", Color.SeaGreen);
                if (!string.IsNullOrEmpty(result.BackupDir))
                    WriteLog($"备份保留在：{result.BackupDir}", Color.DimGray);
                WriteLog("请启动服务端，确认角色能正常进入游戏。", Color.FromArgb(30, 90, 170));
                SetStatus("升级成功");

                string humUp = result.HumUpRemapped > 0
                    ? $"\n羽化 BUFF：迁移 {result.HumUpRemapped} 条 / {result.HumUpOwners} 个角色"
                      + (result.HumUpDedup > 0 ? $"，去重 {result.HumUpDedup} 条" : "")
                    : "";

                Info($"完成。\n\nMirDB  v{result.DbBefore} → v{result.DbAfter}\n" +
                     $"MirADB v{result.AdbBefore} → v{result.AdbAfter}" + humUp + "\n\n" +
                     $"数据逐条校验一致，耗时 {result.Seconds:F1} 秒。\n\n" +
                     (string.IsNullOrEmpty(result.BackupDir) ? "（本次没有改写任何文件）" : $"备份：{result.BackupDir}"));
            }
            else
            {
                WriteLog("!! " + result.Message, Color.Firebrick);
                SetStatus("升级失败");
                Warn("升级未完成：\n\n" + result.Message);
            }
        }
        catch (Exception ex)
        {
            WriteLog("!! 升级异常: " + ex.Message, Color.Firebrick);
            SetStatus("升级异常");
            Warn("升级出现异常：\n" + ex.Message);
        }
        finally
        {
            SetBusy(false);
            RefreshVersion();
        }
    }

    private void LogFromBackground(string msg, LogLevel level)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(new Action(() =>
            {
                foreach (var line in msg.Replace("\r\n", "\n").Split('\n'))
                    WriteLog(line, ColorFor(level));
            }));
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    private static Color ColorFor(LogLevel level) => level switch
    {
        LogLevel.Good => Color.SeaGreen,
        LogLevel.Warn => Color.DarkOrange,
        LogLevel.Error => Color.Firebrick,
        LogLevel.Muted => Color.FromArgb(110, 110, 110),
        _ => Color.FromArgb(30, 30, 30),
    };

    private bool ConfirmUpgrade(int vDb, int vAdb)
    {
        string msg =
            $"即将把数据库从 v{Math.Max(vDb, vAdb)} 升级到 v{TargetVersion}。\n\n" +
            $"目录：{_serverDir}\n\n" +
            "这个过程会：\n" +
            "  · 先把两个库完整备份到 Back Up\\dbupgrade-…（并校验备份 MD5）\n" +
            "  · 再原地重写 Server.MirDB / Server.MirADB\n" +
            "  · 重写后重新加载，逐条比对升级前后的数据\n" +
            "  · 一旦比对不一致，自动从备份还原\n\n" +
            "请确认服务端已经关闭。\n\n继续吗？";

        var r = MessageBox.Show(this, msg, "确认升级", MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        return r == DialogResult.OK;
    }

    // ============================ 回滚 ============================

    private void DoRollback()
    {
        if (!EnsureReady()) return;
        if (!CheckNotRunning()) return;

        string baseDir = Path.Combine(_serverDir, "Back Up");
        if (!Directory.Exists(baseDir))
        {
            Info("没有 Back Up 目录，找不到可用的备份。");
            return;
        }

        using var dlg = new FolderBrowserDialog
        {
            Description = "选择要回滚到的备份目录（形如 dbupgrade-20261027_0030）",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            SelectedPath = baseDir,
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        string dir = dlg.SelectedPath;
        if (!File.Exists(Path.Combine(dir, "Server.MirDB")) || !File.Exists(Path.Combine(dir, "Server.MirADB")))
        {
            Warn("选中的目录里没有 Server.MirDB / Server.MirADB。\n请选 dbupgrade-… 这样的备份目录。");
            return;
        }

        int vDb = Worker.PeekVersion(Path.Combine(dir, "Server.MirDB"));
        int vAdb = Worker.PeekVersion(Path.Combine(dir, "Server.MirADB"));

        var r = MessageBox.Show(this,
            $"将把下面这份备份覆盖回服务端目录：\n\n{dir}\n\n" +
            $"备份版本：MirDB v{vDb} / MirADB v{vAdb}\n目标目录：{_serverDir}\n\n" +
            "回滚前会先把当前库另存一份，防止点错。\n继续吗？",
            "确认回滚", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

        if (r != DialogResult.OK) return;

        SetBusy(true, "回滚中…");
        WriteLog("");
        WriteLog("──────── 回滚 ────────", Color.DarkOrange);

        try
        {
            string safety = UpgradeRunner.CreateBackup(_serverDir,
                Worker.PeekVersion(Path.Combine(_serverDir, "Server.MirDB")),
                Worker.PeekVersion(Path.Combine(_serverDir, "Server.MirADB")));
            WriteLog($"回滚前已备份当前库：{safety}", Color.Gray);

            UpgradeRunner.RestoreFrom(_serverDir, dir);
            WriteLog($"已还原：{dir}", Color.SeaGreen);
            SetStatus("回滚完成");
            Info("回滚完成。请用与备份数据匹配的服务端版本启动。");
        }
        catch (Exception ex)
        {
            WriteLog("!! 回滚失败：" + ex.Message, Color.Firebrick);
            SetStatus("回滚失败");
            Warn("回滚失败：\n" + ex.Message);
        }
        finally
        {
            SetBusy(false);
            RefreshVersion();
        }
    }

    private void OpenBackupFolder()
    {
        if (!EnsureReady()) return;
        string dir = Path.Combine(_serverDir, "Back Up");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }

    // ============================ 杂项 ============================

    private void SetBusy(bool busy, string status = null)
    {
        _busy = busy;
        _progress.Visible = busy;
        _browseBtn.Enabled = !busy;
        _precheckBtn.Enabled = !busy;
        _upgradeBtn.Enabled = !busy;
        _rollbackBtn.Enabled = !busy;
        _openBackupBtn.Enabled = !busy;
        if (busy) SetStatus(status ?? "处理中…");
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void SetStatus(string s) => _statusLabel.Text = s;

    private void WriteLog(string s, Color? color = null)
    {
        if (_log.IsDisposed) return;

        _log.SelectionStart = _log.TextLength;
        _log.SelectionLength = 0;
        _log.SelectionColor = color ?? Color.FromArgb(30, 30, 30);
        _log.AppendText(s + Environment.NewLine);
        _log.SelectionColor = _log.ForeColor;
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void Warn(string s) => MessageBox.Show(this, s, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    private void Info(string s) => MessageBox.Show(this, s, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);

    // 记住上次目录，省得每次重选
    private static string LastDirFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mir2DbUpgrade", "lastdir.txt");

    private static string LoadLastDir()
    {
        try { return File.Exists(LastDirFile) ? File.ReadAllText(LastDirFile).Trim() : null; }
        catch { return null; }
    }

    private static void SaveLastDir(string dir)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LastDirFile));
            File.WriteAllText(LastDirFile, dir);
        }
        catch { }
    }

    // 拖文件夹进窗口直接填路径
    protected override void OnDragEnter(DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy;
        base.OnDragEnter(e);
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            string p = paths[0];
            if (File.Exists(p)) p = Path.GetDirectoryName(p);
            if (Directory.Exists(p)) _dirBox.Text = p;
        }
        base.OnDragDrop(e);
    }
}
