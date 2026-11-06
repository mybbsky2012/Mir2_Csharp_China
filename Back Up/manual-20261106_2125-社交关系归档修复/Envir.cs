using ClientPackets;
using Server.Library.Utils;
using Server.MirDatabase;
using Server.MirNetwork;
using Server.MirObjects;
using Server.MirObjects.Monsters;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.RegularExpressions;
using S = ServerPackets;

namespace Server.MirEnvir
{
    public class MobThread
    {
        public int Id = 0;
        public long LastRunTime = 0;
        public long StartTime = 0;
        public long EndTime = 0;
        public LinkedList<MapObject> ObjectsList = new LinkedList<MapObject>();
        public LinkedListNode<MapObject> _current = null;
        public bool Stop = false;
    }

    public class RandomProvider
    {
        private static int seed = Environment.TickCount;
        private static readonly ThreadLocal<Random> RandomWrapper = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));

        public static Random GetThreadRandom() =>
            RandomWrapper.Value;

        public int Next() =>
            RandomWrapper.Value.Next();
        public int Next(int maxValue) =>
            RandomWrapper.Value.Next(maxValue);
        public int Next(int minValue, int maxValue) =>
            RandomWrapper.Value.Next(minValue, maxValue);
    }

    public class Envir
    {
        public static Envir Main { get; } = new Envir();

        public static Envir Edit { get; } = new Envir();

        protected static MessageQueue MessageQueue => MessageQueue.Instance;

        public static object AccountLock = new object();
        public static object LoadLock = new object();

        public const int MinVersion = 60;
        public const int Version = 111;   // 111：英雄存档加 Buffs 段（HeroInfo.Save/Load，见 version > 110）
        public const int CustomVersion = 0;
        public static readonly string DatabasePath = Path.Combine(".", "Server.MirDB");
        public static readonly string AccountPath = Path.Combine(".", "Server.MirADB");
        public static readonly string BackUpPath = Path.Combine(".", "Back Up");
        public static readonly string AccountsBackUpPath = Path.Combine(".", "Back Up", "Accounts");
        public static readonly string ArchivePath = Path.Combine(".", "Archive");
        public bool ResetGS = false;
        public bool GuildRefreshNeeded;

        private static readonly Regex AccountIDReg, PasswordReg, EMailReg, CharacterReg;

        public static int LoadVersion;
        public static int LoadCustomVersion;

        private readonly DateTime _startTime = DateTime.UtcNow;
        public readonly Stopwatch Stopwatch = Stopwatch.StartNew();

        public long Time { get; private set; }
        public RespawnTimer RespawnTick = new RespawnTimer();

        private static List<string> DisabledCharNames = new List<string>();
        private static List<string> LineMessages = new List<string>();

        public static ConcurrentDictionary<string, DateTime> IPBlocks = new ConcurrentDictionary<string, DateTime>();

        public static ConcurrentDictionary<string, MirConnectionLog> ConnectionLogs = new ConcurrentDictionary<string, MirConnectionLog>();

        public DateTime Now =>
            _startTime.AddMilliseconds(Time);

        public bool Running { get; private set; }


        private static uint _objectID;

        /// <summary>
        /// 离线挂机总开关的上一次取值（10-26）：只在「开 → 关」那一刻扫一次在线挂机角色，
        /// 平时每帧只做一次 bool 比较，没有额外开销。
        /// </summary>
        private bool _offlineAfkWasEnabled = true;
        public uint ObjectID => ++_objectID;

        public static int _playerCount;
        public int PlayerCount => Players.Count;

        public int[] OnlineRankingCount = new int[6];
        public int HeroCount => Heroes.Count;

        public RandomProvider Random = new RandomProvider();

        private Thread _thread;
        private TcpListener _listener;
        private bool StatusPortEnabled = true;
        public List<MirStatusConnection> StatusConnections = new List<MirStatusConnection>();
        private TcpListener _StatusPort;
        private int _sessionID;
        public List<MirConnection> Connections = new List<MirConnection>();

        // 非工作线程（编辑器窗体/菜单按钮）想改运行服数据时，统一排队到工作线程执行，
        // 避免 MapList / Cells / NPCs 等运行时集合被并发修改（地图重载会新增 Map 对象，尤其危险）。
        private readonly List<Action> _mainThreadQueue = new List<Action>();
        private readonly object _mainThreadQueueLock = new object();
        private volatile bool _hasMainThreadWork;
        private bool _executingMainThreadTask;

        //Server DB
        public int MapIndex, ItemIndex, MonsterIndex, NPCIndex, QuestIndex, GameshopIndex, ConquestIndex, RespawnIndex, ScriptIndex;
        public List<MapInfo> MapInfoList = new List<MapInfo>();
        public List<ItemInfo> ItemInfoList = new List<ItemInfo>();
        public List<MonsterInfo> MonsterInfoList = new List<MonsterInfo>();
        public List<MagicInfo> MagicInfoList = new List<MagicInfo>();
        public List<NPCInfo> NPCInfoList = new List<NPCInfo>();
        public DragonInfo DragonInfo = new DragonInfo();
        public List<QuestInfo> QuestInfoList = new List<QuestInfo>();
        public List<GameShopItem> GameShopList = new List<GameShopItem>();
        public List<RecipeInfo> RecipeInfoList = new List<RecipeInfo>();
        public List<BuffInfo> BuffInfoList = new List<BuffInfo>();
        public List<ConquestInfo> ConquestInfoList = new List<ConquestInfo>();

        //User DB
        public int NextAccountID, NextCharacterID, NextGuildID, NextHeroID;
        public ulong NextUserItemID, NextAuctionID, NextMailID, NextRecipeID;
        public List<AccountInfo> AccountList = new List<AccountInfo>();
        public List<CharacterInfo> CharacterList = new List<CharacterInfo>();
        public List<GuildInfo> GuildList = new List<GuildInfo>();
        public LinkedList<AuctionInfo> Auctions = new LinkedList<AuctionInfo>();
        public List<ConquestGuildInfo> ConquestList = new List<ConquestGuildInfo>();
        public Dictionary<int, int> GameshopLog = new Dictionary<int, int>();
        public List<HeroInfo> HeroList = new List<HeroInfo>();

        public int GuildCount; //This shouldn't be needed?? -> remove in the future

        //Live Info
        public bool Saving = false;

        /// <summary>
        /// HTTP 管理接口请求「立刻存档」（10-30）：HTTP 在自己的线程上跑，
        /// 直接调 SaveAccounts 会和游戏线程的定时存档抢同一个文件 → 只置标志，
        /// 由工作线程下一拍统一消费。
        /// </summary>
        public volatile bool HttpSaveRequested = false;

        /// <summary>
        /// HTTP 管理接口投递的踢人请求（10-30）：socket 收发 / 断链统一回工作线程做，
        /// 避免 HTTP 线程和游戏线程同时往一条连接写包。
        /// </summary>
        private readonly List<MirConnection> _httpKickQueue = new List<MirConnection>();

        public void QueueHttpKick(MirConnection connection)
        {
            if (connection == null) return;

            lock (_httpKickQueue)
                _httpKickQueue.Add(connection);
        }

        /// <summary>工作线程每拍消费一次 HTTP 管理接口的写请求（存档 + 踢人）。</summary>
        private void ProcessHttpRequests()
        {
            // 先踢人再存档：踢下线会让角色 StopGame，先踢后存才能把下线后的状态一起落盘
            MirConnection[] kicks;

            lock (_httpKickQueue)
            {
                if (_httpKickQueue.Count == 0) 
                {
                    kicks = null;
                }
                else
                {
                    kicks = _httpKickQueue.ToArray();
                    _httpKickQueue.Clear();
                }
            }

            if (kicks != null)
            {
                foreach (var connection in kicks)
                {
                    try
                    {
                        connection.SendDisconnect(3);
                    }
                    catch (Exception ex)
                    {
                        LogInnerError("HTTP踢人失败", ex);
                    }
                }
            }

            if (HttpSaveRequested && !Saving)
            {
                HttpSaveRequested = false;
                BeginSaveAccounts();
            }
        }
        public List<Map> MapList = new List<Map>();
        public List<SafeZoneInfo> StartPoints = new List<SafeZoneInfo>();
        public List<ItemInfo> StartItems = new List<ItemInfo>();

        public List<PlayerObject> Players = new List<PlayerObject>();
        public List<SpellObject> Spells = new List<SpellObject>();
        public List<NPCObject> NPCs = new List<NPCObject>();
        public List<GuildObject> Guilds = new List<GuildObject>();
        public List<ConquestObject> Conquests = new List<ConquestObject>();
        public List<HeroObject> Heroes = new List<HeroObject>();

        public LightSetting Lights;
        public LinkedList<MapObject> Objects = new LinkedList<MapObject>();
        public Dictionary<int, NPCScript> Scripts = new Dictionary<int, NPCScript>();
        public Dictionary<string, Timer> Timers = new Dictionary<string, Timer>();

        //multithread vars
        readonly object _locker = new object();
        public MobThread[] MobThreads = new MobThread[Settings.ThreadLimit];
        private readonly Thread[] MobThreading = new Thread[Settings.ThreadLimit];
        public int SpawnMultiplier = 1;//set this to 2 if you want double spawns (warning this can easily lag your server far beyond what you imagine)

        public List<string> CustomCommands = new List<string>();

        public Dragon DragonSystem;
        public NPCScript DefaultNPC, MonsterNPC, RobotNPC;

        public List<DropInfo> FishingDrops = new List<DropInfo>();
        public List<DropInfo> AwakeningDrops = new List<DropInfo>();

        public List<DropInfo> StrongboxDrops = new List<DropInfo>();
        public List<DropInfo> BlackstoneDrops = new List<DropInfo>();

        public List<GuildAtWar> GuildsAtWar = new List<GuildAtWar>();
        public List<MapRespawn> SavedSpawns = new List<MapRespawn>();

        public List<RankCharacterInfo> RankTop = new List<RankCharacterInfo>();
        public List<RankCharacterInfo>[] RankClass = new List<RankCharacterInfo>[5];

        static HttpServer http;

        static Envir()
        {
            AccountIDReg = new Regex(@"^[A-Za-z0-9]{" + Globals.MinAccountIDLength + "," + Globals.MaxAccountIDLength + "}$");
            PasswordReg = new Regex(@"^[A-Za-z0-9]{" + Globals.MinPasswordLength + "," + Globals.MaxPasswordLength + "}$");
            EMailReg = new Regex(@"\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*");
            CharacterReg = new Regex(@"^[A-Za-z0-9\u4E00-\u9FA5]{" + Globals.MinCharacterNameLength + "," + Globals.MaxCharacterNameLength + "}$");
        }

        public static int LastCount = 0, LastRealCount = 0;
        public static long LastRunTime = 0;
        public int MonsterCount;

        private long warTime, guildTime, conquestTime, rentalItemsTime, auctionTime, spawnTime, robotTime, timerTime;
        private int dailyTime = DateTime.UtcNow.Day;

        private bool MagicExists(Spell spell)
        {
            for (var i = 0; i < MagicInfoList.Count; i++)
            {
                if (MagicInfoList[i].Spell == spell) return true;
            }
            return false;
        }

        private void UpdateMagicInfo()
        {
            for (var i = 0; i < MagicInfoList.Count; i++)
            {
                switch (MagicInfoList[i].Spell)
                {
                    //warrior
                    case Spell.Thrusting:
                        MagicInfoList[i].MultiplierBase = 0.25f;
                        MagicInfoList[i].MultiplierBonus = 0.25f;
                        break;
                    case Spell.HalfMoon:
                        MagicInfoList[i].MultiplierBase = 0.3f;
                        MagicInfoList[i].MultiplierBonus = 0.1f;
                        break;
                    case Spell.ShoulderDash:
                        MagicInfoList[i].MPowerBase = 4;
                        break;
                    case Spell.TwinDrakeBlade:
                        MagicInfoList[i].MultiplierBase = 0.8f;
                        MagicInfoList[i].MultiplierBonus = 0.1f;
                        break;
                    case Spell.FlamingSword:
                        MagicInfoList[i].MultiplierBase = 1.4f;
                        MagicInfoList[i].MultiplierBonus = 0.4f;
                        break;
                    case Spell.CrossHalfMoon:
                        MagicInfoList[i].MultiplierBase = 0.4f;
                        MagicInfoList[i].MultiplierBonus = 0.1f;
                        break;
                    case Spell.BladeAvalanche:
                        MagicInfoList[i].MultiplierBase = 1f;
                        MagicInfoList[i].MultiplierBonus = 0.4f;
                        break;
                    case Spell.SlashingBurst:
                        MagicInfoList[i].MultiplierBase = 3.25f;
                        MagicInfoList[i].MultiplierBonus = 0.25f;
                        break;
                    case Spell.DimensionalSword:
                        MagicInfoList[i].MultiplierBase = 1f;
                        MagicInfoList[i].MultiplierBonus = 0.25f;
                        break;
                    case Spell.DimensionalSwordRare:
                        MagicInfoList[i].MultiplierBase = 1f;
                        MagicInfoList[i].MultiplierBonus = 0.25f;
                        break;
                    //wiz
                    case Spell.Repulsion:
                        MagicInfoList[i].MPowerBase = 4;
                        break;
                    //tao
                    case Spell.Poisoning:
                        MagicInfoList[i].MPowerBase = 0;
                        break;
                    case Spell.Curse:
                        MagicInfoList[i].MPowerBase = 20;
                        break;
                    case Spell.Plague:
                        MagicInfoList[i].MPowerBase = 0;
                        MagicInfoList[i].PowerBase = 0;
                        break;
                    //sin
                    case Spell.FatalSword:
                        MagicInfoList[i].MPowerBase = 20;
                        break;
                    case Spell.DoubleSlash:
                        MagicInfoList[i].MultiplierBase = 0.8f;
                        MagicInfoList[i].MultiplierBonus = 0.1f;
                        break;
                    case Spell.FireBurst:
                        MagicInfoList[i].MPowerBase = 4;
                        break;
                    case Spell.MoonLight:
                        MagicInfoList[i].MPowerBase = 20;
                        break;
                    case Spell.DarkBody:
                        MagicInfoList[i].MPowerBase = 20;
                        break;
                    case Spell.Hemorrhage:
                        MagicInfoList[i].MultiplierBase = 0.2f;
                        MagicInfoList[i].MultiplierBonus = 0.05f;
                        break;
                    case Spell.CrescentSlash:
                        MagicInfoList[i].MultiplierBase = 1f;
                        MagicInfoList[i].MultiplierBonus = 0.4f;
                        break;
                }
            }
        }

        private void FillMagicInfoList()
        {
            //Warrior
            if (!MagicExists(Spell.Fencing)) MagicInfoList.Add(new MagicInfo {Name = "基本剑术", Spell = Spell.Fencing, Icon = 2, Level1 = 7, Level2 = 9, Level3 = 12, Need1 = 270, Need2 = 600, Need3 = 1300, Range = 0 });
            if (!MagicExists(Spell.Slaying)) MagicInfoList.Add(new MagicInfo { Name = "攻杀剑术", Spell = Spell.Slaying, Icon = 6, Level1 = 15, Level2 = 17, Level3 = 20, Need1 = 500, Need2 = 1100, Need3 = 1800, Range = 0 });
            if (!MagicExists(Spell.Thrusting)) MagicInfoList.Add(new MagicInfo { Name = "刺杀剑术", Spell = Spell.Thrusting, Icon = 11, Level1 = 22, Level2 = 24, Level3 = 27, Need1 = 2000, Need2 = 3500, Need3 = 6000, Range = 0, MultiplierBase = 0.25f, MultiplierBonus = 0.25f });
            if (!MagicExists(Spell.HalfMoon)) MagicInfoList.Add(new MagicInfo { Name = "半月弯刀", Spell = Spell.HalfMoon, Icon = 24, Level1 = 26, Level2 = 28, Level3 = 31, Need1 = 5000, Need2 = 8000, Need3 = 14000, BaseCost = 3, Range = 0, MultiplierBase =0.3f, MultiplierBonus = 0.1f });
            if (!MagicExists(Spell.ShoulderDash)) MagicInfoList.Add(new MagicInfo { Name = "野蛮冲撞", Spell = Spell.ShoulderDash, Icon = 26, Level1 = 30, Level2 = 32, Level3 = 34, Need1 = 3000, Need2 = 4000, Need3 = 6000, BaseCost = 4, LevelCost = 4, DelayBase = 2500, Range = 0 , MPowerBase = 4});
            if (!MagicExists(Spell.TwinDrakeBlade)) MagicInfoList.Add(new MagicInfo { Name = "双龙斩", Spell = Spell.TwinDrakeBlade, Icon = 37, Level1 = 32, Level2 = 34, Level3 = 37, Need1 = 4000, Need2 = 6000, Need3 = 10000, BaseCost = 10, Range = 0 , MultiplierBase = 0.8f, MultiplierBonus = 0.1f});
            if (!MagicExists(Spell.Entrapment)) MagicInfoList.Add(new MagicInfo { Name = "捕绳剑", Spell = Spell.Entrapment, Icon = 46, Level1 = 32, Level2 = 35, Level3 = 37, Need1 = 2000, Need2 = 3500, Need3 = 5500, BaseCost = 15, LevelCost = 3, Range = 9 });
            if (!MagicExists(Spell.FlamingSword)) MagicInfoList.Add(new MagicInfo { Name = "烈火剑法", Spell = Spell.FlamingSword, Icon = 25, Level1 = 35, Level2 = 37, Level3 = 40, Need1 = 2000, Need2 = 4000, Need3 = 6000, BaseCost = 7, Range = 0, MultiplierBase = 1.4f, MultiplierBonus = 0.4f});
            if (!MagicExists(Spell.LionRoar)) MagicInfoList.Add(new MagicInfo { Name = "狮子吼", Spell = Spell.LionRoar, Icon = 42, Level1 = 36, Level2 = 39, Level3 = 41, Need1 = 5000, Need2 = 8000, Need3 = 12000, BaseCost = 14, LevelCost = 4, Range = 0 });
            if (!MagicExists(Spell.CrossHalfMoon)) MagicInfoList.Add(new MagicInfo { Name = "狂风斩", Spell = Spell.CrossHalfMoon, Icon = 33, Level1 = 38, Level2 = 40, Level3 = 42, Need1 = 7000, Need2 = 11000, Need3 = 16000, BaseCost = 6, Range = 0, MultiplierBase = 0.4f, MultiplierBonus = 0.1f });
            if (!MagicExists(Spell.BladeAvalanche)) MagicInfoList.Add(new MagicInfo { Name = "空破闪", Spell = Spell.BladeAvalanche, Icon = 43, Level1 = 38, Level2 = 41, Level3 = 43, Need1 = 5000, Need2 = 8000, Need3 = 12000, BaseCost = 14, LevelCost = 4, Range = 0, MultiplierBonus = 0.3f});
            if (!MagicExists(Spell.ProtectionField)) MagicInfoList.Add(new MagicInfo { Name = "护身气幕", Spell = Spell.ProtectionField, Icon = 50, Level1 = 39, Level2 = 42, Level3 = 45, Need1 = 6000, Need2 = 12000, Need3 = 18000, BaseCost = 23, LevelCost = 6, Range = 0 });
            if (!MagicExists(Spell.Rage)) MagicInfoList.Add(new MagicInfo { Name = "剑气爆", Spell = Spell.Rage, Icon = 49, Level1 = 44, Level2 = 47, Level3 = 50, Need1 = 8000, Need2 = 14000, Need3 = 20000, BaseCost = 20, LevelCost = 5, Range = 0 });
            if (!MagicExists(Spell.CounterAttack)) MagicInfoList.Add(new MagicInfo { Name = "天务", Spell = Spell.CounterAttack, Icon = 72, Level1 = 47, Level2 = 51, Level3 = 55, Need1 = 7000, Need2 = 11000, Need3 = 15000, BaseCost = 12, LevelCost = 4, DelayBase = 24000, Range = 0 , MultiplierBonus = 0.4f});
            if (!MagicExists(Spell.SlashingBurst)) MagicInfoList.Add(new MagicInfo { Name = "日闪", Spell = Spell.SlashingBurst, Icon = 55, Level1 = 50, Level2 = 53, Level3 = 56, Need1 = 10000, Need2 = 16000, Need3 = 24000, BaseCost = 25, LevelCost = 4, MPowerBase = 1, PowerBase = 3, DelayBase = 14000, DelayReduction = 4000, Range = 0 , MultiplierBase = 3.25f, MultiplierBonus = 0.25f});
            if (!MagicExists(Spell.Fury)) MagicInfoList.Add(new MagicInfo { Name = "血龙剑法", Spell = Spell.Fury, Icon = 76, Level1 = 45, Level2 = 48, Level3 = 51, Need1 = 8000, Need2 = 14000, Need3 = 20000, BaseCost = 10, LevelCost = 4, DelayBase = 600000, DelayReduction = 120000, Range = 0 });
            if (!MagicExists(Spell.ImmortalSkin)) MagicInfoList.Add(new MagicInfo { Name = "金刚不坏", Spell = Spell.ImmortalSkin, Icon = 80, Level1 = 60, Level2 = 61, Level3 = 62, Need1 = 1560, Need2 = 2200, Need3 = 3000, BaseCost = 10, LevelCost = 4, DelayBase = 600000, DelayReduction = 120000, Range = 0 });
            if (!MagicExists(Spell.EntrapmentRare)) MagicInfoList.Add(new MagicInfo { Name = "捕绳剑-秘籍", Spell = Spell.EntrapmentRare, Icon = 107, Level1 = 55, Level2 = 60, Level3 = 65, Need1 = 10000, Need2 = 13000, Need3 = 16000, BaseCost = 15, LevelCost = 3, Range = 9 });
            if (!MagicExists(Spell.ImmortalSkinRare)) MagicInfoList.Add(new MagicInfo { Name = "金刚不坏-秘籍", Spell = Spell.ImmortalSkinRare, Icon = 84, Level1 = 62, Level2 = 64, Level3 = 66, Need1 = 1000, Need2 = 1560, Need3 = 2200, BaseCost = 10, LevelCost = 4, DelayBase = 600000, DelayReduction = 120000, Range = 0 });
            if (!MagicExists(Spell.LionRoarRare)) MagicInfoList.Add(new MagicInfo { Name = "狮子吼-秘籍", Spell = Spell.LionRoarRare, Icon = 112, Level1 = 95, Level2 = 97, Level3 = 102, Need1 = 8900, Need2 = 15000, Need3 = 21600, BaseCost = 14, LevelCost = 4, Range = 0 });
            if (!MagicExists(Spell.DimensionalSword)) MagicInfoList.Add(new MagicInfo { Name = "时空剑", Spell = Spell.DimensionalSword, Icon = 117, Level1 = 90, Level2 = 92, Level3 = 94, Need1 = 3800, Need2 = 6300, Need3 = 9300, BaseCost = 32, LevelCost = 4, MPowerBase = 1, PowerBase = 3, DelayBase = 14000, DelayReduction = 4000, Range = 2 , MultiplierBase = 1f, MultiplierBonus = 0.25f});
            if (!MagicExists(Spell.DimensionalSwordRare)) MagicInfoList.Add(new MagicInfo { Name = "时空剑-秘籍", Spell = Spell.DimensionalSwordRare, Icon = 122, Level1 = 100, Level2 = 105, Level3 = 110, Need1 = 8800, Need2 = 13000, Need3 = 21600, BaseCost = 32, LevelCost = 4, MPowerBase = 1, PowerBase = 3, DelayBase = 14000, DelayReduction = 4000, Range = 2 , MultiplierBase = 1f, MultiplierBonus = 0.25f});

            //Wizard
            if (!MagicExists(Spell.FireBall)) MagicInfoList.Add(new MagicInfo { Name = "火球术", Spell = Spell.FireBall, Icon = 0, Level1 = 7, Level2 = 9, Level3 = 11, Need1 = 200, Need2 = 350, Need3 = 700, BaseCost = 3, LevelCost = 2, MPowerBase = 8, PowerBase = 2, Range = 9 });
            if (!MagicExists(Spell.Repulsion)) MagicInfoList.Add(new MagicInfo { Name = "抗拒火环", Spell = Spell.Repulsion, Icon = 7, Level1 = 12, Level2 = 15, Level3 = 19, Need1 = 500, Need2 = 1300, Need3 = 2200, BaseCost = 2, LevelCost = 2, Range = 0, MPowerBase = 4});
            if (!MagicExists(Spell.ElectricShock)) MagicInfoList.Add(new MagicInfo { Name = "诱惑之光", Spell = Spell.ElectricShock, Icon = 19, Level1 = 13, Level2 = 18, Level3 = 24, Need1 = 530, Need2 = 1100, Need3 = 2200, BaseCost = 3, LevelCost = 1, Range = 9 });
            if (!MagicExists(Spell.GreatFireBall)) MagicInfoList.Add(new MagicInfo { Name = "大火球", Spell = Spell.GreatFireBall, Icon = 4, Level1 = 15, Level2 = 18, Level3 = 21, Need1 = 2000, Need2 = 2700, Need3 = 3500, BaseCost = 5, LevelCost = 1, MPowerBase = 6, PowerBase = 10, Range = 9 });
            if (!MagicExists(Spell.HellFire)) MagicInfoList.Add(new MagicInfo { Name = "地狱火", Spell = Spell.HellFire, Icon = 8, Level1 = 16, Level2 = 20, Level3 = 24, Need1 = 700, Need2 = 2700, Need3 = 3500, BaseCost = 10, LevelCost = 3, MPowerBase = 14, PowerBase = 6, Range = 0 });
            if (!MagicExists(Spell.ThunderBolt)) MagicInfoList.Add(new MagicInfo { Name = "雷电术", Spell = Spell.ThunderBolt, Icon = 10, Level1 = 17, Level2 = 20, Level3 = 23, Need1 = 500, Need2 = 2000, Need3 = 3500, BaseCost = 9, LevelCost = 2, MPowerBase = 8, MPowerBonus = 20, PowerBase = 9, Range = 9 });
            if (!MagicExists(Spell.Teleport)) MagicInfoList.Add(new MagicInfo { Name = "瞬息移动", Spell = Spell.Teleport, Icon = 20, Level1 = 19, Level2 = 22, Level3 = 25, Need1 = 350, Need2 = 1000, Need3 = 2000, BaseCost = 10, LevelCost = 3, Range = 0 });
            if (!MagicExists(Spell.FireBang)) MagicInfoList.Add(new MagicInfo { Name = "爆裂火焰", Spell = Spell.FireBang, Icon = 22, Level1 = 22, Level2 = 25, Level3 = 28, Need1 = 3000, Need2 = 5000, Need3 = 10000, BaseCost = 14, LevelCost = 4, MPowerBase = 8, PowerBase = 8, Range = 9 });
            if (!MagicExists(Spell.FireWall)) MagicInfoList.Add(new MagicInfo { Name = "火墙", Spell = Spell.FireWall, Icon = 21, Level1 = 24, Level2 = 28, Level3 = 33, Need1 = 4000, Need2 = 10000, Need3 = 20000, BaseCost = 30, LevelCost = 5, MPowerBase = 3, PowerBase = 3, Range = 9 });
            if (!MagicExists(Spell.Lightning)) MagicInfoList.Add(new MagicInfo { Name = "疾光电影", Spell = Spell.Lightning, Icon = 9, Level1 = 26, Level2 = 29, Level3 = 32, Need1 = 3000, Need2 = 6000, Need3 = 12000, BaseCost = 38, LevelCost = 7, MPowerBase = 12, PowerBase = 12, Range = 0 });
            if (!MagicExists(Spell.FrostCrunch)) MagicInfoList.Add(new MagicInfo { Name = "寒冰掌", Spell = Spell.FrostCrunch, Icon = 38, Level1 = 28, Level2 = 30, Level3 = 33, Need1 = 3000, Need2 = 5000, Need3 = 8000, BaseCost = 15, LevelCost = 3, MPowerBase = 12, PowerBase = 12, Range = 9 });
            if (!MagicExists(Spell.ThunderStorm)) MagicInfoList.Add(new MagicInfo { Name = "地狱雷光", Spell = Spell.ThunderStorm, Icon = 23, Level1 = 30, Level2 = 32, Level3 = 34, Need1 = 4000, Need2 = 8000, Need3 = 12000, BaseCost = 29, LevelCost = 9, MPowerBase = 10, MPowerBonus = 20, PowerBase = 10, PowerBonus = 20, Range = 0 });
            if (!MagicExists(Spell.MagicShield)) MagicInfoList.Add(new MagicInfo { Name = "魔法盾", Spell = Spell.MagicShield, Icon = 30, Level1 = 31, Level2 = 34, Level3 = 38, Need1 = 3000, Need2 = 7000, Need3 = 10000, BaseCost = 35, LevelCost = 5, Range = 0 });
            if (!MagicExists(Spell.TurnUndead)) MagicInfoList.Add(new MagicInfo { Name = "圣言术", Spell = Spell.TurnUndead, Icon = 31, Level1 = 32, Level2 = 35, Level3 = 39, Need1 = 3000, Need2 = 7000, Need3 = 10000, BaseCost = 52, LevelCost = 13, Range = 9 });
            if (!MagicExists(Spell.Vampirism)) MagicInfoList.Add(new MagicInfo { Name = "嗜血术", Spell = Spell.Vampirism, Icon = 47, Level1 = 33, Level2 = 36, Level3 = 40, Need1 = 3000, Need2 = 5000, Need3 = 8000, BaseCost = 26, LevelCost = 13, MPowerBase = 12, PowerBase = 12, Range = 9 });
            if (!MagicExists(Spell.IceStorm)) MagicInfoList.Add(new MagicInfo { Name = "冰咆哮", Spell = Spell.IceStorm, Icon = 32, Level1 = 35, Level2 = 37, Level3 = 40, Need1 = 4000, Need2 = 8000, Need3 = 12000, BaseCost = 33, LevelCost = 3, MPowerBase = 12, PowerBase = 14, Range = 9 });
            if (!MagicExists(Spell.FlameDisruptor)) MagicInfoList.Add(new MagicInfo { Name = "灭天火", Spell = Spell.FlameDisruptor, Icon = 34, Level1 = 38, Level2 = 40, Level3 = 42, Need1 = 5000, Need2 = 9000, Need3 = 14000, BaseCost = 28, LevelCost = 3, MPowerBase = 15, MPowerBonus = 20, PowerBase = 9, Range = 9 });
            if (!MagicExists(Spell.Mirroring)) MagicInfoList.Add(new MagicInfo { Name = "分身术", Spell = Spell.Mirroring, Icon = 41, Level1 = 41, Level2 = 43, Level3 = 45, Need1 = 6000, Need2 = 11000, Need3 = 16000, BaseCost = 21, Range = 0 });
            if (!MagicExists(Spell.FlameField)) MagicInfoList.Add(new MagicInfo { Name = "火龙气焰", Spell = Spell.FlameField, Icon = 44, Level1 = 42, Level2 = 43, Level3 = 45, Need1 = 6000, Need2 = 11000, Need3 = 16000, BaseCost = 45, LevelCost = 8, MPowerBase = 100, PowerBase = 25, Range = 9 });
            if (!MagicExists(Spell.Blizzard)) MagicInfoList.Add(new MagicInfo { Name = "天霜冰环", Spell = Spell.Blizzard, Icon = 51, Level1 = 44, Level2 = 47, Level3 = 50, Need1 = 8000, Need2 = 16000, Need3 = 24000, BaseCost = 65, LevelCost = 10, MPowerBase = 30, MPowerBonus = 10, PowerBase = 20, PowerBonus = 5, Range = 9 });
            if (!MagicExists(Spell.MagicBooster)) MagicInfoList.Add(new MagicInfo { Name = "深延术", Spell = Spell.MagicBooster, Icon = 73, Level1 = 47, Level2 = 49, Level3 = 52, Need1 = 12000, Need2 = 18000, Need3 = 24000, BaseCost = 150, LevelCost = 15, Range = 0 });
            if (!MagicExists(Spell.MeteorStrike)) MagicInfoList.Add(new MagicInfo { Name = "天上落焰", Spell = Spell.MeteorStrike, Icon = 52, Level1 = 49, Level2 = 52, Level3 = 55, Need1 = 15000, Need2 = 20000, Need3 = 25000, BaseCost = 115, LevelCost = 17, MPowerBase = 40, MPowerBonus = 10, PowerBase = 20, PowerBonus = 15, Range = 9 });
            if (!MagicExists(Spell.IceThrust)) MagicInfoList.Add(new MagicInfo { Name = "冰焰术", Spell = Spell.IceThrust, Icon = 56, Level1 = 53, Level2 = 56, Level3 = 59, Need1 = 17000, Need2 = 22000, Need3 = 27000, BaseCost = 100, LevelCost = 20, MPowerBase = 100, PowerBase = 50, Range = 0 });
            if (!MagicExists(Spell.Blink)) MagicInfoList.Add(new MagicInfo { Name = "时光涌动", Spell = Spell.Blink, Icon = 20, Level1 = 19, Level2 = 22, Level3 = 25, Need1 = 350, Need2 = 1000, Need3 = 2000, BaseCost = 10, LevelCost = 3, Range = 9 });
            //if (!MagicExists(Spell.FastMove)) MagicInfoList.Add(new MagicInfo { Name = "FastMove", Spell = Spell.FastMove, Icon = ?, Level1 = ?, Level2 = ?, Level3 = ?, Need1 = ?, Need2 = ?, Need3 = ?, BaseCost = ?, LevelCost = ?, DelayBase = ?, DelayReduction = ? });
            if (!MagicExists(Spell.StormEscape)) MagicInfoList.Add(new MagicInfo { Name = "雷仙风", Spell = Spell.StormEscape, Icon = 81, Level1 = 60, Level2 = 61, Level3 = 62, Need1 = 2200, Need2 = 3300, Need3 = 4400, BaseCost = 65, LevelCost = 8, MPowerBase = 12, PowerBase = 4, DelayBase = 300000, DelayReduction = 40000, Range = 9 , MultiplierBase = 1f, MultiplierBonus = 0f});
            if (!MagicExists(Spell.HeavenlySecrets)) MagicInfoList.Add(new MagicInfo { Name = "天上秘术", Spell = Spell.HeavenlySecrets, Icon = 77, Level1 = 50, Level2 = 63, Level3 = 56, Need1 = 1000, Need2 = 2000, Need3 = 3500, BaseCost = 28, LevelCost = 2, MPowerBase = 1, PowerBase = 1, DelayBase = 600000, DelayReduction = 100000, Range = 0 , MultiplierBase = 1f, MultiplierBonus = 0f});
            if (!MagicExists(Spell.GreatFireBallRare)) MagicInfoList.Add(new MagicInfo { Name = "大火球-秘籍", Spell = Spell.GreatFireBallRare, Icon = 108, Level1 = 55, Level2 = 60, Level3 = 65, Need1 = 17000, Need2 = 22000, Need3 = 27000, BaseCost = 5, LevelCost = 1, MPowerBase = 15, PowerBase = 18, Range = 9 });
            if (!MagicExists(Spell.StormEscapeRare)) MagicInfoList.Add(new MagicInfo { Name = "雷仙风-秘籍", Spell = Spell.StormEscapeRare, Icon = 85, Level1 = 62, Level2 = 64, Level3 = 66, Need1 = 2200, Need2 = 3300, Need3 = 4400, BaseCost = 65, LevelCost = 8, MPowerBase = 30, PowerBase = 10, DelayBase = 300000, DelayReduction = 40000, Range = 9 , MultiplierBase = 3.25f, MultiplierBonus = 0.25f});
            
            
            //Taoist
            if (!MagicExists(Spell.Healing)) MagicInfoList.Add(new MagicInfo { Name = "治愈术", Spell = Spell.Healing, Icon = 1, Level1 = 7, Level2 = 11, Level3 = 14, Need1 = 150, Need2 = 350, Need3 = 700, BaseCost = 3, LevelCost = 2, MPowerBase = 14, Range = 9 });
            if (!MagicExists(Spell.SpiritSword)) MagicInfoList.Add(new MagicInfo { Name = "精神力战法", Spell = Spell.SpiritSword, Icon = 3, Level1 = 9, Level2 = 12, Level3 = 15, Need1 = 350, Need2 = 1300, Need3 = 2700, Range = 0 });
            if (!MagicExists(Spell.Poisoning)) MagicInfoList.Add(new MagicInfo { Name = "施毒术", Spell = Spell.Poisoning, Icon = 5, Level1 = 14, Level2 = 17, Level3 = 20, Need1 = 700, Need2 = 1300, Need3 = 2700, BaseCost = 2, LevelCost = 1, Range = 9 });
            if (!MagicExists(Spell.SoulFireBall)) MagicInfoList.Add(new MagicInfo { Name = "灵魂火符", Spell = Spell.SoulFireBall, Icon = 12, Level1 = 18, Level2 = 21, Level3 = 24, Need1 = 1300, Need2 = 2700, Need3 = 4000, BaseCost = 3, LevelCost = 1, MPowerBase = 8, PowerBase = 3, Range = 9 });
            if (!MagicExists(Spell.SummonSkeleton)) MagicInfoList.Add(new MagicInfo { Name = "召唤骷髅", Spell = Spell.SummonSkeleton, Icon = 16, Level1 = 19, Level2 = 22, Level3 = 26, Need1 = 1000, Need2 = 2000, Need3 = 3500, BaseCost = 12, LevelCost = 4, Range = 0 });
            if (!MagicExists(Spell.Hiding)) MagicInfoList.Add(new MagicInfo { Name = "隐身术", Spell = Spell.Hiding, Icon = 17, Level1 = 20, Level2 = 23, Level3 = 26, Need1 = 1300, Need2 = 2700, Need3 = 5300, BaseCost = 1, LevelCost = 1, Range = 0 });
            if (!MagicExists(Spell.MassHiding)) MagicInfoList.Add(new MagicInfo { Name = "集体隐身术", Spell = Spell.MassHiding, Icon = 18, Level1 = 21, Level2 = 25, Level3 = 29, Need1 = 1300, Need2 = 2700, Need3 = 5300, BaseCost = 2, LevelCost = 2, Range = 9 });
            if (!MagicExists(Spell.SoulShield)) MagicInfoList.Add(new MagicInfo { Name = "幽灵盾", Spell = Spell.SoulShield, Icon = 13, Level1 = 22, Level2 = 24, Level3 = 26, Need1 = 2000, Need2 = 3500, Need3 = 7000, BaseCost = 2, LevelCost = 2, Range = 9 });
            if (!MagicExists(Spell.Revelation)) MagicInfoList.Add(new MagicInfo { Name = "心灵启示", Spell = Spell.Revelation, Icon = 27, Level1 = 23, Level2 = 25, Level3 = 28, Need1 = 1500, Need2 = 2500, Need3 = 4000, BaseCost = 4, LevelCost = 4, Range = 9 });
            if (!MagicExists(Spell.BlessedArmour)) MagicInfoList.Add(new MagicInfo { Name = "神圣战甲术", Spell = Spell.BlessedArmour, Icon = 14, Level1 = 25, Level2 = 27, Level3 = 29, Need1 = 4000, Need2 = 6000, Need3 = 10000, BaseCost = 2, LevelCost = 2, Range = 9 });
            if (!MagicExists(Spell.EnergyRepulsor)) MagicInfoList.Add(new MagicInfo { Name = "气功波", Spell = Spell.EnergyRepulsor, Icon = 36, Level1 = 27, Level2 = 29, Level3 = 31, Need1 = 1800, Need2 = 2400, Need3 = 3200, BaseCost = 2, LevelCost = 2, Range = 0, MPowerBase = 4 });
            if (!MagicExists(Spell.TrapHexagon)) MagicInfoList.Add(new MagicInfo { Name = "困魔咒", Spell = Spell.TrapHexagon, Icon = 15, Level1 = 28, Level2 = 30, Level3 = 32, Need1 = 2500, Need2 = 5000, Need3 = 10000, BaseCost = 7, LevelCost = 3, Range = 9 });
            if (!MagicExists(Spell.Purification)) MagicInfoList.Add(new MagicInfo { Name = "净化术", Spell = Spell.Purification, Icon = 39, Level1 = 30, Level2 = 32, Level3 = 35, Need1 = 3000, Need2 = 5000, Need3 = 8000, BaseCost = 14, LevelCost = 2, Range = 9 });
            if (!MagicExists(Spell.MassHealing)) MagicInfoList.Add(new MagicInfo { Name = "群体治疗术", Spell = Spell.MassHealing, Icon = 28, Level1 = 31, Level2 = 33, Level3 = 36, Need1 = 2000, Need2 = 4000, Need3 = 8000, BaseCost = 28, LevelCost = 3, MPowerBase = 10, PowerBase = 4, Range = 9 });
            if (!MagicExists(Spell.Hallucination)) MagicInfoList.Add(new MagicInfo { Name = "迷魂术", Spell = Spell.Hallucination, Icon = 48, Level1 = 31, Level2 = 34, Level3 = 36, Need1 = 4000, Need2 = 6000, Need3 = 9000, BaseCost = 22, LevelCost = 10, Range = 9 });
            if (!MagicExists(Spell.UltimateEnhancer)) MagicInfoList.Add(new MagicInfo { Name = "无极真气", Spell = Spell.UltimateEnhancer, Icon = 35, Level1 = 33, Level2 = 35, Level3 = 38, Need1 = 5000, Need2 = 7000, Need3 = 10000, BaseCost = 28, LevelCost = 4, Range = 9 });
            if (!MagicExists(Spell.SummonShinsu)) MagicInfoList.Add(new MagicInfo { Name = "召唤神兽", Spell = Spell.SummonShinsu, Icon = 29, Level1 = 35, Level2 = 37, Level3 = 40, Need1 = 2000, Need2 = 4000, Need3 = 6000, BaseCost = 28, LevelCost = 4, Range = 0 });
            if (!MagicExists(Spell.Reincarnation)) MagicInfoList.Add(new MagicInfo { Name = "苏生术", Spell = Spell.Reincarnation, Icon = 53, Level1 = 37, Level2 = 39, Level3 = 41, Need1 = 2000, Need2 = 6000, Need3 = 10000, BaseCost = 125, LevelCost = 17, Range = 9 });
            if (!MagicExists(Spell.SummonHolyDeva)) MagicInfoList.Add(new MagicInfo { Name = "精魂召唤术", Spell = Spell.SummonHolyDeva, Icon = 40, Level1 = 38, Level2 = 41, Level3 = 43, Need1 = 4000, Need2 = 6000, Need3 = 9000, BaseCost = 28, LevelCost = 4, Range = 0 });
            if (!MagicExists(Spell.Curse)) MagicInfoList.Add(new MagicInfo { Name = "诅咒术", Spell = Spell.Curse, Icon = 45, Level1 = 40, Level2 = 42, Level3 = 44, Need1 = 4000, Need2 = 6000, Need3 = 9000, BaseCost = 17, LevelCost = 3, Range = 9, MPowerBase = 20 });
            if (!MagicExists(Spell.Plague)) MagicInfoList.Add(new MagicInfo { Name = "烦脑", Spell = Spell.Plague, Icon = 74, Level1 = 42, Level2 = 44, Level3 = 47, Need1 = 5000, Need2 = 9000, Need3 = 13000, BaseCost = 20, LevelCost = 5, Range = 9 });
            if (!MagicExists(Spell.PoisonCloud)) MagicInfoList.Add(new MagicInfo { Name = "毒雾", Spell = Spell.PoisonCloud, Icon = 54, Level1 = 43, Level2 = 45, Level3 = 48, Need1 = 4000, Need2 = 8000, Need3 = 12000, BaseCost = 30, LevelCost = 5, MPowerBase = 40, PowerBase = 20, DelayBase = 18000, DelayReduction = 2000, Range = 9 });
            if (!MagicExists(Spell.EnergyShield)) MagicInfoList.Add(new MagicInfo { Name = "先天气功", Spell = Spell.EnergyShield, Icon = 57, Level1 = 48, Level2 = 51, Level3 = 54, Need1 = 5000, Need2 = 9000, Need3 = 13000, BaseCost = 50, LevelCost = 20, Range = 9 });
            if (!MagicExists(Spell.PetEnhancer)) MagicInfoList.Add(new MagicInfo { Name = "血龙水", Spell = Spell.PetEnhancer, Icon = 78, Level1 = 45, Level2 = 48, Level3 = 51, Need1 = 4000, Need2 = 8000, Need3 = 12000, BaseCost = 30, LevelCost = 40, Range = 0 });
            if (!MagicExists(Spell.HealingCircle)) MagicInfoList.Add(new MagicInfo { Name = "阴阳五行阵", Spell = Spell.HealingCircle, Icon = 82, Level1 = 39, Level2 = 41, Level3 = 43, Need1 = 7000, Need2 = 12000, Need3 = 15000, BaseCost = 10, LevelCost = 100 });
			if (!MagicExists(Spell.HealingRare)) MagicInfoList.Add(new MagicInfo { Name = "治愈术-秘籍", Spell = Spell.HealingRare, Icon = 109, Level1 = 55, Level2 = 60, Level3 = 65, Need1 = 17000, Need2 = 22000, Need3 = 27000, BaseCost = 3, LevelCost = 2, MPowerBase = 14, Range = 9 });
            if (!MagicExists(Spell.HealingcircleRare)) MagicInfoList.Add(new MagicInfo { Name = "阴阳五行阵-秘籍", Spell = Spell.HealingcircleRare, Icon = 86, Level1 = 60, Level2 = 61, Level3 = 61, Need1 = 4400, Need2 = 7400, Need3 = 11700, BaseCost = 28, LevelCost = 3, DelayBase = 1800, DelayReduction = 0 });

            //Assassin
            if (!MagicExists(Spell.FatalSword)) MagicInfoList.Add(new MagicInfo { Name = "绝命剑法", Spell = Spell.FatalSword, Icon = 58, Level1 = 7, Level2 = 9, Level3 = 12, Need1 = 500, Need2 = 1000, Need3 = 2300, Range = 0 });
            if (!MagicExists(Spell.DoubleSlash)) MagicInfoList.Add(new MagicInfo { Name = "风剑术", Spell = Spell.DoubleSlash, Icon = 59, Level1 = 15, Level2 = 17, Level3 = 19, Need1 = 700, Need2 = 1500, Need3 = 2200, BaseCost = 2, LevelCost = 1 });
            if (!MagicExists(Spell.Haste)) MagicInfoList.Add(new MagicInfo { Name = "体迅风", Spell = Spell.Haste, Icon = 60, Level1 = 20, Level2 = 22, Level3 = 25, Need1 = 2000, Need2 = 3000, Need3 = 6000, BaseCost = 3, LevelCost = 2, Range = 0 });
            if (!MagicExists(Spell.FlashDash)) MagicInfoList.Add(new MagicInfo { Name = "拔刀术", Spell = Spell.FlashDash, Icon = 61, Level1 = 25, Level2 = 27, Level3 = 30, Need1 = 4000, Need2 = 7000, Need3 = 9000, BaseCost = 12, LevelCost = 2, DelayBase = 200, Range = 0 });
            if (!MagicExists(Spell.LightBody)) MagicInfoList.Add(new MagicInfo { Name = "风身术", Spell = Spell.LightBody, Icon = 68, Level1 = 27, Level2 = 29, Level3 = 32, Need1 = 5000, Need2 = 7000, Need3 = 10000, BaseCost = 11, LevelCost = 2, Range = 0 });
            if (!MagicExists(Spell.HeavenlySword)) MagicInfoList.Add(new MagicInfo { Name = "迁移剑", Spell = Spell.HeavenlySword, Icon = 62, Level1 = 30, Level2 = 32, Level3 = 35, Need1 = 4000, Need2 = 8000, Need3 = 10000, BaseCost = 13, LevelCost = 2, MPowerBase = 8, Range = 0 });
            if (!MagicExists(Spell.FireBurst)) MagicInfoList.Add(new MagicInfo { Name = "烈风击", Spell = Spell.FireBurst, Icon = 63, Level1 = 33, Level2 = 35, Level3 = 38, Need1 = 4000, Need2 = 6000, Need3 = 8000, BaseCost = 10, LevelCost = 1, Range = 0 });
            if (!MagicExists(Spell.Trap)) MagicInfoList.Add(new MagicInfo { Name = "捕缚术", Spell = Spell.Trap, Icon = 64, Level1 = 33, Level2 = 35, Level3 = 38, Need1 = 2000, Need2 = 4000, Need3 = 6000, BaseCost = 14, LevelCost = 2, DelayBase = 60000, DelayReduction = 15000, Range = 9 });
            if (!MagicExists(Spell.PoisonSword)) MagicInfoList.Add(new MagicInfo { Name = "猛毒剑气", Spell = Spell.PoisonSword, Icon = 69, Level1 = 34, Level2 = 36, Level3 = 39, Need1 = 5000, Need2 = 8000, Need3 = 11000, BaseCost = 14, LevelCost = 3, Range = 0 });
            if (!MagicExists(Spell.MoonLight)) MagicInfoList.Add(new MagicInfo { Name = "月影术", Spell = Spell.MoonLight, Icon = 65, Level1 = 36, Level2 = 39, Level3 = 42, Need1 = 3000, Need2 = 5000, Need3 = 8000, BaseCost = 36, LevelCost = 3, Range = 0 });
            if (!MagicExists(Spell.MPEater)) MagicInfoList.Add(new MagicInfo { Name = "吸气", Spell = Spell.MPEater, Icon = 66, Level1 = 38, Level2 = 41, Level3 = 44, Need1 = 5000, Need2 = 8000, Need3 = 11000, Range = 0 });
            if (!MagicExists(Spell.SwiftFeet)) MagicInfoList.Add(new MagicInfo { Name = "轻身步", Spell = Spell.SwiftFeet, Icon = 67, Level1 = 40, Level2 = 43, Level3 = 46, Need1 = 4000, Need2 = 6000, Need3 = 9000, BaseCost = 17, LevelCost = 5, DelayBase = 210000, DelayReduction = 40000, Range = 0 });
            if (!MagicExists(Spell.DarkBody)) MagicInfoList.Add(new MagicInfo { Name = "烈火身", Spell = Spell.DarkBody, Icon = 70, Level1 = 46, Level2 = 49, Level3 = 52, Need1 = 6000, Need2 = 10000, Need3 = 14000, BaseCost = 40, LevelCost = 7, Range = 0 });
            if (!MagicExists(Spell.Hemorrhage)) MagicInfoList.Add(new MagicInfo { Name = "血风击", Spell = Spell.Hemorrhage, Icon = 75, Level1 = 47, Level2 = 51, Level3 = 55, Need1 = 9000, Need2 = 15000, Need3 = 21000, Range = 0 });
            if (!MagicExists(Spell.CrescentSlash)) MagicInfoList.Add(new MagicInfo { Name = "月华乱舞", Spell = Spell.CrescentSlash, Icon = 71, Level1 = 50, Level2 = 53, Level3 = 56, Need1 = 12000, Need2 = 16000, Need3 = 24000, BaseCost = 19, LevelCost = 3, Range = 0 });
            if (!MagicExists(Spell.MoonMist)) MagicInfoList.Add(new MagicInfo { Name = "月影雾", Spell = Spell.MoonMist, Icon = 83, Level1 = 60, Level2 = 61, Level3 = 62, Need1 = 3650, Need2 = 5950, Need3 = 8800, BaseCost = 30, LevelCost = 5, DelayBase = 180000, DelayReduction = 30000 });
            if (!MagicExists(Spell.CatTongue)) MagicInfoList.Add(new MagicInfo { Name = "猫舌兰", Spell = Spell.CatTongue, Icon = 79, Level1 = 48, Level2 = 51, Level3 = 53, Need1 = 2000, Need2 = 4000, Need3 = 7000, BaseCost = 30, LevelCost = 5, DelayBase = 360000, DelayReduction = 60000 });

            //Archer
            if (!MagicExists(Spell.Focus)) MagicInfoList.Add(new MagicInfo { Name = "必中闪", Spell = Spell.Focus, Icon = 88, Level1 = 7, Level2 = 13, Level3 = 17, Need1 = 270, Need2 = 600, Need3 = 1300, Range = 0 });
            if (!MagicExists(Spell.StraightShot)) MagicInfoList.Add(new MagicInfo { Name = "天日闪", Spell = Spell.StraightShot, Icon = 89, Level1 = 9, Level2 = 12, Level3 = 16, Need1 = 350, Need2 = 750, Need3 = 1400, BaseCost = 3, LevelCost = 2, MPowerBase = 8, PowerBase = 3, Range = 9 });
            if (!MagicExists(Spell.DoubleShot)) MagicInfoList.Add(new MagicInfo { Name = "无我闪", Spell = Spell.DoubleShot, Icon = 90, Level1 = 14, Level2 = 18, Level3 = 21, Need1 = 700, Need2 = 1500, Need3 = 2100, BaseCost = 3, LevelCost = 2, MPowerBase = 6, PowerBase = 2, Range = 9 });
            if (!MagicExists(Spell.ExplosiveTrap)) MagicInfoList.Add(new MagicInfo { Name = "爆阱", Spell = Spell.ExplosiveTrap, Icon = 91, Level1 = 22, Level2 = 25, Level3 = 30, Need1 = 2000, Need2 = 3500, Need3 = 5000, BaseCost = 10, LevelCost = 3, MPowerBase = 15, PowerBase = 15, Range = 0 });
            if (!MagicExists(Spell.DelayedExplosion)) MagicInfoList.Add(new MagicInfo { Name = "爆闪", Spell = Spell.DelayedExplosion, Icon = 92, Level1 = 31, Level2 = 34, Level3 = 39, Need1 = 3000, Need2 = 7000, Need3 = 10000, BaseCost = 8, LevelCost = 2, MPowerBase = 30, PowerBase = 15, Range = 9 });
            if (!MagicExists(Spell.Meditation)) MagicInfoList.Add(new MagicInfo { Name = "气功术", Spell = Spell.Meditation, Icon = 93, Level1 = 19, Level2 = 24, Level3 = 29, Need1 = 1800, Need2 = 2600, Need3 = 5600, BaseCost = 8, LevelCost = 2, Range = 0 });
            if (!MagicExists(Spell.ElementalShot)) MagicInfoList.Add(new MagicInfo { Name = "万金闪", Spell = Spell.ElementalShot, Icon = 94, Level1 = 20, Level2 = 25, Level3 = 31, Need1 = 1800, Need2 = 2700, Need3 = 6000, BaseCost = 8, LevelCost = 2, MPowerBase = 6, PowerBase = 3, Range = 9 });
            if (!MagicExists(Spell.Concentration)) MagicInfoList.Add(new MagicInfo { Name = "气流术", Spell = Spell.Concentration, Icon = 96, Level1 = 23, Level2 = 27, Level3 = 32, Need1 = 2100, Need2 = 3800, Need3 = 6500, BaseCost = 8, LevelCost = 2, Range = 0 });
            if (!MagicExists(Spell.ElementalBarrier)) MagicInfoList.Add(new MagicInfo { Name = "金刚术", Spell = Spell.ElementalBarrier, Icon = 98, Level1 = 33, Level2 = 38, Level3 = 44, Need1 = 3000, Need2 = 7000, Need3 = 10000, BaseCost = 10, LevelCost = 2, MPowerBase = 15, PowerBase = 5, Range = 0 });
            if (!MagicExists(Spell.BackStep)) MagicInfoList.Add(new MagicInfo { Name = "风弹步", Spell = Spell.BackStep, Icon = 95, Level1 = 30, Level2 = 34, Level3 = 38, Need1 = 2400, Need2 = 3000, Need3 = 6000, BaseCost = 12, LevelCost = 2, DelayBase = 2500, Range = 0 });
            if (!MagicExists(Spell.BindingShot)) MagicInfoList.Add(new MagicInfo { Name = "困兽笼", Spell = Spell.BindingShot, Icon = 46, Level1 = 35, Level2 = 39, Level3 = 42, Need1 = 400, Need2 = 7000, Need3 = 9500, BaseCost = 7, LevelCost = 3, Range = 9 });
            if (!MagicExists(Spell.Stonetrap)) MagicInfoList.Add(new MagicInfo { Name = "地柱钉", Spell = Spell.Stonetrap, Icon = 97, Level1 = 40, Level2 = 43, Level3 = 46, Need1 = 4900, Need2 = 9800, Need3 = 14100, BaseCost = 7, LevelCost = 3, Range = 9 });
            if (!MagicExists(Spell.SummonVampire)) MagicInfoList.Add(new MagicInfo { Name = "吸血地精", Spell = Spell.SummonVampire, Icon = 99, Level1 = 28, Level2 = 33, Level3 = 41, Need1 = 2000, Need2 = 2700, Need3 = 7500, BaseCost = 10, LevelCost = 5, Range = 9 });
            if (!MagicExists(Spell.VampireShot)) MagicInfoList.Add(new MagicInfo { Name = "吸血地闪", Spell = Spell.VampireShot, Icon = 100, Level1 = 26, Level2 = 32, Level3 = 36, Need1 = 3000, Need2 = 6000, Need3 = 12000, BaseCost = 12, LevelCost = 3, MPowerBase = 10, PowerBase = 7, Range = 9 });
            if (!MagicExists(Spell.SummonToad)) MagicInfoList.Add(new MagicInfo { Name = "痹魔阱", Spell = Spell.SummonToad, Icon = 101, Level1 = 37, Level2 = 43, Level3 = 47, Need1 = 5800, Need2 = 10000, Need3 = 13000, BaseCost = 10, LevelCost = 5, Range = 9 });
            if (!MagicExists(Spell.PoisonShot)) MagicInfoList.Add(new MagicInfo { Name = "毒魔闪", Spell = Spell.PoisonShot, Icon = 102, Level1 = 40, Level2 = 45, Level3 = 49, Need1 = 6000, Need2 = 14000, Need3 = 16000, BaseCost = 10, LevelCost = 4, MPowerBase = 10, PowerBase = 10, Range = 9 });
            if (!MagicExists(Spell.CrippleShot)) MagicInfoList.Add(new MagicInfo { Name = "邪爆闪", Spell = Spell.CrippleShot, Icon = 103, Level1 = 43, Level2 = 47, Level3 = 50, Need1 = 12000, Need2 = 15000, Need3 = 18000, BaseCost = 15, LevelCost = 3, MPowerBase = 10, MPowerBonus = 20, PowerBase = 10, Range = 9 });
            if (!MagicExists(Spell.SummonSnakes)) MagicInfoList.Add(new MagicInfo { Name = "蛇柱阱", Spell = Spell.SummonSnakes, Icon = 104, Level1 = 46, Level2 = 51, Level3 = 54, Need1 = 14000, Need2 = 17000, Need3 = 20000, BaseCost = 10, LevelCost = 5, Range = 9 });
            if (!MagicExists(Spell.NapalmShot)) MagicInfoList.Add(new MagicInfo { Name = "血龙闪", Spell = Spell.NapalmShot, Icon = 105, Level1 = 48, Level2 = 52, Level3 = 55, Need1 = 15000, Need2 = 18000, Need3 = 21000, BaseCost = 40, LevelCost = 10, MPowerBase = 25, MPowerBonus = 25, PowerBase = 25, Range = 9 });
            if (!MagicExists(Spell.OneWithNature)) MagicInfoList.Add(new MagicInfo { Name = "血龙闪秘笈", Spell = Spell.OneWithNature, Icon = 106, Level1 = 50, Level2 = 53, Level3 = 56, Need1 = 17000, Need2 = 19000, Need3 = 24000, BaseCost = 80, LevelCost = 15, MPowerBase = 75, MPowerBonus = 35, PowerBase = 30, PowerBonus = 20, Range = 9 });
            if (!MagicExists(Spell.MentalState)) MagicInfoList.Add(new MagicInfo { Name = "精神状态", Spell = Spell.MentalState, Icon = 81, Level1 = 11, Level2 = 15, Level3 = 22, Need1 = 500, Need2 = 900, Need3 = 1800, BaseCost = 1, LevelCost = 1, Range = 0 });

            //Custom
            if (!MagicExists(Spell.Portal)) MagicInfoList.Add(new MagicInfo { Name = "传送门", Spell = Spell.Portal, Icon = 1, Level1 = 7, Level2 = 11, Level3 = 14, Need1 = 150, Need2 = 350, Need3 = 700, BaseCost = 3, LevelCost = 2, Range = 9 });
            if (!MagicExists(Spell.BattleCry)) MagicInfoList.Add(new MagicInfo {  Name = "呐喊", Spell = Spell.BattleCry, Icon = 42, Level1 = 48, Level2 = 51, Level3 = 55, Need1 = 8000, Need2 = 11000, Need3 = 15000, BaseCost = 22, LevelCost = 10, Range = 0 });
            if (!MagicExists(Spell.FireBounce)) MagicInfoList.Add(new MagicInfo { Name = "火焰跳动", Spell = Spell.FireBounce, Icon = 4, Level1 = 15, Level2 = 18, Level3 = 21, Need1 = 2000, Need2 = 2700, Need3 = 3500, BaseCost = 5, LevelCost = 1, MPowerBase = 6, PowerBase = 10, Range = 9 });
            if (!MagicExists(Spell.MeteorShower)) MagicInfoList.Add(new MagicInfo { Name = "火流星", Spell = Spell.MeteorShower, Icon = 4, Level1 = 15, Level2 = 18, Level3 = 21, Need1 = 2000, Need2 = 2700, Need3 = 3500, BaseCost = 5, LevelCost = 1, MPowerBase = 6, PowerBase = 10, Range = 9 });

            //—— 新增技能（数值 / 图标 对照官网 mir2db 技能列表 49~53，Icon = 官网技能id - 1）——

            //法师
            if (!MagicExists(Spell.ThunderBoltRare)) MagicInfoList.Add(new MagicInfo { Name = "强击-秘籍", Spell = Spell.ThunderBoltRare, Icon = 114, Level1 = 95, Level2 = 95, Level3 = 95, Need1 = 20000, Need2 = 32000, Need3 = 45000, BaseCost = 25, LevelCost = 5, MPowerBase = 8, MPowerBonus = 20, PowerBase = 9, Range = 9, DelayBase = 8000, DelayReduction = 1000, MultiplierBase = 1f, MultiplierBonus = 0.15f });
            if (!MagicExists(Spell.AbsorbFlame)) MagicInfoList.Add(new MagicInfo { Name = "吸魔炎风", Spell = Spell.AbsorbFlame, Icon = 119, Level1 = 90, Level2 = 90, Level3 = 90, Need1 = 3800, Need2 = 6300, Need3 = 9300, BaseCost = 30, LevelCost = 6, MPowerBase = 12, MPowerBonus = 12, PowerBase = 15, Range = 9, DelayBase = 6000, DelayReduction = 500, MultiplierBase = 1f, MultiplierBonus = 0.25f });
            if (!MagicExists(Spell.AbsorbFlameRare)) MagicInfoList.Add(new MagicInfo { Name = "吸魔炎风-秘籍", Spell = Spell.AbsorbFlameRare, Icon = 124, Level1 = 100, Level2 = 100, Level3 = 100, Need1 = 5400, Need2 = 8800, Need3 = 13000, BaseCost = 35, LevelCost = 6, MPowerBase = 18, MPowerBonus = 15, PowerBase = 20, Range = 9, DelayBase = 10000, DelayReduction = 1000, MultiplierBase = 2f, MultiplierBonus = 0.5f });

            //道士
            if (!MagicExists(Spell.GreatTalisman)) MagicInfoList.Add(new MagicInfo { Name = "万效符", Spell = Spell.GreatTalisman, Icon = 120, Level1 = 90, Level2 = 90, Level3 = 90, Need1 = 18500, Need2 = 29900, Need3 = 43200, BaseCost = 50, LevelCost = 10, MPowerBase = 10, PowerBase = 5, Range = 0, DelayBase = 600000, DelayReduction = 120000 });
            if (!MagicExists(Spell.GreatTalismanRare)) MagicInfoList.Add(new MagicInfo { Name = "万效符-秘籍", Spell = Spell.GreatTalismanRare, Icon = 125, Level1 = 100, Level2 = 100, Level3 = 100, Need1 = 23600, Need2 = 38900, Need3 = 57600, BaseCost = 55, LevelCost = 10, MPowerBase = 15, PowerBase = 8, Range = 0, DelayBase = 600000, DelayReduction = 120000 });
            if (!MagicExists(Spell.SoulFireBallPlus)) MagicInfoList.Add(new MagicInfo { Name = "灵魂火符(万效)", Spell = Spell.SoulFireBallPlus, Icon = 128, Level1 = 90, Level2 = 90, Level3 = 90, Need1 = 3000, Need2 = 6000, Need3 = 12000, BaseCost = 12, LevelCost = 3, MPowerBase = 8, PowerBase = 3, Range = 9, DelayBase = 4000, DelayReduction = 500 });
            if (!MagicExists(Spell.SoulFireBallPlusRare)) MagicInfoList.Add(new MagicInfo { Name = "灵魂火符(万效-秘)", Spell = Spell.SoulFireBallPlusRare, Icon = 129, Level1 = 100, Level2 = 100, Level3 = 100, Need1 = 4000, Need2 = 8000, Need3 = 15000, BaseCost = 15, LevelCost = 3, MPowerBase = 12, PowerBase = 4, Range = 9, DelayBase = 4000, DelayReduction = 500 });
            if (!MagicExists(Spell.PetEnhancerRare)) MagicInfoList.Add(new MagicInfo { Name = "血龙兽-秘籍", Spell = Spell.PetEnhancerRare, Icon = 115, Level1 = 95, Level2 = 95, Level3 = 95, Need1 = 23600, Need2 = 38900, Need3 = 57600, BaseCost = 40, LevelCost = 8, MPowerBase = 10, PowerBase = 6, Range = 0, DelayBase = 300000, DelayReduction = 50000 });

            //刺客
            if (!MagicExists(Spell.CrescentSlashRare)) MagicInfoList.Add(new MagicInfo { Name = "月华乱舞-秘籍", Spell = Spell.CrescentSlashRare, Icon = 113, Level1 = 95, Level2 = 95, Level3 = 95, Need1 = 23600, Need2 = 38900, Need3 = 57600, BaseCost = 30, LevelCost = 6, MPowerBase = 1, PowerBase = 8, Range = 0, MultiplierBase = 1f, MultiplierBonus = 2f });
            if (!MagicExists(Spell.ShadowCombo)) MagicInfoList.Add(new MagicInfo { Name = "影华连击", Spell = Spell.ShadowCombo, Icon = 118, Level1 = 90, Level2 = 90, Level3 = 90, Need1 = 4500, Need2 = 7300, Need3 = 10800, BaseCost = 20, LevelCost = 4, MPowerBase = 1, PowerBase = 6, Range = 1, MultiplierBase = 1f, MultiplierBonus = 0.25f, DelayBase = 6000, DelayReduction = 1000 });
            if (!MagicExists(Spell.ShadowComboRare)) MagicInfoList.Add(new MagicInfo { Name = "影华连击-秘笈", Spell = Spell.ShadowComboRare, Icon = 123, Level1 = 100, Level2 = 100, Level3 = 100, Need1 = 6600, Need2 = 11100, Need3 = 16200, BaseCost = 25, LevelCost = 4, MPowerBase = 1, PowerBase = 8, Range = 1, MultiplierBase = 3f, MultiplierBonus = 1f, DelayBase = 8000, DelayReduction = 1500 });
            if (!MagicExists(Spell.FlashDashRare)) MagicInfoList.Add(new MagicInfo { Name = "拔刀术-秘籍", Spell = Spell.FlashDashRare, Icon = 110, Level1 = 55, Level2 = 60, Level3 = 65, Need1 = 10000, Need2 = 13000, Need3 = 16000, BaseCost = 25, LevelCost = 4, MPowerBase = 10, PowerBase = 280, Range = 0, MultiplierBase = 1f, MultiplierBonus = 0.25f, DelayBase = 3000 });
            if (!MagicExists(Spell.MoonMistRare)) MagicInfoList.Add(new MagicInfo { Name = "月影雾-秘籍", Spell = Spell.MoonMistRare, Icon = 87, Level1 = 62, Level2 = 64, Level3 = 66, Need1 = 3650, Need2 = 5950, Need3 = 8800, BaseCost = 30, LevelCost = 6, Range = 0, DelayBase = 240000, DelayReduction = 40000 });

            //弓手
            if (!MagicExists(Spell.SkyThunderShot)) MagicInfoList.Add(new MagicInfo { Name = "落雷杀", Spell = Spell.SkyThunderShot, Icon = 121, Level1 = 90, Level2 = 90, Level3 = 90, Need1 = 3800, Need2 = 6300, Need3 = 9300, BaseCost = 25, LevelCost = 5, MPowerBase = 5, PowerBase = 10, Range = 9, DelayBase = 8000, DelayReduction = 1000, MultiplierBase = 1f, MultiplierBonus = 0.25f });
            if (!MagicExists(Spell.SkyThunderShotRare)) MagicInfoList.Add(new MagicInfo { Name = "落雷杀-秘笈", Spell = Spell.SkyThunderShotRare, Icon = 126, Level1 = 100, Level2 = 100, Level3 = 100, Need1 = 5400, Need2 = 8800, Need3 = 13000, BaseCost = 30, LevelCost = 5, MPowerBase = 8, PowerBase = 15, Range = 9, DelayBase = 10000, DelayReduction = 1500, MultiplierBase = 2f, MultiplierBonus = 0.125f });
            if (!MagicExists(Spell.DeathBlow)) MagicInfoList.Add(new MagicInfo { Name = "灭杀", Spell = Spell.DeathBlow, Icon = 127, Level1 = 100, Level2 = 100, Level3 = 100, Need1 = 4000, Need2 = 8000, Need3 = 14000, BaseCost = 15, LevelCost = 3, MPowerBase = 10, PowerBase = 25, Range = 1, DelayBase = 3000, DelayReduction = 500, MultiplierBase = 2f, MultiplierBonus = 0.25f });
            if (!MagicExists(Spell.DelayedExplosionRare)) MagicInfoList.Add(new MagicInfo { Name = "爆闪-秘籍", Spell = Spell.DelayedExplosionRare, Icon = 111, Level1 = 55, Level2 = 60, Level3 = 65, Need1 = 17000, Need2 = 22000, Need3 = 27000, BaseCost = 30, LevelCost = 5, MPowerBase = 10, PowerBase = 300, Range = 9, DelayBase = 3000 });
            if (!MagicExists(Spell.ConcentrationRare)) MagicInfoList.Add(new MagicInfo { Name = "气流术-秘籍", Spell = Spell.ConcentrationRare, Icon = 116, Level1 = 95, Level2 = 95, Level3 = 95, Need1 = 23600, Need2 = 38900, Need3 = 57600, BaseCost = 20, LevelCost = 4, Range = 0 });
        }

        private string CanStartEnvir()
        {
            if (StartPoints.Count == 0) return "尚未设置角色出生点，无法启动服务器";

            if (Settings.EnforceDBChecks)
            {
                if (GetMonsterInfo(Settings.SkeletonName, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.SkeletonName;
                if (GetMonsterInfo(Settings.ShinsuName, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.ShinsuName;
                if (GetMonsterInfo(Settings.BugBatName, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.BugBatName;
                if (GetMonsterInfo(Settings.Zuma1, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Zuma1;
                if (GetMonsterInfo(Settings.Zuma2, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Zuma2;
                if (GetMonsterInfo(Settings.Zuma3, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Zuma3;
                if (GetMonsterInfo(Settings.Zuma4, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Zuma4;
                if (GetMonsterInfo(Settings.Zuma5, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Zuma5;
                if (GetMonsterInfo(Settings.Zuma6, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Zuma6;
                if (GetMonsterInfo(Settings.Zuma7, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Zuma7;
                if (GetMonsterInfo(Settings.Turtle1, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Turtle1;
                if (GetMonsterInfo(Settings.Turtle2, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Turtle2;
                if (GetMonsterInfo(Settings.Turtle3, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Turtle3;
                if (GetMonsterInfo(Settings.Turtle4, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Turtle4;
                if (GetMonsterInfo(Settings.Turtle5, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Turtle5;
                if (GetMonsterInfo(Settings.BoneMonster1, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.BoneMonster1;
                if (GetMonsterInfo(Settings.BoneMonster2, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.BoneMonster2;
                if (GetMonsterInfo(Settings.BoneMonster3, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.BoneMonster3;
                if (GetMonsterInfo(Settings.BoneMonster4, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.BoneMonster4;
                if (GetMonsterInfo(Settings.BehemothMonster1, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.BehemothMonster1;
                if (GetMonsterInfo(Settings.BehemothMonster2, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.BehemothMonster2;
                if (GetMonsterInfo(Settings.BehemothMonster3, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.BehemothMonster3;
                if (GetMonsterInfo(Settings.HellKnight1, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.HellKnight1;
                if (GetMonsterInfo(Settings.HellKnight2, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.HellKnight2;
                if (GetMonsterInfo(Settings.HellKnight3, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.HellKnight3;
                if (GetMonsterInfo(Settings.HellKnight4, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.HellKnight4;
                if (GetMonsterInfo(Settings.HellBomb1, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.HellBomb1;
                if (GetMonsterInfo(Settings.HellBomb2, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.HellBomb2;
                if (GetMonsterInfo(Settings.HellBomb3, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.HellBomb3;
                if (GetMonsterInfo(Settings.WhiteSnake, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.WhiteSnake;
                if (GetMonsterInfo(Settings.AngelName, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.AngelName;
                if (GetMonsterInfo(Settings.BombSpiderName, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.BombSpiderName;
                if (GetMonsterInfo(Settings.CloneName, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.CloneName;
                if (GetMonsterInfo(Settings.AssassinCloneName, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.AssassinCloneName;
                if (GetMonsterInfo(Settings.VampireName, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.VampireName;
                if (GetMonsterInfo(Settings.ToadName, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.ToadName;
                if (GetMonsterInfo(Settings.SnakeTotemName, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.SnakeTotemName;
                if (GetMonsterInfo(Settings.StoneName, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.StoneName;
                if (GetMonsterInfo(Settings.FishingMonster, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.FishingMonster;
                if (GetMonsterInfo(Settings.GeneralMeowMeowMob1, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.GeneralMeowMeowMob1;
                if (GetMonsterInfo(Settings.GeneralMeowMeowMob2, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.GeneralMeowMeowMob2;
                if (GetMonsterInfo(Settings.GeneralMeowMeowMob3, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.GeneralMeowMeowMob3;
                if (GetMonsterInfo(Settings.GeneralMeowMeowMob4, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.GeneralMeowMeowMob4;
                if (GetMonsterInfo(Settings.KingHydraxMob, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.KingHydraxMob;
                if (GetMonsterInfo(Settings.HornedCommanderMob1, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.HornedCommanderMob1;
                if (GetMonsterInfo(Settings.HornedCommanderMob2, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.HornedCommanderMob2;
                if (GetMonsterInfo(Settings.HornedCommanderBombMob, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.HornedCommanderBombMob;
                if (GetMonsterInfo(Settings.SnowWolfKingMob, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.SnowWolfKingMob;
                if (GetMonsterInfo(Settings.CallScrollMob, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.CallScrollMob;
                if (GetMonsterInfo(Settings.ShardMaidenMob1, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.ShardMaidenMob1;
                if (GetMonsterInfo(Settings.ShardMaidenMob2, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.ShardMaidenMob2;
                if (GetMonsterInfo(Settings.ShardMaidenMob3, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.ShardMaidenMob3;
                if (GetMonsterInfo(Settings.ShardMaidenMob4, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.ShardMaidenMob4;
                if (GetMonsterInfo(Settings.Mon570NMirrorImageMob, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Mon570NMirrorImageMob;
                if (GetMonsterInfo(Settings.Mon573NMob, true) == null) return "缺少必要怪物无法启动服务器 " + Settings.Mon573NMob;

                if (GetItemInfo(Settings.RefineOreName) == null) return "缺少精炼所需矿石无法启动服务器" + Settings.RefineOreName;
            }

            WorldMapIcon wmi = ValidateWorldMap();
            if (wmi != null)
                return $"无效的世界地图目录: {wmi.MapIndex} ({wmi.Title})";


            //add intelligent creature checks?

            return "true";
        }

        //＝＝＝内循环容错（10-24）：任何一拍异常只记日志，不停服、不踢人＝＝＝
        //
        // 11-02 重做（用户实测「服务器刷错误的时候进图明显变慢」）：
        //   原实现每 1 秒最多记一条，但每条都要
        //     · new StackTrace(ex, true) —— 会去解析 PDB 取行号，异常路径上最贵的一步；
        //     · 再同步写一次日志文件。
        //   坏对象/脏数据是「每拍都炸」的，1 秒一条意味着一直在花这份钱。
        // 现在改成：
        //   ① 同一处异常（tag+类型+方法+消息首行）30 秒内只写一条，并累计「又压掉了 N 次」；
        //   ② 不再构造 StackTrace —— 直接用异常自带的 StackTrace 字符串，零解析开销；
        //   ③ 落盘交给 AsyncLog 后台线程，主循环只入队。
        // 结果：报错再怎么刷，也不会再拖慢进图/走路的节奏。
        private long _lastInnerErrorLog;
        private sealed class InnerErrorStat
        {
            public long LastLogTime;
            public int Suppressed;
        }
        private readonly Dictionary<string, InnerErrorStat> _innerErrorStats = new Dictionary<string, InnerErrorStat>(64);
        private const int InnerErrorWindowMs = 30000;
        private const int InnerErrorGlobalIntervalMs = 200;
        private const int InnerErrorMaxFingerprints = 256;

        /// <summary>异常指纹：同一处反复报错只算一个。</summary>
        private static string InnerErrorFingerprint(string tag, Exception ex)
        {
            string msg;
            try { msg = ex.Message ?? string.Empty; }
            catch { msg = string.Empty; }

            int cut = msg.IndexOfAny(new[] { '\r', '\n' });
            if (cut > 0) msg = msg.Substring(0, cut);
            if (msg.Length > 80) msg = msg.Substring(0, 80);

            string site = ex.TargetSite != null ? ex.TargetSite.Name : "?";

            return tag + "|" + ex.GetType().Name + "|" + site + "|" + msg;
        }

        private void LogInnerError(string tag, Exception ex)
        {
            if (ex == null) return;

            long now = Stopwatch.ElapsedMilliseconds;

            // 全局兜底节流：各不相同的异常同时爆发时，也别把主循环写死
            if (now - _lastInnerErrorLog < InnerErrorGlobalIntervalMs) return;
            _lastInnerErrorLog = now;

            string key;
            try { key = InnerErrorFingerprint(tag, ex); }
            catch { key = tag; }

            InnerErrorStat stat;
            if (!_innerErrorStats.TryGetValue(key, out stat))
            {
                if (_innerErrorStats.Count >= InnerErrorMaxFingerprints)
                    _innerErrorStats.Clear();   // 指纹太多（多半是随机内容）→ 整体重置，绝不无限增长

                stat = new InnerErrorStat { LastLogTime = 0, Suppressed = 0 };
                _innerErrorStats[key] = stat;
            }

            if (now - stat.LastLogTime < InnerErrorWindowMs)
            {
                stat.Suppressed++;
                return;
            }

            string suffix = stat.Suppressed > 0
                ? string.Format("（过去 {0} 秒内同类异常另有 {1} 次，已合并）", InnerErrorWindowMs / 1000, stat.Suppressed)
                : string.Empty;

            stat.LastLogTime = now;
            stat.Suppressed = 0;

            MessageQueue.Enqueue($"[{tag}]" + ex + suffix);
        }

        /// <summary>
        /// 把一段操作交给工作线程执行。已经在工作线程（GM 命令、脚本）时立即执行；
        /// 在 UI 线程（菜单按钮）时排队，等下一拍工作循环执行 —— 这样改 MapList/Objects 不会和主循环打架。
        /// </summary>
        public void RunOnMainThread(Action action)
        {
            if (action == null) return;

            if (_thread == null || Thread.CurrentThread == _thread)
            {
                action();
                return;
            }

            EnqueueMainThreadTask(action);
        }

        /// <summary>
        /// 无条件排队（哪怕自己就在工作线程）：用于"必须等到对象遍历之外再执行"的操作。
        /// </summary>
        private void EnqueueMainThreadTask(Action action)
        {
            if (action == null) return;

            lock (_mainThreadQueueLock)
            {
                _mainThreadQueue.Add(action);
                _hasMainThreadWork = true;
            }
        }

        private void DrainMainThreadQueue()
        {
            if (!_hasMainThreadWork) return;

            List<Action> pending;

            lock (_mainThreadQueueLock)
            {
                if (_mainThreadQueue.Count == 0)
                {
                    _hasMainThreadWork = false;
                    return;
                }

                pending = new List<Action>(_mainThreadQueue);
                _mainThreadQueue.Clear();
                _hasMainThreadWork = false;
            }

            for (int i = 0; i < pending.Count; i++)
            {
                _executingMainThreadTask = true;

                try
                {
                    pending[i]();
                }
                catch (Exception ex)
                {
                    LogInnerError("主线程任务错误", ex);
                }
                finally
                {
                    _executingMainThreadTask = false;
                }
            }
        }

        private void WorkLoop()
        {
            try
            {
                Time = Stopwatch.ElapsedMilliseconds;

                var conTime = Time;
                var saveTime = Time + Settings.SaveDelay * Settings.Minute;
                var userTime = Time + Settings.Minute * 5;
                var lineMessageTime = Time + Settings.Minute * Settings.LineMessageTimer;
                var processTime = Time + 1000;
                var startTime = Time;

                var processCount = 0;
                var processRealCount = 0;

                LinkedListNode<MapObject> current = null;

                if (Settings.Multithreaded)
                {
                    for (var j = 0; j < MobThreads.Length; j++)
                    {
                        MobThreads[j] = new MobThread();
                        MobThreads[j].Id = j;
                    }
                }

                StartEnvir();
                var canstartserver = CanStartEnvir();
                if (canstartserver != "true")
                {
                    MessageQueue.Enqueue(canstartserver);
                    StopEnvir();
                    _thread = null;
                    Stop();
                    return;
                }

                if (Settings.Multithreaded)
                {
                    for (var j = 0; j < MobThreads.Length; j++)
                    {
                        var Info = MobThreads[j];
                        if (j <= 0) continue;
                        MobThreading[j] = new Thread(() => ThreadLoop(Info)) { IsBackground = true };
                        MobThreading[j].Start();
                    }
                }

                StartNetwork();
                if (Settings.StartHTTPService || Settings.EnableResourceService)
                {
                    http = new HttpServer();
                    http.Start();

                    if (Settings.EnableResourceService)
                    {
                        // 监听是另起线程去做的，而且要依次试探 http.sys / 纯Socket 两条通道，
                        // 所以这里等一下「监听结果已确定」，免得日志里报的是配置值、
                        // 实际生效的却是另一个地址（排查「到底听在哪」时这点很关键）。
                        bool settled = http.WaitListening(8000);

                        if (!settled)
                        {
                            MessageQueue.Instance.Enqueue("微端资源服务 启动中，稍后请看 Http服务器 的日志确认监听地址" +
                                " (资源目录: " + Path.GetFullPath(Settings.ResourcePath) + ")");
                        }
                        else if (string.IsNullOrEmpty(http.ActivePrefix))
                        {
                            MessageQueue.Instance.Enqueue("微端资源服务 未能开启，客户端将无法下载缺失资源。" +
                                " (资源目录: " + Path.GetFullPath(Settings.ResourcePath) + ")");
                        }
                        else
                        {
                            MessageQueue.Instance.Enqueue("微端资源服务 已开启: " + http.ActivePrefix +
                                " [" + http.Transport + "]" +
                                " (资源目录: " + Path.GetFullPath(Settings.ResourcePath) + ")");

                            // 说清「HTTP 服务」开关到底管什么：关了它，注册/改名/广播等管理接口
                            // 一律 404，这个监听只为微端下发资源而存在。避免「我明明关了却还在跑」的误会。
                            if (!Settings.StartHTTPService)
                            {
                                MessageQueue.Instance.Enqueue("  注意: 「启用HTTP服务」为关闭状态，" +
                                    "此监听仅供微端下发资源（/resource、/resindex）；" +
                                    "注册账号/改名/广播等 HTTP 接口已停用。");
                            }

                            var lanAddresses = HttpService.GetLanIPv4();
                            if (lanAddresses.Count > 0)
                            {
                                int httpPort = HttpService.GetPortFromPrefix(http.ActivePrefix);
                                MessageQueue.Instance.Enqueue("  客户端 Mir2Config.ini 请填: Host=http://" +
                                    lanAddresses[0] + ":" + httpPort + "/");
                            }

                            // 「为什么监听地址和配置的不一样」必答：把通道降级的原因一起打出来
                            if (!string.IsNullOrEmpty(http.ListenReport))
                            {
                                MessageQueue.Instance.Enqueue(http.ListenReport);
                            }
                        }
                    }
                }
                bool innerRestart;   //内循环兜底：出错不踢人不停服，记日志后续跑下一轮
                do
                {
                    innerRestart = false;
                    try
                    {
                    while (Running)
                    {
                        Time = Stopwatch.ElapsedMilliseconds;

                        // 处理 UI 线程投递过来的任务（如「重载地图」）：必须在工作线程里改 MapList
                        DrainMainThreadQueue();

                        if (Time >= processTime)
                        {
                            LastCount = processCount;
                            LastRealCount = processRealCount;
                            processCount = 0;
                            processRealCount = 0;
                            processTime = Time + 1000;
                        }

                        if (conTime != Time)
                        {
                            conTime = Time;

                            AdjustLights();

                            lock (Connections)
                            {
                                for (var i = Connections.Count - 1; i >= 0; i--)
                                {
                                    Connections[i].Process();
                                }
                            }

                            lock (StatusConnections)
                            {
                                for (var i = StatusConnections.Count - 1; i >= 0; i--)
                                {
                                    StatusConnections[i].Process();
                                }
                            }
                        }


                        if (current == null)
                            current = Objects.First;

                        if (current == Objects.First)
                        {
                            LastRunTime = Time - startTime;
                            startTime = Time;
                        }

                        if (Settings.Multithreaded)
                        {
                            for (var j = 1; j < MobThreads.Length; j++)
                            {
                                var Info = MobThreads[j];

                                if (!Info.Stop) continue;
                                Info.EndTime = Time + 10;
                                Info.Stop = false;
                            }
                            lock (_locker)
                            {
                                Monitor.PulseAll(_locker); //changing a blocking condition. (this makes the threads wake up!)
                            }
                            //run the first loop in the main thread so the main thread automaticaly 'halts' until the other threads are finished
                            ThreadLoop(MobThreads[0]);
                        }

                        var TheEnd = false;
                        var Start = Stopwatch.ElapsedMilliseconds;
                        while (!TheEnd && Stopwatch.ElapsedMilliseconds - Start < 20)
                        {
                            if (current == null)
                            {
                                TheEnd = true;
                                break;
                            }

                            var next = current.Next;
                            if (!Settings.Multithreaded || current.Value.Race != ObjectType.Monster || current.Value.Master != null)
                            {
                                if (Time > current.Value.OperateTime)
                                {
                                    try
                                    {
                                        current.Value.Process();
                                    }
                                    catch (Exception ex)
                                    {
                                        //单个对象出错只记日志，不影响同拍其它对象，也不打断主循环
                                        LogInnerError("对象处理错误", ex);
                                    }
                                    finally
                                    {
                                        //无论成败都推进节拍，避免同一坏对象下一帧立刻再炸
                                        try { current.Value.SetOperateTime(); } catch { }
                                    }
                                }
                                processCount++;
                            }
                            current = next;
                        }

                        for (var i = 0; i < MapList.Count; i++)
                        {
                            try { MapList[i].Process(); }
                            catch (Exception ex) { LogInnerError("地图处理错误", ex); }
                        }

                        if (DragonSystem != null)
                        {
                            try { DragonSystem.Process(); }
                            catch (Exception ex) { LogInnerError("龙系统处理错误", ex); }
                        }

                        try
                        {
                            Process();
                        }
                        catch (Exception ex)
                        {
                            LogInnerError("系统处理错误", ex);
                        }

                        // HTTP 管理接口的写操作（存档 / 踢人）统一在工作线程落地（10-30）
                        try
                        {
                            ProcessHttpRequests();
                        }
                        catch (Exception ex)
                        {
                            LogInnerError("HTTP管理请求处理错误", ex);
                        }

                        if (Time >= saveTime)
                        {
                            saveTime = Time + Settings.SaveDelay * Settings.Minute;
                            BeginSaveAccounts();
                            SaveGuilds();
                            SaveGoods();
                            SaveConquests();
                        }

                        if (Time >= userTime)
                        {
                            userTime = Time + Settings.Minute * 5;
                            Broadcast(new S.Chat
                            {
                                Message = string.Format(GameLanguage.OnlinePlayers, Players.Count),
                                Type = ChatType.Hint
                            });
                        }

                        if (LineMessages.Count > 0 && Time >= lineMessageTime)
                        {
                            lineMessageTime = Time + Settings.Minute * Settings.LineMessageTimer;
                            Broadcast(new S.Chat
                            {
                                Message = LineMessages[Random.Next(LineMessages.Count)],
                                Type = ChatType.LineMessage
                            });
                        }

                        //   if (Players.Count == 0) Thread.Sleep(1);
                        //   GC.Collect();
                    }
                    }
                    catch (Exception ex)
                    {
                        //兜底：漏网的异常只记日志，不踢人、不停服，直接续跑下一轮。
                        //★原版「踢全服+停服」逻辑注释保留，需要恢复原行为时取消注释并删掉 innerRestart = true 即可
                        //lock (Connections)
                        //{
                        //    for (var i = Connections.Count - 1; i >= 0; i--)
                        //        Connections[i].SendDisconnect(3);
                        //}

                        LogInnerError("内循环错误(已忽略·继续运行)", ex);
                        innerRestart = true;
                    }
                } while (innerRestart && Running);

                StopNetwork();
                StopEnvir();
                SaveAccounts();
                SaveGuilds(true);
                SaveConquests(true);
            }
            catch (Exception ex)
            {
                // Get stack trace for the exception with source file information
                var st = new StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();

                MessageQueue.Enqueue($"[外循环错误 线程 {line}]" + ex);
            }

            _thread = null;
        }

        private void ThreadLoop(MobThread Info)
        {
            Info.Stop = false;

            try
            {

                var stopping = false;
                if (Info._current == null)
                    Info._current = Info.ObjectsList.First;
                stopping = Info._current == null;

                while (Running)
                {
                    if (Info._current == null)
                        Info._current = Info.ObjectsList.First;
                    else
                    {
                        var next = Info._current.Next;

                        //if we reach the end of our list > go back to the top (since we are running threaded, we dont want the system to sit there for xxms doing nothing)
                        if (Info._current == Info.ObjectsList.Last)
                        {
                            next = Info.ObjectsList.First;
                            Info.LastRunTime = (Info.LastRunTime + (Time - Info.StartTime)) / 2;
                            //Info.LastRunTime = (Time - Info.StartTime) /*> 0 ? (Time - Info.StartTime) : Info.LastRunTime */;
                            Info.StartTime = Time;
                        }
                        if (Time > Info._current.Value.OperateTime)
                        {
                            if (Info._current.Value.Master == null) //since we are running multithreaded, dont allow pets to be processed (unless you constantly move pets into their map appropriate thead)
                            {
                                try
                                {
                                    Info._current.Value.Process();
                                }
                                catch (Exception ex)
                                {
                                    //单只怪/玩家出错只记日志，不终止本处理线程
                                    LogInnerError("怪物线程处理错误", ex);
                                }
                                finally
                                {
                                    //无论成败都推进节拍，避免同一坏对象每帧重炸
                                    try { Info._current.Value.SetOperateTime(); } catch { }
                                }
                            }
                        }
                        Info._current = next;
                    }

                    //if it's the main thread > make it loop till the subthreads are done, else make it stop after 'endtime'
                    if (Info.Id == 0)
                    {
                        stopping = true;
                        for (var x = 1; x < MobThreads.Length; x++)
                        {
                            if (MobThreads[x].Stop == false)
                            {
                                stopping = false;
                            }
                        }
                        if (!stopping) continue;
                        Info.Stop = stopping;
                        return;
                    }

                    if (Stopwatch.ElapsedMilliseconds <= Info.EndTime || !Running) continue;
                    Info.Stop = true;
                    lock (_locker)
                    {
                        while (Info.Stop) Monitor.Wait(_locker);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is ThreadInterruptedException) return;

                MessageQueue.Enqueue($"[数据循环处理出现错误]" + ex);
            }
        }

        private void AdjustLights()
        {
            var oldLights = Lights;

            var hours = Now.Hour * 2 % 24;
            if (hours == 6 || hours == 7)
                Lights = LightSetting.Dawn;
            else if (hours >= 8 && hours <= 15)
                Lights = LightSetting.Day;
            else if (hours == 16 || hours == 17)
                Lights = LightSetting.Evening;
            else
                Lights = LightSetting.Night;

            if (oldLights == Lights) return;

            Broadcast(new S.TimeOfDay { Lights = Lights });
        }

        public void Process()
        {
            if (Now.Day != dailyTime)
            {
                dailyTime = Now.Day;
                ProcessNewDay();
            }

            if (Time >= warTime)
            {
                warTime = Time + Settings.Minute;
                for (var i = GuildsAtWar.Count - 1; i >= 0; i--)
                {
                    GuildsAtWar[i].TimeRemaining -= Settings.Minute;

                    if (GuildsAtWar[i].TimeRemaining >= 0) continue;
                    GuildsAtWar[i].EndWar();
                    GuildsAtWar.RemoveAt(i);
                }
            }

            if (Time >= guildTime)
            {
                guildTime = Time + Settings.Minute;
                for (var i = 0; i < Guilds.Count; i++)
                {
                    Guilds[i].Process();
                }
            }

            if (Time >= conquestTime)
            {
                conquestTime = Time + Settings.Second * 10;
                for (var i = 0; i < Conquests.Count; i++)
                {
                    Conquests[i].Process();
                }
            }

            if (Time >= rentalItemsTime)
            {
                rentalItemsTime = Time + Settings.Minute * 5;
                ProcessRentedItems();
            }

            if (Time >= auctionTime)
            {
                auctionTime = Time + Settings.Minute * 10;
                ProcessAuction();
            }

            if (Time >= spawnTime)
            {
                spawnTime = Time + Settings.Second * 10;
                Main.RespawnTick.Process();
            }

            if (Time >= robotTime)
            {
                robotTime = Time + Settings.Minute;
                Robot.Process(RobotNPC);
            }

            if (Time >= timerTime)
            {
                timerTime = Time + Settings.Second;

                string[] keys = Timers.Keys.ToArray();

                foreach (var key in keys)
                {
                    if (Timers[key].RelativeTime <= Time)
                    {
                        Timers.Remove(key);
                    }
                }
            }

            //离线挂机总开关（10-26）：后台把「允许离线挂机」关掉的那一刻，
            //  已经在挂机的角色立刻走完整下线。放在这里而不是 PlayerObject.Process 里——
            //  此时已脱离地图对象遍历，StopGame 拆对象是安全的（与假人巡检同一位置）。
            if (_offlineAfkWasEnabled != Settings.OfflineAfkEnabled)
            {
                _offlineAfkWasEnabled = Settings.OfflineAfkEnabled;

                if (!Settings.OfflineAfkEnabled)
                    ProcessOfflineAfkShutdown();
            }

            //假人系统：随机上下线巡检（内部自带 1 秒节流）
            FakePlayerManager.Process();

            //内存治理（20261106）：定期全堆压缩，把 GC 缓存的空闲段还给系统。
            //MemCompactMinutes = 0 时完全不干预（默认关）。
            if (Settings.MemCompactMinutes > 0 && Time >= _nextMemCompact)
            {
                _nextMemCompact = Time + (long)Settings.MemCompactMinutes * Settings.Minute;
                CompactHeaps(true);
            }
        }

        private long _nextMemCompact;

        /// <summary>
        /// 全堆压缩（20261106 内存治理）：LOH Compact + 带压缩的 Full Blocking GC。
        /// 2 万假人建档、日志峰值这类「冲高后留下一堆空闲段」的场景，压缩后
        /// 提交内存（任务管理器看到的那一项）会明显回落。代价是压缩瞬间一次停顿。
        /// </summary>
        /// <param name="log">true = 把前后差值写进日志，方便观察省了多少</param>
        public static void CompactHeaps(bool log)
        {
            long before = GC.GetTotalMemory(false);
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            long commitBefore = proc.PrivateMemorySize64;

            System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
                System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Forced, true, true);

            if (log)
                MessageQueue.Enqueue(string.Format(
                    "[内存] 已全堆压缩：托管堆 {0:N0}→{1:N0} MB，提交内存 {2:N0}→{3:N0} MB",
                    before >> 20, GC.GetTotalMemory(false) >> 20,
                    commitBefore >> 20, System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 >> 20));
        }

        /// <summary>
        /// 开服内存分布报告（20261106）：只读不改，把每一块数据的规模打进日志，
        /// 用来回答「内存到底花在哪」——先看报告再决定砍哪里，别凭感觉猜。
        /// 在 StartEnvir 末尾调用（此时地图/物品库/假人档案都已就绪，网络未开）。
        /// </summary>
        public void LogMemoryReport()
        {
            try
            {
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                GCMemoryInfo gcInfo = GC.GetGCMemoryInfo();

                MessageQueue.Enqueue("──── 内存分布报告（开服完成）────");
                MessageQueue.Enqueue(string.Format(
                    "工作集 {0:N0} MB｜私有内存 {1:N0} MB｜托管堆 {2:N0} MB｜提交内存 {3:N0} MB（含 GC 预留，可被 [Memory] CompactMinutes 压缩回收）",
                    proc.WorkingSet64 >> 20, proc.PrivateMemorySize64 >> 20,
                    GC.GetTotalMemory(false) >> 20, gcInfo.TotalCommittedBytes >> 20));

                long mapCells = 0;
                foreach (Map map in MapList)
                {
                    if (map?.Cells == null) continue;
                    mapCells += (long)map.Cells.GetLength(0) * map.Cells.GetLength(1);
                }

                long dropCount = 0;
                foreach (MonsterInfo mi in MonsterInfoList)
                    dropCount += mi.Drops != null ? mi.Drops.Count : 0;

                MessageQueue.Enqueue(string.Format(
                    "地图 {0} 张（{1:N0} 万格，格子是类对象，粗估 {2:N0} MB）｜怪物 {3} 只（掉落表 {4:N0} 条）",
                    MapList.Count, mapCells / 10000.0, mapCells * 40L >> 20,
                    MonsterInfoList.Count, dropCount));

                MessageQueue.Enqueue(string.Format(
                    "物品库 {0}｜技能库 {1}｜任务 {2}｜NPC {3}（脚本 {4} 个）｜行会 {5}｜账号 {6}｜角色 {7}",
                    ItemInfoList.Count, MagicInfoList.Count, QuestInfoList.Count,
                    NPCInfoList.Count, Scripts.Count, GuildList.Count,
                    AccountList.Count, CharacterList.Count));

                MessageQueue.Enqueue(string.Format(
                    "假人档案 {0} 个（每个 = 账号 + 角色档案 + 背包装备，粗估 {1:N0} MB；上线峰值另计在线对象）",
                    FakePlayerManager.RecordCount,
                    (long)FakePlayerManager.RecordCount * 5 / 1024));
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue("[内存报告] 生成失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 关掉「允许离线挂机」后收回所有挂机中的角色（10-26）。
        /// 只在 Envir.Process 里调用——此处已脱离地图对象遍历，StopGame 拆对象是安全的。
        /// 倒序遍历：TerminateOfflineAFK → StopGame 可能把角色从 Players 里移除。
        /// </summary>
        public void ProcessOfflineAfkShutdown()
        {
            int cutoff = 0;

            for (int i = Players.Count - 1; i >= 0; i--)
            {
                if (i >= Players.Count) continue;

                PlayerObject player = Players[i];
                if (player == null || !player.OfflineAFK) continue;

                player.TerminateOfflineAFK();
                cutoff++;
            }

            if (cutoff > 0)
                MessageQueue.Enqueue($"[离线挂机] 服务端已禁用离线挂机，收回 {cutoff} 个挂机中的角色");
        }

        private void ProcessAuction()
        {
            LinkedListNode<AuctionInfo> current = Auctions.First;

            while (current != null)
            {
                AuctionInfo info = current.Value;

                if (!info.Expired && !info.Sold && Now >= info.ConsignmentDate.AddDays(Globals.ConsignmentLength))
                {
                    if (info.ItemType == MarketItemType.Auction && info.CurrentBid > info.Price)
                    {
                        string message = string.Format("你以 {1:#,##0} 金币赢得了 {0}", info.Item.FriendlyName, info.CurrentBid);

                        info.Sold = true;
                        MailCharacter(info.CurrentBuyerInfo, item: info.Item, customMessage: message);

                        MessageAccount(info.CurrentBuyerInfo.AccountInfo, string.Format("你以 {1:#,##0} 金币购买了 {0}", info.Item.FriendlyName, info.CurrentBid), ChatType.Hint);
                        MessageAccount(info.SellerInfo.AccountInfo, string.Format("你以 {1:#,##0} 金币卖出了 {0}", info.Item.FriendlyName, info.CurrentBid), ChatType.Hint);
                    }
                    else
                    {
                        info.Expired = true;
                    }
                }

                current = current.Next;
            }
        }

        public void Broadcast(Packet p)
        {
            for (var i = 0; i < Players.Count; i++) Players[i].Enqueue(p);
        }

        public void RequiresBaseStatUpdate()
        {
            for (var i = 0; i < Players.Count; i++) Players[i].HasUpdatedBaseStats = false;
        }

        public void SaveDB()
        {
            using (var stream = File.Create(DatabasePath))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(CustomVersion);
                writer.Write(MapIndex);
                writer.Write(ItemIndex);
                writer.Write(MonsterIndex);
                writer.Write(NPCIndex);
                writer.Write(QuestIndex);
                writer.Write(GameshopIndex);
                writer.Write(ConquestIndex);
                writer.Write(RespawnIndex);

                writer.Write(MapInfoList.Count);
                for (var i = 0; i < MapInfoList.Count; i++)
                    MapInfoList[i].Save(writer);

                writer.Write(ItemInfoList.Count);
                for (var i = 0; i < ItemInfoList.Count; i++)
                    ItemInfoList[i].Save(writer);

                writer.Write(MonsterInfoList.Count);
                for (var i = 0; i < MonsterInfoList.Count; i++)
                    MonsterInfoList[i].Save(writer);

                writer.Write(NPCInfoList.Count);
                for (var i = 0; i < NPCInfoList.Count; i++)
                    NPCInfoList[i].Save(writer);

                writer.Write(QuestInfoList.Count);
                for (var i = 0; i < QuestInfoList.Count; i++)
                    QuestInfoList[i].Save(writer);

                DragonInfo.Save(writer);
                writer.Write(MagicInfoList.Count);
                for (var i = 0; i < MagicInfoList.Count; i++)
                    MagicInfoList[i].Save(writer);

                writer.Write(GameShopList.Count);
                for (var i = 0; i < GameShopList.Count; i++)
                    GameShopList[i].Save(writer);

                writer.Write(ConquestInfoList.Count);
                for (var i = 0; i < ConquestInfoList.Count; i++)
                    ConquestInfoList[i].Save(writer);

                RespawnTick.Save(writer);
            }
        }


        //★11-06 归档安全化（6 个暗坑，备份见 Back Up\manual-20261106_2110-归档暗坑修复）：
        //  1. 自动归档幂等 —— 已归档过的角色不再写第二份（原来账号没立刻落盘，每次启动都重归档一次，实测留下 2~3 份）；
        //  2. 查询不再用 "{name}*.MirCA" 前缀通配 —— "张三" 会命中 "张三丰_…"；
        //     改成「去掉时间戳后缀后与角色名完全相等」；
        //  3. 同一角色有多份归档时取【最新一份】，不再 files.Length != 1 就返回 null
        //     （原来多份 = 永久恢复不了，现网 4 个角色就是这么废掉的）；
        //  4. Archive 目录不存在时返回空列表，不抛 DirectoryNotFoundException；
        //  5. 文件名时间戳改用【本地时间】（Envir.Now 是 UTC，运维看到的时间比实际早 8 小时）；
        //  6. 恢复成功后归档文件挪进 Archive\Restored\，避免同一份被反复恢复、也避免下次自动归档被"已存在"挡住。
        public static readonly string ArchiveRestoredPath = Path.Combine(ArchivePath, "Restored");

        private const string ArchiveExtension = ".MirCA";

        /// <summary>归档文件名里的时间戳格式。前导下划线是格式串的一部分，会原样输出成 "{角色名}_{MMddyyyy_HHmmss}.MirCA"。</summary>
        private const string ArchiveStampFormat = "_MMddyyyy_HHmmss";

        /// <summary>
        /// 时间戳后缀：8 位日期 + 下划线 + 6 位时间，可选再跟 "_序号"（同一秒内重复备份时才出现）。
        /// 前导下划线可选，兼容早期无分隔符的文件名。
        /// </summary>
        private static readonly Regex ArchiveStampRegex = new Regex(@"^_?\d{8}_\d{6}(_\d{1,3})?$", RegexOptions.Compiled);

        /// <summary>从文件名里提取时间戳值，用于排序（前导下划线可有可无，兼容早期无分隔符的命名）。</summary>
        private static readonly Regex ArchiveStampValueRegex = new Regex(@"(\d{8})_(\d{6})(?:_\d{1,3})?$", RegexOptions.Compiled);

        /// <summary>从文件名里抠出时间戳（只当排序次序用，解析失败给 MinValue）。</summary>
        private static DateTime ArchiveStampOf(FileInfo file)
        {
            var m = ArchiveStampValueRegex.Match(Path.GetFileNameWithoutExtension(file.Name));

            if (!m.Success) return DateTime.MinValue;

            return DateTime.TryParseExact($"{m.Groups[1].Value}_{m.Groups[2].Value}", "MMddyyyy_HHmmss",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var t)
                ? t
                : DateTime.MinValue;
        }

        /// <summary>判断某个归档文件名（不含扩展名）是不是 name 这个角色的。</summary>
        private static bool IsArchiveOf(string fileBase, string name)        {
            if (string.IsNullOrEmpty(fileBase) || string.IsNullOrEmpty(name)) return false;

            if (fileBase.Length <= name.Length) return false;
            if (!fileBase.StartsWith(name, StringComparison.OrdinalIgnoreCase)) return false;

            // 剩余部分必须整段是时间戳，才算这个角色的归档
            return ArchiveStampRegex.IsMatch(fileBase.Substring(name.Length));
        }

        /// <summary>
        /// 列出某角色的全部归档文件，按文件写入时间【新 → 旧】排序。
        /// name 也可以直接写完整文件名（带不带 .MirCA 都行），此时只返回这一个文件。
        /// 目录不存在时返回空列表。
        /// </summary>
        public List<FileInfo> GetArchivedCharacterFiles(string name)
        {
            var result = new List<FileInfo>();

            if (string.IsNullOrWhiteSpace(name)) return result;
            if (!Directory.Exists(ArchivePath)) return result;   //★原来没有这一句，目录不在直接抛异常

            name = name.Trim();

            // GM 直接贴完整文件名也能用（旧提示语一直写着「请尝试指定完整的存档文件名」，但代码从来没实现过）
            if (name.EndsWith(ArchiveExtension, StringComparison.OrdinalIgnoreCase))
            {
                var exact = Path.Combine(ArchivePath, name);
                if (File.Exists(exact)) result.Add(new FileInfo(exact));
                return result;
            }

            try
            {
                foreach (var file in new DirectoryInfo(ArchivePath).GetFiles("*" + ArchiveExtension))
                {
                    if (IsArchiveOf(Path.GetFileNameWithoutExtension(file.Name), name))
                        result.Add(file);
                }
            }
            catch (Exception ex)
            {
                // 权限 / 磁盘 / 路径异常都不该让归档功能连带把开服或 GM 命令弄挂
                MessageQueue.Enqueue($"扫描归档目录失败：{ex.Message}");
                return result;
            }

            //★排序键是【文件名里的时间戳】，不是 mtime：
            //  整目录复制 / 解压 / 同步工具刷时间之后，同一角色的几份档 mtime 可能全一样（甚至乱序），
            //  按 mtime 排会随机挑中一个更老的档。文件名时间戳才是归档发生的真实顺序。
            //  老档是 UTC、新档是本地时间，混排也不会错：新档真实时间更晚，且本地时间还比 UTC 多 8 小时。
            //  mtime / 文件名只作为时间戳相同（同秒备份）时的次序。
            result.Sort((a, b) =>
            {
                int c = ArchiveStampOf(b).CompareTo(ArchiveStampOf(a));
                if (c != 0) return c;

                c = b.LastWriteTime.CompareTo(a.LastWriteTime);
                if (c != 0) return c;

                return string.CompareOrdinal(b.Name, a.Name);
            });

            return result;
        }

        public CharacterInfo GetArchivedCharacter(string name)
        {
            return GetArchivedCharacter(name, out _);
        }

        /// <summary>读取某角色的归档；<paramref name="file"/> 回传实际用到的文件（恢复成功后要挪走它）。</summary>
        public CharacterInfo GetArchivedCharacter(string name, out FileInfo file)
        {
            file = null;

            var files = GetArchivedCharacterFiles(name);

            if (files.Count == 0) return null;

            //★同一角色有多份归档时取最新一份。原来 files.Length != 1 直接返回 null —— 一旦重复归档就再也恢复不了。
            //  某一份损坏（写一半断电、被占用）就顺延试更早的一份，而不是整体失败。
            foreach (var candidate in files)
            {
                try
                {
                    using var stream = candidate.OpenRead();
                    using var reader = new BinaryReader(stream);

                    var version = reader.ReadInt32();
                    var customVersion = reader.ReadInt32();

                    var info = new CharacterInfo(reader, version, customVersion);

                    file = candidate;   //★读成功才回传，避免把坏档挪进 Restored
                    return info;
                }
                catch (Exception ex)
                {
                    // 单个归档文件损坏不该把 GM 命令或启动流程带崩
                    MessageQueue.Enqueue($"归档文件 {candidate.Name} 读取失败（{ex.Message}），尝试更早的一份");
                }
            }

            return null;
        }

        /// <summary>写一份新的归档文件（GM 手动备份用，总是新建）。返回写出的文件名。</summary>
        public string SaveArchivedCharacter(CharacterInfo info)
        {
            if (!Directory.Exists(ArchivePath)) Directory.CreateDirectory(ArchivePath);

            //★本地时间：Envir.Now 是 UTC，直接落进文件名会让运维看到的时间比实际早 8 小时
            string stamp = Now.ToLocalTime().ToString(ArchiveStampFormat);
            string fileName = $"{info.Name}{stamp}{ArchiveExtension}";
            string path = Path.Combine(ArchivePath, fileName);

            // 同一秒内重复（GM 连点 BACKUPPLAYER）时补序号，别覆盖上一份。
            //★序号必须还在时间戳格式里（…_HHmmss_2），否则 IsArchiveOf 认不出来 —— 写出去的档自己读不回来。
            if (File.Exists(path))
            {
                int seq = 1;
                do
                {
                    seq++;
                    fileName = $"{info.Name}{stamp}_{seq}{ArchiveExtension}";
                    path = Path.Combine(ArchivePath, fileName);
                } while (File.Exists(path));
            }

            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(CustomVersion);

                info.Save(writer);
            }

            return fileName;
        }

        /// <summary>
        /// 自动归档（未登录 / 已删除等条件触发），★幂等：已经存在该角色的归档就不再写第二份。
        /// 返回 true 表示本次真的新写了一份。
        /// </summary>
        public bool ArchiveCharacterOnce(CharacterInfo info)
        {
            //★写归档失败（磁盘满 / 文件被占用 / 名字含非法字符）绝不能让开服失败：
            //  这里在 AccountInfo 的构造函数里被调用，一抛就是整个账号库读不完 → 服起不来。
            try
            {
                var existing = GetArchivedCharacterFiles(info.Name);

                if (existing.Count > 0)
                {
                    MessageQueue.Enqueue($"角色 {info.Name} 之前已归档过（{existing[0].Name}），跳过重复归档");
                    return false;
                }

                SaveArchivedCharacter(info);
                return true;
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue($"角色 {info.Name} 归档失败（{ex.Message}），本次未落档，请检查 Archive 目录");
                return false;
            }
        }

        /// <summary>
        /// 把归档文件挪进 Archive\Restored\（恢复成功后调用）。挪动而不是删除，出错时还能找回。
        /// </summary>
        public bool MoveArchiveToRestored(FileInfo file, out string error)
        {
            error = null;

            if (file == null || !file.Exists) return false;

            try
            {
                if (!Directory.Exists(ArchiveRestoredPath)) Directory.CreateDirectory(ArchiveRestoredPath);

                string dest = Path.Combine(ArchiveRestoredPath, file.Name);

                if (File.Exists(dest))
                    dest = Path.Combine(ArchiveRestoredPath,
                        $"{Path.GetFileNameWithoutExtension(file.Name)}_{Guid.NewGuid():N}{ArchiveExtension}");

                File.Move(file.FullName, dest);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public void SaveAccounts()
        {
            while (Saving)
                Thread.Sleep(1);

            try
            {
                using (var stream = File.Create(AccountPath + "n"))
                    SaveAccounts(stream);
                if (File.Exists(AccountPath))
                    File.Move(AccountPath, AccountPath + "o");
                File.Move(AccountPath + "n", AccountPath);
                if (File.Exists(AccountPath + "o"))
                    File.Delete(AccountPath + "o");
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue(ex);
            }
        }

        private void SaveAccounts(Stream stream)
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(CustomVersion);
                writer.Write(NextAccountID);
                writer.Write(NextCharacterID);
                writer.Write(NextUserItemID);
                writer.Write(NextHeroID);

                writer.Write(GuildList.Count);
                writer.Write(NextGuildID);
                writer.Write(HeroList.Count);
                for (var i = 0; i < HeroList.Count; i++)
                    HeroList[i].Save(writer);
                writer.Write(AccountList.Count);
                for (var i = 0; i < AccountList.Count; i++)
                    AccountList[i].Save(writer);

                writer.Write(NextAuctionID);
                writer.Write(Auctions.Count);
                foreach (var auction in Auctions)
                    auction.Save(writer);

                writer.Write(NextMailID);

                writer.Write(GameshopLog.Count);
                foreach (var item in GameshopLog)
                {
                    writer.Write(item.Key);
                    writer.Write(item.Value);
                }

                writer.Write(SavedSpawns.Count);
                foreach (var Spawn in SavedSpawns)
                {
                    var Save = new RespawnSave { RespawnIndex = Spawn.Info.RespawnIndex, NextSpawnTick = Spawn.NextSpawnTick, Spawned = Spawn.Count >= Spawn.Info.Count * SpawnMultiplier };
                    Save.Save(writer);
                }
            }
        }

        private void SaveGuilds(bool forced = false)
        {
            if (!Directory.Exists(Settings.GuildPath)) Directory.CreateDirectory(Settings.GuildPath);

            if (GuildRefreshNeeded == true) //deletes guild files and resaves with new indexing if a guild is deleted.
            {
                foreach (var guildfile in Directory.GetFiles(Settings.GuildPath, "*.mgd"))
                {
                    File.Delete(guildfile);
                }

                GuildRefreshNeeded = false;
                forced = true; //triggers a full resave of all guilds
            }

            for (var i = 0; i < GuildList.Count; i++)
            {
                if (GuildList[i].NeedSave || forced)
                {
                    GuildList[i].NeedSave = false;
                    var mStream = new MemoryStream();
                    var writer = new BinaryWriter(mStream);
                    GuildList[i].Save(writer);
                    var fStream = new FileStream(Path.Combine(Settings.GuildPath, i + ".mgdn"), FileMode.Create);
                    var data = mStream.ToArray();
                    fStream.BeginWrite(data, 0, data.Length, EndSaveGuildsAsync, fStream);
                }
            }
        }
        private void EndSaveGuildsAsync(IAsyncResult result)
        {
            var fStream = result.AsyncState as FileStream;
            try
            {
                if (fStream == null) return;
                var oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                var newfilename = fStream.Name;
                fStream.EndWrite(result);
                fStream.Dispose();
                if (File.Exists(oldfilename))
                    File.Move(oldfilename, oldfilename + "o");
                File.Move(newfilename, oldfilename);
                if (File.Exists(oldfilename + "o"))
                    File.Delete(oldfilename + "o");
            }
            catch (Exception)
            {
            }
        }

        private void SaveGoods(bool forced = false)
        {
            if (!Directory.Exists(Settings.GoodsPath)) Directory.CreateDirectory(Settings.GoodsPath);

            for (var i = 0; i < MapList.Count; i++)
            {
                var map = MapList[i];

                if (map.NPCs.Count == 0) continue;

                for (var j = 0; j < map.NPCs.Count; j++)
                {
                    var npc = map.NPCs[j];

                    if (forced)
                    {
                        npc.ProcessGoods(forced);
                    }

                    if (!npc.NeedSave) continue;

                    var path = Path.Combine(Settings.GoodsPath, npc.Info.Index + ".msdn");

                    var mStream = new MemoryStream();
                    var writer = new BinaryWriter(mStream);
                    var Temp = 9999;
                    writer.Write(Temp);
                    writer.Write(Version);
                    writer.Write(CustomVersion);
                    writer.Write(npc.UsedGoods.Count);

                    for (var k = 0; k < npc.UsedGoods.Count; k++)
                    {
                        npc.UsedGoods[k].Save(writer);
                    }

                    var fStream = new FileStream(path, FileMode.Create);
                    var data = mStream.ToArray();
                    fStream.BeginWrite(data, 0, data.Length, EndSaveGoodsAsync, fStream);
                }
            }
        }
        private void EndSaveGoodsAsync(IAsyncResult result)
        {
            try
            {
                var fStream = result.AsyncState as FileStream;
                if (fStream == null) return;
                var oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                var newfilename = fStream.Name;
                fStream.EndWrite(result);
                fStream.Dispose();
                if (File.Exists(oldfilename))
                    File.Move(oldfilename, oldfilename + "o");
                File.Move(newfilename, oldfilename);
                if (File.Exists(oldfilename + "o"))
                    File.Delete(oldfilename + "o");
            }
            catch (Exception)
            {
            }
        }

        private void SaveConquests(bool forced = false)
        {
            if (!Directory.Exists(Settings.ConquestsPath)) Directory.CreateDirectory(Settings.ConquestsPath);
            for (var i = 0; i < ConquestList.Count; i++)
            {
                if (!ConquestList[i].NeedSave && !forced) continue;
                ConquestList[i].NeedSave = false;
                var mStream = new MemoryStream();
                var writer = new BinaryWriter(mStream);
                ConquestList[i].Save(writer);
                var fStream = new FileStream(Path.Combine(Settings.ConquestsPath, ConquestList[i].Info.Index + ".mcdn"), FileMode.Create);
                var data = mStream.ToArray();
                fStream.BeginWrite(data, 0, data.Length, EndSaveConquestsAsync, fStream);
            }
        }
        private void EndSaveConquestsAsync(IAsyncResult result)
        {
            var fStream = result.AsyncState as FileStream;
            try
            {
                if (fStream == null) return;
                var oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                var newfilename = fStream.Name;
                fStream.EndWrite(result);
                fStream.Dispose();
                if (File.Exists(oldfilename))
                    File.Move(oldfilename, oldfilename + "o");
                File.Move(newfilename, oldfilename);
                if (File.Exists(oldfilename + "o"))
                    File.Delete(oldfilename + "o");
            }
            catch (Exception)
            {

            }
        }

        public void BeginSaveAccounts()
        {
            if (Saving) return;

            Saving = true;


            using (var mStream = new MemoryStream())
            {
                if (File.Exists(AccountPath))
                {
                    if (!Directory.Exists(AccountsBackUpPath)) Directory.CreateDirectory(AccountsBackUpPath);
                    var fileName =
                        $"Accounts {Now.Year:0000}-{Now.Month:00}-{Now.Day:00} {Now.Hour:00}-{Now.Minute:00}-{Now.Second:00}.bak";
                    if (File.Exists(Path.Combine(AccountsBackUpPath, fileName))) File.Delete(Path.Combine(AccountsBackUpPath, fileName));
                    File.Move(AccountPath, Path.Combine(AccountsBackUpPath, fileName));
                }

                SaveAccounts(mStream);
                var fStream = new FileStream(AccountPath + "n", FileMode.Create);

                var data = mStream.ToArray();
                fStream.BeginWrite(data, 0, data.Length, EndSaveAccounts, fStream);
            }

        }
        private void EndSaveAccounts(IAsyncResult result)
        {
            var fStream = result.AsyncState as FileStream;
            try
            {
                if (fStream != null)
                {
                    var oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                    var newfilename = fStream.Name;
                    fStream.EndWrite(result);
                    fStream.Dispose();
                    if (File.Exists(oldfilename))
                        File.Move(oldfilename, oldfilename + "o");
                    File.Move(newfilename, oldfilename);
                    if (File.Exists(oldfilename + "o"))
                        File.Delete(oldfilename + "o");
                }
            }
            catch (Exception)
            {
            }

            Saving = false;
        }

        public bool LoadDB()
        {
            lock (LoadLock)
            {
                if (!File.Exists(DatabasePath))
                {
                    SaveDB();
                }

                using (var stream = File.OpenRead(DatabasePath))
                using (var reader = new BinaryReader(stream))
                {
                    LoadVersion = reader.ReadInt32();
                    LoadCustomVersion = reader.ReadInt32();

                    if (LoadVersion < MinVersion)
                    {
                        MessageQueue.Enqueue($"无法运行 {LoadVersion} 版本的数据库. 支持的最老版本为：{MinVersion}.");
                        return false;
                    }
                    else if (LoadVersion > Version)
                    {
                        MessageQueue.Enqueue($"无法运行 {LoadVersion} 版本的数据库. 支持的最新版本为： {Version} ");
                        return false;

                    }

                    MapIndex = reader.ReadInt32();
                    ItemIndex = reader.ReadInt32();
                    MonsterIndex = reader.ReadInt32();

                    NPCIndex = reader.ReadInt32();
                    QuestIndex = reader.ReadInt32();

                    if (LoadVersion >= 63)
                    {
                        GameshopIndex = reader.ReadInt32();
                    }

                    if (LoadVersion >= 66)
                    {
                        ConquestIndex = reader.ReadInt32();
                    }

                    if (LoadVersion >= 68)
                        RespawnIndex = reader.ReadInt32();


                    var count = reader.ReadInt32();
                    MapInfoList.Clear();
                    for (var i = 0; i < count; i++)
                        MapInfoList.Add(new MapInfo(reader));

                    count = reader.ReadInt32();
                    ItemInfoList.Clear();
                    for (var i = 0; i < count; i++)
                    {
                        ItemInfoList.Add(new ItemInfo(reader, LoadVersion, LoadCustomVersion));
                        if (ItemInfoList[i] != null && ItemInfoList[i].RandomStatsId < Settings.RandomItemStatsList.Count)
                        {
                            ItemInfoList[i].RandomStats = Settings.RandomItemStatsList[ItemInfoList[i].RandomStatsId];
                        }
                    }
                    count = reader.ReadInt32();
                    MonsterInfoList.Clear();
                    for (var i = 0; i < count; i++)
                        MonsterInfoList.Add(new MonsterInfo(reader));

                    count = reader.ReadInt32();
                    NPCInfoList.Clear();
                    for (var i = 0; i < count; i++)
                        NPCInfoList.Add(new NPCInfo(reader));

                    count = reader.ReadInt32();
                    QuestInfoList.Clear();
                    for (var i = 0; i < count; i++)
                        QuestInfoList.Add(new QuestInfo(reader));

                    DragonInfo = new DragonInfo(reader);
                    count = reader.ReadInt32();
                    for (var i = 0; i < count; i++)
                    {
                        var m = new MagicInfo(reader, LoadVersion, LoadCustomVersion);
                        if (!MagicExists(m.Spell))
                            MagicInfoList.Add(m);
                    }

                    FillMagicInfoList();
                    if (LoadVersion <= 70)
                        UpdateMagicInfo();

                    if (LoadVersion >= 63)
                    {
                        count = reader.ReadInt32();
                        GameShopList.Clear();
                        for (var i = 0; i < count; i++)
                        {
                            var item = new GameShopItem(reader, LoadVersion, LoadCustomVersion);
                            if (Main.BindGameShop(item))
                            {
                                GameShopList.Add(item);
                            }
                        }
                    }

                    if (LoadVersion >= 66)
                    {
                        ConquestInfoList.Clear();
                        count = reader.ReadInt32();
                        for (var i = 0; i < count; i++)
                        {
                            ConquestInfoList.Add(new ConquestInfo(reader));
                        }
                    }

                    if (LoadVersion > 67)
                        RespawnTick = new RespawnTimer(reader);

                }

                Settings.LinkGuildCreationItems(ItemInfoList);
            }

            return true;
        }

        public void LoadAccounts()
        {
            //reset ranking
            for (var i = 0; i < RankClass.Count(); i++)
            {
                if (RankClass[i] != null)
                {
                    RankClass[i].Clear();
                }
                else
                {
                    RankClass[i] = new List<RankCharacterInfo>();
                }
            }

            RankTop.Clear();

            //★11-06：本次加载中自动归档掉的角色数（在 using 里累加，读文件关闭后才落盘）
            int archivedWhileLoading = 0;

            lock (LoadLock)
            {
                if (!File.Exists(AccountPath))
                {
                    SaveAccounts();
                }

                using (var stream = File.OpenRead(AccountPath))
                using (var reader = new BinaryReader(stream))
                {
                    LoadVersion = reader.ReadInt32();
                    LoadCustomVersion = reader.ReadInt32();
                    NextAccountID = reader.ReadInt32();
                    NextCharacterID = reader.ReadInt32();
                    NextUserItemID = reader.ReadUInt64();
                    if (LoadVersion > 98)
                        NextHeroID = reader.ReadInt32();

                    GuildCount = reader.ReadInt32();
                    NextGuildID = reader.ReadInt32();

                    int count;
                    if (LoadVersion > 102)
                    {
                        count = reader.ReadInt32();

                        HeroList.Clear();

                        for (var i = 0; i < count; i++)
                            HeroList.Add(new HeroInfo(reader, LoadVersion, LoadCustomVersion));
                    }

                    count = reader.ReadInt32();

                    AccountList.Clear();
                    CharacterList.Clear();

                    int TrueAccount = 0;

                    for (var i = 0; i < count; i++)
                    {
                        AccountInfo NextAccount = new AccountInfo(reader);

                        if (NextAccount.ArchivedCharacters > 0)
                            archivedWhileLoading += NextAccount.ArchivedCharacters;

                        //★11-06：原来无条件丢掉「0 角色账号」。但角色被自动归档后账号也会变成 0 角色，
                        //  一丢就把账号级数据一起丢了 —— 仓库 Storage、金币 Gold、充值 Credit 都不在 .MirCA 里，
                        //  而且玩家再也没法登录这个账号去 @RESTOREPLAYER 找回角色。
                        //  原版 Crystal 本来就不跳这类账号，这里恢复原版行为；只有 AccountID 彻底为空的损坏记录才跳。
                        if (string.IsNullOrEmpty(NextAccount.AccountID))
                            continue;
                        AccountList.Add(NextAccount);
                        CharacterList.AddRange(AccountList[TrueAccount].Characters);
                        if (LoadVersion > 98 && LoadVersion < 103)
                            AccountList[TrueAccount].Characters.ForEach(character => HeroList.AddRange(character.Heroes));
                        TrueAccount++;
                    }

                    foreach (var auction in Auctions)
                    {
                        auction.SellerInfo.AccountInfo.Auctions.Remove(auction);
                    }

                    Auctions.Clear();

                    NextAuctionID = reader.ReadUInt64();

                    count = reader.ReadInt32();
                    for (var i = 0; i < count; i++)
                    {
                        var auction = new AuctionInfo(reader, LoadVersion, LoadCustomVersion);

                        if (!BindItem(auction.Item) || !BindCharacter(auction)) continue;

                        Auctions.AddLast(auction);
                        auction.SellerInfo.AccountInfo.Auctions.AddLast(auction);
                    }

                    NextMailID = reader.ReadUInt64();

                    if (LoadVersion <= 80)
                    {
                        count = reader.ReadInt32();
                        for (var i = 0; i < count; i++)
                        {
                            var mail = new MailInfo(reader, LoadVersion, LoadCustomVersion);

                            mail.RecipientInfo = GetCharacterInfo(mail.RecipientIndex);

                            if (mail.RecipientInfo != null)
                            {
                                mail.RecipientInfo.Mail.Add(mail); //add to players inbox
                            }
                        }
                    }

                    if (LoadVersion >= 63)
                    {
                        //★11-06：这里原来没有 Clear。LoadAccounts 第二次被调用（服主在界面上「重启」、
                        //  或备份/恢复流程重载账号）时 GameshopLog.Add 撞键直接抛 ArgumentException，
                        //  整个账号库读不完 —— 表现为「重启后角色全没了」。同段里 Auctions 是清过的，这里漏了。
                        GameshopLog.Clear();

                        var logCount = reader.ReadInt32();
                        for (var i = 0; i < logCount; i++)
                        {
                            GameshopLog.Add(reader.ReadInt32(), reader.ReadInt32());
                        }

                        if (ResetGS) ClearGameshopLog();
                    }

                    if (LoadVersion >= 68)
                    {
                        var saveCount = reader.ReadInt32();
                        for (var i = 0; i < saveCount; i++)
                        {
                            var saved = new RespawnSave(reader);
                            foreach (var respawn in SavedSpawns)
                            {
                                if (respawn.Info.RespawnIndex != saved.RespawnIndex) continue;

                                respawn.NextSpawnTick = saved.NextSpawnTick;

                                if (!saved.Spawned || respawn.Info.Count * SpawnMultiplier <= respawn.Count)
                                {
                                    continue;
                                }

                                var mobcount = respawn.Info.Count * SpawnMultiplier - respawn.Count;
                                for (var j = 0; j < mobcount; j++)
                                {
                                    respawn.Spawn();
                                }
                            }
                        }
                    }
                }

                //★11-06：加载期自动归档只改了内存（角色已从账号中摘掉），账号文件还是旧的。
                //  不立刻落盘，下次启动会把同一批角色再归档一遍 —— 现网实测留了 2~3 份副本。
                //  用异步版 BeginSaveAccounts：序列化在主线程（账号库 180KB，毫秒级），文件写入不阻塞开服。
                if (archivedWhileLoading > 0)
                {
                    MessageQueue.Enqueue($"加载时自动归档了 {archivedWhileLoading} 个角色，正在保存账号数据......");

                    //★保存失败不能让开服卡住：最坏结果只是「下次启动再归档一次」，幂等判断会兜住
                    try { BeginSaveAccounts(); }
                    catch (Exception ex) { MessageQueue.Enqueue($"归档后保存账号失败：{ex.Message}"); }
                }
            }
        }

        public void LoadGuilds()
        {
            lock (LoadLock)
            {
                var count = 0;

                GuildList.Clear();

                for (var i = 0; i < GuildCount; i++)
                {
                    GuildInfo guildInfo;
                    if (!File.Exists(Path.Combine(Settings.GuildPath, i + ".mgd"))) continue;

                    using (var stream = File.OpenRead(Path.Combine(Settings.GuildPath, i + ".mgd")))
                    {
                        using var reader = new BinaryReader(stream);
                        guildInfo = new GuildInfo(reader);
                    }

                    GuildList.Add(guildInfo);

                    new GuildObject(guildInfo);

                    count++;
                }

                if (count != GuildCount) GuildCount = count;
            }
        }

        public void LoadConquests()
        {
            lock (LoadLock)
            {
                Conquests.Clear();
                ConquestList.Clear();

                for (var i = 0; i < ConquestInfoList.Count; i++)
                {
                    ConquestObject newConquest;
                    ConquestGuildInfo conquestGuildInfo;
                    var tempMap = GetMap(ConquestInfoList[i].MapIndex);

                    if (tempMap == null) continue;

                    if (File.Exists(Path.Combine(Settings.ConquestsPath, ConquestInfoList[i].Index + ".mcd")))
                    {
                        using (var stream = File.OpenRead(Path.Combine(Settings.ConquestsPath, ConquestInfoList[i].Index + ".mcd")))
                        {
                            using var reader = new BinaryReader(stream);
                            conquestGuildInfo = new ConquestGuildInfo(reader) { Info = ConquestInfoList[i] };
                        }

                        newConquest = new ConquestObject(conquestGuildInfo)
                        {
                            ConquestMap = tempMap
                        };

                        for (var k = 0; k < Guilds.Count; k++)
                        {
                            if (conquestGuildInfo.Owner != Guilds[k].Guildindex) continue;
                            newConquest.Guild = Guilds[k];
                            Guilds[k].Conquest = newConquest;
                        }
                    }
                    else
                    {
                        conquestGuildInfo = new ConquestGuildInfo { Info = ConquestInfoList[i], NeedSave = true };
                        newConquest = new ConquestObject(conquestGuildInfo)
                        {
                            ConquestMap = tempMap
                        };
                    }

                    ConquestList.Add(conquestGuildInfo);
                    Conquests.Add(newConquest);
                    tempMap.Conquest.Add(newConquest);

                    newConquest.Bind();
                }
            }
        }

        public void LoadDisabledChars()
        {
            DisabledCharNames.Clear();

            var path = Path.Combine(Settings.EnvirPath, "DisabledChars.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "");
            }
            else
            {
                var lines = File.ReadAllLines(path);

                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;
                    DisabledCharNames.Add(lines[i].ToUpper());
                }
            }
        }

        public void LoadLineMessages()
        {
            LineMessages.Clear();

            var path = Path.Combine(Settings.EnvirPath, "LineMessage.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "");
            }
            else
            {
                var lines = File.ReadAllLines(path);

                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;
                    LineMessages.Add(lines[i]);
                }
            }
        }

        private bool BindCharacter(AuctionInfo auction)
        {
            bool bound = false;

            for (int i = 0; i < CharacterList.Count; i++)
            {
                if (CharacterList[i].Index == auction.SellerIndex)
                {
                    auction.SellerInfo = CharacterList[i];
                    bound = true;
                }

                else if (CharacterList[i].Index == auction.CurrentBuyerIndex)
                {
                    auction.CurrentBuyerInfo = CharacterList[i];
                    bound = true;
                }
            }

            return bound;
        }

        public void Start()
        {
            if (Running || _thread != null) return;

            Running = true;

            _thread = new Thread(WorkLoop) { IsBackground = true };
            _thread.Start();

        }
        public void Stop()
        {
            Running = false;

            lock (_locker)
            {
                // changing a blocking condition. (this makes the threads wake up!)
                Monitor.PulseAll(_locker);
            }

            //simply intterupt all the mob threads if they are running (will give an invisible error on them but fastest way of getting rid of them on shutdowns)
            for (var i = 1; i < MobThreading.Length; i++)
            {
                if (MobThreads[i] != null)
                {
                    MobThreads[i].EndTime = Time + 9999;
                }
                if (MobThreading[i] != null &&
                    MobThreading[i].ThreadState != System.Threading.ThreadState.Stopped && MobThreading[i].ThreadState != System.Threading.ThreadState.Unstarted)
                {
                    MobThreading[i].Interrupt();
                }
            }

            http?.Stop();

            while (_thread != null)
                Thread.Sleep(1);
        }

        public void Reboot()
        {
            new Thread(() =>
            {
                // 重启涉及全量 DB/地图/掉落重载，数据有问题时不能把整个进程炸掉（10-30）
                try
                {
                    MessageQueue.Enqueue("服务器正在重启...");
                    Stop();
                    Start();
                }
                catch (Exception ex)
                {
                    MessageQueue.Enqueue("[重启失败] " + ex);
                    Running = false;
                }
            }).Start();
        }

        public void UpdateIPBlock(string ipAddress, TimeSpan value)
        {
            IPBlocks[ipAddress] = Now.Add(value);
        }

        private void StartEnvir()
        {
            Players.Clear();
            StartPoints.Clear();
            StartItems.Clear();
            MapList.Clear();
            GameshopLog.Clear();
            CustomCommands.Clear();
            Heroes.Clear();
            MonsterCount = 0;

            LoadDB();

            BuffInfoList.Clear();
            foreach (var buff in BuffInfo.Load())
            {
                BuffInfoList.Add(buff);
            }

            MessageQueue.Enqueue($"游戏特效 {BuffInfoList.Count}种 加载完成");

            RecipeInfoList.Clear();
            int skippedRecipes = 0;
            foreach (var recipe in Directory.GetFiles(Settings.RecipePath, "*.txt")
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .ToArray())
            {
                RecipeInfo recipeInfo = new RecipeInfo(recipe);

                // 配方本体物品在当前数据库中不存在时直接跳过，不进入列表，
                // 否则玩家登录推送配方列表会因 Item 为 null 而崩溃
                if (!recipeInfo.IsValid)
                {
                    skippedRecipes++;
                    continue;
                }

                RecipeInfoList.Add(recipeInfo);
            }

            MessageQueue.Enqueue(skippedRecipes > 0
                ? $"合成配方 {RecipeInfoList.Count}个 加载完成（跳过 {skippedRecipes}个：其物品/工具/材料在当前数据库中不存在）"
                : $"合成配方 {RecipeInfoList.Count}个 加载完成");

            for (var i = 0; i < MapInfoList.Count; i++)
            {
                MapInfoList[i].CreateMap();
            }
            MessageQueue.Enqueue($"地图文件 {MapInfoList.Count}张 加载完成");

            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                if (ItemInfoList[i].StartItem)
                {
                    StartItems.Add(ItemInfoList[i]);
                }
            }

            ReloadDrops();

            LoadDisabledChars();
            LoadLineMessages();

            if (DragonInfo.Enabled)
            {
                DragonSystem = new Dragon(DragonInfo);
                if (DragonSystem != null)
                {
                    if (DragonSystem.Load()) DragonSystem.Info.LoadDrops();
                }

                MessageQueue.Enqueue("破天魔龙加载完成");
            }

            DefaultNPC = NPCScript.GetOrAdd((uint)Random.Next(1000000, 1999999), Settings.DefaultNPCFilename, NPCScriptType.AutoPlayer);
            MonsterNPC = NPCScript.GetOrAdd((uint)Random.Next(2000000, 2999999), Settings.MonsterNPCFilename, NPCScriptType.AutoMonster);
            RobotNPC = NPCScript.GetOrAdd((uint)Random.Next(3000000, 3999999), Settings.RobotNPCFilename, NPCScriptType.Robot);

            //假人（AI 玩家）：此时地图、物品、技能表都已就绪，可以生成了
            FakePlayerManager.Init();

            MessageQueue.Enqueue("正在部署游戏环境......");

            //内存治理（20261106）：开服完成即打一份分布报告，回答「内存花在哪」
            LogMemoryReport();
        }
        private void StartNetwork()
        {
            Connections.Clear();

            LoadAccounts();

            LoadGuilds();

            LoadConquests();

            _listener = new TcpListener(IPAddress.Parse(Settings.IPAddress), Settings.Port);
            _listener.Start();
            _listener.BeginAcceptTcpClient(Connection, null);

            if (StatusPortEnabled)
            {
                _StatusPort = new TcpListener(IPAddress.Parse(Settings.IPAddress), 3000);
                _StatusPort.Start();
                _StatusPort.BeginAcceptTcpClient(StatusConnection, null);
            }

            MessageQueue.Enqueue("网络设置加载完成");
        }

        private void StopEnvir()
        {
            //先让假人正常下线（会走 StopGame，把在线人数等计数还原）
            FakePlayerManager.Close();

            SaveGoods(true);

            // 停服前丢掉还没执行的主线程任务（例如排队中的"重载地图"），
            // 免得下次开服时被一个针对旧运行态的操作打到新环境上
            lock (_mainThreadQueueLock)
            {
                _mainThreadQueue.Clear();
                _hasMainThreadWork = false;
            }

            MapList.Clear();
            StartPoints.Clear();
            StartItems.Clear();
            Objects.Clear();
            Players.Clear();
            Heroes.Clear();

            CleanUp();

            GC.Collect();

            MessageQueue.Enqueue("游戏服务已停止");
        }
        private void StopNetwork()
        {
            _listener.Stop();
            lock (Connections)
            {
                for (var i = Connections.Count - 1; i >= 0; i--)
                    Connections[i].SendDisconnect(0);
            }

            if (StatusPortEnabled)
            {
                _StatusPort.Stop();
                for (var i = StatusConnections.Count - 1; i >= 0; i--)
                    StatusConnections[i].SendDisconnect();
            }

            var expire = Time + 5000;

            while (Connections.Count != 0 && Stopwatch.ElapsedMilliseconds < expire)
            {
                Time = Stopwatch.ElapsedMilliseconds;

                for (var i = Connections.Count - 1; i >= 0; i--)
                    Connections[i].Process();

                Thread.Sleep(1);
            }


            Connections.Clear();

            expire = Time + 10000;
            while (StatusConnections.Count != 0 && Stopwatch.ElapsedMilliseconds < expire)
            {
                Time = Stopwatch.ElapsedMilliseconds;

                for (var i = StatusConnections.Count - 1; i >= 0; i--)
                    StatusConnections[i].Process();

                Thread.Sleep(1);
            }


            StatusConnections.Clear();
            MessageQueue.Enqueue("网络已停止");
        }

        private void CleanUp()
        {
            for (var i = 0; i < CharacterList.Count; i++)
            {
                var info = CharacterList[i];

                if (info.Deleted)
                {
                    #region Mentor Cleanup
                    if (info.Mentor > 0)
                    {
                        var mentor = GetCharacterInfo(info.Mentor);

                        if (mentor != null)
                        {
                            mentor.Mentor = 0;
                            mentor.MentorExp = 0;
                            mentor.IsMentor = false;
                        }

                        info.Mentor = 0;
                        info.MentorExp = 0;
                        info.IsMentor = false;
                    }
                    #endregion

                    #region Marriage Cleanup
                    if (info.Married > 0)
                    {
                        var Lover = GetCharacterInfo(info.Married);

                        info.Married = 0;
                        info.MarriedDate = Now;

                        Lover.Married = 0;
                        Lover.MarriedDate = Now;
                        if (Lover.Equipment[(int)EquipmentSlot.左戒指] != null)
                            Lover.Equipment[(int)EquipmentSlot.左戒指].WeddingRing = -1;
                    }
                    #endregion
                }

                if (info.Mail.Count > Settings.MailCapacity)
                {
                    for (var j = info.Mail.Count - 1 - (int)Settings.MailCapacity; j >= 0; j--)
                    {
                        if (info.Mail[j].DateOpened > Now && info.Mail[j].Collected && info.Mail[j].Items.Count == 0 && info.Mail[j].Gold == 0)
                        {
                            info.Mail.Remove(info.Mail[j]);
                        }
                    }
                }
            }
        }

        private void Connection(IAsyncResult result)
        {
            try
            {
                if (!Running || !_listener.Server.IsBound) return;
            }
            catch (Exception e)
            {
                MessageQueue.Enqueue(e.ToString());
            }

            try
            {
                var tempTcpClient = _listener.EndAcceptTcpClient(result);

                bool connected = false;
                var ipAddress = tempTcpClient.Client.RemoteEndPoint.ToString().Split(':')[0];

                if (!IPBlocks.TryGetValue(ipAddress, out DateTime banDate) || banDate < Now)
                {
                    int count = 0;

                    for (int i = 0; i < Connections.Count; i++)
                    {
                        var connection = Connections[i];

                        if (!connection.Connected || connection.IPAddress != ipAddress)
                            continue;

                        count++;
                    }

                    if (count >= Settings.MaxIP)
                    {
                        UpdateIPBlock(ipAddress, TimeSpan.FromSeconds(Settings.IPBlockSeconds));

                        MessageQueue.Enqueue(ipAddress + " 已断开连接，连接次数过于频繁");
                    }
                    else
                    {
                        var tempConnection = new MirConnection(++_sessionID, tempTcpClient);
                        if (tempConnection.Connected)
                        {
                            connected = true;
                            lock (Connections)
                                Connections.Add(tempConnection);
                        }
                    }
                }

                if (!connected)
                    tempTcpClient.Close();
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue(ex);
            }
            finally
            {
                while (Connections.Count >= Settings.MaxUser)
                    Thread.Sleep(1);

                if (Running && _listener.Server.IsBound)
                    _listener.BeginAcceptTcpClient(Connection, null);
            }
        }

        private void StatusConnection(IAsyncResult result)
        {
            if (!Running || !_StatusPort.Server.IsBound) return;

            try
            {
                var tempTcpClient = _StatusPort.EndAcceptTcpClient(result);
                lock (StatusConnections)
                    StatusConnections.Add(new MirStatusConnection(tempTcpClient));
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue(ex);
            }
            finally
            {
                while (StatusConnections.Count >= 5) //dont allow to many status port connections it's just an abuse thing
                    Thread.Sleep(1);

                if (Running && _StatusPort.Server.IsBound)
                    _StatusPort.BeginAcceptTcpClient(StatusConnection, null);
            }
        }

        public void NewAccount(ClientPackets.NewAccount p, MirConnection c)
        {
            if (!Settings.AllowNewAccount)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 0 });
                return;
            }

            
            if (ConnectionLogs.TryGetValue(c.IPAddress, out MirConnectionLog currentlog))
            {
                if (currentlog.AccountsMade.Count > 2)
                {
                    IPBlocks[c.IPAddress] = Now.AddHours(24);
                    c.Enqueue(new ServerPackets.NewAccount { Result = 0 });
                    return;
                }
                currentlog.AccountsMade.Add(Time);
                for (int i = 0; i < currentlog.AccountsMade.Count; i++)
                {
                    if ((currentlog.AccountsMade[i] + 60 * 60 * 1000) < Time)
                    {
                        currentlog.AccountsMade.RemoveAt(i);
                        break;
                    }
                }
            }
            else
            {
                ConnectionLogs[c.IPAddress] = new MirConnectionLog() { IPAddress = c.IPAddress};
            }


            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 1 });
                return;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 2 });
                return;
            }
            if (!string.IsNullOrWhiteSpace(p.EMailAddress) && !EMailReg.IsMatch(p.EMailAddress) ||
                p.EMailAddress.Length > 50)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 3 });
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.UserName) && p.UserName.Length > 20)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 4 });
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretQuestion) && p.SecretQuestion.Length > 30)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 5 });
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretAnswer) && p.SecretAnswer.Length > 30)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 6 });
                return;
            }

            lock (AccountLock)
            {
                if (AccountExists(p.AccountID))
                {
                    c.Enqueue(new ServerPackets.NewAccount { Result = 7 });
                    return;
                }

                AccountList.Add(new AccountInfo(p) { Index = ++NextAccountID, CreationIP = c.IPAddress });


                c.Enqueue(new ServerPackets.NewAccount { Result = 8 });
            }
        }

        public int HTTPNewAccount(ClientPackets.NewAccount p, string ip)
        {
            if (!Settings.AllowNewAccount)
            {
                return 0;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                return 1;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                return 2;
            }
            if (!string.IsNullOrWhiteSpace(p.EMailAddress) && !EMailReg.IsMatch(p.EMailAddress) ||
                p.EMailAddress.Length > 50)
            {
                return 3;
            }

            if (!string.IsNullOrWhiteSpace(p.UserName) && p.UserName.Length > 20)
            {
                return 4;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretQuestion) && p.SecretQuestion.Length > 30)
            {
                return 5;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretAnswer) && p.SecretAnswer.Length > 30)
            {
                return 6;
            }

            lock (AccountLock)
            {
                if (AccountExists(p.AccountID))
                {
                    return 7;
                }

                AccountList.Add(new AccountInfo(p) { Index = ++NextAccountID, CreationIP = ip });
                return 8;
            }
        }

        public void ChangePassword(ClientPackets.ChangePassword p, MirConnection c)
        {
            if (!Settings.AllowChangePassword)
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 0 });
                return;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 1 });
                return;
            }

            if (!PasswordReg.IsMatch(p.CurrentPassword))
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 2 });
                return;
            }

            if (!PasswordReg.IsMatch(p.NewPassword))
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 3 });
                return;
            }

            var account = GetAccount(p.AccountID);

            if (account == null)
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 4 });
                return;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > Now)
                {
                    c.Enqueue(new ServerPackets.ChangePasswordBanned { Reason = account.BanReason, ExpiryDate = account.ExpiryDate });
                    return;
                }
                account.Banned = false;
            }
            account.BanReason = string.Empty;
            account.ExpiryDate = DateTime.MinValue;

            p.CurrentPassword = Utils.Crypto.HashPassword(p.CurrentPassword, account.Salt);
            if (string.CompareOrdinal(account.Password, p.CurrentPassword) != 0)
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 5 });
                return;
            }

            account.Password = p.NewPassword;
            account.RequirePasswordChange = false;
            c.Enqueue(new ServerPackets.ChangePassword { Result = 6 });
        }
        public void Login(ClientPackets.Login p, MirConnection c)
        {
            if (!Settings.AllowLogin)
            {
                c.Enqueue(new ServerPackets.Login { Result = 0 });
                return;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.Login { Result = 1 });
                return;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                c.Enqueue(new ServerPackets.Login { Result = 2 });
                return;
            }
            var account = GetAccount(p.AccountID);

            if (account == null)
            {
                c.Enqueue(new ServerPackets.Login { Result = 3 });
                return;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > Now)
                {
                    c.Enqueue(new ServerPackets.LoginBanned
                    {
                        Reason = account.BanReason,
                        ExpiryDate = account.ExpiryDate
                    });
                    return;
                }
                account.Banned = false;
            }
            account.BanReason = string.Empty;
            account.ExpiryDate = DateTime.MinValue;

            p.Password = Utils.Crypto.HashPassword(p.Password, account.Salt);

            if (string.CompareOrdinal(account.Password, p.Password) != 0)
            {
                if (account.WrongPasswordCount++ >= 5)
                {
                    account.Banned = true;
                    account.BanReason = "错误登录次数太多";
                    account.ExpiryDate = Now.AddMinutes(2);

                    c.Enqueue(new ServerPackets.LoginBanned
                    {
                        Reason = account.BanReason,
                        ExpiryDate = account.ExpiryDate
                    });
                    return;
                }

                c.Enqueue(new ServerPackets.Login { Result = 4 });
                return;
            }
            account.WrongPasswordCount = 0;

            if (account.RequirePasswordChange)
            {
                c.Enqueue(new ServerPackets.Login { Result = 5 });
                return;
            }

            lock (AccountLock)
            {
                account.Connection?.SendDisconnect(1);

                account.Connection = c;
            }

            c.Account = account;
            c.Stage = GameStage.Select;

            account.LastDate = Now;
            account.LastIP = c.IPAddress;

            MessageQueue.Enqueue(account.Connection.SessionID + ", " + account.Connection.IPAddress + "已登录服务器");
            c.Enqueue(new ServerPackets.LoginSuccess { Characters = account.GetSelectInfo() });
        }

        public int HTTPLogin(string AccountID, string Password)
        {
            if (!Settings.AllowLogin)
            {
                return 0;
            }

            if (!AccountIDReg.IsMatch(AccountID))
            {
                return 1;
            }

            if (!PasswordReg.IsMatch(Password))
            {
                return 2;
            }

            var account = GetAccount(AccountID);

            if (account == null)
            {
                return 3;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > Now)
                {
                    return 4;
                }
                account.Banned = false;
            }
            account.BanReason = string.Empty;
            account.ExpiryDate = DateTime.MinValue;
            if (string.CompareOrdinal(account.Password, Password) != 0)
            {
                if (account.WrongPasswordCount++ >= 5)
                {
                    account.Banned = true;
                    account.BanReason = "登录错误次数太多";
                    account.ExpiryDate = Now.AddMinutes(2);
                    return 5;
                }
                return 6;
            }
            account.WrongPasswordCount = 0;
            return 7;
        }

        public void NewCharacter(ClientPackets.NewCharacter p, MirConnection c, bool IsGm)
        {
            if (!Settings.AllowNewCharacter)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 0 });
                return;
            }

            if (ConnectionLogs.TryGetValue(c.IPAddress, out MirConnectionLog currentlog))
            {
                if (currentlog.CharactersMade.Count > 4)
                {
                    IPBlocks[c.IPAddress] = Now.AddHours(24);
                    c.Enqueue(new ServerPackets.NewCharacter { Result = 0 });
                    return;
                }
                currentlog.CharactersMade.Add(Time);
                for (int i = 0; i < currentlog.CharactersMade.Count; i++)
                {
                    if ((currentlog.CharactersMade[i] + 60 * 60 * 1000) < Time)
                    {
                        currentlog.CharactersMade.RemoveAt(i);
                        break;
                    }
                }
            }
            else
            {
                ConnectionLogs[c.IPAddress] = new MirConnectionLog() { IPAddress = c.IPAddress };
            }


            if (!CharacterReg.IsMatch(p.Name))
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 1 });
                return;
            }

            if (!IsGm && DisabledCharNames.Contains(p.Name.ToUpper()))
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 1 });
                return;
            }

            if (p.Gender != MirGender.男性 && p.Gender != MirGender.女性)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 2 });
                return;
            }

            if (p.Class != MirClass.战士 && p.Class != MirClass.法师 && p.Class != MirClass.道士 &&
                p.Class != MirClass.刺客 && p.Class != MirClass.弓箭)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 3 });
                return;
            }

            if(p.Class == MirClass.刺客 && !Settings.AllowCreateAssassin ||
                p.Class == MirClass.弓箭 && !Settings.AllowCreateArcher)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 3 });
                return;
            }

            var count = 0;

            for (var i = 0; i < c.Account.Characters.Count; i++)
            {
                if (c.Account.Characters[i].Deleted) continue;

                if (++count >= Globals.MaxCharacterCount)
                {
                    c.Enqueue(new ServerPackets.NewCharacter { Result = 4 });
                    return;
                }
            }

            lock (AccountLock)
            {
                if (CharacterExists(p.Name))
                {
                    c.Enqueue(new ServerPackets.NewCharacter { Result = 5 });
                    return;
                }

                var info = new CharacterInfo(p, c) { Index = ++NextCharacterID, AccountInfo = c.Account };

                c.Account.Characters.Add(info);
                CharacterList.Add(info);

                c.Enqueue(new ServerPackets.NewCharacterSuccess { CharInfo = info.ToSelectInfo() });
            }
        }

        public bool CanCreateHero(ClientPackets.NewHero p, MirConnection c, bool IsGm)
        {
            if (!Settings.AllowNewHero)
            {
                c.Enqueue(new S.NewHero { Result = 0 });
                return false;
            }

            if (!CharacterReg.IsMatch(p.Name))
            {
                c.Enqueue(new S.NewHero { Result = 1 });
                return false;
            }

            if (!IsGm && DisabledCharNames.Contains(p.Name.ToUpper()))
            {
                c.Enqueue(new S.NewHero { Result = 1 });
                return false;
            }

            if (p.Gender != MirGender.男性 && p.Gender != MirGender.女性)
            {
                c.Enqueue(new S.NewHero { Result = 2 });
                return false;
            }

            if (p.Class != MirClass.战士 && p.Class != MirClass.法师 && p.Class != MirClass.道士 && p.Class != MirClass.刺客 && p.Class != MirClass.弓箭)
            {
                c.Enqueue(new S.NewHero { Result = 3 });
                return false;
            }

            if (p.Class == MirClass.战士 && !Settings.Hero_CanCreateClass[0] || p.Class == MirClass.法师 && !Settings.Hero_CanCreateClass[1] || p.Class == MirClass.道士 && !Settings.Hero_CanCreateClass[2] || p.Class == MirClass.刺客 && !Settings.Hero_CanCreateClass[3] || p.Class == MirClass.弓箭 && !Settings.Hero_CanCreateClass[4])
            {
                c.Enqueue(new S.NewHero { Result = 3 });
                return false;
            }

            lock (AccountLock)
            {
                if (CharacterExists(p.Name))
                {
                    c.Enqueue(new S.NewHero { Result = 5 });
                    return false;
                }
            }

            return true;
        }

        public bool AccountExists(string accountID)
        {
            for (var i = 0; i < AccountList.Count; i++)
                if (string.Compare(AccountList[i].AccountID, accountID, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;

            return false;
        }

        public bool CharacterExists(string name)
        {
            for (var i = 0; i < CharacterList.Count; i++)
                if (string.Compare(CharacterList[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;

            return false;
        }

        public List<CharacterInfo> MatchPlayer(string PlayerID, bool match = false)
        {
            if (string.IsNullOrEmpty(PlayerID)) return new List<CharacterInfo>(CharacterList);

            List<CharacterInfo> list = new List<CharacterInfo>();

            for (int i = 0; i < CharacterList.Count; i++)
            {
                if (match)
                {
                    if (CharacterList[i].Name.Equals(PlayerID, StringComparison.OrdinalIgnoreCase))
                        list.Add(CharacterList[i]);
                }
                else
                {
                    if (CharacterList[i].Name.IndexOf(PlayerID, StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(CharacterList[i]);
                }
            }

            return list;
        }
        public List<CharacterInfo> MatchPlayerbyItem(string itemIdentifier, bool match = false)
        {
            List<CharacterInfo> list = new List<CharacterInfo>();

            bool isNumeric = ulong.TryParse(itemIdentifier, out ulong itemId);

            for (int i = 0; i < CharacterList.Count; i++)
            {
                if (match)
                {
                    foreach (var item in CharacterList[i].Inventory)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].AccountInfo.Storage)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].QuestInventory)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].Equipment)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var mail in CharacterList[i].Mail)
                        foreach (var item in mail.Items)
                            if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                                list.Add(CharacterList[i]);
                }
                else
                {
                    foreach (var item in CharacterList[i].Inventory)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.IndexOf(itemIdentifier, StringComparison.OrdinalIgnoreCase) >= 0)) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].QuestInventory)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.IndexOf(itemIdentifier, StringComparison.OrdinalIgnoreCase) >= 0)) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].Equipment)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.IndexOf(itemIdentifier, StringComparison.OrdinalIgnoreCase) >= 0)) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].AccountInfo.Storage)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.IndexOf(itemIdentifier, StringComparison.OrdinalIgnoreCase) >= 0)) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);
                }
            }

            return list;
        }

        public AccountInfo GetAccount(string accountID)
        {
            for (var i = 0; i < AccountList.Count; i++)
                if (string.Compare(AccountList[i].AccountID, accountID, StringComparison.OrdinalIgnoreCase) == 0)
                    return AccountList[i];

            return null;
        }

        public AccountInfo GetAccountByCharacter(string name)
        {
            for (var i = 0; i < AccountList.Count; i++)
            {
                for (int j = 0; j < AccountList[i].Characters.Count; j++)
                {
                    if (string.Compare(AccountList[i].Characters[j].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                        return AccountList[i];
                }
            }

            return null;
        }

        public List<AccountInfo> MatchAccounts(string accountID, bool match = false)
        {
            if (string.IsNullOrEmpty(accountID)) return new List<AccountInfo>(AccountList);

            var list = new List<AccountInfo>();

            for (var i = 0; i < AccountList.Count; i++)
            {
                if (match)
                {
                    if (AccountList[i].AccountID.Equals(accountID, StringComparison.OrdinalIgnoreCase))
                        list.Add(AccountList[i]);
                }
                else
                {
                    if (AccountList[i].AccountID.IndexOf(accountID, StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(AccountList[i]);
                }
            }

            return list;
        }

        public List<AccountInfo> MatchAccountsByPlayer(string playerName, bool match = false)
        {
            if (string.IsNullOrEmpty(playerName)) return new List<AccountInfo>(AccountList);

            var list = new List<AccountInfo>();

            for (var i = 0; i < AccountList.Count; i++)
            {
                for (var j = 0; j < AccountList[i].Characters.Count; j++)
                {
                    if (match)
                    {
                        if (AccountList[i].Characters[j].Name.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                            list.Add(AccountList[i]);
                    }
                    else
                    {
                        if (AccountList[i].Characters[j].Name.IndexOf(playerName, StringComparison.OrdinalIgnoreCase) >= 0)
                            list.Add(AccountList[i]);
                    }
                }
            }

            return list;
        }

        public List<AccountInfo> MatchAccountsByIP(string ipAddress, bool matchLastIP = false, bool match = false)
        {
            if (string.IsNullOrEmpty(ipAddress)) return new List<AccountInfo>(AccountList);

            var list = new List<AccountInfo>();

            for (var i = 0; i < AccountList.Count; i++)
            {
                string ipToMatch = matchLastIP ? AccountList[i].LastIP : AccountList[i].CreationIP;

                if (match)
                {
                    if (ipToMatch.Equals(ipAddress, StringComparison.OrdinalIgnoreCase))
                        list.Add(AccountList[i]);
                }
                else
                {
                    if (ipToMatch.IndexOf(ipAddress, StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(AccountList[i]);
                }
            }

            return list;
        }


        public void CreateAccountInfo()
        {
            AccountList.Add(new AccountInfo {Index = ++NextAccountID});
        }
        public void CreateMapInfo()
        {
            MapInfoList.Add(new MapInfo {Index = ++MapIndex});
        }
        public void CreateItemInfo(ItemType type = ItemType.杂物)
        {
            ItemInfoList.Add(new ItemInfo { Index = ++ItemIndex, Type = type, RandomStatsId = 255});
        }
        public void CreateMonsterInfo()
        {
            MonsterInfoList.Add(new MonsterInfo {Index = ++MonsterIndex});
        }
        public void CreateNPCInfo()
        {
            NPCInfoList.Add(new NPCInfo { Index = ++NPCIndex });
        }
        public void CreateQuestInfo()
        {
            QuestInfoList.Add(new QuestInfo { Index = ++QuestIndex });
        }

        public void AddToGameShop(ItemInfo Info)
        {
            GameShopList.Add(new GameShopItem { GIndex = ++GameshopIndex, GoldPrice = (uint)(1000 * Settings.CredxGold), CreditPrice = 1000, ItemIndex = Info.Index, Info = Info, Date = Now, Class = "All", Category = Info.Type.ToString() });
        }

        public void Remove(MapInfo info)
        {
            MapInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(ItemInfo info)
        {
            ItemInfoList.Remove(info);
        }
        public void Remove(MonsterInfo info)
        {
            MonsterInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(NPCInfo info)
        {
            NPCInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(QuestInfo info)
        {
            QuestInfoList.Remove(info);
            //Desync all objects\
        }

        public void Remove(GameShopItem info)
        {
            GameShopList.Remove(info);

            if (GameShopList.Count == 0)
            {
                GameshopIndex = 0;
            }
                
            //Desync all objects\
        }

        public UserItem CreateFreshItem(ItemInfo info)
        {
            var item = new UserItem(info)
                {
                    UniqueID = ++NextUserItemID,
                    CurrentDura = info.Durability,
                    MaxDura = info.Durability
                };

            UpdateItemExpiry(item);

            return item;
        }
        public UserItem CreateDropItem(int index)
        {
            return CreateDropItem(GetItemInfo(index));
        }
        public UserItem CreateDropItem(ItemInfo info)
        {
            if (info == null) return null;

            var item = new UserItem(info)
                {
                    UniqueID = ++NextUserItemID,
                    MaxDura = info.Durability,
                    CurrentDura = (ushort) Math.Min(info.Durability, Random.Next(info.Durability) + 1000)
                };

            UpgradeItem(item);

            UpdateItemExpiry(item);

            if (!info.NeedIdentify) item.Identified = true;
            return item;
        }

        public UserItem CreateShopItem(ItemInfo info, ulong id)
        {
            if (info == null) return null;

            var item = new UserItem(info)
            {
                UniqueID = id,
                CurrentDura = info.Durability,
                MaxDura = info.Durability,
                IsShopItem = true,
            };

            return item;
        }

        public void UpdateItemExpiry(UserItem item)
        {
            var expiryInfo = new ExpireInfo();

            var r = new Regex(@"\[(.*?)\]");
            var expiryMatch = r.Match(item.Info.Name);

            if (expiryMatch.Success)
            {
                var parameter = expiryMatch.Groups[1].Captures[0].Value;

                var numAlpha = new Regex("(?<Numeric>[0-9]*)(?<Alpha>[a-zA-Z]*)");
                var match = numAlpha.Match(parameter);

                var alpha = match.Groups["Alpha"].Value;
                var num = 0;

                int.TryParse(match.Groups["Numeric"].Value, out num);

                switch (alpha)
                {
                    case "m":
                        expiryInfo.ExpiryDate = Now.AddMinutes(num);
                        break;
                    case "h":
                        expiryInfo.ExpiryDate = Now.AddHours(num);
                        break;
                    case "d":
                        expiryInfo.ExpiryDate = Now.AddDays(num);
                        break;
                    case "M":
                        expiryInfo.ExpiryDate = Now.AddMonths(num);
                        break;
                    case "y":
                        expiryInfo.ExpiryDate = Now.AddYears(num);
                        break;
                    default:
                        expiryInfo.ExpiryDate = DateTime.MaxValue;
                        break;
                }

                item.ExpireInfo = expiryInfo;
            }
        }

        public void UpgradeItem(UserItem item)
        {
            if (item.Info.RandomStats == null) return;
            var stat = item.Info.RandomStats;
            if (stat.MaxDuraChance > 0 && Random.Next(stat.MaxDuraChance) == 0)
            {
                var dura = RandomomRange(stat.MaxDuraMaxStat, stat.MaxDuraStatChance);
                item.MaxDura = (ushort)Math.Min(ushort.MaxValue, item.MaxDura + dura * 1000);
                item.CurrentDura = (ushort)Math.Min(ushort.MaxValue, item.CurrentDura + dura * 1000);
            }

            if (stat.MaxAcChance > 0 && Random.Next(stat.MaxAcChance) == 0) item.AddedStats[Stat.MaxAC] = (byte)(RandomomRange(stat.MaxAcMaxStat-1, stat.MaxAcStatChance)+1);
            if (stat.MaxMacChance > 0 && Random.Next(stat.MaxMacChance) == 0) item.AddedStats[Stat.MaxMAC] = (byte)(RandomomRange(stat.MaxMacMaxStat-1, stat.MaxMacStatChance)+1);
            if (stat.MaxDcChance > 0 && Random.Next(stat.MaxDcChance) == 0) item.AddedStats[Stat.MaxDC] = (byte)(RandomomRange(stat.MaxDcMaxStat-1, stat.MaxDcStatChance)+1);
            if (stat.MaxMcChance > 0 && Random.Next(stat.MaxMcChance) == 0) item.AddedStats[Stat.MaxMC] = (byte)(RandomomRange(stat.MaxMcMaxStat-1, stat.MaxMcStatChance)+1);
            if (stat.MaxScChance > 0 && Random.Next(stat.MaxScChance) == 0) item.AddedStats[Stat.MaxSC] = (byte)(RandomomRange(stat.MaxScMaxStat-1, stat.MaxScStatChance)+1);
            if (stat.AccuracyChance > 0 && Random.Next(stat.AccuracyChance) == 0) item.AddedStats[Stat.准确] = (byte)(RandomomRange(stat.AccuracyMaxStat-1, stat.AccuracyStatChance)+1);
            if (stat.AgilityChance > 0 && Random.Next(stat.AgilityChance) == 0) item.AddedStats[Stat.敏捷] = (byte)(RandomomRange(stat.AgilityMaxStat-1, stat.AgilityStatChance)+1);
            if (stat.HpChance > 0 && Random.Next(stat.HpChance) == 0) item.AddedStats[Stat.HP] = (byte)(RandomomRange(stat.HpMaxStat-1, stat.HpStatChance)+1);
            if (stat.MpChance > 0 && Random.Next(stat.MpChance) == 0) item.AddedStats[Stat.MP] = (byte)(RandomomRange(stat.MpMaxStat-1, stat.MpStatChance)+1);
            if (stat.StrongChance > 0 && Random.Next(stat.StrongChance) == 0) item.AddedStats[Stat.强度] = (byte)(RandomomRange(stat.StrongMaxStat-1, stat.StrongStatChance)+1);
            if (stat.MagicResistChance > 0 && Random.Next(stat.MagicResistChance) == 0) item.AddedStats[Stat.魔法躲避] = (byte)(RandomomRange(stat.MagicResistMaxStat-1, stat.MagicResistStatChance)+1);
            if (stat.PoisonResistChance > 0 && Random.Next(stat.PoisonResistChance) == 0) item.AddedStats[Stat.毒物躲避] = (byte)(RandomomRange(stat.PoisonResistMaxStat-1, stat.PoisonResistStatChance)+1);
            if (stat.HpRecovChance > 0 && Random.Next(stat.HpRecovChance) == 0) item.AddedStats[Stat.生命恢复] = (byte)(RandomomRange(stat.HpRecovMaxStat-1, stat.HpRecovStatChance)+1);
            if (stat.MpRecovChance > 0 && Random.Next(stat.MpRecovChance) == 0) item.AddedStats[Stat.法力恢复] = (byte)(RandomomRange(stat.MpRecovMaxStat-1, stat.MpRecovStatChance)+1);
            if (stat.PoisonRecovChance > 0 && Random.Next(stat.PoisonRecovChance) == 0) item.AddedStats[Stat.中毒恢复] = (byte)(RandomomRange(stat.PoisonRecovMaxStat-1, stat.PoisonRecovStatChance)+1);
            if (stat.CriticalRateChance > 0 && Random.Next(stat.CriticalRateChance) == 0) item.AddedStats[Stat.暴击倍率] = (byte)(RandomomRange(stat.CriticalRateMaxStat-1, stat.CriticalRateStatChance)+1);
            if (stat.CriticalDamageChance > 0 && Random.Next(stat.CriticalDamageChance) == 0) item.AddedStats[Stat.暴击伤害] = (byte)(RandomomRange(stat.CriticalDamageMaxStat-1, stat.CriticalDamageStatChance)+1);
            if (stat.FreezeChance > 0 && Random.Next(stat.FreezeChance) == 0) item.AddedStats[Stat.冰冻伤害] = (byte)(RandomomRange(stat.FreezeMaxStat-1, stat.FreezeStatChance)+1);
            if (stat.PoisonAttackChance > 0 && Random.Next(stat.PoisonAttackChance) == 0) item.AddedStats[Stat.毒素伤害] = (byte)(RandomomRange(stat.PoisonAttackMaxStat-1, stat.PoisonAttackStatChance)+1);
            if (stat.AttackSpeedChance > 0 && Random.Next(stat.AttackSpeedChance) == 0) item.AddedStats[Stat.攻击速度] = (sbyte)(RandomomRange(stat.AttackSpeedMaxStat-1, stat.AttackSpeedStatChance)+1);
            if (stat.LuckChance > 0 && Random.Next(stat.LuckChance) == 0) item.AddedStats[Stat.幸运] = (sbyte)(RandomomRange(stat.LuckMaxStat-1, stat.LuckStatChance)+1);
            if (stat.CurseChance > 0 && Random.Next(100) <= stat.CurseChance) item.Cursed = true;

            if (stat.SlotChance > 0 && Random.Next(stat.SlotChance) == 0)
            {
                var slot = (byte)(RandomomRange(stat.SlotMaxStat - 1, stat.SlotStatChance) + 1);

                if (slot > item.Info.Slots)
                {
                    item.SetSlotSize(slot);
                }
            }
        }

        public int RandomomRange(int count, int rate)
        {
            var x = 0;
            for (var i = 0; i < count; i++) if (Random.Next(rate) == 0) x++;
            return x;
        }
        public bool BindItem(UserItem item)
        {
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var info = ItemInfoList[i];
                if (info.Index != item.ItemIndex) continue;
                item.Info = info;

                return BindSlotItems(item);
            }
            return false;
        }

        public bool BindGameShop(GameShopItem item, bool editEnvir = true)
        {
            for (var i = 0; i < Edit.ItemInfoList.Count; i++)
            {
                var info = Edit.ItemInfoList[i];
                if (info.Index != item.ItemIndex) continue;
                item.Info = info;

                return true;
            }
            return false;
        }

        public bool BindSlotItems(UserItem item)
        {           
            for (var i = 0; i < item.Slots.Length; i++)
            {
                if (item.Slots[i] == null) continue;

                if (!BindItem(item.Slots[i])) return false;
            }

            return true;
        }

        public bool BindQuest(QuestProgressInfo quest)
        {
            for (var i = 0; i < QuestInfoList.Count; i++)
            {
                var info = QuestInfoList[i];
                if (info.Index != quest.Index) continue;
                quest.Info = info;
                return true;
            }
            return false;
        }

        public Map GetMap(int index)
        {
            return MapList.FirstOrDefault(t => t.Info.Index == index);
        }

        public Map GetMap(string name, bool strict = true)
        {
            return MapList.FirstOrDefault(t => strict ? string.Equals(t.Info.Title, name, StringComparison.CurrentCultureIgnoreCase) : t.Info.Title.StartsWith(name, StringComparison.CurrentCultureIgnoreCase));
        }

        public Map GetWorldMap(string name)
        {
            return MapList.FirstOrDefault(t => t.Info.Title.StartsWith(name, StringComparison.CurrentCultureIgnoreCase) && t.Info.BigMap > 0);
        }

        public MapInfo GetMapInfo(int index)
        {
            return MapInfoList.FirstOrDefault(t => t.Index == index);
        }

        public Map GetMapByNameAndInstance(string name, int instanceValue = 0)
        {
            if (instanceValue < 0) instanceValue = 0;
            if (instanceValue > 0) instanceValue--;

            var instanceMapList = MapList.Where(t => string.Equals(t.Info.FileName, name, StringComparison.CurrentCultureIgnoreCase)).ToList();
            return instanceValue < instanceMapList.Count() ? instanceMapList[instanceValue] : null;
        }

        public MonsterInfo GetMonsterInfo(int index)
        {
            for (var i = 0; i < MonsterInfoList.Count; i++)
                if (MonsterInfoList[i].Index == index) return MonsterInfoList[i];

            return null;
        }

        public MonsterInfo GetMonsterInfo(int ai, int effect = -1)
        {
            for (var i = 0; i < MonsterInfoList.Count; i++)
                if (MonsterInfoList[i].AI == ai && (MonsterInfoList[i].Effect == effect || effect < 0)) return MonsterInfoList[i];

            return null;
        }

        public NPCObject GetNPC(string name)
        {
            return MapList.SelectMany(t1 => t1.NPCs.Where(t => t.Info.Name == name)).FirstOrDefault();
        }

        public NPCObject GetWorldMapNPC(string name)
        {
            return MapList.SelectMany(t1 => t1.NPCs.Where(t => t.Info.GameName.StartsWith(name, StringComparison.CurrentCultureIgnoreCase) && t.Info.ShowOnBigMap)).FirstOrDefault();
        }

        public MonsterInfo GetMonsterInfo(int id, bool strict = false)
        {
            String monsterName = MonsterInfoList.FirstOrDefault(x => x.Index == id)?.Name;

            if (monsterName == null)
            {
                return null;
            }
            else
            {
                return (GetMonsterInfo(monsterName, strict));
            }
        }

        public MonsterInfo GetMonsterInfo(string name, bool Strict = false)
        {
            for (var i = 0; i < MonsterInfoList.Count; i++)
            {
                var info = MonsterInfoList[i];
                if (Strict)
                {
                    if (info.Name != name) continue;
                    return info;
                }
                else
                {
                    if (string.Compare(info.Name, name, StringComparison.OrdinalIgnoreCase) != 0 && string.Compare(info.Name.Replace(" ", ""), name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) != 0) continue;
                    return info;
                }
            }
            return null;
        }
        public PlayerObject GetPlayer(string name)
        {
            for (var i = 0; i < Players.Count; i++)
                if (string.Compare(Players[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return Players[i];

            return null;
        }
        public PlayerObject GetPlayer(uint PlayerId)
        {
            for (var i = 0; i < Players.Count; i++)
                if (Players[i].Info.Index == PlayerId)
                    return Players[i];

            return null;
        }
        public CharacterInfo GetCharacterInfo(string name)
        {
            for (var i = 0; i < CharacterList.Count; i++)
                if (string.Compare(CharacterList[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return CharacterList[i];

            return null;
        }

        public CharacterInfo GetCharacterInfo(int index)
        {
            for (var i = 0; i < CharacterList.Count; i++)
                if (CharacterList[i].Index == index)
                    return CharacterList[i];

            return null;
        }
        public HeroInfo GetHeroInfo(int index)
        {
            return HeroList.FirstOrDefault(x => x.Index == index);
        }

        public ItemInfo GetItemInfo(int index)
        {
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var info = ItemInfoList[i];
                if (info.Index != index) continue;
                return info;
            }
            return null;
        }

        public ItemInfo GetItemInfo(string name)
        {
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var info = ItemInfoList[i];
                if (string.Compare(info.Name.Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase) != 0) continue;
                return info;
            }
            return null;
        }

        public QuestInfo GetQuestInfo(int index)
        {
            return QuestInfoList.FirstOrDefault(info => info.Index == index);
        }

        public ItemInfo GetBook(short Skill)
        {
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var info = ItemInfoList[i];
                if (info.Type != ItemType.技能书 || info.Shape != Skill) continue;
                return info;
            }
            return null;
        }

        public BuffInfo GetBuffInfo(BuffType type)
        {
            for (int i = 0; i < BuffInfoList.Count; i++)
            {
                var info = BuffInfoList[i];
                if (info.Type != type) continue;

                return info;
            }

            throw new NotImplementedException($"{type} 尚未实施");
        }

        public void MessageAccount(AccountInfo account, string message, ChatType type)
        {
            if (account?.Characters == null) return;

            for (var i = 0; i < account.Characters.Count; i++)
            {
                if (account.Characters[i].Player == null) continue;
                account.Characters[i].Player.ReceiveChat(message, type);
                return;
            }
        }


        public void MailCharacter(CharacterInfo info, UserItem item = null, uint gold = 0, int reason = 0, string customMessage = null)
        {
            string sender = "飞天城管理员";

            string message = "由于以下原因，您已收到邮件:\r\n\r\n";

            switch (reason)
            {
                case 1:
                    message += "交易后无法将物品退回到背包中";
                    break;
                case 99:
                    message += "密码不正确不能打开仓库";
                    break;
                default:
                    message += customMessage ?? "未知原因";
                    break;
            }

            MailInfo mail = new MailInfo(info.Index)
            {
                Sender = sender,
                Message = message,
                Gold = gold
            };

            if (item != null)
            {
                mail.Items.Add(item);
            }

            mail.Send();
        }

        public GuildObject GetGuild(string name)
        {
            for (var i = 0; i < Guilds.Count; i++)
            {
                if (string.Compare(Guilds[i].Name.Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase) != 0) continue;

                return Guilds[i];
            }

            return null;
        }
        public GuildObject GetGuild(int index)
        {
            for (var i = 0; i < Guilds.Count; i++)
            {
                if (Guilds[i].Guildindex == index)
                {
                    return Guilds[i];
                }
            }

            return null;
        }

        public void ProcessNewDay()
        {
            foreach (var c in CharacterList)
            {
                ClearDailyQuests(c);

                c.NewDay = true;

                c.Player?.CallDefaultNPC(DefaultNPCType.Daily);
            }
        }

        private void ProcessRentedItems()
        {
            foreach (var characterInfo in CharacterList)
            {
                if (characterInfo.RentedItems.Count <= 0)
                {
                    continue;
                }

                foreach (var rentedItemInfo in characterInfo.RentedItems)
                {
                    if (rentedItemInfo.ItemReturnDate >= Now)
                        continue;

                    var rentingPlayer = GetCharacterInfo(rentedItemInfo.RentingPlayerName);

                    for (var i = 0; i < rentingPlayer.Inventory.Length; i++)
                    {
                        if (rentedItemInfo.ItemId != rentingPlayer?.Inventory[i]?.UniqueID)
                        {
                            continue;
                        }

                        var item = rentingPlayer.Inventory[i];

                        if (item?.RentalInformation == null)
                        {
                            continue;
                        }

                        if (Now <= item.RentalInformation.ExpiryDate)
                        {
                            continue;
                        }

                        ReturnRentalItem(item, item.RentalInformation.OwnerName, rentingPlayer, false);
                        rentingPlayer.Inventory[i] = null;
                        rentingPlayer.HasRentedItem = false;

                        if (rentingPlayer.Player == null)
                        {
                            continue;
                        }

                        rentingPlayer.Player.ReceiveChat($"仓库中 {item.Info.FriendlyName} 已过期", ChatType.Hint);
                        rentingPlayer.Player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                        rentingPlayer.Player.RefreshStats();
                    }

                    for (var i = 0; i < rentingPlayer.Equipment.Length; i++)
                    {
                        var item = rentingPlayer.Equipment[i];

                        if (item?.RentalInformation == null)
                        {
                            continue;
                        }

                        if (Now <= item.RentalInformation.ExpiryDate)
                        {
                            continue;
                        }

                        ReturnRentalItem(item, item.RentalInformation.OwnerName, rentingPlayer, false);
                        rentingPlayer.Equipment[i] = null;
                        rentingPlayer.HasRentedItem = false;

                        if (rentingPlayer.Player == null)
                        {
                            continue;
                        }

                        rentingPlayer.Player.ReceiveChat($"仓库中 {item.Info.FriendlyName} 已过期。", ChatType.Hint);
                        rentingPlayer.Player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                        rentingPlayer.Player.RefreshStats();
                    }
                }
            }

            foreach (var characterInfo in CharacterList)
            {
                if (characterInfo.RentedItemsToRemove.Count <= 0)
                {
                    continue;
                }

                foreach (var rentalInformationToRemove in characterInfo.RentedItemsToRemove)
                {
                    characterInfo.RentedItems.Remove(rentalInformationToRemove);
                }

                characterInfo.RentedItemsToRemove.Clear();
            }
        }

        public bool ReturnRentalItem(UserItem rentedItem, string ownerName, CharacterInfo rentingCharacterInfo, bool removeNow = true)
        {
            if (rentedItem.RentalInformation == null)
            {
                return false;
            }

            var owner = GetCharacterInfo(ownerName);
            var returnItems = new List<UserItem>();

            foreach (var rentalInformation in owner.RentedItems)
            {
                if (rentalInformation.ItemId == rentedItem.UniqueID)
                {
                    owner.RentedItemsToRemove.Add(rentalInformation);
                }
            }
            
            rentedItem.RentalInformation.BindingFlags = BindMode.None;
            rentedItem.RentalInformation.RentalLocked = true;
            rentedItem.RentalInformation.ExpiryDate = rentedItem.RentalInformation.ExpiryDate.AddDays(1);

            returnItems.Add(rentedItem);

            var mail = new MailInfo(owner.Index, true)
            {
                Sender = rentingCharacterInfo.Name,
                Message = rentedItem.Info.FriendlyName,
                Items = returnItems
            };

            mail.Send();

            if (removeNow)
            {
                foreach (var rentalInformationToRemove in owner.RentedItemsToRemove)
                {
                    owner.RentedItems.Remove(rentalInformationToRemove);
                }

                owner.RentedItemsToRemove.Clear();
            }

            return true;
        }

        private void ClearDailyQuests(CharacterInfo info)
        {
            foreach (var quest in QuestInfoList)
            {
                if (quest.Type != QuestType.每日) continue;

                for (var i = 0; i < info.CompletedQuests.Count; i++)
                {
                    if (info.CompletedQuests[i] != quest.Index) continue;

                    info.CompletedQuests.RemoveAt(i);
                } 
            }

            info.Player?.GetCompletedQuests();
        }

        public GuildBuffInfo FindGuildBuffInfo(int Id)
        {
            for (var i = 0; i < Settings.Guild_BuffList.Count; i++)
            {
                if (Settings.Guild_BuffList[i].Id == Id)
                {
                    return Settings.Guild_BuffList[i];
                }
            }

            return null;
        }

        public void ClearGameshopLog()
        {
            Main.GameshopLog.Clear();

            for (var i = 0; i < AccountList.Count; i++)
            {
                for (var f = 0; f < AccountList[i].Characters.Count; f++)
                {
                    AccountList[i].Characters[f].GSpurchases.Clear();
                }
            }

            ResetGS = false;
            MessageQueue.Enqueue("游戏商城购买日志已清除");
        }

        public void Inspect(MirConnection con, uint id)
        {
            if (ObjectID == id) return;

            PlayerObject player = Players.SingleOrDefault(x => x.ObjectID == id || x.Pets.Count(y => y.ObjectID == id && y is HumanWizard) > 0);

            if (player == null) return;
            Inspect(con, player.Info.Index);
        }

        public void Inspect(MirConnection con, int id)
        {
            if (ObjectID == id) return;

            CharacterInfo player = GetCharacterInfo(id);

            // 假人（AI 玩家）不在 CharacterList 里，GetCharacterInfo 查不到；
            // 直接从在线玩家里按 Info.Index 找，否则 Ctrl+点击假人看不到装备。
            if (player == null)
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i].Info.Index == id)
                    {
                        player = Players[i].Info;
                        break;
                    }
                }
            }

            if (player == null) return;

            CharacterInfo Lover = null;
            string loverName = "";

            if (player.Married != 0) Lover = GetCharacterInfo(player.Married);

            if (Lover != null)
            {
                loverName = Lover.Name;
            }

            for (int i = 0; i < player.Equipment.Length; i++)
            {
                UserItem u = player.Equipment[i];
                if (u == null) continue;

                con.CheckItem(u);
            }

            string guildname = "";
            string guildrank = "";
            GuildObject guild = null;
            GuildRank guildRank = null;
            if (player.GuildIndex != -1)
            {
                guild = GetGuild(player.GuildIndex);
                if (guild != null)
                {
                    guildRank = guild.FindRank(player.Name);
                    if (guildRank == null)
                    {
                        guild = null;
                    }
                    else
                    {
                        guildname = guild.Name;
                        guildrank = guildRank.Name;
                    }
                }
            }

            con.Enqueue(new S.PlayerInspect
            {
                Name = player.Name,
                Equipment = player.Equipment,
                GuildName = guildname,
                GuildRank = guildrank,
                Hair = player.Hair,
                Gender = player.Gender,
                Class = player.Class,
                Level = player.Level,
                LoverName = loverName,
                AllowObserve = player.AllowObserve && Settings.AllowObserve
            });
        }

        public void InspectHero(MirConnection con, int id)
        {
            if (ObjectID == id)
            {
                return;
            }

            HeroObject heroObject = Heroes.SingleOrDefault(h => h.ObjectID == id);

            if (heroObject == null)
            {
                return;
            }

            HeroInfo heroInfo = GetHeroInfo(heroObject.Info.Index);

            if (heroInfo == null)
            {
                return;
            }

            for (int i = 0; i < heroInfo.Equipment.Length; i++)
            {
                UserItem u = heroInfo.Equipment[i];

                if (u == null)
                {
                    continue;
                }

                con.CheckItem(u);
            }

            var ownerName = heroObject.Owner.Name;

            con.Enqueue(new S.PlayerInspect
            {
                Name = $"{ownerName}的英雄",
                Equipment = heroInfo.Equipment,
                GuildName = String.Empty,
                GuildRank = String.Empty,
                Hair = heroInfo.Hair,
                Gender = heroInfo.Gender,
                Class = heroInfo.Class,
                Level = heroInfo.Level,
                LoverName = String.Empty,
                AllowObserve = false,
                IsHero = true
            });

        }

        public void Observe(MirConnection con, string Name)
        {
            var player = GetPlayer(Name);

            if (player == null) return;
            if (!player.AllowObserve || !Settings.AllowObserve) return;

            player.AddObserver(con);
        }

        public void GetRanking(MirConnection con, byte RankType, int RankIndex, bool OnlineOnly)
        {
            if (RankType > 6) return;
            List<RankCharacterInfo> listings = RankType == 0 ? RankTop : RankClass[RankType - 1];

            if (RankIndex >= listings.Count || RankIndex < 0) return;

            S.Rankings p = new S.Rankings
            {
                RankType = RankType,
                Count = OnlineOnly ? OnlineRankingCount[RankType] : listings.Count
            };

            if (con.Player != null)
            {
                if (RankType == 0)
                    p.MyRank = con.Player.Info.Rank[0];
                else
                    p.MyRank = (byte)con.Player.Class == (RankType - 1) ? con.Player.Info.Rank[1] : 0;
            }

            int c = 0;
            for (int i = RankIndex; i < listings.Count; i++)
            {
                if (OnlineOnly && GetPlayer(listings[i].Name) == null) continue;

                if (!CheckListing(con, listings[i]))
                    p.ListingDetails.Add(listings[i]);
                p.Listings.Add(listings[i].PlayerId);
                c++;

                if (c > 19 || c >= p.Count) break;
            }

            con.Enqueue(p);
        }

        private bool CheckListing(MirConnection con, RankCharacterInfo listing)
        {
            if (!con.SentRankings.ContainsKey(listing.PlayerId))
            {
                con.SentRankings.Add(listing.PlayerId, listing.LastUpdated);
                return false;
            }

            DateTime lastUpdated = con.SentRankings[listing.PlayerId];
            if (lastUpdated != listing.LastUpdated)
            {
                con.SentRankings[listing.PlayerId] = lastUpdated;
                return false;
            }

            return true;
        }

        public int InsertRank(List<RankCharacterInfo> Ranking, RankCharacterInfo NewRank)
        {
            if (Ranking.Count == 0)
            {
                Ranking.Add(NewRank);
                return Ranking.Count;
            }

            for (var i = 0; i < Ranking.Count; i++)
            {
                //if level is lower
                if (Ranking[i].level < NewRank.level)
                {
                    Ranking.Insert(i, NewRank);
                    return i + 1;
                }

                //if exp is lower but level = same
                if (Ranking[i].level == NewRank.level && Ranking[i].Experience < NewRank.Experience)
                {
                    Ranking.Insert(i, NewRank);
                    return i + 1;
                }
            }

            Ranking.Add(NewRank);
            return Ranking.Count;
        }

        public bool TryAddRank(List<RankCharacterInfo> Ranking, CharacterInfo info, byte type)
        {
            var NewRank = new RankCharacterInfo() { Name = info.Name, Class = info.Class, Experience = info.Experience, level = info.Level, PlayerId = info.Index, info = info, LastUpdated = Now };
            var NewRankIndex = InsertRank(Ranking, NewRank);
            if (NewRankIndex == 0) return false;
            for (var i = NewRankIndex; i < Ranking.Count; i++ )
            {
                SetNewRank(Ranking[i], i + 1, type);
            }
            info.Rank[type] = NewRankIndex;
            return true;
        }

        public int FindRank(List<RankCharacterInfo> Ranking, CharacterInfo info, byte type)
        {
            var startindex = info.Rank[type];
            if (startindex > 0) //if there's a previously known rank then the user can only have gone down in the ranking (or stayed the same)
            {
                for (var i = startindex-1; i < Ranking.Count; i++)
                {
                    if (Ranking[i].Name == info.Name)
                        return i;
                }
                info.Rank[type] = 0;//set the rank to 0 to tell future searches it's not there anymore
            }
            return -1;//index can be 0
        }

        public bool UpdateRank(List<RankCharacterInfo> Ranking, CharacterInfo info, byte type)
        {
            var CurrentRank = FindRank(Ranking, info, type);
            if (CurrentRank == -1) return false;//not in ranking list atm
            
            var NewRank = CurrentRank;
            //next find our updated rank
            for (var i = CurrentRank-1; i >= 0; i-- )
            {
                if (Ranking[i].level > info.Level || Ranking[i].level == info.Level && Ranking[i].Experience > info.Experience) break;
                    NewRank =i;
            }

            Ranking[CurrentRank].level = info.Level;
            Ranking[CurrentRank].Experience = info.Experience;
            Ranking[CurrentRank].LastUpdated = Now;

            if (NewRank < CurrentRank)
            {//if we gained any ranks
                Ranking.Insert(NewRank, Ranking[CurrentRank]);
                Ranking.RemoveAt(CurrentRank + 1);
                for (var i = NewRank + 1; i < Math.Min(Ranking.Count, CurrentRank +1); i++)
                {
                    SetNewRank(Ranking[i], i + 1, type);
                }
            }
            info.Rank[type] = NewRank+1;
            
            return true;
        }

        public void SetNewRank(RankCharacterInfo Rank, int Index, byte type)
        {
            Rank.LastUpdated = Now;
            if (!(Rank.info is CharacterInfo Player)) return;
            Player.Rank[type] = Index;
        }

        public void RemoveRank(CharacterInfo info)
        {
            List<RankCharacterInfo> Ranking;
            var Rankindex = -1;
            //first check overall top           
            Ranking = RankTop;
            Rankindex = FindRank(Ranking, info, 0);
            if (Rankindex >= 0)
            {
                Ranking.RemoveAt(Rankindex);
                for (var i = Rankindex; i < Ranking.Count(); i++)
                {
                    SetNewRank(Ranking[i], i, 0);
                }
            }

            //next class based top
            Ranking = RankTop;
            Rankindex = FindRank(Ranking, info, 1);
            if (Rankindex >= 0)
            {
                Ranking.RemoveAt(Rankindex);
                for (var i = Rankindex; i < Ranking.Count(); i++)
                {
                    SetNewRank(Ranking[i], i, 1);
                }
            }

        }

        public void CheckRankUpdate(CharacterInfo info)
        {
            //假人只计入「在线人数」，不进入排行榜
            if (info != null && info.Player is FakePlayerObject) return;

            List<RankCharacterInfo> Ranking;

            //first check overall top           

            Ranking = RankTop;
            if (!UpdateRank(Ranking, info, 0))
            {
                TryAddRank(Ranking, info, 0);
            }

            //now check class top

            Ranking = RankClass[(byte)info.Class];
            if (!UpdateRank(Ranking, info, 1))
            {
                TryAddRank(Ranking, info, 1);
            }
        }


        public void ReloadNPCs()
        {
            //★11-03：服务端菜单「重新加载 NPC」是在 WinForms 线程上直接调的，
            //  会和工作线程抢 Envir.NPCs / Scripts（重载中途新建的 NPCObject 还没加载脚本，
            //  被主循环 Process 到 → ProcessGoods → NPCScript.Get(0) → KeyNotFoundException '0'）。
            //  统一排到游戏线程执行，和 ReloadMaps 的做法一致。
            if (_thread != null && Thread.CurrentThread != _thread)
            {
                MessageQueue.Enqueue("NPC脚本重载：已排队，下一拍由游戏线程执行");
                RunOnMainThread(ReloadNPCs);
                return;
            }

            SaveGoods(true);

            Robot.Clear();

            var keys = Scripts.Keys;

            foreach (var key in keys)
            {
                // ★在线热重载（@RELOADNPCS）必须容错：单个脚本写错只能自己加载失败，
                //   不能把异常抛到调用链上（那会被 WorkLoop 的 catch 接住 → 踢玩家 + 停服）。
                try
                {
                    Scripts[key].Load();
                }
                catch (Exception ex)
                {
                    MessageQueue.Enqueue(string.Format("NPC脚本加载失败（已跳过）：{0} —— {1}", Scripts[key].FileName, ex.Message));
                }
            }

            MessageQueue.Enqueue("NPC脚本已重新加载");
        }

        /// <summary>
        /// 全量重载 NPC（GM 在线命令 @RELOADALLNPC / 菜单「重新加载 → NPC全量」用它）。
        /// 在 ReloadNPCs（容错重载全部已有脚本，含 #INCLUDE 展开后的新内容）基础上，额外：
        ///   1) 撤下「NPC信息列表里已被删除」的普通 NPC（攻城旗等 Conquest 附属 NPC 不动）；
        ///   2) 把「启动后新加进 NPC 信息列表」的 NPC 现场生成上线（其脚本文件在 NPCObject
        ///      构造时按 FileName 自动加载 —— 所以 00Default 等目录里新加的脚本同样生效）。
        /// 新 NPC 同时写回所属地图的 MapInfo.NPCs 列表，保证下次重启后仍然存在。
        /// </summary>
        public void ReloadAllNPCs()
        {
            //★11-03：同上 —— 菜单线程直接调会边建 NPC 边被主循环遍历，必须回游戏线程
            if (_thread != null && Thread.CurrentThread != _thread)
            {
                MessageQueue.Enqueue("NPC全量重载：已排队，下一拍由游戏线程执行");
                RunOnMainThread(ReloadAllNPCs);
                return;
            }

            ReloadNPCs();

            // 数据源用编辑器实例（Envir.Edit）的列表：编辑器里新增/删除/修改 NPC 后
            // （即使编辑器窗体还没关），这里能立即同步到运行服，不用重启。
            // Main 的 NPCInfoList 只在启动时从 DB 读一次，是"新增 NPC 不重启不上线"的根因。
            //
            // 除全局 NPCInfoList 外，还兼容只存在于 MapInfo.NPCs 里的"地图级 NPC"
            //（用别的工具改地图段、或历史遗留数据时会出现），这类条目 Index 可能为 0，
            // 只能按对象引用识别，因此单独记录。
            List<NPCInfo> editInfos = new List<NPCInfo>(Edit.NPCInfoList);
            HashSet<NPCInfo> mapOnly = new HashSet<NPCInfo>();

            for (int m = 0; m < Edit.MapInfoList.Count; m++)
            {
                MapInfo mi = Edit.MapInfoList[m];
                if (mi?.NPCs == null) continue;

                for (int n = 0; n < mi.NPCs.Count; n++)
                {
                    NPCInfo info = mi.NPCs[n];
                    if (info == null) continue;
                    if (editInfos.Contains(info)) continue;
                    if (info.Index != 0 && editInfos.Exists(x => x.Index == info.Index)) continue;

                    editInfos.Add(info);
                    mapOnly.Add(info);
                }
            }

            HashSet<int> editIds = new HashSet<int>();
            HashSet<NPCInfo> editRefs = new HashSet<NPCInfo>();
            for (int i = 0; i < editInfos.Count; i++)
            {
                if (editInfos[i].Index != 0) editIds.Add(editInfos[i].Index);
                editRefs.Add(editInfos[i]);
            }

            // 攻城旗 / 争夺旗由攻城系统托管：它们用的是运行时临时 NPCInfo、不在任何编辑器数据里，
            // 且创建时 Conq 并未赋值（ConquestGuildFlagInfo.Spawn 只造对象），必须按引用显式保护，
            // 否则每次全量重载都会把旗子撤掉。
            HashSet<NPCObject> guarded = new HashSet<NPCObject>();
            for (int i = 0; i < Conquests.Count; i++)
            {
                ConquestObject conq = Conquests[i];
                if (conq == null || conq.GuildInfo == null) continue;

                for (int j = 0; j < conq.FlagList.Count; j++)
                {
                    ConquestGuildFlagInfo flag = conq.FlagList[j];
                    if (flag != null && flag.Flag != null) guarded.Add(flag.Flag);
                }

                foreach (KeyValuePair<ConquestGuildFlagInfo, Dictionary<GuildObject, int>> kv in conq.ControlPoints)
                {
                    if (kv.Key != null && kv.Key.Flag != null) guarded.Add(kv.Key.Flag);
                }
            }

            // ---- 1) 撤下已在编辑器里删除的 NPC ----
            List<NPCObject> removed = new List<NPCObject>();

            for (int i = NPCs.Count - 1; i >= 0; i--)
            {
                NPCObject npc = NPCs[i];

                if (npc == null || npc.Conq != null) continue;  // 攻城 NPC 由攻城系统管理，不在此处理
                if (npc.Info == null) continue;
                if (guarded.Contains(npc)) continue;            // 攻城旗 / 争夺旗

                // 全局 NPC 按 Index 匹配；地图级 NPC（Index 可能为 0）按对象引用匹配
                if (npc.Info.Index != 0 && editIds.Contains(npc.Info.Index)) continue;
                if (editRefs.Contains(npc.Info)) continue;

                // 运行时唯一的临时 NPC 是攻城旗（已被 Conq 守卫挡住），
                // 其余 Index 为 0 的只可能是地图级 NPC，编辑器里没了就是被删了
                removed.Add(npc);
            }

            foreach (NPCObject npc in removed)
            {
                npc.CurrentMap?.RemoveObject(npc);
                npc.Despawn();
                NPCs.Remove(npc);

                if (npc.Info.Index != 0)
                    NPCInfoList.RemoveAll(x => x.Index == npc.Info.Index); // 信息列表一并移除

                MessageQueue.Enqueue(string.Format("NPC全量重载：撤下 {0}（地图 {1} 坐标 {2}）",
                    string.IsNullOrEmpty(npc.Info.Name) ? "(无名)" : npc.Info.Name,
                    npc.Info.MapIndex, npc.Info.Location));
            }

            // ---- 2) 同步新增/修改的 NPC ----
            int added = 0, rebuilt = 0, skipped = 0;

            for (int i = 0; i < editInfos.Count; i++)
            {
                NPCInfo info = editInfos[i];
                bool isMapOnly = mapOnly.Contains(info);

                int listIdx = -1;
                NPCInfo old = null;

                if (!isMapOnly && info.Index != 0)
                {
                    listIdx = NPCInfoList.FindIndex(x => x.Index == info.Index);
                    if (listIdx >= 0) old = NPCInfoList[listIdx];
                }

                // 找到场上对应的对象：全局的靠 Info 引用（Main 侧的旧对象），地图级的靠自身引用
                NPCObject existing = old != null
                    ? NPCs.FirstOrDefault(x => x.Info == old && x.Conq == null)
                    : NPCs.FirstOrDefault(x => x.Info == info && x.Conq == null);

                if (existing != null && old != null && SameConfig(old, info)) continue; // 配置没变，跳过
                if (existing != null && old == null) continue;                          // 已在场，无需重复生成

                string who = string.IsNullOrEmpty(info.Name) ? "(无名)" : info.Name;

                Map map = GetMap(info.MapIndex);
                if (map == null)
                {
                    MessageQueue.Enqueue(string.Format("NPC全量重载：跳过 {0} —— 所属地图 {1} 不存在", who, info.MapIndex));
                    skipped++;
                    continue;
                }

                if (map.ValidPoint(info.Location))
                {
                    // ok
                }
                else
                {
                    MessageQueue.Enqueue(string.Format("NPC全量重载：跳过 {0} —— 坐标不可站立（地图 {1} {2},{3}），请换到可走的格子",
                        who, info.MapIndex, info.Location.X, info.Location.Y));
                    skipped++;
                    continue;
                }

                // 修改场景：撤下旧对象（位置/外观/脚本名等变了需要重建）
                if (existing != null)
                {
                    existing.CurrentMap?.RemoveObject(existing);
                    existing.Despawn();
                    NPCs.Remove(existing);
                    rebuilt++;

                    Map oldMap = GetMap(old.MapIndex);
                    oldMap?.Info?.NPCs.Remove(old);
                }

                if (!isMapOnly && info.Index != 0)
                {
                    if (listIdx >= 0) NPCInfoList[listIdx] = info;
                    else
                    {
                        NPCInfoList.Add(info);
                        added++;
                    }
                }

                // 写回地图的 NPC 列表，保证下次重启后仍会生成
                if (!map.Info.NPCs.Contains(info)) map.Info.NPCs.Add(info);

                // 与 Map.Load 相同的生成方式：构造时按 FileName 自动加载脚本（新脚本文件由此生效）
                map.AddObject(new NPCObject(info) { CurrentMap = map });

                if (info.Image == 0 || string.IsNullOrEmpty(info.FileName))
                    MessageQueue.Enqueue(string.Format("NPC全量重载：{0} 已上线，但{1}为{2}，游戏里可能看不见或没有对话",
                        who, info.Image == 0 ? "外观图号" : "脚本名", info.Image == 0 ? "0" : "空"));
            }

            MessageQueue.Enqueue(string.Format("NPC全量重载完成：新上线 {0} 个，重建 {1} 个，撤下 {2} 个，跳过 {3} 个（共扫描 {4} 条）",
                added, rebuilt, removed.Count, skipped, editInfos.Count));
        }

        /// <summary>
        /// 重载刷怪配置（合并式）：数据源 = 编辑器实例（Edit.MapInfoList）里每张图的 Respawns。
        /// 配置没变的刷怪点沿用原 MapRespawn 对象，保住 Count 计数（场上活着的怪不会导致重复刷满）；
        /// 新增的刷怪点下一轮计时自动刷；被删掉的只摘配置，场上的怪不杀，照常活动到被打死。
        /// </summary>
        public void ReloadRespawns()
        {
            int mapCount = 0, added = 0, kept = 0, removed = 0, skipped = 0;

            for (int i = 0; i < MapList.Count; i++)
            {
                Map map = MapList[i];
                if (map == null || map.Info == null) continue;

                MapInfo editMap = null;
                for (int e = 0; e < Edit.MapInfoList.Count; e++)
                {
                    if (Edit.MapInfoList[e].Index != map.Info.Index) continue;
                    editMap = Edit.MapInfoList[e];
                    break;
                }

                if (editMap == null) continue; // 编辑器里没有这张图（运行时临时图等），保持不动

                mapCount++;

                List<RespawnInfo> newInfos = editMap.Respawns ?? new List<RespawnInfo>();
                List<MapRespawn> merged = new List<MapRespawn>(newInfos.Count);

                for (int j = 0; j < newInfos.Count; j++)
                {
                    RespawnInfo info = newInfos[j];
                    if (info == null) continue;

                    MapRespawn existing = null;
                    for (int k = 0; k < map.Respawns.Count; k++)
                    {
                        if (!SameRespawnKey(map.Respawns[k].Info, info)) continue;
                        existing = map.Respawns[k];
                        break;
                    }

                    if (existing != null)
                    {
                        // 同一个刷怪点：沿用对象（Count/计时都在里面），只把配置换成最新的
                        bool routeChanged = !string.Equals(existing.Info.RoutePath ?? string.Empty, info.RoutePath ?? string.Empty, StringComparison.Ordinal);

                        existing.Info = info;

                        //★11-04 阶段一：Spread 变化不需要任何重算 —— 出生点是取点时按方框现场采样的，
                        //  不再有「可走点副本」需要刷新（原来这里要调 FillRespawnCells）。
                        if (routeChanged) existing.LoadRoutes();

                        if (!merged.Contains(existing)) merged.Add(existing);
                        kept++;
                        continue;
                    }

                    MapRespawn created = new MapRespawn(info) { Map = map };

                    if (created.Monster == null)
                    {
                        MessageQueue.Enqueue(string.Format("刷怪重载：跳过（地图 {0}）怪物编号 {1} 不存在于怪物库（坐标 {2},{3}）",
                            map.Info.Index, info.MonsterIndex, info.Location.X, info.Location.Y));
                        skipped++;
                        continue;
                    }

                    if (created.GetSpawnPoint(map).X < 0)
                    {
                        MessageQueue.Enqueue(string.Format("刷怪重载：警告（地图 {0}）怪物 {1} 在坐标 {2},{3} 范围 {4} 内没有可走格子，可能刷不出来",
                            map.Info.Index, created.Monster.Name, info.Location.X, info.Location.Y, info.Spread));
                    }

                    merged.Add(created);
                    added++;
                }

                for (int k = 0; k < map.Respawns.Count; k++)
                    if (!merged.Contains(map.Respawns[k])) removed++;

                // 已删除刷怪点的存档计时清理；场上已存在的怪不杀，它们死亡时对摘掉的条目减计数，无副作用
                for (int k = SavedSpawns.Count - 1; k >= 0; k--)
                    if (SavedSpawns[k].Map == map && !merged.Contains(SavedSpawns[k])) SavedSpawns.RemoveAt(k);

                map.Respawns = merged;

                // DB 保存时写新配置（保留原列表实例，避免外部持有引用失效）
                map.Info.Respawns.Clear();
                map.Info.Respawns.AddRange(newInfos);

                for (int k = 0; k < merged.Count; k++)
                {
                    MapRespawn r = merged[k];
                    if (!r.Info.SaveRespawnTime || r.Info.RespawnTicks == 0) continue;
                    if (!SavedSpawns.Contains(r)) SavedSpawns.Add(r);
                }
            }

            MessageQueue.Enqueue(string.Format("刷怪配置已重载：{0} 张地图，新增 {1} 个刷怪点，沿用 {2} 个，移除 {3} 个，跳过 {4} 个",
                mapCount, added, kept, removed, skipped));
        }

        /// <summary>
        /// 刷怪点身份判定：怪物 + 坐标 + 范围 + 路线。数量/延迟/刷新间隔变了算同一个点（就地更新）。
        /// </summary>
        private static bool SameRespawnKey(RespawnInfo a, RespawnInfo b)
        {
            if (a == null || b == null) return false;

            return a.MonsterIndex == b.MonsterIndex
                && a.Location.Equals(b.Location)
                && a.Spread == b.Spread
                && string.Equals(a.RoutePath ?? string.Empty, b.RoutePath ?? string.Empty, StringComparison.Ordinal);
        }

        //★11-04 阶段一：FillRespawnCells 已删除。
        //  它和 Map.Load 里那段是同一算法的两份拷贝，作用是给每个刷怪点生成一份「可走点副本」
        //  （全服 1.44 GB）。现在取点统一走 MapRespawn.GetSpawnPoint(map) 现场采样，
        //  两处调用点一并去掉，热重载路径与启动路径行为一致。

        #region 地图热重载（免重启加新图 / 老图配置刷新）

        /// <summary>
        /// 重载地图（菜单「重新加载 → 重载地图」/ GM 命令 @RELOADMAP）。
        ///
        /// A. 新图：编辑器里新增、运行服还没有的地图 → 现场读 .map 建图上线（格子/刷怪点/NPC/安全区/矿区全部按启动流程生成）；
        ///    建好后自动顺带同步刷怪点与 NPC，不用再点另外两个菜单。
        /// B. 老图：只刷新「配置」（标题/大小地图/音乐/光照/各种禁制开关/火雷伤害/天气/安全区/传送点/矿区），
        ///    格子(Cells) 与在场玩家对象一律不动 —— 不掉线、不位移、不重置刷怪计时。
        ///    这就是「新图加好了、老图上的传送点也要能立刻传过去」所必需的那一步：传送点(Movements)是点击时实时读的，
        ///    配置刷新后老图上的传送口立刻就能进新图。
        ///
        /// 明确不做的三件事（会依次在日志里提示）：
        ///   1) 编辑器里删掉的地图不会从运行服摘掉（摘除牵扯在场玩家/攻城/存档），需重启生效；
        ///   2) 老图的 .map 地形文件改过（或 FileName 改了）不重建格子，需重启生效；
        ///   3) 地图显示类参数（标题/小地图/大地图/音乐/光照）已生效，但客户端要重新进图才会看到。
        /// </summary>
        public void ReloadMaps()
        {
            // 一律排到"工作循环下一拍的开头"再执行：
            //   · 菜单按钮在 UI 线程点下来 —— 不能和主循环抢 MapList/Objects；
            //   · GM 命令在工作线程执行，但那时正处于"遍历 Objects 处理每个对象"的循环里，
            //     重载会新建/撤下 NPC（增删 Envir.Objects 链表节点），边遍历边删会踩空。
            // 放到每拍开头（对象遍历之外）执行，两种来源都安全。
            if (_thread != null && !_executingMainThreadTask)
            {
                EnqueueMainThreadTask(ReloadMaps);
                MessageQueue.Enqueue("重载地图：已提交，将在本拍对象处理结束后执行（结果见后续日志）");
                return;
            }

            if (!Running)
            {
                MessageQueue.Enqueue("重载地图：服务器未在运行，已忽略");
                return;
            }

            // 编辑器侧数据没加载时，整份列表为空 —— 直接退出，免得到处误报"地图已被删除"
            if (Edit.MapInfoList.Count == 0)
            {
                MessageQueue.Enqueue("重载地图：编辑器里的地图列表是空的（数据库未在编辑器加载？），已忽略");
                return;
            }

            int created = 0, refreshed = 0, unchanged = 0, skipped = 0, failed = 0;

            // 这次新上线的地图：等 NPC / 刷怪点都同步完再做体检，免得把"还没轮到补"的情况误报成生成失败
            List<Map> newMaps = new List<Map>();

            // 编辑器现存的地图编号（用来判断"运行服上有、编辑器里已经没有了"的孤儿图）
            HashSet<int> editIds = new HashSet<int>();
            for (int e = 0; e < Edit.MapInfoList.Count; e++)
            {
                MapInfo mi = Edit.MapInfoList[e];
                if (mi != null) editIds.Add(mi.Index);
            }

            for (int e = 0; e < Edit.MapInfoList.Count; e++)
            {
                MapInfo edit = Edit.MapInfoList[e];
                if (edit == null) continue;

                string who = string.IsNullOrEmpty(edit.Title) ? "(无名地图)" : edit.Title;

                Map runtime = GetMap(edit.Index);

                // ---- 已有地图：只刷新配置 ----
                if (runtime != null)
                {
                    if (!string.Equals(runtime.Info.FileName, edit.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageQueue.Enqueue(string.Format(
                            "重载地图：{0}（编号 {1}）的地图文件从 {2} 改成了 {3} —— 地形不能热换，本次只刷新其它配置，地形改动需重启",
                            who, edit.Index, runtime.Info.FileName, edit.FileName));
                    }

                    bool zonesChanged, movementsChanged, minesChanged;

                    string oldTitle = runtime.Info.Title;
                    ushort oldMini = runtime.Info.MiniMap, oldBig = runtime.Info.BigMap, oldMusic = runtime.Info.Music;
                    LightSetting oldLight = runtime.Info.Light;

                    bool anyChanged = runtime.Info.CopyConfigFrom(edit, out zonesChanged, out movementsChanged, out minesChanged);

                    bool displayChanged = oldTitle != runtime.Info.Title || oldMini != runtime.Info.MiniMap
                        || oldBig != runtime.Info.BigMap || oldMusic != runtime.Info.Music || oldLight != runtime.Info.Light;

                    if (zonesChanged)
                    {
                        // 安全区改了：撤掉旧的边框/回血法术 → 重建 → 同步全服出生点
                        runtime.RebuildSafeZones();
                        RebuildStartPoints(runtime);
                    }

                    if (minesChanged)
                    {
                        // 矿区重建要按 MineSet 配置铺格子，配置越界时不能把异常抛出去（会打断这一拍工作循环）
                        try
                        {
                            runtime.RebuildMine();
                        }
                        catch (Exception ex)
                        {
                            MessageQueue.Enqueue(string.Format("重载地图：{0}（编号 {1}）矿区重建失败（检查矿区设置编号是否超出 MineSet 数量）：{2}",
                                who, edit.Index, ex.Message));
                        }
                    }

                    if (anyChanged)
                    {
                        refreshed++;

                        string detail = string.Format("{0}{1}{2}",
                            zonesChanged ? "安全区 " : string.Empty,
                            movementsChanged ? "传送点 " : string.Empty,
                            minesChanged ? "矿区 " : string.Empty);

                        MessageQueue.Enqueue(string.Format("重载地图：{0}（编号 {1}）配置已刷新{2}",
                            who, edit.Index,
                            string.IsNullOrEmpty(detail) ? "（显示参数/开关/伤害等）" : "（" + detail.Trim() + "）"));

                        if (displayChanged)
                        {
                            MessageQueue.Enqueue(string.Format(
                                "  提示：{0}（编号 {1}）的标题/小地图/大地图/音乐/光照改动已生效，客户端需要重新进图（或重登）才看得到",
                                who, edit.Index));
                        }
                    }
                    else
                    {
                        unchanged++;
                    }

                    continue;
                }

                // ---- 新图：先挡掉"删掉又重建"的重复风险 ----
                // 编辑器里删掉地图再加一张同文件名的，会拿到新编号；运行服上还挂着旧编号那张，
                // 直接建新图会让同一份 .map 变成两个运行实例（刷怪/NPC 双份）。这种情况只能重启。
                Map ghost = null;
                for (int m = 0; m < MapList.Count; m++)
                {
                    Map rm = MapList[m];
                    if (rm?.Info == null) continue;
                    if (editIds.Contains(rm.Info.Index)) continue;   // 编辑器里还有，是另一张正常的复用图
                    if (!string.Equals(rm.Info.FileName, edit.FileName, StringComparison.OrdinalIgnoreCase)) continue;

                    ghost = rm;
                    break;
                }

                if (ghost != null)
                {
                    MessageQueue.Enqueue(string.Format(
                        "重载地图：跳过 {0}（编号 {1}）—— 运行服上已有同文件名的地图（编号 {2}，可能是删掉后重建的），为避免刷怪/NPC 重复，请重启",
                        who, edit.Index, ghost.Info.Index));
                    skipped++;
                    continue;
                }

                string error;
                Map map = CreateMapForHotReload(edit, out error);

                if (map == null)
                {
                    MessageQueue.Enqueue(string.Format("重载地图：{0}（编号 {1}）加载失败 —— {2}", who, edit.Index, error));
                    failed++;
                    continue;
                }

                created++;

                MessageQueue.Enqueue(string.Format(
                    "重载地图：新图 {0}（编号 {1}，文件 {2}.map）已上线 —— {3}x{4}，刷怪点 {5} 个，NPC {6} 个，出生点 {7} 个",
                    who, edit.Index, edit.FileName, map.Width, map.Height,
                    map.Respawns.Count, map.NPCs.Count, CountStartPoints(map)));

                newMaps.Add(map);

                MessageQueue.Enqueue("  提示：客户端 Map 目录下必须有同名 .map 文件（走微端的话要放进资源目录），否则玩家进图会黑屏/报错");
                MessageQueue.Enqueue("  提示：地图定义已进内存，重启前请在数据库窗口保存一次，否则重启后这张图会消失");

                // 攻城地图的 Conquest 对象是启动时一次性挂的，热加的图挂不上去
                for (int c = 0; c < ConquestInfoList.Count; c++)
                {
                    ConquestInfo ci = ConquestInfoList[c];
                    if (ci == null || ci.MapIndex != edit.Index) continue;

                    MessageQueue.Enqueue(string.Format(
                        "  注意：这张图是攻城地图「{0}」，攻城系统未随热重载挂载，攻城玩法需重启后生效", ci.Name));
                    break;
                }
            }

            // 已删除的地图：只提示，不动运行服
            for (int m = 0; m < MapList.Count; m++)
            {
                Map rm = MapList[m];
                if (rm?.Info == null) continue;
                if (editIds.Contains(rm.Info.Index)) continue;

                MessageQueue.Enqueue(string.Format(
                    "重载地图：地图 {0}（编号 {1}）在编辑器里已删除，但运行服不会移除（会牵扯在场玩家与存档），重启后才会消失",
                    string.IsNullOrEmpty(rm.Info.Title) ? "(无名地图)" : rm.Info.Title, rm.Info.Index));
            }

            MessageQueue.Enqueue(string.Format(
                "重载地图完成：新上线 {0} 张，配置刷新 {1} 张，无变化 {2} 张，跳过 {3} 张，失败 {4} 张（共扫描 {5} 条）",
                created, refreshed, unchanged, skipped, failed, Edit.MapInfoList.Count));

            if (created > 0)
            {
                // 新图自带刷怪点/NPC：直接走现成的两个重载，把它们按运行服对象补齐（已存在的按引用/编号识别，不会重复生成）
                ReloadRespawns();
                ReloadAllNPCs();

                // 都补齐之后再体检：这时还缺的，才是真的坐标站不住 / 怪物编号不存在
                for (int i = 0; i < newMaps.Count; i++)
                    ReportMapSpawnGaps(newMaps[i]);
            }
        }

        /// <summary>
        /// 现场建一张运行服地图（新图热加载用）。与 MapInfo.CreateMap 的区别：
        ///   1) 不在编辑器实例上建，而是先 Clone 一份运行服副本 —— 运行服和编辑器绝不能共用同一个 MapInfo；
        ///   2) NPC 信息换成运行服 NPCInfoList 里的同一实例（新 NPC 登记进去），
        ///      否则随后的 NPC 全量重载会把它们当"新 NPC"再生成一遍，地图上出现双份 NPC；
        ///   3) 失败时回滚刚登记的 NPC 信息，不留下"所属地图不存在"的孤儿。
        /// </summary>
        private Map CreateMapForHotReload(MapInfo edit, out string error)
        {
            error = string.Empty;

            MapInfo info = edit.Clone();

            List<NPCInfo> bound = new List<NPCInfo>(info.NPCs.Count);
            List<NPCInfo> registered = new List<NPCInfo>();

            for (int i = 0; i < info.NPCs.Count; i++)
            {
                NPCInfo n = info.NPCs[i];
                if (n == null) continue;

                NPCInfo main = n.Index != 0 ? NPCInfoList.FirstOrDefault(x => x.Index == n.Index) : null;

                if (main != null)
                {
                    bound.Add(main);
                    continue;
                }

                if (n.Index != 0 && !NPCInfoList.Contains(n))
                {
                    NPCInfoList.Add(n);
                    registered.Add(n);
                }

                bound.Add(n);
            }

            info.NPCs = bound;

            Map map = new Map(info);

            if (!map.Load())
            {
                for (int i = 0; i < registered.Count; i++) NPCInfoList.Remove(registered[i]);

                // 半途失败时 Map 对象会被丢掉，但它的刷怪点可能已经被登记进存档列表 —— 一并清掉
                for (int i = SavedSpawns.Count - 1; i >= 0; i--)
                    if (SavedSpawns[i].Map == map) SavedSpawns.RemoveAt(i);

                error = string.Format("无法读入地图文件 {0}.map（检查服务器 Maps 目录）",
                    info.FileName);
                return null;
            }

            // 同名编号若残留在 MapInfoList（启动时加载失败的那张），用新副本顶掉，避免出现两条同编号记录
            MapInfo stale = MapInfoList.FirstOrDefault(x => x.Index == info.Index);
            if (stale != null) MapInfoList.Remove(stale);

            MapInfoList.Add(info);
            if (info.Index > MapIndex) MapIndex = info.Index;

            MapList.Add(map);

            for (int i = 0; i < info.SafeZones.Count; i++)
                if (info.SafeZones[i] != null && info.SafeZones[i].StartPoint) StartPoints.Add(info.SafeZones[i]);

            return map;
        }

        /// <summary>
        /// 老图安全区变了以后重建全服出生点：StartPoints 里存的是 SafeZoneInfo 对象引用，
        /// 不跟着换的话，新角色/随机传送还会往旧安全区坐标落。
        /// </summary>
        private void RebuildStartPoints(Map map)
        {
            StartPoints.RemoveAll(x => x == null || x.Info == null || x.Info.Index == map.Info.Index);

            for (int i = 0; i < map.Info.SafeZones.Count; i++)
            {
                SafeZoneInfo szi = map.Info.SafeZones[i];
                if (szi != null && szi.StartPoint) StartPoints.Add(szi);
            }
        }

        private static int CountStartPoints(Map map)
        {
            int count = 0;

            for (int i = 0; i < map.Info.SafeZones.Count; i++)
                if (map.Info.SafeZones[i] != null && map.Info.SafeZones[i].StartPoint) count++;

            return count;
        }

        /// <summary>
        /// 新图体检：把"配了但没生成出来"的刷怪点/NPC 直接报出来，
        /// 免得玩家进去发现是空图再回头猜原因。
        /// </summary>
        private void ReportMapSpawnGaps(Map map)
        {
            if (map.Info == null) return;

            int badNPC = 0;
            for (int i = 0; i < map.Info.NPCs.Count; i++)
            {
                NPCInfo info = map.Info.NPCs[i];
                if (info == null) continue;

                bool spawned = false;
                for (int j = 0; j < map.NPCs.Count; j++)
                {
                    if (map.NPCs[j].Info != info) continue;
                    spawned = true;
                    break;
                }

                if (spawned) continue;

                badNPC++;
                MessageQueue.Enqueue(string.Format("  警告：NPC {0} 未生成 —— 地图 {1} 上坐标 {2},{3} 不可站立（或者没有刷在该格）",
                    string.IsNullOrEmpty(info.Name) ? "(无名)" : info.Name, map.Info.Index,
                    info.Location.X, info.Location.Y));
            }

            int badRespawn = 0;
            for (int i = 0; i < map.Info.Respawns.Count; i++)
            {
                RespawnInfo ri = map.Info.Respawns[i];
                if (ri == null) continue;

                bool ok = false;
                for (int j = 0; j < map.Respawns.Count; j++)
                {
                    if (!SameRespawnKey(map.Respawns[j].Info, ri)) continue;
                    ok = true;
                    break;
                }

                if (ok) continue;

                badRespawn++;
                MessageQueue.Enqueue(string.Format("  警告：刷怪点未生效（地图 {0}）怪物编号 {1}，坐标 {2},{3} —— 怪物库里没有这个编号",
                    map.Info.Index, ri.MonsterIndex, ri.Location.X, ri.Location.Y));
            }

            if (badNPC > 0 || badRespawn > 0)
                MessageQueue.Enqueue(string.Format("  体检结果：{0} 个 NPC、{1} 个刷怪点没生成，请按上面提示改坐标/编号", badNPC, badRespawn));
        }

        #endregion

        /// <summary>
        /// 对比两个 NPCInfo 的全部配置字段（不含 Index，调用前已保证相等）。
        /// 用于全量重载判断编辑器里的改动是否需要重建 NPC 对象。
        /// </summary>
        private static bool SameConfig(NPCInfo a, NPCInfo b)
        {
            return a.FileName == b.FileName && a.Name == b.Name
                && a.MapIndex == b.MapIndex && a.Location.Equals(b.Location)
                && a.Rate == b.Rate && a.Image == b.Image && a.Colour.Equals(b.Colour)
                && a.TimeVisible == b.TimeVisible
                && a.HourStart == b.HourStart && a.MinuteStart == b.MinuteStart
                && a.HourEnd == b.HourEnd && a.MinuteEnd == b.MinuteEnd
                && a.MinLev == b.MinLev && a.MaxLev == b.MaxLev
                && a.DayofWeek == b.DayofWeek && a.ClassRequired == b.ClassRequired
                && a.Sabuk == b.Sabuk && a.FlagNeeded == b.FlagNeeded && a.Conquest == b.Conquest
                && a.ShowOnBigMap == b.ShowOnBigMap && a.BigMapIcon == b.BigMapIcon
                && a.CanTeleportTo == b.CanTeleportTo && a.ConquestVisible == b.ConquestVisible
                && a.CollectQuestIndexes.SequenceEqual(b.CollectQuestIndexes)
                && a.FinishQuestIndexes.SequenceEqual(b.FinishQuestIndexes);
        }

        /// <summary>
        /// 重新从数据库文件加载商城列表（GM 在线命令 @RELOADGAMESHOP / 菜单用它）。
        /// 按启动时相同的二进制顺序读到 GameShop 段为止（前面的段只用于推进读取位置，
        /// 构造出的对象全部丢弃），重建 GameShopList 并绑定到当前物品表。
        /// 读取失败时保留原列表不动。
        /// </summary>
        public void ReloadGameShop()
        {
            // 数据源用编辑器实例（Envir.Edit）的商城列表：编辑器里加/删/改商品后
            // （即使窗体还没关），点重载立即同步到运行服，并全服推送刷新客户端。
            // Main 的 GameShopList 只在启动时从 DB 读一次，是"重载后还是旧数据"的根因。
            List<GameShopItem> newList = new List<GameShopItem>();
            int unbound = 0;

            for (int i = 0; i < Edit.GameShopList.Count; i++)
            {
                GameShopItem src = Edit.GameShopList[i];

                // Info 必须重新绑定到运行服（Main）的物品表：编辑器条目指向 Edit 的 ItemInfo 实例
                ItemInfo bound = null;
                for (int j = 0; j < ItemInfoList.Count; j++)
                {
                    if (ItemInfoList[j].Index != src.ItemIndex) continue;
                    bound = ItemInfoList[j];
                    break;
                }

                if (bound == null) { unbound++; continue; }

                newList.Add(new GameShopItem
                {
                    ItemIndex = src.ItemIndex,
                    GIndex = src.GIndex,
                    Info = bound,
                    GoldPrice = src.GoldPrice,
                    CreditPrice = src.CreditPrice,
                    Count = src.Count,
                    Class = src.Class,
                    Category = src.Category,
                    Stock = src.Stock,
                    iStock = src.iStock,
                    Deal = src.Deal,
                    TopItem = src.TopItem,
                    Date = src.Date,
                    CanBuyGold = src.CanBuyGold,
                    CanBuyCredit = src.CanBuyCredit,
                });
            }

            GameShopList.Clear();
            GameShopList.AddRange(newList);

            MessageQueue.Enqueue(string.Format("商城已重新加载，共 {0} 条商品{1}",
                GameShopList.Count, unbound > 0 ? string.Format("（{0} 条物品在物品库中未找到，已跳过）", unbound) : ""));

            // 全服推送：先 Reset 清空客户端本地缓存，再全量下发
            for (int i = 0; i < Players.Count; i++)
            {
                PlayerObject online = Players[i];
                if (online == null) continue;

                online.Enqueue(new S.GameShopInfo { Reset = true });
                online.GetGameShop();
            }
        }

        /// <summary>
        /// 重载物品库：与编辑器实例（Envir.Edit）的物品列表按 Index 同步。
        /// ItemInfo 为纯数据类（无跨环境引用），条目直接采用编辑器实例；
        /// 已有物品（UserItem.Info）继续指向旧实例、属性不变，新生成的物品用新数据。
        /// </summary>
        public void ReloadItems()
        {
            int added = 0, removed = 0;

            HashSet<int> editIds = new HashSet<int>();
            for (int i = 0; i < Edit.ItemInfoList.Count; i++) editIds.Add(Edit.ItemInfoList[i].Index);

            for (int i = 0; i < Edit.ItemInfoList.Count; i++)
            {
                ItemInfo src = Edit.ItemInfoList[i];
                int idx = ItemInfoList.FindIndex(x => x.Index == src.Index);
                if (idx >= 0) ItemInfoList[idx] = src;
                else { ItemInfoList.Add(src); added++; }
            }

            for (int i = ItemInfoList.Count - 1; i >= 0; i--)
            {
                if (editIds.Contains(ItemInfoList[i].Index)) continue;
                ItemInfoList.RemoveAt(i);
                removed++;
            }

            // 商城条目的 Info 重新绑定到新物品实例
            for (int i = 0; i < GameShopList.Count; i++)
            {
                GameShopItem gs = GameShopList[i];
                ItemInfo bound = ItemInfoList.FirstOrDefault(x => x.Index == gs.ItemIndex);
                if (bound != null) gs.Info = bound;
            }

            MessageQueue.Enqueue(string.Format("物品库已重载：新增 {0}，删除 {1}，其余已同步（已有物品属性不变，新物品即生效）", added, removed));
        }

        /// <summary>
        /// 重载怪物库：与编辑器实例（Envir.Edit）的怪物列表按 Index 同步。
        /// 已刷出的怪物继续用旧属性直到死亡/重刷，之后新刷出的生效；
        /// 掉落表对新条目重新加载（绑定到 Main 的物品实例）。
        /// </summary>
        public void ReloadMonsters()
        {
            int added = 0, removed = 0;

            HashSet<int> editIds = new HashSet<int>();
            for (int i = 0; i < Edit.MonsterInfoList.Count; i++) editIds.Add(Edit.MonsterInfoList[i].Index);

            for (int i = 0; i < Edit.MonsterInfoList.Count; i++)
            {
                MonsterInfo src = Edit.MonsterInfoList[i];
                int idx = MonsterInfoList.FindIndex(x => x.Index == src.Index);
                if (idx >= 0) MonsterInfoList[idx] = src;
                else { MonsterInfoList.Add(src); added++; }
            }

            for (int i = MonsterInfoList.Count - 1; i >= 0; i--)
            {
                if (editIds.Contains(MonsterInfoList[i].Index)) continue;
                MonsterInfoList.RemoveAt(i);
                removed++;
            }

            // 掉落表对新条目重新加载；容错处理——掉落文件异常不能炸掉管理端 UI 线程
            try
            {
                ReloadDrops();
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue(string.Format("怪物掉落重载失败（怪物数据已同步）：{0}", ex.Message));
            }

            MessageQueue.Enqueue(string.Format("怪物库已重载：新增 {0}，删除 {1}，掉落已重载（已刷出的怪物保持原属性，新刷出的生效）", added, removed));
        }

        /// <summary>
        /// 重载技能库：与编辑器实例（Envir.Edit）的技能列表按 Spell 同步（MagicInfo 无 Index）。
        /// 在线玩家已学技能重新绑定到新数据（等级/经验保留，威力/延迟/消耗立即生效）。
        /// </summary>
        public void ReloadMagics()
        {
            int added = 0, removed = 0, rebound = 0;

            HashSet<Spell> editSpells = new HashSet<Spell>();
            for (int i = 0; i < Edit.MagicInfoList.Count; i++) editSpells.Add(Edit.MagicInfoList[i].Spell);

            for (int i = 0; i < Edit.MagicInfoList.Count; i++)
            {
                MagicInfo src = Edit.MagicInfoList[i];
                int idx = MagicInfoList.FindIndex(x => x.Spell == src.Spell);
                if (idx >= 0) MagicInfoList[idx] = src;
                else { MagicInfoList.Add(src); added++; }
            }

            for (int i = MagicInfoList.Count - 1; i >= 0; i--)
            {
                if (editSpells.Contains(MagicInfoList[i].Spell)) continue;
                MagicInfoList.RemoveAt(i);
                removed++;
            }

            for (int p = 0; p < Players.Count; p++)
            {
                PlayerObject player = Players[p];
                if (player == null || player.Info == null) continue;

                for (int m = 0; m < player.Info.Magics.Count; m++)
                {
                    UserMagic magic = player.Info.Magics[m];
                    MagicInfo bound = MagicInfoList.FirstOrDefault(x => x.Spell == magic.Spell);
                    if (bound == null || bound == magic.Info) continue;
                    magic.Info = bound;
                    rebound++;
                }
            }

            MessageQueue.Enqueue(string.Format("技能库已重载：新增 {0}，删除 {1}，在线玩家技能重绑 {2} 个", added, removed, rebound));
        }

        /// <summary>
        /// 重新加载 Configs\BuffSystem.ini（GM 在线命令 @RELOADBUFF 用它）。
        ///
        /// 语义：配置里的「自定义槽位 BUFF」内容完全来自配置文件，配置一变就必须重挂，
        /// 所以先把在线玩家身上的自定义 BUFF 清掉；已有 BUFF 只把 Info 指向新表
        /// （属性的变化要等下次重新施放才生效 —— 因为没法从 buff.Stats 里区分
        ///  「代码给的」和「配置给的」，硬改会把技能本身的加成一起抹掉）。
        /// </summary>
        public void ReloadBuffConfig()
        {
            BuffInfoList.Clear();

            // BuffInfo.Load() 内部会先调 BuffConfig.Load()
            foreach (var buff in BuffInfo.Load())
            {
                BuffInfoList.Add(buff);
            }

            for (int i = 0; i < Players.Count; i++)
            {
                PlayerObject player = Players[i];

                if (player == null) continue;

                try
                {
                    for (int j = player.Buffs.Count - 1; j >= 0; j--)
                    {
                        if (j >= player.Buffs.Count) continue;

                        Buff buff = player.Buffs[j];

                        BuffDef def = BuffConfig.Get(buff.Type);

                        if (def != null && def.IsCustom)
                        {
                            player.RemoveBuff(buff.Type);
                            continue;
                        }

                        try
                        {
                            BuffInfo info = GetBuffInfo(buff.Type);
                            if (info != null) buff.Info = info;
                        }
                        catch
                        {
                            // 配置里删掉了某个枚举的注册时忽略，不影响其他 buff
                        }
                    }

                    player.RefreshStats();
                }
                catch (Exception ex)
                {
                    MessageQueue.Enqueue(string.Format("重载 BUFF 配置时刷新玩家 {0} 失败：{1}", player.Name, ex.Message));
                }
            }

            MessageQueue.Enqueue(string.Format("BUFF 配置已重新加载，共 {0} 条定义（自定义槽位 {1} 个）",
                BuffConfig.Defs.Count, BuffConfig.CustomSlotCount));
        }

        public void ReloadDrops()
        {
            for (var i = 0; i < MonsterInfoList.Count; i++)
            {
                string path = Path.Combine(Settings.DropPath, MonsterInfoList[i].Name + ".txt");

                if (!string.IsNullOrEmpty(MonsterInfoList[i].DropPath))
                {
                    path = Path.Combine(Settings.DropPath, MonsterInfoList[i].DropPath + ".txt");
                }

                MonsterInfoList[i].Drops.Clear();

                DropInfo.Load(MonsterInfoList[i].Drops, MonsterInfoList[i].Name, path, 0, true);
            }

            FishingDrops.Clear();
            for (int i = 0; i < 19; i++)
            {
                var path = Path.Combine(Settings.DropPath, Settings.FishingDropFilename + ".txt");
                path = path.Replace("00", i.ToString("D2"));

                DropInfo.Load(FishingDrops, $"钓鱼功能 {i}", path, (byte)i, i < 2);
            }

            AwakeningDrops.Clear();
            DropInfo.Load(AwakeningDrops, "觉醒功能", Path.Combine(Settings.DropPath, Settings.AwakeningDropFilename + ".txt"));

            StrongboxDrops.Clear();
            DropInfo.Load(StrongboxDrops, "宝箱功能", Path.Combine(Settings.DropPath, Settings.StrongboxDropFilename + ".txt"));

            BlackstoneDrops.Clear();
            DropInfo.Load(BlackstoneDrops, "灵物石功能", Path.Combine(Settings.DropPath, Settings.BlackstoneDropFilename + ".txt"));

            MessageQueue.Enqueue("怪物掉落加载完成");
        }

        public void ReloadLineMessages()
        {
            LineMessages.Clear();

            var path = Path.Combine(Settings.EnvirPath, "LineMessage.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "");
            }
            else
            {
                var lines = File.ReadAllLines(path);

                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;
                    LineMessages.Add(lines[i]);
                }
                MessageQueue.Enqueue("LineMessages 已重新加载");
            }
        }

        private WorldMapIcon ValidateWorldMap()
        {
            foreach (WorldMapIcon wmi in Settings.WorldMapSetup.Icons)
            {
                MapInfo info = GetMapInfo(wmi.MapIndex);

                if (info == null)
                    return wmi;
            }
            return null;
        }

        public void DeleteGuild(GuildObject guild)
        {
            Guilds.Remove(guild);
            GuildList.Remove(guild.Info);

            GuildRefreshNeeded = true;
            MessageQueue.Enqueue(guild.Info.Name + " 注销行会完成服务器数据已删除");
        }
    }
}
