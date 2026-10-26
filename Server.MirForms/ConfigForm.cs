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
