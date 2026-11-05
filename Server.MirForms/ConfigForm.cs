using System.Net;
using System.Text.RegularExpressions;
using Server.MirObjects;

namespace Server
{
    public partial class ConfigForm : Form
    {
        public ConfigForm()
        {
            InitializeComponent();

            // 20261105 其他选项里再挂一页「诱惑之光」（代码构建，不动 Designer 文件）
            BuildTameTab();

            VPathTextBox.Text = Settings.VersionPath;
            VersionCheckBox.Checked = Settings.CheckVersion;
            RelogDelayTextBox.Text = Settings.RelogDelay.ToString();

            // 版本功能设置
            MultiThreadCheckBox.Checked = Settings.Multithreaded;
            TestServerCheckBox.Checked = Settings.TestServer;
            GoldDirectCheckBox.Checked = !Settings.DropGold;    //勾选=金币直接入包（DropGold=false 时怪物金币不落地）

            // 离线挂机 / 假人系统（10-26 从 ini 搬到界面）
            OfflineAfkEnabledCheckBox.Checked = Settings.OfflineAfkEnabled;
            OfflineAfkNoConsumableCheckBox.Checked = Settings.OfflineAfkNoConsumableDura;
            OfflineAfkNoEquipCheckBox.Checked = Settings.OfflineAfkNoEquipDura;
            FakePlayerEnabledCheckBox.Checked = Settings.FakePlayerEnabled;
            ExpRateTextBox.Text = Settings.ExpRate.ToString();
            DropRateTextBox.Text = Settings.DropRate.ToString();
            HighLevelRateTextBox.Text = Settings.HighLevelExpRate.ToString();
            DeathExpTextBox.Text = Settings.DeathExpLossRate.ToString();

            IPAddressTextBox.Text = Settings.IPAddress;
            PortTextBox.Text = Settings.Port.ToString();
            TimeOutTextBox.Text = Settings.TimeOut.ToString();
            MaxUserTextBox.Text = Settings.MaxUser.ToString();

            StartHTTPCheckBox.Checked = Settings.StartHTTPService;
            EnableResourceCheckBox.Checked = Settings.EnableResourceService;
            ResourcePathTextBox.Text = Settings.ResourcePath;
            ResourceAllowAnyIPCheckBox.Checked = Settings.ResourceAllowAnyIP;
            HTTPIPAddressTextBox.Text = Settings.HTTPIPAddress;
            HTTPTrustedIPAddressTextBox.Text = Settings.HTTPTrustedIPAddress;

            AccountCheckBox.Checked = Settings.AllowNewAccount;
            PasswordCheckBox.Checked = Settings.AllowChangePassword;
            LoginCheckBox.Checked = Settings.AllowLogin;
            NCharacterCheckBox.Checked = Settings.AllowNewCharacter;
            DCharacterCheckBox.Checked = Settings.AllowDeleteCharacter;
            StartGameCheckBox.Checked = Settings.AllowStartGame;
            AllowAssassinCheckBox.Checked = Settings.AllowCreateAssassin;
            AllowArcherCheckBox.Checked = Settings.AllowCreateArcher;
            Resolution_textbox.Text = Settings.AllowedResolution.ToString();

            SafeZoneBorderCheckBox.Checked = Settings.SafeZoneBorder;
            SafeZoneHealingCheckBox.Checked = Settings.SafeZoneHealing;
            gameMasterEffect_CheckBox.Checked = Settings.GameMasterEffect;
            lineMessageTimeTextBox.Text = Settings.LineMessageTimer.ToString();

            // 穿行辅助（内挂「穿人」拆成的 4 档，各自独立勾选）
            WalkThroughCheckBox.Checked = Settings.EnableWalkThrough;
            WalkThroughMonsterCheckBox.Checked = Settings.EnableWalkThroughMonster;
            WalkThroughNPCCheckBox.Checked = Settings.EnableWalkThroughNPC;
            WalkThroughWallCheckBox.Checked = Settings.EnableWalkThroughWall;

            // 诱惑之光（20261105）
            TameSuccessRateUpDown.Value = Clamp(Settings.TameSuccessRate, 0, 100);
            TameIgnoreLevelCheckBox.Checked = Settings.TameIgnoreLevelLimit;
            TameIgnoreHpCheckBox.Checked = Settings.TameIgnoreHpPenalty;
            TameIgnoreUntameableCheckBox.Checked = Settings.TameIgnoreUntameable;
            SyncTameUi();

            // 地面物品 / 尸体停留时间
            itemTimeOutTextBox.Text = Settings.ItemTimeOut.ToString();
            playerDiedItemTimeOutTextBox.Text = Settings.PlayerDiedItemTimeOut.ToString();
            corpseTimeTextBox.Text = Settings.CorpseTime.ToString();

            SaveDelayTextBox.Text = Settings.SaveDelay.ToString();

            ServerVersionLabel.Text = Application.ProductVersion;
            DBVersionLabel.Text = MirEnvir.Envir.LoadVersion.ToString() + ((MirEnvir.Envir.LoadVersion < MirEnvir.Envir.Version) ? " (需要更新)" : "");

            // 总开关联动：不允许离线挂机时，下面两个免损耗子项置灰
            OfflineAfkEnabledCheckBox.CheckedChanged += (o, e) => SyncOfflineAfkUi();
            SyncOfflineAfkUi();
        }

