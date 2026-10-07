using Client.MirSounds;

namespace Client
{
    class Settings
    {
        public const long CleanDelay = 600000;

        public static int ScreenWidth = 800, ScreenHeight = 600; //ScreenWidth = 1024, ScreenHeight = 768
        private static InIReader Reader = new InIReader(@".\Mir2Config.ini");
        private static InIReader QuestTrackingReader = new InIReader(Path.Combine(UserDataPath, @".\QuestTracking.ini"));

        private static bool _useTestConfig;
        public static bool UseTestConfig
        {
            get
            {
                return _useTestConfig;
            }
            set 
            {
                if (value == true)
                {
                    Reader = new InIReader(@".\Mir2Test.ini");
                }
                _useTestConfig = value;
            }
        }

        public const string DataPath = @".\Data\",
                            MapPath = @".\Map\",
                            SoundPath = @".\Sound\",
                            ExtraDataPath = @".\Data\Extra\",
                            ShadersPath = @".\Data\Shaders\",
                            MonsterPath = @".\Data\Monster\",
                            GatePath = @".\Data\Gate\",
                            FlagPath = @".\Data\Flag\",
                            SiegePath = @".\Data\Siege\",
                            NPCPath = @".\Data\NPC\",
                            CArmourPath = @".\Data\CArmour\",
                            CWeaponPath = @".\Data\CWeapon\",
							CWeaponEffectPath = @".\Data\CWeaponEffect\",
							CHairPath = @".\Data\CHair\",
                            AArmourPath = @".\Data\AArmour\",
                            AWeaponPath = @".\Data\AWeapon\",
                            AWeaponEffectPath = @".\Data\AWeaponEffect\",
                            AHairPath = @".\Data\AHair\",
                            ARArmourPath = @".\Data\ARArmour\",
                            ARWeaponPath = @".\Data\ARWeapon\",
                            ARWeaponEffectPath = @".\Data\ARWeaponEffect\",
                            ARHairPath = @".\Data\ARHair\",
                            CHumEffectPath = @".\Data\CHumEffect\",
                            AHumEffectPath = @".\Data\AHumEffect\",
                            ARHumEffectPath = @".\Data\ARHumEffect\",
                            MountPath = @".\Data\Mount\",
                            FishingPath = @".\Data\Fishing\",
                            PetsPath = @".\Data\Pet\",
                            TransformPath = @".\Data\Transform\",
                            TransformMountsPath = @".\Data\TransformRide2\",
                            TransformEffectPath = @".\Data\TransformEffect\",
                            TransformWeaponEffectPath = @".\Data\TransformWeaponEffect\",
                            MouseCursorPath = @".\Data\Cursors\",
                            ResourcePath = @".\DirectX\",
                            UserDataPath = @".\Data\UserData\";

        //Logs
        public static bool LogErrors = true;
        public static bool LogChat = true;
        public static int RemainingErrorLogs = 100;

        //Graphics
        public static bool FullScreen = false, Borderless = true, TopMost = true, MouseClip = false;
        public static string FontName = "Arial"; //"MS Sans Serif"
        public static float FontSize = 8F;
        public static bool UseMouseCursors = true;

        public static bool FPSCap = true;
        public static int MaxFPS = 100;
        public static int Resolution = 1024;
        public static bool DebugMode = false;

        //Network
        public static bool UseConfig = false;
        public static string IPAddress = "127.0.0.1";
        public static int Port = 7000;
        public const int TimeOut = 5000;

        //Sound
        public static int SoundOverLap = 3;
        private static byte _volume = 100;
        public static int SoundCleanMinutes = 5;

        public static byte Volume
        {
            get { return _volume; }
            set
            {
                switch (value)
                {
                    case > 100:
                        _volume = (byte)100;
                        break;
                    case <= 0:
                        _volume = (byte)0;
                        break;
                    default:
                        _volume = value;
                        break;
                }

                SoundManager.Vol = Convert.ToInt32(_volume);
            }
        }

        private static byte _musicVolume = 100;
        public static byte MusicVolume
        {
            get { return _musicVolume; }
            set
            {
                switch(value)
                {
                    case > 100:
                        _musicVolume = (byte)100;
                        break;
                    case <= 0:
                        _musicVolume = (byte)0;
                        break;
                    default:
                        _musicVolume = value;
                        break;
                }

                SoundManager.MusicVol = Convert.ToInt32(_musicVolume);
            }
        }

