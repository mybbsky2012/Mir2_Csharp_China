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
            RelogDelayTextBox.Location = new Point(104, 88);
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
            label7.Location = new Point(22, 91);
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
            // tabPage2
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
            tabPage5.Location = new Point(4, 26);
            tabPage5.Margin = new Padding(5, 7, 5, 7);
            tabPage5.Name = "tabPage5";
            tabPage5.Padding = new Padding(5, 7, 5, 7);
            tabPage5.Size = new Size(476, 396);
            tabPage5.TabIndex = 4;
            tabPage5.Text = "其他选项";
            tabPage5.UseVisualStyleBackColor = true;
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
    }
}