        /// <summary>
        /// 离线挂机总开关联动（10-26）：只切子项的可用性，不动它们的勾选值 ——
        /// 这样临时关掉总开关再打开，之前配好的免损耗设置还在。
        /// </summary>
        private void SyncOfflineAfkUi()
        {
            bool on = OfflineAfkEnabledCheckBox.Checked;

            OfflineAfkNoConsumableCheckBox.Enabled = on;
            OfflineAfkNoEquipCheckBox.Enabled = on;
        }

        // 20261105 诱惑之光页的控件（代码构建，见 BuildTameTab）
        private NumericUpDown TameSuccessRateUpDown;
        private CheckBox TameIgnoreLevelCheckBox;
        private CheckBox TameIgnoreHpCheckBox;
        private CheckBox TameIgnoreUntameableCheckBox;
        private Label TameRateHintLabel;

        /// <summary>
        /// 20261105 「其他选项」里新增的第 6 页：诱惑之光参数。
        /// 用代码构建、不改 Designer 文件，好处是这个页面的控件全集中在这里，
        /// 后面要加/减项一眼能看完，也不会被设计器重新生成时覆盖。
        /// </summary>
        private void BuildTameTab()
        {
            var page = new TabPage
            {
                Name = "tabPage6",
                Text = "诱惑之光",
                Padding = new Padding(5, 7, 5, 7),
                UseVisualStyleBackColor = true,
            };

            // ---------- 召唤成功率 ----------
            var rateGroup = new GroupBox
            {
                Text = "召唤成功率",
                Location = new Point(12, 8),
                Size = new Size(448, 126),
            };

            var rateLabel = new Label
            {
                Text = "每次施法的成功概率",
                AutoSize = true,
                Location = new Point(14, 26),
            };

            TameSuccessRateUpDown = new NumericUpDown
            {
                Location = new Point(122, 23),
                Size = new Size(72, 26),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                TextAlign = HorizontalAlignment.Center,
            };

            var percentLabel = new Label
            {
                Text = "%",
                AutoSize = true,
                Location = new Point(200, 27),
            };

            var quickButton = new Button
            {
                Text = "秒招（设为 100）",
                Location = new Point(236, 21),
                Size = new Size(128, 29),
                UseVisualStyleBackColor = true,
            };
            quickButton.Click += (o, e) =>
            {
                TameSuccessRateUpDown.Value = 100;
                SyncTameUi();
            };

            TameRateHintLabel = new Label
            {
                AutoSize = false,
                Location = new Point(14, 60),
                Size = new Size(420, 58),
                ForeColor = Color.DimGray,
            };

            rateGroup.Controls.Add(rateLabel);
            rateGroup.Controls.Add(TameSuccessRateUpDown);
            rateGroup.Controls.Add(percentLabel);
            rateGroup.Controls.Add(quickButton);
            rateGroup.Controls.Add(TameRateHintLabel);

            // ---------- 召唤限制 ----------
            var limitGroup = new GroupBox
            {
                Text = "召唤限制",
                Location = new Point(12, 138),
                Size = new Size(448, 178),
            };

            TameIgnoreLevelCheckBox = new CheckBox
            {
                Text = "允许召唤高等级怪物（无视「自身等级 + 2」）",
                AutoSize = true,
                Location = new Point(14, 24),
            };

            TameIgnoreHpCheckBox = new CheckBox
            {
                Text = "允许召唤高血量怪物（无视血量抗性）",
                AutoSize = true,
                Location = new Point(14, 54),
            };

            TameIgnoreUntameableCheckBox = new CheckBox
            {
                Text = "允许召唤「不可诱惑」的怪（BOSS 等，城门/城墙除外）",
                AutoSize = true,
                Location = new Point(14, 84),
            };

            var limitHint = new Label
            {
                Text = "※ 库里绝大多数怪（含全部 BOSS）都标了「不可诱惑」，要招它们必须勾上面第三项。\r\n"
                     + "※ 宠物数量上限仍按技能等级走，秒招也不会突破。",
                AutoSize = false,
                Location = new Point(14, 112),
                Size = new Size(420, 56),
                ForeColor = Color.DimGray,
            };

            limitGroup.Controls.Add(TameIgnoreLevelCheckBox);
            limitGroup.Controls.Add(TameIgnoreHpCheckBox);
            limitGroup.Controls.Add(TameIgnoreUntameableCheckBox);
            limitGroup.Controls.Add(limitHint);

            // ---------- 底部说明 ----------
            var footNote = new Label
            {
                Text = "三项写入 Configs\\Setup.ini 的 [Optional]，保存后立即生效。\r\n"
                     + "只影响法师「诱惑之光」，不改数据库、不动存档。",
                AutoSize = false,
                Location = new Point(18, 322),
                Size = new Size(440, 50),
                ForeColor = Color.DimGray,
            };

            page.Controls.Add(rateGroup);
            page.Controls.Add(limitGroup);
            page.Controls.Add(footNote);

            configTabs.Controls.Add(page);

            TameSuccessRateUpDown.ValueChanged += (o, e) => SyncTameUi();
        }