        //Game
        public static string AccountID = "",
                             Password = "";

        public static bool
            SkillMode = false,
            SkillBar = true,
            //SkillSet = true,
            Effect = true,
            LevelEffect = true,
            DropView = true,
            NameView = true,
            HPView = true,
            TransparentChat = false,
            ModeView = false,
            DuraView = false,
            DisplayDamage = true,
            TargetDead = false,
            HighlightTarget = true,
            ExpandedBuffWindow = true,
            ExpandedHeroBuffWindow = true,
            DisplayBodyName = false,
            NewMove = false;

        //内挂（自动挂机）
        public static bool
            AutoPlay = false,           // 内挂总开关
            AutoAttack = true,          // 自动攻击目标
            AutoMove = true,            // 目标超出攻击距离时自动走近
            AutoPickup = true,          // 自动捡取身边物品
            AutoPotHP = false,          // 自动使用 HP 药水
            AutoPotMP = false,          // 自动使用 MP 药水
            AutoMoveRun = true,         // 自动走位时优先跑动（路况不佳自动降级为走路）
            AutoSkill = true,           // 自动按需用技能：远怪用远程、近怪用近攻、围攻用群攻
            AutoDodge = false,          // 被怪物围攻时自动躲避走位
<<<<<<< HEAD
            AutoThrustingGap = true,    // 战士隔位刺杀（刀刀刺杀）：目标隔一格时原地用刺杀剑气打，不贴脸
=======
<<<<<<< HEAD
            AutoThrustingGap = true,    // 战士隔位刺杀（刀刀刺杀）：目标隔一格时原地用刺杀剑气打，不贴脸
=======
<<<<<<< HEAD
            AutoThrustingGap = true,    // 战士隔位刺杀（刀刀刺杀）：目标隔一格时原地用刺杀剑气打，不贴脸
=======
<<<<<<< HEAD
            AutoThrustingGap = true,    // 战士隔位刺杀（刀刀刺杀）：目标隔一格时原地用刺杀剑气打，不贴脸
=======
<<<<<<< HEAD
            AutoThrustingGap = true,    // 战士隔位刺杀（刀刀刺杀）：目标隔一格时原地用刺杀剑气打，不贴脸
=======
>>>>>>> 47baf6042e798ed36472152258ad576f62b810f0
>>>>>>> 387da1057651bb867b9fab9d85af1be42ce16c01
>>>>>>> 7eeff19091ec70721f299b982289ed347c0f740b
>>>>>>> 7c699bd4ee3c46cb11ca30fd0e30d4c1767e660a
>>>>>>> f8fcb17699a047662b6d41157c79ca9cfcc17c41
            AutoSwapPoison = true;      // 道士毒符互换：符与毒共用「护身符」装备槽，按需自动切换（换毒时红绿交替）

        public static int
            AutoPotHPPercent = 50,      // HP 低于该百分比时自动喝药
            AutoPotMPPercent = 30,      // MP 低于该百分比时自动喝药
            AutoSearchRange = 8;        // 自动索敌/捡物的搜索半径（格）

        //自动攻击时忽略的怪物名关键字（逗号分隔），用于避开守卫/NPC类怪物
        public static string AutoAttackIgnore = "守卫,卫士,大刀,弓箭手,城主,门卫";

        //不死系怪物名关键字（逗号分隔）：法师内挂命中这些名字的怪时改用圣言术
        public static string AutoSaintKeywords = "僵尸,骷髅,白骨,腐尸,尸,亡灵,死灵,幽灵,幽魂,巫妖,骨";

        //服务器开关型辅助功能（需要服务器放行，见 S.PlayerOption）
        public static bool
            NoLamp = false,             // 免蜡：夜晚不需要照明
            WalkThrough = false,        // 穿人：可以穿过其他玩家、怪物和 NPC
            NoRunUp = false,            // 免助跑：无需先走一步即可直接奔跑
            OverWeight = false,         // 超负重：负重超限仍可奔跑、装备不受负重限制
            MountTai = false;           // 泰山：被攻击时不后仰，且不打断跑动与施法

        //上列开关的「本地偏好」。服务器登录时会把这几个开关统一置为关闭并下发，
        //若直接覆盖上面几个字段就会丢掉玩家的勾选。因此偏好单独存一份、单独落盘，
        //进图后由 GameScene.RestorePlayerOptions() 再次向服务器申请。
        public static bool
            PreferredNoLamp = false,
            PreferredWalkThrough = false,
            PreferredNoRunUp = false,
            PreferredOverWeight = false,
            PreferredMountTai = false;

