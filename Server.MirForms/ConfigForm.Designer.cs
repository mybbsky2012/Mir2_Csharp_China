namespace Server
{
    partial class ConfigForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            SaveButton = new Button();
            configTabs = new TabControl();
            tabPage1 = new TabPage();
            groupBox1 = new GroupBox();
            versionFuncGroupBox = new GroupBox();
            MultiThreadCheckBox = new CheckBox();
            TestServerCheckBox = new CheckBox();
            GoldDirectCheckBox = new CheckBox();
            OfflineAfkEnabledCheckBox = new CheckBox();
            OfflineAfkNoConsumableCheckBox = new CheckBox();
            OfflineAfkNoEquipCheckBox = new CheckBox();
            FakePlayerEnabledCheckBox = new CheckBox();
            ExpRateLabel = new Label();
            ExpRateTextBox = new TextBox();
            DropRateLabel = new Label();
            DropRateTextBox = new TextBox();
            HighLevelRateLabel = new Label();
            HighLevelRateTextBox = new TextBox();
            DeathExpLabel = new Label();
            DeathExpTextBox = new TextBox();
            label11 = new Label();
            DBVersionLabel = new Label();
            ServerVersionLabel = new Label();
            label10 = new Label();
            RelogDelayTextBox = new TextBox();
            label7 = new Label();
            VersionCheckBox = new CheckBox();
            VPathBrowseButton = new Button();
            VPathTextBox = new TextBox();
            label1 = new Label();
            tabPage2 = new TabPage();
            StartHTTPCheckBox = new CheckBox();
            EnableResourceCheckBox = new CheckBox();
            ResourcePathLabel = new Label();
            ResourcePathTextBox = new TextBox();
            ResourceAllowAnyIPCheckBox = new CheckBox();
            label15 = new Label();
            HTTPTrustedIPAddressTextBox = new TextBox();
            label14 = new Label();
            HTTPIPAddressTextBox = new TextBox();
            label13 = new Label();
            MaxUserTextBox = new TextBox();
            label5 = new Label();
            TimeOutTextBox = new TextBox();
            label4 = new Label();
            PortTextBox = new TextBox();
            label3 = new Label();
            IPAddressTextBox = new TextBox();
            label2 = new Label();
            tabPage3 = new TabPage();
            label9 = new Label();
            label8 = new Label();
            Resolution_textbox = new TextBox();
            AllowArcherCheckBox = new CheckBox();
            AllowAssassinCheckBox = new CheckBox();
            StartGameCheckBox = new CheckBox();
            DCharacterCheckBox = new CheckBox();
            NCharacterCheckBox = new CheckBox();
            LoginCheckBox = new CheckBox();
            PasswordCheckBox = new CheckBox();
            AccountCheckBox = new CheckBox();
            tabPage4 = new TabPage();
            label12 = new Label();
            SaveDelayTextBox = new TextBox();
            label6 = new Label();
            tabPage5 = new TabPage();
            label16 = new Label();
            lineMessageTimeTextBox = new TextBox();
            label17 = new Label();
            gameMasterEffect_CheckBox = new CheckBox();
            SafeZoneHealingCheckBox = new CheckBox();
            SafeZoneBorderCheckBox = new CheckBox();
            label18 = new Label();
            itemTimeOutTextBox = new TextBox();
            label19 = new Label();
            label20 = new Label();
            playerDiedItemTimeOutTextBox = new TextBox();
            label21 = new Label();
            label22 = new Label();
            corpseTimeTextBox = new TextBox();
            label23 = new Label();
            WalkThroughGroupLabel = new Label();
            WalkThroughCheckBox = new CheckBox();
            WalkThroughMonsterCheckBox = new CheckBox();
            WalkThroughNPCCheckBox = new CheckBox();
            WalkThroughWallCheckBox = new CheckBox();
            VPathDialog = new OpenFileDialog();
            configTabs.SuspendLayout();
            tabPage1.SuspendLayout();
            groupBox1.SuspendLayout();
            tabPage2.SuspendLayout();
            tabPage3.SuspendLayout();
            tabPage4.SuspendLayout();
            tabPage5.SuspendLayout();
            SuspendLayout();
            // 
            // SaveButton
            // 
            SaveButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            SaveButton.Location = new Point(411, 452);
            SaveButton.Margin = new Padding(5, 7, 5, 7);
            SaveButton.Name = "SaveButton";
            SaveButton.Size = new Size(88, 30);
            SaveButton.TabIndex = 6;
            SaveButton.Text = "关闭";
            SaveButton.UseVisualStyleBackColor = true;
            SaveButton.Click += SaveButton_Click;
            // 
            // configTabs
            // 
            configTabs.Controls.Add(tabPage1);
            configTabs.Controls.Add(tabPage2);
            configTabs.Controls.Add(tabPage3);
            configTabs.Controls.Add(tabPage4);
            configTabs.Controls.Add(tabPage5);
            configTabs.Location = new Point(14, 16);
            configTabs.Margin = new Padding(5, 7, 5, 7);
            configTabs.Name = "configTabs";
            configTabs.SelectedIndex = 0;
            configTabs.Size = new Size(484, 426);
            configTabs.TabIndex = 5;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(groupBox1);
            tabPage1.Controls.Add(versionFuncGroupBox);
            tabPage1.Controls.Add(MultiThreadCheckBox);
            tabPage1.Controls.Add(TestServerCheckBox);
            tabPage1.Controls.Add(GoldDirectCheckBox);
            tabPage1.Controls.Add(ExpRateLabel);
            tabPage1.Controls.Add(ExpRateTextBox);
            tabPage1.Controls.Add(DropRateLabel);
            tabPage1.Controls.Add(DropRateTextBox);
            tabPage1.Controls.Add(HighLevelRateLabel);
            tabPage1.Controls.Add(HighLevelRateTextBox);
            tabPage1.Controls.Add(DeathExpLabel);
            tabPage1.Controls.Add(DeathExpTextBox);
            tabPage1.Controls.Add(RelogDelayTextBox);
            tabPage1.Controls.Add(label7);
            tabPage1.Controls.Add(VersionCheckBox);
            tabPage1.Controls.Add(VPathBrowseButton);
            tabPage1.Controls.Add(VPathTextBox);
            tabPage1.Controls.Add(label1);
            tabPage1.Location = new Point(4, 26);
            tabPage1.Margin = new Padding(5, 7, 5, 7);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(5, 7, 5, 7);
            tabPage1.Size = new Size(476, 396);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "版本信息";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(label11);
            groupBox1.Controls.Add(DBVersionLabel);
            groupBox1.Controls.Add(ServerVersionLabel);
            groupBox1.Controls.Add(label10);
            groupBox1.Location = new Point(7, 300);
            groupBox1.Margin = new Padding(5, 7, 5, 7);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(5, 7, 5, 7);
            groupBox1.Size = new Size(461, 84);
            groupBox1.TabIndex = 25;
            groupBox1.TabStop = false;
            groupBox1.Text = "服务器版本信息";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new Point(29, 55);
            label11.Margin = new Padding(5, 0, 5, 0);
            label11.Name = "label11";
            label11.Size = new Size(44, 17);
            label11.TabIndex = 23;
            label11.Text = "数据库";
            // 
            // DBVersionLabel
            // 
            DBVersionLabel.AutoSize = true;
            DBVersionLabel.Location = new Point(89, 55);
            DBVersionLabel.Margin = new Padding(5, 0, 5, 0);
            DBVersionLabel.Name = "DBVersionLabel";
            DBVersionLabel.Size = new Size(52, 17);
            DBVersionLabel.TabIndex = 24;
            DBVersionLabel.Text = "Version";
            // 
            // ServerVersionLabel
            // 
            ServerVersionLabel.AutoSize = true;
            ServerVersionLabel.Location = new Point(89, 27);
            ServerVersionLabel.Margin = new Padding(5, 0, 5, 0);
            ServerVersionLabel.Name = "ServerVersionLabel";
            ServerVersionLabel.Size = new Size(52, 17);
            ServerVersionLabel.TabIndex = 7;
            ServerVersionLabel.Text = "Version";
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(29, 28);
            label10.Margin = new Padding(5, 0, 5, 0);
            label10.Name = "label10";
            label10.Size = new Size(44, 17);
            label10.TabIndex = 22;
            label10.Text = "服务器";
            // 
            // RelogDelayTextBox
            // 
            RelogDelayTextBox.Location = new Point(320, 58);
            RelogDelayTextBox.Margin = new Padding(5, 7, 5, 7);
            RelogDelayTextBox.MaxLength = 5;
            RelogDelayTextBox.Name = "RelogDelayTextBox";
            RelogDelayTextBox.Size = new Size(108, 23);
            RelogDelayTextBox.TabIndex = 21;
            RelogDelayTextBox.TextChanged += CheckUShort;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(240, 61);
            label7.Margin = new Padding(5, 0, 5, 0);
            label7.Name = "label7";
            label7.Size = new Size(80, 17);
            label7.TabIndex = 20;
            label7.Text = "重新登录延时";
            // 
            // VersionCheckBox
            // 
            VersionCheckBox.AutoSize = true;
            VersionCheckBox.Location = new Point(104, 58);
            VersionCheckBox.Margin = new Padding(5, 7, 5, 7);
            VersionCheckBox.Name = "VersionCheckBox";
            VersionCheckBox.Size = new Size(111, 21);
            VersionCheckBox.TabIndex = 3;
            VersionCheckBox.Text = "检查登录器版本";
            VersionCheckBox.UseVisualStyleBackColor = true;
            // 
            // VPathBrowseButton
            // 
            VPathBrowseButton.Location = new Point(433, 17);
            VPathBrowseButton.Margin = new Padding(5, 7, 5, 7);
            VPathBrowseButton.Name = "VPathBrowseButton";
            VPathBrowseButton.Size = new Size(33, 30);
            VPathBrowseButton.TabIndex = 2;
            VPathBrowseButton.Text = "...";
            VPathBrowseButton.UseVisualStyleBackColor = true;
            VPathBrowseButton.Click += VPathBrowseButton_Click;
            // 
            // VPathTextBox
            // 
            VPathTextBox.Location = new Point(104, 21);
            VPathTextBox.Margin = new Padding(5, 7, 5, 7);
            VPathTextBox.Name = "VPathTextBox";
            VPathTextBox.ReadOnly = true;
            VPathTextBox.Size = new Size(324, 23);
            VPathTextBox.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(34, 24);
            label1.Margin = new Padding(5, 0, 5, 0);
            label1.Name = "label1";
            label1.Size = new Size(68, 17);
            label1.TabIndex = 0;
            label1.Text = "登录器路径";
            //
            // versionFuncGroupBox（版本功能设置）
            //
            versionFuncGroupBox.Controls.Add(MultiThreadCheckBox);
            versionFuncGroupBox.Controls.Add(TestServerCheckBox);
            versionFuncGroupBox.Controls.Add(GoldDirectCheckBox);
            versionFuncGroupBox.Controls.Add(OfflineAfkEnabledCheckBox);
            versionFuncGroupBox.Controls.Add(OfflineAfkNoConsumableCheckBox);
            versionFuncGroupBox.Controls.Add(OfflineAfkNoEquipCheckBox);
            versionFuncGroupBox.Controls.Add(FakePlayerEnabledCheckBox);
            versionFuncGroupBox.Controls.Add(ExpRateLabel);
            versionFuncGroupBox.Controls.Add(ExpRateTextBox);
            versionFuncGroupBox.Controls.Add(DropRateLabel);
            versionFuncGroupBox.Controls.Add(DropRateTextBox);
            versionFuncGroupBox.Controls.Add(HighLevelRateLabel);
            versionFuncGroupBox.Controls.Add(HighLevelRateTextBox);
            versionFuncGroupBox.Controls.Add(DeathExpLabel);
            versionFuncGroupBox.Controls.Add(DeathExpTextBox);
            versionFuncGroupBox.Location = new Point(7, 100);
            versionFuncGroupBox.Name = "versionFuncGroupBox";
            versionFuncGroupBox.Size = new Size(461, 194);
            versionFuncGroupBox.TabIndex = 26;
            versionFuncGroupBox.TabStop = false;
            versionFuncGroupBox.Text = "版本功能设置";
            //
            // MultiThreadCheckBox
            //
            MultiThreadCheckBox.AutoSize = true;
            MultiThreadCheckBox.Location = new Point(20, 22);
            MultiThreadCheckBox.Name = "MultiThreadCheckBox";
            MultiThreadCheckBox.Size = new Size(99, 21);
            MultiThreadCheckBox.TabIndex = 0;
            MultiThreadCheckBox.Text = "启用多线程";
            MultiThreadCheckBox.UseVisualStyleBackColor = true;
            //
            // TestServerCheckBox
            //
            TestServerCheckBox.AutoSize = true;
            TestServerCheckBox.Location = new Point(20, 46);
            TestServerCheckBox.Name = "TestServerCheckBox";
            TestServerCheckBox.Size = new Size(168, 21);
            TestServerCheckBox.TabIndex = 1;
            TestServerCheckBox.Text = "开启测试(开放GM指令)";
            TestServerCheckBox.UseVisualStyleBackColor = true;
            //
            // GoldDirectCheckBox
            //
            GoldDirectCheckBox.AutoSize = true;
            GoldDirectCheckBox.Location = new Point(20, 70);
            GoldDirectCheckBox.Name = "GoldDirectCheckBox";
            GoldDirectCheckBox.Size = new Size(123, 21);
            GoldDirectCheckBox.TabIndex = 2;
            GoldDirectCheckBox.Text = "金币直接入包";
            GoldDirectCheckBox.UseVisualStyleBackColor = true;
            // 
            // OfflineAfkEnabledCheckBox
            // 
            OfflineAfkEnabledCheckBox.AutoSize = true;
            OfflineAfkEnabledCheckBox.Location = new Point(20, 94);
            OfflineAfkEnabledCheckBox.Name = "OfflineAfkEnabledCheckBox";
            OfflineAfkEnabledCheckBox.Size = new Size(150, 21);
            OfflineAfkEnabledCheckBox.TabIndex = 14;
            OfflineAfkEnabledCheckBox.Text = "允许离线挂机";
            OfflineAfkEnabledCheckBox.UseVisualStyleBackColor = true;
            // 
            // OfflineAfkNoConsumableCheckBox
            // 
            OfflineAfkNoConsumableCheckBox.AutoSize = true;
            OfflineAfkNoConsumableCheckBox.Location = new Point(20, 118);
            OfflineAfkNoConsumableCheckBox.Name = "OfflineAfkNoConsumableCheckBox";
            OfflineAfkNoConsumableCheckBox.Size = new Size(190, 21);
            OfflineAfkNoConsumableCheckBox.TabIndex = 11;
            OfflineAfkNoConsumableCheckBox.Text = "离线挂机不扣消耗品持久";
            OfflineAfkNoConsumableCheckBox.UseVisualStyleBackColor = true;
            // 
            // OfflineAfkNoEquipCheckBox
            // 
            OfflineAfkNoEquipCheckBox.AutoSize = true;
            OfflineAfkNoEquipCheckBox.Location = new Point(20, 142);
            OfflineAfkNoEquipCheckBox.Name = "OfflineAfkNoEquipCheckBox";
            OfflineAfkNoEquipCheckBox.Size = new Size(178, 21);
            OfflineAfkNoEquipCheckBox.TabIndex = 12;
            OfflineAfkNoEquipCheckBox.Text = "离线挂机不扣装备耐久";
            OfflineAfkNoEquipCheckBox.UseVisualStyleBackColor = true;
            // 
            // FakePlayerEnabledCheckBox
            // 
            FakePlayerEnabledCheckBox.AutoSize = true;
            FakePlayerEnabledCheckBox.Location = new Point(20, 166);
            FakePlayerEnabledCheckBox.Name = "FakePlayerEnabledCheckBox";
            FakePlayerEnabledCheckBox.Size = new Size(160, 21);
            FakePlayerEnabledCheckBox.TabIndex = 13;
            FakePlayerEnabledCheckBox.Text = "启用假人系统(AI玩家)";
            FakePlayerEnabledCheckBox.UseVisualStyleBackColor = true;
            //
            // ExpRateLabel
            //
            ExpRateLabel.AutoSize = true;
            ExpRateLabel.Location = new Point(225, 25);
            ExpRateLabel.Name = "ExpRateLabel";
            ExpRateLabel.Size = new Size(67, 17);
            ExpRateLabel.TabIndex = 3;
            ExpRateLabel.Text = "经验倍率";
            //
            // ExpRateTextBox
            //
            ExpRateTextBox.Location = new Point(320, 21);
            ExpRateTextBox.MaxLength = 6;
            ExpRateTextBox.Name = "ExpRateTextBox";
            ExpRateTextBox.Size = new Size(108, 23);
            ExpRateTextBox.TabIndex = 4;
            //
            // DropRateLabel
            //
            DropRateLabel.AutoSize = true;
            DropRateLabel.Location = new Point(225, 49);
            DropRateLabel.Name = "DropRateLabel";
            DropRateLabel.Size = new Size(67, 17);
            DropRateLabel.TabIndex = 5;
            DropRateLabel.Text = "掉率倍率";
            //
            // DropRateTextBox
            //
            DropRateTextBox.Location = new Point(320, 45);
            DropRateTextBox.MaxLength = 6;
            DropRateTextBox.Name = "DropRateTextBox";
            DropRateTextBox.Size = new Size(108, 23);
            DropRateTextBox.TabIndex = 6;
            //
            // HighLevelRateLabel
            //
            HighLevelRateLabel.AutoSize = true;
            HighLevelRateLabel.Location = new Point(225, 73);
            HighLevelRateLabel.Name = "HighLevelRateLabel";
            HighLevelRateLabel.Size = new Size(90, 17);
            HighLevelRateLabel.TabIndex = 7;
            HighLevelRateLabel.Text = "60级以上倍率";
            //
            // HighLevelRateTextBox
            //
            HighLevelRateTextBox.Location = new Point(320, 69);
            HighLevelRateTextBox.MaxLength = 6;
            HighLevelRateTextBox.Name = "HighLevelRateTextBox";
            HighLevelRateTextBox.Size = new Size(108, 23);
            HighLevelRateTextBox.TabIndex = 8;
            //
            // DeathExpLabel
            //
            DeathExpLabel.AutoSize = true;
            DeathExpLabel.Location = new Point(225, 97);
            DeathExpLabel.Name = "DeathExpLabel";
            DeathExpLabel.Size = new Size(90, 17);
            DeathExpLabel.TabIndex = 9;
            DeathExpLabel.Text = "死亡掉经验%";
            //
            // DeathExpTextBox
            //
            DeathExpTextBox.Location = new Point(320, 93);
            DeathExpTextBox.MaxLength = 3;
            DeathExpTextBox.Name = "DeathExpTextBox";
            DeathExpTextBox.Size = new Size(108, 23);
            DeathExpTextBox.TabIndex = 10;
            //
            // 
            tabPage2.Controls.Add(StartHTTPCheckBox);
            tabPage2.Controls.Add(EnableResourceCheckBox);
            tabPage2.Controls.Add(ResourcePathLabel);
            tabPage2.Controls.Add(ResourcePathTextBox);
            tabPage2.Controls.Add(ResourceAllowAnyIPCheckBox);
            tabPage2.Controls.Add(label15);
            tabPage2.Controls.Add(HTTPTrustedIPAddressTextBox);
            tabPage2.Controls.Add(label14);
            tabPage2.Controls.Add(HTTPIPAddressTextBox);
            tabPage2.Controls.Add(label13);
            tabPage2.Controls.Add(MaxUserTextBox);
            tabPage2.Controls.Add(label5);
            tabPage2.Controls.Add(TimeOutTextBox);
            tabPage2.Controls.Add(label4);
            tabPage2.Controls.Add(PortTextBox);
            tabPage2.Controls.Add(label3);
            tabPage2.Controls.Add(IPAddressTextBox);
            tabPage2.Controls.Add(label2);
            tabPage2.Location = new Point(4, 26);
            tabPage2.Margin = new Padding(5, 7, 5, 7);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(5, 7, 5, 7);
            tabPage2.Size = new Size(476, 396);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "网络";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // StartHTTPCheckBox
            // 
            StartHTTPCheckBox.AutoSize = true;
            StartHTTPCheckBox.Location = new Point(28, 204);
            StartHTTPCheckBox.Margin = new Padding(5, 7, 5, 7);
            StartHTTPCheckBox.Name = "StartHTTPCheckBox";
            StartHTTPCheckBox.Size = new Size(105, 21);
            StartHTTPCheckBox.TabIndex = 23;
            StartHTTPCheckBox.Text = "启用HTTP服务";
            StartHTTPCheckBox.UseVisualStyleBackColor = true;
            StartHTTPCheckBox.CheckedChanged += StartHTTPCheckBox_CheckedChanged;
            // 
            // EnableResourceCheckBox
            // 
            EnableResourceCheckBox.AutoSize = true;
            EnableResourceCheckBox.Location = new Point(152, 204);
            EnableResourceCheckBox.Margin = new Padding(5, 7, 5, 7);
            EnableResourceCheckBox.Name = "EnableResourceCheckBox";
            EnableResourceCheckBox.Size = new Size(200, 21);
            EnableResourceCheckBox.TabIndex = 24;
            EnableResourceCheckBox.Text = "启用微端资源服务(边玩边下载)";
            EnableResourceCheckBox.UseVisualStyleBackColor = true;
            EnableResourceCheckBox.CheckedChanged += EnableResourceCheckBox_CheckedChanged;
            // 
            // ResourcePathLabel
            // 
            ResourcePathLabel.AutoSize = true;
            ResourcePathLabel.Location = new Point(26, 355);
            ResourcePathLabel.Margin = new Padding(5, 0, 5, 0);
            ResourcePathLabel.Name = "ResourcePathLabel";
            ResourcePathLabel.TabIndex = 25;
            ResourcePathLabel.Text = "微端资源路径";
            // 
            // ResourcePathTextBox
            // 
            ResourcePathTextBox.Location = new Point(152, 352);
            ResourcePathTextBox.Margin = new Padding(5, 7, 5, 7);
            ResourcePathTextBox.Name = "ResourcePathTextBox";
            ResourcePathTextBox.Size = new Size(280, 23);
            ResourcePathTextBox.TabIndex = 26;
            // 
            // ResourceAllowAnyIPCheckBox
            // 
            ResourceAllowAnyIPCheckBox.AutoSize = true;
            ResourceAllowAnyIPCheckBox.Location = new Point(28, 165);
            ResourceAllowAnyIPCheckBox.Margin = new Padding(5, 7, 5, 7);
            ResourceAllowAnyIPCheckBox.Name = "ResourceAllowAnyIPCheckBox";
            ResourceAllowAnyIPCheckBox.Size = new Size(230, 21);
            ResourceAllowAnyIPCheckBox.TabIndex = 27;
            ResourceAllowAnyIPCheckBox.Text = "允许任意IP下载微端资源(微端必开)";
            ResourceAllowAnyIPCheckBox.UseVisualStyleBackColor = true;
            ResourceAllowAnyIPCheckBox.CheckedChanged += ResourceAllowAnyIPCheckBox_CheckedChanged;
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.Location = new Point(26, 329);
            label15.Margin = new Padding(5, 0, 5, 0);
            label15.Name = "label15";
            label15.Size = new Size(173, 17);
            label15.TabIndex = 22;
            label15.Text = "(HTTP 服务只允许受信任的 IP)";
            // 
            // HTTPTrustedIPAddressTextBox
            // 
            HTTPTrustedIPAddressTextBox.Location = new Point(152, 282);
            HTTPTrustedIPAddressTextBox.Margin = new Padding(5, 7, 5, 7);
            HTTPTrustedIPAddressTextBox.MaxLength = 30;
            HTTPTrustedIPAddressTextBox.Name = "HTTPTrustedIPAddressTextBox";
            HTTPTrustedIPAddressTextBox.Size = new Size(198, 23);
            HTTPTrustedIPAddressTextBox.TabIndex = 21;
            HTTPTrustedIPAddressTextBox.TextChanged += HTTPTrustedIPAddressTextBox_TextChanged;
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.Location = new Point(40, 285);
            label14.Margin = new Padding(5, 0, 5, 0);
            label14.Name = "label14";
            label14.Size = new Size(109, 17);
            label14.TabIndex = 20;
            label14.Text = "HTTP 可信 IP 地址";
            // 
            // HTTPIPAddressTextBox
            // 
            HTTPIPAddressTextBox.Location = new Point(152, 239);
            HTTPIPAddressTextBox.Margin = new Padding(5, 7, 5, 7);
            HTTPIPAddressTextBox.MaxLength = 30;
            HTTPIPAddressTextBox.Name = "HTTPIPAddressTextBox";
            HTTPIPAddressTextBox.Size = new Size(198, 23);
            HTTPIPAddressTextBox.TabIndex = 19;
            HTTPIPAddressTextBox.TextChanged += HTTPIPAddressTextBox_TextChanged;
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Location = new Point(72, 242);
            label13.Margin = new Padding(5, 0, 5, 0);
            label13.Name = "label13";
            label13.Size = new Size(77, 17);
            label13.TabIndex = 18;
            label13.Text = "HTTP IP地址";
            // 
            // MaxUserTextBox
            // 
            MaxUserTextBox.Location = new Point(104, 123);
            MaxUserTextBox.Margin = new Padding(5, 7, 5, 7);
            MaxUserTextBox.MaxLength = 5;
            MaxUserTextBox.Name = "MaxUserTextBox";
            MaxUserTextBox.Size = new Size(48, 23);
            MaxUserTextBox.TabIndex = 17;
            MaxUserTextBox.TextChanged += CheckUShort;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(33, 126);
            label5.Margin = new Padding(5, 0, 5, 0);
            label5.Name = "label5";
            label5.Size = new Size(68, 17);
            label5.TabIndex = 16;
            label5.Text = "最大登录数";
            // 
            // TimeOutTextBox
            // 
            TimeOutTextBox.Location = new Point(104, 89);
            TimeOutTextBox.Margin = new Padding(5, 7, 5, 7);
            TimeOutTextBox.MaxLength = 5;
            TimeOutTextBox.Name = "TimeOutTextBox";
            TimeOutTextBox.Size = new Size(108, 23);
            TimeOutTextBox.TabIndex = 15;
            TimeOutTextBox.TextChanged += CheckUShort;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(45, 92);
            label4.Margin = new Padding(5, 0, 5, 0);
            label4.Name = "label4";
            label4.Size = new Size(56, 17);
            label4.TabIndex = 14;
            label4.Text = "连接超时";
            // 
            // PortTextBox
            // 
            PortTextBox.Location = new Point(104, 55);
            PortTextBox.Margin = new Padding(5, 7, 5, 7);
            PortTextBox.MaxLength = 5;
            PortTextBox.Name = "PortTextBox";
            PortTextBox.Size = new Size(48, 23);
            PortTextBox.TabIndex = 13;
            PortTextBox.TextChanged += CheckUShort;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(57, 58);
            label3.Margin = new Padding(5, 0, 5, 0);
            label3.Name = "label3";
            label3.Size = new Size(44, 17);
            label3.TabIndex = 12;
            label3.Text = "端口号";
            // 
            // IPAddressTextBox
            // 
            IPAddressTextBox.Location = new Point(104, 21);
            IPAddressTextBox.Margin = new Padding(5, 7, 5, 7);
            IPAddressTextBox.MaxLength = 15;
            IPAddressTextBox.Name = "IPAddressTextBox";
            IPAddressTextBox.Size = new Size(108, 23);
            IPAddressTextBox.TabIndex = 11;
            IPAddressTextBox.TextChanged += IPAddressCheck;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(58, 24);
            label2.Margin = new Padding(5, 0, 5, 0);
            label2.Name = "label2";
            label2.Size = new Size(43, 17);
            label2.TabIndex = 10;
            label2.Text = "IP地址";
            // 
            // tabPage3
            // 
            tabPage3.Controls.Add(label9);
            tabPage3.Controls.Add(label8);
            tabPage3.Controls.Add(Resolution_textbox);
            tabPage3.Controls.Add(AllowArcherCheckBox);
            tabPage3.Controls.Add(AllowAssassinCheckBox);
            tabPage3.Controls.Add(StartGameCheckBox);
            tabPage3.Controls.Add(DCharacterCheckBox);
            tabPage3.Controls.Add(NCharacterCheckBox);
            tabPage3.Controls.Add(LoginCheckBox);
            tabPage3.Controls.Add(PasswordCheckBox);
            tabPage3.Controls.Add(AccountCheckBox);
            tabPage3.Location = new Point(4, 26);
            tabPage3.Margin = new Padding(5, 7, 5, 7);
            tabPage3.Name = "tabPage3";
            tabPage3.Padding = new Padding(5, 7, 5, 7);
            tabPage3.Size = new Size(476, 396);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "权限";
            tabPage3.UseVisualStyleBackColor = true;
            tabPage3.Click += tabPage3_Click;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(77, 303);
            label9.Margin = new Padding(5, 0, 5, 0);
            label9.Name = "label9";
            label9.Size = new Size(92, 17);
            label9.TabIndex = 16;
            label9.Text = "允许最大分辨率";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(4, 4);
            label8.Margin = new Padding(5, 0, 5, 0);
            label8.Name = "label8";
            label8.Size = new Size(32, 17);
            label8.TabIndex = 15;
            label8.Text = "设置";
            // 
            // Resolution_textbox
            // 
            Resolution_textbox.Location = new Point(172, 300);
            Resolution_textbox.Margin = new Padding(5, 7, 5, 7);
            Resolution_textbox.Name = "Resolution_textbox";
            Resolution_textbox.Size = new Size(93, 23);
            Resolution_textbox.TabIndex = 14;
            Resolution_textbox.TextChanged += Resolution_textbox_TextChanged;
            // 
            // AllowArcherCheckBox
            // 
            AllowArcherCheckBox.AutoSize = true;
            AllowArcherCheckBox.Location = new Point(28, 258);
            AllowArcherCheckBox.Margin = new Padding(5, 7, 5, 7);
            AllowArcherCheckBox.Name = "AllowArcherCheckBox";
            AllowArcherCheckBox.Size = new Size(123, 21);
            AllowArcherCheckBox.TabIndex = 13;
            AllowArcherCheckBox.Text = "允许创建弓箭职业";
            AllowArcherCheckBox.UseVisualStyleBackColor = true;
            // 
            // AllowAssassinCheckBox
            // 
            AllowAssassinCheckBox.AutoSize = true;
            AllowAssassinCheckBox.Location = new Point(28, 227);
            AllowAssassinCheckBox.Margin = new Padding(5, 7, 5, 7);
            AllowAssassinCheckBox.Name = "AllowAssassinCheckBox";
            AllowAssassinCheckBox.Size = new Size(123, 21);
            AllowAssassinCheckBox.TabIndex = 12;
            AllowAssassinCheckBox.Text = "允许创建刺客职业";
            AllowAssassinCheckBox.UseVisualStyleBackColor = true;
            // 
            // StartGameCheckBox
            // 
            StartGameCheckBox.AutoSize = true;
            StartGameCheckBox.Location = new Point(28, 177);
            StartGameCheckBox.Margin = new Padding(5, 7, 5, 7);
            StartGameCheckBox.Name = "StartGameCheckBox";
            StartGameCheckBox.Size = new Size(123, 21);
            StartGameCheckBox.TabIndex = 11;
            StartGameCheckBox.Text = "允许角色登录游戏";
            StartGameCheckBox.UseVisualStyleBackColor = true;
            // 
            // DCharacterCheckBox
            // 
            DCharacterCheckBox.AutoSize = true;
            DCharacterCheckBox.Location = new Point(28, 146);
            DCharacterCheckBox.Margin = new Padding(5, 7, 5, 7);
            DCharacterCheckBox.Name = "DCharacterCheckBox";
            DCharacterCheckBox.Size = new Size(99, 21);
            DCharacterCheckBox.TabIndex = 10;
            DCharacterCheckBox.Text = "允许删除角色";
            DCharacterCheckBox.UseVisualStyleBackColor = true;
            // 
            // NCharacterCheckBox
            // 
            NCharacterCheckBox.AutoSize = true;
            NCharacterCheckBox.Location = new Point(28, 116);
            NCharacterCheckBox.Margin = new Padding(5, 7, 5, 7);
            NCharacterCheckBox.Name = "NCharacterCheckBox";
            NCharacterCheckBox.Size = new Size(99, 21);
            NCharacterCheckBox.TabIndex = 9;
            NCharacterCheckBox.Text = "允许新建角色";
            NCharacterCheckBox.UseVisualStyleBackColor = true;
            // 
            // LoginCheckBox
            // 
            LoginCheckBox.AutoSize = true;
            LoginCheckBox.Location = new Point(28, 86);
            LoginCheckBox.Margin = new Padding(5, 7, 5, 7);
            LoginCheckBox.Name = "LoginCheckBox";
            LoginCheckBox.Size = new Size(99, 21);
            LoginCheckBox.TabIndex = 8;
            LoginCheckBox.Text = "允许账户登录";
            LoginCheckBox.UseVisualStyleBackColor = true;
            // 
            // PasswordCheckBox
            // 
            PasswordCheckBox.AutoSize = true;
            PasswordCheckBox.Location = new Point(28, 57);
            PasswordCheckBox.Margin = new Padding(5, 7, 5, 7);
            PasswordCheckBox.Name = "PasswordCheckBox";
            PasswordCheckBox.Size = new Size(123, 21);
            PasswordCheckBox.TabIndex = 7;
            PasswordCheckBox.Text = "允许账户更改密码";
            PasswordCheckBox.UseVisualStyleBackColor = true;
            // 
            // AccountCheckBox
            // 
            AccountCheckBox.AutoSize = true;
            AccountCheckBox.Location = new Point(28, 27);
            AccountCheckBox.Margin = new Padding(5, 7, 5, 7);
            AccountCheckBox.Name = "AccountCheckBox";
            AccountCheckBox.Size = new Size(111, 21);
            AccountCheckBox.TabIndex = 6;
            AccountCheckBox.Text = "允许创建新账户";
            AccountCheckBox.UseVisualStyleBackColor = true;
            // 
            // tabPage4
            // 
            tabPage4.Controls.Add(label12);
            tabPage4.Controls.Add(SaveDelayTextBox);
            tabPage4.Controls.Add(label6);
            tabPage4.Location = new Point(4, 26);
            tabPage4.Margin = new Padding(5, 7, 5, 7);
            tabPage4.Name = "tabPage4";
            tabPage4.Padding = new Padding(5, 7, 5, 7);
            tabPage4.Size = new Size(476, 396);
            tabPage4.TabIndex = 3;
            tabPage4.Text = "数据保存";
            tabPage4.UseVisualStyleBackColor = true;
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Location = new Point(215, 28);
            label12.Margin = new Padding(5, 0, 5, 0);
            label12.Name = "label12";
            label12.Size = new Size(32, 17);
            label12.TabIndex = 26;
            label12.Text = "分钟";
            // 
            // SaveDelayTextBox
            // 
            SaveDelayTextBox.Location = new Point(104, 21);
            SaveDelayTextBox.Margin = new Padding(5, 7, 5, 7);
            SaveDelayTextBox.MaxLength = 5;
            SaveDelayTextBox.Name = "SaveDelayTextBox";
            SaveDelayTextBox.Size = new Size(108, 23);
            SaveDelayTextBox.TabIndex = 25;
            SaveDelayTextBox.TextChanged += CheckUShort;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(22, 24);
            label6.Margin = new Padding(5, 0, 5, 0);
            label6.Name = "label6";
            label6.Size = new Size(80, 17);
            label6.TabIndex = 24;
            label6.Text = "保存数据延时";
            // 
            // tabPage5
            // 
            tabPage5.Controls.Add(label16);
            tabPage5.Controls.Add(lineMessageTimeTextBox);
            tabPage5.Controls.Add(label17);
            tabPage5.Controls.Add(gameMasterEffect_CheckBox);
            tabPage5.Controls.Add(SafeZoneHealingCheckBox);
            tabPage5.Controls.Add(SafeZoneBorderCheckBox);
            tabPage5.Controls.Add(label18);
            tabPage5.Controls.Add(itemTimeOutTextBox);
            tabPage5.Controls.Add(label19);
            tabPage5.Controls.Add(label20);
            tabPage5.Controls.Add(playerDiedItemTimeOutTextBox);
            tabPage5.Controls.Add(label21);
            tabPage5.Controls.Add(label22);
            tabPage5.Controls.Add(corpseTimeTextBox);
            tabPage5.Controls.Add(label23);
            tabPage5.Controls.Add(WalkThroughGroupLabel);
            tabPage5.Controls.Add(WalkThroughCheckBox);
            tabPage5.Controls.Add(WalkThroughMonsterCheckBox);
            tabPage5.Controls.Add(WalkThroughNPCCheckBox);
            tabPage5.Controls.Add(WalkThroughWallCheckBox);
            tabPage5.Location = new Point(4, 26);
            tabPage5.Margin = new Padding(5, 7, 5, 7);
            tabPage5.Name = "tabPage5";
            tabPage5.Padding = new Padding(5, 7, 5, 7);
            tabPage5.Size = new Size(476, 396);
            tabPage5.TabIndex = 4;
            tabPage5.Text = "其他选项";
            tabPage5.UseVisualStyleBackColor = true;
            // 
            // WalkThroughGroupLabel
            // 
            WalkThroughGroupLabel.AutoSize = true;
            WalkThroughGroupLabel.Location = new Point(22, 244);
            WalkThroughGroupLabel.Margin = new Padding(5, 0, 5, 0);
            WalkThroughGroupLabel.Name = "WalkThroughGroupLabel";
            WalkThroughGroupLabel.Size = new Size(140, 17);
            WalkThroughGroupLabel.TabIndex = 22;
            WalkThroughGroupLabel.Text = "穿行辅助（内挂用）";
            // 
            // WalkThroughCheckBox
            // 
            WalkThroughCheckBox.AutoSize = true;
            WalkThroughCheckBox.Location = new Point(28, 270);
            WalkThroughCheckBox.Margin = new Padding(5, 4, 5, 4);
            WalkThroughCheckBox.Name = "WalkThroughCheckBox";
            WalkThroughCheckBox.Size = new Size(200, 21);
            WalkThroughCheckBox.TabIndex = 23;
            WalkThroughCheckBox.Text = "穿人（穿过其他玩家 / 英雄）";
            WalkThroughCheckBox.UseVisualStyleBackColor = true;
            // 
            // WalkThroughMonsterCheckBox
            // 
            WalkThroughMonsterCheckBox.AutoSize = true;
            WalkThroughMonsterCheckBox.Location = new Point(28, 298);
            WalkThroughMonsterCheckBox.Margin = new Padding(5, 4, 5, 4);
            WalkThroughMonsterCheckBox.Name = "WalkThroughMonsterCheckBox";
            WalkThroughMonsterCheckBox.Size = new Size(200, 21);
            WalkThroughMonsterCheckBox.TabIndex = 24;
            WalkThroughMonsterCheckBox.Text = "穿怪（穿过普通怪物）";
            WalkThroughMonsterCheckBox.UseVisualStyleBackColor = true;
            // 
            // WalkThroughNPCCheckBox
            // 
            WalkThroughNPCCheckBox.AutoSize = true;
            WalkThroughNPCCheckBox.Location = new Point(28, 326);
            WalkThroughNPCCheckBox.Margin = new Padding(5, 4, 5, 4);
            WalkThroughNPCCheckBox.Name = "WalkThroughNPCCheckBox";
            WalkThroughNPCCheckBox.Size = new Size(200, 21);
            WalkThroughNPCCheckBox.TabIndex = 25;
            WalkThroughNPCCheckBox.Text = "穿NPC（穿过商人 / NPC）";
            WalkThroughNPCCheckBox.UseVisualStyleBackColor = true;
            // 
            // WalkThroughWallCheckBox
            // 
            WalkThroughWallCheckBox.AutoSize = true;
            WalkThroughWallCheckBox.Location = new Point(28, 354);
            WalkThroughWallCheckBox.Margin = new Padding(5, 4, 5, 4);
            WalkThroughWallCheckBox.Name = "WalkThroughWallCheckBox";
            WalkThroughWallCheckBox.Size = new Size(260, 21);
            WalkThroughWallCheckBox.TabIndex = 26;
            WalkThroughWallCheckBox.Text = "穿城墙 / 城门（攻城堵门，默认不勾）";
            WalkThroughWallCheckBox.UseVisualStyleBackColor = true;
            // 
            // label16
            // 
            label16.AutoSize = true;
            label16.Location = new Point(227, 122);
            label16.Margin = new Padding(5, 0, 5, 0);
            label16.Name = "label16";
            label16.Size = new Size(32, 17);
            label16.TabIndex = 29;
            label16.Text = "分钟";
            // 
            // lineMessageTimeTextBox
            // 
            lineMessageTimeTextBox.Location = new Point(183, 116);
            lineMessageTimeTextBox.Margin = new Padding(5, 7, 5, 7);
            lineMessageTimeTextBox.MaxLength = 5;
            lineMessageTimeTextBox.Name = "lineMessageTimeTextBox";
            lineMessageTimeTextBox.Size = new Size(41, 23);
            lineMessageTimeTextBox.TabIndex = 28;
            lineMessageTimeTextBox.Text = "10";
            // 
            // label17
            // 
            label17.AutoSize = true;
            label17.Location = new Point(76, 120);
            label17.Margin = new Padding(5, 0, 5, 0);
            label17.Name = "label17";
            label17.Size = new Size(104, 17);
            label17.TabIndex = 27;
            label17.Text = "在线信息显示频率";
            // 
            // gameMasterEffect_CheckBox
            // 
            gameMasterEffect_CheckBox.AutoSize = true;
            gameMasterEffect_CheckBox.Location = new Point(28, 86);
            gameMasterEffect_CheckBox.Margin = new Padding(5, 7, 5, 7);
            gameMasterEffect_CheckBox.Name = "gameMasterEffect_CheckBox";
            gameMasterEffect_CheckBox.Size = new Size(99, 21);
            gameMasterEffect_CheckBox.TabIndex = 2;
            gameMasterEffect_CheckBox.Text = "游戏特效显示";
            gameMasterEffect_CheckBox.UseVisualStyleBackColor = true;
            // 
            // SafeZoneHealingCheckBox
            // 
            SafeZoneHealingCheckBox.AutoSize = true;
            SafeZoneHealingCheckBox.Location = new Point(28, 57);
            SafeZoneHealingCheckBox.Margin = new Padding(5, 7, 5, 7);
            SafeZoneHealingCheckBox.Name = "SafeZoneHealingCheckBox";
            SafeZoneHealingCheckBox.Size = new Size(135, 21);
            SafeZoneHealingCheckBox.TabIndex = 1;
            SafeZoneHealingCheckBox.Text = "启用安全区恢复功能";
            SafeZoneHealingCheckBox.UseVisualStyleBackColor = true;
            SafeZoneHealingCheckBox.CheckedChanged += SafeZoneHealingCheckBox_CheckedChanged;
            // 
            // SafeZoneBorderCheckBox
            // 
            SafeZoneBorderCheckBox.AutoSize = true;
            SafeZoneBorderCheckBox.Location = new Point(28, 27);
            SafeZoneBorderCheckBox.Margin = new Padding(5, 7, 5, 7);
            SafeZoneBorderCheckBox.Name = "SafeZoneBorderCheckBox";
            SafeZoneBorderCheckBox.Size = new Size(111, 21);
            SafeZoneBorderCheckBox.TabIndex = 0;
            SafeZoneBorderCheckBox.Text = "启用安全区边框";
            SafeZoneBorderCheckBox.UseVisualStyleBackColor = true;
            SafeZoneBorderCheckBox.CheckedChanged += SafeZoneBorderCheckBox_CheckedChanged;
            // 
            // label18
            // 
            label18.AutoSize = true;
            label18.Location = new Point(22, 155);
            label18.Margin = new Padding(5, 0, 5, 0);
            label18.Name = "label18";
            label18.Size = new Size(112, 17);
            label18.TabIndex = 30;
            label18.Text = "地面物品停留时间";
            // 
            // itemTimeOutTextBox
            // 
            itemTimeOutTextBox.Location = new Point(200, 149);
            itemTimeOutTextBox.Margin = new Padding(5, 7, 5, 7);
            itemTimeOutTextBox.MaxLength = 6;
            itemTimeOutTextBox.Name = "itemTimeOutTextBox";
            itemTimeOutTextBox.Size = new Size(60, 23);
            itemTimeOutTextBox.TabIndex = 31;
            itemTimeOutTextBox.Text = "30";
            itemTimeOutTextBox.TextChanged += CheckNumber;
            // 
            // label19
            // 
            label19.AutoSize = true;
            label19.Location = new Point(266, 155);
            label19.Margin = new Padding(5, 0, 5, 0);
            label19.Name = "label19";
            label19.Size = new Size(32, 17);
            label19.TabIndex = 32;
            label19.Text = "分钟";
            // 
            // label20
            // 
            label20.AutoSize = true;
            label20.Location = new Point(22, 185);
            label20.Margin = new Padding(5, 0, 5, 0);
            label20.Name = "label20";
            label20.Size = new Size(140, 17);
            label20.TabIndex = 33;
            label20.Text = "死亡掉落物品停留时间";
            // 
            // playerDiedItemTimeOutTextBox
            // 
            playerDiedItemTimeOutTextBox.Location = new Point(200, 179);
            playerDiedItemTimeOutTextBox.Margin = new Padding(5, 7, 5, 7);
            playerDiedItemTimeOutTextBox.MaxLength = 6;
            playerDiedItemTimeOutTextBox.Name = "playerDiedItemTimeOutTextBox";
            playerDiedItemTimeOutTextBox.Size = new Size(60, 23);
            playerDiedItemTimeOutTextBox.TabIndex = 34;
            playerDiedItemTimeOutTextBox.Text = "120";
            playerDiedItemTimeOutTextBox.TextChanged += CheckNumber;
            // 
            // label21
            // 
            label21.AutoSize = true;
            label21.Location = new Point(266, 185);
            label21.Margin = new Padding(5, 0, 5, 0);
            label21.Name = "label21";
            label21.Size = new Size(32, 17);
            label21.TabIndex = 35;
            label21.Text = "分钟";
            // 
            // label22
            // 
            label22.AutoSize = true;
            label22.Location = new Point(22, 215);
            label22.Margin = new Padding(5, 0, 5, 0);
            label22.Name = "label22";
            label22.Size = new Size(112, 17);
            label22.TabIndex = 36;
            label22.Text = "怪物尸体停留时间";
            // 
            // corpseTimeTextBox
            // 
            corpseTimeTextBox.Location = new Point(200, 209);
            corpseTimeTextBox.Margin = new Padding(5, 7, 5, 7);
            corpseTimeTextBox.MaxLength = 6;
            corpseTimeTextBox.Name = "corpseTimeTextBox";
            corpseTimeTextBox.Size = new Size(60, 23);
            corpseTimeTextBox.TabIndex = 37;
            corpseTimeTextBox.Text = "180";
            corpseTimeTextBox.TextChanged += CheckNumber;
            // 
            // label23
            // 
            label23.AutoSize = true;
            label23.Location = new Point(266, 215);
            label23.Margin = new Padding(5, 0, 5, 0);
            label23.Name = "label23";
            label23.Size = new Size(32, 17);
            label23.TabIndex = 38;
            label23.Text = "秒";
            // 
            // VPathDialog
            // 
            VPathDialog.FileName = "Mir2.Exe";
            VPathDialog.Filter = "Executable Files (*.exe)|*.exe";
            VPathDialog.Multiselect = true;
            // 
            // ConfigForm
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(512, 487);
            Controls.Add(SaveButton);
            Controls.Add(configTabs);
            Margin = new Padding(5, 7, 5, 7);
            Name = "ConfigForm";
            Text = "服务器设置";
            FormClosed += ConfigForm_FormClosed;
            configTabs.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            tabPage2.ResumeLayout(false);
            tabPage2.PerformLayout();
            tabPage3.ResumeLayout(false);
            tabPage3.PerformLayout();
            tabPage4.ResumeLayout(false);
            tabPage4.PerformLayout();
            tabPage5.ResumeLayout(false);
            tabPage5.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button SaveButton;
        private System.Windows.Forms.TabControl configTabs;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TextBox RelogDelayTextBox;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.CheckBox VersionCheckBox;
        private System.Windows.Forms.Button VPathBrowseButton;
        private System.Windows.Forms.TextBox VPathTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TextBox MaxUserTextBox;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox TimeOutTextBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox PortTextBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox IPAddressTextBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.OpenFileDialog VPathDialog;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.CheckBox StartGameCheckBox;
        private System.Windows.Forms.CheckBox DCharacterCheckBox;
        private System.Windows.Forms.CheckBox NCharacterCheckBox;
        private System.Windows.Forms.CheckBox LoginCheckBox;
        private System.Windows.Forms.CheckBox PasswordCheckBox;
        private System.Windows.Forms.CheckBox AccountCheckBox;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.TextBox SaveDelayTextBox;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TabPage tabPage5;
        private System.Windows.Forms.CheckBox SafeZoneBorderCheckBox;
        private System.Windows.Forms.CheckBox SafeZoneHealingCheckBox;
        private System.Windows.Forms.CheckBox AllowArcherCheckBox;
        private System.Windows.Forms.CheckBox AllowAssassinCheckBox;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox Resolution_textbox;
        private System.Windows.Forms.Label ServerVersionLabel;
        private System.Windows.Forms.Label DBVersionLabel;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox versionFuncGroupBox;
        private System.Windows.Forms.CheckBox MultiThreadCheckBox;
        private System.Windows.Forms.CheckBox TestServerCheckBox;
        private System.Windows.Forms.CheckBox GoldDirectCheckBox;
        private System.Windows.Forms.CheckBox OfflineAfkEnabledCheckBox;
        private System.Windows.Forms.CheckBox OfflineAfkNoConsumableCheckBox;
        private System.Windows.Forms.CheckBox OfflineAfkNoEquipCheckBox;
        private System.Windows.Forms.CheckBox FakePlayerEnabledCheckBox;
        private System.Windows.Forms.Label ExpRateLabel;
        private System.Windows.Forms.TextBox ExpRateTextBox;
        private System.Windows.Forms.Label DropRateLabel;
        private System.Windows.Forms.TextBox DropRateTextBox;
        private System.Windows.Forms.Label HighLevelRateLabel;
        private System.Windows.Forms.TextBox HighLevelRateTextBox;
        private System.Windows.Forms.Label DeathExpLabel;
        private System.Windows.Forms.TextBox DeathExpTextBox;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.CheckBox gameMasterEffect_CheckBox;
        private System.Windows.Forms.TextBox HTTPIPAddressTextBox;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox HTTPTrustedIPAddressTextBox;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.CheckBox StartHTTPCheckBox;
        private System.Windows.Forms.CheckBox EnableResourceCheckBox;
        private System.Windows.Forms.Label ResourcePathLabel;
        private System.Windows.Forms.TextBox ResourcePathTextBox;
        private System.Windows.Forms.CheckBox ResourceAllowAnyIPCheckBox;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox lineMessageTimeTextBox;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.TextBox itemTimeOutTextBox;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.TextBox playerDiedItemTimeOutTextBox;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.TextBox corpseTimeTextBox;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.Label WalkThroughGroupLabel;
        private System.Windows.Forms.CheckBox WalkThroughCheckBox;
        private System.Windows.Forms.CheckBox WalkThroughMonsterCheckBox;
        private System.Windows.Forms.CheckBox WalkThroughNPCCheckBox;
        private System.Windows.Forms.CheckBox WalkThroughWallCheckBox;
    }
}