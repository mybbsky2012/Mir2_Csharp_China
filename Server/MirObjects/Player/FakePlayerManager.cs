using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;

namespace Server.MirObjects
{
    /// <summary>
    /// 假人（AI 玩家）管理器。
    ///
    /// 职责：
    ///   1. 服务端启动时按配置生成 N 个假人（等级 / 职业 / 性别 / 装备 / 技能都是随机生成的，
    ///      重启即重新生成，不写入数据库、不进角色列表）；
    ///   2. 维持「随机上下线」——一段时间后有人下线，过一会儿又换个人上线；
    ///   3. 给 AI 大脑提供工具函数：挑练级图、回城、补药、台词库；
    ///   4. 提供给 GM 命令 @假人 的查询 / 开关 / 重生成接口。
    ///
    /// 注意：假人的 CharacterInfo / AccountInfo 只存在于内存，**不会**出现在
    /// Envir.CharacterList / Envir.AccountList 里，所以：
    ///   - 存档循环不会碰它们（重启就没了，正是我们要的）；
    ///   - 它们不会出现在排行榜里（Envir.CheckRankUpdate 里也做了兜底排除）。
    /// </summary>
    public static class FakePlayerManager
    {
        private static Envir Envir => Envir.Main;

        // 假人专用的 ID 段，避开真实角色（真实角色 ID 从 1 开始递增）
        private const int AccountIndexBase = 900000;
        private const int CharacterIndexBase = 900000;
        private const int SessionIdBase = 950000;

        /// <summary>
        /// 穿装备时的「等级相称窗口」：只穿等级需求在自己身上 Level-15 ~ Level 之间的装备。
        /// 放宽这个值会让假人穿到更强的装备（也更容易撞上 GM / 活动装）。
        /// </summary>
        private const int GearLevelWindow = 15;

        private class BotRecord
        {
            public int Seq;
            public string Name;
            public MirClass Class;
            public MirGender Gender;
            public ushort Level;

            public AccountInfo Account;
            public CharacterInfo Info;

            public int HomeMapIndex;

            public bool HasSpawn;         // true = 用了「登录点配置」里指定的地图/坐标
            public BotSpawn Spawn;        // 上面那个登录点

            public bool Online;
            public FakePlayerObject Bot;

            public long NextLoginTime;    // 离线中：到这个时间尝试上线
            public long NextLogoutTime;   // 在线中：到这个时间考虑下线

            public bool ForceRelogin;     // 大脑请求「立刻下线、稍后自动重登」（例如跟随的队长阵亡）
            public long ReloginDelay;     // 上面那种情况下的离线时长

            public int FailCount;         // 连续上线失败次数（防止死循环）
        }

        /// <summary>
        /// 一个「登录点」：假人上线时直接就站在这张图的这个坐标上。
        /// 由 Configs/FakePlayerSpawns.txt（或 Setup.ini 的 Spawns=）配置，可以写很多个，
        /// 假人会被均匀铺到这些点上 —— 想让假人集中在某几张图，就把这几张图的坐标写进来。
        /// </summary>
        public struct BotSpawn
        {
            public int MapIndex;
            public Point Location;
        }

        private static readonly List<BotRecord> Records = new List<BotRecord>();
        private static readonly List<string> ChatLines = new List<string>();
        private static readonly List<BotEquipSet> EquipSets = new List<BotEquipSet>();
        private static readonly HashSet<string> MissingEquipNames = new HashSet<string>();

        private static readonly List<int> HuntMaps = new List<int>();
        private static readonly List<int> TownMaps = new List<int>();

        private static readonly List<BotSpawn> SpawnPoints = new List<BotSpawn>();   // 配置的登录点
        private static readonly List<int> SpawnMaps = new List<int>();               // 登录点涉及的地图

        private static long _nextTick;
        private static bool _started;

