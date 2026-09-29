using Client;
using System.Resources;
using System.Reflection;
using Client.Resolution;

namespace Launcher
{

    public partial class Config : Form
    {
        // 分辨率档位 -> 单选框/文字控件。
        // 用字典驱动，新增分辨率档位只要在 BuildResolutionOptions 的表里加一行，不必改 designer。
        private readonly Dictionary<eSupportedResolution, PictureBox> _resRadios = new Dictionary<eSupportedResolution, PictureBox>();
        private readonly Dictionary<eSupportedResolution, Label> _resLabels = new Dictionary<eSupportedResolution, Label>();
        private bool _resOptionsBuilt;

        public Config()
        {
            InitializeComponent();
        }

        private void Config_Load(object sender, EventArgs e)
        {
            this.label10.Text = GameLanguage.Resolution;
            this.AutoStart_label.Text = GameLanguage.Autostart;
            this.ID_l.Text = GameLanguage.Usrname;
            this.Password_l.Text = GameLanguage.Password;

            BuildResolutionOptions();
            DrawSupportedResolutions();
        }

        /// <summary>
        /// 建立分辨率选项列表。
        /// 前 4 档复用 designer 里已有控件，其余档位按相同坐标规律动态生成，
        /// 这样扩充分辨率不需要动 designer 文件、也不用担心漏掉 BeginInit/Controls.Add。
        /// </summary>
        private void BuildResolutionOptions()
        {
            if (_resOptionsBuilt) return;
            _resOptionsBuilt = true;

            _resRadios[eSupportedResolution.w1024h768] = Res2_pb;
            _resLabels[eSupportedResolution.w1024h768] = label2;
            _resRadios[eSupportedResolution.w1280h720] = Res4_pb;
            _resLabels[eSupportedResolution.w1280h720] = label5;
            _resRadios[eSupportedResolution.w1366h768] = Res3_pb;
            _resLabels[eSupportedResolution.w1366h768] = label3;
            _resRadios[eSupportedResolution.w1920h1080] = Res5_pb;
            _resLabels[eSupportedResolution.w1920h1080] = label1;

            // designer 里这一项写的是 1280x800，与枚举名和实际窗口尺寸都不符，统一成 1280x720
            label5.Text = "1280x720";

            AddResolutionOption(eSupportedResolution.w1600h900, "1600x900", 159);
            AddResolutionOption(eSupportedResolution.w2560h1440, "2560x1440", 184);
        }

        private void AddResolutionOption(eSupportedResolution res, string caption, int y)
        {
            var radio = new PictureBox
            {
                Image = Client.Resources.Images.Radio_Unactive,
                Location = new Point(124, y),
                Margin = new Padding(5, 4, 5, 4),
                Name = "Res_" + res,
                Size = new Size(14, 16),
                TabStop = false,
                BackColor = Color.Transparent
            };
            radio.Click += (o, args) => resolutionChoice(res);

            var label = new Label
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                Font = new Font("Calibri", 8.25F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.Gray,
                Location = new Point(143, y + 2),
                Margin = new Padding(5, 0, 5, 0),
                Name = "ResLabel_" + res,
                Text = caption
            };
            label.Click += (o, args) => resolutionChoice(res);

            Controls.Add(radio);
            Controls.Add(label);

            _resRadios[res] = radio;
            _resLabels[res] = label;
        }

        private void Res1_pb_Click(object sender, EventArgs e)
        {
            resolutionChoice(eSupportedResolution.w1024h768);

        }

        public void resolutionChoice(eSupportedResolution res)
        {
            foreach (var radio in _resRadios.Values)
                radio.Image = Client.Resources.Images.Radio_Unactive;

            if (_resRadios.TryGetValue(res, out var active))
                active.Image = Client.Resources.Images.Config_Radio_On;

            Settings.Resolution = (int)res;
        }

        private void Res2_pb_Click(object sender, EventArgs e)
        {
            resolutionChoice(eSupportedResolution.w1024h768);
        }

        private void Res3_pb_Click(object sender, EventArgs e)
        {
            resolutionChoice(eSupportedResolution.w1366h768);
        }