        /// <summary>
        /// 成功率输入框的即时提示：把「0 / 中间值 / 100」三种含义直接写在页面上，
        /// 省得以后忘了 0 是原版随机、100 是秒招。
        /// </summary>
        private void SyncTameUi()
        {
            if (TameRateHintLabel == null || TameSuccessRateUpDown == null) return;

            int rate = (int)TameSuccessRateUpDown.Value;

            if (rate <= 0)
            {
                TameRateHintLabel.Text = "0 = 原版随机（默认）。命中、麻痹、抵抗、血量抗性四层判定都要过一遍，"
                                       + "高等级高血量的怪基本招不到。";
                TameRateHintLabel.ForeColor = Color.DimGray;
            }
            else if (rate >= 100)
            {
                TameRateHintLabel.Text = "100 = 必定成功（秒招）。每次施法都把射程内选中的那只怪招过来，"
                                       + "只剩宠物上限和「不可诱惑」标记会拦（要招 BOSS 记得勾第三项）。";
                TameRateHintLabel.ForeColor = Color.FromArgb(0, 130, 60);
            }
            else
            {
                TameRateHintLabel.Text = "每次施法 " + rate + "% 成功。值越大越好招，"
                                       + "调到 100 就是秒招。";
                TameRateHintLabel.ForeColor = Color.FromArgb(170, 95, 0);
            }
        }

        private void ConfigForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Settings.Save();
            Settings.LoadVersion();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            Save();