        // ---------------- 对外状态 ----------------
        public static bool Enabled { get; private set; }
        public static bool ChatEnabled { get; private set; }
        public static int ConfigCount { get; private set; }
        public static int EquipSetCount => EquipSets.Count;
        public static int OnlineCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Records.Count; i++)
                    if (Records[i].Online) n++;
                return n;
            }
        }

        // ==================================================================================
        //  启动 / 停止
        // ==================================================================================
        /// <summary>由 Envir.StartEnvir() 调用（此时地图、物品、技能表都已就绪）</summary>
        public static void Init()
        {
            Enabled = Settings.FakePlayerEnabled;
            ChatEnabled = Settings.FakePlayerChat;
            ConfigCount = Settings.FakePlayerCount;

            Close();

            if (!Enabled || ConfigCount <= 0)
            {
                if (Enabled) MessageQueue.Instance.Enqueue("[假人] 数量配置为 0，未生成假人");
                return;
            }

            LoadChatLines();
            LoadEquipSets();
            LoadSpawnPoints();   // 必须先于 BuildHuntMaps：配了登录点就让假人主要待在那些图上
            BuildHuntMaps();
            PrioritizeSpawnMaps();

            if (HuntMaps.Count == 0)
            {
                MessageQueue.Instance.Enqueue("[假人] 找不到可用的活动地图（没有任何怪物刷新点），假人系统未启动");
                Enabled = false;
                return;
            }

            BuildRecords();

            int ok = 0;
            for (int i = 0; i < Records.Count; i++)
                if (Login(Records[i])) ok++;

            _started = true;
            _nextTick = Envir.Time + 1000;

            MessageQueue.Instance.Enqueue($"[假人] 已生成 {ok}/{Records.Count} 个假人，活动地图 {HuntMaps.Count} 张，在线 {OnlineCount} 人");
        }

        /// <summary>关服 / 重新初始化时清理</summary>
        public static void Close()
        {
            for (int i = 0; i < Records.Count; i++)
            {
                BotRecord rec = Records[i];

                if (rec.Online && rec.Bot != null && rec.Bot.Node != null)
                {
                    try { rec.Bot.StopGame(0); }
                    catch { /* 关服阶段，忽略 */ }
                }

                rec.Online = false;
                rec.Bot = null;
            }

            Records.Clear();
            HuntMaps.Clear();
            SpawnPoints.Clear();
            SpawnMaps.Clear();
            _started = false;
        }

        /// <summary>由 Envir.Process() 每帧调用（内部自带 1 秒节流）</summary>
        public static void Process()
        {
            if (!Enabled || !_started) return;
            if (Envir.Time < _nextTick) return;

            _nextTick = Envir.Time + 1000;

            // 性能探针：每秒采一次样（假人 Process 耗时 / 大脑耗时 / 主循环周期峰值）
            FakePlayerPerf.Sample(Envir.LastRunTime);

            for (int i = 0; i < Records.Count; i++)
            {
                BotRecord rec = Records[i];

                // 大脑主动请求的「立刻下线重登」优先处理（例如跟着的队长阵亡了），
                // 不受随机上下线开关影响。特意放在 Envir.Process 里执行：
                // 这时已经脱离了主循环对 Objects 链表的遍历，StopGame 立刻拆对象是安全的。
                if (rec.Online && rec.ForceRelogin)
                {
                    rec.ForceRelogin = false;
                    Logout(rec, rec.ReloginDelay);
                    continue;
                }

                if (!Settings.FakePlayerRandomLogin) continue;

                if (rec.Online)
                {
                    // 正在组队跟随真人玩家 → 别随机下线，不然就跟丢人了（组队结束自然会回到常规节奏）
                    if (rec.Bot != null && rec.Bot.GroupMembers != null) continue;

                    if (Envir.Time < rec.NextLogoutTime) continue;
                    Logout(rec);
                }
                else
                {
                    if (Envir.Time < rec.NextLoginTime) continue;
                    Login(rec);
                }
            }
        }

        /// <summary>
        /// 大脑请求「这个假人立刻下线，过几秒再自己上来」。
        /// 只打标记，真正的下线交给 Process()（主循环之外）执行 ——
        /// 大脑是在 PlayerObject.Process() 里跑的，那个时刻不能直接拆对象。
        /// </summary>
        internal static void RequestRelogin(FakePlayerObject bot)
        {
            if (bot == null) return;

            for (int i = 0; i < Records.Count; i++)
            {
                BotRecord rec = Records[i];
                if (rec.Bot != bot) continue;

                if (rec.ForceRelogin) return;

                rec.ForceRelogin = true;
                rec.ReloginDelay = Envir.Random.Next(4000, 15000);

                return;
            }
        }

        // ==================================================================================
        //  生成假人档案
        // ==================================================================================
        // ==================================================================================
        //  登录点（Configs/FakePlayerSpawns.txt 或 Setup.ini 的 Spawns=）
        //
        //  不配的时候，假人是从「活动地图」里随机挑一张、再在图上随机找个点落下来，
        //  所以看起来会到处乱冒、还可能全挤在出生点附近。
        //  配了登录点之后，假人只在这些「地图 + 坐标」上上线，而且是均匀铺开的。
        //
        //  支持的写法（一行一个点，# ; // 开头是注释）：
        //      0 330 330            ← 地图序号 + X + Y
        //      0,330,330            ← 逗号也行
        //      比奇省 330 330        ← 地图名（FileName 或 Title，不区分大小写）
        //      比奇省:330,330        ← 冒号也行
        //  Setup.ini 里的一行写法（多个点用分号隔开）：
        //      Spawns=0:330,330; 比奇省:300,300
        // ==================================================================================
        private static void LoadSpawnPoints()
        {
            SpawnPoints.Clear();
            SpawnMaps.Clear();

            // 1) Setup.ini 里的内联写法（分号分隔多个点）
            ParseSpawnText(Settings.FakePlayerSpawns, "Setup.ini [FakePlayer] Spawns");

            // 2) 登录点文件
            string path = Settings.FakePlayerSpawnFile;

            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    if (!Path.IsPathRooted(path))
                        path = Path.Combine(Settings.ConfigPath, path);

                    if (!File.Exists(path))
                    {
                        File.WriteAllText(path, DefaultSpawnFile);
                        MessageQueue.Instance.Enqueue("[假人] 未找到登录点文件，已生成模板 Configs/FakePlayerSpawns.txt（当前仍按活动地图随机登录）");
                    }
                    else
                    {
                        string[] lines = File.ReadAllLines(path);

                        for (int i = 0; i < lines.Length; i++)
                        {
                            string line = lines[i].Trim();

                            if (line.Length == 0) continue;
                            if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//")) continue;

                            AddSpawnEntry(line, $"FakePlayerSpawns.txt 第 {i + 1} 行");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageQueue.Instance.Enqueue($"[假人] 登录点文件读取失败：{ex.Message}");
                }
            }

            if (SpawnPoints.Count > 0)
                MessageQueue.Instance.Enqueue($"[假人] 已加载登录点 {SpawnPoints.Count} 个，分布在 {SpawnMaps.Count} 张地图上");
            else
                MessageQueue.Instance.Enqueue("[假人] 未配置登录点，按活动地图随机登录");
        }

        private static void ParseSpawnText(string text, string source)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            string[] parts = text.Split(new[] { ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                string s = parts[i].Trim();
                if (s.Length == 0) continue;

                AddSpawnEntry(s, source);
            }
        }

        /// <summary>解析一条「地图 X Y」，校验通过就加进登录点列表</summary>
        private static void AddSpawnEntry(string line, string source)
        {
            // 逗号 / 冒号 / 等号 / 制表符 全部当空格看
            string norm = line.Replace(',', ' ').Replace('：', ' ').Replace(':', ' ')
                               .Replace('=', ' ').Replace('\t', ' ');

            string[] t = norm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (t.Length < 3)
            {
                MessageQueue.Instance.Enqueue($"[假人] 登录点格式不对（要写「地图 X Y」）：{source} → {line}");
                return;
            }

            // 地图：先按序号，再按 FileName / Title
            Map map = null;

            if (int.TryParse(t[0], out int index))
                map = Envir.GetMap(index);

            if (map == null)
            {
                map = Envir.MapList.FirstOrDefault(m => m.Info != null &&
                    (string.Equals(m.Info.FileName, t[0], StringComparison.CurrentCultureIgnoreCase) ||
                     string.Equals(m.Info.Title, t[0], StringComparison.CurrentCultureIgnoreCase)));
            }

            if (map == null)
            {
                MessageQueue.Instance.Enqueue($"[假人] 登录点里的地图找不到：{t[0]}（{source}）");
                return;
            }

            if (!int.TryParse(t[1], out int x) || !int.TryParse(t[2], out int y))
            {
                MessageQueue.Instance.Enqueue($"[假人] 登录点坐标不是数字：{line}（{source}）");
                return;
            }

            Point p = new Point(x, y);

            if (map.WalkableCells == null || map.WalkableCells.Count == 0)
            {
                MessageQueue.Instance.Enqueue($"[假人] 登录点所在的地图没有可走格子：{map.Info.FileName}（{source}）");
                return;
            }

            if (!map.ValidPoint(p))
            {
                MessageQueue.Instance.Enqueue($"[假人] 登录点站不住（墙里 / 越界）：{map.Info.FileName} {x},{y}（{source}）");
                return;
            }

            for (int i = 0; i < SpawnPoints.Count; i++)
                if (SpawnPoints[i].MapIndex == map.Info.Index && SpawnPoints[i].Location == p) return;   // 重复的忽略

            SpawnPoints.Add(new BotSpawn { MapIndex = map.Info.Index, Location = p });

            if (!SpawnMaps.Contains(map.Info.Index)) SpawnMaps.Add(map.Info.Index);
        }

        private const string DefaultSpawnFile =
            "# 假人登录点：每行写一个「地图 X Y」，假人上线时会均匀铺在这些点上。\r\n" +
            "# 不配置（或全部注释掉）时，假人会在活动地图上随机找位置登录。\r\n" +
            "# 地图可以写序号、也可以写地图名；# ; // 开头的是注释。\r\n" +
            "#\r\n" +
            "# 例子（把前面的 # 去掉就能用）：\r\n" +
            "# 0 330 330            地图序号 0，坐标 330,330\r\n" +
            "# 比奇省 300 300        直接写地图名也行\r\n" +
            "# 0,320,340            逗号分隔同样可以\r\n" +
            "#\r\n" +
            "# 也可以写多个点让假人铺满一张图：\r\n" +
            "# 0 300 300\r\n" +
            "# 0 340 300\r\n" +
            "# 0 300 340\r\n" +
            "# 0 340 340\r\n";

        private static void BuildHuntMaps()
        {
            HuntMaps.Clear();

            // 1) 配置里写了就用配置的
            string cfg = Settings.FakePlayerMaps;
            if (!string.IsNullOrWhiteSpace(cfg))
            {
                string[] parts = cfg.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < parts.Length; i++)
                {
                    string key = parts[i].Trim();
                    if (key.Length == 0) continue;

                    Map map = null;

                    if (int.TryParse(key, out int index))
                        map = Envir.GetMap(index);

                    if (map == null)
                    {
                        map = Envir.MapList.FirstOrDefault(m =>
                            string.Equals(m.Info.FileName, key, StringComparison.CurrentCultureIgnoreCase) ||
                            string.Equals(m.Info.Title, key, StringComparison.CurrentCultureIgnoreCase));
                    }

                    if (map == null)
                    {
                        MessageQueue.Instance.Enqueue($"[假人] 配置里的地图找不到：{key}");
                        continue;
                    }

                    if (!HuntMaps.Contains(map.Info.Index)) HuntMaps.Add(map.Info.Index);
                }

                if (HuntMaps.Count > 0)
                {
                    MessageQueue.Instance.Enqueue($"[假人] 使用配置指定的活动地图 {HuntMaps.Count} 张");
                    return;
                }
            }

            // 2) 没配置 → 自动挑选：
            //    ① 先把「出生点所在的地图（也就是主城）」加进来 —— 这样玩家一上线就能在城里
            //       看见有假人在走动、说话，而不是空荡荡一片；
            //    ② 再按地图序号从小到大，取前 20 张「有怪物刷新点」的地图作为练级图。
            for (int i = 0; i < Envir.StartPoints.Count; i++)
            {
                SafeZoneInfo sz = Envir.StartPoints[i];
                if (sz.Info == null) continue;

                if (!TownMaps.Contains(sz.Info.Index)) TownMaps.Add(sz.Info.Index);
                if (!HuntMaps.Contains(sz.Info.Index)) HuntMaps.Add(sz.Info.Index);
            }

            int townCount = HuntMaps.Count;

            List<Map> list = new List<Map>();

            for (int i = 0; i < Envir.MapList.Count; i++)
            {
                Map map = Envir.MapList[i];
                if (map.Info == null) continue;
                if (map.Respawns == null || map.Respawns.Count == 0) continue;

                list.Add(map);
            }

            list.Sort((a, b) => a.Info.Index.CompareTo(b.Info.Index));

            int take = Math.Min(20, list.Count);
            for (int i = 0; i < take; i++)
            {
                if (HuntMaps.Contains(list[i].Info.Index)) continue;
                HuntMaps.Add(list[i].Info.Index);
            }

            if (HuntMaps.Count > 0)
                MessageQueue.Instance.Enqueue($"[假人] 自动挑选活动地图 {HuntMaps.Count} 张（主城 {townCount} 张 + 有怪物刷新的图 {HuntMaps.Count - townCount} 张）");
        }

        /// <summary>
        /// 配了登录点 → 把登录点所在的图提到活动地图列表的最前面。
        /// 这样「补给完挑张图继续练级」时也会优先回到这些图，而不是散到全服各地。
        /// </summary>
        private static void PrioritizeSpawnMaps()
        {
            if (SpawnMaps.Count == 0) return;

            List<int> ordered = new List<int>();

            for (int i = 0; i < SpawnMaps.Count; i++)
                if (!ordered.Contains(SpawnMaps[i])) ordered.Add(SpawnMaps[i]);

            for (int i = 0; i < HuntMaps.Count; i++)
                if (!ordered.Contains(HuntMaps[i])) ordered.Add(HuntMaps[i]);

            HuntMaps.Clear();
            HuntMaps.AddRange(ordered);

            MessageQueue.Instance.Enqueue($"[假人] 登录点所在地图 {SpawnMaps.Count} 张已置顶为优先活动地图");
        }

        private static void BuildRecords()
        {
            // 名字尽量不重复
            HashSet<string> used = new HashSet<string>();

            // 登录点：先打乱一次，再按顺序轮流分给每个假人 ——
            // 这样 50 个假人会**均匀**铺到配置的各个点上，而不是随机全挤到同一个点。
            int[] spawnOrder = null;

            if (SpawnPoints.Count > 0)
            {
                spawnOrder = new int[SpawnPoints.Count];

                for (int i = 0; i < spawnOrder.Length; i++) spawnOrder[i] = i;

                for (int i = spawnOrder.Length - 1; i > 0; i--)
                {
                    int j = Envir.Random.Next(i + 1);
                    int tmp = spawnOrder[i];
                    spawnOrder[i] = spawnOrder[j];
                    spawnOrder[j] = tmp;
                }
            }

            for (int i = 0; i < ConfigCount; i++)
            {
                BotRecord rec = new BotRecord
                {
                    Seq = i + 1,
                    Class = (MirClass)Envir.Random.Next(5),
                    Gender = (MirGender)Envir.Random.Next(2),
                };

                if (spawnOrder != null)
                {
                    rec.HasSpawn = true;
                    rec.Spawn = SpawnPoints[spawnOrder[i % spawnOrder.Length]];
                    rec.HomeMapIndex = rec.Spawn.MapIndex;
                }
                else
                {
                    rec.HomeMapIndex = HuntMaps[Envir.Random.Next(HuntMaps.Count)];
                }

                int min = Math.Max(1, Settings.FakePlayerLevelMin);
                int max = Math.Max(min, Settings.FakePlayerLevelMax);
                rec.Level = (ushort)Envir.Random.Next(min, max + 1);

                rec.Name = MakeName(used);

                BuildAccountInfo(rec);

                Records.Add(rec);
            }
        }

        private static string MakeName(HashSet<string> used)
        {
            string[] first = { "风", "云", "雷", "龙", "战", "天", "海", "刀", "剑", "影", "玄", "冰", "火", "狂",
                               "醉", "紫", "青", "银", "血", "铁", "孤", "长", "无", "夜", "月", "雪", "小", "老", "大" };

            string[] second = { "星辰", "之痕", "无双", "天下", "追风", "逐月", "惊雷", "若尘", "清歌", "一刀",
                                "屠龙", "破军", "逍遥", "忘忧", "流年", "拾光", "不悔", "长歌", "微凉", "半夏",
                                "千寻", "万古", "如故", "归尘", "逐鹿", "听雨", "观澜", "问天", "朝歌", "暮雪" };

            for (int attempt = 0; attempt < 60; attempt++)
            {
                string name = first[Envir.Random.Next(first.Length)] + second[Envir.Random.Next(second.Length)];

                if (used.Contains(name)) continue;

                // 不能和真实角色重名
                if (Envir.GetCharacterInfo(name) != null) continue;

                used.Add(name);
                return name;
            }

            // 兜底：加序号
            string fallback;
            int n = 1;
            do { fallback = "行者" + n++; } while (used.Contains(fallback));
            used.Add(fallback);
            return fallback;
        }

        private static void BuildAccountInfo(BotRecord rec)
        {
            AccountInfo account = new AccountInfo
            {
                Index = AccountIndexBase + rec.Seq,
                AccountID = "__bot_" + rec.Seq,
                UserName = rec.Name,
                CreationIP = "bot",
                CreationDate = Envir.Now,
                LastDate = Envir.Now,
                Gold = 2000000,
                AdminAccount = false,
            };

            CharacterInfo info = new CharacterInfo
            {
                Index = CharacterIndexBase + rec.Seq,
                Name = rec.Name,
                Class = rec.Class,
                Gender = rec.Gender,
                Level = rec.Level,
                Hair = (byte)Envir.Random.Next(0, 9),
                CreationIP = "bot",
                CreationDate = Envir.Now,
                AccountInfo = account,

                AMode = AttackMode.All,     // 敢打怪（Peace 模式下服务端会拒绝一切攻击）
                PMode = PetMode.Both,
                AllowGroup = true,
                AllowTrade = true,

                MaximumHeroCount = 1,
                Heroes = new HeroInfo[1],
            };

            SetBindPoint(info, rec);

            account.Characters.Add(info);

            rec.Account = account;
            rec.Info = info;
        }

        /// <summary>给假人设一个复活点（模拟「新手村出生点」）</summary>
        private static void SetBindPoint(CharacterInfo info, BotRecord rec)
        {
            // 配了登录点 → 出生/复活点也跟着走。
            // 不这么做的话，假人一死就 TownRevive 回主城，你在指定地图上就再也看不到它们了。
            if (rec != null && rec.HasSpawn)
            {
                Map spawnMap = Envir.GetMap(rec.Spawn.MapIndex);

                if (spawnMap != null && spawnMap.Info != null)
                {
                    // 复活点尽量落在该图的安全区，免得在野外复活后立刻又被怪打死
                    SafeZoneInfo sz = FindSafeZone(spawnMap);

                    info.BindMapIndex = spawnMap.Info.Index;
                    info.BindLocation = (sz != null && spawnMap.ValidPoint(sz.Location)) ? sz.Location : rec.Spawn.Location;

                    info.CurrentMapIndex = spawnMap.Info.Index;
                    info.CurrentLocation = rec.Spawn.Location;
                    return;
                }
            }

            if (Envir.StartPoints.Count > 0)
            {
                SafeZoneInfo szi = Envir.StartPoints[Envir.Random.Next(Envir.StartPoints.Count)];

                if (szi.Info != null)
                {
                    info.BindMapIndex = szi.Info.Index;
                    info.BindLocation = szi.Location;

                    info.CurrentMapIndex = szi.Info.Index;
                    info.CurrentLocation = szi.Location;
                    return;
                }
            }

            // 没有出生点配置 → 用第一张练级图兜底
            Map map = Envir.GetMap(HuntMaps[0]);

            if (map == null) return;

            info.BindMapIndex = map.Info.Index;
            info.CurrentMapIndex = map.Info.Index;
            info.BindLocation = PickSpawnPoint(map);
            info.CurrentLocation = info.BindLocation;
        }

        // ==================================================================================
        //  上线下线
        // ==================================================================================
        private static bool Login(BotRecord rec)
        {
            if (rec.Online) return false;
            if (rec.FailCount >= 5) return false;   // 一再失败就别试了

            // 每次上线都从登录点配置里**随机**挑一个点（地图、坐标都随机）。
            // 之前是「一个假人固定绑一个点」，假人每次重登都回同一个位置，看起来太死板。
            // 随机挑 + 50 个假人多次上下线，自然会均匀铺开；复活点也跟着这次的点走，
            // 不然死了会飘回上次的老地方。
            if (SpawnPoints.Count > 0)
            {
                BotSpawn pick = SpawnPoints[Envir.Random.Next(SpawnPoints.Count)];
                Map pickMap = Envir.GetMap(pick.MapIndex);

                if (pickMap != null && pickMap.WalkableCells != null && pickMap.WalkableCells.Count > 0
                    && pickMap.ValidPoint(pick.Location))
                {
                    rec.HasSpawn = true;
                    rec.Spawn = pick;
                    rec.HomeMapIndex = pick.MapIndex;

                    SetBindPoint(rec.Info, rec);   // 复活点跟着这次的登录点走
                }
            }

            Map map = Envir.GetMap(rec.HomeMapIndex);

            if (map == null || map.WalkableCells == null || map.WalkableCells.Count == 0)
            {
                rec.FailCount++;
                return false;
            }

            // 配了登录点 → 就落在那个点上（万一站不住，才退回「图上随机找点」）
            Point spawn = rec.HasSpawn && map.ValidPoint(rec.Spawn.Location)
                ? rec.Spawn.Location
                : PickSpawnPoint(map);

            if (spawn == Point.Empty)
            {
                rec.FailCount++;
                return false;
            }

            // 每次重登都换一身状态：位置随机、血量清 0（Load 里会自动补满）
            rec.Info.CurrentMapIndex = map.Info.Index;
            rec.Info.CurrentLocation = spawn;
            rec.Info.Direction = (MirDirection)Envir.Random.Next(8);
            rec.Info.HP = 0;

            BotConnection connection = new BotConnection(SessionIdBase + rec.Seq)
            {
                Account = rec.Account,
            };

            FakePlayerObject bot;

            try
            {
                bot = new FakePlayerObject(rec.Info, connection);
            }
            catch (Exception ex)
            {
                rec.FailCount++;
                MessageQueue.Instance.Enqueue($"[假人] {rec.Name} 创建失败：{ex.Message}");
                return false;
            }

            connection.Player = bot;
            rec.Account.Connection = connection;

            // 驻地：配了登录点就用登录点，否则沿用 Bind（主城出生点）。
            // 「回城补给」会回到这里，所以配了登录点的假人会一直在你指定的地图上活动。
            if (rec.HasSpawn)
            {
                bot.HasHome = true;
                bot.HomeMapIndex = rec.Spawn.MapIndex;
                bot.HomeLocation = rec.Spawn.Location;
            }
            else
            {
                bot.HasHome = true;
                bot.HomeMapIndex = rec.Info.BindMapIndex;
                bot.HomeLocation = rec.Info.BindLocation;
            }

            // 假人开启「超负重」：不会被负重卡住走不动路，也不会因为穿装备超重而光着身子
            bot.OverWeight = true;

            // 等级、装备、技能
            bot.RefreshStats();
            ApplyEquipSet(bot);      // 装备方案（含等级）：有方案就听方案的
            EquipBasicGear(bot);     // 方案没写的部位 / 方案物品不存在 → 自动配装兜底
            GivePotions(bot);
            LearnBasicSpells(bot);   // 必须在 new FakePlayerBrain 之前，大脑构造时就要挑技能

            bot.RefreshStats();

            bot.Brain = new FakePlayerBrain(bot);

            if (!Spawn(bot))
            {
                rec.FailCount++;
                return false;
            }

            rec.Bot = bot;
            rec.Online = true;
            rec.FailCount = 0;

            // 在线时长 25~80 分钟
            rec.NextLogoutTime = Envir.Time + Envir.Random.Next(25 * 60 * 1000, 80 * 60 * 1000);

            // 上线的第一个 3~20 秒先别动，装成「刚进游戏在读条」
            bot.Brain.BeginIdle(Envir.Random.Next(3000, 20000));

            // 假人计入「在线人数」，但不进排行榜（排行榜靠 Envir.CheckRankUpdate 里排除）。
            // 下线时的减法由 PlayerObject.StopGame() 自己完成，这里不要重复扣。
            Envir.OnlineRankingCount[0]++;
            Envir.OnlineRankingCount[(int)bot.Class + 1]++;

            return true;
        }

        private static void Logout(BotRecord rec, long offlineMs = -1)
        {
            if (!rec.Online) return;

            FakePlayerObject bot = rec.Bot;

            if (bot != null && bot.Node != null)
            {
                try
                {
                    // 23 = 返回人物选择界面（最常见的「玩家自己下线」）。
                    // StopGame 内部会 LeaveGroup()，所以下线不会在队伍里留一个幽灵队友。
                    bot.StopGame(23);
                }
                catch (Exception ex)
                {
                    MessageQueue.Instance.Enqueue($"[假人] {rec.Name} 下线异常：{ex.Message}");
                }
            }

            rec.Bot = null;
            rec.Online = false;

            // 离线 2~12 分钟（「立刻重登」那种走调用方给的短时长）
            rec.NextLoginTime = Envir.Time + (offlineMs > 0 ? offlineMs : Envir.Random.Next(2 * 60 * 1000, 12 * 60 * 1000));
        }

        /// <summary>把假人真正放进世界：等同于 PlayerObject.StartGame() 的轻量版（不推地图/物品/任务全量包）</summary>
        private static bool Spawn(FakePlayerObject bot)
        {
            Map map = Envir.GetMap(bot.Info.CurrentMapIndex);

            if (map == null || !map.ValidPoint(bot.Info.CurrentLocation))
            {
                map = Envir.GetMap(bot.Info.BindMapIndex);

                if (map == null || !map.ValidPoint(bot.Info.BindLocation)) return false;

                bot.Info.CurrentMapIndex = bot.Info.BindMapIndex;
                bot.Info.CurrentLocation = bot.Info.BindLocation;
            }

            map.AddObject(bot);
            bot.CurrentMap = map;
            Envir.Players.Add(bot);

            bot.Spawned();

            return true;
        }

        /// <summary>在这张图上找个配置好的登录点（同一张图配了多个点时会轮流用，不会总落同一个）</summary>
        private static bool TryGetSpawnPoint(int mapIndex, out Point p)
        {
            p = Point.Empty;

            if (SpawnPoints.Count == 0) return false;

            int start = Envir.Random.Next(SpawnPoints.Count);

            for (int i = 0; i < SpawnPoints.Count; i++)
            {
                BotSpawn s = SpawnPoints[(start + i) % SpawnPoints.Count];
                if (s.MapIndex != mapIndex) continue;

                p = s.Location;
                return true;
            }

            return false;
        }

        private static Point PickSpawnPoint(Map map)
        {
            if (map.WalkableCells == null || map.WalkableCells.Count == 0) return Point.Empty;

            for (int i = 0; i < 30; i++)
            {
                Point p = map.WalkableCells[Envir.Random.Next(map.WalkableCells.Count)];

                if (!map.ValidPoint(p)) continue;
                if (map.GetSafeZone(p) != null) continue;   // 别站在安全区里（补给时会专门去）

                return p;
            }

            return map.WalkableCells[Envir.Random.Next(map.WalkableCells.Count)];
        }

        // ==================================================================================
        //  装备 / 药品 / 技能
        // ==================================================================================
        private static int ClassFlag(MirClass cls) => 1 << (int)cls;

        private static int InfoScore(ItemInfo info)
        {
            if (info == null) return 0;

            return info.Stats[Stat.MaxDC]
                 + info.Stats[Stat.MaxMC]
                 + info.Stats[Stat.MaxSC]
                 + info.Stats[Stat.MaxAC] * 2
                 + info.Stats[Stat.MaxMAC] * 2
                 + info.Stats[Stat.HP] / 2;
        }

        private static bool GenderOk(ItemInfo info, MirGender gender)
        {
            return gender == MirGender.男性
                ? info.RequiredGender.HasFlag(RequiredGender.男性)
                : info.RequiredGender.HasFlag(RequiredGender.女性);
        }

        private static void EquipBasicGear(FakePlayerObject bot)
        {
            int classFlag = ClassFlag(bot.Class);

            EquipSlot(bot, ItemType.武器, EquipmentSlot.武器, classFlag, 0);
            EquipSlot(bot, ItemType.盔甲, EquipmentSlot.盔甲, classFlag, 0);
            EquipSlot(bot, ItemType.头盔, EquipmentSlot.头盔, classFlag, 15);
            EquipSlot(bot, ItemType.项链, EquipmentSlot.项链, classFlag, 15);
            EquipSlot(bot, ItemType.手镯, EquipmentSlot.左手镯, classFlag, 15);
            EquipSlot(bot, ItemType.手镯, EquipmentSlot.右手镯, classFlag, 15);
            EquipSlot(bot, ItemType.戒指, EquipmentSlot.左戒指, classFlag, 15);
            EquipSlot(bot, ItemType.戒指, EquipmentSlot.右戒指, classFlag, 15);
            EquipSlot(bot, ItemType.腰带, EquipmentSlot.腰带, classFlag, 20);
            EquipSlot(bot, ItemType.靴子, EquipmentSlot.靴子, classFlag, 20);
            EquipSlot(bot, ItemType.守护石, EquipmentSlot.守护石, classFlag, 40);
        }

        /// <param name="skipPercent">空手概率（%），让假人的装备看起来参差不齐</param>
        private static void EquipSlot(FakePlayerObject bot, ItemType type, EquipmentSlot slot, int classFlag, int skipPercent)
        {
            if (bot.Info.Equipment[(int)slot] != null) return;   // 装备方案已经配了这个部位，别覆盖
            if (skipPercent > 0 && Envir.Random.Next(100) < skipPercent) return;

            List<ItemInfo> candidates = new List<ItemInfo>();

            // 先只看「和这个假人等级相称」的装备：等级需求落在 [Level - GearLevelWindow, Level]。
            // 这一步能滤掉两类不该出现的东西：
            //   1) 孤鹜、落霞 这种 Lv1 却属性爆表的 GM / 活动装 —— 穿上会一眼假；
            //   2) 等级需求远低于自身的低级白装 —— 穿着寒酸，不像在练级的人。
            CollectCandidates(bot, type, classFlag, bot.Level - GearLevelWindow, candidates);

            // 万一窗口内没候选（有些冷门部位条目很少），放宽成「等级够就能穿」
            if (candidates.Count == 0)
                CollectCandidates(bot, type, classFlag, 0, candidates);

            if (candidates.Count == 0) return;

            // 越强越靠前；然后从「前 5 名」里随机挑一个，避免 50 个假人穿着一模一样
            candidates.Sort((a, b) => InfoScore(b).CompareTo(InfoScore(a)));

            int top = Math.Min(5, candidates.Count);
            int start = Envir.Random.Next(top);

            for (int i = start; i < candidates.Count; i++)
            {
                UserItem item = Envir.CreateFreshItem(candidates[i]);

                if (item.Info.Durability > 0 && item.CurrentDura == 0) continue;
                if (!bot.CanEquipItem(item, (int)slot)) continue;

                bot.Info.Equipment[(int)slot] = item;
                bot.RefreshStats();
                return;
            }
        }

        /// <summary>
        /// 收集某个部位、某个职业、某个性别可用的装备。
        /// minLevel 用来卡住「等级需求下限」，把不该出现的低级神器过滤掉。
        /// </summary>
        private static void CollectCandidates(FakePlayerObject bot, ItemType type, int classFlag, int minLevel, List<ItemInfo> result)
        {
            for (int i = 0; i < Envir.ItemInfoList.Count; i++)
            {
                ItemInfo info = Envir.ItemInfoList[i];

                if (info.Type != type) continue;
                if (((byte)info.RequiredClass & classFlag) == 0) continue;
                if (!GenderOk(info, bot.Gender)) continue;

                if (info.RequiredType == RequiredType.Level)
                {
                    if (info.RequiredAmount > bot.Level) continue;
                    if (info.RequiredAmount < minLevel) continue;
                }
                else if (info.RequiredType == RequiredType.MaxLevel)
                {
                    if (bot.Level > info.RequiredAmount) continue;
                }

                result.Add(info);
            }
        }

        /// <summary>刚上线时把药配齐（和「回城补给」走同一套逻辑，避免两处规则不一致）</summary>
        private static void GivePotions(FakePlayerObject bot)
        {
            RefillPotions(bot);
        }

        private static void AddToBag(FakePlayerObject bot, ItemInfo info, int count)
        {
            if (info == null || info.StackSize == 0) return;

            int left = count;

            for (int i = 0; i < bot.Info.Inventory.Length && left > 0; i++)
            {
                if (bot.Info.Inventory[i] != null) continue;

                UserItem item = Envir.CreateFreshItem(info);
                item.Count = (ushort)Math.Min(left, info.StackSize);

                left -= item.Count;
                bot.Info.Inventory[i] = item;
            }

            bot.RefreshBagWeight();
        }

        // ==================================================================================
        //  技能
        // ==================================================================================
        // Spell 枚举在 Shared/Enums.cs 里就是按职业分段排的，所以直接用数值区间判断归属即可，
        // 不用一一点名 —— 以后服务端技能表怎么加怎么改，这里都不会漏。
        private const int WarriorSpellMin = 1, WarriorSpellMax = 30;      // 战士
        private const int WizardSpellMin = 31, WizardSpellMax = 60;       // 法师
        private const int TaoistSpellMin = 61, TaoistSpellMax = 90;       // 道士
        private const int AssassinSpellMin = 91, AssassinSpellMax = 120;  // 刺客
        private const int ArcherSpellMin = 121, ArcherSpellMax = 150;     // 弓箭

        /// <summary>
        /// 按「职业 + 等级」给假人配技能。
        ///
        /// 只教技能表里真实存在、且等级门槛已经够的技能，技能等级(0~3)也按人物等级推上去。
        /// 这样法师会放雷电、道士会自愈，而不是全员在怪物堆里抡拳头 —— 一行假人代码不写，
        /// 观感却完全是两回事。
        /// </summary>
        private static void LearnBasicSpells(FakePlayerObject bot)
        {
            int min, max;

            switch (bot.Class)
            {
                case MirClass.战士: min = WarriorSpellMin; max = WarriorSpellMax; break;
                case MirClass.法师: min = WizardSpellMin; max = WizardSpellMax; break;
                case MirClass.道士: min = TaoistSpellMin; max = TaoistSpellMax; break;
                case MirClass.刺客: min = AssassinSpellMin; max = AssassinSpellMax; break;
                case MirClass.弓箭: min = ArcherSpellMin; max = ArcherSpellMax; break;
                default: return;
            }

            bot.Info.Magics.Clear();

            byte key = 1;

            for (int s = min; s <= max; s++)
            {
                UserMagic magic = new UserMagic((Spell)s);

                // 服务端技能表里没有这个技能（UserMagic 会去 MagicInfoList 里查）—— 跳过
                if (magic.Info == null) continue;

                // Level1 = 学会这招所需的人物等级
                if (magic.Info.Level1 > bot.Level) continue;

                // 练到几级：Level2 / Level3 分别是升到 2 级 / 3 级的人物等级门槛。
                // 大部分假人练满，少部分差一级，看起来更像真人在练。
                byte lvl = 0;

                if (magic.Info.Level2 > 0 && bot.Level >= magic.Info.Level2) lvl = 1;
                if (magic.Info.Level3 > 0 && bot.Level >= magic.Info.Level3) lvl = 2;

                if (lvl > 0 && Envir.Random.Next(100) < 30) lvl--;

                magic.Level = lvl;
                magic.Key = key;

                if (key < 8) key++;

                bot.Info.Magics.Add(magic);
            }
        }

        // ==================================================================================
        //  给 AI 大脑用的工具
        // ==================================================================================
        /// <summary>补给完毕 → 送回练级图</summary>
        internal static void SendBotHunting(FakePlayerBrain brain, bool force)
        {
            if (brain == null) return;

            FakePlayerObject bot = brain.Player;
            if (bot == null || bot.Node == null || Envir.MapList.Count == 0) return;

            // 药补满
            RefillPotions(bot);

            Map target = null;

            // 配了登录点 → 大多数时候回到登录点所在的图，
            // 这样玩家总是在那几张图里碰得到假人；剩下 25% 让它偶尔换换口味。
            List<int> pool = (SpawnMaps.Count > 0 && Envir.Random.Next(100) < 75) ? SpawnMaps : HuntMaps;
            if (pool.Count == 0) pool = HuntMaps;

            for (int i = 0; i < 12 && target == null; i++)
            {
                int idx = pool[Envir.Random.Next(pool.Count)];
                Map m = Envir.GetMap(idx);

                if (m == null || m.WalkableCells == null || m.WalkableCells.Count == 0) continue;
                if (m == bot.CurrentMap && Envir.Random.Next(2) == 0) continue;   // 有一半概率换张图

                target = m;
            }

            if (target == null) target = bot.CurrentMap;

            // 落点：优先用这张图配置的登录点，没配就在图上随机找
            Point p = Point.Empty;

            if (target.Info != null) TryGetSpawnPoint(target.Info.Index, out p);

            if (p == Point.Empty) p = PickSpawnPoint(target);
            if (p == Point.Empty) return;

            bot.Teleport(target, p, true, 0);

            brain.SetHome(p);
            brain.Restart();
        }

        /// <summary>让假人回城（补给 / 发呆）</summary>
        internal static void SendBotToTown(FakePlayerBrain brain)
        {
            if (brain == null) return;

            FakePlayerObject bot = brain.Player;
            if (bot == null || bot.Node == null) return;

            // 回「驻地」补给（配了登录点时就是配置的那张图/那个点，而不是主城出生点）
            int homeMap = bot.HasHome ? bot.HomeMapIndex : bot.Info.BindMapIndex;
            Point homeLoc = bot.HasHome ? bot.HomeLocation : bot.Info.BindLocation;

            Map town = Envir.GetMap(homeMap) ?? bot.CurrentMap;
            if (town == null) return;

            Point p = homeLoc;

            if (!town.ValidPoint(p))
            {
                SafeZoneInfo sz = FindSafeZone(town);
                p = sz?.Location ?? Point.Empty;
            }

            if (p == Point.Empty || !town.ValidPoint(p)) return;

            bot.Teleport(town, p, true, 0);
            brain.SetHome(p);
        }

        /// <summary>
        /// 把假人送到某个玩家身边（组队跟随时用：队长换图了，假人也跟着「进图」）。
        /// 落点随机取队长周围 3 格内的一个可走点，避免几个假人叠在同一格。
        /// </summary>
        internal static void TeleportNear(FakePlayerObject bot, PlayerObject target)
        {
            if (bot == null || bot.Node == null || target == null || target.Node == null) return;

            Map map = target.CurrentMap;
            if (map == null) return;

            for (int i = 0; i < 24; i++)
            {
                Point p = new Point(
                    target.CurrentLocation.X + Envir.Random.Next(-3, 4),
                    target.CurrentLocation.Y + Envir.Random.Next(-3, 4));

                if (!map.ValidPoint(p)) continue;
                if (p == target.CurrentLocation) continue;

                bot.Teleport(map, p, true, 0);
                return;
            }

            // 周围都落不下 → 就落在队长脚下
            if (map.ValidPoint(target.CurrentLocation))
                bot.Teleport(map, target.CurrentLocation, true, 0);
        }

        /// <summary>
        /// GM 命令 @假人 组队：把身边「闲着」的在线假人拉进喊话者的队伍。
        /// 只设 GroupInvitation（等于服务端发了个组队邀请），假人大脑下一拍会自动同意 ——
        /// 不走 AddMember 是因为那个有邀请冷却，一次只能拉一个人。
        /// </summary>
        internal static string InviteNearbyBots(PlayerObject leader, int max, int range)
        {
            if (leader == null || leader.Node == null || leader.CurrentMap == null) return "当前地图无效";
            if (leader.GroupMembers != null && leader.GroupMembers[0] != leader) return "你不是队长，先自己开一个组";

            int invited = 0;

            for (int i = 0; i < Records.Count && invited < max; i++)
            {
                BotRecord rec = Records[i];

                if (!rec.Online || rec.Bot == null || rec.Bot.Node == null) continue;
                if (rec.Bot == leader) continue;
                if (rec.Bot.CurrentMap != leader.CurrentMap) continue;
                if (rec.Bot.GroupMembers != null || rec.Bot.GroupInvitation != null) continue;
                if (Functions.MaxDistance(leader.CurrentLocation, rec.Bot.CurrentLocation) > range) continue;

                rec.Bot.GroupInvitation = leader;
                invited++;
            }

            if (invited == 0) return $"附近 {range} 格内没有空闲的假人（换个地方，或者走近点再喊一次）";

            return $"已向附近 {invited} 个假人发出组队邀请，它们会自动进组并开始跟随你";
        }

        private static SafeZoneInfo FindSafeZone(Map map)
        {
            for (int i = 0; i < Envir.StartPoints.Count; i++)
            {
                SafeZoneInfo sz = Envir.StartPoints[i];
                if (sz.Info == null || sz.Info.Index != map.Info.Index) continue;
                if (map.ValidPoint(sz.Location)) return sz;
            }

            return null;
        }

        /// <summary>把药补满（模拟「在城里买药」）</summary>
        internal static void RefillPotions(FakePlayerObject bot)
        {
            if (bot == null || bot.Info == null) return;

            ItemInfo hpPot = null, mpPot = null;

            for (int i = 0; i < Envir.ItemInfoList.Count; i++)
            {
                ItemInfo info = Envir.ItemInfoList[i];
                if (info.Type != ItemType.药水 || info.Shape != 0) continue;

                if (info.Stats[Stat.HP] > 0 && (hpPot == null || info.Stats[Stat.HP] > hpPot.Stats[Stat.HP]))
                    hpPot = info;

                if (info.Stats[Stat.MP] > 0 && (mpPot == null || info.Stats[Stat.MP] > mpPot.Stats[Stat.MP]))
                    mpPot = info;
            }

            // 如果血/蓝药够多了就不重复补。给得宽一点（3 组）：练级路上药不够会频繁回城，
            // 而假人「回城 = 传送 + 发呆」，在玩家眼里就是人突然不见了。
            int hpCount = CountItem(bot, hpPot);
            int mpCount = CountItem(bot, mpPot);

            const int potionGroups = 3;

            if (hpPot != null && hpCount < hpPot.StackSize * potionGroups)
                AddToBag(bot, hpPot, hpPot.StackSize * potionGroups - hpCount);

            if (mpPot != null && mpPot != hpPot && mpCount < mpPot.StackSize * potionGroups)
                AddToBag(bot, mpPot, mpPot.StackSize * potionGroups - mpCount);
        }

        private static int CountItem(FakePlayerObject bot, ItemInfo info)
        {
            if (info == null) return 0;

            int n = 0;

            for (int i = 0; i < bot.Info.Inventory.Length; i++)
            {
                UserItem item = bot.Info.Inventory[i];
                if (item == null || item.Info != info) continue;

                n += item.Count;
            }

            return n;
        }

        // ==================================================================================
        //  装备方案（Configs/FakePlayerEquip.txt）
        //  一个文件里写几套方案，假人上线时随机抽一套来穿；等级也可以由方案指定。
        // ==================================================================================

        public class BotEquipSet
        {
            public string Name;
            public int LevelMin, LevelMax;      // 等级=最小-最大；<=0 表示用 Setup.ini 里的全局等级
            public MirClass? ClassFilter;       // 职业=…；null 表示对所有职业生效
            public readonly List<EquipEntry> Entries = new List<EquipEntry>();
        }

        public struct EquipEntry
        {
            public EquipmentSlot Slot;
            public string ItemName;
        }

        private static readonly Dictionary<string, EquipmentSlot> SlotKeys = new Dictionary<string, EquipmentSlot>
        {
            { "武器", EquipmentSlot.武器 },
            { "盔甲", EquipmentSlot.盔甲 },
            { "上衣", EquipmentSlot.盔甲 },
            { "头盔", EquipmentSlot.头盔 },
            { "项链", EquipmentSlot.项链 },
            { "左手镯", EquipmentSlot.左手镯 },
            { "右手镯", EquipmentSlot.右手镯 },
            { "左戒指", EquipmentSlot.左戒指 },
            { "右戒指", EquipmentSlot.右戒指 },
            { "腰带", EquipmentSlot.腰带 },
            { "靴子", EquipmentSlot.靴子 },
            { "守护石", EquipmentSlot.守护石 },
            { "护身符", EquipmentSlot.护身符 },
            { "照明物", EquipmentSlot.照明物 },
        };

        private static void LoadEquipSets()
        {
            EquipSets.Clear();
            MissingEquipNames.Clear();

            string path = Settings.FakePlayerEquipFile;

            try
            {
                if (string.IsNullOrWhiteSpace(path)) return;

                if (!Path.IsPathRooted(path))
                    path = Path.Combine(Settings.ConfigPath, path);

                if (!File.Exists(path))
                {
                    // 生成一份带本服真实物品名的模板，服主照着改就行
                    File.WriteAllText(path, DefaultEquipFile);
                    MessageQueue.Instance.Enqueue("[假人] 未找到装备方案文件，已生成模板 FakePlayerEquip.txt（当前仍用自动配装）");
                    return;
                }

                string[] lines = File.ReadAllLines(path);
                BotEquipSet cur = null;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//")) continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        cur = new BotEquipSet { Name = line.Substring(1, line.Length - 2).Trim() };
                        EquipSets.Add(cur);
                        continue;
                    }

                    int eq = line.IndexOf('=');
                    if (eq <= 0 || cur == null) continue;

                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    if (val.Length == 0) continue;

                    if (key == "等级" || key.Equals("level", StringComparison.OrdinalIgnoreCase))
                    {
                        int dash = val.IndexOfAny(new[] { '-', '~', '－', '～' });
                        int lo, hi;

                        if (dash >= 0 && int.TryParse(val.Substring(0, dash).Trim(), out lo) && int.TryParse(val.Substring(dash + 1).Trim(), out hi))
                        {
                            cur.LevelMin = Math.Min(lo, hi);
                            cur.LevelMax = Math.Max(lo, hi);
                        }
                        else if (int.TryParse(val, out lo))
                        {
                            cur.LevelMin = cur.LevelMax = lo;
                        }
                        continue;
                    }

                    if (key == "职业" || key.Equals("class", StringComparison.OrdinalIgnoreCase))
                    {
                        cur.ClassFilter = ParseClass(val);
                        continue;
                    }

                    // 成对部位：写一次，左右都穿
                    if (key == "手镯")
                    {
                        cur.Entries.Add(new EquipEntry { Slot = EquipmentSlot.左手镯, ItemName = val });
                        cur.Entries.Add(new EquipEntry { Slot = EquipmentSlot.右手镯, ItemName = val });
                        continue;
                    }

                    if (key == "戒指")
                    {
                        cur.Entries.Add(new EquipEntry { Slot = EquipmentSlot.左戒指, ItemName = val });
                        cur.Entries.Add(new EquipEntry { Slot = EquipmentSlot.右戒指, ItemName = val });
                        continue;
                    }

                    if (SlotKeys.TryGetValue(key, out EquipmentSlot slot))
                        cur.Entries.Add(new EquipEntry { Slot = slot, ItemName = val });
                }
            }
            catch (Exception ex)
            {
                MessageQueue.Instance.Enqueue($"[假人] 读取装备方案失败：{ex.Message}");
                EquipSets.Clear();
                return;
            }

            if (EquipSets.Count > 0)
                MessageQueue.Instance.Enqueue($"[假人] 已加载装备方案 {EquipSets.Count} 套（假人上线时随机抽取）");
        }

        private static MirClass? ParseClass(string val)
        {
            switch (val.Trim())
            {
                case "战士": case "Warrior": return MirClass.战士;
                case "法师": case "Wizard": return MirClass.法师;
                case "道士": case "Taoist": return MirClass.道士;
                case "刺客": case "Assassin": return MirClass.刺客;
                case "弓箭": case "弓手": case "Archer": return MirClass.弓箭;
                default: return null;
            }
        }

        /// <summary>
        /// 按方案给假人配等级 + 装备。只从「匹配本职业」的方案里抽；
        /// 方案里没有的部位 / 物品名写错 / 穿不上的，交给 EquipBasicGear 自动兜底。
        /// </summary>
        private static void ApplyEquipSet(FakePlayerObject bot)
        {
            if (EquipSets.Count == 0) return;

            List<BotEquipSet> pool = new List<BotEquipSet>();

            for (int i = 0; i < EquipSets.Count; i++)
                if (EquipSets[i].ClassFilter == null || EquipSets[i].ClassFilter == bot.Class)
                    pool.Add(EquipSets[i]);

            if (pool.Count == 0) return;

            BotEquipSet set = pool[Envir.Random.Next(pool.Count)];

            // 等级：方案里写了就听方案的（必须在配装备之前定，装备按等级需求校验）
            if (set.LevelMax > 0)
            {
                int lvl = set.LevelMax > set.LevelMin
                    ? Envir.Random.Next(set.LevelMin, set.LevelMax + 1)
                    : set.LevelMin;

                bot.Level = (ushort)Math.Max(1, lvl);
            }

            int classFlag = ClassFlag(bot.Class);

            for (int i = 0; i < set.Entries.Count; i++)
            {
                EquipEntry e = set.Entries[i];

                if (bot.Info.Equipment[(int)e.Slot] != null) continue;

                ItemInfo info = ResolveItem(e.ItemName, bot);

                if (info == null)
                {
                    if (MissingEquipNames.Add(e.ItemName))
                        MessageQueue.Instance.Enqueue($"[假人] 装备方案里的物品在物品库中找不到：{e.ItemName}（该部位自动补装）");
                    continue;
                }

                // 方案与职业不符（写错方案）→ 跳过，让自动配装兜底
                if (((byte)info.RequiredClass & classFlag) == 0) continue;

                EquipNamed(bot, e.Slot, info);
            }
        }

        private static ItemInfo ResolveItem(string name, FakePlayerObject bot)
        {
            ItemInfo info = Envir.GetItemInfo(name);
            if (info == null) return null;

            // 盔甲分男女：方案里写的性别跟假人对不上时，自动换后缀再找
            if (!GenderOk(info, bot.Gender) && info.Type == ItemType.盔甲)
            {
                string other = name.Contains("(男)") ? name.Replace("(男)", "(女)")
                             : name.Contains("(女)") ? name.Replace("(女)", "(男)") : null;

                if (other != null)
                {
                    ItemInfo alt = Envir.GetItemInfo(other);
                    if (alt != null && GenderOk(alt, bot.Gender)) return alt;
                }

                return null;
            }

            return info;
        }

        private static void EquipNamed(FakePlayerObject bot, EquipmentSlot slot, ItemInfo info)
        {
            try
            {
                UserItem item = Envir.CreateFreshItem(info);

                if (item.Info.Durability > 0 && item.CurrentDura == 0) return;
                if (!bot.CanEquipItem(item, (int)slot)) return;

                bot.Info.Equipment[(int)slot] = item;
            }
            catch
            {
                // 单件装备失败不影响其他部位
            }
        }

        /// <summary>默认模板：物品名都取自本服物品库（30~45 级档），直接改名字就能用。</summary>
        private static readonly string DefaultEquipFile =