        /// <summary>记录玩家对某个服务器开关的偏好（由 RequestPlayerOption 调用）</summary>
        public static void SetPreferred(PlayerOptionType option, bool value)
        {
            switch (option)
            {
                case PlayerOptionType.NoLamp: PreferredNoLamp = value; break;
                case PlayerOptionType.WalkThrough: PreferredWalkThrough = value; break;
                case PlayerOptionType.NoRunUp: PreferredNoRunUp = value; break;
                case PlayerOptionType.OverWeight: PreferredOverWeight = value; break;
                case PlayerOptionType.MountTai: PreferredMountTai = value; break;
            }
        }

        public static int[,] SkillbarLocation = new int[2, 2] { { 0, 0 }, { 216, 0 }  };

        //Quests
        public static int[] TrackedQuests = new int[5];

        //Chat
        public static bool
            ShowNormalChat = true,
            ShowYellChat = true,
            ShowWhisperChat = true,
            ShowLoverChat = true,
            ShowMentorChat = true,
            ShowGroupChat = true,
            ShowGuildChat = true;

        //Filters
        public static bool
            FilterNormalChat = false,
            FilterWhisperChat = false,
            FilterShoutChat = false,
            FilterSystemChat = false,
            FilterLoverChat = false,
            FilterMentorChat = false,
            FilterGroupChat = false,
            FilterGuildChat = false;


        //AutoPatcher
        public static bool P_Patcher = true;
        public static string P_Host = @"http://127.0.0.1/mir2/cmir/patch/";//默认 mirfiles.com
        public static string P_PatchFileName = @"PList.gz";
        public static bool P_NeedLogin = false;
        public static string P_Login = string.Empty;
        public static string P_Password = string.Empty;
        public static string P_ServerName = string.Empty;
        public static string P_BrowserAddress = "https://127.0.0.1/mir2-patchsite/";//默认 www.lomcn.org
        public static string P_Client = Application.StartupPath + "\\";
        public static bool P_AutoStart = false;
        public static int P_Concurrency = 1;

        //微端：客户端只带核心资源，其余在用到时向服务端 HTTP 资源服务按需下载
        public static bool MicroClient = false;
        public static string MicroHost = @"http://127.0.0.1:5679/";
        public static int MicroTimeout = 8000;
        public static bool MicroLog = true;

        //微端下载策略：资源一律"用到才下"，绝不在启动阶段批量预下载。
        public static int MicroConcurrency = 2;      //后台下载线程数
        public static int MicroRateLimit = 0;        //每个线程的下载限速(KB/s)，0=不限速

        //是否在聊天框显示微端资源下载提示（"微端已启用…" / "资源补齐完成…"）。
        //默认 False：微端本来就是后台悄悄补资源，不该打扰玩家；
        //需要确认"到底有没有在下"时，把 Mir2Config.ini 里 [MicroClient] Hint 改成 True 即可。
        public static bool MicroHint = false;