            // 勾了假人却把数量配成 0，界面上不会报错，但一个假人都不会出现——这里提前说一声
            if (Settings.FakePlayerEnabled && Settings.FakePlayerCount <= 0)
            {
                MessageBox.Show("假人系统已勾选，但生成数量配置为 0，启动后不会出现任何假人。\r\n请先把 Configs\\Setup.ini 里 [FakePlayer] Count 改成需要的数量。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            Close();
        }

        public void Save()
        {
            Settings.VersionPath = VPathTextBox.Text;
            Settings.CheckVersion = VersionCheckBox.Checked;

            // 版本功能设置（非法输入保留原值）
            Settings.Multithreaded = MultiThreadCheckBox.Checked;
            Settings.TestServer = TestServerCheckBox.Checked;
            Settings.DropGold = !GoldDirectCheckBox.Checked;    //勾选=直接入包 → DropGold=false

            // 离线挂机：总开关 + 两项免损耗。全是运行时逐次读取的静态值，
            // 保存即生效；关掉总开关后，已在挂机的角色会在下一帧被服务端收回（Envir.Process 巡检）。
            Settings.OfflineAfkEnabled = OfflineAfkEnabledCheckBox.Checked;
            Settings.OfflineAfkNoConsumableDura = OfflineAfkNoConsumableCheckBox.Checked;
            Settings.OfflineAfkNoEquipDura = OfflineAfkNoEquipCheckBox.Checked;

            // 假人系统：启停要创建/销毁角色、往地图里撒对象，只能由游戏主线程做，
            // 所以这里只置标记，下一帧由 FakePlayerManager.Process 真正执行（无需重启服务端）。
            bool fakePlayerBefore = Settings.FakePlayerEnabled;
            Settings.FakePlayerEnabled = FakePlayerEnabledCheckBox.Checked;
            if (Settings.FakePlayerEnabled != fakePlayerBefore)
                FakePlayerManager.RestartPending = true;

            float tempFloat;
            if (float.TryParse(ExpRateTextBox.Text, out tempFloat) && tempFloat >= 0)
                Settings.ExpRate = tempFloat;
            if (float.TryParse(DropRateTextBox.Text, out tempFloat) && tempFloat >= 0)
                Settings.DropRate = tempFloat;
            if (float.TryParse(HighLevelRateTextBox.Text, out tempFloat) && tempFloat >= 0)
                Settings.HighLevelExpRate = tempFloat;

            int tempInt2;
            if (int.TryParse(DeathExpTextBox.Text, out tempInt2))
                Settings.DeathExpLossRate = Math.Max(0, Math.Min(100, tempInt2));

            IPAddress tempIP;
            if (IPAddress.TryParse(IPAddressTextBox.Text, out tempIP))
                Settings.IPAddress = tempIP.ToString();

            Settings.StartHTTPService = StartHTTPCheckBox.Checked;
            Settings.EnableResourceService = EnableResourceCheckBox.Checked;

            // 微端资源路径：之前界面没这个输入框，手动改 Setup.ini 会在保存设置时被写回旧值。
            // 留空则保留原值（相对路径基于服务端目录，如 .\ClientResources\）。
            if (!string.IsNullOrWhiteSpace(ResourcePathTextBox.Text))
                Settings.ResourcePath = ResourcePathTextBox.Text.Trim();

            // 允许任意 IP 下载微端资源。关着的话非受信 IP 请求 /res 会拿到 notrusted:<IP> 文本，
            // 客户端会把它当成资源清单，导致微端整个不可用。
            Settings.ResourceAllowAnyIP = ResourceAllowAnyIPCheckBox.Checked;

            if (tryParseHttp())
                Settings.HTTPIPAddress = HTTPIPAddressTextBox.Text.ToString();

            if (tryParseTrustedHttp())
                Settings.HTTPTrustedIPAddress = HTTPTrustedIPAddressTextBox.Text.ToString();

            ushort tempshort;
            int tempint;

            if (ushort.TryParse(PortTextBox.Text, out tempshort))
                Settings.Port = tempshort;

            if (ushort.TryParse(TimeOutTextBox.Text, out tempshort))
                Settings.TimeOut = tempshort;

            if (ushort.TryParse(MaxUserTextBox.Text, out tempshort))
                Settings.MaxUser = tempshort;

            if (ushort.TryParse(RelogDelayTextBox.Text, out tempshort))
                Settings.RelogDelay = tempshort;

            if (ushort.TryParse(SaveDelayTextBox.Text, out tempshort))
                Settings.SaveDelay = tempshort;

            Settings.AllowNewAccount = AccountCheckBox.Checked;
            Settings.AllowChangePassword = PasswordCheckBox.Checked;
            Settings.AllowLogin = LoginCheckBox.Checked;
            Settings.AllowNewCharacter = NCharacterCheckBox.Checked;
            Settings.AllowDeleteCharacter = DCharacterCheckBox.Checked;
            Settings.AllowStartGame = StartGameCheckBox.Checked;
            Settings.AllowCreateAssassin = AllowAssassinCheckBox.Checked;
            Settings.AllowCreateArcher = AllowArcherCheckBox.Checked;

            if (int.TryParse(Resolution_textbox.Text, out tempint))
                Settings.AllowedResolution = tempint;

            Settings.SafeZoneBorder = SafeZoneBorderCheckBox.Checked;
            Settings.SafeZoneHealing = SafeZoneHealingCheckBox.Checked;
            Settings.GameMasterEffect = gameMasterEffect_CheckBox.Checked;
            if (int.TryParse(lineMessageTimeTextBox.Text, out tempint))
                Settings.LineMessageTimer = tempint;

            // 穿行辅助：4 档独立开关（保存后立即生效，移动判定每次都会读这几项）
            Settings.EnableWalkThrough = WalkThroughCheckBox.Checked;
            Settings.EnableWalkThroughMonster = WalkThroughMonsterCheckBox.Checked;
            Settings.EnableWalkThroughNPC = WalkThroughNPCCheckBox.Checked;
            Settings.EnableWalkThroughWall = WalkThroughWallCheckBox.Checked;

            // 诱惑之光（20261105）：这三个都是静态值，保存即生效 ——
            // 玩家下一次施法就会走新参数，不用重启服务端、也不用重登。
            Settings.TameSuccessRate = Clamp((int)TameSuccessRateUpDown.Value, 0, 100);
            Settings.TameIgnoreLevelLimit = TameIgnoreLevelCheckBox.Checked;
            Settings.TameIgnoreHpPenalty = TameIgnoreHpCheckBox.Checked;
            Settings.TameIgnoreUntameable = TameIgnoreUntameableCheckBox.Checked;

            // 地面物品 / 尸体停留时间（越界一律夹到合法区间，避免手滑写成 0 或天文数字）
            Settings.ItemTimeOut = Clamp(ParseInt(itemTimeOutTextBox.Text, Settings.ItemTimeOut), 1, 10080);
            Settings.PlayerDiedItemTimeOut = Clamp(ParseInt(playerDiedItemTimeOutTextBox.Text, Settings.PlayerDiedItemTimeOut), 1, 10080);
            Settings.CorpseTime = Clamp(ParseInt(corpseTimeTextBox.Text, Settings.CorpseTime), 1, 3600);
        }

        /// <summary>文本框解析失败时保留旧值（不把配置写坏）。</summary>
        private static int ParseInt(string text, int fallback)
        {
            int value;
            return int.TryParse(text.Trim(), out value) ? value : fallback;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>时间类输入框的即时校验：非数字标红（保存时另有区间夹取）。</summary>
        private void CheckNumber(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;

            int temp;
            ActiveControl.BackColor = !int.TryParse(ActiveControl.Text.Trim(), out temp) ? Color.Red : SystemColors.Window;
        }

        private void IPAddressCheck(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;

            IPAddress temp;

            ActiveControl.BackColor = !IPAddress.TryParse(ActiveControl.Text, out temp) ? Color.Red : SystemColors.Window;
        }

        private void CheckUShort(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;

            ushort temp;

            ActiveControl.BackColor = !ushort.TryParse(ActiveControl.Text, out temp) ? Color.Red : SystemColors.Window;
        }

        private void VPathBrowseButton_Click(object sender, EventArgs e)
        {
            if (VPathDialog.ShowDialog() == DialogResult.OK)
            {
                VPathTextBox.Text = string.Join(",", VPathDialog.FileNames);
            }
        }

        private void Resolution_textbox_TextChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;

            int temp;

            ActiveControl.BackColor = !int.TryParse(ActiveControl.Text, out temp) ? Color.Red : SystemColors.Window;

        }

        private void tabPage3_Click(object sender, EventArgs e)
        {

        }

        private void SafeZoneBorderCheckBox_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void SafeZoneHealingCheckBox_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void HTTPIPAddressTextBox_TextChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;
            ActiveControl.BackColor = !tryParseHttp() ? Color.Red : SystemColors.Window;
        }