@"# ==================================================================================
#  假人装备方案（FakePlayerEquip.txt）
#
#  用法
#   - 每个中括号段（例如 [战士重装]）就是一套方案；假人上线时从
#     「匹配自己职业」的方案里随机抽一套，下线再上线就换一套。
#   - 等级=最小-最大：假人等级在区间内随机；不写就用 Setup.ini 的 LevelMin/LevelMax。
#   - 职业=战士 / 法师 / 道士 / 刺客 / 弓箭：可选；不写则所有职业都可能抽到这套。
#   - 部位名支持：武器 盔甲(或 上衣) 头盔 项链 手镯(自动穿双手) 戒指(自动穿双只)
#                左手镯 右手镯 左戒指 右戒指 腰带 靴子 守护石 护身符 照明物
#   - 物品名必须与物品库完全一致（含括号和数字后缀）；找不到的部位自动跳过，
#     由系统自动补装一件等级相称的装备。
#   - 盔甲分男女：写 (男) 就行，遇到女性假人系统会自动换成 (女)。
#   - 假人身上会校验职业 / 性别 / 等级需求，穿不上的自动换兜底装备。
#   - 行首 # 或 ; 是注释。改完重启服务端生效。
# ==================================================================================

[战士重装]
职业=战士
等级=40-45
武器=龙纹剑2
盔甲=黑龙战甲(男)60
头盔=钢铁头盔
项链=赤兰项链
手镯=魔鬼手镯
戒指=虎威戒指
腰带=黄金腰带
靴子=赤鳞靴
守护石=木青石(特)