        public static void Load()
        {
            GameLanguage.LoadClientLanguage(@".\Language.ini");

            if (!Directory.Exists(DataPath)) Directory.CreateDirectory(DataPath);
            if (!Directory.Exists(MapPath)) Directory.CreateDirectory(MapPath);
            if (!Directory.Exists(SoundPath)) Directory.CreateDirectory(SoundPath);
           
            //Graphics
            FullScreen = Reader.ReadBoolean("Graphics", "FullScreen", FullScreen);
            Borderless = Reader.ReadBoolean("Graphics", "Borderless", Borderless);
            MouseClip = Reader.ReadBoolean("Graphics", "MouseClip", MouseClip);
            TopMost = Reader.ReadBoolean("Graphics", "AlwaysOnTop", TopMost);
            FPSCap = Reader.ReadBoolean("Graphics", "FPSCap", FPSCap);
            Resolution = Reader.ReadInt32("Graphics", "Resolution", Resolution);
            DebugMode = Reader.ReadBoolean("Graphics", "DebugMode", DebugMode);
            UseMouseCursors = Reader.ReadBoolean("Graphics", "UseMouseCursors", UseMouseCursors);

            //Network
            UseConfig = Reader.ReadBoolean("Network", "UseConfig", UseConfig);
            if (UseConfig)
            {
                IPAddress = Reader.ReadString("Network", "IPAddress", IPAddress);
                Port = Reader.ReadInt32("Network", "Port", Port);
            }

            //Logs
            LogErrors = Reader.ReadBoolean("Logs", "LogErrors", LogErrors);
            LogChat = Reader.ReadBoolean("Logs", "LogChat", LogChat);

            //Sound
            Volume = Reader.ReadByte("Sound", "Volume", Volume);
            SoundOverLap = Reader.ReadInt32("Sound", "SoundOverLap", SoundOverLap);
            MusicVolume = Reader.ReadByte("Sound", "Music", MusicVolume);
            var n = Reader.ReadInt32("Sound", "CleanMinutes", SoundCleanMinutes);
            if (n < 1 || n > 60 * 3) n = SoundCleanMinutes;
            SoundCleanMinutes = n;


            //Game
            AccountID = Reader.ReadString("Game", "AccountID", AccountID);
            Password = Reader.ReadString("Game", "Password", Password);

            SkillMode = Reader.ReadBoolean("Game", "SkillMode", SkillMode);
            SkillBar = Reader.ReadBoolean("Game", "SkillBar", SkillBar);
            //SkillSet = Reader.ReadBoolean("Game", "SkillSet", SkillSet);
            Effect = Reader.ReadBoolean("Game", "Effect", Effect);
            LevelEffect = Reader.ReadBoolean("Game", "LevelEffect", Effect);
            DropView = Reader.ReadBoolean("Game", "DropView", DropView);
            NameView = Reader.ReadBoolean("Game", "NameView", NameView);
            HPView = Reader.ReadBoolean("Game", "HPMPView", HPView);
            ModeView = Reader.ReadBoolean("Game", "ModeView", ModeView);
            FontName = Reader.ReadString("Game", "FontName", FontName);
            TransparentChat = Reader.ReadBoolean("Game", "TransparentChat", TransparentChat);
            DisplayDamage = Reader.ReadBoolean("Game", "DisplayDamage", DisplayDamage);
            TargetDead = Reader.ReadBoolean("Game", "TargetDead", TargetDead);
            HighlightTarget = Reader.ReadBoolean("Game", "HighlightTarget", HighlightTarget);
            ExpandedBuffWindow = Reader.ReadBoolean("Game", "ExpandedBuffWindow", ExpandedBuffWindow);
            ExpandedHeroBuffWindow = Reader.ReadBoolean("Game", "ExpandedHeroBuffWindow", ExpandedHeroBuffWindow);
            DuraView = Reader.ReadBoolean("Game", "DuraWindow", DuraView);
            DisplayBodyName = Reader.ReadBoolean("Game", "DisplayBodyName", DisplayBodyName);
            NewMove = Reader.ReadBoolean("Game", "NewMove", NewMove);

            //内挂
            AutoPlay = Reader.ReadBoolean("AutoPlay", "AutoPlay", AutoPlay);
            AutoAttack = Reader.ReadBoolean("AutoPlay", "AutoAttack", AutoAttack);
            AutoMove = Reader.ReadBoolean("AutoPlay", "AutoMove", AutoMove);
            AutoPickup = Reader.ReadBoolean("AutoPlay", "AutoPickup", AutoPickup);
            AutoPotHP = Reader.ReadBoolean("AutoPlay", "AutoPotHP", AutoPotHP);
            AutoPotMP = Reader.ReadBoolean("AutoPlay", "AutoPotMP", AutoPotMP);
            AutoMoveRun = Reader.ReadBoolean("AutoPlay", "AutoMoveRun", AutoMoveRun);
            AutoSkill = Reader.ReadBoolean("AutoPlay", "AutoSkill", AutoSkill);
            AutoDodge = Reader.ReadBoolean("AutoPlay", "AutoDodge", AutoDodge);
<<<<<<< HEAD
            AutoThrustingGap = Reader.ReadBoolean("AutoPlay", "AutoThrustingGap", AutoThrustingGap);
=======
<<<<<<< HEAD
            AutoThrustingGap = Reader.ReadBoolean("AutoPlay", "AutoThrustingGap", AutoThrustingGap);
=======
<<<<<<< HEAD
            AutoThrustingGap = Reader.ReadBoolean("AutoPlay", "AutoThrustingGap", AutoThrustingGap);
=======
<<<<<<< HEAD
            AutoThrustingGap = Reader.ReadBoolean("AutoPlay", "AutoThrustingGap", AutoThrustingGap);
=======
<<<<<<< HEAD
            AutoThrustingGap = Reader.ReadBoolean("AutoPlay", "AutoThrustingGap", AutoThrustingGap);
=======
>>>>>>> 47baf6042e798ed36472152258ad576f62b810f0
>>>>>>> 387da1057651bb867b9fab9d85af1be42ce16c01
>>>>>>> 7eeff19091ec70721f299b982289ed347c0f740b
>>>>>>> 7c699bd4ee3c46cb11ca30fd0e30d4c1767e660a
>>>>>>> f8fcb17699a047662b6d41157c79ca9cfcc17c41
            AutoSwapPoison = Reader.ReadBoolean("AutoPlay", "AutoSwapPoison", AutoSwapPoison);
            AutoPotHPPercent = Reader.ReadInt32("AutoPlay", "AutoPotHPPercent", AutoPotHPPercent);
            AutoPotMPPercent = Reader.ReadInt32("AutoPlay", "AutoPotMPPercent", AutoPotMPPercent);
            AutoSearchRange = Reader.ReadInt32("AutoPlay", "AutoSearchRange", AutoSearchRange);
            AutoAttackIgnore = Reader.ReadString("AutoPlay", "AutoAttackIgnore", AutoAttackIgnore);
            AutoSaintKeywords = Reader.ReadString("AutoPlay", "AutoSaintKeywords", AutoSaintKeywords);

            // 忽略名单被清空时恢复默认，保证守卫类怪物始终被过滤
            if (string.IsNullOrWhiteSpace(AutoAttackIgnore)) AutoAttackIgnore = "守卫,卫士,大刀,弓箭手,城主,门卫";

            // 不死系关键字被清空时恢复默认，保证圣言术仍能识别常见不死系
            if (string.IsNullOrWhiteSpace(AutoSaintKeywords)) AutoSaintKeywords = "僵尸,骷髅,白骨,腐尸,尸,亡灵,死灵,幽灵,幽魂,巫妖,骨";

            if (AutoPotHPPercent < 1 || AutoPotHPPercent > 99) AutoPotHPPercent = 50;
            if (AutoPotMPPercent < 1 || AutoPotMPPercent > 99) AutoPotMPPercent = 30;
            if (AutoSearchRange < 1 || AutoSearchRange > 16) AutoSearchRange = 8;

            //服务器开关型辅助功能（本地记忆，进游戏后重新向服务器申请）
            NoLamp = Reader.ReadBoolean("AutoPlay", "NoLamp", NoLamp);
            WalkThrough = Reader.ReadBoolean("AutoPlay", "WalkThrough", WalkThrough);
            NoRunUp = Reader.ReadBoolean("AutoPlay", "NoRunUp", NoRunUp);
            OverWeight = Reader.ReadBoolean("AutoPlay", "OverWeight", OverWeight);
            MountTai = Reader.ReadBoolean("AutoPlay", "MountTai", MountTai);

            // 启动时把 ini 里的值记为玩家偏好（运行时 Settings.* 会被服务器下发值覆盖）
            SetPreferred(PlayerOptionType.NoLamp, NoLamp);
            SetPreferred(PlayerOptionType.WalkThrough, WalkThrough);
            SetPreferred(PlayerOptionType.NoRunUp, NoRunUp);
            SetPreferred(PlayerOptionType.OverWeight, OverWeight);
            SetPreferred(PlayerOptionType.MountTai, MountTai);

            for (int i = 0; i < SkillbarLocation.Length / 2; i++)
            {
                SkillbarLocation[i, 0] = Reader.ReadInt32("Game", "Skillbar" + i.ToString() + "X", SkillbarLocation[i, 0]);
                SkillbarLocation[i, 1] = Reader.ReadInt32("Game", "Skillbar" + i.ToString() + "Y", SkillbarLocation[i, 1]);
            }

            //Chat
            ShowNormalChat = Reader.ReadBoolean("Chat", "ShowNormalChat", ShowNormalChat);
            ShowYellChat = Reader.ReadBoolean("Chat", "ShowYellChat", ShowYellChat);
            ShowWhisperChat = Reader.ReadBoolean("Chat", "ShowWhisperChat", ShowWhisperChat);
            ShowLoverChat = Reader.ReadBoolean("Chat", "ShowLoverChat", ShowLoverChat);
            ShowMentorChat = Reader.ReadBoolean("Chat", "ShowMentorChat", ShowMentorChat);
            ShowGroupChat = Reader.ReadBoolean("Chat", "ShowGroupChat", ShowGroupChat);
            ShowGuildChat = Reader.ReadBoolean("Chat", "ShowGuildChat", ShowGuildChat);

            //Filters
            FilterNormalChat = Reader.ReadBoolean("Filter", "FilterNormalChat", FilterNormalChat);
            FilterWhisperChat = Reader.ReadBoolean("Filter", "FilterWhisperChat", FilterWhisperChat);
            FilterShoutChat = Reader.ReadBoolean("Filter", "FilterShoutChat", FilterShoutChat);
            FilterSystemChat = Reader.ReadBoolean("Filter", "FilterSystemChat", FilterSystemChat);
            FilterLoverChat = Reader.ReadBoolean("Filter", "FilterLoverChat", FilterLoverChat);
            FilterMentorChat = Reader.ReadBoolean("Filter", "FilterMentorChat", FilterMentorChat);
            FilterGroupChat = Reader.ReadBoolean("Filter", "FilterGroupChat", FilterGroupChat);
            FilterGuildChat = Reader.ReadBoolean("Filter", "FilterGuildChat", FilterGuildChat);

            //AutoPatcher
            P_Patcher = Reader.ReadBoolean("Launcher", "Enabled", P_Patcher);
            P_Host = Reader.ReadString("Launcher", "Host", P_Host);
            P_PatchFileName = Reader.ReadString("Launcher", "PatchFile", P_PatchFileName);
            P_NeedLogin = Reader.ReadBoolean("Launcher", "NeedLogin", P_NeedLogin);
            P_Login = Reader.ReadString("Launcher", "Login", P_Login);
            P_Password = Reader.ReadString("Launcher", "Password", P_Password);
            P_AutoStart = Reader.ReadBoolean("Launcher", "AutoStart", P_AutoStart);
            P_ServerName = Reader.ReadString("Launcher", "ServerName", P_ServerName);
            P_BrowserAddress = Reader.ReadString("Launcher", "Browser", P_BrowserAddress);
            P_Concurrency = Reader.ReadInt32("Launcher", "ConcurrentDownloads", P_Concurrency);
            

            if (!P_Host.EndsWith("/")) P_Host += "/";
            if (P_Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) P_Host = P_Host.Insert(0, "http://");
            if (P_BrowserAddress.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) P_BrowserAddress = P_BrowserAddress.Insert(0, "http://");

            //Temp check to update everyones address
            if (P_Host.ToLower() == "http://127.0.0.1/mir2/cmir/patch/")//默认 mirfiles.co.uk
            {
                P_Host = "http://127.0.0.1/mir2/cmir/patch/";//默认 mirfiles.com
            }

            if (P_Concurrency < 1) P_Concurrency = 1;
            if (P_Concurrency > 100) P_Concurrency = 100;

            //微端（按需下载缺失资源）
            MicroClient = Reader.ReadBoolean("MicroClient", "Enabled", MicroClient);
            MicroHost = Reader.ReadString("MicroClient", "Host", MicroHost);
            MicroTimeout = Reader.ReadInt32("MicroClient", "Timeout", MicroTimeout);
            MicroLog = Reader.ReadBoolean("MicroClient", "Log", MicroLog);
            MicroConcurrency = Reader.ReadInt32("MicroClient", "Concurrency", MicroConcurrency);
            MicroRateLimit = Reader.ReadInt32("MicroClient", "RateLimit", MicroRateLimit);
            MicroHint = Reader.ReadBoolean("MicroClient", "Hint", MicroHint);   //默认 False，不显示下载提示

            if (string.IsNullOrWhiteSpace(MicroHost)) MicroHost = "http://127.0.0.1:5679/";
            if (!MicroHost.EndsWith("/")) MicroHost += "/";
            if (MicroTimeout < 1000) MicroTimeout = 8000;
            if (MicroConcurrency < 1) MicroConcurrency = 2;
            if (MicroConcurrency > 8) MicroConcurrency = 8;
            if (MicroRateLimit < 0) MicroRateLimit = 0;

            // 客户端根目录下存在 microclient.flag 时自动启用微端。
            // 分发「微端客户端包」时只要附带这个空标记文件，玩家就不需要自己改配置；
            // 完整客户端不放该文件，自然走全本地资源、没有任何额外开销。
            if (!MicroClient)
            {
                try
                {
                    if (File.Exists(Path.Combine(P_Client, "microclient.flag")))
                    {
                        MicroClient = true;
                        MicroLog = true;
                    }
                }
                catch
                {
                }
            }
        }