        private void Config_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible)
            {
                AccountLogin_txt.Text = Settings.AccountID;
                AccountPass_txt.Text = Settings.Password;
                resolutionChoice((eSupportedResolution)Settings.Resolution);

                Fullscreen_pb.Image = Settings.FullScreen
                    ? Client.Resources.Images.Config_Check_On
                    : Client.Resources.Images.Config_Check_Off1;

                FPScap_pb.Image = Settings.FPSCap
                    ? Client.Resources.Images.Config_Check_On
                    : Client.Resources.Images.Config_Check_Off1;

                OnTop_pb.Image = Settings.TopMost
                    ? Client.Resources.Images.Config_Check_On
                    : Client.Resources.Images.Config_Check_Off1;

                AutoStart_pb.Image = Settings.P_AutoStart
                    ? Client.Resources.Images.Config_Check_On
                    : Client.Resources.Images.Config_Check_Off1;

                this.ActiveControl = label4;
            }
            else
            {             
                Settings.AccountID = AccountLogin_txt.Text;
                Settings.Password = AccountPass_txt.Text;
                Settings.Save();
            }
        }

        private void AccountLogin_txt_TextChanged(object sender, EventArgs e)
        {
            if (AccountLogin_txt.Text == string.Empty) ID_l.Visible = true;
            else ID_l.Visible = false;
        }

        private void AccountPass_txt_TextChanged(object sender, EventArgs e)
        {
            if (AccountPass_txt.Text == string.Empty) Password_l.Visible = true;
            else Password_l.Visible = false;
        }

        private void AccountLogin_txt_Click(object sender, EventArgs e)
        {
            ID_l.Visible = false;
            AccountLogin_txt.Focus();
        }

        private void AccountPass_txt_Click(object sender, EventArgs e)
        {
            Password_l.Visible = false;
            AccountPass_txt.Focus();
        }

        private void Config_Click(object sender, EventArgs e)
        {
            this.ActiveControl = label4;
        }

        private void Fullscreen_pb_Click(object sender, EventArgs e)
        {
            Settings.FullScreen = !Settings.FullScreen;

            Fullscreen_pb.Image = Settings.FullScreen
                    ? Client.Resources.Images.Config_Check_On
                    : Client.Resources.Images.Config_Check_Off1;
        }

        private void FPScap_pb_Click(object sender, EventArgs e)
        {
            Settings.FPSCap = !Settings.FPSCap;

            FPScap_pb.Image = Settings.FPSCap
                    ? Client.Resources.Images.Config_Check_On
                    : Client.Resources.Images.Config_Check_Off1;
        }

        private void OnTop_pb_Click(object sender, EventArgs e)
        {
            Settings.TopMost = !Settings.TopMost;

            OnTop_pb.Image = Settings.TopMost
                    ? Client.Resources.Images.Config_Check_On
                    : Client.Resources.Images.Config_Check_Off1;
        }

        private void AutoStart_pb_Click(object sender, EventArgs e)
        {
            Settings.P_AutoStart = !Settings.P_AutoStart;

            AutoStart_pb.Image = Settings.P_AutoStart
                    ? Client.Resources.Images.Config_Check_On
                    : Client.Resources.Images.Config_Check_Off1;
        }

        private void CleanFiles_pb_MouseDown(object sender, MouseEventArgs e)
        {
            CleanFiles_pb.Image = Client.Resources.Images.CheckF_Pressed;
        }

        private void CleanFiles_pb_MouseUp(object sender, MouseEventArgs e)
        {
            CleanFiles_pb.Image = Client.Resources.Images.CheckF_Base2;
        }

        private void CleanFiles_pb_MouseEnter(object sender, EventArgs e)
        {
            CleanFiles_pb.Image = Client.Resources.Images.CheckF_Hover;
        }

        private void CleanFiles_pb_MouseLeave(object sender, EventArgs e)
        {
            CleanFiles_pb.Image = Client.Resources.Images.CheckF_Base2;
        }

        private void CleanFiles_pb_Click(object sender, EventArgs e)
        {
            if (!Program.PForm.Launch_pb.Enabled) return;

            Program.PForm.Completed = false;
            Program.PForm.InterfaceTimer.Enabled = true;
            Program.PForm.CleanFiles = true;
            Program.PForm._workThread = new Thread(Program.PForm.Start) { IsBackground = true };
            Program.PForm._workThread.Start();
        }

        private void Res4_pb_Click(object sender, EventArgs e)
        {
            resolutionChoice(eSupportedResolution.w1280h720);
        }

        private void Res5_pb_Click(object sender, EventArgs e)
        {
            resolutionChoice(eSupportedResolution.w1920h1080);
        }

        private void DrawSupportedResolutions()
        {
            var supported = DisplayResolutions.DisplaySupportedResolutions;
            var desktop = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1024, 768);

            foreach (var kv in _resRadios)
            {
                var size = ResolutionHelper.GetSize(kv.Key);

                // 系统枚举到的显示模式直接可用；没枚举到但窗口尺寸不超过桌面时也放开，
                // 因为窗口模式下并不要求显示器原生支持该模式，否则高分辨率档位会无谓地变成灰色。
                bool ok = supported.Contains(kv.Key) ||
                          (size.Width <= desktop.Width && size.Height <= desktop.Height);

                kv.Value.Enabled = ok;

                if (_resLabels.TryGetValue(kv.Key, out var label))
                    label.ForeColor = ok ? Color.Gray : Color.Red;
            }
        }
    }
}