[战士精锐]
职业=战士
等级=42-45
武器=龙血剑
盔甲=黑龙战甲(男)70
头盔=血龙头盔
项链=镇魂项链
手镯=黑狐套袖
戒指=黑狐戒指
腰带=金刚腰带
靴子=赤鳞靴
守护石=绿魔石(特)

[法师长袍]
职业=法师
等级=42-45
武器=龙纹剑2
盔甲=黑龙术甲(男)60
头盔=贤人帽
项链=镇魂项链
手镯=魂锁轮
戒指=魔灵指环
腰带=金刚腰带
靴子=赤鳞靴
守护石=绿魔石(特)

[道士道袍]
职业=道士
等级=40-45
武器=龙纹剑2
盔甲=黑龙道甲(男)60
头盔=贤人帽
项链=黑狐项链
手镯=雪玉手镯
戒指=鬼刃环
腰带=金刚腰带
靴子=赤鳞靴
守护石=木青石(特)

[刺客夜行]
职业=刺客
等级=40-45
武器=片血刀
盔甲=黑龙刺甲(男)60
头盔=英雄头盔
项链=赤兰项链
手镯=魂锁轮
戒指=鬼刃环
腰带=金刚腰带
靴子=赤鳞靴
守护石=晴岚石(特)

[弓箭游猎]
职业=弓箭
等级=42-45
武器=黄龙弓
盔甲=黑龙弓甲(男)60
头盔=贤人帽
项链=青狐项链
手镯=青狐套袖
戒指=风来戒指
腰带=金刚腰带
靴子=赤鳞靴
守护石=绿魔石(特)
";

        // ==================================================================================
        //  台词库
        // ==================================================================================
        private static void LoadChatLines()
        {
            ChatLines.Clear();

            string path = Settings.FakePlayerChatFile;

            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    if (!Path.IsPathRooted(path))
                        path = Path.Combine(Settings.ConfigPath, path);

                    if (File.Exists(path))
                    {
                        string[] lines = File.ReadAllLines(path);

                        for (int i = 0; i < lines.Length; i++)
                        {
                            string line = lines[i].Trim();

                            if (line.Length == 0) continue;
                            if (line.StartsWith("#") || line.StartsWith(";")) continue;
                            if (line.StartsWith("//")) continue;

                            ChatLines.Add(line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageQueue.Instance.Enqueue($"[假人] 台词文件读取失败：{ex.Message}");
                }
            }

            if (ChatLines.Count == 0) ChatLines.AddRange(DefaultChatLines);

            MessageQueue.Instance.Enqueue($"[假人] 台词库 {ChatLines.Count} 条");
        }

        private static readonly string[] DefaultChatLines =
        {
            "有人组队打宝吗？",
            "哪里的怪经验高啊",
            "刚打到一件不错的装备，运气不错",
            "有老玩家带带新人吗",
            "这怪也太难打了",
            "谁有多的血药给我几瓶",
            "这地图怎么走啊，找半天了",
            "刚刚差点被挂掉，吓死我了",
            "现在几点啦，玩的时候都不看时间",
            "谁在打祖玛？带我一个",
            "我装备又掉了，心疼",
            "这爆率也太低了吧",
            "有人卖药吗，价格好说",
            "刚升了一级，终于",
            "深夜还有人在吗",
            "第一次来这个地图，有点怕",
            "大家一起冲级啊",
            "这游戏还是人多好玩",
            "刚才在城里看见个全身神装的大佬",
            "谁教我一下这个技能怎么用",
            "打了半天一件装备都没掉",
            "这地方安全吗，会不会有boss",
            "有人一起吗，我一个人打不过",
            "累了一天，上来放松一下",
            "刚回来，看看还有谁在线",
        };

        internal static string RandomChatLine()
        {
            if (ChatLines.Count == 0) return null;
            return ChatLines[Envir.Random.Next(ChatLines.Count)];
        }

        // ==================================================================================
        //  GM 接口（@假人）
        // ==================================================================================
        public static string StatusReport()
        {
            if (!Settings.FakePlayerEnabled) return "假人系统：配置文件里已关闭（[FakePlayer] Enabled=False）";

            if (Records.Count == 0) return "假人系统：已开启，但当前没有生成任何假人（检查练级地图配置）";

            int roam = 0, combat = 0, idle = 0, dead = 0, restock = 0, loot = 0, follow = 0, grouped = 0;

            for (int i = 0; i < Records.Count; i++)
            {
                BotRecord rec = Records[i];
                if (!rec.Online || rec.Bot?.Brain == null) continue;

                if (rec.Bot.GroupMembers != null) grouped++;

                switch (rec.Bot.Brain.State)
                {
                    case FakeBotState.Roam: roam++; break;
                    case FakeBotState.Combat: combat++; break;
                    case FakeBotState.Idle: idle++; break;
                    case FakeBotState.Dead: dead++; break;
                    case FakeBotState.Restock: restock++; break;
                    case FakeBotState.Loot: loot++; break;
                    case FakeBotState.Follow: follow++; break;
                }
            }

            return $"假人系统：总数 {Records.Count}，在线 {OnlineCount}（巡游 {roam}，战斗 {combat}，跟随 {follow}，发呆 {idle}，捡物 {loot}，补给 {restock}，躺尸 {dead}），"
                 + $"组队中 {grouped}，活动图 {HuntMaps.Count} 张（登录点 {SpawnPoints.Count} 个/{SpawnMaps.Count} 张图），装备方案 {EquipSets.Count} 套，台词 {ChatLines.Count} 条，"
                 + $"互殴 {(Settings.FakePlayerPK ? "开" : "关")}，跟随 {(Settings.FakePlayerFollow ? "开" : "关")}";
        }

        /// <summary>GM 命令：@假人 登录点 —— 列出当前生效的登录点（确认配置文件有没有读进去）</summary>
        public static string SpawnReport()
        {
            if (SpawnPoints.Count == 0)
                return "当前没有配置登录点，假人是在活动地图上随机找位置登录的。\n"
                     + "想固定登录位置：编辑 Configs/FakePlayerSpawns.txt（每行写「地图 X Y」），保存后敲 @假人 reset 生效。";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            sb.Append($"已加载登录点 {SpawnPoints.Count} 个，分布在 {SpawnMaps.Count} 张地图上：");

            int show = Math.Min(SpawnPoints.Count, 24);

            for (int i = 0; i < show; i++)
            {
                BotSpawn s = SpawnPoints[i];
                Map m = Envir.GetMap(s.MapIndex);

                string title = m?.Info == null
                    ? ("#" + s.MapIndex)
                    : (string.IsNullOrWhiteSpace(m.Info.Title) ? m.Info.FileName : m.Info.Title);

                sb.Append($"\n  {i + 1}. {title} [{m?.Info?.FileName}] {s.Location.X},{s.Location.Y}");
            }

            if (SpawnPoints.Count > show) sb.Append($"\n  …还有 {SpawnPoints.Count - show} 个");

            sb.Append($"\n每次上线会从这些点里随机挑一个登录（当前在线假人落在哪张图，看 @假人 状态）。");

            return sb.ToString();
        }

        /// <summary>GM 命令：@假人 药 —— 抽样看在线假人的血量与背包药水（验证「会捡药 / 会吃药」）</summary>
        public static string PotionReport()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("[假人药品] 抽样：");

            int shown = 0;

            for (int i = 0; i < Records.Count && shown < 6; i++)
            {
                BotRecord rec = Records[i];

                if (!rec.Online || rec.Bot == null || rec.Bot.Node == null || rec.Bot.Info == null) continue;

                FakePlayerObject bot = rec.Bot;

                int hp = 0, mp = 0;

                for (int j = 0; j < bot.Info.Inventory.Length; j++)
                {
                    UserItem it = bot.Info.Inventory[j];
                    if (it == null || it.Info == null || it.Info.Type != ItemType.药水) continue;

                    if (it.GetTotal(Stat.HP) > 0 || it.GetTotal(Stat.生命值数率) > 0) hp += it.Count;
                    if (it.GetTotal(Stat.MP) > 0 || it.GetTotal(Stat.法力值数率) > 0) mp += it.Count;
                }

                int maxHP = bot.Stats[Stat.HP];

                sb.Append($"\n  {bot.Name} {bot.Info.Class} Lv{bot.Level} 血量 {(maxHP > 0 ? bot.HP * 100 / maxHP : 0)}% 血药 {hp} 瓶 蓝药 {mp} 瓶");

                shown++;
            }

            if (shown == 0) return "[假人药品] 当前没有在线假人";

            return sb.ToString();
        }

        /// <summary>GM 命令：@假人 reset —— 全部踢掉重新生成</summary>
        public static string RespawnAll()
        {
            if (!Settings.FakePlayerEnabled) return "假人系统在配置里是关闭状态，无法重生成";

            Init();

            return StatusReport();
        }

        /// <summary>
        /// GM 命令：@假人 性能 —— 看假人到底吃掉了多少服务端资源。
        /// 用来一句话回答「客户端卡顿是不是假人多了造成的」：
        ///   假人每秒只花几毫秒 → 跟假人无关；主循环周期上了三位数 → 是服务端自己在卡。
        /// 数据是最近 1 秒的平均值（每秒由 FakePlayerManager.Process 采样一次）。
        /// </summary>
        public static string PerfReport()
        {
            if (!Settings.FakePlayerEnabled) return "假人系统：配置文件里已关闭（Enabled=False），没什么可测的";

            Envir envir = Envir;

            double botCpu = FakePlayerPerf.ProcessUsPerSec / 10000.0;   // 微秒/秒 → 占 1 秒的百分比

            string peak = FakePlayerPerf.CycleDelayPeak <= 0 ? "无" : FakePlayerPerf.CycleDelayPeak + "ms";

            return $"[假人性能] 主循环周期：当前 {FakePlayerPerf.CycleDelay}ms，峰值 {peak}；"
                 + $"世界对象 {envir.Objects.Count} 个（在线玩家 {envir.Players.Count}，其中假人 {OnlineCount}）；"
                 + $"假人 Process：每秒 {FakePlayerPerf.ProcessCallsPerSec} 次，合计 {FakePlayerPerf.ProcessUsPerSec / 1000.0:0.0}ms（约 {botCpu:0.00}% CPU）；"
                 + $"大脑 Tick：每秒 {FakePlayerPerf.BrainCallsPerSec} 次，合计 {FakePlayerPerf.BrainUsPerSec / 1000.0:0.0}ms；"
                 + $"因收件人是假人而跳过的广播：每秒 {FakePlayerPerf.BroadcastSkippedPerSec} 次";
        }

        /// <summary>GM 命令：@假人 性能 重置 —— 把主循环周期峰值清零，方便「关假人 / 开假人」各测一轮做对比</summary>
        public static string ResetPerfPeak()
        {
            FakePlayerPerf.ResetPeak();

            return "假人性能：主循环周期峰值已清零，让服务器跑一会儿再 @假人 性能 看新峰值";
        }

        /// <summary>GM 命令：@假人 喊话 —— 让一个假人立刻喊句话（测试聊天通路）</summary>
        public static string ForceChat(bool shout)
        {
            for (int i = 0; i < Records.Count; i++)
            {
                BotRecord rec = Records[i];
                if (!rec.Online || rec.Bot == null) continue;

                string line = RandomChatLine();
                if (string.IsNullOrWhiteSpace(line)) return "台词库是空的";

                rec.Bot.Chat(shout ? "!" + line : line);
                return $"{rec.Name}{(shout ? " 喊话" : " 说话")}：{line}";
            }

            return "当前没有在线假人";
        }
    }

    /// <summary>
    /// 假人性能探针 —— 专门用来回答一个问题：
    /// 「客户端跑起来一顿一顿的，到底是不是假人多了拖的？」
    ///
    /// 采集三样东西（只在服务端主循环线程里读写，不加锁；每次调用只是几次 long 加法，
    /// 换算成毫秒只在每秒一次的快照里做，所以对主循环的影响可以忽略）：
    ///   1. 假人 Process() 的调用次数与总耗时 —— 也就是「50 个假人冒充 50 个玩家」占掉的主循环时间；
    ///   2. 假人 AI 大脑 Tick() 的总耗时；
    ///   3. 主循环周期（Envir.LastRunTime，服务端界面上那个「延迟周期」）的当前值与峰值；
    ///   4. 因为收件人是假人而被跳过的广播次数（这些广播本来会被 BotConnection 白丢）。
    ///
    /// 用法：GM 账号在游戏里输入 `@假人 性能`。
    /// </summary>
    public static class FakePlayerPerf
    {
        private static long _processCalls, _processTicks;
        private static long _brainCalls, _brainTicks;
        private static long _broadcastSkipped;

        // ---- 每秒刷新的快照（供 @假人 性能 读取） ----
        public static long ProcessCallsPerSec;
        public static long ProcessUsPerSec;
        public static long BrainCallsPerSec;
        public static long BrainUsPerSec;
        public static long BroadcastSkippedPerSec;

        public static long CycleDelay;        // 最近一次主循环周期（ms）
        public static long CycleDelayPeak;    // 观测到的周期峰值（ms）
        public static long PeakSince;         // 峰值是从什么时候开始统计的

        public static void NoteBotProcess(long elapsedTicks)
        {
            _processCalls++;
            _processTicks += elapsedTicks;
        }

        public static void NoteBrain(long elapsedTicks)
        {
            if (elapsedTicks <= 0) return;

            _brainCalls++;
            _brainTicks += elapsedTicks;
        }

        public static void NoteBroadcastSkipped()
        {
            _broadcastSkipped++;
        }

        /// <summary>每秒调用一次（挂在 FakePlayerManager.Process 上），把累计值换算成「每秒」快照</summary>
        public static void Sample(long cycleDelay)
        {
            ProcessCallsPerSec = _processCalls;
            ProcessUsPerSec = ToUs(_processTicks);
            BrainCallsPerSec = _brainCalls;
            BrainUsPerSec = ToUs(_brainTicks);
            BroadcastSkippedPerSec = _broadcastSkipped;

            _processCalls = _processTicks = 0;
            _brainCalls = _brainTicks = 0;
            _broadcastSkipped = 0;

            CycleDelay = cycleDelay;

            if (cycleDelay > CycleDelayPeak || CycleDelayPeak == 0)
            {
                CycleDelayPeak = cycleDelay;
                PeakSince = Envir.Main.Time;
            }
        }

        /// <summary>把峰值统计清零，方便「关掉假人再跑一次对比」</summary>
        public static void ResetPeak()
        {
            CycleDelayPeak = CycleDelay;
            PeakSince = Envir.Main.Time;
        }

        private static long ToUs(long ticks)
        {
            if (ticks <= 0) return 0;

            return ticks * 1000000L / System.Diagnostics.Stopwatch.Frequency;
        }
    }
}