        public static void Save()
        {
            //Graphics
            Reader.Write("Graphics", "FullScreen", FullScreen);
            Reader.Write("Graphics", "Borderless", Borderless);
            Reader.Write("Graphics", "MouseClip", MouseClip);
            Reader.Write("Graphics", "AlwaysOnTop", TopMost);
            Reader.Write("Graphics", "FPSCap", FPSCap);
            Reader.Write("Graphics", "Resolution", Resolution);
            Reader.Write("Graphics", "DebugMode", DebugMode);
            Reader.Write("Graphics", "UseMouseCursors", UseMouseCursors);

            //Sound
            Reader.Write("Sound", "Volume", Volume);
            Reader.Write("Sound", "SoundOverLap", SoundOverLap);
            Reader.Write("Sound", "Music", MusicVolume);
            Reader.Write("Sound", "CleanMinutes", SoundCleanMinutes);

            //Game
            Reader.Write("Game", "AccountID", AccountID);
            Reader.Write("Game", "Password", Password);
            Reader.Write("Game", "SkillMode", SkillMode);
            Reader.Write("Game", "SkillBar", SkillBar);
            //Reader.Write("Game", "SkillSet", SkillSet);
            Reader.Write("Game", "Effect", Effect);
            Reader.Write("Game", "LevelEffect", LevelEffect);
            Reader.Write("Game", "DropView", DropView);
            Reader.Write("Game", "NameView", NameView);
            Reader.Write("Game", "HPMPView", HPView);
            Reader.Write("Game", "ModeView", ModeView);
            Reader.Write("Game", "FontName", FontName);
            Reader.Write("Game", "TransparentChat", TransparentChat);
            Reader.Write("Game", "DisplayDamage", DisplayDamage);
            Reader.Write("Game", "TargetDead", TargetDead);
            Reader.Write("Game", "HighlightTarget", HighlightTarget);
            Reader.Write("Game", "ExpandedBuffWindow", ExpandedBuffWindow);
            Reader.Write("Game", "ExpandedHeroBuffWindow", ExpandedBuffWindow);
            Reader.Write("Game", "DuraWindow", DuraView);
            Reader.Write("Game", "DisplayBodyName", DisplayBodyName);
            Reader.Write("Game", "NewMove", NewMove);

            //内挂
            Reader.Write("AutoPlay", "AutoPlay", AutoPlay);
            Reader.Write("AutoPlay", "AutoAttack", AutoAttack);
            Reader.Write("AutoPlay", "AutoMove", AutoMove);
            Reader.Write("AutoPlay", "AutoPickup", AutoPickup);
            Reader.Write("AutoPlay", "AutoPotHP", AutoPotHP);
            Reader.Write("AutoPlay", "AutoPotMP", AutoPotMP);
            Reader.Write("AutoPlay", "AutoMoveRun", AutoMoveRun);
            Reader.Write("AutoPlay", "AutoSkill", AutoSkill);
            Reader.Write("AutoPlay", "AutoDodge", AutoDodge);
<<<<<<< HEAD
            Reader.Write("AutoPlay", "AutoThrustingGap", AutoThrustingGap);
=======
<<<<<<< HEAD
            Reader.Write("AutoPlay", "AutoThrustingGap", AutoThrustingGap);
=======
<<<<<<< HEAD
            Reader.Write("AutoPlay", "AutoThrustingGap", AutoThrustingGap);
=======
<<<<<<< HEAD
            Reader.Write("AutoPlay", "AutoThrustingGap", AutoThrustingGap);
=======
<<<<<<< HEAD
            Reader.Write("AutoPlay", "AutoThrustingGap", AutoThrustingGap);
=======
>>>>>>> 47baf6042e798ed36472152258ad576f62b810f0
>>>>>>> 387da1057651bb867b9fab9d85af1be42ce16c01
>>>>>>> 7eeff19091ec70721f299b982289ed347c0f740b
>>>>>>> 7c699bd4ee3c46cb11ca30fd0e30d4c1767e660a
>>>>>>> f8fcb17699a047662b6d41157c79ca9cfcc17c41
            Reader.Write("AutoPlay", "AutoSwapPoison", AutoSwapPoison);
            Reader.Write("AutoPlay", "AutoPotHPPercent", AutoPotHPPercent);
            Reader.Write("AutoPlay", "AutoPotMPPercent", AutoPotMPPercent);
            Reader.Write("AutoPlay", "AutoSearchRange", AutoSearchRange);
            Reader.Write("AutoPlay", "AutoAttackIgnore", AutoAttackIgnore);
            Reader.Write("AutoPlay", "AutoSaintKeywords", AutoSaintKeywords);
            // 落盘的是玩家偏好，不是服务器当前下发值，否则登录一次偏好就被清空
            Reader.Write("AutoPlay", "NoLamp", PreferredNoLamp);
            Reader.Write("AutoPlay", "WalkThrough", PreferredWalkThrough);
            Reader.Write("AutoPlay", "NoRunUp", PreferredNoRunUp);
            Reader.Write("AutoPlay", "OverWeight", PreferredOverWeight);
            Reader.Write("AutoPlay", "MountTai", PreferredMountTai);

            for (int i = 0; i < SkillbarLocation.Length / 2; i++)
            {

                Reader.Write("Game", "Skillbar" + i.ToString() + "X", SkillbarLocation[i, 0]);
                Reader.Write("Game", "Skillbar" + i.ToString() + "Y", SkillbarLocation[i, 1]);
            }

            //Chat
            Reader.Write("Chat", "ShowNormalChat", ShowNormalChat);
            Reader.Write("Chat", "ShowYellChat", ShowYellChat);
            Reader.Write("Chat", "ShowWhisperChat", ShowWhisperChat);
            Reader.Write("Chat", "ShowLoverChat", ShowLoverChat);
            Reader.Write("Chat", "ShowMentorChat", ShowMentorChat);
            Reader.Write("Chat", "ShowGroupChat", ShowGroupChat);
            Reader.Write("Chat", "ShowGuildChat", ShowGuildChat);

            //Filters
            Reader.Write("Filter", "FilterNormalChat", FilterNormalChat);
            Reader.Write("Filter", "FilterWhisperChat", FilterWhisperChat);
            Reader.Write("Filter", "FilterShoutChat", FilterShoutChat);
            Reader.Write("Filter", "FilterSystemChat", FilterSystemChat);
            Reader.Write("Filter", "FilterLoverChat", FilterLoverChat);
            Reader.Write("Filter", "FilterMentorChat", FilterMentorChat);
            Reader.Write("Filter", "FilterGroupChat", FilterGroupChat);
            Reader.Write("Filter", "FilterGuildChat", FilterGuildChat);

            //AutoPatcher
            Reader.Write("Launcher", "Enabled", P_Patcher);
            Reader.Write("Launcher", "Host", P_Host);
            Reader.Write("Launcher", "PatchFile", P_PatchFileName);
            Reader.Write("Launcher", "NeedLogin", P_NeedLogin);
            Reader.Write("Launcher", "Login", P_Login);
            Reader.Write("Launcher", "Password", P_Password);
            Reader.Write("Launcher", "ServerName", P_ServerName);
            Reader.Write("Launcher", "Browser", P_BrowserAddress);
            Reader.Write("Launcher", "AutoStart", P_AutoStart);

            //微端
            Reader.Write("MicroClient", "Enabled", MicroClient);
            Reader.Write("MicroClient", "Host", MicroHost);
            Reader.Write("MicroClient", "Timeout", MicroTimeout);
            Reader.Write("MicroClient", "Log", MicroLog);
            Reader.Write("MicroClient", "Concurrency", MicroConcurrency);
            Reader.Write("MicroClient", "RateLimit", MicroRateLimit);
            Reader.Write("MicroClient", "Hint", MicroHint);
            Reader.Write("Launcher", "ConcurrentDownloads", P_Concurrency);
        }

        public static void LoadTrackedQuests(string charName)
        {
            //Quests
            for (int i = 0; i < TrackedQuests.Length; i++)
            {
                TrackedQuests[i] = QuestTrackingReader.ReadInt32(charName, "Quest-" + i.ToString(), -1);
            }
        }

        public static void SaveTrackedQuests(string charName)
        {
            //Quests
            for (int i = 0; i < TrackedQuests.Length; i++)
            {
                QuestTrackingReader.Write(charName, "Quest-" + i.ToString(), TrackedQuests[i]);
            }
        }
    }

    
}