        private void HTTPTrustedIPAddressTextBox_TextChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;
            ActiveControl.BackColor = !tryParseTrustedHttp() ? Color.Red : SystemColors.Window;
        }

        bool tryParseHttp()
        {
            if ((HTTPIPAddressTextBox.Text.StartsWith("http://") || HTTPIPAddressTextBox.Text.StartsWith("https://")) && HTTPIPAddressTextBox.Text.EndsWith("/"))
            {
                return true;
            }
            return false;
        }

        bool tryParseTrustedHttp()
        {
            string pattern = @"[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}";
            return Regex.IsMatch(HTTPTrustedIPAddressTextBox.Text, pattern);
        }

        private void StartHTTPCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.StartHTTPService = StartHTTPCheckBox.Checked;
        }

        /// <summary>
        /// 微端资源服务。它必须有一个 HTTP 监听才能把资源下发给玩家，
        /// 所以取消「启用HTTP服务」并不会关掉监听 —— 关的只是注册/改名/广播这些管理接口。
        /// </summary>
        private void EnableResourceCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.EnableResourceService = EnableResourceCheckBox.Checked;
        }

        private void ResourceAllowAnyIPCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.ResourceAllowAnyIP = ResourceAllowAnyIPCheckBox.Checked;
        }
    }
}
