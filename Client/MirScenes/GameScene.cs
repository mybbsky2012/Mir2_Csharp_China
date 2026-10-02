using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using SlimDX;
using SlimDX.Direct3D9;
using Font = System.Drawing.Font;
using S = ServerPackets;
using C = ClientPackets;
using Effect = Client.MirObjects.Effect;
using Client.MirScenes.Dialogs;
using Client.Utils;
using Client.Resolution;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;
using Client.MirGraphics.Particles;

namespace Client.MirScenes
{
    public sealed class GameScene : MirScene
    {
        public static GameScene Scene;
        public static bool Observing;
        public static bool AllowObserve;

        public static UserObject User
        {
            get { return MapObject.User; }
            set { MapObject.User = value; }
        }

        public static UserHeroObject Hero
        {
            get { return MapObject.Hero; }
            set { MapObject.Hero = value; }
        }
        public static HeroObject HeroObject
        {
            get { return MapObject.HeroObject; }
            set { MapObject.HeroObject = value; }
        }

        public static long MoveTime, AttackTime, NextRunTime, LogTime, LastRunTime;
        public static bool CanMove, CanRun;

        //服务器是否开放对应的辅助开关（由 S.PlayerOption 下发）
        public static bool NoLampAllowed, WalkThroughAllowed, NoRunUpAllowed, OverWeightAllowed, MountTaiAllowed;

        private DateTime lastChangeTime = DateTime.MinValue;
        private readonly TimeSpan changeCooldown = TimeSpan.FromSeconds(1);

        //进图后按本地偏好重新申请辅助开关（见 RestorePlayerOptions）
        private bool _playerOptionsRestored;
        private readonly int _playerOptionsRestoreTime = unchecked(Environment.TickCount + 1500);

        private bool hasHero;
        public bool HasHero
        {
            get { return hasHero; }
            set
            {
                if (hasHero == value) return;

                hasHero = value;               
                MainDialog.HeroSummonButton.Visible = value;
            }
        }
        public HeroSpawnState HeroSpawnState;
        public List<ParticleEngine> ParticleEngines = new List<ParticleEngine>();
        public MapControl MapControl;
        public MainDialog MainDialog;
        public ChatDialog ChatDialog;
        public ChatControlBar ChatControl;
        public InventoryDialog InventoryDialog;
        public CharacterDialog CharacterDialog;
        public CharacterDialog HeroDialog;
        public HeroInventoryDialog HeroInventoryDialog;
        public HeroManageDialog HeroManageDialog;
        public CraftDialog CraftDialog;
        public StorageDialog StorageDialog;
        public BeltDialog BeltDialog;
        public MiniMapDialog MiniMapDialog;
        public InspectDialog InspectDialog;
        public OptionDialog OptionDialog;
        public MenuDialog MenuDialog;
        public AutoPlayDialog AutoPlayDialog;
        public NPCDialog NPCDialog;
        public NPCGoodsDialog NPCGoodsDialog;
        public NPCGoodsDialog NPCSubGoodsDialog;
        public NPCGoodsDialog NPCCraftGoodsDialog;
        public NPCDropDialog NPCDropDialog;
        public NPCAwakeDialog NPCAwakeDialog;
        public HelpDialog HelpDialog;
        public MountDialog MountDialog;
        public FishingDialog FishingDialog;
        public FishingStatusDialog FishingStatusDialog;
        public RefineDialog RefineDialog;

        public GroupDialog GroupDialog;
        public GuildDialog GuildDialog;

        public NewCharacterDialog NewHeroDialog;
        public HeroBeltDialog HeroBeltDialog;

        public BigMapDialog BigMapDialog;
        public TrustMerchantDialog TrustMerchantDialog;
        public CharacterDuraPanel CharacterDuraPanel;
        public DuraStatusDialog DuraStatusPanel;
        public TradeDialog TradeDialog;
        public GuestTradeDialog GuestTradeDialog;

        public HeroMenuPanel HeroMenuPanel;
        public HeroBehaviourPanel HeroBehaviourPanel;
        public HeroAIDialog HeroAIDialog;
        public SocketDialog SocketDialog;

        public List<SkillBarDialog> SkillBarDialogs = new List<SkillBarDialog>();
        public ChatOptionDialog ChatOptionDialog;
        public ChatNoticeDialog ChatNoticeDialog;

        public QuestListDialog QuestListDialog;
        public QuestDetailDialog QuestDetailDialog;
        public QuestDiaryDialog QuestLogDialog;
        public QuestTrackingDialog QuestTrackingDialog;

        public RankingDialog RankingDialog;

        public MailListDialog MailListDialog;
        public MailComposeLetterDialog MailComposeLetterDialog;
        public MailComposeParcelDialog MailComposeParcelDialog;
        public MailReadLetterDialog MailReadLetterDialog;
        public MailReadParcelDialog MailReadParcelDialog;

        public IntelligentCreatureDialog IntelligentCreatureDialog;
        public IntelligentCreatureOptionsDialog IntelligentCreatureOptionsDialog;
        public IntelligentCreatureOptionsGradeDialog IntelligentCreatureOptionsGradeDialog;

        public FriendDialog FriendDialog;
        public MemoDialog MemoDialog;
        public RelationshipDialog RelationshipDialog;
        public MentorDialog MentorDialog;
        public GameShopDialog GameShopDialog;

        public ReportDialog ReportDialog;

        public ItemRentingDialog ItemRentingDialog;
        public ItemRentDialog ItemRentDialog;
        public GuestItemRentingDialog GuestItemRentingDialog;
        public GuestItemRentDialog GuestItemRentDialog;
        public ItemRentalDialog ItemRentalDialog;

        public BuffDialog BuffsDialog;
        public BuffDialog HeroBuffsDialog;

        public KeyboardLayoutDialog KeyboardLayoutDialog;
        public NoticeDialog NoticeDialog;

        public TimerDialog TimerControl;
        public CompassDialog CompassControl;
        public RollDialog RollControl;


        public static List<ItemInfo> ItemInfoList = new List<ItemInfo>();
        public static List<UserId> UserIdList = new List<UserId>();
        public static List<UserItem> ChatItemList = new List<UserItem>();
        public static List<ClientQuestInfo> QuestInfoList = new List<ClientQuestInfo>();
        public static List<GameShopItem> GameShopInfoList = new List<GameShopItem>();
        public static List<ClientRecipeInfo> RecipeInfoList = new List<ClientRecipeInfo>();
        public static Dictionary<int, BigMapRecord> MapInfoList = new Dictionary<int, BigMapRecord>();
        public static List<ClientHeroInformation> HeroInfoList = new List<ClientHeroInformation>();
        public static ClientHeroInformation[] HeroStorage = new ClientHeroInformation[8];
        public static Dictionary<long, RankCharacterInfo> RankingList = new Dictionary<long, RankCharacterInfo>();
        public static int TeleportToNPCCost;
        public static int MaximumHeroCount;

        public static UserItem[] Storage = new UserItem[80];
        public static UserItem[] GuildStorage = new UserItem[112];
        public static UserItem[] Refine = new UserItem[16];
        public static UserItem HoverItem, SelectedItem;
        public static MirItemCell SelectedCell;

        public static bool PickedUpGold;
        public MirControl ItemLabel, MailLabel, MemoLabel, GuildBuffLabel;
        public static long UseItemTime, PickUpTime, DropViewTime, TargetDeadTime;
        public static uint Gold, Credit;
        public static long InspectTime;
        public bool ShowReviveMessage;


        public bool NewMail;
        public int NewMailCounter = 0;


        public AttackMode AMode;
        public PetMode PMode;
        public LightSetting Lights;

        public static long NPCTime;
        public static uint NPCID;
        public static float NPCRate;
        public static uint DefaultNPCID;
        public static bool HideAddedStoreStats;

        public long ToggleTime;        
        public static long SpellTime;

        public MirLabel[] OutputLines = new MirLabel[10];
        public List<OutPutMessage> OutputMessages = new List<OutPutMessage>();

        public long OutputDelay;

        public GameScene()
        {
            MapControl.AutoRun = false;
            MapControl.AutoHit = false;

            Scene = this;
            BackColour = Color.Transparent;
            MoveTime = CMain.Time;

            KeyDown += GameScene_KeyDown;

            MainDialog = new MainDialog { Parent = this };
            ChatDialog = new ChatDialog { Parent = this };
            ChatControl = new ChatControlBar { Parent = this };
            InventoryDialog = new InventoryDialog { Parent = this };            
            BeltDialog = new BeltDialog { Parent = this };
            StorageDialog = new StorageDialog { Parent = this, Visible = false };
            CraftDialog = new CraftDialog { Parent = this, Visible = false };
            MiniMapDialog = new MiniMapDialog { Parent = this };
            InspectDialog = new InspectDialog { Parent = this, Visible = false };
            OptionDialog = new OptionDialog { Parent = this, Visible = false };
            MenuDialog = new MenuDialog { Parent = this, Visible = false };
            AutoPlayDialog = new AutoPlayDialog { Parent = this, Visible = false };
            NPCDialog = new NPCDialog { Parent = this, Visible = false };
            NPCGoodsDialog = new NPCGoodsDialog(PanelType.Buy) { Parent = this, Visible = false };
            NPCSubGoodsDialog = new NPCGoodsDialog(PanelType.BuySub) { Parent = this, Visible = false };
            NPCCraftGoodsDialog = new NPCGoodsDialog(PanelType.Craft) { Parent = this, Visible = false };
            NPCDropDialog = new NPCDropDialog { Parent = this, Visible = false };
            NPCAwakeDialog = new NPCAwakeDialog { Parent = this, Visible = false };

            HelpDialog = new HelpDialog { Parent = this, Visible = false };
            KeyboardLayoutDialog = new KeyboardLayoutDialog { Parent = this, Visible = false };
            NoticeDialog = new NoticeDialog { Parent = this, Visible = false };

            MountDialog = new MountDialog { Parent = this, Visible = false };
            FishingDialog = new FishingDialog { Parent = this, Visible = false };
            FishingStatusDialog = new FishingStatusDialog { Parent = this, Visible = false };

            GroupDialog = new GroupDialog { Parent = this, Visible = false };
            GuildDialog = new GuildDialog { Parent = this, Visible = false };

            NewHeroDialog = new NewCharacterDialog { Parent = this, Visible = false };
            NewHeroDialog.TitleLabel.Index = 847;
            NewHeroDialog.TitleLabel.Location = new Point(246, 11);
            NewHeroDialog.OnCreateCharacter += (o, e) =>
            {
                Network.Enqueue(new C.NewHero
                {
                    Name = NewHeroDialog.NameTextBox.Text,
                    Class = NewHeroDialog.Class,
                    Gender = NewHeroDialog.Gender
                });
            };

            HeroMenuPanel = new HeroMenuPanel(this) { Visible = false };
            HeroBehaviourPanel = new HeroBehaviourPanel { Parent = this, Visible = false };
            HeroAIDialog = new HeroAIDialog { Parent = this, Visible = false };
            HeroManageDialog = new HeroManageDialog { Parent = this, Visible = false };

            BigMapDialog = new BigMapDialog { Parent = this, Visible = false };
            TrustMerchantDialog = new TrustMerchantDialog { Parent = this, Visible = false };
            CharacterDuraPanel = new CharacterDuraPanel { Parent = this, Visible = false };
            DuraStatusPanel = new DuraStatusDialog { Parent = this, Visible = true };
            TradeDialog = new TradeDialog { Parent = this, Visible = false };
            GuestTradeDialog = new GuestTradeDialog { Parent = this, Visible = false };            

            SocketDialog = new SocketDialog { Parent = this, Visible = false };

            SkillBarDialog Bar1 = new SkillBarDialog { Parent = this, Visible = false, BarIndex = 0 };
            SkillBarDialogs.Add(Bar1);
            SkillBarDialog Bar2 = new SkillBarDialog { Parent = this, Visible = false, BarIndex = 1 };
            SkillBarDialogs.Add(Bar2);
            ChatOptionDialog = new ChatOptionDialog { Parent = this, Visible = false };
            ChatNoticeDialog = new ChatNoticeDialog { Parent = this, Visible = false };

            QuestListDialog = new QuestListDialog { Parent = this, Visible = false };
            QuestDetailDialog = new QuestDetailDialog { Parent = this, Visible = false };
            QuestTrackingDialog = new QuestTrackingDialog { Parent = this, Visible = false };
            QuestLogDialog = new QuestDiaryDialog { Parent = this, Visible = false };

            RankingDialog = new RankingDialog { Parent = this, Visible = false };

            MailListDialog = new MailListDialog { Parent = this, Visible = false };
            MailComposeLetterDialog = new MailComposeLetterDialog { Parent = this, Visible = false };
            MailComposeParcelDialog = new MailComposeParcelDialog { Parent = this, Visible = false };
            MailReadLetterDialog = new MailReadLetterDialog { Parent = this, Visible = false };
            MailReadParcelDialog = new MailReadParcelDialog { Parent = this, Visible = false };

            IntelligentCreatureDialog = new IntelligentCreatureDialog { Parent = this, Visible = false };
            IntelligentCreatureOptionsDialog = new IntelligentCreatureOptionsDialog { Parent = this, Visible = false };
            IntelligentCreatureOptionsGradeDialog = new IntelligentCreatureOptionsGradeDialog { Parent = this, Visible = false };

            RefineDialog = new RefineDialog { Parent = this, Visible = false };
            RelationshipDialog = new RelationshipDialog { Parent = this, Visible = false };
            FriendDialog = new FriendDialog { Parent = this, Visible = false };
            MemoDialog = new MemoDialog { Parent = this, Visible = false };
            MentorDialog = new MentorDialog { Parent = this, Visible = false };
            GameShopDialog = new GameShopDialog { Parent = this, Visible = false };
            ReportDialog = new ReportDialog { Parent = this, Visible = false };

            ItemRentingDialog = new ItemRentingDialog { Parent = this, Visible = false };
            ItemRentDialog = new ItemRentDialog { Parent = this, Visible = false };
            GuestItemRentingDialog = new GuestItemRentingDialog { Parent = this, Visible = false };
            GuestItemRentDialog = new GuestItemRentDialog { Parent = this, Visible = false };
            ItemRentalDialog = new ItemRentalDialog { Parent = this, Visible = false };

            BuffsDialog = new BuffDialog
            {
                Parent = this,
                Visible = true,
                GetExpandedParameter = () => { return Settings.ExpandedBuffWindow; },
                SetExpandedParameter = (value) => { Settings.ExpandedBuffWindow = value; }
            };

            KeyboardLayoutDialog = new KeyboardLayoutDialog { Parent = this, Visible = false };

            TimerControl = new TimerDialog { Parent = this, Visible = false };
            CompassControl = new CompassDialog { Parent = this, Visible = false };
            RollControl = new RollDialog { Parent = this, Visible = false };

            for (int i = 0; i < OutputLines.Length; i++)
                OutputLines[i] = new MirLabel
                {
                    AutoSize = true,
                    BackColour = Color.Transparent,
                    Font = new Font(Settings.FontName, 10F),
                    ForeColour = Color.LimeGreen,
                    Location = new Point(20, 25 + i * 13),
                    OutLine = true,
                };

            if (MapInfoList.Count > 0)
                RecreateBigMapButtons();
        }

        private void UpdateMouseCursor()
        {
            if (!Settings.UseMouseCursors) return;

            if (GameScene.HoverItem != null)
            {
                if (GameScene.SelectedCell != null && GameScene.SelectedCell.Item != null && GameScene.SelectedCell.Item.Info.Type == ItemType.宝玉神珠 && CMain.Ctrl)
                {
                    CMain.SetMouseCursor(MouseCursor.Upgrade);
                }
                else
                {
                    CMain.SetMouseCursor(MouseCursor.Default);
                }
            }
            else if (MapObject.MouseObject != null)
            {
                switch (MapObject.MouseObject.Race)
                {
                    case ObjectType.Monster:
                        CMain.SetMouseCursor(MouseCursor.Attack);
                        break;
                    case ObjectType.Merchant:
                        CMain.SetMouseCursor(MouseCursor.NPCTalk);
                        break;
                    case ObjectType.Player:
                        if (CMain.Shift)
                        {
                            CMain.SetMouseCursor(MouseCursor.AttackRed);
                        }
                        else
                        {
                            CMain.SetMouseCursor(MouseCursor.Default);
                        }
                        break;
                    default:
                        CMain.SetMouseCursor(MouseCursor.Default);
                        break;
                }
            }
            else
            {
                CMain.SetMouseCursor(MouseCursor.Default);
            }

        }

        public void OutputMessage(string message, OutputMessageType type = OutputMessageType.Normal)
        {
            OutputMessages.Add(new OutPutMessage { Message = message, ExpireTime = CMain.Time + 5000, Type = type });
            if (OutputMessages.Count > 10)
                OutputMessages.RemoveAt(0);
        }

        private void ProcessOuput()
        {
            for (int i = 0; i < OutputMessages.Count; i++)
            {
                if (CMain.Time >= OutputMessages[i].ExpireTime)
                    OutputMessages.RemoveAt(i);
            }

            for (int i = 0; i < OutputLines.Length; i++)
            {
                if (OutputMessages.Count > i)
                {
                    Color color;
                    switch (OutputMessages[i].Type)
                    {
                        case OutputMessageType.Quest:
                            color = Color.Gold;
                            break;
                        case OutputMessageType.Guild:
                            color = Color.DeepPink;
                            break;
                        default:
                            color = Color.LimeGreen;
                            break;
                    }

                    OutputLines[i].Text = OutputMessages[i].Message;
                    OutputLines[i].ForeColour = color;
                    OutputLines[i].Visible = true;
                }
                else
                {
                    OutputLines[i].Text = string.Empty;
                    OutputLines[i].Visible = false;
                }
            }
        }
        private void GameScene_KeyDown(object sender, KeyEventArgs e)
        {
            if (GameScene.Scene.KeyboardLayoutDialog.WaitingForBind != null)
            {
                GameScene.Scene.KeyboardLayoutDialog.CheckNewInput(e);
                return;
            }

            foreach (KeyBind KeyCheck in CMain.InputKeys.Keylist)
            {
                if (KeyCheck.Key == Keys.None)
                    continue;
                if (KeyCheck.Key != e.KeyCode)
                    continue;
                if ((KeyCheck.RequireAlt != 2) && (KeyCheck.RequireAlt != (CMain.Alt ? 1 : 0)))
                    continue;
                if ((KeyCheck.RequireShift != 2) && (KeyCheck.RequireShift != (CMain.Shift ? 1 : 0)))
                    continue;
                if ((KeyCheck.RequireCtrl != 2) && (KeyCheck.RequireCtrl != (CMain.Ctrl ? 1 : 0)))
                    continue;
                if ((KeyCheck.RequireTilde != 2) && (KeyCheck.RequireTilde != (CMain.Tilde ? 1 : 0)))
                    continue;
                //now run the real code
                switch (KeyCheck.function)
                {
                    case KeybindOptions.Bar1Skill1: UseSpell(1); break;
                    case KeybindOptions.Bar1Skill2: UseSpell(2); break;
                    case KeybindOptions.Bar1Skill3: UseSpell(3); break;
                    case KeybindOptions.Bar1Skill4: UseSpell(4); break;
                    case KeybindOptions.Bar1Skill5: UseSpell(5); break;
                    case KeybindOptions.Bar1Skill6: UseSpell(6); break;
                    case KeybindOptions.Bar1Skill7: UseSpell(7); break;
                    case KeybindOptions.Bar1Skill8: UseSpell(8); break;
                    case KeybindOptions.Bar2Skill1: UseSpell(9); break;
                    case KeybindOptions.Bar2Skill2: UseSpell(10); break;
                    case KeybindOptions.Bar2Skill3: UseSpell(11); break;
                    case KeybindOptions.Bar2Skill4: UseSpell(12); break;
                    case KeybindOptions.Bar2Skill5: UseSpell(13); break;
                    case KeybindOptions.Bar2Skill6: UseSpell(14); break;
                    case KeybindOptions.Bar2Skill7: UseSpell(15); break;
                    case KeybindOptions.Bar2Skill8: UseSpell(16); break;
                    case KeybindOptions.HeroSkill1: UseSpell(17); break;
                    case KeybindOptions.HeroSkill2: UseSpell(18); break;
                    case KeybindOptions.HeroSkill3: UseSpell(19); break;
                    case KeybindOptions.HeroSkill4: UseSpell(20); break;
                    case KeybindOptions.HeroSkill5: UseSpell(21); break;
                    case KeybindOptions.HeroSkill6: UseSpell(22); break;
                    case KeybindOptions.HeroSkill7: UseSpell(23); break;
                    case KeybindOptions.HeroSkill8: UseSpell(24); break;
                    case KeybindOptions.Inventory:
                    case KeybindOptions.Inventory2:
                        if (!InventoryDialog.Visible) InventoryDialog.Show();
                        else InventoryDialog.Hide();
                        break;
                    case KeybindOptions.Equipment:
                    case KeybindOptions.Equipment2:
                        if (!CharacterDialog.Visible || !CharacterDialog.CharacterPage.Visible)
                        {
                            CharacterDialog.Show();
                            CharacterDialog.ShowCharacterPage();
                        }
                        else CharacterDialog.Hide();
                        break;
                    case KeybindOptions.Skills:
                    case KeybindOptions.Skills2:
                        if (!CharacterDialog.Visible || !CharacterDialog.SkillPage.Visible)
                        {
                            CharacterDialog.Show();
                            CharacterDialog.ShowSkillPage();
                        }
                        else CharacterDialog.Hide();
                        break;
                    case KeybindOptions.HeroInventory:
                        if (Hero == null)
                            break;
                        if (!HeroInventoryDialog.Visible) HeroInventoryDialog.Show();
                        else HeroInventoryDialog.Hide();
                        break;
                    case KeybindOptions.HeroEquipment:
                        if (Hero == null)
                            break;
                        if (!HeroDialog.Visible || !HeroDialog.CharacterPage.Visible)
                        {
                            HeroDialog.Show();
                            HeroDialog.ShowCharacterPage();
                        }
                        else HeroDialog.Hide();
                        break;
                    case KeybindOptions.HeroSkills:
                        if (Hero == null)
                            break;
                        if (!HeroDialog.Visible || !HeroDialog.SkillPage.Visible)
                        {
                            HeroDialog.Show();
                            HeroDialog.ShowSkillPage();
                        }
                        else HeroDialog.Hide();
                        break;
                    case KeybindOptions.Creature:
                        if (!IntelligentCreatureDialog.Visible) IntelligentCreatureDialog.Show();
                        else IntelligentCreatureDialog.Hide();
                        break;
                    case KeybindOptions.MountWindow:
                        if (!MountDialog.Visible) MountDialog.Show();
                        else MountDialog.Hide();
                        break;

                    case KeybindOptions.GameShop:
                        if (!GameShopDialog.Visible) GameShopDialog.Show();
                        else GameShopDialog.Hide();
                        break;
                    case KeybindOptions.Fishing:
                        if (!FishingDialog.Visible) FishingDialog.Show();
                        else FishingDialog.Hide();
                        break;
                    case KeybindOptions.Skillbar:
                        if (!Settings.SkillBar)
                            foreach (SkillBarDialog Bar in SkillBarDialogs)
                                Bar.Show();
                        else
                            foreach (SkillBarDialog Bar in SkillBarDialogs)
                                Bar.Hide();
                        break;
                    case KeybindOptions.Mount:
                        if (GameScene.Scene.MountDialog.CanRide())
                            GameScene.Scene.MountDialog.Ride();
                        break;
                    case KeybindOptions.Mentor:
                        if (!MentorDialog.Visible) MentorDialog.Show();
                        else MentorDialog.Hide();
                        break;
                    case KeybindOptions.Relationship:
                        if (!RelationshipDialog.Visible) RelationshipDialog.Show();
                        else RelationshipDialog.Hide();
                        break;
                    case KeybindOptions.Friends:
                        if (!FriendDialog.Visible) FriendDialog.Show();
                        else FriendDialog.Hide();
                        break;
                    case KeybindOptions.Guilds:
                        if (!GuildDialog.Visible) GuildDialog.Show();
                        else
                        {
                            GuildDialog.Hide();
                        }
                        break;

                    case KeybindOptions.Ranking:
                        if (!RankingDialog.Visible) RankingDialog.Show();
                        else RankingDialog.Hide();
                        break;
                    case KeybindOptions.Quests:
                        if (!QuestLogDialog.Visible) QuestLogDialog.Show();
                        else QuestLogDialog.Hide();
                        break;
                    case KeybindOptions.Exit:
                        QuitGame();
                        return;

                    case KeybindOptions.Closeall:
                        InventoryDialog.Hide();
                        CharacterDialog.Hide();
                        OptionDialog.Hide();
                        MenuDialog.Hide();
                        if (NPCDialog.Visible) NPCDialog.Hide();
                        HelpDialog.Hide();
                        KeyboardLayoutDialog.Hide();
                        RankingDialog.Hide();
                        IntelligentCreatureDialog.Hide();
                        IntelligentCreatureOptionsDialog.Hide();
                        IntelligentCreatureOptionsGradeDialog.Hide();
                        MountDialog.Hide();
                        FishingDialog.Hide();
                        FriendDialog.Hide();
                        RelationshipDialog.Hide();
                        MentorDialog.Hide();
                        GameShopDialog.Hide();
                        GroupDialog.Hide();
                        GuildDialog.Hide();
                        InspectDialog.Hide();
                        StorageDialog.Hide();
                        TrustMerchantDialog.Hide();
                        //CharacterDuraPanel.Hide();
                        QuestListDialog.Hide();
                        QuestDetailDialog.Hide();
                        QuestLogDialog.Hide();
                        NPCAwakeDialog.Hide();
                        RefineDialog.Hide();
                        BigMapDialog.Hide();
                        if (FishingStatusDialog.bEscExit) FishingStatusDialog.Cancel();
                        MailComposeLetterDialog.Hide();
                        MailComposeParcelDialog.Hide();
                        MailListDialog.Hide();
                        MailReadLetterDialog.Hide();
                        MailReadParcelDialog.Hide();
                        ItemRentalDialog.Hide();
                        NoticeDialog.Hide();
                        HeroInventoryDialog?.Hide();
                        HeroManageDialog?.Hide();
                        HeroDialog?.Hide();

                        GameScene.Scene.DisposeItemLabel();
                        break;
                    case KeybindOptions.Options:
                    case KeybindOptions.Options2:
                        if (!OptionDialog.Visible) OptionDialog.Show();
                        else OptionDialog.Hide();
                        break;
                    case KeybindOptions.Group:
                        if (!GroupDialog.Visible) GroupDialog.Show();
                        else GroupDialog.Hide();
                        break;
                    case KeybindOptions.Belt:
                        if (!BeltDialog.Visible) BeltDialog.Show();
                        else BeltDialog.Hide();
                        break;
                    case KeybindOptions.BeltFlip:
                        BeltDialog.Flip();
                        break;
                    case KeybindOptions.Pickup:
                        if (CMain.Time > PickUpTime)
                        {
                            PickUpTime = CMain.Time + 200;
                            Network.Enqueue(new C.PickUp());
                        }
                        break;
                    case KeybindOptions.Belt1:
                    case KeybindOptions.Belt1Alt:
                        BeltDialog.Grid[0].UseItem();
                        break;
                    case KeybindOptions.Belt2:
                    case KeybindOptions.Belt2Alt:
                        BeltDialog.Grid[1].UseItem();
                        break;
                    case KeybindOptions.Belt3:
                    case KeybindOptions.Belt3Alt:
                        BeltDialog.Grid[2].UseItem();
                        break;
                    case KeybindOptions.Belt4:
                    case KeybindOptions.Belt4Alt:
                        BeltDialog.Grid[3].UseItem();
                        break;
                    case KeybindOptions.Belt5:
                    case KeybindOptions.Belt5Alt:
                        BeltDialog.Grid[4].UseItem();
                        break;
                    case KeybindOptions.Belt6:
                    case KeybindOptions.Belt6Alt:
                        BeltDialog.Grid[5].UseItem();
                        break;
                    case KeybindOptions.Belt7:
                    case KeybindOptions.Belt7Alt:
                        HeroBeltDialog?.Grid[0].UseItem();
                        break;
                    case KeybindOptions.Belt8:
                    case KeybindOptions.Belt8Alt:
                        HeroBeltDialog?.Grid[1].UseItem();
                        break;
                    case KeybindOptions.Logout:
                        LogOut();
                        break;
                    case KeybindOptions.Minimap:
                        MiniMapDialog.Toggle();
                        break;
                    case KeybindOptions.Bigmap:
                        BigMapDialog.Toggle();
                        break;
                    case KeybindOptions.Trade:
                        Network.Enqueue(new C.TradeRequest());
                        break;
                    case KeybindOptions.Rental:
                        ItemRentalDialog.Toggle();
                        break;
                    case KeybindOptions.ChangePetmode:
                        ChangePetMode();
                        break;
                    case KeybindOptions.PetmodeBoth:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.Both });
                        return;
                    case KeybindOptions.PetmodeMoveonly:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.MoveOnly });
                        return;
                    case KeybindOptions.PetmodeAttackonly:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.AttackOnly });
                        return;
                    case KeybindOptions.PetmodeNone:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.None });
                        return;
                    case KeybindOptions.PetmodeFocusMasterTarget:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.FocusMasterTarget });
                        return;
                    case KeybindOptions.CreatureAutoPickup://semiauto!
                        if (DateTime.Now - lastChangeTime < changeCooldown)
                        {
                            return;
                        }
                        lastChangeTime = DateTime.Now;

                        Network.Enqueue(new C.IntelligentCreaturePickup { MouseMode = false, Location = MapControl.MapLocation });
                        break;
                    case KeybindOptions.CreaturePickup:
                        if (DateTime.Now - lastChangeTime < changeCooldown)
                        {
                            return;
                        }
                        lastChangeTime = DateTime.Now;

                        Network.Enqueue(new C.IntelligentCreaturePickup { MouseMode = true, Location = MapControl.MapLocation });
                        break;
                    case KeybindOptions.ChangeAttackmode:
                        ChangeAttackMode();
                        break;
                    case KeybindOptions.AttackmodePeace:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.Peace });
                        return;
                    case KeybindOptions.AttackmodeGroup:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.Group });
                        return;
                    case KeybindOptions.AttackmodeGuild:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.Guild });
                        return;
                    case KeybindOptions.AttackmodeEnemyguild:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.EnemyGuild });
                        return;
                    case KeybindOptions.AttackmodeRedbrown:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.RedBrown });
                        return;
                    case KeybindOptions.AttackmodeAll:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.All });
                        return;

                    case KeybindOptions.Help:
                        if (!HelpDialog.Visible) HelpDialog.Show();
                        else HelpDialog.Hide();
                        break;
                    case KeybindOptions.Keybind:
                        if (!KeyboardLayoutDialog.Visible) KeyboardLayoutDialog.Show();
                        else KeyboardLayoutDialog.Hide();
                        break;
                    case KeybindOptions.Autorun:
                        MapControl.AutoRun = !MapControl.AutoRun;
                        break;
                    case KeybindOptions.Cameramode:

                        if (!MainDialog.Visible)
                        {
                            MainDialog.Show();
                            ChatDialog.Show();
                            BeltDialog.Show();
                            ChatControl.Show();
                            MiniMapDialog.Show();
                            CharacterDuraPanel.Show();
                            DuraStatusPanel.Show();
                            BuffsDialog.Show();
                        }
                        else
                        {
                            MainDialog.Hide();
                            ChatDialog.Hide();
                            BeltDialog.Hide();
                            ChatControl.Hide();
                            MiniMapDialog.Hide();
                            CharacterDuraPanel.Hide();
                            DuraStatusPanel.Hide();
                            BuffsDialog.Hide();
                        }
                        break;
                    case KeybindOptions.DropView:
                        if (CMain.Time > DropViewTime)
                            DropViewTime = CMain.Time + 5000;
                        break;
                    case KeybindOptions.TargetDead:
                        if (CMain.Time > TargetDeadTime)
                            TargetDeadTime = CMain.Time + 5000;
                        break;
                    case KeybindOptions.AddGroupMember:
                        if (MapObject.MouseObject == null) break;
                        if (MapObject.MouseObject.Race != ObjectType.Player) break;

                        GameScene.Scene.GroupDialog.AddMember(MapObject.MouseObject.Name);
                        break;
                    case KeybindOptions.AutoPlay:
                        if (!AutoPlayDialog.Visible)
                        {
                            AutoPlayDialog.UpdateState();
                            AutoPlayDialog.Show();
                        }
                        else
                            AutoPlayDialog.Hide();
                        break;
                    case KeybindOptions.AutoPlayToggle:
                        Settings.AutoPlay = !Settings.AutoPlay;

                        if (!Settings.AutoPlay)
                            MapObject.TargetObjectID = 0;

                        Settings.Save();
                        OutputMessage(Settings.AutoPlay ? "内挂已开启" : "内挂已关闭");

                        if (AutoPlayDialog.Visible)
                            AutoPlayDialog.UpdateState();
                        break;
                }
            }
        }

        public void ChangeSkillMode(bool? ctrl)
        {
            if (Settings.SkillMode || ctrl == true)
            {
                Settings.SkillMode = false;
                GameScene.Scene.ChatDialog.ReceiveChat("[SkillMode Ctrl]", ChatType.Hint);
                GameScene.Scene.OptionDialog.ToggleSkillButtons(true);
            }
            else if (!Settings.SkillMode || ctrl == false)
            {
                Settings.SkillMode = true;
                GameScene.Scene.ChatDialog.ReceiveChat("[SkillMode ~]", ChatType.Hint);
                GameScene.Scene.OptionDialog.ToggleSkillButtons(false);
            }
        }

        public void ChangePetMode()
        {
            if (DateTime.Now - lastChangeTime < changeCooldown)
            {
                return;
            }
            lastChangeTime = DateTime.Now;

            switch (PMode)
            {
                case PetMode.Both:
                    Network.Enqueue(new C.ChangePMode { Mode = PetMode.MoveOnly });
                    return;
                case PetMode.MoveOnly:
                    Network.Enqueue(new C.ChangePMode { Mode = PetMode.AttackOnly });
                    return;
                case PetMode.AttackOnly:
                    Network.Enqueue(new C.ChangePMode { Mode = PetMode.None });
                    return;
                case PetMode.None:
                    Network.Enqueue(new C.ChangePMode { Mode = PetMode.FocusMasterTarget });
                    return;
                case PetMode.FocusMasterTarget:
                    Network.Enqueue(new C.ChangePMode { Mode = PetMode.Both });
                    return;
            }
        }

        public void ChangeAttackMode()
        {
            if (DateTime.Now - lastChangeTime < changeCooldown)
            {
                return;
            }
            lastChangeTime = DateTime.Now;

            switch (AMode)
            {
                case AttackMode.Peace:
                    Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.Group });
                    return;
                case AttackMode.Group:
                    Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.Guild });
                    return;
                case AttackMode.Guild:
                    Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.EnemyGuild });
                    return;
                case AttackMode.EnemyGuild:
                    Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.RedBrown });
                    return;
                case AttackMode.RedBrown:
                    Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.All });
                    return;
                case AttackMode.All:
                    Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.Peace });
                    return;
            }
        }

        public void UseSpell(int key)
        {
            UserObject actor = User;
            if (key > 16)
            {
                if (HeroObject == null) return;
                actor = Hero;
            }

            if (actor.Dead || actor.RidingMount || actor.Fishing) return;

            if (!actor.HasClassWeapon && actor.Weapon >= 0)
            {
                ChatDialog.ReceiveChat("必须佩带适合的武器以施展此项技能", ChatType.System);
                return;
            }

            if (CMain.Time < actor.BlizzardStopTime || CMain.Time < User.GreatFireBallRareStopTime || CMain.Time < actor.ReincarnationStopTime) return;

            ClientMagic magic = null;

            for (int i = 0; i < actor.Magics.Count; i++)
            {
                if (actor.Magics[i].Key != key) continue;
                magic = actor.Magics[i];
                break;
            }

            if (magic == null) return;

            switch (magic.Spell)
            {
                case Spell.CounterAttack:
                    if ((CMain.Time < magic.CastTime + magic.Delay))
                    {
                        if (CMain.Time >= OutputDelay)
                        {
                            OutputDelay = CMain.Time + 1000;
                            Scene.OutputMessage(string.Format("技能再使用间隔 {1} 秒", magic.Spell.ToString(), ((magic.CastTime + magic.Delay) - CMain.Time - 1) / 1000 + 1));
                        }

                        return;
                    }
                    magic.CastTime = CMain.Time;
                    break;
            }

            int cost;
            string prefix = actor == Hero ? "(英雄) " : string.Empty;
            switch (magic.Spell)
            {
                case Spell.Fencing:
                case Spell.FatalSword:
                case Spell.MPEater:
                case Spell.Hemorrhage:
                case Spell.SpiritSword:
                case Spell.Slaying:
                case Spell.Focus:
                case Spell.Meditation:
                    return;
                case Spell.Thrusting:
                    if (CMain.Time < ToggleTime) return;
                    actor.Thrusting = !actor.Thrusting;
                    ChatDialog.ReceiveChat(prefix + (actor.Thrusting ? "开启技能：刺杀剑术" : "关闭技能：刺杀剑术"), ChatType.Hint);
                    ToggleTime = CMain.Time + 1000;
                    SendSpellToggle(actor, magic.Spell, actor.Thrusting);                    
                    break;
                case Spell.HalfMoon:
                    if (CMain.Time < ToggleTime) return;
                    actor.HalfMoon = !actor.HalfMoon;
                    ChatDialog.ReceiveChat(prefix + (actor.HalfMoon ? "开启技能：半月弯刀" : "关闭技能：半月弯刀"), ChatType.Hint);
                    ToggleTime = CMain.Time + 1000;
                    SendSpellToggle(actor, magic.Spell, actor.HalfMoon);
                    break;
                case Spell.CrossHalfMoon:
                    if (CMain.Time < ToggleTime) return;
                    actor.CrossHalfMoon = !actor.CrossHalfMoon;
                    ChatDialog.ReceiveChat(prefix + (actor.CrossHalfMoon ? "开启技能：狂风斩" : "关闭技能：狂风斩"), ChatType.Hint);
                    ToggleTime = CMain.Time + 1000;
                    SendSpellToggle(actor, magic.Spell, actor.CrossHalfMoon);
                    break;
                case Spell.DoubleSlash:
                    if (CMain.Time < ToggleTime) return;
                    actor.DoubleSlash = !actor.DoubleSlash;
                    ChatDialog.ReceiveChat(prefix + (actor.DoubleSlash ? "开启技能：风剑术" : "关闭技能：风剑术"), ChatType.Hint);
                    ToggleTime = CMain.Time + 1000;
                    SendSpellToggle(actor, magic.Spell, actor.DoubleSlash);
                    break;
                case Spell.TwinDrakeBlade:
                    if (CMain.Time < ToggleTime) return;
                    ToggleTime = CMain.Time + 500;

                    cost = magic.Level * magic.LevelCost + magic.BaseCost;
                    if (cost > actor.MP)
                    {
                        Scene.OutputMessage(GameLanguage.LowMana);
                        return;
                    }
                    actor.TwinDrakeBlade = true;
                    SendSpellToggle(actor, magic.Spell, true);
                    if (actor == Hero)
                        HeroObject?.Effects.Add(new Effect(Libraries.Magic2, 210, 6, 500, HeroObject));
                    else
                        actor.Effects.Add(new Effect(Libraries.Magic2, 210, 6, 500, actor));
                    break;
                case Spell.FlamingSword:
                    if (CMain.Time < ToggleTime) return;
                    ToggleTime = CMain.Time + 500;

                    cost = magic.Level * magic.LevelCost + magic.BaseCost;
                    if (cost > actor.MP)
                    {
                        Scene.OutputMessage(GameLanguage.LowMana);
                        return;
                    }
                    SendSpellToggle(actor, magic.Spell, true);
                    break;
                case Spell.CounterAttack:
                    cost = magic.Level * magic.LevelCost + magic.BaseCost;
                    if (cost > actor.MP)
                    {
                        Scene.OutputMessage(GameLanguage.LowMana);
                        return;
                    }

                    SoundManager.PlaySound(20000 + (ushort)Spell.CounterAttack * 10);
                    SendSpellToggle(actor, magic.Spell, true);
                    break;
                case Spell.MentalState:
                    if (CMain.Time < ToggleTime) return;
                    ToggleTime = CMain.Time + 500;
                    SendSpellToggle(actor, magic.Spell, true);
                    break;
                default:
                    // 毒符互换（手动）：面板勾选后不用开内挂总开关。
                    // 这个技能吃符就先把符换进护身符槽、吃毒就先换毒，换好后再自动把这个技能补放出去。
                    if (actor == User && MapControl != null && MapControl.AutoPlayPrepareManualSpell(magic)) return;

                    actor.NextMagic = magic;
                    actor.NextMagicLocation = MapControl.MapLocation;
                    actor.NextMagicObject = MapObject.MouseObject;
                    actor.NextMagicDirection = MapControl.MouseDirection();

                    if (actor == Hero)
                        MapControl.UseMagic(Hero.NextMagic, Hero);
                    break;
            }
        }
        private void SendSpellToggle(UserObject Actor, Spell Spell, bool CanUse)
        {
            if (Actor == User)
                Network.Enqueue(new C.SpellToggle { Spell = Spell, CanUse = CanUse });
            else
                Network.Enqueue(new C.SpellToggle { Spell = Spell });
        }
        public void QuitGame()
        {
            if (CMain.Time >= LogTime)
            {
                //If Last Combat < 10 CANCEL
                MirMessageBox messageBox = new MirMessageBox(GameLanguage.ExitTip, MirMessageBoxButtons.YesNo);
                messageBox.YesButton.Click += (o, e) => Program.Form.Close();
                messageBox.Show();
            }
            else if (User.Dead)
            {
                MirMessageBox messageBox = new MirMessageBox(GameLanguage.ExitTip, MirMessageBoxButtons.YesNo);
                messageBox.YesButton.Click += (o, e) => Program.Form.Close();
                messageBox.Show();
            }
            else
            {
                ChatDialog.ReceiveChat(string.Format(GameLanguage.CannotLeaveGame, (LogTime - CMain.Time) / 1000), ChatType.System);
            }
        }
        public void LogOut()
        {
            if (CMain.Time >= LogTime)
            {
                //If Last Combat < 10 CANCEL
                MirMessageBox messageBox = new MirMessageBox(GameLanguage.LogOutTip, MirMessageBoxButtons.YesNo);
                messageBox.YesButton.Click += (o, e) =>
                {
                    Network.Enqueue(new C.LogOut());
                    Enabled = false;
                };
                messageBox.Show();
            }
            else if (User.Dead)
            {
                MirMessageBox messageBox = new MirMessageBox(GameLanguage.LogOutTip, MirMessageBoxButtons.YesNo);
                messageBox.YesButton.Click += (o, e) =>
                {
                    Network.Enqueue(new C.LogOut());
                    Enabled = false;
                };
                messageBox.Show();
            }
            else
            {
                ChatDialog.ReceiveChat(string.Format(GameLanguage.CannotLeaveGame, (LogTime - CMain.Time) / 1000), ChatType.System);
            }
        }

        protected internal override void DrawControl()
        {
            if (MapControl != null && !MapControl.IsDisposed)
                MapControl.DrawControl();
            base.DrawControl();


            if (PickedUpGold || (SelectedCell != null && SelectedCell.Item != null))
            {
                int image = PickedUpGold ? 116 : SelectedCell.Item.Image;
                Size imgSize = Libraries.Items.GetTrueSize(image);
                Point p = CMain.MPoint.Add(-imgSize.Width / 2, -imgSize.Height / 2);

                if (p.X + imgSize.Width >= Settings.ScreenWidth)
                    p.X = Settings.ScreenWidth - imgSize.Width;

                if (p.Y + imgSize.Height >= Settings.ScreenHeight)
                    p.Y = Settings.ScreenHeight - imgSize.Height;

                Libraries.Items.Draw(image, p.X, p.Y);
            }

            for (int i = 0; i < OutputLines.Length; i++)
                OutputLines[i].Draw();
        }
        // 微端下载进度提示的节流状态
        private static bool _microBusyHinted;
        private static bool _microIdleHinted;
        private static int _microHintCount;
        private static int _microNextCheck;

        /// <summary>
        /// 微端提示：第一次开始补资源、以及每批资源补齐时，在聊天框给一次反馈。
        /// 资源是按需下载的，缺图时最多"晚几帧出现"，玩家不需要做任何操作，
        /// 但给出可见反馈能让"边玩边加载"这件事不显得莫名其妙。
        /// </summary>
        private void ProcessMicroHint()
        {
            if (!Settings.MicroClient || !Settings.MicroHint) return;
            if (ChatDialog == null) return;

            if (unchecked(Environment.TickCount - _microNextCheck) < 0) return;
            _microNextCheck = Environment.TickCount + 2000;

            if (ResourceDownloader.PendingCount > 0)
            {
                if (!_microBusyHinted && _microHintCount < 10)
                {
                    _microBusyHinted = true;
                    _microIdleHinted = false;
                    _microHintCount++;
                    ChatDialog.ReceiveChat("微端已启用：所需资源会随游戏进度自动补齐，不影响当前操作。", ChatType.Hint);
                }
            }
            else if (!_microIdleHinted)
            {
                _microIdleHinted = true;
                _microBusyHinted = false;

                if (_microHintCount < 10)
                {
                    _microHintCount++;
                    ChatDialog.ReceiveChat(
                        string.Format("资源补齐完成，本次累计下载 {0} 个文件（{1}）。",
                            ResourceDownloader.CompletedCount,
                            Functions.ConvertByteSize(ResourceDownloader.DownloadedBytes)),
                        ChatType.Hint);
                }
            }
        }

        public override void Process()
        {
            if (MapControl == null || User == null)
                return;

            // 微端：让玩家看得见"后台正在悄悄补资源"，而不是遇到缺图时一脸茫然
            ProcessMicroHint();

            // 进图后自动恢复玩家上次勾选的辅助开关（免蜡/穿人/免助跑/超负重/泰山）
            RestorePlayerOptions();

            if (CMain.Time >= MoveTime)
            {
                MoveTime += 100; //Move Speed
                CanMove = true;
                MapControl.AnimationCount++;
                MapControl.TextureValid = false;
            }
            else
                CanMove = false;

            if (CMain.Time >= CMain.NextPing)
            {
                CMain.NextPing = CMain.Time + 60000;
                Network.Enqueue(new C.KeepAlive() { Time = CMain.Time });
            }

            TimerControl.Process();
            CompassControl.Process();
            RankingDialog.Process();

            MirItemCell cell = MouseControl as MirItemCell;

            if (cell != null && HoverItem != cell.Item && HoverItem != cell.ShadowItem)
            {
                DisposeItemLabel();
                HoverItem = null;
                CreateItemLabel(cell.Item);
            }

            if (ItemLabel != null && !ItemLabel.IsDisposed)
            {
                ItemLabel.BringToFront();

                int x = CMain.MPoint.X + 15, y = CMain.MPoint.Y;
                if (x + ItemLabel.Size.Width > Settings.ScreenWidth)
                    x = Settings.ScreenWidth - ItemLabel.Size.Width;

                if (y + ItemLabel.Size.Height > Settings.ScreenHeight)
                    y = Settings.ScreenHeight - ItemLabel.Size.Height;
                ItemLabel.Location = new Point(x, y);
            }

            if (MailLabel != null && !MailLabel.IsDisposed)
            {
                MailLabel.BringToFront();

                int x = CMain.MPoint.X + 15, y = CMain.MPoint.Y;
                if (x + MailLabel.Size.Width > Settings.ScreenWidth)
                    x = Settings.ScreenWidth - MailLabel.Size.Width;

                if (y + MailLabel.Size.Height > Settings.ScreenHeight)
                    y = Settings.ScreenHeight - MailLabel.Size.Height;
                MailLabel.Location = new Point(x, y);
            }

            if (MemoLabel != null && !MemoLabel.IsDisposed)
            {
                MemoLabel.BringToFront();

                int x = CMain.MPoint.X + 15, y = CMain.MPoint.Y;
                if (x + MemoLabel.Size.Width > Settings.ScreenWidth)
                    x = Settings.ScreenWidth - MemoLabel.Size.Width;

                if (y + MemoLabel.Size.Height > Settings.ScreenHeight)
                    y = Settings.ScreenHeight - MemoLabel.Size.Height;
                MemoLabel.Location = new Point(x, y);
            }

            if (GuildBuffLabel != null && !GuildBuffLabel.IsDisposed)
            {
                GuildBuffLabel.BringToFront();

                int x = CMain.MPoint.X + 15, y = CMain.MPoint.Y;
                if (x + GuildBuffLabel.Size.Width > Settings.ScreenWidth)
                    x = Settings.ScreenWidth - GuildBuffLabel.Size.Width;

                if (y + GuildBuffLabel.Size.Height > Settings.ScreenHeight)
                    y = Settings.ScreenHeight - GuildBuffLabel.Size.Height;
                GuildBuffLabel.Location = new Point(x, y);
            }

            if (!User.Dead) ShowReviveMessage = false;

            if (ShowReviveMessage && CMain.Time > User.DeadTime && User.CurrentAction == MirAction.死后尸体)
            {
                ShowReviveMessage = false;
                MirMessageBox messageBox = new MirMessageBox(GameLanguage.DiedTip, MirMessageBoxButtons.YesNo, false);

                messageBox.YesButton.Click += (o, e) =>
                {
                    if (User.Dead) Network.Enqueue(new C.TownRevive());
                };

                messageBox.AfterDraw += (o, e) =>
                {
                    if (!User.Dead) messageBox.Dispose();
                };

                messageBox.Show();
            }

            BuffsDialog.Process();
            HeroBuffsDialog?.Process();

            MapControl.Process();
            MainDialog.Process();
            InventoryDialog.Process();
            GameShopDialog.Process();
            MiniMapDialog.Process();

            foreach (SkillBarDialog Bar in Scene.SkillBarDialogs)
                Bar.Process();

            foreach (ParticleEngine pe in ParticleEngines)
                pe.Process();

            DialogProcess();

            ProcessOuput();

            UpdateMouseCursor();

            SoundManager.ProcessDelayedSounds();
        }

        public void DialogProcess()
        {
            if(Settings.SkillBar)
            {
                foreach (SkillBarDialog Bar in Scene.SkillBarDialogs)
                    Bar.Show();
            }
            else
            {
                foreach (SkillBarDialog Bar in Scene.SkillBarDialogs)
                    Bar.Hide();
            }

            for (int i = 0; i < Scene.SkillBarDialogs.Count; i++)
            {
                if (i * 2 > Settings.SkillbarLocation.Length) break;
                if ((Settings.SkillbarLocation[i, 0] > Settings.Resolution - 100) || (Settings.SkillbarLocation[i, 1] > 700)) continue;//in theory you'd want the y coord to be validated based on resolution, but since client only allows for wider screens and not higher :(
                Scene.SkillBarDialogs[i].Location = new Point(Settings.SkillbarLocation[i, 0], Settings.SkillbarLocation[i, 1]);
            }

            if (Settings.DuraView)
                CharacterDuraPanel.Show();
            else
                CharacterDuraPanel.Hide();
        }

        public override void ProcessPacket(Packet p)
        {
            switch (p.Index)
            {
                case (short)ServerPacketIds.KeepAlive:
                    KeepAlive((S.KeepAlive)p);
                    break;
                case (short)ServerPacketIds.MapInformation: //MapInfo
                    MapInformation((S.MapInformation)p);
                    break;
                case (short)ServerPacketIds.NewMapInfo:
                    NewMapInfo((S.NewMapInfo)p);
                    break;
                case (short)ServerPacketIds.WorldMapSetup:
                    WorldMapSetup((S.WorldMapSetupInfo)p);
                    break;
                case (short)ServerPacketIds.SearchMapResult:
                    SearchMapResult((S.SearchMapResult)p);
                    break;
                case (short)ServerPacketIds.UserInformation:
                    UserInformation((S.UserInformation)p);
                    break;
                case (short)ServerPacketIds.UserSlotsRefresh:
                    UserSlotsRefresh((S.UserSlotsRefresh)p);
                    break;
                case (short)ServerPacketIds.UserLocation:
                    UserLocation((S.UserLocation)p);
                    break;
                case (short)ServerPacketIds.ObjectPlayer:
                    ObjectPlayer((S.ObjectPlayer)p);
                    break;
                case (short)ServerPacketIds.ObjectHero:
                    ObjectHero((S.ObjectHero)p);
                    break;
                case (short)ServerPacketIds.ObjectRemove:
                    ObjectRemove((S.ObjectRemove)p);
                    break;
                case (short)ServerPacketIds.ObjectTurn:
                    ObjectTurn((S.ObjectTurn)p);
                    break;
                case (short)ServerPacketIds.ObjectWalk:
                    ObjectWalk((S.ObjectWalk)p);
                    break;
                case (short)ServerPacketIds.ObjectRun:
                    ObjectRun((S.ObjectRun)p);
                    break;
                case (short)ServerPacketIds.Chat:
                    ReceiveChat((S.Chat)p);
                    break;
                case (short)ServerPacketIds.ObjectChat:
                    ObjectChat((S.ObjectChat)p);
                    break;
                case (short)ServerPacketIds.MoveItem:
                    MoveItem((S.MoveItem)p);
                    break;
                case (short)ServerPacketIds.EquipItem:
                    EquipItem((S.EquipItem)p);
                    break;
                case (short)ServerPacketIds.MergeItem:
                    MergeItem((S.MergeItem)p);
                    break;
                case (short)ServerPacketIds.RemoveItem:
                    RemoveItem((S.RemoveItem)p);
                    break;
                case (short)ServerPacketIds.RemoveSlotItem:
                    RemoveSlotItem((S.RemoveSlotItem)p);
                    break;
                case (short)ServerPacketIds.TakeBackItem:
                    TakeBackItem((S.TakeBackItem)p);
                    break;
                case (short)ServerPacketIds.StoreItem:
                    StoreItem((S.StoreItem)p);
                    break;
                case (short)ServerPacketIds.DepositRefineItem:
                    DepositRefineItem((S.DepositRefineItem)p);
                    break;
                case (short)ServerPacketIds.RetrieveRefineItem:
                    RetrieveRefineItem((S.RetrieveRefineItem)p);
                    break;
                case (short)ServerPacketIds.RefineCancel:
                    RefineCancel((S.RefineCancel)p);
                    break;
                case (short)ServerPacketIds.RefineItem:
                    RefineItem((S.RefineItem)p);
                    break;
                case (short)ServerPacketIds.DepositTradeItem:
                    DepositTradeItem((S.DepositTradeItem)p);
                    break;
                case (short)ServerPacketIds.RetrieveTradeItem:
                    RetrieveTradeItem((S.RetrieveTradeItem)p);
                    break;
                case (short)ServerPacketIds.SplitItem:
                    SplitItem((S.SplitItem)p);
                    break;
                case (short)ServerPacketIds.SplitItem1:
                    SplitItem1((S.SplitItem1)p);
                    break;
                case (short)ServerPacketIds.UseItem:
                    UseItem((S.UseItem)p);
                    break;
                case (short)ServerPacketIds.DropItem:
                    DropItem((S.DropItem)p);
                    break;
                case (short)ServerPacketIds.TakeBackHeroItem:
                    TakeBackHeroItem((S.TakeBackHeroItem)p);
                    break;
                case (short)ServerPacketIds.TransferHeroItem:
                    TransferHeroItem((S.TransferHeroItem)p);
                    break;
                case (short)ServerPacketIds.PlayerUpdate:
                    PlayerUpdate((S.PlayerUpdate)p);
                    break;
                case (short)ServerPacketIds.PlayerInspect:
                    PlayerInspect((S.PlayerInspect)p);
                    break;
                case (short)ServerPacketIds.LogOutSuccess:
                    LogOutSuccess((S.LogOutSuccess)p);
                    break;
                case (short)ServerPacketIds.LogOutFailed:
                    LogOutFailed((S.LogOutFailed)p);
                    break;
                case (short)ServerPacketIds.ReturnToLogin:
                    ReturnToLogin((S.ReturnToLogin)p);
                    break;
                case (short)ServerPacketIds.TimeOfDay:
                    TimeOfDay((S.TimeOfDay)p);
                    break;
                case (short)ServerPacketIds.ChangeAMode:
                    ChangeAMode((S.ChangeAMode)p);
                    break;
                case (short)ServerPacketIds.ChangePMode:
                    ChangePMode((S.ChangePMode)p);
                    break;
                case (short)ServerPacketIds.ObjectItem:
                    ObjectItem((S.ObjectItem)p);
                    break;
                case (short)ServerPacketIds.ObjectGold:
                    ObjectGold((S.ObjectGold)p);
                    break;
                case (short)ServerPacketIds.GainedItem:
                    GainedItem((S.GainedItem)p);
                    break;
                case (short)ServerPacketIds.GainedGold:
                    GainedGold((S.GainedGold)p);
                    break;
                case (short)ServerPacketIds.LoseGold:
                    LoseGold((S.LoseGold)p);
                    break;
                case (short)ServerPacketIds.GainedCredit:
                    GainedCredit((S.GainedCredit)p);
                    break;
                case (short)ServerPacketIds.LoseCredit:
                    LoseCredit((S.LoseCredit)p);
                    break;
                case (short)ServerPacketIds.ObjectMonster:
                    ObjectMonster((S.ObjectMonster)p);
                    break;
                case (short)ServerPacketIds.ObjectAttack:
                    ObjectAttack((S.ObjectAttack)p);
                    break;
                case (short)ServerPacketIds.Struck:
                    Struck((S.Struck)p);
                    break;
                case (short)ServerPacketIds.DamageIndicator:
                    DamageIndicator((S.DamageIndicator)p);
                    break;
                case (short)ServerPacketIds.ObjectStruck:
                    ObjectStruck((S.ObjectStruck)p);
                    break;
                case (short)ServerPacketIds.DuraChanged:
                    DuraChanged((S.DuraChanged)p);
                    break;
                case (short)ServerPacketIds.HealthChanged:
                    HealthChanged((S.HealthChanged)p);
                    break;
                case (short)ServerPacketIds.HeroHealthChanged:
                    HeroHealthChanged((S.HeroHealthChanged)p);
                    break;
                case (short)ServerPacketIds.DeleteItem:
                    DeleteItem((S.DeleteItem)p);
                    break;
                case (short)ServerPacketIds.Death:
                    Death((S.Death)p);
                    break;
                case (short)ServerPacketIds.ObjectDied:
                    ObjectDied((S.ObjectDied)p);
                    break;
                case (short)ServerPacketIds.ColourChanged:
                    ColourChanged((S.ColourChanged)p);
                    break;
                case (short)ServerPacketIds.ObjectColourChanged:
                    ObjectColourChanged((S.ObjectColourChanged)p);
                    break;
                case (short)ServerPacketIds.ObjectGuildNameChanged:
                    ObjectGuildNameChanged((S.ObjectGuildNameChanged)p);
                    break;
                case (short)ServerPacketIds.GainExperience:
                    GainExperience((S.GainExperience)p);
                    break;
                case (short)ServerPacketIds.GainHeroExperience:
                    GainHeroExperience((S.GainHeroExperience)p);
                    break;
                case (short)ServerPacketIds.LevelChanged:
                    LevelChanged((S.LevelChanged)p);
                    break;
                case (short)ServerPacketIds.HeroLevelChanged:
                    HeroLevelChanged((S.HeroLevelChanged)p);
                    break;
                case (short)ServerPacketIds.ObjectLeveled:
                    ObjectLeveled((S.ObjectLeveled)p);
                    break;
                case (short)ServerPacketIds.ObjectHarvest:
                    ObjectHarvest((S.ObjectHarvest)p);
                    break;
                case (short)ServerPacketIds.ObjectHarvested:
                    ObjectHarvested((S.ObjectHarvested)p);
                    break;
                case (short)ServerPacketIds.ObjectNpc:
                    ObjectNPC((S.ObjectNPC)p);
                    break;
                case (short)ServerPacketIds.NPCResponse:
                    NPCResponse((S.NPCResponse)p);
                    break;
                case (short)ServerPacketIds.ObjectHide:
                    ObjectHide((S.ObjectHide)p);
                    break;
                case (short)ServerPacketIds.ObjectShow:
                    ObjectShow((S.ObjectShow)p);
                    break;
                case (short)ServerPacketIds.Poisoned:
                    Poisoned((S.Poisoned)p);
                    break;
                case (short)ServerPacketIds.ObjectPoisoned:
                    ObjectPoisoned((S.ObjectPoisoned)p);
                    break;
                case (short)ServerPacketIds.MapChanged:
                    MapChanged((S.MapChanged)p);
                    break;
                case (short)ServerPacketIds.ObjectTeleportOut:
                    ObjectTeleportOut((S.ObjectTeleportOut)p);
                    break;
                case (short)ServerPacketIds.ObjectTeleportIn:
                    ObjectTeleportIn((S.ObjectTeleportIn)p);
                    break;
                case (short)ServerPacketIds.TeleportIn:
                    TeleportIn();
                    break;
                case (short)ServerPacketIds.NPCGoods:
                    NPCGoods((S.NPCGoods)p);
                    break;
                case (short)ServerPacketIds.NPCSell:
                    NPCSell();
                    break;
                case (short)ServerPacketIds.NPCRepair:
                    NPCRepair((S.NPCRepair)p);
                    break;
                case (short)ServerPacketIds.NPCSRepair:
                    NPCSRepair((S.NPCSRepair)p);
                    break;
                case (short)ServerPacketIds.NPCRefine:
                    NPCRefine((S.NPCRefine)p);
                    break;
                case (short)ServerPacketIds.NPCCheckRefine:
                    NPCCheckRefine((S.NPCCheckRefine)p);
                    break;
                case (short)ServerPacketIds.NPCCollectRefine:
                    NPCCollectRefine((S.NPCCollectRefine)p);
                    break;
                case (short)ServerPacketIds.NPCReplaceWedRing:
                    NPCReplaceWedRing((S.NPCReplaceWedRing)p);
                    break;
                case (short)ServerPacketIds.NPCStorage:
                    NPCStorage();
                    break;
                case (short)ServerPacketIds.NPCRequestInput:
                    NPCRequestInput((S.NPCRequestInput)p);
                    break;
                case (short)ServerPacketIds.SellItem:
                    SellItem((S.SellItem)p);
                    break;
                case (short)ServerPacketIds.CraftItem:
                    CraftItem((S.CraftItem)p);
                    break;
                case (short)ServerPacketIds.RepairItem:
                    RepairItem((S.RepairItem)p);
                    break;
                case (short)ServerPacketIds.ItemRepaired:
                    ItemRepaired((S.ItemRepaired)p);
                    break;
                case (short)ServerPacketIds.ItemSlotSizeChanged:
                    ItemSlotSizeChanged((S.ItemSlotSizeChanged)p);
                    break;
                case (short)ServerPacketIds.ItemSealChanged:
                    ItemSealChanged((S.ItemSealChanged)p);
                    break;
                case (short)ServerPacketIds.NewMagic:
                    NewMagic((S.NewMagic)p);
                    break;
                case (short)ServerPacketIds.MagicLeveled:
                    MagicLeveled((S.MagicLeveled)p);
                    break;
                case (short)ServerPacketIds.Magic:
                    Magic((S.Magic)p);
                    break;
                case (short)ServerPacketIds.MagicDelay:
                    MagicDelay((S.MagicDelay)p);
                    break;
                case (short)ServerPacketIds.MagicCast:
                    MagicCast((S.MagicCast)p);
                    break;
                case (short)ServerPacketIds.ObjectMagic:
                    ObjectMagic((S.ObjectMagic)p);
                    break;
                case (short)ServerPacketIds.ObjectProjectile:
                    ObjectProjectile((S.ObjectProjectile)p);
                    break;
                case (short)ServerPacketIds.ObjectEffect:
                    ObjectEffect((S.ObjectEffect)p);
                    break;
                case (short)ServerPacketIds.RangeAttack:
                    RangeAttack((S.RangeAttack)p);
                    break;
                case (short)ServerPacketIds.Pushed:
                    Pushed((S.Pushed)p);
                    break;
                case (short)ServerPacketIds.ObjectPushed:
                    ObjectPushed((S.ObjectPushed)p);
                    break;
                case (short)ServerPacketIds.ObjectName:
                    ObjectName((S.ObjectName)p);
                    break;
                case (short)ServerPacketIds.UserStorage:
                    UserStorage((S.UserStorage)p);
                    break;
                case (short)ServerPacketIds.SwitchGroup:
                    SwitchGroup((S.SwitchGroup)p);
                    break;
                case (short)ServerPacketIds.DeleteGroup:
                    DeleteGroup();
                    break;
                case (short)ServerPacketIds.DeleteMember:
                    DeleteMember((S.DeleteMember)p);
                    break;
                case (short)ServerPacketIds.GroupInvite:
                    GroupInvite((S.GroupInvite)p);
                    break;
                case (short)ServerPacketIds.AddMember:
                    AddMember((S.AddMember)p);
                    break;
                case (short)ServerPacketIds.GroupMembersMap:
                    GroupMembersMap((S.GroupMembersMap)p);
                    break;
                case (short)ServerPacketIds.SendMemberLocation:
                    SendMemberLocation((S.SendMemberLocation)p);
                    break;
                case (short)ServerPacketIds.Revived:
                    Revived();
                    break;
                case (short)ServerPacketIds.ObjectRevived:
                    ObjectRevived((S.ObjectRevived)p);
                    break;
                case (short)ServerPacketIds.SpellToggle:
                    SpellToggle((S.SpellToggle)p);
                    break;
                case (short)ServerPacketIds.ObjectHealth:
                    ObjectHealth((S.ObjectHealth)p);
                    break;
                case (short)ServerPacketIds.ObjectMana:
                    ObjectMana((S.ObjectMana)p);
                    break;
                case (short)ServerPacketIds.MapEffect:
                    MapEffect((S.MapEffect)p);
                    break;
                case (short)ServerPacketIds.AllowObserve:
                    AllowObserve = ((S.AllowObserve)p).Allow;
                    break;
                case (short)ServerPacketIds.ObjectRangeAttack:
                    ObjectRangeAttack((S.ObjectRangeAttack)p);
                    break;
                case (short)ServerPacketIds.AddBuff:
                    AddBuff((S.AddBuff)p);
                    break;
                case (short)ServerPacketIds.RemoveBuff:
                    RemoveBuff((S.RemoveBuff)p);
                    break;
                case (short)ServerPacketIds.PauseBuff:
                    PauseBuff((S.PauseBuff)p);
                    break;
                case (short)ServerPacketIds.ObjectHidden:
                    ObjectHidden((S.ObjectHidden)p);
                    break;
                case (short)ServerPacketIds.RefreshItem:
                    RefreshItem((S.RefreshItem)p);
                    break;
                case (short)ServerPacketIds.ObjectSpell:
                    ObjectSpell((S.ObjectSpell)p);
                    break;
                case (short)ServerPacketIds.UserDash:
                    UserDash((S.UserDash)p);
                    break;
                case (short)ServerPacketIds.ObjectDash:
                    ObjectDash((S.ObjectDash)p);
                    break;
                case (short)ServerPacketIds.UserDashFail:
                    UserDashFail((S.UserDashFail)p);
                    break;
                case (short)ServerPacketIds.ObjectDashFail:
                    ObjectDashFail((S.ObjectDashFail)p);
                    break;
                case (short)ServerPacketIds.NPCConsign:
                    NPCConsign();
                    break;
                case (short)ServerPacketIds.NPCMarket:
                    NPCMarket((S.NPCMarket)p);
                    break;
                case (short)ServerPacketIds.NPCMarketPage:
                    NPCMarketPage((S.NPCMarketPage)p);
                    break;
                case (short)ServerPacketIds.ConsignItem:
                    ConsignItem((S.ConsignItem)p);
                    break;
                case (short)ServerPacketIds.MarketFail:
                    MarketFail((S.MarketFail)p);
                    break;
                case (short)ServerPacketIds.MarketSuccess:
                    MarketSuccess((S.MarketSuccess)p);
                    break;
                case (short)ServerPacketIds.ObjectSitDown:
                    ObjectSitDown((S.ObjectSitDown)p);
                    break;
                case (short)ServerPacketIds.InTrapRock:
                    S.InTrapRock packetdata = (S.InTrapRock)p;
                    User.InTrapRock = packetdata.Trapped;
                    break;
                case (short)ServerPacketIds.RemoveMagic:
                    RemoveMagic((S.RemoveMagic)p);
                    break;
                case (short)ServerPacketIds.BaseStatsInfo:
                    BaseStatsInfo((S.BaseStatsInfo)p);
                    break;
                case (short)ServerPacketIds.HeroBaseStatsInfo:
                    HeroBaseStatsInfo((S.HeroBaseStatsInfo)p);
                    break;
                case (short)ServerPacketIds.UserName:
                    UserName((S.UserName)p);
                    break;
                case (short)ServerPacketIds.ChatItemStats:
                    ChatItemStats((S.ChatItemStats)p);
                    break;
                case (short)ServerPacketIds.GuildInvite:
                    GuildInvite((S.GuildInvite)p);
                    break;
                case (short)ServerPacketIds.GuildMemberChange:
                    GuildMemberChange((S.GuildMemberChange)p);
                    break;
                case (short)ServerPacketIds.GuildNoticeChange:
                    GuildNoticeChange((S.GuildNoticeChange)p);
                    break;
                case (short)ServerPacketIds.GuildStatus:
                    GuildStatus((S.GuildStatus)p);
                    break;
                case (short)ServerPacketIds.GuildExpGain:
                    GuildExpGain((S.GuildExpGain)p);
                    break;
                case (short)ServerPacketIds.GuildNameRequest:
                    GuildNameRequest((S.GuildNameRequest)p);
                    break;
                case (short)ServerPacketIds.GuildStorageGoldChange:
                    GuildStorageGoldChange((S.GuildStorageGoldChange)p);
                    break;
                case (short)ServerPacketIds.GuildStorageItemChange:
                    GuildStorageItemChange((S.GuildStorageItemChange)p);
                    break;
                case (short)ServerPacketIds.GuildStorageList:
                    GuildStorageList((S.GuildStorageList)p);
                    break;
                case (short)ServerPacketIds.GuildRequestWar:
                    GuildRequestWar((S.GuildRequestWar)p);
                    break;
                case (short)ServerPacketIds.HeroCreateRequest:
                    HeroCreateRequest((S.HeroCreateRequest)p);
                    break;
                case (short)ServerPacketIds.NewHero:
                    NewHero((S.NewHero)p);
                    break;
                case (short)ServerPacketIds.HeroInformation:
                    HeroInformation((S.HeroInformation)p);
                    break;
                case (short)ServerPacketIds.UpdateHeroSpawnState:
                    UpdateHeroSpawnState((S.UpdateHeroSpawnState)p);
                    break;
                case (short)ServerPacketIds.UnlockHeroAutoPot:
                    UnlockHeroAutoPot(true);
                    break;
                case (short)ServerPacketIds.SetAutoPotValue:
                    SetAutoPotValue((S.SetAutoPotValue)p);
                    break;
                case (short)ServerPacketIds.SetHeroBehaviour:
                    SetHeroBehaviour((S.SetHeroBehaviour)p);
                    break;
                case (short)ServerPacketIds.SetAutoPotItem:
                    SetAutoPotItem((S.SetAutoPotItem)p);
                    break;
                case (short)ServerPacketIds.ManageHeroes:
                    ManageHeroes((S.ManageHeroes)p);
                    break;
                case (short)ServerPacketIds.ChangeHero:
                    ChangeHero((S.ChangeHero)p);
                    break;
                case (short)ServerPacketIds.DefaultNPC:
                    DefaultNPC((S.DefaultNPC)p);
                    break;
                case (short)ServerPacketIds.NPCUpdate:
                    NPCUpdate((S.NPCUpdate)p);
                    break;
                case (short)ServerPacketIds.NPCImageUpdate:
                    NPCImageUpdate((S.NPCImageUpdate)p);
                    break;
                case (short)ServerPacketIds.MarriageRequest:
                    MarriageRequest((S.MarriageRequest)p);
                    break;
                case (short)ServerPacketIds.DivorceRequest:
                    DivorceRequest((S.DivorceRequest)p);
                    break;
                case (short)ServerPacketIds.MentorRequest:
                    MentorRequest((S.MentorRequest)p);
                    break;
                case (short)ServerPacketIds.TradeRequest:
                    TradeRequest((S.TradeRequest)p);
                    break;
                case (short)ServerPacketIds.TradeAccept:
                    TradeAccept((S.TradeAccept)p);
                    break;
                case (short)ServerPacketIds.TradeGold:
                    TradeGold((S.TradeGold)p);
                    break;
                case (short)ServerPacketIds.TradeItem:
                    TradeItem((S.TradeItem)p);
                    break;
                case (short)ServerPacketIds.TradeConfirm:
                    TradeConfirm();
                    break;
                case (short)ServerPacketIds.TradeCancel:
                    TradeCancel((S.TradeCancel)p);
                    break;
                case (short)ServerPacketIds.MountUpdate:
                    MountUpdate((S.MountUpdate)p);
                    break;
                case (short)ServerPacketIds.TransformUpdate:
                    TransformUpdate((S.TransformUpdate)p);
                    break;
                case (short)ServerPacketIds.EquipSlotItem:
                    EquipSlotItem((S.EquipSlotItem)p);
                    break;
                case (short)ServerPacketIds.FishingUpdate:
                    FishingUpdate((S.FishingUpdate)p);
                    break;
                case (short)ServerPacketIds.ChangeQuest:
                    ChangeQuest((S.ChangeQuest)p);
                    break;
                case (short)ServerPacketIds.CompleteQuest:
                    CompleteQuest((S.CompleteQuest)p);
                    break;
                case (short)ServerPacketIds.ShareQuest:
                    ShareQuest((S.ShareQuest)p);
                    break;
                case (short)ServerPacketIds.GainedQuestItem:
                    GainedQuestItem((S.GainedQuestItem)p);
                    break;
                case (short)ServerPacketIds.DeleteQuestItem:
                    DeleteQuestItem((S.DeleteQuestItem)p);
                    break;
                case (short)ServerPacketIds.CancelReincarnation:
                    User.ReincarnationStopTime = 0;
                    break;
                case (short)ServerPacketIds.RequestReincarnation:
                    if (!User.Dead) return;
                    RequestReincarnation();
                    break;
                case (short)ServerPacketIds.UserBackStep:
                    UserBackStep((S.UserBackStep)p);
                    break;
                case (short)ServerPacketIds.ObjectBackStep:
                    ObjectBackStep((S.ObjectBackStep)p);
                    break;
                case (short)ServerPacketIds.UserDashAttack:
                    UserDashAttack((S.UserDashAttack)p);
                    break;
                case (short)ServerPacketIds.ObjectDashAttack:
                    ObjectDashAttack((S.ObjectDashAttack)p);
                    break;
                case (short)ServerPacketIds.UserAttackMove://Warrior Skill - SlashingBurst
                    UserAttackMove((S.UserAttackMove)p);
                    break;
                case (short)ServerPacketIds.CombineItem:
                    CombineItem((S.CombineItem)p);
                    break;
                case (short)ServerPacketIds.ItemUpgraded:
                    ItemUpgraded((S.ItemUpgraded)p);
                    break;
                case (short)ServerPacketIds.SetConcentration:
                    SetConcentration((S.SetConcentration)p);
                    break;
                case (short)ServerPacketIds.SetElemental:
                    SetElemental((S.SetElemental)p);
                    break;
                case (short)ServerPacketIds.RemoveDelayedExplosion:
                    RemoveDelayedExplosion((S.RemoveDelayedExplosion)p);
                    break;
                case (short)ServerPacketIds.ObjectDeco:
                    ObjectDeco((S.ObjectDeco)p);
                    break;
                case (short)ServerPacketIds.ObjectSneaking:
                    ObjectSneaking((S.ObjectSneaking)p);
                    break;
                case (short)ServerPacketIds.ObjectLevelEffects:
                    ObjectLevelEffects((S.ObjectLevelEffects)p);
                    break;
                case (short)ServerPacketIds.SetBindingShot:
                    SetBindingShot((S.SetBindingShot)p);
                    break;
                case (short)ServerPacketIds.SendOutputMessage:
                    SendOutputMessage((S.SendOutputMessage)p);
                    break;
                case (short)ServerPacketIds.NPCAwakening:
                    NPCAwakening();
                    break;
                case (short)ServerPacketIds.NPCDisassemble:
                    NPCDisassemble();
                    break;
                case (short)ServerPacketIds.NPCDowngrade:
                    NPCDowngrade();
                    break;
                case (short)ServerPacketIds.NPCReset:
                    NPCReset();
                    break;
                case (short)ServerPacketIds.AwakeningNeedMaterials:
                    AwakeningNeedMaterials((S.AwakeningNeedMaterials)p);
                    break;
                case (short)ServerPacketIds.AwakeningLockedItem:
                    AwakeningLockedItem((S.AwakeningLockedItem)p);
                    break;
                case (short)ServerPacketIds.Awakening:
                    Awakening((S.Awakening)p);
                    break;
                case (short)ServerPacketIds.ReceiveMail:
                    ReceiveMail((S.ReceiveMail)p);
                    break;
                case (short)ServerPacketIds.MailLockedItem:
                    MailLockedItem((S.MailLockedItem)p);
                    break;
                case (short)ServerPacketIds.MailSent:
                    MailSent((S.MailSent)p);
                    break;
                case (short)ServerPacketIds.MailSendRequest:
                    MailSendRequest((S.MailSendRequest)p);
                    break;
                case (short)ServerPacketIds.ParcelCollected:
                    ParcelCollected((S.ParcelCollected)p);
                    break;
                case (short)ServerPacketIds.MailCost:
                    MailCost((S.MailCost)p);
                    break;
                case (short)ServerPacketIds.ResizeInventory:
                    ResizeInventory((S.ResizeInventory)p);
                    break;
                case (short)ServerPacketIds.ResizeStorage:
                    ResizeStorage((S.ResizeStorage)p);
                    break;
                case (short)ServerPacketIds.NewIntelligentCreature:
                    NewIntelligentCreature((S.NewIntelligentCreature)p);
                    break;
                case (short)ServerPacketIds.UpdateIntelligentCreatureList:
                    UpdateIntelligentCreatureList((S.UpdateIntelligentCreatureList)p);
                    break;
                case (short)ServerPacketIds.IntelligentCreatureEnableRename:
                    IntelligentCreatureEnableRename((S.IntelligentCreatureEnableRename)p);
                    break;
                case (short)ServerPacketIds.IntelligentCreaturePickup:
                    IntelligentCreaturePickup((S.IntelligentCreaturePickup)p);
                    break;
                case (short)ServerPacketIds.NPCPearlGoods:
                    NPCPearlGoods((S.NPCPearlGoods)p);
                    break;
                case (short)ServerPacketIds.FriendUpdate:
                    FriendUpdate((S.FriendUpdate)p);
                    break;
                case (short)ServerPacketIds.LoverUpdate:
                    LoverUpdate((S.LoverUpdate)p);
                    break;
                case (short)ServerPacketIds.MentorUpdate:
                    MentorUpdate((S.MentorUpdate)p);
                    break;
                case (short)ServerPacketIds.GuildBuffList:
                    GuildBuffList((S.GuildBuffList)p);
                    break;
                case (short)ServerPacketIds.GameShopInfo:
                    GameShopUpdate((S.GameShopInfo)p);
                    break;
                case (short)ServerPacketIds.GameShopStock:
                    GameShopStock((S.GameShopStock)p);
                    break;
                case (short)ServerPacketIds.Rankings:
                    Rankings((S.Rankings)p);
                    break;
                case (short)ServerPacketIds.Opendoor:
                    Opendoor((S.Opendoor)p);
                    break;
                case (short)ServerPacketIds.GetRentedItems:
                    RentedItems((S.GetRentedItems) p);
                    break;
                case (short)ServerPacketIds.ItemRentalRequest:
                    ItemRentalRequest((S.ItemRentalRequest)p);
                    break;
                case (short)ServerPacketIds.ItemRentalFee:
                    ItemRentalFee((S.ItemRentalFee)p);
                    break;
                case (short)ServerPacketIds.ItemRentalPeriod:
                    ItemRentalPeriod((S.ItemRentalPeriod)p);
                    break;
                case (short)ServerPacketIds.DepositRentalItem:
                    DepositRentalItem((S.DepositRentalItem)p);
                    break;
                case (short)ServerPacketIds.RetrieveRentalItem:
                    RetrieveRentalItem((S.RetrieveRentalItem)p);
                    break;
                case (short)ServerPacketIds.UpdateRentalItem:
                    UpdateRentalItem((S.UpdateRentalItem)p);
                    break;
                case (short)ServerPacketIds.CancelItemRental:
                    CancelItemRental((S.CancelItemRental)p);
                    break;
                case (short)ServerPacketIds.ItemRentalLock:
                    ItemRentalLock((S.ItemRentalLock)p);
                    break;
                case (short)ServerPacketIds.ItemRentalPartnerLock:
                    ItemRentalPartnerLock((S.ItemRentalPartnerLock)p);
                    break;
                case (short)ServerPacketIds.CanConfirmItemRental:
                    CanConfirmItemRental((S.CanConfirmItemRental)p);
                    break;
                case (short)ServerPacketIds.ConfirmItemRental:
                    ConfirmItemRental((S.ConfirmItemRental)p);
                    break;
                case (short)ServerPacketIds.OpenBrowser:                  
                    OpenBrowser((S.OpenBrowser)p);
                    break;
                case (short)ServerPacketIds.PlaySound:
                    PlaySound((S.PlaySound)p);
                    break;
                case (short)ServerPacketIds.SetTimer:
                    SetTimer((S.SetTimer)p);
                    break;
                case (short)ServerPacketIds.ExpireTimer:
                    ExpireTimer((S.ExpireTimer)p);
                    break;
                case (short)ServerPacketIds.PlayerOption:
                    PlayerOption((S.PlayerOption)p);
                    break;
                case (short)ServerPacketIds.UpdateNotice:
                    ShowNotice((S.UpdateNotice)p);
                    break;
                case (short)ServerPacketIds.Roll:
                    Roll((S.Roll)p);
                    break;
                case (short)ServerPacketIds.SetCompass:
                    SetCompass((S.SetCompass)p);
                    break;
                default:
                    base.ProcessPacket(p);
                    break;
            }
        }

        private void KeepAlive(S.KeepAlive p)
        {
            if (p.Time == 0) return;
            CMain.PingTime = (CMain.Time - p.Time);
        }
        private void MapInformation(S.MapInformation p)
        {
            if (MapControl != null && !MapControl.IsDisposed)
                MapControl.Dispose();
            MapControl = new MapControl { Index = p.MapIndex, FileName = Path.Combine(Settings.MapPath, p.FileName + ".map"), Title = p.Title, MiniMap = p.MiniMap, BigMap = p.BigMap, Lights = p.Lights, Lightning = p.Lightning, Fire = p.Fire, MapDarkLight = p.MapDarkLight, Music = p.Music};
            MapControl.Weather = p.WeatherParticles;
            MapControl.LoadMap();
            InsertControl(0, MapControl);
        }

        private void WorldMapSetup(S.WorldMapSetupInfo info)
        {
            BigMapDialog.WorldMapSetup(info.Setup);
            TeleportToNPCCost = info.TeleportToNPCCost;
        }        

        private void NewMapInfo(S.NewMapInfo info)
        {
            BigMapRecord newRecord = new BigMapRecord() { Index = info.MapIndex, MapInfo = info.Info };
            CreateBigMapButtons(newRecord);           
            MapInfoList.Add(info.MapIndex, newRecord);
        }

        private void CreateBigMapButtons(BigMapRecord record)
        {
            record.MovementButtons.Clear();
            record.NPCButtons.Clear();

            foreach (ClientMovementInfo mInfo in record.MapInfo.Movements)
            {
                MirButton button = new MirButton()
                {
                    Library = Libraries.MapLinkIcon,
                    Index = mInfo.Icon,
                    PressedIndex = mInfo.Icon,
                    Sound = SoundList.ButtonA,
                    Parent = BigMapDialog.ViewPort,
                    Location = new Point(20, 38),
                    Hint = mInfo.Title,
                    Visible = false
                };
                button.MouseEnter += (o, e) =>
                {
                    BigMapDialog.MouseLocation = mInfo.Location;
                };

                button.Click += (o, e) =>
                {
                    BigMapDialog.SetTargetMap(mInfo.Destination);
                };
                record.MovementButtons.Add(mInfo, button);
            }

            foreach (ClientNPCInfo npcInfo in record.MapInfo.NPCs)
            {
                BigMapNPCRow row = new BigMapNPCRow(npcInfo) { Parent = BigMapDialog };
                record.NPCButtons.Add(row);
            }
        }

        private void RecreateBigMapButtons()
        {
            foreach (var record in MapInfoList.Values)
                CreateBigMapButtons(record);
        }

        private void SearchMapResult(S.SearchMapResult info)
        {
            if (info.MapIndex == -1 && info.NPCIndex == 0)
            {
                MirMessageBox messageBox = new MirMessageBox("未找到此地图", MirMessageBoxButtons.OK);
                messageBox.OKButton.Click += (o, a) =>
                {
                    BigMapDialog.SearchTextBox.SetFocus();
                };
                messageBox.Show();
                return;
            }

            BigMapDialog.SetTargetMap(info.MapIndex);
            BigMapDialog.SetTargetNPC(info.NPCIndex);
        }
        private void UserInformation(S.UserInformation p)
        {
            User = new UserObject(p.ObjectID);
            User.Load(p);
            MainDialog.PModeLabel.Visible = User.Class == MirClass.法师 || User.Class == MirClass.道士;
            HasHero = p.HasHero;
            HeroBehaviourPanel.UpdateBehaviour(p.HeroBehaviour);
            HeroAIDialog.UpdateBehaviour(p.HeroBehaviour);
            Gold = p.Gold;
            Credit = p.Credit;

            CharacterDialog = new CharacterDialog(MirGridType.Equipment, User) { Parent = this, Visible = false };
            InventoryDialog.RefreshInventory();
            foreach (SkillBarDialog Bar in SkillBarDialogs)
                Bar.Update();
            AllowObserve = p.AllowObserve;
            Observing = p.Observer;
        }
        private void UserSlotsRefresh(S.UserSlotsRefresh p)
        {
            User.SetSlots(p);
        }

        private void UserLocation(S.UserLocation p)
        {
            MapControl.NextAction = 0;
            if (User.CurrentLocation == p.Location && User.Direction == p.Direction) return;

            if (Settings.DebugMode)
            {
                ReceiveChat(new S.Chat { Message = "Displacement", Type = ChatType.System });
            }

            MapControl.RemoveObject(User);
            User.CurrentLocation = p.Location;
            User.MapLocation = p.Location;
            MapControl.AddObject(User);

            MapControl.FloorValid = false;
            MapControl.InputDelay = CMain.Time + 400;

            if (User.Dead) return;

            User.ClearMagic();
            User.QueuedAction = null;

            for (int i = User.ActionFeed.Count - 1; i >= 0; i--)
            {
                if (User.ActionFeed[i].Action == MirAction.推开动作) continue;
                User.ActionFeed.RemoveAt(i);
            }

            User.SetAction();
        }
        private void ReceiveChat(S.Chat p)
        {
            ChatDialog.ReceiveChat(p.Message, p.Type);
        }
        private void ObjectPlayer(S.ObjectPlayer p)
        {
            PlayerObject player = new PlayerObject(p.ObjectID);
            player.Load(p);
        }

        private void ObjectHero(S.ObjectHero p)
        {
            HeroObject hero = new HeroObject(p.ObjectID);
            hero.Load(p);

            if (p.ObjectID == Hero?.ObjectID)
                HeroObject = hero;
        }

        private void ObjectRemove(S.ObjectRemove p)
        {
            if (p.ObjectID == User.ObjectID) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;

                ob.Remove();
            }
        }
        private void ObjectTurn(S.ObjectTurn p)
        {
            if (p.ObjectID == User.ObjectID && !Observing) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.站立动作, Direction = p.Direction, Location = p.Location });
                return;
            }
        }
        private void ObjectWalk(S.ObjectWalk p)
        {
            if (p.ObjectID == User.ObjectID && !Observing) return;

            if (p.ObjectID == Hero?.ObjectID)
                Hero.CurrentLocation = p.Location;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.行走动作, Direction = p.Direction, Location = p.Location });
                return;
            }
        }
        private void ObjectRun(S.ObjectRun p)
        {
            if (p.ObjectID == User.ObjectID && !Observing) return;

            if (p.ObjectID == Hero?.ObjectID)
                Hero.CurrentLocation = p.Location;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.跑步动作, Direction = p.Direction, Location = p.Location });
                return;
            }
        }
        private void ObjectChat(S.ObjectChat p)
        {
            ChatDialog.ReceiveChat(p.Text, p.Type);

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.Chat(RegexFunctions.CleanChatString(p.Text));
                return;
            }

        }
        private void MoveItem(S.MoveItem p)
        {
            MirItemCell toCell, fromCell;

            switch (p.Grid)
            {
                case MirGridType.Inventory:
                    fromCell = p.From < User.BeltIdx ? BeltDialog.Grid[p.From] : InventoryDialog.Grid[p.From - User.BeltIdx];
                    break;
                case MirGridType.Storage:
                    fromCell = StorageDialog.Grid[p.From];
                    break;
                case MirGridType.Trade:
                    fromCell = TradeDialog.Grid[p.From];
                    break;
                case MirGridType.Refine:
                    fromCell = RefineDialog.Grid[p.From];
                    break;
                case MirGridType.HeroInventory:
                    fromCell = p.From < User.HeroBeltIdx ? HeroBeltDialog.Grid[p.From] : HeroInventoryDialog.Grid[p.From - User.HeroBeltIdx];
                    break;
                default:
                    return;
            }

            switch (p.Grid)
            {
                case MirGridType.Inventory:
                    toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];
                    break;
                case MirGridType.Storage:
                    toCell = StorageDialog.Grid[p.To];
                    break;
                case MirGridType.Trade:
                    toCell = TradeDialog.Grid[p.To];
                    break;
                case MirGridType.Refine:
                    toCell = RefineDialog.Grid[p.To];
                    break;
                case MirGridType.HeroInventory:
                    toCell = p.To < User.HeroBeltIdx ? HeroBeltDialog.Grid[p.To] : HeroInventoryDialog.Grid[p.To - User.HeroBeltIdx];
                    break;
                default:
                    return;
            }

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (p.Grid == MirGridType.Trade)
                TradeDialog.ChangeLockState(false);

            if (!p.Success) return;

            UserItem i = fromCell.Item;
            fromCell.Item = toCell.Item;
            toCell.Item = i;

            User.RefreshStats();
            CharacterDuraPanel.GetCharacterDura();
        }
        private void EquipItem(S.EquipItem p)
        {
            MirItemCell fromCell, toCell;

            switch (p.Grid)
            {
                case MirGridType.HeroInventory:
                    toCell = HeroDialog.Grid[p.To];
                    break;
                default:
                    toCell = CharacterDialog.Grid[p.To];
                    break;
            }

            switch (p.Grid)
            {
                case MirGridType.Inventory:
                    fromCell = InventoryDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);
                    break;
                case MirGridType.Storage:
                    fromCell = StorageDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);
                    break;
                case MirGridType.HeroInventory:
                    fromCell = HeroInventoryDialog.GetCell(p.UniqueID) ?? HeroBeltDialog.GetCell(p.UniqueID);
                    break;
                default:
                    return;
            }

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success) return;

            UserItem i = fromCell.Item;
            fromCell.Item = toCell.Item;
            toCell.Item = i;
            CharacterDuraPanel.UpdateCharacterDura(i);
            if (p.Grid == MirGridType.HeroInventory)
                Hero.RefreshStats();
            else
                User.RefreshStats();
        }
        private void EquipSlotItem(S.EquipSlotItem p)
        {
            MirItemCell fromCell;
            MirItemCell toCell;

            switch (p.GridTo)
            {
                case MirGridType.Socket:
                    toCell = SocketDialog.Grid[p.To];
                    break;
                case MirGridType.Mount:
                    toCell = MountDialog.Grid[p.To];
                    break;
                case MirGridType.Fishing:
                    toCell = FishingDialog.Grid[p.To];
                    break;
                default:
                    return;
            }

            switch (p.Grid)
            {
                case MirGridType.Inventory:
                    fromCell = InventoryDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);
                    break;
                case MirGridType.Storage:
                    fromCell = StorageDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);
                    break;
                default:
                    return;
            }

            //if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success) return;

            UserItem i = fromCell.Item;
            fromCell.Item = null;
            toCell.Item = i;
            User.RefreshStats();
        }

        private void CombineItem(S.CombineItem p)
        {
            MirItemCell fromCell = null;
            MirItemCell toCell = null;
            switch (p.Grid)
            {
                case MirGridType.Inventory:
                    fromCell = InventoryDialog.GetCell(p.IDFrom) ?? BeltDialog.GetCell(p.IDFrom);
                    toCell = InventoryDialog.GetCell(p.IDTo) ?? BeltDialog.GetCell(p.IDTo);
                    break;
                case MirGridType.HeroInventory:
                    fromCell = HeroInventoryDialog.GetCell(p.IDFrom) ?? HeroBeltDialog.GetCell(p.IDFrom);
                    toCell = HeroInventoryDialog.GetCell(p.IDTo) ?? HeroBeltDialog.GetCell(p.IDTo);
                    break;
            }            

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (p.Destroy) toCell.Item = null;

            if (!p.Success) return;

            fromCell.Item = null;

            switch (p.Grid)
            {
                case MirGridType.Inventory:
                    User.RefreshStats();
                    break;
                case MirGridType.HeroInventory:
                    Hero.RefreshStats();
                    break;
            }
        }

        private void MergeItem(S.MergeItem p)
        {
            MirItemCell toCell, fromCell;

            switch (p.GridFrom)
            {
                case MirGridType.Inventory:
                    fromCell = InventoryDialog.GetCell(p.IDFrom) ?? BeltDialog.GetCell(p.IDFrom);
                    break;
                case MirGridType.Storage:
                    fromCell = StorageDialog.GetCell(p.IDFrom);
                    break;
                case MirGridType.Equipment:
                    fromCell = CharacterDialog.GetCell(p.IDFrom);
                    break;
                case MirGridType.Trade:
                    fromCell = TradeDialog.GetCell(p.IDFrom);
                    break;
                case MirGridType.Fishing:
                    fromCell = FishingDialog.GetCell(p.IDFrom);
                    break;
                case MirGridType.HeroEquipment:
                    fromCell = HeroDialog.GetCell(p.IDFrom);
                    break;
                case MirGridType.HeroInventory:
                    fromCell = HeroInventoryDialog.GetCell(p.IDFrom) ?? HeroBeltDialog.GetCell(p.IDFrom);
                    break;
                default:
                    return;
            }

            switch (p.GridTo)
            {
                case MirGridType.Inventory:
                    toCell = InventoryDialog.GetCell(p.IDTo) ?? BeltDialog.GetCell(p.IDTo);
                    break;
                case MirGridType.Storage:
                    toCell = StorageDialog.GetCell(p.IDTo);
                    break;
                case MirGridType.Equipment:
                    toCell = CharacterDialog.GetCell(p.IDTo);
                    break;
                case MirGridType.Trade:
                    toCell = TradeDialog.GetCell(p.IDTo);
                    break;
                case MirGridType.Fishing:
                    toCell = FishingDialog.GetCell(p.IDTo);
                    break;
                case MirGridType.HeroEquipment:
                    toCell = HeroDialog.GetCell(p.IDTo);
                    break;
                case MirGridType.HeroInventory:
                    toCell = HeroInventoryDialog.GetCell(p.IDTo) ?? HeroBeltDialog.GetCell(p.IDTo);
                    break;
                default:
                    return;
            }

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (p.GridFrom == MirGridType.Trade || p.GridTo == MirGridType.Trade)
                TradeDialog.ChangeLockState(false);

            if (!p.Success) return;
            if (fromCell.Item.Count <= toCell.Item.Info.StackSize - toCell.Item.Count)
            {
                toCell.Item.Count += fromCell.Item.Count;
                fromCell.Item = null;
            }
            else
            {
                fromCell.Item.Count -= (ushort)(toCell.Item.Info.StackSize - toCell.Item.Count);
                toCell.Item.Count = toCell.Item.Info.StackSize;
            }

            User.RefreshStats();
        }
        private void RemoveItem(S.RemoveItem p)
        {
            MirItemCell toCell;

            int index = -1;
            MirItemCell fromCell = null;
            for (int i = 0; i < MapObject.User.Equipment.Length; i++)
            {
                if (MapObject.User.Equipment[i] == null || MapObject.User.Equipment[i].UniqueID != p.UniqueID) continue;
                index = i;
                fromCell = CharacterDialog.Grid[index];
                break;
            }

            if (index == -1 && Hero != null)
            {
                for (int i = 0; i < MapObject.Hero.Equipment.Length; i++)
                {
                    if (MapObject.Hero.Equipment[i] == null || MapObject.Hero.Equipment[i].UniqueID != p.UniqueID) continue;
                    index = i;
                    fromCell = HeroDialog.Grid[index];
                    break;
                }
            }          

            switch (p.Grid)
            {
                case MirGridType.Inventory:
                    toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];
                    break;
                case MirGridType.Storage:
                    toCell = StorageDialog.Grid[p.To];
                    break;
                case MirGridType.HeroInventory:
                    toCell = p.To < User.HeroBeltIdx ? HeroBeltDialog.Grid[p.To] : HeroInventoryDialog.Grid[p.To - User.HeroBeltIdx];
                    break;
                default:
                    return;
            }

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success) return;
            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            CharacterDuraPanel.GetCharacterDura();
            if (p.Grid == MirGridType.HeroInventory)
                Hero.RefreshStats();
            else
                User.RefreshStats();

        }
        private void RemoveSlotItem(S.RemoveSlotItem p)
        {
            MirItemCell fromCell;
            MirItemCell toCell;

            switch (p.Grid)
            {
                case MirGridType.Socket:
                    fromCell = SocketDialog.GetCell(p.UniqueID);
                    break;
                case MirGridType.Mount:
                    fromCell = MountDialog.GetCell(p.UniqueID);
                    break;
                case MirGridType.Fishing:
                    fromCell = FishingDialog.GetCell(p.UniqueID);
                    break;
                default:
                    return;
            }

            switch (p.GridTo)
            {
                case MirGridType.Inventory:
                    toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];
                    break;
                case MirGridType.Storage:
                    toCell = StorageDialog.Grid[p.To];
                    break;
                default:
                    return;
            }

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success) return;
            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            CharacterDuraPanel.GetCharacterDura();
            User.RefreshStats();
        }
        private void TakeBackItem(S.TakeBackItem p)
        {
            MirItemCell fromCell = StorageDialog.Grid[p.From];

            MirItemCell toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success) return;
            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            User.RefreshStats();
            CharacterDuraPanel.GetCharacterDura();
        }
        private void StoreItem(S.StoreItem p)
        {
            MirItemCell fromCell = p.From < User.BeltIdx ? BeltDialog.Grid[p.From] : InventoryDialog.Grid[p.From - User.BeltIdx];

            MirItemCell toCell = StorageDialog.Grid[p.To];

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success) return;
            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            User.RefreshStats();
        }
        private void DepositRefineItem(S.DepositRefineItem p)
        {
            MirItemCell fromCell = p.From < User.BeltIdx ? BeltDialog.Grid[p.From] : InventoryDialog.Grid[p.From - User.BeltIdx];

            MirItemCell toCell = RefineDialog.Grid[p.To];

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success) return;
            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            User.RefreshStats();
        }

        private void RetrieveRefineItem(S.RetrieveRefineItem p)
        {
            MirItemCell fromCell = RefineDialog.Grid[p.From];
            MirItemCell toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success) return;
            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            User.RefreshStats();
        }

        private void RefineCancel(S.RefineCancel p)
        {
            RefineDialog.RefineReset();  
        }

        private void RefineItem(S.RefineItem p)
        {
            RefineDialog.RefineReset();
            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.UniqueID)
                {
                    User.Inventory[i] = null;
                    break;
                }
            }
            NPCDialog.Hide();
        }
        private void DepositTradeItem(S.DepositTradeItem p)
        {
            MirItemCell fromCell = p.From < User.BeltIdx ? BeltDialog.Grid[p.From] : InventoryDialog.Grid[p.From - User.BeltIdx];

            MirItemCell toCell = TradeDialog.Grid[p.To];

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;
            TradeDialog.ChangeLockState(false);

            if (!p.Success) return;
            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            User.RefreshStats();
        }
        private void RetrieveTradeItem(S.RetrieveTradeItem p)
        {
            MirItemCell fromCell = TradeDialog.Grid[p.From];
            MirItemCell toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;
            TradeDialog.ChangeLockState(false);

            if (!p.Success) return;
            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            User.RefreshStats();
        }
        private void SplitItem(S.SplitItem p)
        {
            Bind(p.Item);

            UserItem[] array;
            switch (p.Grid)
            {
                case MirGridType.Inventory:
                    array = MapObject.User.Inventory;
                    break;
                case MirGridType.Storage:
                    array = Storage;
                    break;
                default:
                    return;
            }

            if (p.Grid == MirGridType.Inventory && (p.Item.Info.Type == ItemType.药水 || p.Item.Info.Type == ItemType.卷轴 || p.Item.Info.Type == ItemType.护身符 || (p.Item.Info.Type == ItemType.特殊消耗品 && p.Item.Info.Effect == 1)))
            {
                if (p.Item.Info.Type == ItemType.药水 || p.Item.Info.Type == ItemType.卷轴 || (p.Item.Info.Type == ItemType.特殊消耗品 && p.Item.Info.Effect == 1))
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (array[i] != null) continue;
                        array[i] = p.Item;
                        User.RefreshStats();
                        return;
                    }
                }
                else if (p.Item.Info.Type == ItemType.护身符)
                {
                    for (int i = 4; i < GameScene.User.BeltIdx; i++)
                    {
                        if (array[i] != null) continue;
                        array[i] = p.Item;
                        User.RefreshStats();
                        return;
                    }
                }
            }

            for (int i = GameScene.User.BeltIdx; i < array.Length; i++)
            {
                if (array[i] != null) continue;
                array[i] = p.Item;
                User.RefreshStats();
                return;
            }

            for (int i = 0; i < GameScene.User.BeltIdx; i++)
            {
                if (array[i] != null) continue;
                array[i] = p.Item;
                User.RefreshStats();
                return;
            }
        }

        private void SplitItem1(S.SplitItem1 p)
        {
            MirItemCell cell;

            switch (p.Grid)
            {
                case MirGridType.Inventory:
                    cell = InventoryDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);
                    break;
                case MirGridType.Storage:
                    cell = StorageDialog.GetCell(p.UniqueID);
                    break;
                default:
                    return;
            }

            if (cell == null) return;

            cell.Locked = false;

            if (!p.Success) return;
            cell.Item.Count -= p.Count;
            User.RefreshStats();
        }
        private void UseItem(S.UseItem p)
        {
            MirItemCell cell = null;
            bool hero = false;
            
            switch (p.Grid)
            {
                case MirGridType.Inventory:
                    cell = InventoryDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);
                    break;
                case MirGridType.HeroInventory:
                    cell = HeroInventoryDialog.GetCell(p.UniqueID) ?? HeroBeltDialog.GetCell(p.UniqueID);
                    hero = true;
                    break;
            }            

            if (cell == null) return;

            cell.Locked = false;

            if (!p.Success) return;
            if (cell.Item.Count > 1) cell.Item.Count--;
            else cell.Item = null;
            if (hero)
                Hero.RefreshStats();
            else
                User.RefreshStats();
        }
        private void DropItem(S.DropItem p)
        {
            MirItemCell cell;
            if (p.HeroItem)
            {
                cell = HeroInventoryDialog.GetCell(p.UniqueID) ?? HeroBeltDialog.GetCell(p.UniqueID);
            }
            else
            {
                cell = InventoryDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);
            }
            

            if (cell == null) return;

            cell.Locked = false;

            if (!p.Success) return;

            if (p.Count == cell.Item.Count)
                cell.Item = null;
            else
                cell.Item.Count -= p.Count;

            if (p.HeroItem)
            {
                Hero.RefreshStats();
            }
            else
            {
                User.RefreshStats();
            }
            
        }

        private void TakeBackHeroItem(S.TakeBackHeroItem p)
        {
            MirItemCell fromCell = p.From < User.HeroBeltIdx ? HeroBeltDialog.Grid[p.From] : HeroInventoryDialog.Grid[p.From - User.HeroBeltIdx];

            MirItemCell toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success) return;
            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            User.RefreshStats();
            Hero.RefreshStats();
            CharacterDuraPanel.GetCharacterDura();
        }

        private void TransferHeroItem(S.TransferHeroItem p)
        {
            MirItemCell fromCell = p.From < User.BeltIdx ? BeltDialog.Grid[p.From] : InventoryDialog.Grid[p.From - User.BeltIdx];

            MirItemCell toCell = p.To < User.HeroBeltIdx ? HeroBeltDialog.Grid[p.To] : HeroInventoryDialog.Grid[p.To - User.HeroBeltIdx];

            if (toCell == null || fromCell == null) return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success) return;
            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            User.RefreshStats();
            Hero.RefreshStats();
            CharacterDuraPanel.GetCharacterDura();
        }

        private void MountUpdate(S.MountUpdate p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                if (MapControl.Objects[i].ObjectID != p.ObjectID) continue;

                PlayerObject player = MapControl.Objects[i] as PlayerObject;
                if (player != null)
                {
                    player.MountUpdate(p);
                }
                break;
            }

            if (p.ObjectID != User.ObjectID) return;

            CanRun = false;

            User.RefreshStats();

            GameScene.Scene.MountDialog.RefreshDialog();
            GameScene.Scene.Redraw();
        }

        private void TransformUpdate(S.TransformUpdate p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                if (MapControl.Objects[i].ObjectID != p.ObjectID) continue;

                if (MapControl.Objects[i] is PlayerObject player)
                {
                    player.TransformType = p.TransformType;
                }
                break;
            }
        }

        private void FishingUpdate(S.FishingUpdate p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                if (MapControl.Objects[i].ObjectID != p.ObjectID) continue;

                PlayerObject player = MapControl.Objects[i] as PlayerObject;
                if (player != null)
                {
                    player.FishingUpdate(p);
                    
                }
                break;
            }

            if (p.ObjectID != User.ObjectID) return;

            GameScene.Scene.FishingStatusDialog.ProgressPercent = p.ProgressPercent;
            GameScene.Scene.FishingStatusDialog.ChancePercent = p.ChancePercent;

            GameScene.Scene.FishingStatusDialog.ChanceLabel.Text = string.Format("{0}%", GameScene.Scene.FishingStatusDialog.ChancePercent);

            if (p.Fishing)
                GameScene.Scene.FishingStatusDialog.Show();
            else
                GameScene.Scene.FishingStatusDialog.Hide();

            Redraw();
        }

        private void CompleteQuest(S.CompleteQuest p)
        {
            User.CompletedQuests = p.CompletedQuests;
        }

        private void ShareQuest(S.ShareQuest p)
        {
            ClientQuestInfo quest = GameScene.QuestInfoList.FirstOrDefault(e => e.Index == p.QuestIndex);
            
            if (quest == null) return;

            MirMessageBox messageBox = new MirMessageBox(string.Format("{0} 与你共享任务是否接受", p.SharerName), MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.AcceptQuest { NPCIndex = 0, QuestIndex = quest.Index });

            messageBox.Show();
        }

        private void ChangeQuest(S.ChangeQuest p)
        {
            switch(p.QuestState)
            {
                case QuestState.Add:
                    User.CurrentQuests.Add(p.Quest);

                    foreach (ClientQuestProgress quest in User.CurrentQuests)
                        BindQuest(quest);
                    if (Settings.TrackedQuests.Contains(p.Quest.Id))
                    {
                        GameScene.Scene.QuestTrackingDialog.AddQuest(p.Quest, true);
                    }

                    if (p.TrackQuest)
                    {
                        GameScene.Scene.QuestTrackingDialog.AddQuest(p.Quest);
                    }

                    break;
                case QuestState.Update:
                    for (int i = 0; i < User.CurrentQuests.Count; i++)
                    {
                        if (User.CurrentQuests[i].Id != p.Quest.Id) continue;

                        User.CurrentQuests[i] = p.Quest;
                    }

                    foreach (ClientQuestProgress quest in User.CurrentQuests)
                        BindQuest(quest);

                    break;
                case QuestState.Remove:

                    for (int i = User.CurrentQuests.Count - 1; i >= 0; i--)
                    {
                        if (User.CurrentQuests[i].Id != p.Quest.Id) continue;

                        User.CurrentQuests.RemoveAt(i);
                    }

                    GameScene.Scene.QuestTrackingDialog.RemoveQuest(p.Quest);

                    break;
            }

            GameScene.Scene.QuestTrackingDialog.DisplayQuests();

            if (Scene.QuestListDialog.Visible)
            {
                Scene.QuestListDialog.DisplayInfo();
            }

            if (Scene.QuestLogDialog.Visible)
            {
                Scene.QuestLogDialog.DisplayQuests();
            }
        }

        private void PlayerUpdate(S.PlayerUpdate p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                if (MapControl.Objects[i].ObjectID != p.ObjectID) continue;

                PlayerObject player = MapControl.Objects[i] as PlayerObject;
                if (player != null) player.Update(p);
                return;
            }
        }
        private void PlayerInspect(S.PlayerInspect p)
        {
            InspectDialog.Items = p.Equipment;

            InspectDialog.Name = p.Name;
            InspectDialog.GuildName = p.GuildName;
            InspectDialog.GuildRank = p.GuildRank;
            InspectDialog.Class = p.Class;
            InspectDialog.Gender = p.Gender;
            InspectDialog.Hair = p.Hair;
            InspectDialog.Level = p.Level;
            InspectDialog.LoverName = p.LoverName;
            InspectDialog.AllowObserve = p.AllowObserve;

            InspectDialog.RefreshInferface(p.IsHero);
            InspectDialog.Show();
        }
        private void LogOutSuccess(S.LogOutSuccess p)
        {
            for (int i = 0; i <= 3; i++)//Fix for orbs sound
                SoundManager.StopSound(20000 + 126 * 10 + 5 + i);

            User = null;

            // 退回选人界面时缩回小窗口，避免在选人界面占用大分辨率
            Size loginSize = ResolutionHelper.LoginSize;
            if (Settings.ScreenWidth != loginSize.Width || Settings.ScreenHeight != loginSize.Height)
                CMain.SetResolution(loginSize.Width, loginSize.Height);

            ActiveScene = new SelectScene(p.Characters);

            Dispose();
        }
        private void LogOutFailed(S.LogOutFailed p)
        {
            Enabled = true;
        }

        private void ReturnToLogin(S.ReturnToLogin p)
        {
            User = null;

            // 登录界面固定用默认档窗口，这里按实际尺寸比较，避免分辨率值语义变化后漏判
            Size loginSize = ResolutionHelper.GetSize(ResolutionHelper.DefaultResolution);
            if (Settings.ScreenWidth != loginSize.Width || Settings.ScreenHeight != loginSize.Height)
                CMain.SetResolution(loginSize.Width, loginSize.Height);

            ActiveScene = new LoginScene();
            Dispose();
            MirMessageBox.Show("注销观察模式");
        }

        private void TimeOfDay(S.TimeOfDay p)
        {
            Lights = p.Lights;
            switch (Lights)
            {
                case LightSetting.Day:
                case LightSetting.Normal:
                    MiniMapDialog.LightSetting.Index = 2093;
                    break;
                case LightSetting.Dawn:
                    MiniMapDialog.LightSetting.Index = 2095;
                    break;
                case LightSetting.Evening:
                    MiniMapDialog.LightSetting.Index = 2094;
                    break;
                case LightSetting.Night:
                    MiniMapDialog.LightSetting.Index = 2092;
                    break;
            }
        }
        private void ChangeAMode(S.ChangeAMode p)
        {
            AMode = p.Mode;

            switch (p.Mode)
            {
                case AttackMode.Peace:
                    ChatDialog.ReceiveChat(GameLanguage.AttackMode_Peace, ChatType.Hint);
                    break;
                case AttackMode.Group:
                    ChatDialog.ReceiveChat(GameLanguage.AttackMode_Group, ChatType.Hint);
                    break;
                case AttackMode.Guild:
                    ChatDialog.ReceiveChat(GameLanguage.AttackMode_Guild, ChatType.Hint);
                    break;
                case AttackMode.EnemyGuild:
                    ChatDialog.ReceiveChat(GameLanguage.AttackMode_EnemyGuild, ChatType.Hint);
                    break;
                case AttackMode.RedBrown:
                    ChatDialog.ReceiveChat(GameLanguage.AttackMode_RedBrown, ChatType.Hint);
                    break;
                case AttackMode.All:
                    ChatDialog.ReceiveChat(GameLanguage.AttackMode_All, ChatType.Hint);
                    break;
            }
        }
        private void ChangePMode(S.ChangePMode p)
        {
            PMode = p.Mode;
            switch (p.Mode)
            {
                case PetMode.Both:
                    ChatDialog.ReceiveChat(GameLanguage.PetMode_Both, ChatType.Hint);
                    break;
                case PetMode.MoveOnly:
                    ChatDialog.ReceiveChat(GameLanguage.PetMode_MoveOnly, ChatType.Hint);
                    break;
                case PetMode.AttackOnly:
                    ChatDialog.ReceiveChat(GameLanguage.PetMode_AttackOnly, ChatType.Hint);
                    break;
                case PetMode.None:
                    ChatDialog.ReceiveChat(GameLanguage.PetMode_None, ChatType.Hint);
                    break;
                case PetMode.FocusMasterTarget:
                    ChatDialog.ReceiveChat(GameLanguage.PetMode_FocusMasterTarget, ChatType.Hint);
                    break;
            }
        }

        private void ObjectItem(S.ObjectItem p)
        {
            ItemObject ob = new ItemObject(p.ObjectID);
            ob.Load(p);
            /*
            string[] Warnings = new string[] {"HeroNecklace","AdamantineNecklace","8TrigramWheel","HangMaWheel","BaekTaGlove","SpiritReformer","BokMaWheel","BoundlessRing","ThunderRing","TaeGukRing","OmaSpiritRing","NobleRing"};
            if (Warnings.Contains(p.Name))
            {
                ChatDialog.ReceiveChat(string.Format("{0} at {1}", p.Name, p.Location), ChatType.Hint);
            }
            */
        }
        private void ObjectGold(S.ObjectGold p)
        {
            ItemObject ob = new ItemObject(p.ObjectID);
            ob.Load(p);
        }
        private void GainedItem(S.GainedItem p)
        {
            Bind(p.Item);
            AddItem(p.Item);
            User.RefreshStats();

            if (p.Item.Info.Type != ItemType.特殊消耗品)
            {
                OutputMessage(string.Format(GameLanguage.YouGained, p.Item.FriendlyName));
            }
        }
        private void GainedQuestItem(S.GainedQuestItem p)
        {
            Bind(p.Item);
            AddQuestItem(p.Item);
        }

        private void GainedGold(S.GainedGold p)
        {
            if (p.Gold == 0) return;

            Gold += p.Gold;
            SoundManager.PlaySound(SoundList.Gold);
            OutputMessage(string.Format(GameLanguage.YouGained2, p.Gold, GameLanguage.Gold));
        }
        private void LoseGold(S.LoseGold p)
        {
            Gold -= p.Gold;
            SoundManager.PlaySound(SoundList.Gold);
        }
        private void GainedCredit(S.GainedCredit p)
        {
            if (p.Credit == 0) return;

            Credit += p.Credit;
            SoundManager.PlaySound(SoundList.Gold);
            OutputMessage(string.Format(GameLanguage.YouGained2, p.Credit, GameLanguage.Credit));
        }
        private void LoseCredit(S.LoseCredit p)
        {
            Credit -= p.Credit;
            SoundManager.PlaySound(SoundList.Gold);
        }
        private void ObjectMonster(S.ObjectMonster p)
        {
            var found = false;
            var mob = (MonsterObject)MapControl.Objects.Find(ob => ob.ObjectID == p.ObjectID);
            if (mob != null)
                found = true;
            if (!found)
                mob = new MonsterObject(p.ObjectID);
            mob.Load(p, found);
        }
        private void ObjectAttack(S.ObjectAttack p)
        {
            if (p.ObjectID == User.ObjectID && !Observing) return;

            QueuedAction action = null;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                if (ob.Race == ObjectType.Player)
                {
                    action = new QueuedAction { Action = MirAction.近距攻击1, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                }
                else
                {
                    switch (p.Type)
                    {
                        default:
                            {
                                action = new QueuedAction { Action = MirAction.近距攻击1, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                        case 1:
                            {
                                action = new QueuedAction { Action = MirAction.近距攻击2, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                        case 2:
                            {
                                action = new QueuedAction { Action = MirAction.近距攻击3, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                        case 3:
                            {
                                action = new QueuedAction { Action = MirAction.近距攻击4, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                        case 4:
                            {
                                action = new QueuedAction { Action = MirAction.近距攻击5, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                    }
                }
                action.Params.Add(p.Spell);
                action.Params.Add(p.Level);
                ob.ActionFeed.Add(action);
                return;
            }
        }
        //内挂：最近一次打到自己的对象（道士隐身后判断「怪物贴身近攻」的依据，由 MapControl 读取）
        public static uint LastStruckAttackerID;
        public static long LastStruckAttackerTime;

        private void Struck(S.Struck p)
        {
            LogTime = CMain.Time + Globals.LogDelay;

            // 记录最近一次打我的是谁（内挂：道士隐身后判断「怪物贴身近攻」的依据）
            LastStruckAttackerID = p.AttackerID;
            LastStruckAttackerTime = CMain.Time;

            // 泰山：被攻击后不后仰（不播放被击动作），也不打断跑动、施法与复活吟唱。
            // 只影响自身表现，服务器仍照常结算伤害。
            if (Settings.MountTai) return;

            NextRunTime = CMain.Time + 2500;
            User.BlizzardStopTime = 0;
            User.ClearMagic();
            if (User.ReincarnationStopTime > CMain.Time)
                Network.Enqueue(new C.CancelReincarnation {});

            MirDirection dir = User.Direction;
            Point location = User.CurrentLocation;

            for (int i = 0; i < User.ActionFeed.Count; i++)
                if (User.ActionFeed[i].Action == MirAction.被击动作) return;


            if (User.ActionFeed.Count > 0)
            {
                dir = User.ActionFeed[User.ActionFeed.Count - 1].Direction;
                location = User.ActionFeed[User.ActionFeed.Count - 1].Location;
            }

            if (User.Buffs.Any(a => a == BuffType.先天气功))
            {
                for (int j = 0; j < User.Effects.Count; j++)
                {
                    BuffEffect effect = null;
                    effect = User.Effects[j] as BuffEffect;

                    if (effect != null && effect.BuffType == BuffType.先天气功)
                    {
                        effect.Clear();
                        effect.Remove();

                        User.Effects.Add(effect = new BuffEffect(Libraries.Magic2, 1890, 6, 600, User, true, BuffType.先天气功) { Repeat = false });
                        SoundManager.PlaySound(20000 + (ushort)Spell.EnergyShield * 10 + 1);
                        
                        effect.Complete += (o, e) =>
                        {
                            User.Effects.Add(new BuffEffect(Libraries.Magic2, 1900, 2, 800, User, true, BuffType.先天气功) { Repeat = true });
                        };


                        break;
                    }
                }
            }

            QueuedAction action = new QueuedAction { Action = MirAction.被击动作, Direction = dir, Location = location, Params = new List<object>() };
            action.Params.Add(p.AttackerID);
            User.ActionFeed.Add(action);

        }
        private void ObjectStruck(S.ObjectStruck p)
        {
            if (p.ObjectID == User.ObjectID) return;

                for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
                {
                    MapObject ob = MapControl.Objects[i];
                    if (ob.ObjectID != p.ObjectID) continue;

                    ob.LastStruckTime = CMain.Time;   // 记录受击时间（内挂打不中检测的判定信号）

                    // 只有自己（或自己的英雄）造成的伤害才算「自己的攻击有效」
                    // （服务端只在真正打出伤害时才广播受击，别人打中不算）
                    if (p.AttackerID == User.ObjectID ||
                        (Hero != null && p.AttackerID == Hero.ObjectID) ||
                        (HeroObject != null && p.AttackerID == HeroObject.ObjectID))
                        ob.LastStruckByMeTime = CMain.Time;

                    if (ob.SkipFrames) return;
                if (ob.ActionFeed.Count > 0 && ob.ActionFeed[ob.ActionFeed.Count - 1].Action == MirAction.被击动作) return;

                if (ob.Race == ObjectType.Player)
                    ((PlayerObject)ob).BlizzardStopTime = 0;
                QueuedAction action = new QueuedAction { Action = MirAction.被击动作, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                action.Params.Add(p.AttackerID);
                ob.ActionFeed.Add(action);

                if (ob.Buffs.Any(a => a == BuffType.先天气功))
                {
                    for (int j = 0; j < ob.Effects.Count; j++)
                    {
                        BuffEffect effect = null;
                        effect = ob.Effects[j] as BuffEffect;

                        if (effect != null && effect.BuffType == BuffType.先天气功)
                        {
                            effect.Clear();
                            effect.Remove();

                            ob.Effects.Add(effect = new BuffEffect(Libraries.Magic2, 1890, 6, 600, ob, true, BuffType.先天气功) { Repeat = false });
                            SoundManager.PlaySound(20000 + (ushort)Spell.EnergyShield * 10 + 1);

                            effect.Complete += (o, e) =>
                            {
                                ob.Effects.Add(new BuffEffect(Libraries.Magic2, 1900, 2, 800, ob, true, BuffType.先天气功) { Repeat = true });
                            };

                            break;
                        }
                    }
                }

                return;
            }
        }

        private void DamageIndicator(S.DamageIndicator p)
        {
            if (Settings.DisplayDamage)
            {
                for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
                {
                    MapObject obj = MapControl.Objects[i];
                    if (obj.ObjectID != p.ObjectID) continue;

                    if (obj.Damages.Count >= 10) return;

                    switch (p.Type)
                    {
                        case DamageType.Hit: //add damage level colours
                            obj.Damages.Add(new Damage(p.Damage.ToString("#,##0"), 1000, obj.Race == ObjectType.Player ? Color.Red : Color.White, 50));
                            break;
                        case DamageType.Miss:
                            obj.Damages.Add(new Damage("未击中", 1200, obj.Race == ObjectType.Player ? Color.LightCoral : Color.LightGray, 50));
                            break;
                        case DamageType.Critical:
                            obj.Damages.Add(new Damage("暴击", 1000, obj.Race == ObjectType.Player ? Color.DarkRed : Color.DarkRed, 50) { Offset = 15 });
                            break;
                        case DamageType.HpRegen:
                            obj.Damages.Add(new Damage(p.Damage.ToString("#,##0"), 1000, obj.Race == ObjectType.Player ? Color.OrangeRed : Color.OrangeRed, 50));
                            break;
                        case DamageType.Poisoning:
                            obj.Damages.Add(new Damage(p.Damage.ToString("#,##0"), 1000, obj.Race == ObjectType.Player ? Color.Green : Color.Green, 50));
                            break;
                    }
                }
            }
        }

        private void DuraChanged(S.DuraChanged p)
        {
            UserItem item = null;
            for (int i = 0; i < User.Inventory.Length; i++)
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.UniqueID)
                {
                    item = User.Inventory[i];
                    break;
                }


            if (item == null)
                for (int i = 0; i < User.Equipment.Length; i++)
                {
                    if (User.Equipment[i] != null && User.Equipment[i].UniqueID == p.UniqueID)
                    {
                        item = User.Equipment[i];
                        break;
                    }
                    if (User.Equipment[i] != null && User.Equipment[i].Slots != null)
                    {
                        for (int j = 0; j < User.Equipment[i].Slots.Length; j++)
                        {
                            if (User.Equipment[i].Slots[j] != null && User.Equipment[i].Slots[j].UniqueID == p.UniqueID)
                            {
                                item = User.Equipment[i].Slots[j];
                                break;
                            }
                        }

                        if (item != null) break;
                    }
                }

            if (item == null) return;

            item.CurrentDura = p.CurrentDura;

            if (item.CurrentDura == 0)
            {
                User.RefreshStats();
                switch (item.Info.Type)
                {
                    case ItemType.坐骑:
                        ChatDialog.ReceiveChat(string.Format("{0} 坐骑忠诚度不足", item.Info.FriendlyName), ChatType.System);
                        break;
                    default:
                        ChatDialog.ReceiveChat(string.Format("{0} 持久度降为 0", item.Info.FriendlyName), ChatType.System);
                        break;
                }
                
            }

            if (HoverItem == item)
            {
                DisposeItemLabel();
                CreateItemLabel(item);
            }

            CharacterDuraPanel.UpdateCharacterDura(item);
        }
        private void HealthChanged(S.HealthChanged p)
        {
            User.HP = p.HP;
            User.MP = p.MP;

            User.PercentHealth = (byte)(User.HP / (float)User.Stats[Stat.HP] * 100);
        }
        private void HeroHealthChanged(S.HeroHealthChanged p)
        {
            Hero.HP = p.HP;
            Hero.MP = p.MP;

            Hero.PercentHealth = (byte)(Hero.HP / (float)Hero.Stats[Stat.HP] * 100);
            Hero.PercentMana = (byte)(Hero.MP / (float)Hero.Stats[Stat.MP] * 100);
        }

        private void DeleteQuestItem(S.DeleteQuestItem p)
        {
            for (int i = 0; i < User.QuestInventory.Length; i++)
            {
                UserItem item = User.QuestInventory[i];

                if (item == null || item.UniqueID != p.UniqueID) continue;

                if (item.Count == p.Count)
                    User.QuestInventory[i] = null;
                else
                    item.Count -= p.Count;
                break;
            } 
        }

        private void DeleteItem(S.DeleteItem p)
        {
            UserObject actor = null;
            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (actor != null) break;
                UserItem item = User.Inventory[i];

                if (item != null && item.Slots.Length > 0)
                {
                    for (int j = 0; j < item.Slots.Length; j++)
                    {
                        UserItem slotItem = item.Slots[j];

                        if (slotItem == null || slotItem.UniqueID != p.UniqueID) continue;

                        if (slotItem.Count == p.Count)
                            item.Slots[j] = null;
                        else
                            slotItem.Count -= p.Count;
                        actor = User;
                        break;
                    }
                }

                if (item == null || item.UniqueID != p.UniqueID) continue;

                if (item.Count == p.Count)
                    User.Inventory[i] = null;
                else
                    item.Count -= p.Count;
                actor = User;
            }

            if (actor == null)
            {
                for (int i = 0; i < User.Equipment.Length; i++)
                {
                    if (actor != null) break;
                    UserItem item = User.Equipment[i];

                    if (item != null && item.Slots.Length > 0)
                    {
                        for (int j = 0; j < item.Slots.Length; j++)
                        {
                            UserItem slotItem = item.Slots[j];

                            if (slotItem == null || slotItem.UniqueID != p.UniqueID) continue;

                            if (slotItem.Count == p.Count)
                                item.Slots[j] = null;
                            else
                                slotItem.Count -= p.Count;
                            actor = User;
                            break;
                        }
                    }

                    if (item == null || item.UniqueID != p.UniqueID) continue;

                    if (item.Count == p.Count)
                        User.Equipment[i] = null;
                    else
                        item.Count -= p.Count;
                    actor = User;
                }
            }

            if (Hero != null && actor == null)
            {                
                for (int i = 0; i < Hero.Inventory.Length; i++)
                {
                    if (actor != null) break;
                    UserItem item = Hero.Inventory[i];

                    if (item != null && item.Slots.Length > 0)
                    {
                        for (int j = 0; j < item.Slots.Length; j++)
                        {
                            UserItem slotItem = item.Slots[j];

                            if (slotItem == null || slotItem.UniqueID != p.UniqueID) continue;

                            if (slotItem.Count == p.Count)
                                item.Slots[j] = null;
                            else
                                slotItem.Count -= p.Count;
                            actor = Hero;
                            break;
                        }
                    }

                    if (item == null || item.UniqueID != p.UniqueID) continue;

                    if (item.Count == p.Count)
                        User.Inventory[i] = null;
                    else
                        item.Count -= p.Count;
                    actor = Hero;
                }

                if (actor == null)
                {
                    for (int i = 0; i < Hero.Equipment.Length; i++)
                    {
                        if (actor != null) break;
                        UserItem item = Hero.Equipment[i];

                        if (item != null && item.Slots.Length > 0)
                        {
                            for (int j = 0; j < item.Slots.Length; j++)
                            {
                                UserItem slotItem = item.Slots[j];

                                if (slotItem == null || slotItem.UniqueID != p.UniqueID) continue;

                                if (slotItem.Count == p.Count)
                                    item.Slots[j] = null;
                                else
                                    slotItem.Count -= p.Count;
                                actor = Hero;
                                break;
                            }
                        }

                        if (item == null || item.UniqueID != p.UniqueID) continue;

                        if (item.Count == p.Count)
                            User.Equipment[i] = null;
                        else
                            item.Count -= p.Count;
                        actor = Hero;
                    }
                }
            }

            if (actor == null)
            {
                for (int i = 0; i < Storage.Length; i++)
                {
                    var item = Storage[i];
                    if (item == null || item.UniqueID != p.UniqueID) continue;

                    if (item.Count == p.Count)
                        Storage[i] = null;
                    else
                        item.Count -= p.Count;
                    break;
                }
            }
            actor?.RefreshStats();
        }
        private void Death(S.Death p)
        {
            User.Dead = true;

            User.ActionFeed.Add(new QueuedAction { Action = MirAction.死亡动作, Direction = p.Direction, Location = p.Location });
            ShowReviveMessage = true;

            LogTime = 0;
        }
        private void ObjectDied(S.ObjectDied p)
        {
            if (p.ObjectID == User.ObjectID) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;

                switch(p.Type)
                {
                    default:
                        ob.ActionFeed.Add(new QueuedAction { Action = MirAction.死亡动作, Direction = p.Direction, Location = p.Location });
                        ob.Dead = true;
                        break;
                    case 1:
                        MapControl.Effects.Add(new Effect(Libraries.Magic2, 690, 10, 1000, ob.CurrentLocation));
                        ob.Remove();
                        break;
                    case 2:
                        SoundManager.PlaySound(20000 + (ushort)Spell.DarkBody * 10 + 1);
                        MapControl.Effects.Add(new Effect(Libraries.Magic2, 2600, 10, 1200, ob.CurrentLocation));
                        ob.Remove();
                        break;
                }
                return;
            }
        }
        private void ColourChanged(S.ColourChanged p)
        {
            User.NameColour = p.NameColour;
        }
        private void ObjectColourChanged(S.ObjectColourChanged p)
        {
            if (p.ObjectID == User.ObjectID) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.NameColour = p.NameColour;
                return;
            }
        }

        private void ObjectGuildNameChanged(S.ObjectGuildNameChanged p)
        {
            if (p.ObjectID == User.ObjectID) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                PlayerObject obPlayer = (PlayerObject)ob;
                obPlayer.GuildName = p.GuildName;
                return;
            }
        }
        private void GainExperience(S.GainExperience p)
        {
            OutputMessage(string.Format(GameLanguage.ExperienceGained, p.Amount));
            MapObject.User.Experience += p.Amount;
        }

        private void GainHeroExperience(S.GainHeroExperience p)
        {
            OutputMessage(string.Format(GameLanguage.HeroExperienceGained, p.Amount));
            MapObject.Hero.Experience += p.Amount;
        }
        private void LevelChanged(S.LevelChanged p)
        {
            User.Level = p.Level;
            User.Experience = p.Experience;
            User.MaxExperience = p.MaxExperience;
            User.RefreshStats();
            OutputMessage(GameLanguage.LevelUp);
            User.Effects.Add(new Effect(Libraries.Magic2, 1200, 20, 2000, User));
            SoundManager.PlaySound(SoundList.LevelUp);
            ChatDialog.ReceiveChat(GameLanguage.LevelUp, ChatType.LevelUp); 
        }
        private void HeroLevelChanged(S.HeroLevelChanged p)
        {
            Hero.Level = p.Level;
            Hero.Experience = p.Experience;
            Hero.MaxExperience = p.MaxExperience;
            Hero.RefreshStats();
            OutputMessage(GameLanguage.HeroLevelUp);
            Hero.Effects.Add(new Effect(Libraries.Magic2, 1200, 20, 2000, User));
            SoundManager.PlaySound(SoundList.LevelUp);
            ChatDialog.ReceiveChat(GameLanguage.HeroLevelUp, ChatType.LevelUp);
            MainDialog.HeroInfoPanel.Update();
        }
        private void ObjectLeveled(S.ObjectLeveled p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.Effects.Add(new Effect(Libraries.Magic2, 1180, 16, 2500, ob));
                SoundManager.PlaySound(SoundList.LevelUp);
                return;
            }
        }
        private void ObjectHarvest(S.ObjectHarvest p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.挖矿展示, Direction = ob.Direction, Location = ob.CurrentLocation });
                return;
            }
        }
        private void ObjectHarvested(S.ObjectHarvested p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.挖后尸骸, Direction = ob.Direction, Location = ob.CurrentLocation });
                return;
            }
        }
        private void ObjectNPC(S.ObjectNPC p)
        {
            NPCObject ob = new NPCObject(p.ObjectID);
            ob.Load(p);
        }
        private void NPCResponse(S.NPCResponse p)
        {
            NPCTime = 0;
            NPCDialog.BigButtons.Clear();
            NPCDialog.BigButtonDialog.Hide();
            NPCDialog.NewText(p.Page);

            if (p.Page.Count > 0 || NPCDialog.BigButtons.Count > 0)
                NPCDialog.Show();
            else
                NPCDialog.Hide();

            NPCGoodsDialog.Hide();
            NPCSubGoodsDialog.Hide();
            NPCCraftGoodsDialog.Hide();
            NPCDropDialog.Hide();
            StorageDialog.Hide();
            NPCAwakeDialog.Hide();
            RefineDialog.Hide();
            StorageDialog.Hide();
            TrustMerchantDialog.Hide();
            QuestListDialog.Hide();
        }

        private void NPCUpdate(S.NPCUpdate p)
        {
            GameScene.NPCID = p.NPCID; //Updates the client with the correct NPC ID if it's manually called from the client
        }

        private void NPCImageUpdate(S.NPCImageUpdate p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID || ob.Race != ObjectType.Merchant) continue;

                NPCObject npc = (NPCObject)ob;
                npc.Image = p.Image;
                npc.Colour = p.Colour;

                npc.LoadLibrary();
                return;
            }
        }
        private void DefaultNPC(S.DefaultNPC p)
        {
            GameScene.DefaultNPCID = p.ObjectID; //Updates the client with the correct Default NPC ID
        }


        private void ObjectHide(S.ObjectHide p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.切换LIB, Direction = ob.Direction, Location = ob.CurrentLocation });
                return;
            }
        }
        private void ObjectShow(S.ObjectShow p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.石化苏醒, Direction = ob.Direction, Location = ob.CurrentLocation });
                return;
            }
        }
        private void Poisoned(S.Poisoned p)
        {
            var previousPoisons = User.Poison;

            User.Poison = p.Poison;
            if (p.Poison.HasFlag(PoisonType.Stun) || p.Poison.HasFlag(PoisonType.Dazed) || p.Poison.HasFlag(PoisonType.Frozen) || p.Poison.HasFlag(PoisonType.Paralysis) || p.Poison.HasFlag(PoisonType.LRParalysis))
            {
                User.ClearMagic();
            }

            if (previousPoisons.HasFlag(PoisonType.Blindness) && !User.Poison.HasFlag(PoisonType.Blindness))
            {
                User.BlindCount = 0;
            }
        }

        private void ObjectPoisoned(S.ObjectPoisoned p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.Poison = p.Poison;
                return;
            }
        }
        private void MapChanged(S.MapChanged p)
        {
            var isCurrentMap = (MapControl.Index == p.MapIndex);

            if (isCurrentMap)
                MapControl.ResetMap();
            else
            {
                MapControl.Index = p.MapIndex;
                MapControl.FileName = Path.Combine(Settings.MapPath, p.FileName + ".map");
                MapControl.Title = p.Title;
                MapControl.MiniMap = p.MiniMap;
                MapControl.BigMap = p.BigMap;
                MapControl.Lights = p.Lights;
                MapControl.MapDarkLight = p.MapDarkLight;
                MapControl.Music = p.Music;
                MapControl.Weather = p.Weather;
                MapControl.LoadMap();
            }

            MapControl.NextAction = 0;
            Scene.MapControl.AutoPath = false;
            User.CurrentLocation = p.Location;
            User.MapLocation = p.Location;
            MapControl.AddObject(User);

            User.Direction = p.Direction;

            User.QueuedAction = null;
            User.ActionFeed.Clear();
            User.ClearMagic();
            User.SetAction();

            GameScene.CanRun = false;

            MapControl.FloorValid = false;
            MapControl.InputDelay = CMain.Time + 400;

            MapControl.UpdateWeather();
        }
        private void ObjectTeleportOut(S.ObjectTeleportOut p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                Effect effect = null;

                bool playDefaultSound = true;

                switch (p.Type)
                {
                    case 1: //Yimoogi
                        {
                            effect = new Effect(Libraries.Magic2, 1300, 10, 500, ob.CurrentLocation);
                            break;
                        }
                    case 2: //RedFoxman
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.RedFoxman], 243, 10, 500, ob.CurrentLocation);
                            break;
                        }
                    case 4: //MutatedManWorm
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.MutatedManworm], 272, 6, 500, ob.CurrentLocation);

                            SoundManager.PlaySound(((ushort)Monster.MutatedManworm) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    case 5: //WitchDoctor
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.WitchDoctor], 328, 20, 1000, ob.CurrentLocation);
                            SoundManager.PlaySound(((ushort)Monster.WitchDoctor) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    case 6: //TurtleKing
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.TurtleKing], 946, 10, 500, ob.CurrentLocation);
                            break;
                        }
                    case 7: //Mandrill
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.Mandrill], 320, 10, 1000, ob.CurrentLocation);
                            SoundManager.PlaySound(((ushort)Monster.Mandrill) * 10 + 6);
                            playDefaultSound = false;
                            break;
                        }
                    case 8: //DarkCaptain
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.DarkCaptain], 1248, 10, 1000, ob.CurrentLocation));
                            SoundManager.PlaySound(((ushort)Monster.DarkCaptain) * 10 + 8);
                            playDefaultSound = false;
                            break;
                        }
                    case 9: //Doe
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.Doe], 208, 10, 1000, ob.CurrentLocation);
                            SoundManager.PlaySound(((ushort)Monster.Doe) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    case 10: //HornedCommander
                        {
                            MapControl.Effects.Add(effect = new Effect(Libraries.Monsters[(ushort)Monster.HornedCommander], 976, 10, 1000, ob.CurrentLocation));
                            SoundManager.PlaySound(8455);
                            playDefaultSound = false;
                            break;
                        }
                    case 11: //SnowWolfKing
                        {
                            MapControl.Effects.Add(effect = new Effect(Libraries.Monsters[(ushort)Monster.SnowWolfKing], 609, 10, 1000, ob.CurrentLocation));
                            SoundManager.PlaySound(8455);
                            playDefaultSound = false;
                            break;
                        }
                    case 12: //Behemoth
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.Behemoth], 810, 10, 1000, ob.CurrentLocation);
                            SoundManager.PlaySound(((ushort)Monster.Behemoth) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    default:
                        {
                            effect = new Effect(Libraries.Magic, 250, 10, 500, ob.CurrentLocation);
                            break;
                        }
                }

                //Doesn't seem to have ever worked properly - Meant to remove object after animation complete, however due to server mechanics will always
                //instantly remove object and never play TeleportOut animation. Changing to a MapEffect - not ideal as theres no delay.

                MapControl.Effects.Add(effect);

                //if (effect != null)
                //{
                //    effect.Complete += (o, e) => ob.Remove();
                //    ob.Effects.Add(effect);
                //}

                if (playDefaultSound)
                {
                    SoundManager.PlaySound(SoundList.Teleport);
                }

                return;
            }
        }
        private void ObjectTeleportIn(S.ObjectTeleportIn p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;

                bool playDefaultSound = true;

                switch (p.Type)
                {
                    case 1: //Yimoogi
                        {
                            ob.Effects.Add(new Effect(Libraries.Magic2, 1310, 10, 500, ob));
                            break;
                        }
                    case 2: //RedFoxman
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.RedFoxman], 253, 10, 500, ob));
                            break;
                        }
                    case 4: //MutatedManWorm
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.MutatedManworm], 278, 7, 500, ob));
                            SoundManager.PlaySound(((ushort)Monster.MutatedManworm) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    case 5: //WitchDoctor
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.WitchDoctor], 348, 20, 1000, ob));
                            SoundManager.PlaySound(((ushort)Monster.WitchDoctor) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    case 6: //TurtleKing
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.TurtleKing], 956, 10, 500, ob));
                            break;
                        }
                    case 7: //Mandrill
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mandrill], 330, 10, 1000, ob));
                            SoundManager.PlaySound(((ushort)Monster.Mandrill) * 10 + 6);
                            playDefaultSound = false;
                            break;
                        }
                    case 8: //DarkCaptain
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.DarkCaptain], 1248, 10, 1000, ob));
                            SoundManager.PlaySound(((ushort)Monster.DarkCaptain) * 10 + 9);
                            playDefaultSound = false;
                            break;
                        }
                    case 9: //Doe
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Doe], 208, 10, 1000, ob));
                            SoundManager.PlaySound(((ushort)Monster.Doe) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    case 10: //HornedCommander
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.HornedCommander], 976, 10, 1000, ob));
                            SoundManager.PlaySound(8455);
                            playDefaultSound = false;
                            break;
                        }
                    case 11: //SnowWolfKing
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.SnowWolfKing], 619, 10, 1000, ob));
                            SoundManager.PlaySound(8455);
                            playDefaultSound = false;
                            break;
                        }
                    case 12: //Behemoth
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Behemoth], 820, 5, 500, ob));
                            SoundManager.PlaySound(((ushort)Monster.Behemoth) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    default:
                        {
                            ob.Effects.Add(new Effect(Libraries.Magic, 260, 10, 500, ob));
                            break;
                        }
                }

                if (p.ObjectID == User.ObjectID)
                {
                    User.TargetID = User.LastTargetObjectId;
                }

                if (playDefaultSound)
                {
                    SoundManager.PlaySound(SoundList.Teleport);
                }

                return;
            }
        }

        private void TeleportIn()
        {
            User.Effects.Add(new Effect(Libraries.Magic, 260, 10, 500, User));
            SoundManager.PlaySound(SoundList.Teleport);
        }
        private void NPCGoods(S.NPCGoods p)
        {
            for (int i = 0; i < p.List.Count; i++)
            {
                p.List[i].Info = GetInfo(p.List[i].ItemIndex);
            }

            NPCRate = p.Rate;
            HideAddedStoreStats = p.HideAddedStats;

            if (!NPCDialog.Visible) return;

            switch (p.Type)
            {
                case PanelType.Buy:
                    NPCGoodsDialog.UsePearls = false;
                    NPCGoodsDialog.NewGoods(p.List);
                    NPCGoodsDialog.Show();
                    break;
                case PanelType.BuySub:
                    NPCSubGoodsDialog.UsePearls = false;
                    NPCSubGoodsDialog.NewGoods(p.List);
                    NPCSubGoodsDialog.Show();
                    break;
                case PanelType.Craft:
                    NPCCraftGoodsDialog.UsePearls = false;
                    NPCCraftGoodsDialog.NewGoods(p.List);
                    NPCCraftGoodsDialog.Show();
                    CraftDialog.Show();
                    break;
            }
        }
        private void NPCPearlGoods(S.NPCPearlGoods p)
        {
            for (int i = 0; i < p.List.Count; i++)
            {
                p.List[i].Info = GetInfo(p.List[i].ItemIndex);
            }

            NPCRate = p.Rate;

            if (!NPCDialog.Visible) return;

            NPCGoodsDialog.UsePearls = true;
            NPCGoodsDialog.NewGoods(p.List);
            NPCGoodsDialog.Show();
        }

        private void NPCSell()
        {
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Sell;
            NPCDropDialog.Show();
        }
        private void NPCRepair(S.NPCRepair p)
        {
            NPCRate = p.Rate;
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Repair;
            NPCDropDialog.Show();
        }
        private void NPCStorage()
        {
            if (NPCDialog.Visible)
                StorageDialog.Show();
        }
        private void NPCRequestInput(S.NPCRequestInput p)
        {
            MirInputBox inputBox = new MirInputBox("请输入信息");

            inputBox.OKButton.Click += (o1, e1) =>
            {
                Network.Enqueue(new C.NPCConfirmInput { Value = inputBox.InputTextBox.Text, NPCID = p.NPCID, PageName = p.PageName });
                inputBox.Dispose();
            };
            inputBox.Show();
        }

        private void NPCSRepair(S.NPCSRepair p)
        {
            NPCRate = p.Rate;
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.SpecialRepair;
            NPCDropDialog.Show();
        }

        private void NPCRefine(S.NPCRefine p)
        {
            NPCRate = p.Rate;
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Refine;
            if (p.Refining)
            {
                NPCDropDialog.Hide();
                NPCDialog.Hide();
            }
            else
                NPCDropDialog.Show();
        }

        private void NPCCheckRefine(S.NPCCheckRefine p)
        {
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.CheckRefine;
            NPCDropDialog.Show();
        }

        private void NPCCollectRefine(S.NPCCollectRefine p)
        {
            if (!NPCDialog.Visible) return;
            NPCDialog.Hide();
        }

        private void NPCReplaceWedRing(S.NPCReplaceWedRing p)
        {
            if (!NPCDialog.Visible) return;
            NPCRate = p.Rate;
            NPCDropDialog.PType = PanelType.ReplaceWedRing;
            NPCDropDialog.Show();
        }


        private void SellItem(S.SellItem p)
        {
            MirItemCell cell = InventoryDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);

            if (cell == null) return;

            cell.Locked = false;

            if (!p.Success) return;

            if (p.Count == cell.Item.Count)
                cell.Item = null;
            else
                cell.Item.Count -= p.Count;

            User.RefreshStats();
        }
        private void RepairItem(S.RepairItem p)
        {
            MirItemCell cell = InventoryDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);

            if (cell == null) return;

            cell.Locked = false;
        }
        private void CraftItem(S.CraftItem p)
        {
            if (!p.Success) return;

            CraftDialog.UpdateCraftCells();
            User.RefreshStats();
        }
        private void ItemRepaired(S.ItemRepaired p)
        {
            UserItem item = null;
            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.UniqueID)
                {
                    item = User.Inventory[i];
                    break;
                }
            }

            if (item == null)
            {
                for (int i = 0; i < User.Equipment.Length; i++)
                {
                    if (User.Equipment[i] != null && User.Equipment[i].UniqueID == p.UniqueID)
                    {
                        item = User.Equipment[i];
                        break;
                    }
                }
            }

            if (Hero != null)
            {
                if (item == null)
                {
                    for (int i = 0; i < Hero.Inventory.Length; i++)
                    {
                        if (Hero.Inventory[i] != null && Hero.Inventory[i].UniqueID == p.UniqueID)
                        {
                            item = Hero.Inventory[i];
                            break;
                        }
                    }
                }

                if (item == null)
                {
                    for (int i = 0; i < Hero.Equipment.Length; i++)
                    {
                        if (Hero.Equipment[i] != null && Hero.Equipment[i].UniqueID == p.UniqueID)
                        {
                            item = Hero.Equipment[i];
                            break;
                        }
                    }
                }
            }

            if (item == null) return;

            item.MaxDura = p.MaxDura;
            item.CurrentDura = p.CurrentDura;

            if (HoverItem == item)
            {
                DisposeItemLabel();
                CreateItemLabel(item);
            }
        }

        private void ItemSlotSizeChanged(S.ItemSlotSizeChanged p)
        {
            UserItem item = null;
            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.UniqueID)
                {
                    item = User.Inventory[i];
                    break;
                }
            }

            if (item == null)
            {
                for (int i = 0; i < User.Equipment.Length; i++)
                {
                    if (User.Equipment[i] != null && User.Equipment[i].UniqueID == p.UniqueID)
                    {
                        item = User.Equipment[i];
                        break;
                    }
                }
            }

            if (Hero != null)
            {
                if (item == null)
                {
                    for (int i = 0; i < Hero.Inventory.Length; i++)
                    {
                        if (Hero.Inventory[i] != null && Hero.Inventory[i].UniqueID == p.UniqueID)
                        {
                            item = Hero.Inventory[i];
                            break;
                        }
                    }
                }

                if (item == null)
                {
                    for (int i = 0; i < Hero.Equipment.Length; i++)
                    {
                        if (Hero.Equipment[i] != null && Hero.Equipment[i].UniqueID == p.UniqueID)
                        {
                            item = Hero.Equipment[i];
                            break;
                        }
                    }
                }
            }

            if (item == null) return;

            item.SetSlotSize(p.SlotSize);
        }

        private void ItemSealChanged(S.ItemSealChanged p)
        {
            UserItem item = null;
            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.UniqueID)
                {
                    item = User.Inventory[i];
                    break;
                }
            }

            if (item == null)
            {
                for (int i = 0; i < User.Equipment.Length; i++)
                {
                    if (User.Equipment[i] != null && User.Equipment[i].UniqueID == p.UniqueID)
                    {
                        item = User.Equipment[i];
                        break;
                    }
                }
            }

            if (Hero != null)
            {
                if (item == null)
                {
                    for (int i = 0; i < Hero.Inventory.Length; i++)
                    {
                        if (Hero.Inventory[i] != null && Hero.Inventory[i].UniqueID == p.UniqueID)
                        {
                            item = Hero.Inventory[i];
                            break;
                        }
                    }
                }

                if (item == null)
                {
                    for (int i = 0; i < Hero.Equipment.Length; i++)
                    {
                        if (Hero.Equipment[i] != null && Hero.Equipment[i].UniqueID == p.UniqueID)
                        {
                            item = Hero.Equipment[i];
                            break;
                        }
                    }
                }
            }

            if (item == null) return;

            item.SealedInfo = new SealedInfo { ExpiryDate = p.ExpiryDate };

            if (HoverItem == item)
            {
                DisposeItemLabel();
                CreateItemLabel(item);
            }
        }

        private void ItemUpgraded(S.ItemUpgraded p)
        {
            UserItem item = null;
            MirGridType grid = MirGridType.Inventory;
            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.Item.UniqueID)
                {
                    item = User.Inventory[i];
                    break;
                }
            }

            if (item == null && Hero != null)
            {
                for (int i = 0; i < Hero.Inventory.Length; i++)
                {
                    if (Hero.Inventory[i] != null && Hero.Inventory[i].UniqueID == p.Item.UniqueID)
                    {
                        item = Hero.Inventory[i];
                        grid = MirGridType.HeroInventory;
                        break;
                    }
                }
            }

            if (item == null) return;

            item.AddedStats.Clear();
            item.AddedStats.Add(p.Item.AddedStats);

            item.MaxDura = p.Item.MaxDura;
            item.RefineAdded = p.Item.RefineAdded;
            
            switch (grid)
            {
                case MirGridType.Inventory:
                    InventoryDialog.DisplayItemGridEffect(item.UniqueID, 0);
                    break;
                case MirGridType.HeroInventory:
                    HeroInventoryDialog.DisplayItemGridEffect(item.UniqueID, 0);
                    break;
            }
           

            if (HoverItem == item)
            {
                DisposeItemLabel();
                CreateItemLabel(item);
            }
        }

        private void NewMagic(S.NewMagic p)
        {
            ClientMagic magic = p.Magic;

            UserObject actor = User;
            if (p.Hero)
                actor = Hero;

            actor.Magics.Add(magic);
            actor.RefreshStats();
            foreach (SkillBarDialog Bar in SkillBarDialogs)
            {
                Bar.Update();
            }
        }

        private void RemoveMagic(S.RemoveMagic p)
        {
            User.Magics.RemoveAt(p.PlaceId);
            User.RefreshStats();
            foreach (SkillBarDialog Bar in SkillBarDialogs)
            {
                Bar.Update();
            }
        }

        private void MagicLeveled(S.MagicLeveled p)
        {
            UserObject actor = p.ObjectID == Hero?.ObjectID ? Hero : User;

            for (int i = 0; i < actor.Magics.Count; i++)
            {
                ClientMagic magic = actor.Magics[i];
                if (magic.Spell != p.Spell) continue;

                if (magic.Level != p.Level)
                {
                    magic.Level = p.Level;
                    actor.RefreshStats();
                }

                magic.Experience = p.Experience;
                break;
            }
        }
        private void Magic(S.Magic p)
        {
            User.Spell = p.Spell;
            User.Cast = p.Cast;
            User.TargetID = p.TargetID;
            User.TargetPoint = p.Target;
            User.SpellLevel = p.Level;
            User.SecondaryTargetIDs = p.SecondaryTargetIDs;

            if (!p.Cast) return;

            ClientMagic magic = User.GetMagic(p.Spell);
            magic.CastTime = CMain.Time;
        }

        private void MagicDelay(S.MagicDelay p)
        {
            ClientMagic magic;
            if (p.ObjectID == Hero?.ObjectID)
                magic = Hero.GetMagic(p.Spell);
            else
                magic = User.GetMagic(p.Spell);
            magic.Delay = p.Delay;
        }

        private void MagicCast(S.MagicCast p)
        {
            ClientMagic magic = User.GetMagic(p.Spell);
            magic.CastTime = CMain.Time;
        }

        private void ObjectMagic(S.ObjectMagic p)
        {
            if (p.SelfBroadcast == false && p.ObjectID == User.ObjectID && !Observing) return;

            if (p.ObjectID == Hero?.ObjectID && p.Cast)
            {
                ClientMagic magic = Hero.GetMagic(p.Spell);
                magic.CastTime = CMain.Time;
            }

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;

                QueuedAction action = new QueuedAction { Action = MirAction.施法动作, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                action.Params.Add(p.Spell);
                action.Params.Add(p.TargetID);
                action.Params.Add(p.Target);
                action.Params.Add(p.Cast);
                action.Params.Add(p.Level);
                action.Params.Add(p.SecondaryTargetIDs);

                ob.ActionFeed.Add(action);
                return;
            }
        }

        private void ObjectProjectile(S.ObjectProjectile p)
        {
            MapObject source = MapControl.GetObject(p.Source);

            if (source == null) return;

            switch (p.Spell)
            {
                case Spell.FireBounce:
                    {
                        SoundManager.PlaySound(20000 + (ushort)Spell.GreatFireBall * 10 + 1);

                        Missile missile = source.CreateProjectile(410, Libraries.Magic, true, 6, 30, 4, targetID: p.Destination);

                        if (missile.Target != null)
                        {
                            missile.Complete += (o, e) =>
                            {
                                var sender = (Missile)o;

                                if (sender.Target.CurrentAction == MirAction.死后尸体) return;
                                sender.Target.Effects.Add(new Effect(Libraries.Magic, 570, 10, 600, sender.Target));
                                SoundManager.PlaySound(20000 + (ushort)Spell.GreatFireBall * 10 + 2);
                            };
                        }
                    }
                    break;
            }
        }

        private void ObjectEffect(S.ObjectEffect p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                PlayerObject player;

                switch (p.Effect)
                {
                    // Sanjian
                    case SpellEffect.FurbolgWarriorCritical:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.FurbolgWarrior], 448, 6, 600, ob));
                        SoundManager.PlaySound(20000 + (ushort)Spell.FatalSword * 10);
                        break;

                    case SpellEffect.FatalSword:
                        ob.Effects.Add(new Effect(Libraries.Magic2, 1940, 4, 400, ob));
                        SoundManager.PlaySound(20000 + (ushort)Spell.FatalSword * 10);
                        break;
                    case SpellEffect.StormEscape:
                        ob.Effects.Add(new Effect(Libraries.Magic3, 610, 8, 600, ob));
                        SoundManager.PlaySound(SoundList.Teleport);
                        break;
                    case SpellEffect.StormEscapeRare:
                        ob.Effects.Add(new Effect(Libraries.Magic3, 610, 8, 600, ob));
                        SoundManager.PlaySound(SoundList.Teleport);
                        break;
                    case SpellEffect.Teleport:
                        ob.Effects.Add(new Effect(Libraries.Magic, 1600, 10, 600, ob));
                        SoundManager.PlaySound(SoundList.Teleport);
                        break;
                    case SpellEffect.Healing:
                        SoundManager.PlaySound(20000 + (ushort)Spell.Healing * 10 + 1);
                        ob.Effects.Add(new Effect(Libraries.Magic, 370, 10, 800, ob));
                        break;
                    case SpellEffect.HealingRare:
                        SoundManager.PlaySound(20000 + (ushort)Spell.HealingRare * 10 + 1);
                        ob.Effects.Add(new Effect(Libraries.Magic, 370, 10, 800, ob));
                        break;
                    case SpellEffect.RedMoonEvil:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.RedMoonEvil], 32, 6, 400, ob) { Blend = false });
                        break;
                    case SpellEffect.BloodthirstySpike:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.ChieftainSword], 1188, 6, 300, ob, CMain.Time + 1200) { Blend = true, DrawBehind = true});
                        break;
                    case SpellEffect.GroundBurstIce:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.ShardGuardian], 544, 6, 300, ob, CMain.Time + 1200) { Blend = true, DrawBehind = true});
                        break;
                    case SpellEffect.MirEmperor:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.MirEmperor], 62, 4, 400, ob) { Blend = false });
                        SoundManager.PlaySound(5347);
                        break;
                    case SpellEffect.TwinDrakeBlade:
                        ob.Effects.Add(new Effect(Libraries.Magic2, 380, 6, 800, ob));
                        break;
                    case SpellEffect.HealingcircleRare:
                        ob.Effects.Add(new Effect(Libraries.Magic3, 660, 10, 600, ob));
                        break;
                    case SpellEffect.HealingcircleRare1:
                        ob.Effects.Add(new Effect(Libraries.Magic3, 650, 10, 600, ob));
                        break;
                   case SpellEffect.MPEater:
                        for (int j = MapControl.Objects.Count - 1; j >= 0; j--)
                        {
                            MapObject ob2 = MapControl.Objects[j];
                            if (ob2.ObjectID == p.EffectType)
                            {
                                ob2.Effects.Add(new Effect(Libraries.Magic2, 2411, 19, 1900, ob2));
                                break;
                            }
                        }
                        ob.Effects.Add(new Effect(Libraries.Magic2, 2400, 9, 900, ob));
                        SoundManager.PlaySound(20000 + (ushort)Spell.FatalSword * 10);
                        break;
                    case SpellEffect.Bleeding:
                        ob.Effects.Add(new Effect(Libraries.Magic3, 60, 3, 400, ob));
                        break;
                    case SpellEffect.Hemorrhage:
                        SoundManager.PlaySound(20000 + (ushort)Spell.Hemorrhage * 10);
                        ob.Effects.Add(new Effect(Libraries.Magic3, 0, 4, 400, ob));
                        ob.Effects.Add(new Effect(Libraries.Magic3, 28, 6, 600, ob));
                        ob.Effects.Add(new Effect(Libraries.Magic3, 46, 8, 800, ob));
                        break;
                    case SpellEffect.MagicShieldUp:
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Hero) return;
                        player = (PlayerObject)ob;
                        if (player.ShieldEffect != null)
                        {
                            player.ShieldEffect.Clear();
                            player.ShieldEffect.Remove();
                        }
                        player.MagicShield = true;
                        player.Effects.Add(player.ShieldEffect = new Effect(Libraries.Magic, 3890, 3, 600, ob) { Repeat = true });
                        break;
                    case SpellEffect.MagicShieldDown:
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Hero) return;
                        player = (PlayerObject)ob;
                        if (player.ShieldEffect != null)
                        {
                            player.ShieldEffect.Clear();
                            player.ShieldEffect.Remove();
                        }
                        player.ShieldEffect = null;
                        player.MagicShield = false;
                        break;
                    case SpellEffect.GreatFoxSpirit:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.GreatFoxSpirit], 375 + (CMain.Random.Next(3) * 20), 20, 1400, ob));
                        SoundManager.PlaySound(((ushort)Monster.GreatFoxSpirit * 10) + 5);
                        break;
                    case SpellEffect.Entrapment:
                        ob.Effects.Add(new Effect(Libraries.Magic2, 1010, 10, 1500, ob));
                        ob.Effects.Add(new Effect(Libraries.Magic2, 1020, 8, 1200, ob));
                        break;
                    case SpellEffect.EntrapmentRare:
                        ob.Effects.Add(new Effect(Libraries.Magic3, 4390, 10, 1500, ob));
                        ob.Effects.Add(new Effect(Libraries.Magic3, 4400, 8, 1200, ob));
                        break;
                    case SpellEffect.Critical:
                        //ob.Effects.Add(new Effect(Libraries.CustomEffects, 0, 12, 60, ob));
                        break;
                    case SpellEffect.Reflect:
                        ob.Effects.Add(new Effect(Libraries.Effect, 580, 10, 70, ob));
                        break;
                    case SpellEffect.ElementalBarrierUp:
                        if (ob.Race != ObjectType.Player) return;
                        player = (PlayerObject)ob;
                        if (player.ElementalBarrierEffect != null)
                        {
                            player.ElementalBarrierEffect.Clear();
                            player.ElementalBarrierEffect.Remove();
                        }

                        player.ElementalBarrier = true;
                        player.Effects.Add(player.ElementalBarrierEffect = new Effect(Libraries.Magic3, 1890, 10, 2000, ob) { Repeat = true });
                        break;
                    case SpellEffect.ElementalBarrierDown:
                        if (ob.Race != ObjectType.Player) return;
                        player = (PlayerObject)ob;
                        if (player.ElementalBarrierEffect != null)
                        {
                            player.ElementalBarrierEffect.Clear();
                            player.ElementalBarrierEffect.Remove();
                        }
                        player.ElementalBarrierEffect = null;
                        player.ElementalBarrier = false;
                        player.Effects.Add(player.ElementalBarrierEffect = new Effect(Libraries.Magic3, 1910, 7, 1400, ob));
                        SoundManager.PlaySound(20000 + 131 * 10 + 5);
                        break;
                    case SpellEffect.DelayedExplosion:
                        int effectid = DelayedExplosionEffect.GetOwnerEffectID(ob.ObjectID);
                        if (effectid < 0)
                        {
                            ob.Effects.Add(new DelayedExplosionEffect(Libraries.Magic3, 1590, 8, 1200, ob, true, 0, 0));
                        }
                        else if (effectid >= 0)
                        {
                            if (DelayedExplosionEffect.effectlist[effectid].stage < p.EffectType)
                            {
                                DelayedExplosionEffect.effectlist[effectid].Remove();
                                ob.Effects.Add(new DelayedExplosionEffect(Libraries.Magic3, 1590 + ((int)p.EffectType * 10), 8, 1200, ob, true, (int)p.EffectType, 0));
                            }
                        }
                        break;
                    case SpellEffect.AwakeningSuccess:
                        {
                            Effect ef = new Effect(Libraries.Magic3, 900, 16, 1600, ob, CMain.Time + p.DelayTime);
                            ef.Played += (o, e) => SoundManager.PlaySound(50002);
                            ef.Complete += (o, e) => MapControl.AwakeningAction = false;
                            ob.Effects.Add(ef);
                            ob.Effects.Add(new Effect(Libraries.Magic3, 840, 16, 1600, ob, CMain.Time + p.DelayTime) { Blend = false });
                        }
                        break;
                    case SpellEffect.AwakeningFail:
                        {
                            Effect ef = new Effect(Libraries.Magic3, 920, 9, 900, ob, CMain.Time + p.DelayTime);
                            ef.Played += (o, e) => SoundManager.PlaySound(50003);
                            ef.Complete += (o, e) => MapControl.AwakeningAction = false;
                            ob.Effects.Add(ef);
                            ob.Effects.Add(new Effect(Libraries.Magic3, 860, 9, 900, ob, CMain.Time + p.DelayTime) { Blend = false });
                        }
                        break;
                    case SpellEffect.AwakeningHit:
                        {
                            Effect ef = new Effect(Libraries.Magic3, 880, 5, 500, ob, CMain.Time + p.DelayTime);
                            ef.Played += (o, e) => SoundManager.PlaySound(50001);
                            ob.Effects.Add(ef);
                            ob.Effects.Add(new Effect(Libraries.Magic3, 820, 5, 500, ob, CMain.Time + p.DelayTime) { Blend = false });
                        }
                        break;
                    case SpellEffect.AwakeningMiss:
                        {
                            Effect ef = new Effect(Libraries.Magic3, 890, 5, 500, ob, CMain.Time + p.DelayTime);
                            ef.Played += (o, e) => SoundManager.PlaySound(50000);
                            ob.Effects.Add(ef);
                            ob.Effects.Add(new Effect(Libraries.Magic3, 830, 5, 500, ob, CMain.Time + p.DelayTime) { Blend = false });
                        }
                        break;
                    case SpellEffect.TurtleKing:
                        {
                            Effect ef = new Effect(Libraries.Monsters[(ushort)Monster.TurtleKing], CMain.Random.Next(2) == 0 ? 922 : 934, 12, 1200, ob);
                            ef.Played += (o, e) => SoundManager.PlaySound(20000 + (ushort)Spell.HellFire * 10 + 1);
                            ob.Effects.Add(ef);
                        }
                        break;
                    case SpellEffect.Behemoth:
                        {
                            MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Behemoth], 845, 10, 1500, ob.CurrentLocation));
                            MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Behemoth], 835, 10, 1500, ob.CurrentLocation, 0, true) { Blend = false });
                        }
                        break;
                    case SpellEffect.Stunned:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.StoningStatue], 632, 10, 1000, ob)
                        {
                            Repeat = p.Time > 0,
                            RepeatUntil = p.Time > 0 ? CMain.Time + p.Time : 0
                        });
                        break;
                    case SpellEffect.IcePillar:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.IcePillar], 18, 8, 800, ob));
                        break;
                    case SpellEffect.KingGuard:
                        if (p.EffectType == 0)
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.KingGuard], 800, 10, 1000, ob) { Blend = false });
                        }
                        else
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.KingGuard], 810, 10, 1000, ob) { Blend = false });
                        }
                        break;
                    case SpellEffect.FlamingMutantWeb:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.FlamingMutant], 330, 10, 1000, ob)
                        {
                            Repeat = p.Time > 0,
                            RepeatUntil = p.Time > 0 ? CMain.Time + p.Time : 0
                        });
                        break;
                    case SpellEffect.DeathCrawlerBreath:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.DeathCrawler], 304 + ((int)ob.Direction * 9), 9, 400, ob) { Blend = true });
                        break;
                    case SpellEffect.MoonMist:
                        ob.Effects.Add(new Effect(Libraries.Magic3, 705, 10, 800, ob));
                        break;
                    case SpellEffect.Mon562NLightning:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon562N], 666, 5, 300, ob) { Blend = true });
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon562N], 671, 5, 1000, ob) { Blend = true });
                        break;
                    case SpellEffect.Mon563NPoisonCloud:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon563N], 733, 4, 300, ob) { Blend = true, DrawBehind = true });
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon563N], 737, 14, 1000, ob) { Blend = true });
                        break;
                    case SpellEffect.Mon564NFlame:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon564N], 756, 16, 800, ob) { Blend = true });
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon564N], 773, 16, 800, ob) { Blend = true, DrawBehind = true });
                        break;
                    case SpellEffect.Mon572NLightning:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon572N], 406, 8, 300, ob) { Blend = true });
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon572N], 414, 10, 1000, ob) { Blend = true });
                        break;
                    case SpellEffect.Mon573NCobweb:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon573N], 547, 4, 300, ob) { Blend = true });
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon573N], 551, 4, 1000, ob) { Blend = true });
                        break;
                }

                return;
            }
        }

        private void RangeAttack(S.RangeAttack p)
        {
            User.TargetID = p.TargetID;
            User.TargetPoint = p.Target;
            User.Spell = p.Spell;
        }

        private void Pushed(S.Pushed p)
        {
            User.ActionFeed.Add(new QueuedAction { Action = MirAction.推开动作, Direction = p.Direction, Location = p.Location });
        }

        private void ObjectPushed(S.ObjectPushed p)
        {
            if (p.ObjectID == User.ObjectID) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.推开动作, Direction = p.Direction, Location = p.Location });

                return;
            }
        }

        private void ObjectName(S.ObjectName p)
        {
            if (p.ObjectID == User.ObjectID) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.Name = p.Name;
                return;
            }
        }
        private void UserStorage(S.UserStorage p)
        {
            if(Storage.Length != p.Storage.Length)
            {
                Array.Resize(ref Storage, p.Storage.Length);
            }

            Storage = p.Storage;

            for (int i = 0; i < Storage.Length; i++)
            {
                if (Storage[i] == null) continue;
                Bind(Storage[i]);
            }
        }
        private void SwitchGroup(S.SwitchGroup p)
        {
            GroupDialog.AllowGroup = p.AllowGroup;

            if (!p.AllowGroup && GroupDialog.GroupList.Count > 0)
                DeleteGroup();
        }

        private void DeleteGroup()
        {
            GroupDialog.GroupList.Clear();
            GroupDialog.GroupMembersMap.Clear();
            BigMapViewPort.PlayerLocations.Clear();
            ChatDialog.ReceiveChat("离开了组", ChatType.Group);
        }

        private void DeleteMember(S.DeleteMember p)
        {
            GroupDialog.GroupList.Remove(p.Name);
            GroupDialog.GroupMembersMap.Remove(p.Name);
            BigMapViewPort.PlayerLocations.Remove(p.Name);
            ChatDialog.ReceiveChat(string.Format("-{0} 已离开组", p.Name), ChatType.Group);
        }

        private void GroupInvite(S.GroupInvite p)
        {
            MirMessageBox messageBox = new MirMessageBox(string.Format("是否同意跟 {0} 组队", p.Name), MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) =>
            {
                Network.Enqueue(new C.GroupInvite { AcceptInvite = true });
                GroupDialog.Show();
            }; 
            messageBox.NoButton.Click += (o, e) => Network.Enqueue(new C.GroupInvite { AcceptInvite = false });
            messageBox.Show();
        }
        private void AddMember(S.AddMember p)
        {
            GroupDialog.GroupList.Add(p.Name);
            ChatDialog.ReceiveChat(string.Format("-{0} 已加入组", p.Name), ChatType.Group);
        }
        private void GroupMembersMap(S.GroupMembersMap p)
        {
            if (!GroupDialog.GroupMembersMap.ContainsKey(p.PlayerName))
                GroupDialog.GroupMembersMap.Add(p.PlayerName, p.PlayerMap);
            else
            {
                GroupDialog.GroupMembersMap.Remove(p.PlayerName);
                GroupDialog.GroupMembersMap.Add(p.PlayerName, p.PlayerMap);
            }
        }
        private void SendMemberLocation(S.SendMemberLocation p)
        {
            if (!BigMapViewPort.PlayerLocations.ContainsKey(p.MemberName))
                BigMapViewPort.PlayerLocations.Add(p.MemberName, p.MemberLocation);
            else
            {
                BigMapViewPort.PlayerLocations.Remove(p.MemberName);
                BigMapViewPort.PlayerLocations.Add(p.MemberName, p.MemberLocation);
            }
        }
        private void Revived()
        {
            User.SetAction();
            User.Dead = false;
            User.Effects.Add(new Effect(Libraries.Magic2, 1220, 20, 2000, User));
            SoundManager.PlaySound(SoundList.Revive);
        }
        private void ObjectRevived(S.ObjectRevived p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                if (p.Effect)
                {
                    ob.Effects.Add(new Effect(Libraries.Magic2, 1220, 20, 2000, ob));
                    SoundManager.PlaySound(SoundList.Revive);
                }
                ob.Dead = false;
                ob.ActionFeed.Clear();
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.复活动作, Direction = ob.Direction, Location = ob.CurrentLocation });
                return;
            }
        }
        private void SpellToggle(S.SpellToggle p)
        {
            UserObject actor = User;
            string prefix = string.Empty;

            if (p.ObjectID == Hero?.ObjectID)
            {
                actor = Hero;
                prefix = "(英雄) ";
            }

            switch (p.Spell)
            {
                //Warrior
                case Spell.Slaying:
                    actor.Slaying = p.CanUse;
                    break;
                case Spell.Thrusting:
                    actor.Thrusting = p.CanUse;
                    ChatDialog.ReceiveChat(prefix + (actor.Thrusting ? "技能状态：刺杀剑术开启" : "技能状态：刺杀剑术关闭"), ChatType.Hint);
                    break;
                case Spell.HalfMoon:
                    actor.HalfMoon = p.CanUse;
                    ChatDialog.ReceiveChat(prefix + (actor.HalfMoon ? "技能状态：半月弯刀开启" : "技能状态：半月弯刀关闭"), ChatType.Hint);
                    break;
                case Spell.CrossHalfMoon:
                    actor.CrossHalfMoon = p.CanUse;
                    ChatDialog.ReceiveChat(prefix + (actor.CrossHalfMoon ? "技能状态：狂风斩开启" : "技能状态：狂风斩关闭"), ChatType.Hint);
                    break;
                case Spell.DoubleSlash:
                    actor.DoubleSlash = p.CanUse;
                    ChatDialog.ReceiveChat(prefix + (actor.DoubleSlash ? "技能状态：风剑术开启" : "技能状态：风剑术关闭"), ChatType.Hint);
                    break;
                case Spell.FlamingSword:
                    actor.FlamingSword = p.CanUse;
                    if (actor.FlamingSword)
                        ChatDialog.ReceiveChat(prefix + GameLanguage.WeaponSpiritFire, ChatType.Hint);
                    else
                        ChatDialog.ReceiveChat(prefix + GameLanguage.SpiritsFireDisappeared, ChatType.System);
                    break;
            }
        }

        private void ObjectHealth(S.ObjectHealth p)
        {
            if (p.ObjectID == Hero?.ObjectID)
                Hero.PercentHealth = p.Percent;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.PercentHealth = p.Percent;
                ob.HealthTime = CMain.Time + p.Expire * 1000;
                return;
            }
        }

        private void ObjectMana(S.ObjectMana p)
        {
            if (p.ObjectID == Hero?.ObjectID)
                Hero.PercentMana = p.Percent;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.PercentMana = p.Percent;
                return;
            }
        }

        private void MapEffect(S.MapEffect p)
        {
            switch (p.Effect)
            {
                case SpellEffect.Mine:
                    SoundManager.PlaySound(10091);
                    Effect HitWall = new Effect(Libraries.Effect, 8 * p.Value, 3, 240, p.Location) { Light = 0 };
                    MapControl.Effects.Add(HitWall);
                    break;
                case SpellEffect.Tester:
                    Effect eff = new Effect(Libraries.Effect, 328, 10, 500, p.Location) { Light = 0 };
                    MapControl.Effects.Add(eff);
                    break;
            }
        }

        private void ObjectRangeAttack(S.ObjectRangeAttack p)
        {
            if (p.ObjectID == User.ObjectID &&
                !Observing) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                QueuedAction action = null;
                if (ob.Race == ObjectType.Player)
                {
                    switch (p.Type)
                    {
                        default:
                            {
                                action = new QueuedAction { Action = MirAction.远程攻击1, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                    }
                }
                else
                {
                    switch (p.Type)
                    {
                        case 1:
                            {
                                action = new QueuedAction { Action = MirAction.远程攻击2, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                        case 2:
                            {
                                action = new QueuedAction { Action = MirAction.远程攻击3, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                        default:
                            {
                                action = new QueuedAction { Action = MirAction.远程攻击1, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                    }
                }
                action.Params.Add(p.TargetID);
                action.Params.Add(p.Target);
                action.Params.Add(p.Spell);
                action.Params.Add(new List<uint>());
                action.Params.Add(p.Level);

                ob.ActionFeed.Add(action);
                return;
            }
        }

        private void AddBuff(S.AddBuff p)
        {
            ClientBuff buff = p.Buff;

            if (!buff.Paused)
            {
                buff.ExpireTime += CMain.Time;
            }

            if (buff.ObjectID == User.ObjectID)
            {
                for (int i = 0; i < BuffsDialog.Buffs.Count; i++)
                {
                    if (BuffsDialog.Buffs[i].Type != buff.Type) continue;

                    BuffsDialog.Buffs[i] = buff;
                    User.RefreshStats();
                    return;
                }

                BuffsDialog.Buffs.Add(buff);
                BuffsDialog.CreateBuff(buff);

                User.RefreshStats();     
            }

            if (Hero != null && buff.ObjectID == Hero.ObjectID)
            {
                for (int i = 0; i < HeroBuffsDialog.Buffs.Count; i++)
                {
                    if (HeroBuffsDialog.Buffs[i].Type != buff.Type) continue;

                    HeroBuffsDialog.Buffs[i] = buff;
                    Hero.RefreshStats();
                    return;
                }

                HeroBuffsDialog.Buffs.Add(buff);
                HeroBuffsDialog.CreateBuff(buff);

                Hero.RefreshStats();
            }

            if (!buff.Visible || buff.ObjectID <= 0) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != buff.ObjectID) continue;
                if ((ob is PlayerObject) || (ob is MonsterObject))
                {
                    if (!ob.Buffs.Contains(buff.Type))
                    {
                        ob.Buffs.Add(buff.Type);
                    }

                    ob.AddBuffEffect(buff.Type);
                    return;
                }
            }
        }

        private void RemoveBuff(S.RemoveBuff p)
        {
            if (User.ObjectID == p.ObjectID)
            {
                for (int i = 0; i < BuffsDialog.Buffs.Count; i++)
                {
                    if (BuffsDialog.Buffs[i].Type != p.Type) continue;

                    switch (BuffsDialog.Buffs[i].Type)
                    {
                        case BuffType.轻身步:
                            User.Sprint = false;
                            break;
                        case BuffType.变形效果:
                            User.TransformType = -1;
                            break;
                    }

                    BuffsDialog.RemoveBuff(i);
                    BuffsDialog.Buffs.RemoveAt(i);
                }
                User.RefreshStats();
            }

            if (Hero != null && Hero.ObjectID == p.ObjectID)
            {
                for (int i = 0; i < HeroBuffsDialog.Buffs.Count; i++)
                {
                    if (HeroBuffsDialog.Buffs[i].Type != p.Type) continue;

                    switch (HeroBuffsDialog.Buffs[i].Type)
                    {
                        case BuffType.轻身步:
                            Hero.Sprint = false;
                            break;
                        case BuffType.变形效果:
                            Hero.TransformType = -1;
                            break;
                    }

                    HeroBuffsDialog.RemoveBuff(i);
                    HeroBuffsDialog.Buffs.RemoveAt(i);
                }
                Hero.RefreshStats();
            }

            if (p.ObjectID <= 0) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];

                if (ob.ObjectID != p.ObjectID) continue;

                ob.Buffs.Remove(p.Type);
                ob.RemoveBuffEffect(p.Type);
                return;
            }
        }

        private void PauseBuff(S.PauseBuff p)
        {
            if (User.ObjectID == p.ObjectID)
            {
                for (int i = 0; i < BuffsDialog.Buffs.Count; i++)
                {
                    if (BuffsDialog.Buffs[i].Type != p.Type) continue;

                    User.RefreshStats();

                    if (BuffsDialog.Buffs[i].Paused == p.Paused) return;

                    BuffsDialog.Buffs[i].Paused = p.Paused;

                    if (p.Paused)
                    {
                        BuffsDialog.Buffs[i].ExpireTime -= CMain.Time;
                    }
                    else
                    {
                        BuffsDialog.Buffs[i].ExpireTime += CMain.Time;
                    }
                }
            }

            if (Hero != null && Hero.ObjectID == p.ObjectID)
            {
                for (int i = 0; i < HeroBuffsDialog.Buffs.Count; i++)
                {
                    if (HeroBuffsDialog.Buffs[i].Type != p.Type) continue;

                    Hero.RefreshStats();

                    if (HeroBuffsDialog.Buffs[i].Paused == p.Paused) return;

                    HeroBuffsDialog.Buffs[i].Paused = p.Paused;

                    if (p.Paused)
                    {
                        HeroBuffsDialog.Buffs[i].ExpireTime -= CMain.Time;
                    }
                    else
                    {
                        HeroBuffsDialog.Buffs[i].ExpireTime += CMain.Time;
                    }
                }
            }
        }

        private void ObjectHidden(S.ObjectHidden p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                ob.Hidden = p.Hidden;
                return;
            }
        }

        private void ObjectSneaking(S.ObjectSneaking p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
               // ob.SneakingActive = p.SneakingActive;
                return;
            }
        }

        private void ObjectLevelEffects(S.ObjectLevelEffects p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID || ob.Race != ObjectType.Player) continue;

                PlayerObject temp = (PlayerObject)ob;

                temp.LevelEffects = p.LevelEffects;

                temp.SetEffects();
                return;
            }
        }

        private void RefreshItem(S.RefreshItem p)
        {
            Bind(p.Item);

            if (SelectedCell != null && SelectedCell.Item.UniqueID == p.Item.UniqueID)
                SelectedCell = null;

            if (HoverItem != null && HoverItem.UniqueID == p.Item.UniqueID)
            {
                DisposeItemLabel();
                CreateItemLabel(p.Item);
            }

            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.Item.UniqueID)
                {
                    User.Inventory[i] = p.Item;
                    User.RefreshStats();
                    return;
                }
            }

            for (int i = 0; i < User.Equipment.Length; i++)
            {
                if (User.Equipment[i] != null && User.Equipment[i].UniqueID == p.Item.UniqueID)
                {
                    User.Equipment[i] = p.Item;
                    User.RefreshStats();
                    return;
                }
            }

            if (Hero != null)
            {
                for (int i = 0; i < Hero.Inventory.Length; i++)
                {
                    if (Hero.Inventory[i] != null && Hero.Inventory[i].UniqueID == p.Item.UniqueID)
                    {
                        Hero.Inventory[i] = p.Item;
                        Hero.RefreshStats();
                        return;
                    }
                }

                for (int i = 0; i < Hero.Equipment.Length; i++)
                {
                    if (Hero.Equipment[i] != null && Hero.Equipment[i].UniqueID == p.Item.UniqueID)
                    {
                        Hero.Equipment[i] = p.Item;
                        Hero.RefreshStats();
                        return;
                    }
                }
            }
        }

        private void ObjectSpell(S.ObjectSpell p)
        {
            SpellObject ob = new SpellObject(p.ObjectID);
            ob.Load(p);
        }

        private void ObjectDeco(S.ObjectDeco p)
        {
            DecoObject ob = new DecoObject(p.ObjectID);
            ob.Load(p);
        }

        private void UserDash(S.UserDash p)
        {
            if (User.Direction == p.Direction && User.CurrentLocation == p.Location)
            {
                MapControl.NextAction = 0;
                return;
            }
            MirAction action = User.CurrentAction == MirAction.左冲动作 ? MirAction.右冲动作 : MirAction.左冲动作;
            for (int i = User.ActionFeed.Count - 1; i >= 0; i--)
            {
                if (User.ActionFeed[i].Action == MirAction.右冲动作)
                {
                    action = MirAction.左冲动作;
                    break;
                }
                if (User.ActionFeed[i].Action == MirAction.左冲动作)
                {
                    action = MirAction.右冲动作;
                    break;
                }
            }

            User.ActionFeed.Add(new QueuedAction { Action = action, Direction = p.Direction, Location = p.Location });
        }

        private void UserDashFail(S.UserDashFail p)
        {
            MapControl.NextAction = 0;
            User.ActionFeed.Add(new QueuedAction { Action = MirAction.冲击失败, Direction = p.Direction, Location = p.Location });
        }

        private void ObjectDash(S.ObjectDash p)
        {
            if (p.ObjectID == User.ObjectID) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;

                MirAction action = MirAction.左冲动作;

                if (ob.ActionFeed.Count > 0 && ob.ActionFeed[ob.ActionFeed.Count - 1].Action == action)
                    action = MirAction.右冲动作;

                ob.ActionFeed.Add(new QueuedAction { Action = action, Direction = p.Direction, Location = p.Location });

                return;
            }
        }

        private void ObjectDashFail(S.ObjectDashFail p)
        {
            if (p.ObjectID == User.ObjectID) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;

                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.冲击失败, Direction = p.Direction, Location = p.Location });

                return;
            }
        }

        private void UserBackStep(S.UserBackStep p)
        {
            if (User.Direction == p.Direction && User.CurrentLocation == p.Location)
            {
                MapControl.NextAction = 0;
                return;
            }
            User.ActionFeed.Add(new QueuedAction { Action = MirAction.弓箭跳跃, Direction = p.Direction, Location = p.Location });
        }

        private void ObjectBackStep(S.ObjectBackStep p)
        {
            if (p.ObjectID == User.ObjectID) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;

                ob.JumpDistance = p.Distance;

                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.弓箭跳跃, Direction = p.Direction, Location = p.Location });

                return;
            }
        }

        private void UserDashAttack(S.UserDashAttack p)
        {
            if (User.Direction == p.Direction && User.CurrentLocation == p.Location)
            {
                MapControl.NextAction = 0;
                return;
            }
            //User.JumpDistance = p.Distance;
            User.ActionFeed.Add(new QueuedAction { Action = MirAction.刺客冲击, Direction = p.Direction, Location = p.Location });
        }

        private void ObjectDashAttack(S.ObjectDashAttack p)
        {
            if (p.ObjectID == User.ObjectID) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;

                ob.JumpDistance = p.Distance;

                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.刺客冲击, Direction = p.Direction, Location = p.Location });

                return;
            }
        }

        private void UserAttackMove(S.UserAttackMove p)//Warrior Skill - SlashingBurst
        {
            MapControl.NextAction = 0;
            if (User.CurrentLocation == p.Location && User.Direction == p.Direction) return;


            MapControl.RemoveObject(User);
            User.CurrentLocation = p.Location;
            User.MapLocation = p.Location;
            MapControl.AddObject(User);


            MapControl.FloorValid = false;
            MapControl.InputDelay = CMain.Time + 400;


            if (User.Dead) return;


            User.ClearMagic();
            User.QueuedAction = null;


            for (int i = User.ActionFeed.Count - 1; i >= 0; i--)
            {
                if (User.ActionFeed[i].Action == MirAction.推开动作) continue;
                User.ActionFeed.RemoveAt(i);
            }


            User.SetAction();

            User.ActionFeed.Add(new QueuedAction { Action = MirAction.站立动作, Direction = p.Direction, Location = p.Location });
        }

        private void SetConcentration(S.SetConcentration p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                if (MapControl.Objects[i].Race != ObjectType.Player && MapControl.Objects[i].Race != ObjectType.Hero) continue;

                PlayerObject ob = MapControl.Objects[i] as PlayerObject;
                if (ob.ObjectID != p.ObjectID) continue;

                ob.Concentrating = p.Enabled;
                ob.ConcentrateInterrupted = p.Interrupted;

                if (p.Enabled && !p.Interrupted)
                {
                    int idx = InterruptionEffect.GetOwnerEffectID(ob.ObjectID);

                    if (idx < 0)
                    {
                        ob.Effects.Add(new InterruptionEffect(Libraries.Magic3, 1860, 8, 8 * 100, ob, true));
                        SoundManager.PlaySound(20000 + 129 * 10);
                    }
                }
                break;
            }
        }

        private void SetElemental(S.SetElemental p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                if (MapControl.Objects[i].Race != ObjectType.Player && MapControl.Objects[i].Race != ObjectType.Hero) continue;

                PlayerObject ob = MapControl.Objects[i] as PlayerObject;
                if (ob.ObjectID != p.ObjectID) continue;

                ob.HasElements = p.Enabled;
                ob.ElementCasted = p.Casted && User.ObjectID != p.ObjectID;
                ob.ElementsLevel = (int)p.Value;
                int elementType = (int)p.ElementType;
                int maxExp = (int)p.ExpLast;

                if (p.Enabled && p.ElementType > 0)
                {
                    ob.Effects.Add(new ElementsEffect(Libraries.Magic3, 1630 + ((elementType - 1) * 10), 10, 10 * 100, ob, true, 1 + (elementType - 1), maxExp, User.ObjectID == p.ObjectID && ((elementType == 4 || elementType == 3))));
                }
            }
        }

        private void RemoveDelayedExplosion(S.RemoveDelayedExplosion p)
        {
            //if (p.ObjectID == User.ObjectID) return;

            int effectid = DelayedExplosionEffect.GetOwnerEffectID(p.ObjectID);
            if (effectid >= 0)
                DelayedExplosionEffect.effectlist[effectid].Remove();
        }

        private void SetBindingShot(S.SetBindingShot p)
        {
            if (p.ObjectID == User.ObjectID) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                if (ob.Race != ObjectType.Monster) continue;

                TrackableEffect NetCast = new TrackableEffect(new Effect(Libraries.MagicC, 0, 8, 700, ob));
                NetCast.EffectName = "BindingShotDrop";

                //TrackableEffect NetDropped = new TrackableEffect(new Effect(Libraries.ArcherMagic, 7, 1, 1000, ob, CMain.Time + 600) { Repeat = true, RepeatUntil = CMain.Time + (p.Value - 1500) });
                TrackableEffect NetDropped = new TrackableEffect(new Effect(Libraries.MagicC, 7, 1, 1000, ob) { Repeat = true, RepeatUntil = CMain.Time + (p.Value - 1500) });
                NetDropped.EffectName = "BindingShotDown";

                TrackableEffect NetFall = new TrackableEffect(new Effect(Libraries.MagicC, 8, 8, 700, ob));
                NetFall.EffectName = "BindingShotFall";

                NetDropped.Complete += (o1, e1) =>
                {
                    SoundManager.PlaySound(20000 + 130 * 10 + 6);//sound M130-6
                    ob.Effects.Add(NetFall);
                };
                NetCast.Complete += (o, e) =>
                {
                    SoundManager.PlaySound(20000 + 130 * 10 + 5);//sound M130-5
                    ob.Effects.Add(NetDropped);
                };
                ob.Effects.Add(NetCast);
                break;
            }
        }

        private void SendOutputMessage(S.SendOutputMessage p)
        {
            OutputMessage(p.Message, p.Type);
        }

        private void NPCConsign()
        {
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Consign;
            NPCDropDialog.Show();
        }
        private void NPCMarket(S.NPCMarket p)
        {
            for (int i = 0; i < p.Listings.Count; i++)
                Bind(p.Listings[i].Item);

            TrustMerchantDialog.Show();
            TrustMerchantDialog.UserMode = p.UserMode;
            TrustMerchantDialog.Listings = p.Listings;
            TrustMerchantDialog.Page = 0;
            TrustMerchantDialog.PageCount = p.Pages;
            TrustMerchantDialog.UpdateInterface();
        }
        private void NPCMarketPage(S.NPCMarketPage p)
        {
            if (!TrustMerchantDialog.Visible) return;

            for (int i = 0; i < p.Listings.Count; i++)
                Bind(p.Listings[i].Item);

            TrustMerchantDialog.Listings.AddRange(p.Listings);
            TrustMerchantDialog.Page = (TrustMerchantDialog.Listings.Count - 1) / 10;
            TrustMerchantDialog.UpdateInterface();
        }
        private void ConsignItem(S.ConsignItem p)
        {
            MirItemCell cell = InventoryDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);

            if (cell == null) return;

            cell.Locked = false;

            if (!p.Success) return;

            cell.Item = null;

            User.RefreshStats();
        }
        private void MarketFail(S.MarketFail p)
        {
            TrustMerchantDialog.MarketTime = 0;
            switch (p.Reason)
            {
                case 0:
                    MirMessageBox.Show("死亡状态不能使用");
                    break;
                case 1:
                    MirMessageBox.Show("完成购买不支持信用币");
                    break;
                case 2:
                    MirMessageBox.Show("商品已售出");
                    break;
                case 3:
                    MirMessageBox.Show("物品将过期");
                    break;
                case 4:
                    MirMessageBox.Show(GameLanguage.LowGold);
                    break;
                case 5:
                    MirMessageBox.Show("负重不足不能完成购买");
                    break;
                case 6:
                    MirMessageBox.Show("不能购买自己的物品");
                    break;
                case 7:
                    MirMessageBox.Show("离信托商距离太远");
                    break;
                case 8:
                    MirMessageBox.Show("出售所需的佣金不足");
                    break;
                case 9:
                    MirMessageBox.Show("该商品未达到最低报价");
                    break;
                case 10:
                    MirMessageBox.Show("此物品的拍卖已结束");
                    break;
            }

        }
        private void MarketSuccess(S.MarketSuccess p)
        {
            TrustMerchantDialog.MarketTime = 0;
            MirMessageBox.Show(p.Message);
        }
        private void ObjectSitDown(S.ObjectSitDown p)
        {
            if (p.ObjectID == User.ObjectID) return;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;
                if (ob.Race != ObjectType.Monster) continue;
                ob.SitDown = p.Sitting;
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.坐下动作, Direction = p.Direction, Location = p.Location });
                return;
            }
        }

        private void BaseStatsInfo(S.BaseStatsInfo p)
        {
            User.CoreStats = p.Stats;
            User.RefreshStats();
        }

        private void HeroBaseStatsInfo(S.HeroBaseStatsInfo p)
        {
            if (Hero == null) return;

            Hero.CoreStats = p.Stats;
            Hero.RefreshStats();
        }

        private void UserName(S.UserName p)
        {
            for (int i = 0; i < UserIdList.Count; i++)
                if (UserIdList[i].Id == p.Id)
                {
                    UserIdList[i].UserName = p.Name;
                    break;
                }
            DisposeItemLabel();
            HoverItem = null;
        }

        private void ChatItemStats(S.ChatItemStats p)
        {
            //for (int i = 0; i < ChatItemList.Count; i++)
            //    if (ChatItemList[i].ID == p.ChatItemId)
            //    {
            //        ChatItemList[i].ItemStats = p.Stats;
            //        ChatItemList[i].RecievedTick = CMain.Time;
            //    }
        }

        private void GuildInvite(S.GuildInvite p)
        {
            MirMessageBox messageBox = new MirMessageBox(string.Format("你是否想加入 {0} 公会", p.Name), MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.GuildInvite { AcceptInvite = true });
            messageBox.NoButton.Click += (o, e) => Network.Enqueue(new C.GuildInvite { AcceptInvite = false });

            messageBox.Show();
        }

        private void GuildNameRequest(S.GuildNameRequest p)
        {
            MirInputBox inputBox = new MirInputBox("输入的行会名称长度最少为3个字符、中文等最多为7个字符、英文及数字为14个字符");

            inputBox.InputTextBox.TextBox.KeyPress += (o, e) =>
            {
                if (char.IsControl(e.KeyChar))
                {
                    return;
                }
                char inputChar = e.KeyChar;

                if (!char.IsLetterOrDigit(inputChar) && inputChar != '\u4E00' && inputChar != '\u9FA5')
                {
                    ChatDialog.ReceiveChat("字符不被认可", ChatType.System);
                    e.Handled = true;
                    return;
                }
                string newText = inputBox.InputTextBox.Text + e.KeyChar;

                int englishCount = Regex.Matches(newText, @"[A-Za-z0-9]").Count;

                bool containsNonEnglish = !Regex.IsMatch(newText, @"^[A-Za-z0-9]*$");

                if (newText.Length > 7 && containsNonEnglish)
                {
                    ChatDialog.ReceiveChat("不能超过7个字符", ChatType.System);
                    e.Handled = true;
                }
                else if (englishCount > 14)
                {
                    ChatDialog.ReceiveChat("不能超过14个字符", ChatType.System);
                    e.Handled = true;
                }
            };
            inputBox.OKButton.Click += (o, e) =>
            {
                if (inputBox.InputTextBox.Text.Contains('\\'))
                {
                    ChatDialog.ReceiveChat("不能使用 \\ 注册行会名称", ChatType.System);
                    inputBox.InputTextBox.Text = "";
                }
                Network.Enqueue(new C.GuildNameReturn { Name = inputBox.InputTextBox.Text });
                inputBox.Dispose();
            };
            inputBox.Show();
        }
        private void GuildRequestWar(S.GuildRequestWar p)
        {
            MirInputBox inputBox = new MirInputBox("加入想参战的行会");

            inputBox.OKButton.Click += (o, e) =>
            {
                Network.Enqueue(new C.GuildWarReturn { Name = inputBox.InputTextBox.Text });
                inputBox.Dispose();
            };
            inputBox.Show();
        }

        private void GuildNoticeChange(S.GuildNoticeChange p)
        {
            if (p.update == -1)
                GuildDialog.NoticeChanged = true;
            else
                GuildDialog.NoticeChange(p.notice);
        }
        private void GuildMemberChange(S.GuildMemberChange p)
        {
            switch (p.Status)
            {
                case 0: // logged of
                    GuildDialog.MemberStatusChange(p.Name, false);
                    break;
                case 1: // logged on
                    ChatDialog.ReceiveChat(String.Format("{0} 已登录游戏", p.Name), ChatType.Guild);
                    GuildDialog.MemberStatusChange(p.Name, true);
                    break;
                case 2://new member
                    ChatDialog.ReceiveChat(String.Format("{0} 加入行会", p.Name), ChatType.Guild);
                    GuildDialog.MemberCount++;
                    GuildDialog.MembersChanged = true;
                    break;
                case 3://kicked member
                    ChatDialog.ReceiveChat(String.Format("{0} 被行会除名", p.Name), ChatType.Guild);
                    GuildDialog.MembersChanged = true;
                    break;
                case 4://member left
                    ChatDialog.ReceiveChat(String.Format("{0} 离开了行会", p.Name), ChatType.Guild);
                    GuildDialog.MembersChanged = true;
                    break;
                case 5://rank change (name or different rank)
                    GuildDialog.MembersChanged = true;
                    break;
                case 6: //new rank
                    if (p.Ranks.Count > 0)
                        GuildDialog.NewRankRecieved(p.Ranks[0]);
                    break;
                case 7: //rank option changed
                    if (p.Ranks.Count > 0)
                        GuildDialog.RankChangeRecieved(p.Ranks[0]);
                    break;
                case 8: //my rank changed
                    if (p.Ranks.Count > 0)
                        GuildDialog.MyRankChanged(p.Ranks[0]);
                    break;
                case 255:
                    GuildDialog.NewMembersList(p.Ranks);
                    break;
            }
        }

        private void GuildStatus(S.GuildStatus p)
        {
            if ((User.GuildName == "") && (p.GuildName != ""))
            {
                GuildDialog.NoticeChanged = true;
                GuildDialog.MembersChanged = true;
            }
            if (p.GuildName == "")
            {
                GuildDialog.Hide();
            }

            if ((User.GuildName == p.GuildName) && (GuildDialog.Level < p.Level))
            {
                //guild leveled
            }
            bool GuildChange = User.GuildName != p.GuildName;
            User.GuildName = p.GuildName;
            User.GuildRankName = p.GuildRankName;
            GuildDialog.Level = p.Level;
            GuildDialog.Experience = p.Experience;
            GuildDialog.MaxExperience = p.MaxExperience;
            GuildDialog.Gold = p.Gold;
            GuildDialog.SparePoints = p.SparePoints;
            GuildDialog.MemberCount = p.MemberCount;
            GuildDialog.MaxMembers = p.MaxMembers;
            GuildDialog.Voting = p.Voting;
            GuildDialog.ItemCount = p.ItemCount;
            GuildDialog.BuffCount = p.BuffCount;
            GuildDialog.StatusChanged(p.MyOptions);
            GuildDialog.MyRankId = p.MyRankId;
            GuildDialog.UpdateMembers();
            //reset guildbuffs
            if (GuildChange)
            {
                GuildDialog.EnabledBuffs.Clear();
                GuildDialog.UpdateActiveStats();
                RemoveBuff(new S.RemoveBuff { ObjectID = User.ObjectID, Type = BuffType.公会特效 });
                User.RefreshStats();
            }
        }

        private void GuildExpGain(S.GuildExpGain p)
        {
            //OutputMessage(string.Format("Guild Experience Gained {0}.", p.Amount));
            GuildDialog.Experience += p.Amount;
        }

        private void GuildStorageGoldChange(S.GuildStorageGoldChange p)
        {
            switch (p.Type)
            {
                case 0:
                    ChatDialog.ReceiveChat(String.Format("{0} 给行会捐赠 {1} 金币", p.Name, p.Amount), ChatType.Guild);
                    GuildDialog.Gold += p.Amount;
                    break;
                case 1:
                    ChatDialog.ReceiveChat(String.Format("{0} 从行会取走 {1} 金币", p.Name, p.Amount), ChatType.Guild);
                    if (GuildDialog.Gold > p.Amount)
                        GuildDialog.Gold -= p.Amount;
                    else
                        GuildDialog.Gold = 0;
                    break;
                case 2:
                    if (GuildDialog.Gold > p.Amount)
                        GuildDialog.Gold -= p.Amount;
                    else
                        GuildDialog.Gold = 0;
                    break;
                case 3:
                    GuildDialog.Gold += p.Amount;
                    break;
            }
        }

        private void GuildStorageItemChange(S.GuildStorageItemChange p)
        {
            MirItemCell fromCell = null;
            MirItemCell toCell = null;
            switch (p.Type)
            {
                case 0://store
                    toCell = GuildDialog.StorageGrid[p.To];

                    if (toCell == null) return;

                    toCell.Locked = false;
                    toCell.Item = p.Item.Item;
                    Bind(toCell.Item);
                    if (p.User != User.Id) return;
                    fromCell = p.From < User.BeltIdx ? BeltDialog.Grid[p.From] : InventoryDialog.Grid[p.From - User.BeltIdx];
                    fromCell.Locked = false;
                    if (fromCell != null)
                        fromCell.Item = null;
                    User.RefreshStats();
                    break;
                case 1://retrieve
                    fromCell = GuildDialog.StorageGrid[p.From];

                    if (fromCell == null) return;
                    fromCell.Locked = false;

                    if (p.User != User.Id)
                    {
                        fromCell.Item = null;
                        return;
                    }
                    toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];
                    if (toCell == null) return;
                    toCell.Locked = false;
                    toCell.Item = fromCell.Item;
                    fromCell.Item = null;
                    break;

                case 2:
                    toCell = GuildDialog.StorageGrid[p.To];
                    fromCell = GuildDialog.StorageGrid[p.From];

                    if (toCell == null || fromCell == null) return;

                    toCell.Locked = false;
                    fromCell.Locked = false;
                    fromCell.Item = toCell.Item;
                    toCell.Item = p.Item.Item;
                    
                    Bind(toCell.Item);
                    if (fromCell.Item != null) 
                        Bind(fromCell.Item);
                    break;
                case 3://failstore
                    fromCell = p.From < User.BeltIdx ? BeltDialog.Grid[p.From] : InventoryDialog.Grid[p.From - User.BeltIdx];
                    toCell = GuildDialog.StorageGrid[p.To];

                    if (toCell == null || fromCell == null) return;

                    toCell.Locked = false;
                    fromCell.Locked = false;
                    break;
                case 4://failretrieve
                    toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];
                    fromCell = GuildDialog.StorageGrid[p.From];

                    if (toCell == null || fromCell == null) return;

                    toCell.Locked = false;
                    fromCell.Locked = false;
                    break;
                case 5://failmove
                    fromCell = GuildDialog.StorageGrid[p.To];
                    toCell = GuildDialog.StorageGrid[p.From];

                    if (toCell == null || fromCell == null) return;

                    GuildDialog.StorageGrid[p.From].Locked = false;
                    GuildDialog.StorageGrid[p.To].Locked = false;
                    break;
            }
        }
        private void GuildStorageList(S.GuildStorageList p)
        {
            for (int i = 0; i < p.Items.Length; i++)
            {
                if (i >= GuildDialog.StorageGrid.Length) break;
                if (p.Items[i] == null)
                {
                    GuildDialog.StorageGrid[i].Item = null;
                    continue;
                }
                GuildDialog.StorageGrid[i].Item = p.Items[i].Item;
                Bind(GuildDialog.StorageGrid[i].Item);
            }
        }

        private void HeroCreateRequest(S.HeroCreateRequest p)
        {            
            NewHeroDialog.WarriorButton.Visible = p.CanCreateClass[(int)MirClass.战士];
            NewHeroDialog.WizardButton.Visible = p.CanCreateClass[(int)MirClass.法师];
            NewHeroDialog.TaoistButton.Visible = p.CanCreateClass[(int)MirClass.道士];
            NewHeroDialog.AssassinButton.Visible = p.CanCreateClass[(int)MirClass.刺客];
            NewHeroDialog.ArcherButton.Visible = p.CanCreateClass[(int)MirClass.弓箭];

            NewHeroDialog.Show();            
        }

        private void ManageHeroes(S.ManageHeroes p)
        {
            if (p.Heroes != null)
            {
                for (int i = 0; i < p.Heroes.Length; i++)
                    AddHeroInformation(p.Heroes[i], i);
            }

            MaximumHeroCount = p.MaximumCount;
            HeroManageDialog.SetCurrentHero(p.CurrentHero);
            HeroManageDialog.Show();
        }

        private void ChangeHero(S.ChangeHero p)
        {
            ClientHeroInformation temp = HeroStorage[p.FromIndex];
            HeroStorage[p.FromIndex] = HeroManageDialog.CurrentAvatar.Info;
            HeroManageDialog.SetCurrentHero(temp);
            HeroManageDialog.RefreshInterface();
        }

        public int HeroAvatar(MirClass job, MirGender gender) => 1400 + (byte)job + 10 * (byte)gender;

        private void UnlockHeroAutoPot(bool value)
        {
            if (Hero == null) return;

            Hero.AutoPot = value;
            HeroInventoryDialog.RefreshInterface();
        }

        private void SetAutoPotValue(S.SetAutoPotValue p)
        {
            if (Hero == null) return;

            if (p.Stat == Stat.HP)
                Hero.AutoHPPercent = p.Value;
            else
                Hero.AutoMPPercent = p.Value;

            HeroInventoryDialog.RefreshInterface();
        }

        private void SetAutoPotItem(S.SetAutoPotItem p)
        {
            if (Hero == null) return;

            if (p.Grid == MirGridType.HeroHPItem)
                Hero.HPItem[0] = p.ItemIndex > 0 ? new UserItem(GetItemInfo(p.ItemIndex)) : null;
            else
                Hero.MPItem[0] = p.ItemIndex > 0 ? new UserItem(GetItemInfo(p.ItemIndex)) : null;
        }

        private void SetHeroBehaviour(S.SetHeroBehaviour p)
        {
            if (Hero == null) return;
            HeroBehaviourPanel.UpdateBehaviour(p.Behaviour);
            HeroAIDialog.UpdateBehaviour(p.Behaviour);
        }

        private void NewHero(S.NewHero p)
        {
            NewHeroDialog.OKButton.Enabled = true;

            switch (p.Result)
            {
                case 0:
                    MirMessageBox.Show("召唤英雄处于禁用状态");
                    NewHeroDialog.Hide();
                    break;
                case 1:
                    MirMessageBox.Show("所使用的英雄名称不可用");
                    NewHeroDialog.NameTextBox.SetFocus();
                    break;
                case 2:
                    MirMessageBox.Show("英雄性别错误\n 请联系游戏管理员");
                    break;
                case 3:
                    MirMessageBox.Show("英雄职业错误\n 请联系游戏管理员");
                    break;
                case 4:
                    MirMessageBox.Show("不能再召唤新的英雄");
                    NewHeroDialog.Hide();
                    break;
                case 5:
                    MirMessageBox.Show("名称已注册不能再次使用");
                    NewHeroDialog.NameTextBox.SetFocus();
                    break;
                case 6:
                    MirMessageBox.Show("背包空间不足");
                    NewHeroDialog.Hide();
                    break;
                case 10:
                    MirMessageBox.Show("成功召唤英雄");
                    NewHeroDialog.Hide();
                    break;
            }
        }

        private void HeroInformation(S.HeroInformation p)
        {
            Hero = new UserHeroObject(p.ObjectID);
            Hero.Load(p);

            Hero.AutoPot = p.AutoPot;
            Hero.AutoHPPercent = p.AutoHPPercent;
            Hero.AutoMPPercent = p.AutoMPPercent;

            if (p.HPItemIndex > 0)
                Hero.HPItem[0] = new UserItem(GetItemInfo(p.HPItemIndex));
            if (p.MPItemIndex > 0)
                Hero.MPItem[0] = new UserItem(GetItemInfo(p.MPItemIndex));

            HeroDialog = new CharacterDialog(MirGridType.HeroEquipment, Hero) { Parent = this, Visible = false };
            HeroInventoryDialog = new HeroInventoryDialog { Parent = this };
            HeroBeltDialog = new HeroBeltDialog { Parent = this };
            HeroBuffsDialog = new BuffDialog
            {
                Parent = this,
                Visible = true,
                Location = new Point(Settings.ScreenWidth - 170, 80),
                GetExpandedParameter = () => { return Settings.ExpandedHeroBuffWindow; },
                SetExpandedParameter = (value) => { Settings.ExpandedHeroBuffWindow = value; }
            };
            MainDialog.HeroInfoPanel.Update();

            Hero.RefreshStats();
        }

        private void UpdateHeroSpawnState(S.UpdateHeroSpawnState p)
        {
            HeroSpawnState = p.State;

            HasHero = p.State > HeroSpawnState.None;
            MainDialog.HeroInfoPanel.Visible = p.State > HeroSpawnState.Unsummoned;
            MainDialog.HeroMenuButton.Visible = p.State > HeroSpawnState.Unsummoned;
            HeroBehaviourPanel.Visible = p.State > HeroSpawnState.Unsummoned;            
            HeroAIDialog.Visible = p.State > HeroSpawnState.Unsummoned;    
            HeroMenuPanel.Visible = HeroMenuPanel.Visible && MainDialog.HeroMenuButton.Visible;

            if (p.State < HeroSpawnState.Summoned)
            {
                HeroInventoryDialog.Dispose();
                HeroDialog.Dispose();
                HeroBeltDialog.Dispose();
                HeroBuffsDialog.Dispose();
            }
        }

        private void MarriageRequest(S.MarriageRequest p)
        {
            MirMessageBox messageBox = new MirMessageBox(string.Format("{0} 向你求婚", p.Name), MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.MarriageReply { AcceptInvite = true });
            messageBox.NoButton.Click += (o, e) => { Network.Enqueue(new C.MarriageReply { AcceptInvite = false }); messageBox.Dispose(); };

            messageBox.Show();
        }

        private void DivorceRequest(S.DivorceRequest p)
        {
            MirMessageBox messageBox = new MirMessageBox(string.Format("{0} 要求离婚", p.Name), MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.DivorceReply { AcceptInvite = true });
            messageBox.NoButton.Click += (o, e) => { Network.Enqueue(new C.DivorceReply { AcceptInvite = false }); messageBox.Dispose(); };

            messageBox.Show();
        }

        private void MentorRequest(S.MentorRequest p)
        {
            MirMessageBox messageBox = new MirMessageBox(string.Format("玩家:{0}(等级 {1}){2} 请求拜师是否同意？", p.Name, p.Level, GameScene.User.Class.ToString()), MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.MentorReply { AcceptInvite = true });
            messageBox.NoButton.Click += (o, e) => { Network.Enqueue(new C.MentorReply { AcceptInvite = false }); messageBox.Dispose(); };

            messageBox.Show();
        }

        private bool UpdateGuildBuff(GuildBuff buff, bool Remove = false)
        {
            for (int i = 0; i < GuildDialog.EnabledBuffs.Count; i++)
            {
                if (GuildDialog.EnabledBuffs[i].Id == buff.Id)
                {
                    if (Remove)
                    {
                        GuildDialog.EnabledBuffs.RemoveAt(i);
                    }
                    else
                        GuildDialog.EnabledBuffs[i] = buff;
                    return true;
                }
            }
            return false;
        }

        private void GuildBuffList(S.GuildBuffList p)
        {
            //getting the list of all guildbuffs on server?
            if (p.GuildBuffs.Count > 0)
                GuildDialog.GuildBuffInfos.Clear();
            for (int i = 0; i < p.GuildBuffs.Count; i++)
            {
                GuildDialog.GuildBuffInfos.Add(p.GuildBuffs[i]);
            }
            //getting the list of all active/removedbuffs?
            for (int i = 0; i < p.ActiveBuffs.Count; i++)
            {
                //if (p.ActiveBuffs[i].ActiveTimeRemaining > 0)
                //    p.ActiveBuffs[i].ActiveTimeRemaining = Convert.ToInt32(CMain.Time / 1000) + (p.ActiveBuffs[i].ActiveTimeRemaining * 60);
                if (UpdateGuildBuff(p.ActiveBuffs[i], p.Remove == 1)) continue;
                if (!(p.Remove == 1))
                {
                    GuildDialog.EnabledBuffs.Add(p.ActiveBuffs[i]);
                    //CreateGuildBuff(p.ActiveBuffs[i]);
                }
            }

            for (int i = 0; i < GuildDialog.EnabledBuffs.Count; i++)
            {
                if (GuildDialog.EnabledBuffs[i].Info == null)
                {
                    GuildDialog.EnabledBuffs[i].Info = GuildDialog.FindGuildBuffInfo(GuildDialog.EnabledBuffs[i].Id);
                }
            }

            ClientBuff buff = BuffsDialog.Buffs.FirstOrDefault(e => e.Type == BuffType.公会特效);

            if (GuildDialog.EnabledBuffs.Any(e => e.Active))
            {
                if (buff == null)
                {
                    buff = new ClientBuff { Type = BuffType.公会特效, ObjectID = User.ObjectID, Caster = "公会", Infinite = true, Values = new int[0] };

                    BuffsDialog.Buffs.Add(buff);
                    BuffsDialog.CreateBuff(buff);
                }

                GuildDialog.UpdateActiveStats();
            }
            else
            {
                RemoveBuff(new S.RemoveBuff { ObjectID = User.ObjectID, Type = BuffType.公会特效 });
            }

            User.RefreshStats();
        }

        private void TradeRequest(S.TradeRequest p)
        {
            MirMessageBox messageBox = new MirMessageBox(string.Format("玩家 {0} 要求与你交易", p.Name), MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.TradeReply { AcceptInvite = true });
            messageBox.NoButton.Click += (o, e) => { Network.Enqueue(new C.TradeReply { AcceptInvite = false }); messageBox.Dispose(); };

            messageBox.Show();
        }
        private void TradeAccept(S.TradeAccept p)
        {
            GuestTradeDialog.GuestName = p.Name;
            TradeDialog.TradeAccept();
        }
        private void TradeGold(S.TradeGold p)
        {
            GuestTradeDialog.GuestGold = p.Amount;
            TradeDialog.ChangeLockState(false);
            TradeDialog.RefreshInterface();
        }
        private void TradeItem(S.TradeItem p)
        {
            GuestTradeDialog.GuestItems = p.TradeItems;
            TradeDialog.ChangeLockState(false);
            TradeDialog.RefreshInterface();
        }
        private void TradeConfirm()
        {
            TradeDialog.TradeReset();
        }
        private void TradeCancel(S.TradeCancel p)
        {
            if (p.Unlock)
            {
                TradeDialog.ChangeLockState(false);
            }
            else
            {
                TradeDialog.TradeReset();

                MirMessageBox messageBox = new MirMessageBox("交易取消\r\n要完成交易必须面对对方", MirMessageBoxButtons.OK);
                messageBox.Show();
            }
        }
        private void NPCAwakening()
        {
            if (NPCAwakeDialog.Visible != true)
                NPCAwakeDialog.Show();
        }
        private void NPCDisassemble()
        {
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Disassemble;
            NPCDropDialog.Show();
        }
        private void NPCDowngrade()
        {
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Downgrade;
            NPCDropDialog.Show();
        }
        private void NPCReset()
        {
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Reset;
            NPCDropDialog.Show();
        }
        private void AwakeningNeedMaterials(S.AwakeningNeedMaterials p)
        {
            NPCAwakeDialog.setNeedItems(p.Materials, p.MaterialsCount);
        }
        private void AwakeningLockedItem(S.AwakeningLockedItem p)
        {
            MirItemCell cell = InventoryDialog.GetCell(p.UniqueID);
            if (cell != null)
                cell.Locked = p.Locked;
        }
        private void Awakening(S.Awakening p)
        {
            if (NPCAwakeDialog.Visible)
                NPCAwakeDialog.Hide();
            if (InventoryDialog.Visible)
                InventoryDialog.Hide();

            MirItemCell cell = InventoryDialog.GetCell((ulong)p.removeID);
            if (cell != null)
            {
                cell.Locked = false;
                cell.Item = null;
            }

            for (int i = 0; i < InventoryDialog.Grid.Length; i++)
            {
                if (InventoryDialog.Grid[i].Locked == true)
                {
                    InventoryDialog.Grid[i].Locked = false;

                    //if (InventoryDialog.Grid[i].Item.UniqueID == (ulong)p.removeID)
                    //{
                    //    InventoryDialog.Grid[i].Item = null;
                    //}
                }
            }

            for (int i = 0; i < NPCAwakeDialog.ItemsIdx.Length; i++)
            {
                NPCAwakeDialog.ItemsIdx[i] = 0;
            }

            MirMessageBox messageBox = null;

            switch (p.result)
            {
                case -4:
                    messageBox = new MirMessageBox("没有足够的材料", MirMessageBoxButtons.OK);
                    MapControl.AwakeningAction = false;
                    break;
                case -3:
                    messageBox = new MirMessageBox(GameLanguage.LowGold, MirMessageBoxButtons.OK);
                    MapControl.AwakeningAction = false;
                    break;
                case -2:
                    messageBox = new MirMessageBox("觉醒已达上限", MirMessageBoxButtons.OK);
                    MapControl.AwakeningAction = false;
                    break;
                case -1:
                    messageBox = new MirMessageBox("此物品不能觉醒", MirMessageBoxButtons.OK);
                    MapControl.AwakeningAction = false;
                    break;
                case 0:
                    //messageBox = new MirMessageBox("Upgrade Failed.", MirMessageBoxButtons.OK);
                    break;
                case 1:
                    //messageBox = new MirMessageBox("Upgrade Success.", MirMessageBoxButtons.OK);
                    break;

            }

            if (messageBox != null) messageBox.Show();
        }

        private void ReceiveMail(S.ReceiveMail p)
        {
            NewMail = false;
            NewMailCounter = 0;
            User.Mail.Clear();

            User.Mail = p.Mail.OrderByDescending(e => !e.Locked).ThenByDescending(e => e.DateSent).ToList();

            foreach(ClientMail mail in User.Mail)
            {
                foreach(UserItem itm in mail.Items)
                {
                    Bind(itm);
                }
            }

            //display new mail received
            if (User.Mail.Any(e => e.Opened == false))
            {
                NewMail = true;
            }

            GameScene.Scene.MailListDialog.UpdateInterface();
        }

        private void MailLockedItem(S.MailLockedItem p)
        {
            MirItemCell cell = InventoryDialog.GetCell(p.UniqueID);
            if (cell != null)
                cell.Locked = p.Locked;
        }

        private void MailSendRequest(S.MailSendRequest p)
        {
            MirInputBox inputBox = new MirInputBox("输入要邮寄的人的姓名");

            inputBox.OKButton.Click += (o1, e1) =>
            {
                GameScene.Scene.MailComposeParcelDialog.ComposeMail(inputBox.InputTextBox.Text);
                GameScene.Scene.InventoryDialog.Show();

                //open letter dialog, pass in name
                inputBox.Dispose();
            };

            inputBox.Show();
        }

        private void MailSent(S.MailSent p)
        {
            for (int i = 0; i < InventoryDialog.Grid.Length; i++)
            {
                if (InventoryDialog.Grid[i].Locked == true)
                {
                    InventoryDialog.Grid[i].Locked = false;
                }
            }

            for (int i = 0; i < BeltDialog.Grid.Length; i++)
            {
                if (BeltDialog.Grid[i].Locked == true)
                {
                    BeltDialog.Grid[i].Locked = false;
                }
            }

            GameScene.Scene.MailComposeParcelDialog.Hide();
        }

        private void ParcelCollected(S.ParcelCollected p)
        {
            switch(p.Result)
            {
                case -1:
                    MirMessageBox messageBox = new MirMessageBox(string.Format("没有邮件"), MirMessageBoxButtons.OK);
                    messageBox.Show();
                    break;
                case 0:
                    messageBox = new MirMessageBox(string.Format("邮件已接收"), MirMessageBoxButtons.OK);
                    messageBox.Show();
                    break;
                case 1:
                    GameScene.Scene.MailReadParcelDialog.Hide();
                    break;
            }
        }

        private void ResizeInventory(S.ResizeInventory p)
        {
            Array.Resize(ref User.Inventory, p.Size);
            InventoryDialog.RefreshInventory2();
        }

        private void ResizeStorage(S.ResizeStorage p)
        {
            Array.Resize(ref Storage, p.Size);
            User.HasExpandedStorage = p.HasExpandedStorage;
            User.ExpandedStorageExpiryTime = p.ExpiryTime;

            StorageDialog.RefreshStorage2();
        }

        private void MailCost(S.MailCost p)
        {
            if(GameScene.Scene.MailComposeParcelDialog.Visible)
            {
                if (p.Cost > 0)
                    SoundManager.PlaySound(SoundList.Gold);

                GameScene.Scene.MailComposeParcelDialog.ParcelCostLabel.Text = p.Cost.ToString();
            }
        }

        public void AddQuestItem(UserItem item)
        {
            Redraw();

            if (item.Info.StackSize > 1) //Stackable
            {
                for (int i = 0; i < User.QuestInventory.Length; i++)
                {
                    UserItem temp = User.QuestInventory[i];
                    if (temp == null || item.Info != temp.Info || temp.Count >= temp.Info.StackSize) continue;

                    if (item.Count + temp.Count <= temp.Info.StackSize)
                    {
                        temp.Count += item.Count;
                        return;
                    }
                    item.Count -= (ushort)(temp.Info.StackSize - temp.Count);
                    temp.Count = temp.Info.StackSize;
                }
            }

            for (int i = 0; i < User.QuestInventory.Length; i++)
            {
                if (User.QuestInventory[i] != null) continue;
                User.QuestInventory[i] = item;
                return;
            }
        }

        private void RequestReincarnation()
        {
            if (CMain.Time > User.DeadTime && User.CurrentAction == MirAction.死后尸体)
            {
                MirMessageBox messageBox = new MirMessageBox("是否接受玩家对你的复活", MirMessageBoxButtons.YesNo);

                messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.AcceptReincarnation());

                messageBox.Show();
            }
        }

        private void NewIntelligentCreature(S.NewIntelligentCreature p)
        {
            User.IntelligentCreatures.Add(p.Creature);

            MirInputBox inputBox = new MirInputBox("给宠物起个名字");
            inputBox.InputTextBox.Text = GameScene.User.IntelligentCreatures[User.IntelligentCreatures.Count-1].CustomName;
            inputBox.OKButton.Click += (o1, e1) =>
            {
                if (IntelligentCreatureDialog.Visible) IntelligentCreatureDialog.Update();//refresh changes
                GameScene.User.IntelligentCreatures[User.IntelligentCreatures.Count - 1].CustomName = inputBox.InputTextBox.Text;
                Network.Enqueue(new C.UpdateIntelligentCreature { Creature = GameScene.User.IntelligentCreatures[User.IntelligentCreatures.Count - 1] });
                inputBox.Dispose();
            };
            inputBox.Show();
        }

        private void UpdateIntelligentCreatureList(S.UpdateIntelligentCreatureList p)
        {
            User.CreatureSummoned = p.CreatureSummoned;
            User.SummonedCreatureType = p.SummonedCreatureType;
            User.PearlCount = p.PearlCount;
            if (p.CreatureList.Count != User.IntelligentCreatures.Count)
            {
                User.IntelligentCreatures.Clear();
                for (int i = 0; i < p.CreatureList.Count; i++)
                    User.IntelligentCreatures.Add(p.CreatureList[i]);

                for (int i = 0; i < IntelligentCreatureDialog.CreatureButtons.Length; i++)
                    IntelligentCreatureDialog.CreatureButtons[i].Clear();

                IntelligentCreatureDialog.Hide();
            }
            else
            {
                for (int i = 0; i < p.CreatureList.Count; i++)
                    User.IntelligentCreatures[i] = p.CreatureList[i];
                if (IntelligentCreatureDialog.Visible) IntelligentCreatureDialog.Update();
            }
        }

        private void IntelligentCreatureEnableRename(S.IntelligentCreatureEnableRename p)
        {
            IntelligentCreatureDialog.CreatureRenameButton.Visible = true;
            if (IntelligentCreatureDialog.Visible) IntelligentCreatureDialog.Update();
        }

        private void IntelligentCreaturePickup(S.IntelligentCreaturePickup p)
        {
            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != p.ObjectID) continue;

                MonsterObject monOb = (MonsterObject)ob;

                if (monOb != null) monOb.PlayPickupSound();
            }
        }

        private void FriendUpdate(S.FriendUpdate p)
        {
            GameScene.Scene.FriendDialog.Friends = p.Friends;

            if (GameScene.Scene.FriendDialog.Visible)
            {
                GameScene.Scene.FriendDialog.Update(false);
            }
        }

        private void LoverUpdate(S.LoverUpdate p)
        {
            GameScene.Scene.RelationshipDialog.LoverName = p.Name;
            GameScene.Scene.RelationshipDialog.Date = p.Date;
            GameScene.Scene.RelationshipDialog.MapName = p.MapName;
            GameScene.Scene.RelationshipDialog.MarriedDays = p.MarriedDays;
            GameScene.Scene.RelationshipDialog.UpdateInterface();
        }

        private void MentorUpdate(S.MentorUpdate p)
        {
            GameScene.Scene.MentorDialog.MentorName = p.Name;
            GameScene.Scene.MentorDialog.MentorLevel = p.Level;
            GameScene.Scene.MentorDialog.MentorOnline = p.Online;
            GameScene.Scene.MentorDialog.MenteeEXP = p.MenteeEXP;

            GameScene.Scene.MentorDialog.UpdateInterface();
        }

        private void GameShopUpdate(S.GameShopInfo p)
        {
            p.Item.Stock = p.StockLevel;
            GameShopInfoList.Add(p.Item);
            if (p.Item.Date > CMain.Now.AddDays(-7)) GameShopDialog.New.Visible = true;
        }

        private void GameShopStock(S.GameShopStock p)
        {
            for (int i = 0; i < GameShopInfoList.Count; i++)
            {
                if (GameShopInfoList[i].GIndex == p.GIndex)
                    {
                    if (p.StockLevel == 0) GameShopInfoList.Remove(GameShopInfoList[i]);
                    else GameShopInfoList[i].Stock = p.StockLevel;

                    if (GameShopDialog.Visible) GameShopDialog.UpdateShop();
                    }
            }
        }
        public void AddItem(UserItem item)
        {
            Redraw();

            if (item.Info.StackSize > 1) //Stackable
            {
                for (int i = 0; i < User.Inventory.Length; i++)
                {
                    UserItem temp = User.Inventory[i];
                    if (temp == null || item.Info != temp.Info || temp.Count >= temp.Info.StackSize) continue;

                    if (item.Count + temp.Count <= temp.Info.StackSize)
                    {
                        temp.Count += item.Count;
                        return;
                    }
                    item.Count -= (ushort)(temp.Info.StackSize - temp.Count);
                    temp.Count = temp.Info.StackSize;
                }
            }

            if (item.Info.Type == ItemType.药水 || item.Info.Type == ItemType.卷轴 || (item.Info.Type == ItemType.特殊消耗品 && item.Info.Effect == 1))
            {
                for (int i = 0; i < User.BeltIdx - 2; i++)
                {
                    if (User.Inventory[i] != null) continue;
                    User.Inventory[i] = item;
                    return;
                }
            }
            else if (item.Info.Type == ItemType.护身符)
            {
                for (int i = 4; i < User.BeltIdx; i++)
                {
                    if (User.Inventory[i] != null) continue;
                    User.Inventory[i] = item;
                    return;
                }
            }
            else
            {
                for (int i = User.BeltIdx; i < User.Inventory.Length; i++)
                {
                    if (User.Inventory[i] != null) continue;
                    User.Inventory[i] = item;
                    return;
                }
            }

            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null) continue;
                User.Inventory[i] = item;
                return;
            }
        }
        public static void Bind(UserItem item)
        {
            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                if (ItemInfoList[i].Index != item.ItemIndex) continue;

                item.Info = ItemInfoList[i];

                for (int s = 0; s < item.Slots.Length; s++)
                {
                    if (item.Slots[s] == null) continue;

                    Bind(item.Slots[s]);
                }

                return;
            }
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

        public static void BindQuest(ClientQuestProgress quest)
        {
            for (int i = 0; i < QuestInfoList.Count; i++)
            {
                if (QuestInfoList[i].Index != quest.Id) continue;

                quest.QuestInfo = QuestInfoList[i];

                return;
            }
        }

        public Color GradeNameColor(ItemGrade grade)
        {
            switch (grade)
            {
                case ItemGrade.普通:
                    return Color.Yellow;
                case ItemGrade.宝物:
                    return Color.DeepSkyBlue;
                case ItemGrade.圣物:
                    return Color.DarkOrange;
                case ItemGrade.神物:
                    return Color.Plum;
                case ItemGrade.英雄:
                    return Color.Red;
                default:
                    return Color.Yellow;
            }
        }

        public void DisposeItemLabel()
        {
            if (ItemLabel != null && !ItemLabel.IsDisposed)
                ItemLabel.Dispose();
            ItemLabel = null;
        }
        public void DisposeMailLabel()
        {
            if (MailLabel != null && !MailLabel.IsDisposed)
                MailLabel.Dispose();
            MailLabel = null;
        }
        public void DisposeMemoLabel()
        {
            if (MemoLabel != null && !MemoLabel.IsDisposed)
                MemoLabel.Dispose();
            MemoLabel = null;
        }
        public void DisposeGuildBuffLabel()
        {
            if (GuildBuffLabel != null && !GuildBuffLabel.IsDisposed)
                GuildBuffLabel.Dispose();
            GuildBuffLabel = null;
        }

        public MirControl NameInfoLabel(UserItem item, bool inspect = false, bool hideDura = false)
        {
            ushort level = inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfo realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            string GradeString = "";
            switch (HoverItem.Info.Grade)
            {
                case ItemGrade.None:                   
                    break;
                case ItemGrade.普通:
                    GradeString = GameLanguage.ItemGradeCommon;
                    break;
                case ItemGrade.宝物:
                    GradeString = GameLanguage.ItemGradeRare;
                    break;
                case ItemGrade.圣物:
                    GradeString = GameLanguage.ItemGradeLegendary;
                    break;
                case ItemGrade.神物:
                    GradeString = GameLanguage.ItemGradeMythical;
                    break;
                case ItemGrade.英雄:
                    GradeString = GameLanguage.ItemGradeHeroic;
                    break;
            }
            MirLabel nameLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = GradeNameColor(HoverItem.Info.Grade),
                Location = new Point(4, 4),
                OutLine = true,
                Parent = ItemLabel,
                Text = HoverItem.Info.Grade != ItemGrade.None ? string.Format("{0}{1}{2}", HoverItem.Info.FriendlyName, "\n", GradeString) : HoverItem.Info.FriendlyName,
            };

            if (HoverItem.RefineAdded > 0)
                nameLabel.Text = "(*)" + nameLabel.Text;

            ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, nameLabel.DisplayRectangle.Right + 4),
                Math.Max(ItemLabel.Size.Height, nameLabel.DisplayRectangle.Bottom));

            string text = "";

            if (HoverItem.Info.Durability > 0 && !hideDura)
            {
                switch (HoverItem.Info.Type)
                {
                    case ItemType.护身符:
                        text += string.Format(" 护符数 {0}/{1}", HoverItem.CurrentDura, HoverItem.MaxDura);
                        break;
                    case ItemType.矿石:
                        text += string.Format(" 纯度 {0}", Math.Floor(HoverItem.CurrentDura / 1000M));
                        break;
                    case ItemType.肉:
                        text += string.Format(" 品质 {0}", Math.Floor(HoverItem.CurrentDura / 1000M));
                        break;
                    case ItemType.坐骑:
                        text += string.Format(" 忠诚度 {0} / {1}", HoverItem.CurrentDura, HoverItem.MaxDura);
                        break;
                    case ItemType.坐骑食物:
                        text += string.Format(" 忠诚度恢复 {0}", HoverItem.CurrentDura);
                        break;
                    case ItemType.宝玉神珠:
                    case ItemType.药水:
                    case ItemType.外形物品:
                    case ItemType.封印:
                        break;
                    case ItemType.灵物:
                        if (HoverItem.Info.Shape == 26 || HoverItem.Info.Shape == 28)//WonderDrug, Knapsack
                        {
                            string strTime = Functions.PrintTimeSpanFromSeconds((HoverItem.CurrentDura * 60), false);
                            text += string.Format("\n持续时间 {0}", strTime);
                        }
                        break;
                    default:
                        text += string.Format(" {0} {1}/{2}", GameLanguage.Durability, Math.Floor(HoverItem.CurrentDura / 1000M),
                                                   Math.Floor(HoverItem.MaxDura / 1000M));
                        break;
                }
            }

            string baseText = "";
            switch (HoverItem.Info.Type)
            {
                case ItemType.杂物:
                    baseText = GameLanguage.ItemTypeNothing;
                    break;
                case ItemType.武器:
                    baseText = GameLanguage.ItemTypeWeapon;
                    break;
                case ItemType.盔甲:
                    baseText = GameLanguage.ItemTypeArmour;
                    break;
                case ItemType.头盔:
                    baseText = GameLanguage.ItemTypeHelmet;
                    break;
                case ItemType.项链:
                    baseText = GameLanguage.ItemTypeNecklace;
                    break;
                case ItemType.手镯:
                    baseText = GameLanguage.ItemTypeBracelet;
                    break;
                case ItemType.戒指:
                    baseText = GameLanguage.ItemTypeRing;
                    break;
                case ItemType.护身符:
                    baseText = GameLanguage.ItemTypeAmulet;
                    break;
                case ItemType.腰带:
                    baseText = GameLanguage.ItemTypeBelt;
                    break;
                case ItemType.靴子:
                    baseText = GameLanguage.ItemTypeBoots;
                    break;
                case ItemType.守护石:
                    baseText = GameLanguage.ItemTypeStone;
                    break;
                case ItemType.照明物:
                    baseText = GameLanguage.ItemTypeTorch;
                    break;
                case ItemType.药水:
                    baseText = GameLanguage.ItemTypePotion;
                    break;
                case ItemType.矿石:
                    baseText = GameLanguage.ItemTypeOre;
                    break;
                case ItemType.肉:
                    baseText = GameLanguage.ItemTypeMeat;
                    break;
                case ItemType.工艺材料:
                    baseText = GameLanguage.ItemTypeCraftingMaterial;
                    break;
                case ItemType.卷轴:
                    baseText = GameLanguage.ItemTypeScroll;
                    break;
                case ItemType.宝玉神珠:
                    baseText = GameLanguage.ItemTypeGem;
                    break;
                case ItemType.坐骑:
                    baseText = GameLanguage.ItemTypeMount;
                    break;
                case ItemType.技能书:
                    baseText = GameLanguage.ItemTypeBook;
                    break;
                case ItemType.特殊消耗品:
                    baseText = GameLanguage.ItemTypeScript;
                    break;
                case ItemType.缰绳:
                    baseText = GameLanguage.ItemTypeReins;
                    break;
                case ItemType.铃铛:
                    baseText = GameLanguage.ItemTypeBells;
                    break;
                case ItemType.马鞍:
                    baseText = GameLanguage.ItemTypeSaddle;
                    break;
                case ItemType.蝴蝶结:
                    baseText = GameLanguage.ItemTypeRibbon;
                    break;
                case ItemType.面甲:
                    baseText = GameLanguage.ItemTypeMask;
                    break;
                case ItemType.坐骑食物:
                    baseText = GameLanguage.ItemTypeFood;
                    break;
                case ItemType.鱼钩:
                    baseText = GameLanguage.ItemTypeHook;
                    break;
                case ItemType.鱼漂:
                    baseText = GameLanguage.ItemTypeFloat;
                    break;
                case ItemType.鱼饵:
                    baseText = GameLanguage.ItemTypeBait;
                    break;
                case ItemType.探鱼器:
                    baseText = GameLanguage.ItemTypeFinder;
                    break;
                case ItemType.摇轮:
                    baseText = GameLanguage.ItemTypeReel;
                    break;
                case ItemType.鱼:
                    baseText = GameLanguage.ItemTypeFish;
                    break;
                case ItemType.任务物品:
                    baseText = GameLanguage.ItemTypeQuest;
                    break;
                case ItemType.觉醒物品:
                    baseText = GameLanguage.ItemTypeAwakening;
                    break;
                case ItemType.灵物:
                    baseText = GameLanguage.ItemTypePets;
                    break;
                case ItemType.外形物品:
                    baseText = GameLanguage.ItemTypeTransform;
                    break;
                case ItemType.装饰:
                    baseText = GameLanguage.ItemTypeDeco;
                    break;
                case ItemType.怪物蛋:
                    baseText = GameLanguage.ItemTypeMonsterSpawn;
                    break;
                case ItemType.封印:
                    baseText = GameLanguage.ItemTypeSealedHero;
                    break;
            }

            if (HoverItem.WeddingRing != -1)
            {
                baseText = GameLanguage.WeddingRing;
            }

            baseText = string.Format(GameLanguage.ItemTextFormat, baseText, string.IsNullOrEmpty(baseText) ? "" : "\n", GameLanguage.Weight, HoverItem.Weight + text);

            MirLabel etcLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.White,
                Location = new Point(4, nameLabel.DisplayRectangle.Bottom),
                OutLine = true,
                Parent = ItemLabel,
                Text = baseText
            };

            ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, etcLabel.DisplayRectangle.Right + 4),
                Math.Max(ItemLabel.Size.Height, etcLabel.DisplayRectangle.Bottom + 4));

            #region OUTLINE
            MirControl outLine = new MirControl
            {
                BackColour = Color.FromArgb(255, 50, 50, 50),
                Border = true,
                BorderColour = Color.Gray,
                NotControl = true,
                Parent = ItemLabel,
                Opacity = 0.4F,
                Location = new Point(0, 0)
            };
            outLine.Size = ItemLabel.Size;
            #endregion

            return outLine;
        }
        public MirControl AttackInfoLabel(UserItem item, bool Inspect = false, bool hideAdded = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfo realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            bool fishingItem = false;

            switch (HoverItem.Info.Type)
            {
                case ItemType.鱼钩:
                case ItemType.鱼漂:
                case ItemType.鱼饵:
                case ItemType.探鱼器:
                case ItemType.摇轮:
                    fishingItem = true;
                    break;
                case ItemType.武器:
                    if (Globals.FishingRodShapes.Contains(HoverItem.Info.Shape))
                        fishingItem = true;
                    break;
                default:
                    fishingItem = false;
                    break;
            }

            int count = 0;
            int minValue = 0;
            int maxValue = 0;
            int addValue = 0;
            string text = "";

            #region Dura gem
            minValue = realItem.Durability;

            if (minValue > 0 && realItem.Type == ItemType.宝玉神珠)
            {
                switch (realItem.Shape)
                {
                    default:
                        text = string.Format("增加 {0} 持久度", minValue / 1000);
                        break;
                    case 8:
                        text = string.Format("锁定时间为 {0}", Functions.PrintTimeSpanFromSeconds(minValue * 60));
                        break;
                }

                count++;
                MirLabel DuraLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DuraLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DuraLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region DC
            minValue = realItem.Stats[Stat.MinDC];
            maxValue = realItem.Stats[Stat.MaxDC];
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.MaxDC] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.宝玉神珠)
                    text = string.Format(addValue > 0 ? GameLanguage.DC : GameLanguage.DC2, minValue, maxValue + addValue, addValue);
                else
                    text = string.Format("增加 {0} 物理攻击", minValue + maxValue + addValue);
                MirLabel DCLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DCLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DCLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region MC

            minValue = realItem.Stats[Stat.MinMC];
            maxValue = realItem.Stats[Stat.MaxMC];
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.MaxMC] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.宝玉神珠)
                    text = string.Format(addValue > 0 ? GameLanguage.MC : GameLanguage.MC2, minValue, maxValue + addValue, addValue);
                else
                    text = string.Format("增加 {0} 魔法攻击", minValue + maxValue + addValue);
                MirLabel MCLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MCLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MCLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region SC

            minValue = realItem.Stats[Stat.MinSC];
            maxValue = realItem.Stats[Stat.MaxSC];
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.MaxSC] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.宝玉神珠)
                    text = string.Format(addValue > 0 ? GameLanguage.SC : GameLanguage.SC2, minValue, maxValue + addValue, addValue);
                else
                    text = string.Format("增加 {0} 道术攻击", minValue + maxValue + addValue);
                MirLabel SCLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("SC + {0}~{1}", minValue, maxValue + addValue)
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, SCLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, SCLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region LUCK / SUCCESS

            minValue = realItem.Stats[Stat.幸运];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.幸运] : 0;

            if (minValue != 0 || addValue != 0)
            {
                count++;

                if (realItem.Type == ItemType.灵物 && realItem.Shape == 28)
                {
                    text = string.Format("背包负重 + {0}% ", minValue + addValue);
                }
                else if (realItem.Type == ItemType.药水 && realItem.Shape == 4)
                {
                    text = string.Format("经验值 + {0}% ", minValue + addValue);
                }
                else if (realItem.Type == ItemType.药水 && realItem.Shape == 5)
                {
                    text = string.Format("物品掉落率 + {0}% ", minValue + addValue);
                }
                else
                {
                    text = string.Format(minValue + addValue > 0 ? GameLanguage.Luck : "诅咒 + {0}", Math.Abs(minValue + addValue));
                }

                MirLabel LUCKLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, LUCKLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, LUCKLabel.DisplayRectangle.Bottom));
            }

            #endregion



            #region ACC

            minValue = realItem.Stats[Stat.准确];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.准确] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.宝玉神珠)
                    text = string.Format(addValue > 0 ? GameLanguage.Accuracy : GameLanguage.Accuracy2, minValue + addValue, addValue);
                else
                    text = string.Format("增加 {0} 准确", minValue + maxValue + addValue);
                MirLabel ACCLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Accuracy + {0}", minValue + addValue)
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, ACCLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, ACCLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region HOLY

            minValue = realItem.Stats[Stat.神圣];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel HOLYLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Holy + {0}", minValue + addValue)
                    Text = string.Format(addValue > 0 ? GameLanguage.Holy : GameLanguage.Holy2, minValue + addValue, addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, HOLYLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, HOLYLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region ASPEED

            minValue = realItem.Stats[Stat.攻击速度];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.攻击速度] : 0;

            if (minValue != 0 || maxValue != 0 || addValue != 0)
            {
                string plus = (addValue + minValue < 0) ? "" : "+";

                count++;
                if (HoverItem.Info.Type != ItemType.宝玉神珠)
                {
                    string negative = "+";
                    if (addValue < 0) negative = "";
                    text = string.Format(addValue != 0 ? "攻击速度 " + plus + " {0} ({2}{1})" : "攻击速度 " + plus + " {0}", minValue + addValue, addValue, negative);
                    //text = string.Format(addValue > 0 ? "A.Speed: + {0} (+{1})" : "A.Speed: + {0}", minValue + addValue, addValue);
                }
                else
                    text = string.Format("增加 {0} 攻击速度", minValue + maxValue + addValue);
                MirLabel ASPEEDLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("A.Speed + {0}", minValue + addValue)
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, ASPEEDLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, ASPEEDLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region FREEZING

            minValue = realItem.Stats[Stat.冰冻伤害];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.冰冻伤害] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.宝玉神珠)
                    text = string.Format(addValue > 0 ? "冰冻攻击 + {0} (+{1})" : "冰冻攻击 + {0}", minValue + addValue, addValue);
                else
                    text = string.Format("增加 {0} 冰冻攻击", minValue + maxValue + addValue);
                MirLabel FREEZINGLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Freezing + {0}", minValue + addValue)
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, FREEZINGLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, FREEZINGLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region POISON

            minValue = realItem.Stats[Stat.毒素伤害];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.毒素伤害] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.宝玉神珠)
                    text = string.Format(addValue > 0 ? "毒素攻击 + {0} (+{1})" : "毒素攻击 + {0}", minValue + addValue, addValue);
                else
                    text = string.Format("增加 {0} 毒素攻击", minValue + maxValue + addValue);
                MirLabel POISONLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Poison + {0}", minValue + addValue)
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, POISONLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, POISONLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region CRITICALRATE / FLEXIBILITY

            minValue = realItem.Stats[Stat.暴击倍率];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.暴击倍率] : 0;

            if ((minValue > 0 || maxValue > 0 || addValue > 0) && (realItem.Type != ItemType.宝玉神珠))
            {
                count++;                    
                MirLabel CRITICALRATELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Critical Chance + {0}", minValue + addValue)
                    Text = string.Format(addValue > 0 ? "暴击几率 + {0} (+{1})" : "暴击几率 + {0}", minValue + addValue, addValue)
                };

                if(fishingItem)
                {
                    CRITICALRATELabel.Text = string.Format(addValue > 0 ? "弹性 + {0} (+{1})" : "弹性 + {0}", minValue + addValue, addValue);
                }

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, CRITICALRATELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, CRITICALRATELabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region CRITICALDAMAGE

            minValue = realItem.Stats[Stat.暴击伤害];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.暴击伤害] : 0;

            if ((minValue > 0 || maxValue > 0 || addValue > 0) && (realItem.Type != ItemType.宝玉神珠))
            {
                count++;
                MirLabel CRITICALDAMAGELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Critical Damage + {0}", minValue + addValue)
                    Text = string.Format(addValue > 0 ? "暴击伤害 + {0} (+{1})" : "暴击伤害 + {0}", minValue + addValue, addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, CRITICALDAMAGELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, CRITICALDAMAGELabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region Reflect

            minValue = realItem.Stats[Stat.反弹伤害];
            maxValue = 0;
            addValue = 0;

            if ((minValue > 0 || maxValue > 0 || addValue > 0) && (realItem.Type != ItemType.宝玉神珠))
            {
                count++;
                MirLabel ReflectLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("反弹伤害 {0}", minValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, ReflectLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, ReflectLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region Hpdrain

            minValue = realItem.Stats[Stat.吸血数率];
            maxValue = 0;
            addValue = 0;

            if ((minValue > 0 || maxValue > 0 || addValue > 0) && (realItem.Type != ItemType.宝玉神珠))
            {
                count++;
                MirLabel HPdrainLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("吸血数率 {0}%", minValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, HPdrainLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, HPdrainLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region Exp Rate

            minValue = realItem.Stats[Stat.经验增长数率];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.经验增长数率] : 0;

            if (minValue != 0 || maxValue != 0 || addValue != 0)
            {
                string plus = (addValue + minValue < 0) ? "" : "+";

                count++;
                string negative = "+";
                if (addValue < 0) negative = "";
                text = string.Format(addValue != 0 ? "经验倍率: " + plus + "{0}% ({2}{1}%)" : "经验倍率: " + plus + "{0}%", minValue + addValue, addValue, negative);

                MirLabel expRateLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, expRateLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, expRateLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region Drop Rate

            minValue = realItem.Stats[Stat.物品掉落数率];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.物品掉落数率] : 0;

            if (minValue != 0 || maxValue != 0 || addValue != 0)
            {
                string plus = (addValue + minValue < 0) ? "" : "+";

                count++;
                string negative = "+";
                if (addValue < 0) negative = "";
                text = string.Format(addValue != 0 ? "掉落几率: " + plus + "{0}% ({2}{1}%)" : "掉落几率: " + plus + "{0}%", minValue + addValue, addValue, negative);

                MirLabel dropRateLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, dropRateLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, dropRateLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region Gold Rate

            minValue = realItem.Stats[Stat.金币收益数率];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.金币收益数率] : 0;

            if (minValue != 0 || maxValue != 0 || addValue != 0)
            {
                string plus = (addValue + minValue < 0) ? "" : "+";

                count++;
                string negative = "+";
                if (addValue < 0) negative = "";
                text = string.Format(addValue != 0 ? "金币爆率: " + plus + "{0}% ({2}{1}%)" : "金币爆率: " + plus + "{0}%", minValue + addValue, addValue, negative);

                MirLabel goldRateLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, goldRateLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, goldRateLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region Hero

            if (HoverItem.AddedStats[Stat.Hero] > 0)
            {
                ClientHeroInformation heroInfo = HeroInfoList.FirstOrDefault(x => x.Index == HoverItem.AddedStats[Stat.Hero]);
                if (heroInfo != null)
                {
                    count++;
                    text = heroInfo.ToString();

                    MirLabel heroLabel = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = text
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, heroLabel.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, heroLabel.DisplayRectangle.Bottom));
                }
            }

            #endregion

            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                #region OUTLINE
                MirControl outLine = new MirControl
                {
                    BackColour = Color.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = Color.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;
                #endregion

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl DefenceInfoLabel(UserItem item, bool Inspect = false, bool hideAdded = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfo realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            bool fishingItem = false;

            switch (HoverItem.Info.Type)
            {
                case ItemType.鱼钩:
                case ItemType.鱼漂:
                case ItemType.鱼饵:
                case ItemType.探鱼器:
                case ItemType.摇轮:
                    fishingItem = true;
                    break;
                case ItemType.武器:
                    if (HoverItem.Info.Shape == 49 || HoverItem.Info.Shape == 50)
                        fishingItem = true;
                    break;
                default:
                    fishingItem = false;
                    break;
            }

            int count = 0;
            int minValue = 0;
            int maxValue = 0;
            int addValue = 0;

            string text = "";
            #region AC

            minValue = realItem.Stats[Stat.MinAC];
            maxValue = realItem.Stats[Stat.MaxAC];
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.MaxAC] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.宝玉神珠)
                    text = string.Format(addValue > 0 ? GameLanguage.AC : GameLanguage.AC2, minValue, maxValue + addValue, addValue);
                else
                    text = string.Format("增加 {0} 物理防御", minValue + maxValue + addValue);
                MirLabel ACLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("AC + {0}~{1}", minValue, maxValue + addValue)
                    Text = text
                };

                if (fishingItem)
                {
                    if (HoverItem.Info.Type == ItemType.鱼漂)
                    {
                        ACLabel.Text = string.Format("钓鱼咬钩几率 + " + (addValue > 0 ? "{0}~{1}% (+{2})" : "{0}~{1}%"), minValue, maxValue + addValue);
                    }
                    else if (HoverItem.Info.Type == ItemType.探鱼器)
                    {
                        ACLabel.Text = string.Format("弹性 + " + (addValue > 0 ? "{0}~{1}% (+{2})" : "{0}~{1}%"), minValue, maxValue + addValue);
                    }
                    else
                    {
                        ACLabel.Text = string.Format("钓鱼收杆成功率 + " + (addValue > 0 ? "{0}% (+{1})" : "{0}%"), maxValue, maxValue + addValue);
                    }
                }

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, ACLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, ACLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region MAC

            minValue = realItem.Stats[Stat.MinMAC];
            maxValue = realItem.Stats[Stat.MaxMAC];
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.MaxMAC] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.宝玉神珠)
                    text = string.Format(addValue > 0 ? GameLanguage.MAC : GameLanguage.MAC2, minValue, maxValue + addValue, addValue);
                else
                    text = string.Format("增加 {0} 魔法防御", minValue + maxValue + addValue);
                MirLabel MACLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("MAC + {0}~{1}", minValue, maxValue + addValue)
                    Text = text
                };

                if (fishingItem)
                {
                    MACLabel.Text = string.Format("自动钓鱼成功率 + {0}%", maxValue + addValue);
                }

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MACLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MACLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region MAXHP

            if (HoverItem.Info.Type != ItemType.怪物蛋)
            {
                minValue = realItem.Stats[Stat.HP];
                maxValue = 0;
                addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.HP] : 0;

                if (minValue > 0 || maxValue > 0 || addValue > 0)
                {
                    count++;
                    MirLabel MAXHPLabel = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        //Text = string.Format(realItem.Type == ItemType.药水 ? "HP + {0} Recovery" : "MAXHP + {0}", minValue + addValue)
                        Text = string.Format(addValue > 0 ? "生命值 + {0} (+{1})" : "生命值 + {0}", minValue + addValue, addValue)
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXHPLabel.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, MAXHPLabel.DisplayRectangle.Bottom));
                }
            }

            #endregion

            #region MAXMP

            minValue = realItem.Stats[Stat.MP];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.MP] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXMPLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format(realItem.Type == ItemType.药水 ? "MP + {0} Recovery" : "MAXMP + {0}", minValue + addValue)
                    Text = string.Format(addValue > 0 ? "法力值 + {0} (+{1})" : "法力值 + {0}", minValue + addValue, addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXMPLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXMPLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region MAXHPRATE

            minValue = realItem.Stats[Stat.生命值数率];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXHPRATELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("生命值 + {0}%", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXHPRATELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXHPRATELabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region MAXMPRATE

            minValue = realItem.Stats[Stat.法力值数率];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXMPRATELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("法力值 + {0}%", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXMPRATELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXMPRATELabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region MAXACRATE

            minValue = realItem.Stats[Stat.最大防御数率];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXACRATE = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("物理防御 + {0}%", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXACRATE.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXACRATE.DisplayRectangle.Bottom));
            }

            #endregion

            #region MAXMACRATE

            minValue = realItem.Stats[Stat.最大魔御数率];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXMACRATELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("魔法防御 + {0}%", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXMACRATELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXMACRATELabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region HEALTH_RECOVERY

            minValue = realItem.Stats[Stat.生命恢复];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.生命恢复] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel HEALTH_RECOVERYLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format(addValue > 0 ? "生命恢复 + {0} (+{1})" : "生命恢复 + {0}", minValue + addValue, addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, HEALTH_RECOVERYLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, HEALTH_RECOVERYLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region MANA_RECOVERY

            minValue = realItem.Stats[Stat.法力恢复];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.法力恢复] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MANA_RECOVERYLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("ManaRecovery + {0}", minValue + addValue)
                    Text = string.Format(addValue > 0 ? "法力恢复 + {0} (+{1})" : "法力恢复 + {0}", minValue + addValue, addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MANA_RECOVERYLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MANA_RECOVERYLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region POISON_RECOVERY

            minValue = realItem.Stats[Stat.中毒恢复];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.中毒恢复] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel POISON_RECOVERYabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Poison Recovery + {0}", minValue + addValue)
                    Text = string.Format(addValue > 0 ? "中毒恢复 + {0} (+{1})" : "中毒恢复 + {0}", minValue + addValue, addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, POISON_RECOVERYabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, POISON_RECOVERYabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region AGILITY

            minValue = realItem.Stats[Stat.敏捷];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.敏捷] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.宝玉神珠)
                    text = string.Format(addValue > 0 ? GameLanguage.Agility : GameLanguage.Agility2, minValue + addValue, addValue);
                else
                    text = string.Format("增加 {0} 敏捷度", minValue + maxValue + addValue);

                MirLabel AGILITYLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, AGILITYLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, AGILITYLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region STRONG

            minValue = realItem.Stats[Stat.强度];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.强度] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel STRONGLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Strong + {0}", minValue + addValue)
                    Text = string.Format(addValue > 0 ? "强度 + {0} (+{1})" : "强度 + {0}", minValue + addValue, addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, STRONGLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, STRONGLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region POISON_RESIST

            minValue = realItem.Stats[Stat.毒物躲避];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.毒物躲避] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.宝玉神珠)
                    text = string.Format(addValue > 0 ? "毒物躲避 + {0} (+{1})" : "毒物躲避 + {0}", minValue + addValue, addValue);
                else
                    text = string.Format("增加 {0} 毒物躲避", minValue + maxValue + addValue);
                MirLabel POISON_RESISTLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, POISON_RESISTLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, POISON_RESISTLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region MAGIC_RESIST

            minValue = realItem.Stats[Stat.魔法躲避];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? HoverItem.AddedStats[Stat.魔法躲避] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.宝玉神珠)
                    text = string.Format(addValue > 0 ? "魔法躲避 + {0} (+{1})" : "魔法躲避 + {0}", minValue + addValue, addValue);
                else
                    text = string.Format("增加 {0} 魔法躲避", minValue + maxValue + addValue);
                MirLabel MAGIC_RESISTLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Magic Resist + {0}", minValue + addValue)
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAGIC_RESISTLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAGIC_RESISTLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region MAX_DC_RATE

            minValue = realItem.Stats[Stat.最大物理攻击数率];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXDCRATE = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("物理攻击 + {0}%", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXDCRATE.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXDCRATE.DisplayRectangle.Bottom));
            }
            #endregion

            #region MAX_MC_RATE

            minValue = realItem.Stats[Stat.最大魔法攻击数率];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXMCRATE = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("魔法攻击 + {0}%", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXMCRATE.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXMCRATE.DisplayRectangle.Bottom));
            }
            #endregion

            #region MAX_SC_RATE

            minValue = realItem.Stats[Stat.最大道术攻击数率];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXSCRATE = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("道术攻击 + {0}%", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXSCRATE.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXSCRATE.DisplayRectangle.Bottom));
            }
            #endregion

            #region DAMAGE_REDUCTION

            minValue = realItem.Stats[Stat.伤害降低数率];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel DAMAGEREDUC = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("减伤 + {0}%", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DAMAGEREDUC.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DAMAGEREDUC.DisplayRectangle.Bottom));
            }
            #endregion
            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);
                
                #region OUTLINE
                MirControl outLine = new MirControl
                {
                    BackColour = Color.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = Color.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;
                #endregion

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl WeightInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfo realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);
            
            int count = 0;
            int minValue = 0;
            int maxValue = 0;
            int addValue = 0;
            
            #region HANDWEIGHT

            minValue = realItem.Stats[Stat.腕力负重];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel HANDWEIGHTLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Hand Weight + {0}", minValue + addValue)
                    Text = string.Format(addValue > 0 ? "腕力 + {0} (+{1})" : "腕力 + {0}", minValue + addValue, addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, HANDWEIGHTLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, HANDWEIGHTLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region WEARWEIGHT

            minValue = realItem.Stats[Stat.装备负重];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel WEARWEIGHTLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Wear Weight + {0}", minValue + addValue)
                    Text = string.Format(addValue > 0 ? "装备负重 + {0} (+{1})" : "装备负重 + {0}", minValue + addValue, addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, WEARWEIGHTLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, WEARWEIGHTLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region BAGWEIGHT

            minValue = realItem.Stats[Stat.背包负重];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel BAGWEIGHTLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Bag Weight + {0}", minValue + addValue)
                    Text = string.Format(addValue > 0 ? "背包负重 + {0} (+{1})" : "背包负重 + {0}", minValue + addValue, addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, BAGWEIGHTLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, BAGWEIGHTLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region FASTRUN
            minValue = realItem.CanFastRun==true?1:0;
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel BAGWEIGHTLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("免助跑")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, BAGWEIGHTLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, BAGWEIGHTLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region TIME & RANGE
            minValue = 0;
            maxValue = 0;
            addValue = 0;

            if (HoverItem.Info.Type == ItemType.药水 && HoverItem.Info.Durability > 0)
            {
                count++;
                MirLabel TNRLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("时间 {0}", Functions.PrintTimeSpanFromSeconds(HoverItem.Info.Durability * 60))
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, TNRLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, TNRLabel.DisplayRectangle.Bottom));
            }

            if (HoverItem.Info.Type == ItemType.外形物品 && HoverItem.Info.Durability > 0)
            {
                count++;
                MirLabel TNRLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("时间 {0}", Functions.PrintTimeSpanFromSeconds(HoverItem.Info.Durability, false))
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, TNRLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, TNRLabel.DisplayRectangle.Bottom));
            }

            #endregion

            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                #region OUTLINE
                MirControl outLine = new MirControl
                {
                    BackColour = Color.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = Color.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;
                #endregion

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl AwakeInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfo realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            int count = 0;

            #region AWAKENAME
            if (HoverItem.Awake.GetAwakeLevel() > 0)
            {
                count++;
                MirLabel AWAKENAMELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = GradeNameColor(HoverItem.Info.Grade),
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("{0}的觉醒 ({1})", HoverItem.Awake.Type switch
                    {
                        AwakeType.物理攻击 => "勇猛",
                        AwakeType.魔法攻击 => "魔性",
                        AwakeType.道术攻击 => "仙界",
                        AwakeType.物理防御 => "守护",
                        AwakeType.魔法防御 => "制魔",
                        AwakeType.生命法力值 => "体质",
                        _ => throw new NotImplementedException(),
                    }, HoverItem.Awake.GetAwakeLevel())
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, AWAKENAMELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, AWAKENAMELabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region AWAKE_TOTAL_VALUE
            if (HoverItem.Awake.GetAwakeValue() > 0)
            {
                count++;
                MirLabel AWAKE_TOTAL_VALUELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format(realItem.Type != ItemType.盔甲 ? "{0} + {1}~{2}" : "{0}上限 + {1}", HoverItem.Awake.Type.ToString(), HoverItem.Awake.GetAwakeValue(), HoverItem.Awake.GetAwakeValue())
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, AWAKE_TOTAL_VALUELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, AWAKE_TOTAL_VALUELabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region AWAKE_LEVEL_VALUE
            if (HoverItem.Awake.GetAwakeLevel() > 0)
            {
                count++;
                for (int i = 0; i < HoverItem.Awake.GetAwakeLevel(); i++)
                {
                    MirLabel AWAKE_LEVEL_VALUELabel = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = string.Format(realItem.Type != ItemType.盔甲 ? "{0} 阶 觉醒: {1} + {2}~{3}" : "{0} 阶 觉醒: {1}上限 + {2}~{3}", i + 1, HoverItem.Awake.Type.ToString(), HoverItem.Awake.GetAwakeLevelValue(i), HoverItem.Awake.GetAwakeLevelValue(i))
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, AWAKE_LEVEL_VALUELabel.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, AWAKE_LEVEL_VALUELabel.DisplayRectangle.Bottom));
                }
            }

            #endregion

            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                #region OUTLINE
                MirControl outLine = new MirControl
                {
                    BackColour = Color.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = Color.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;
                #endregion

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl SocketInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfo realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);


            int count = 0;

            #region SOCKET

            for (int i = 0; i < item.Slots.Length; i++)
            {
                count++;
                MirLabel SOCKETLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = (count > realItem.Slots && !realItem.IsFishingRod && realItem.Type != ItemType.坐骑) ? Color.Cyan : Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format(" 镶嵌 {0}", item.Slots[i] == null ? "空" : item.Slots[i].FriendlyName)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, SOCKETLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, SOCKETLabel.DisplayRectangle.Bottom));
            }

            #endregion

            if (count > 0)
            {
                #region SOCKET

                count++;
                MirLabel SOCKETLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = "Ctrl+鼠标右键打开镶嵌"
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, SOCKETLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, SOCKETLabel.DisplayRectangle.Bottom));

                #endregion

                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                #region OUTLINE
                MirControl outLine = new MirControl
                {
                    BackColour = Color.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = Color.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;
                #endregion

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl NeedInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfo realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            int count = 0;

            #region LEVEL
            if (realItem.RequiredAmount > 0)
            {
                count++;
                string text;
                Color colour = Color.White;
                switch (realItem.RequiredType)
                {
                    case RequiredType.Level:
                        text = string.Format(GameLanguage.RequiredLevel, realItem.RequiredAmount);
                        if (MapObject.User.Level < realItem.RequiredAmount)
                            colour = Color.Red;
                        break;
                    case RequiredType.MaxAC:
                        text = string.Format("需要物理防御{0}", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MaxAC] < realItem.RequiredAmount)
                            colour = Color.Red;
                        break;
                    case RequiredType.MaxMAC:
                        text = string.Format("需要魔法防御{0}", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MaxMAC] < realItem.RequiredAmount)
                            colour = Color.Red;
                        break;
                    case RequiredType.MaxDC:
                        text = string.Format(GameLanguage.RequiredDC, realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MaxDC] < realItem.RequiredAmount)
                            colour = Color.Red;
                        break;
                    case RequiredType.MaxMC:
                        text = string.Format(GameLanguage.RequiredMC, realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MaxMC] < realItem.RequiredAmount)
                            colour = Color.Red;
                        break;
                    case RequiredType.MaxSC:
                        text = string.Format(GameLanguage.RequiredSC, realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MaxSC] < realItem.RequiredAmount)
                            colour = Color.Red;
                        break;
                    case RequiredType.MaxLevel:
                        text = string.Format("最高等级{0}", realItem.RequiredAmount);
                        if (MapObject.User.Level > realItem.RequiredAmount)
                            colour = Color.Red;
                        break;
                    case RequiredType.MinAC:
                        text = string.Format("需要物理防御 : {0}", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MinAC] < realItem.RequiredAmount)
                            colour = Color.Red;
                        break;
                    case RequiredType.MinMAC:
                        text = string.Format("需要魔法防御 : {0}", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MinMAC] < realItem.RequiredAmount)
                            colour = Color.Red;
                        break;
                    case RequiredType.MinDC:
                        text = string.Format("需要物理攻击 : {0}", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MinDC] < realItem.RequiredAmount)
                            colour = Color.Red;
                        break;
                    case RequiredType.MinMC:
                        text = string.Format("需要魔法攻击 : {0}", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MinMC] < realItem.RequiredAmount)
                            colour = Color.Red;
                        break;
                    case RequiredType.MinSC:
                        text = string.Format("需要道术攻击 : {0}", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MinSC] < realItem.RequiredAmount)
                            colour = Color.Red;
                        break;
                    default:
                        text = "要求类型不明";
                        break;
                }

                MirLabel LEVELLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = colour,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, LEVELLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, LEVELLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region CLASS
            if (realItem.RequiredClass != RequiredClass.全职业)
            {
                count++;
                Color colour = Color.White;

                switch (MapObject.User.Class)
                {
                    case MirClass.战士:
                        if (!realItem.RequiredClass.HasFlag(RequiredClass.战士))
                            colour = Color.Red;
                        break;
                    case MirClass.法师:
                        if (!realItem.RequiredClass.HasFlag(RequiredClass.法师))
                            colour = Color.Red;
                        break;
                    case MirClass.道士:
                        if (!realItem.RequiredClass.HasFlag(RequiredClass.道士))
                            colour = Color.Red;
                        break;
                    case MirClass.刺客:
                        if (!realItem.RequiredClass.HasFlag(RequiredClass.刺客))
                            colour = Color.Red;
                        break;
                    case MirClass.弓箭:
                        if (!realItem.RequiredClass.HasFlag(RequiredClass.弓箭))
                            colour = Color.Red;
                        break;
                }

                MirLabel CLASSLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = colour,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format(GameLanguage.ClassRequired, realItem.RequiredClass)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, CLASSLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, CLASSLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region BUYING - SELLING PRICE
            if (item.Price() > 0)
            {
                count++;
                string text;
                var colour = Color.White;

                text = $"商店价格 : {((long)(item.Price() / 2)).ToString("###,###,##0")} 金币";

                var costLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = colour,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, costLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, costLabel.DisplayRectangle.Bottom));
            }


            #endregion

            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                #region OUTLINE
                MirControl outLine = new MirControl
                {
                    BackColour = Color.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = Color.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;
                #endregion

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }

        public MirControl ItemSetInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfo realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            int count = 0;

            #region ItemSet


            if (realItem.Set != ItemSet.非套装)
            {
                count++;
                MirLabel ItemSetLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.GreenYellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("{0}", realItem.Set)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, ItemSetLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, ItemSetLabel.DisplayRectangle.Bottom));
            }

            #endregion

            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                #region OUTLINE
                MirControl outLine = new MirControl
                {
                    BackColour = Color.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = Color.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;
                #endregion

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }

        public MirControl BindInfoLabel(UserItem item, bool Inspect = false, bool hideAdded = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfo realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            int count = 0;

            #region DONT_DEATH_DROP

            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontDeathdrop))
            {
                count++;
                MirLabel DONT_DEATH_DROPLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("死亡不掉")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_DEATH_DROPLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_DEATH_DROPLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region DONT_DROP

            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontDrop))
            {
                count++;
                MirLabel DONT_DROPLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("永不掉落")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_DROPLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_DROPLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region DONT_UPGRADE

            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontUpgrade))
            {
                count++;
                MirLabel DONT_UPGRADELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("升级禁止")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_UPGRADELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_UPGRADELabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region DONT_SELL

            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontSell))
            {
                count++;
                MirLabel DONT_SELLLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("商店禁止")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_SELLLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_SELLLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region DONT_TRADE

            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontTrade))
            {
                count++;
                MirLabel DONT_TRADELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("交易禁止")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_TRADELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_TRADELabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region DONT_STORE

            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontStore))
            {
                count++;
                MirLabel DONT_STORELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("仓库禁止")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_STORELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_STORELabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region DONT_REPAIR

            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontRepair))
            {
                count++;
                MirLabel DONT_REPAIRLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("修理禁止")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_REPAIRLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_REPAIRLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region DONT_SPECIALREPAIR

            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.NoSRepair))
            {
                count++;
                MirLabel DONT_REPAIRLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("特修禁止")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_REPAIRLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_REPAIRLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region BREAK_ON_DEATH

            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.BreakOnDeath))
            {
                count++;
                MirLabel DONT_REPAIRLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("死亡消失")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_REPAIRLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_REPAIRLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region DONT_DESTROY_ON_DROP

            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DestroyOnDrop))
            {
                count++;
                MirLabel DONT_DODLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("掉落消失")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_DODLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_DODLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region NoWeddingRing

            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.NoWeddingRing))
            {
                count++;
                MirLabel No_WedLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.HotPink, //Yellow
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("婚戒禁止")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, No_WedLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, No_WedLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region NoHero

            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.NoHero))
            {
                count++;
                MirLabel No_HeroLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("英雄禁止")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, No_HeroLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, No_HeroLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region BIND_ON_EQUIP

            if ((HoverItem.Info.Bind.HasFlag(BindMode.BindOnEquip)) & HoverItem.SoulBoundId == -1)
            {
                count++;
                MirLabel BOELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("装备绑定")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, BOELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, BOELabel.DisplayRectangle.Bottom));
            }
            else if (HoverItem.SoulBoundId != -1)
            {
                count++;
                MirLabel BOELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = "绑定到 " + GetUserName((uint)HoverItem.SoulBoundId)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, BOELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, BOELabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region CURSED

            if ((!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) && HoverItem.Cursed)
            {
                count++;
                MirLabel CURSEDLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("诅咒")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, CURSEDLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, CURSEDLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region Gems

            if (HoverItem.Info.Type == ItemType.宝玉神珠)
            {
                #region UseOn text
                count++;
                string Text = "";
                if (HoverItem.Info.Unique == SpecialItemMode.None)
                {
                    Text = "赋能到所选物品";
                }
                else
                {
                    Text = "可赋能在 ";
                }
                MirLabel GemUseOn = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Fuchsia,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = Text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GemUseOn.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, GemUseOn.DisplayRectangle.Bottom));
                #endregion
                #region Weapon text
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Paralize))
                {
                    MirLabel GemWeapon = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = "-武器"
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GemWeapon.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, GemWeapon.DisplayRectangle.Bottom));
                }
                #endregion
                #region Armour text
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Teleport))
                {
                    MirLabel GemArmour = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = "-盔甲"
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GemArmour.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, GemArmour.DisplayRectangle.Bottom));
                }
                #endregion
                #region Helmet text
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.ClearRing))
                {
                    MirLabel Gemhelmet = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = "-头盔"
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gemhelmet.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gemhelmet.DisplayRectangle.Bottom));
                }
                #endregion
                #region Necklace text
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Protection))
                {
                    MirLabel Gemnecklace = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = "-项链"
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gemnecklace.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gemnecklace.DisplayRectangle.Bottom));
                }
                #endregion
                #region Bracelet text
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Revival))
                {
                    MirLabel GemBracelet = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = "-手镯"
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GemBracelet.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, GemBracelet.DisplayRectangle.Bottom));
                }
                #endregion
                #region Ring text
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Muscle))
                {
                    MirLabel GemRing = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = "-戒指"
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GemRing.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, GemRing.DisplayRectangle.Bottom));
                }
                #endregion
                #region Amulet text
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Flame))
                {
                    MirLabel Gemamulet = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = "-护身符"
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gemamulet.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gemamulet.DisplayRectangle.Bottom));
                }
                #endregion
                #region Belt text
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Healing))
                {
                    MirLabel Gembelt = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = "-腰带"
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gembelt.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gembelt.DisplayRectangle.Bottom));
                }
                #endregion
                #region Boots text
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Probe))
                {
                    MirLabel Gemboots = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = "-靴子"
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gemboots.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gemboots.DisplayRectangle.Bottom));
                }
                #endregion
                #region Stone text
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Skill))
                {
                    MirLabel Gemstone = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = "-守护石"
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gemstone.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gemstone.DisplayRectangle.Bottom));
                }
                #endregion
                #region Torch text
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.NoDuraLoss))
                {
                    MirLabel Gemtorch = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = "-照明物"
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gemtorch.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gemtorch.DisplayRectangle.Bottom));
                }
                #endregion
            }

            #endregion

            #region CANTAWAKEN

            //if ((HoverItem.Info.CanAwakening != true) && (HoverItem.Info.Type != ItemType.Gem))
            //{
            //    count++;
            //    MirLabel CANTAWAKENINGLabel = new MirLabel
            //    {
            //        AutoSize = true,
            //        ForeColour = Color.Yellow,
            //        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
            //        OutLine = true,
            //        Parent = ItemLabel,
            //        Text = string.Format("Can't awaken")
            //    };

            //    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, CANTAWAKENINGLabel.DisplayRectangle.Right + 4),
            //        Math.Max(ItemLabel.Size.Height, CANTAWAKENINGLabel.DisplayRectangle.Bottom));
            //}

            if (HoverItem.Info.CanAwakening == true)
            {
                count++;
                MirLabel CANTAWAKENINGLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format("觉醒物品")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, CANTAWAKENINGLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, CANTAWAKENINGLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region EXPIRE

            if (HoverItem.ExpireInfo != null)
            {
                double remainingSeconds = (HoverItem.ExpireInfo.ExpiryDate - CMain.Now).TotalSeconds;

                count++;
                MirLabel EXPIRELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = remainingSeconds > 0 ? string.Format("过期时间 {0}", Functions.PrintTimeSpanFromSeconds(remainingSeconds)) : "过期"
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, EXPIRELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, EXPIRELabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region SEALED

            if (HoverItem.SealedInfo != null)
            {
                double remainingSeconds = (HoverItem.SealedInfo.ExpiryDate - CMain.Now).TotalSeconds;

                if (remainingSeconds > 0)
                {
                    count++;
                    MirLabel SEALEDLabel = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = Color.Red,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = remainingSeconds > 0 ? string.Format("锁定：{0}", Functions.PrintTimeSpanFromSeconds(remainingSeconds)) : ""
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, SEALEDLabel.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, SEALEDLabel.DisplayRectangle.Bottom));
                }
            }

            #endregion

            if (HoverItem.RentalInformation?.RentalLocked == false)
            {
                count++;
                MirLabel OWNERLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.DarkKhaki,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = "物品租赁人 " + HoverItem.RentalInformation.OwnerName
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, OWNERLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, OWNERLabel.DisplayRectangle.Bottom));

                double remainingTime = (HoverItem.RentalInformation.ExpiryDate - CMain.Now).TotalSeconds;

                count++;
                MirLabel RENTALLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Khaki,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = remainingTime > 0 ? string.Format("租赁期限 {0}", Functions.PrintTimeSpanFromSeconds(remainingTime)) : "到期"
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, RENTALLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, RENTALLabel.DisplayRectangle.Bottom));
            }
            else if (HoverItem.RentalInformation?.RentalLocked == true && HoverItem.RentalInformation.ExpiryDate > CMain.Now)
            {
                count++;
                var remainingTime = (HoverItem.RentalInformation.ExpiryDate - CMain.Now).TotalSeconds;
                var RentalLockLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.DarkKhaki,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = remainingTime > 0 ? string.Format("物品租赁截至日期 {0}", Functions.PrintTimeSpanFromSeconds(remainingTime)) : "到期失效"
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, RentalLockLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, RentalLockLabel.DisplayRectangle.Bottom));
            }

            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                #region OUTLINE
                MirControl outLine = new MirControl
                {
                    BackColour = Color.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = Color.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;
                #endregion

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl OverlapInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfo realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            int count = 0;


            #region GEM

            if (realItem.Type == ItemType.宝玉神珠)
            {
                string text = "";

                switch (realItem.Shape)
                {
                    case 1:
                        text = "按CTRL+鼠标左键修复物品";
                        break;
                    case 2:
                        text = "按CTRL+鼠标左键修复物品";
                        break;
                    case 3:
                        text = "按CTRL+鼠标左键使用宝玉";
                        break;
                    case 4:
                        text = "按CTRL+鼠标左键使用神珠";
                        break;
                    case 7:
                        text = "按CTRL+鼠标左键增加嵌孔";
                        break;
                    case 8:
                        text = "按CTRL+鼠标左键物品上锁";
                        break;
                }
                count++;
                MirLabel GEMLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GEMLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, GEMLabel.DisplayRectangle.Bottom));
            }

            #endregion

            #region SPLITUP

            if (realItem.StackSize > 1 && realItem.Type != ItemType.宝玉神珠)
            {
                count++;
                MirLabel SPLITUPLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format(GameLanguage.MaxCombine, realItem.StackSize, "\n")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, SPLITUPLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, SPLITUPLabel.DisplayRectangle.Bottom));
            }

            #endregion

            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                #region OUTLINE
                MirControl outLine = new MirControl
                {
                    BackColour = Color.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = Color.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;
                #endregion

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl StoryInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfo realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            int count = 0;

            #region TOOLTIP

            if (realItem.Type == ItemType.卷轴 && realItem.Shape == 7)//Credit Scroll
            {
                HoverItem.Info.ToolTip = string.Format("添加 {0} 贷记到你的账户上", HoverItem.Info.Price);
            }

            if (!string.IsNullOrEmpty(HoverItem.Info.ToolTip))
            {
                count++;

                MirLabel IDLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.DarkKhaki,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = GameLanguage.ItemDescription
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, IDLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, IDLabel.DisplayRectangle.Bottom));

                MirLabel TOOLTIPLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Khaki,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = HoverItem.Info.ToolTip
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, TOOLTIPLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, TOOLTIPLabel.DisplayRectangle.Bottom));
            }

            #endregion
     
            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                #region OUTLINE
                MirControl outLine = new MirControl
                {
                    BackColour = Color.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = Color.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;
                #endregion

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }

        public MirControl GMMadeLabel(UserItem item)
        {
            if (item.GMMade)
            {


                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                MirLabel GMLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Orchid,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = "制作:游戏管理员"
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GMLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height + 4, GMLabel.DisplayRectangle.Bottom + 4));

                MirControl outLine = new MirControl
                {
                    BackColour = Color.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = Color.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;

                return outLine;
            }
            else
            {
                return null;
            }
        }

        public void CreateItemLabel(UserItem item, bool inspect = false, bool hideDura = false, bool hideAdded = false)
        {
            CMain.DebugText = CMain.Random.Next(1, 100).ToString();

            if (item == null || HoverItem != item)
            {
                DisposeItemLabel();

                if (item == null)
                {
                    HoverItem = null;
                    return;
                }
            }

            if (item == HoverItem && ItemLabel != null && !ItemLabel.IsDisposed) return;
            ushort level = inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfo realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

             ItemLabel = new MirControl
            {
                BackColour = Color.FromArgb(255, 0, 0, 0),
                Border = true,
                BorderColour = ((HoverItem.CurrentDura == 0 && HoverItem.MaxDura != 0) ? Color.Red : Color.FromArgb(255, 148, 146, 148)),
                DrawControlTexture = true,
                NotControl = true,
                Parent = this,
                Opacity = 0.8F
            };

            //Name Info Label
            MirControl[] outlines = new MirControl[12];
            outlines[0] = NameInfoLabel(item, inspect, hideDura);
            //Attribute Info1 Label - Attack Info
            outlines[1] = AttackInfoLabel(item, inspect, hideAdded);
            //Attribute Info2 Label - Defence Info
            outlines[2] = DefenceInfoLabel(item, inspect, hideAdded);
            //Attribute Info3 Label - Weight Info
            outlines[3] = WeightInfoLabel(item, inspect);
            //Awake Info Label
            outlines[4] = AwakeInfoLabel(item, inspect);
            //Socket Info Label
            outlines[5] = SocketInfoLabel(item, inspect);
            //need Info Label
            outlines[6] = NeedInfoLabel(item, inspect);
            //Bind Info Label
            outlines[7] = BindInfoLabel(item, inspect, hideAdded);
            //Overlap Info Label
            outlines[8] = OverlapInfoLabel(item, inspect);
            //Story Label
            outlines[9] = StoryInfoLabel(item, inspect);
            //GM Made
            outlines[10] = GMMadeLabel(item);
			//Info Label
            outlines[11] = ItemSetInfoLabel(item, inspect);

            foreach (var outline in outlines)
            {
                if (outline != null)
                {
                    outline.Size = new Size(ItemLabel.Size.Width, outline.Size.Height);
                }
            }

            //ItemLabel.Visible = true;
        }
        public void CreateMailLabel(ClientMail mail)
        {
            if (mail == null)
            {
                DisposeMailLabel();
                return;
            }

            if (MailLabel != null && !MailLabel.IsDisposed) return;

            MailLabel = new MirControl
            {
                BackColour = Color.FromArgb(255, 50, 50, 50),
                Border = true,
                BorderColour = Color.Gray,
                DrawControlTexture = true,
                NotControl = true,
                Parent = this,
                Opacity = 0.7F
            };

            MirLabel nameLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.Yellow,
                Location = new Point(4, 4),
                OutLine = true,
                Parent = MailLabel,
                Text = mail.SenderName
            };

            MailLabel.Size = new Size(Math.Max(MailLabel.Size.Width, nameLabel.DisplayRectangle.Right + 4),
                Math.Max(MailLabel.Size.Height, nameLabel.DisplayRectangle.Bottom));

            MirLabel dateLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.White,
                Location = new Point(4, MailLabel.DisplayRectangle.Bottom),
                OutLine = true,
                Parent = MailLabel,
                Text = string.Format(GameLanguage.DateSent, mail.DateSent.ToString("dd/MM/yy H:mm:ss"))
            };

            MailLabel.Size = new Size(Math.Max(MailLabel.Size.Width, dateLabel.DisplayRectangle.Right + 4),
                Math.Max(MailLabel.Size.Height, dateLabel.DisplayRectangle.Bottom));

            if (mail.Gold > 0)
            {
                MirLabel goldLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.White,
                    Location = new Point(4, MailLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = MailLabel,
                    Text = "金币: " + mail.Gold
                };

                MailLabel.Size = new Size(Math.Max(MailLabel.Size.Width, goldLabel.DisplayRectangle.Right + 4),
                Math.Max(MailLabel.Size.Height, goldLabel.DisplayRectangle.Bottom));
            }

            MirLabel openedLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.Red,
                Location = new Point(4, MailLabel.DisplayRectangle.Bottom),
                OutLine = true,
                Parent = MailLabel,
                Text = mail.Opened ? "[Old]" : "[New]"
            };

            MailLabel.Size = new Size(Math.Max(MailLabel.Size.Width, openedLabel.DisplayRectangle.Right + 4),
            Math.Max(MailLabel.Size.Height, openedLabel.DisplayRectangle.Bottom));
        }
        public void CreateMemoLabel(ClientFriend friend)
        {
            if (friend == null)
            {
                DisposeMemoLabel();
                return;
            }

            if (MemoLabel != null && !MemoLabel.IsDisposed) return;

            MemoLabel = new MirControl
            {
                BackColour = Color.FromArgb(255, 50, 50, 50),
                Border = true,
                BorderColour = Color.Gray,
                DrawControlTexture = true,
                NotControl = true,
                Parent = this,
                Opacity = 0.7F
            };

            MirLabel memoLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.White,
                Location = new Point(4, 4),
                OutLine = true,
                Parent = MemoLabel,
                Text = Functions.StringOverLines(friend.Memo, 5, 20)
            };

            MemoLabel.Size = new Size(Math.Max(MemoLabel.Size.Width, memoLabel.DisplayRectangle.Right + 4),
                Math.Max(MemoLabel.Size.Height, memoLabel.DisplayRectangle.Bottom + 4));
        }

        public static ItemInfo GetInfo(int index)
        {
            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                ItemInfo info = ItemInfoList[i];
                if (info.Index != index) continue;
                return info;
            }

            return null;
        }

        public string GetUserName(uint id)
        {
            for (int i = 0; i < UserIdList.Count; i++)
            {
                UserId who = UserIdList[i];
                if (id == who.Id)
                    return who.UserName;
            }
            Network.Enqueue(new C.RequestUserName { UserID = id });
            UserIdList.Add(new UserId() { Id = id, UserName = "Unknown" });
            return "";
        }

        public class UserId
        {
            public long Id = 0;
            public string UserName = "";
        }

        public class OutPutMessage
        {
            public string Message;
            public long ExpireTime;
            public OutputMessageType Type;
        }

        public void Rankings(S.Rankings p)
        {
            foreach (RankCharacterInfo info in p.ListingDetails)
            {
                if (RankingList.ContainsKey(info.PlayerId))
                    RankingList[info.PlayerId] = info;
                else
                    RankingList.Add(info.PlayerId, info);
            }
            List<RankCharacterInfo> listings = new List<RankCharacterInfo>();
            foreach (long id in p.Listings)
                listings.Add(RankingList[id]);

            RankingDialog.RecieveRanks(listings, p.RankType, p.MyRank, p.Count);
        }

        public void Opendoor(S.Opendoor p)
        {
            MapControl.OpenDoor(p.DoorIndex, p.Close);
        }

        private void RentedItems(S.GetRentedItems p)
        {
            ItemRentalDialog.ReceiveRentedItems(p.RentedItems);
        }

        private void ItemRentalRequest(S.ItemRentalRequest p)
        {
            if (!p.Renting)
            {
                GuestItemRentDialog.SetGuestName(p.Name);
                ItemRentingDialog.OpenItemRentalDialog();
            }
            else
            {
                GuestItemRentingDialog.SetGuestName(p.Name);
                ItemRentDialog.OpenItemRentDialog();
            }

            ItemRentalDialog.Visible = false;
        }

        private void ItemRentalFee(S.ItemRentalFee p)
        {
            GuestItemRentDialog.SetGuestFee(p.Amount);
            ItemRentDialog.RefreshInterface();
        }

        private void ItemRentalPeriod(S.ItemRentalPeriod p)
        {
            GuestItemRentingDialog.GuestRentalPeriod = p.Days;
            ItemRentingDialog.RefreshInterface();
        }

        private void DepositRentalItem(S.DepositRentalItem p)
        {
            var fromCell = p.From < User.BeltIdx ? BeltDialog.Grid[p.From] : InventoryDialog.Grid[p.From - User.BeltIdx];
            var toCell = ItemRentingDialog.ItemCell;

            if (toCell == null || fromCell == null)
                return;
 
            toCell.Locked = false;
            fromCell.Locked = false;
         
            if (!p.Success)
                return;

            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            User.RefreshStats();

            if (ItemRentingDialog.RentalPeriod == 0)
                ItemRentingDialog.InputRentalPeroid();
        }

        private void RetrieveRentalItem(S.RetrieveRentalItem p)
        {
            var fromCell = ItemRentingDialog.ItemCell;
            var toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];

            if (toCell == null || fromCell == null)
                return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success)
                return;

            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            User.RefreshStats();
        }

        private void UpdateRentalItem(S.UpdateRentalItem p)
        {
            GuestItemRentingDialog.GuestLoanItem = p.LoanItem;
            ItemRentDialog.RefreshInterface();
        }

        private void CancelItemRental(S.CancelItemRental p)
        {
            User.RentalGoldLocked = false;
            User.RentalItemLocked = false;

            ItemRentingDialog.Reset();
            ItemRentDialog.Reset();

            var messageBox = new MirMessageBox("取消物品租赁\r\n" +
                                               "要完成物品租赁，在整个交易过程中面对对方");
            messageBox.Show();
        }

        private void ItemRentalLock(S.ItemRentalLock p)
        {
            if (!p.Success)
                return;
            
            User.RentalGoldLocked = p.GoldLocked;
            User.RentalItemLocked = p.ItemLocked;

            if (User.RentalGoldLocked)
                ItemRentDialog.Lock();
            else if (User.RentalItemLocked)
                ItemRentingDialog.Lock();
        }

        private void ItemRentalPartnerLock(S.ItemRentalPartnerLock p)
        {
            if (p.GoldLocked)
                GuestItemRentDialog.Lock();
            else if (p.ItemLocked)
                GuestItemRentingDialog.Lock();
        }

        private void CanConfirmItemRental(S.CanConfirmItemRental p)
        {
            ItemRentingDialog.EnableConfirmButton();
        }

        private void ConfirmItemRental(S.ConfirmItemRental p)
        {
            User.RentalGoldLocked = false;
            User.RentalItemLocked = false;

            ItemRentingDialog.Reset();
            ItemRentDialog.Reset();
        }

        private void OpenBrowser(S.OpenBrowser p)
        {
            BrowserHelper.OpenDefaultBrowser(p.Url);
        }

        public void PlaySound(S.PlaySound p)
        {
            SoundManager.PlaySound(p.Sound, false);
        }
        private void SetTimer(S.SetTimer p)
        {
            GameScene.Scene.TimerControl.AddTimer(p);
        }

        private void ExpireTimer(S.ExpireTimer p)
        {
            GameScene.Scene.TimerControl.ExpireTimer(p.Key);
        }

        private void SetCompass(S.SetCompass p)
        {
            GameScene.Scene.CompassControl.SetPoint(p.Location);
        }

        /// <summary>
        /// 服务器回执的辅助开关（免蜡 / 穿人 / 免助跑 / 超负重 / 泰山）状态。
        /// Allowed 为 false 时表示本服未开放该功能，界面会置灰并提示。
        /// </summary>
        private void PlayerOption(S.PlayerOption p)
        {
            switch (p.Option)
            {
                case PlayerOptionType.NoLamp:
                    GameScene.NoLampAllowed = p.Allowed;
                    Settings.NoLamp = p.Value;
                    break;
                case PlayerOptionType.WalkThrough:
                    GameScene.WalkThroughAllowed = p.Allowed;
                    Settings.WalkThrough = p.Value;
                    break;
                case PlayerOptionType.NoRunUp:
                    GameScene.NoRunUpAllowed = p.Allowed;
                    Settings.NoRunUp = p.Value;
                    break;
                case PlayerOptionType.OverWeight:
                    GameScene.OverWeightAllowed = p.Allowed;
                    Settings.OverWeight = p.Value;
                    break;
                case PlayerOptionType.MountTai:
                    GameScene.MountTaiAllowed = p.Allowed;
                    Settings.MountTai = p.Value;
                    break;
            }

            Settings.Save();

            // 登录瞬间地图可能还没建好（MapControl 由 MapInformation 创建），必须判空
            if (MapControl != null && !MapControl.IsDisposed)
                MapControl.TextureValid = false;

            if (AutoPlayDialog != null)
                AutoPlayDialog.UpdateState();
        }

        /// <summary>向服务器申请切换辅助开关（最终状态以服务器回执为准）</summary>
        public static void RequestPlayerOption(PlayerOptionType option, bool value)
        {
            // 记下玩家偏好：服务器每次登录都会把这些开关重置为关闭，进图后据此自动恢复
            Settings.SetPreferred(option, value);
            Network.Enqueue(new C.SetPlayerOption { Option = option, Value = value });
        }

        /// <summary>
        /// 服务器在登录时会把各辅助开关统一下发为「关闭」，若就此作罢，
        /// 玩家每次上线都得手动重开。这里在进图稳定后，按本地偏好把
        /// 「服务器已开放 且 玩家原本勾选」的开关重新申请一遍
        /// （是否生效仍以服务器回执为准，未开放的功能不会被反复申请）。
        /// </summary>
        private void RestorePlayerOptions()
        {
            if (_playerOptionsRestored) return;

            // 等服务器登录时那批 S.PlayerOption 先处理完（它们紧跟在 S.StartGame 之后下发）
            if (unchecked(Environment.TickCount - _playerOptionsRestoreTime) < 0) return;
            _playerOptionsRestored = true;

            if (Settings.PreferredNoLamp && NoLampAllowed) RequestPlayerOption(PlayerOptionType.NoLamp, true);
            if (Settings.PreferredWalkThrough && WalkThroughAllowed) RequestPlayerOption(PlayerOptionType.WalkThrough, true);
            if (Settings.PreferredNoRunUp && NoRunUpAllowed) RequestPlayerOption(PlayerOptionType.NoRunUp, true);
            if (Settings.PreferredOverWeight && OverWeightAllowed) RequestPlayerOption(PlayerOptionType.OverWeight, true);
            if (Settings.PreferredMountTai && MountTaiAllowed) RequestPlayerOption(PlayerOptionType.MountTai, true);
        }

        private void Roll(S.Roll p)
        {
            GameScene.Scene.RollControl.Setup(p.Type, p.Page, p.Result, p.AutoRoll);
        }

        public void ShowNotice(S.UpdateNotice p)
        {
            NoticeDialog.Update(p.Notice);
        }

        #region Disposable

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Scene = null;
                User = null;

                MoveTime = 0;
                AttackTime = 0;
                NextRunTime = 0;
                LastRunTime = 0;
                CanMove = false;
                CanRun = false;

                MapControl = null;
                MainDialog = null;
                ChatDialog = null;
                ChatControl = null;
                InventoryDialog = null;
                CharacterDialog = null;
                StorageDialog = null;
                BeltDialog = null;
                MiniMapDialog = null;
                InspectDialog = null;
                OptionDialog = null;
                MenuDialog = null;
                NPCDialog = null;
                QuestDetailDialog = null;
                QuestListDialog = null;
                QuestLogDialog = null;
                QuestTrackingDialog = null;
                GameShopDialog = null;
                MentorDialog = null;

                NewHeroDialog = null;
                HeroInventoryDialog = null;
                HeroDialog = null;
                HeroBeltDialog = null;

                RelationshipDialog = null;
                CharacterDuraPanel = null;
                DuraStatusPanel = null;

                HoverItem = null;
                SelectedCell = null;
                PickedUpGold = false;

                UseItemTime = 0;
                PickUpTime = 0;
                InspectTime = 0;

                DisposeItemLabel();

                AMode = 0;
                PMode = 0;
                Lights = 0;

                NPCTime = 0;
                NPCID = 0;
                DefaultNPCID = 0;

                for (int i = 0; i < OutputLines.Length; i++)
                    if (OutputLines[i] != null && OutputLines[i].IsDisposed)
                        OutputLines[i].Dispose();

                OutputMessages.Clear();
                OutputMessages = null;
            }

            base.Dispose(disposing);
        }

        #endregion

    }

    public sealed class MapControl : MirControl
    {
        public static UserObject User
        {
            get { return MapObject.User; }
            set { MapObject.User = value; }
        }

        public static UserHeroObject Hero
        {
            get { return MapObject.Hero; }
            set { MapObject.Hero = value; }
        }

        public static List<MapObject> Objects = new List<MapObject>();

        public const int CellWidth = 48;
        public const int CellHeight = 32;

        public static int OffSetX;
        public static int OffSetY;

        public static int ViewRangeX;
        public static int ViewRangeY;

        private bool _autoPath;
        public bool AutoPath
        {
            get
            {
                return _autoPath;
            }
            set
            {
                if (_autoPath == value) return;
                _autoPath = value;

                if (!_autoPath)
                    CurrentPath = null;
            }
        }

        public PathFinder PathFinder;
        public List<Node> CurrentPath = null;

        public static Point MapLocation
        {
            get { return GameScene.User == null ? Point.Empty : new Point(MouseLocation.X / CellWidth - OffSetX, MouseLocation.Y / CellHeight - OffSetY).Add(GameScene.User.CurrentLocation); }
        }

        public static Point ToMouseLocation(Point p)
        {
            return new Point((p.X - MapObject.User.Movement.X + OffSetX) * CellWidth, (p.Y - MapObject.User.Movement.Y + OffSetY) * CellHeight).Add(MapObject.User.OffSetMove);
        }

        public static MouseButtons MapButtons;
        public static Point MouseLocation;
        public static long InputDelay;

        private static long nextAction;
        public static long NextAction
        {
            get { return nextAction; }
            set
            {
                if (GameScene.Observing) return;
                nextAction = value;
            }
        }

        public CellInfo[,] M2CellInfo;
        public List<Door> Doors = new List<Door>();
        public int Width, Height;

        public int Index;
        public string FileName = String.Empty;
        public string Title = String.Empty;
        public ushort MiniMap, BigMap, Music, SetMusic;
        public LightSetting Lights;
        public bool Lightning, Fire;
        public byte MapDarkLight;
        public long LightningTime, FireTime;
        public WeatherSetting Weather = WeatherSetting.无效果;
        public bool FloorValid, LightsValid;

        public long OutputDelay;

        private static bool _awakeningAction;
        public static bool AwakeningAction
        {
            get { return _awakeningAction; }
            set
            {
                if (_awakeningAction == value) return;
                _awakeningAction = value; 
            }
        }

        private static bool _autoRun;
        public static bool AutoRun
        {
            get { return _autoRun; }
            set
            {
                if (_autoRun == value) return;
                _autoRun = value;
                if (GameScene.Scene != null)
                    GameScene.Scene.ChatDialog.ReceiveChat(value ? "[自动行走: 开]" : "[自动行走: 关]", ChatType.Hint);
            }

        }
        public static bool AutoHit;

        public int AnimationCount;
        
        public static List<Effect> Effects = new List<Effect>();

        public MapControl()
        {
            MapButtons = MouseButtons.None;

            OffSetX = Settings.ScreenWidth / 2 / CellWidth;
            OffSetY = Settings.ScreenHeight / 2 / CellHeight - 1;

            ViewRangeX = OffSetX + 6;
            ViewRangeY = OffSetY + 6;

            Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);
            DrawControlTexture = true;
            BackColour = Color.Black;

            MouseDown += OnMouseDown;
            MouseMove += (o, e) => MouseLocation = e.Location;
            Click += OnMouseClick;
        }

        public void ResetMap()
        {
            GameScene.Scene.NPCDialog.Hide();

            MapObject.MouseObjectID = 0;
            MapObject.TargetObjectID = 0;
            MapObject.MagicObjectID = 0;

            if (M2CellInfo != null)
            {
                for (var i = Objects.Count - 1; i >= 0; i--)
                {
                    var obj = Objects[i];
                    if (obj == null) continue;

                    obj.Remove();
                }
            }

            Objects.Clear();
            Effects.Clear();
            Doors.Clear();

            if (User != null)
                Objects.Add(User);
        }

        public void LoadMap()
        {
            ResetMap();

            MapObject.MouseObjectID = 0;
            MapObject.TargetObjectID = 0;
            MapObject.MagicObjectID = 0;

            MapReader Map = new MapReader(FileName);
            M2CellInfo = Map.MapCells;
            Width = Map.Width;
            Height = Map.Height;

            PathFinder = new PathFinder(this);

            try
            {
                if (SetMusic != Music)
                {
                    SoundManager.Music?.Dispose();
                    SoundManager.PlayMusic(Music, true);
                }
            }
            catch (Exception)
            {
                // Do nothing. index was not valid.
            }

            SetMusic = Music;
            SoundList.Music = Music;

            UpdateWeather();
        }


        public void Process()
        {
            Processdoors();
            User.Process();
            for (int i = Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = Objects[i];
                if (ob == User) continue;
                //  if (ob.ActionFeed.Count > 0 || ob.Effects.Count > 0 || GameScene.CanMove || CMain.Time >= ob.NextMotion)
                ob.Process();
            }

            for (int i = Effects.Count - 1; i >= 0; i--)
                Effects[i].Process();

            if (MapObject.TargetObject != null && MapObject.TargetObject is MonsterObject && MapObject.TargetObject.AI == 970)
                MapObject.TargetObjectID = 0;
            if (MapObject.MagicObject != null && MapObject.MagicObject is MonsterObject && MapObject.MagicObject.AI == 970)
                MapObject.MagicObjectID = 0;

            ProcessAutoPlay();

            // 毒符互换（手动）：玩家自己按技能时因为要先换符/换毒而被推迟的那次施法，材料到位后自动补放
            AutoPlayRetryManualSpell();

            CheckInput();


            MapObject bestmouseobject = null;
            for (int y = MapLocation.Y + 2; y >= MapLocation.Y - 2; y--)
            {
                if (y >= Height) continue;
                if (y < 0) break;
                for (int x = MapLocation.X + 2; x >= MapLocation.X - 2; x--)
                {
                    if (x >= Width) continue;
                    if (x < 0) break;
                    CellInfo cell = M2CellInfo[x, y];
                    if (cell.CellObjects == null) continue;

                    for (int i = cell.CellObjects.Count - 1; i >= 0; i--)
                    {
                        MapObject ob = cell.CellObjects[i];
                        if (ob == MapObject.User || !ob.MouseOver(CMain.MPoint)) continue;

                        if (MapObject.MouseObject != ob)
                        {
                            if (ob.Dead)
                            {
                                if (!Settings.TargetDead && GameScene.TargetDeadTime <= CMain.Time) continue;

                                bestmouseobject = ob;
                                //continue;
                            }
                            MapObject.MouseObjectID = ob.ObjectID;
                            Redraw();
                        }
                        if (bestmouseobject != null && MapObject.MouseObject == null)
                        {
                            MapObject.MouseObjectID = bestmouseobject.ObjectID;
                            Redraw();
                        }
                        return;
                    }
                }
            }


            if (MapObject.MouseObject != null)
            {
                MapObject.MouseObjectID = 0;
                Redraw();
            }
        }

        public static MapObject GetObject(uint targetID)
        {
            for (int i = 0; i < Objects.Count; i++)
            {
                MapObject ob = Objects[i];
                if (ob.ObjectID != targetID) continue;
                return ob;
            }
            return null;
        }

        public override void Draw()
        {
            //Do nothing.
        }

        protected override void CreateTexture()
        {
            if (User == null) return;

            if (!FloorValid)
                DrawFloor();


            if (Size != TextureSize)
                DisposeTexture();

            if (ControlTexture == null || ControlTexture.Disposed)
            {
                DXManager.ControlList.Add(this);
                ControlTexture = new Texture(DXManager.Device, Size.Width, Size.Height, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
                TextureSize = Size;
            }

            Surface oldSurface = DXManager.CurrentSurface;
            Surface surface = ControlTexture.GetSurfaceLevel(0);
            DXManager.SetSurface(surface);
            DXManager.Device.Clear(ClearFlags.Target, BackColour, 0, 0);

            DrawBackground();

            if (FloorValid)
            {
                DXManager.Draw(DXManager.FloorTexture, new Rectangle(0, 0, Settings.ScreenWidth, Settings.ScreenHeight), Vector3.Zero, Color.White);
            }

            DrawObjects();

            //render weather
            foreach (ParticleEngine engine in GameScene.Scene.ParticleEngines)
            {
                engine.Draw();
            }

            //Render Death, 

            LightSetting setting = Lights == LightSetting.Normal ? GameScene.Scene.Lights : Lights;

            // 免蜡：开启后不再绘制夜晚黑暗层（失明效果仍然保留）
            bool blinded = GameScene.User.Poison.HasFlag(PoisonType.Blindness);

            if (blinded || (setting != LightSetting.Day && !Settings.NoLamp))
            {
                DrawLights(setting);
            }

            if (Settings.DropView || GameScene.DropViewTime > CMain.Time)
            {
                for (int i = 0; i < Objects.Count; i++)
                {
                    ItemObject ob = Objects[i] as ItemObject;
                    if (ob == null) continue;

                    if (!ob.MouseOver(MouseLocation))
                        ob.DrawName();
                }
            }

            if (MapObject.MouseObject != null && !(MapObject.MouseObject is ItemObject))
                MapObject.MouseObject.DrawName();

            int offSet = 0; 
            
            if (Settings.DisplayBodyName)
            {
                for (int i = 0; i < Objects.Count; i++)
                {
                    MonsterObject ob = Objects[i] as MonsterObject;
                    if (ob == null) continue;

                    if (!ob.MouseOver(MouseLocation)) continue;
                    ob.DrawName();
                }
            }

            for (int i = 0; i < Objects.Count; i++)
            {
                ItemObject ob = Objects[i] as ItemObject;
                if (ob == null) continue;

                if (!ob.MouseOver(MouseLocation)) continue;
                ob.DrawName(offSet);
                offSet -= ob.NameLabel.Size.Height + (ob.NameLabel.Border ? 1 : 0);
            }

            if (MapObject.User.MouseOver(MouseLocation))
                MapObject.User.DrawName();

            DXManager.SetSurface(oldSurface);
            surface.Dispose();
            TextureValid = true;

        }
        protected internal override void DrawControl()
        {
            if (!DrawControlTexture)
                return;

            if (!TextureValid)
                CreateTexture();

            if (ControlTexture == null || ControlTexture.Disposed)
                return;

            float oldOpacity = DXManager.Opacity;

            if (MapObject.User.Dead) DXManager.SetGrayscale(true);

            DXManager.DrawOpaque(ControlTexture, new Rectangle(0, 0, Settings.ScreenWidth, Settings.ScreenHeight), Vector3.Zero, Color.White, Opacity);

            if (MapObject.User.Dead) DXManager.SetGrayscale(false);

            CleanTime = CMain.Time + Settings.CleanDelay;
        }

        private void DrawFloor()
        {
            if (DXManager.FloorTexture == null || DXManager.FloorTexture.Disposed)
            {
                DXManager.FloorTexture = new Texture(DXManager.Device, Settings.ScreenWidth, Settings.ScreenHeight, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
                DXManager.FloorSurface = DXManager.FloorTexture.GetSurfaceLevel(0);
            }


            Surface oldSurface = DXManager.CurrentSurface;

            DXManager.SetSurface(DXManager.FloorSurface);
            DXManager.Device.Clear(ClearFlags.Target, Color.Empty, 0, 0); //Color.Black

            int index;
            int drawY, drawX;

            for (int y = User.Movement.Y - ViewRangeY; y <= User.Movement.Y + ViewRangeY; y++)
            {
                if (y <= 0 || y % 2 == 1) continue;
                if (y >= Height) break;
                drawY = (y - User.Movement.Y + OffSetY) * CellHeight + User.OffSetMove.Y; //Moving OffSet

                for (int x = User.Movement.X - ViewRangeX; x <= User.Movement.X + ViewRangeX; x++)
                {
                    if (x <= 0 || x % 2 == 1) continue;
                    if (x >= Width) break;
                    drawX = (x - User.Movement.X + OffSetX) * CellWidth - OffSetX + User.OffSetMove.X; //Moving OffSet
                    if ((M2CellInfo[x, y].BackImage == 0) || (M2CellInfo[x, y].BackIndex == -1)) continue;
                    index = (M2CellInfo[x, y].BackImage & 0x1FFFFFFF) - 1;
                    Libraries.MapLibs[M2CellInfo[x, y].BackIndex].Draw(index, drawX, drawY);
                }
            }

            for (int y = User.Movement.Y - ViewRangeY; y <= User.Movement.Y + ViewRangeY + 5; y++)
            {
                if (y <= 0) continue;
                if (y >= Height) break;
                drawY = (y - User.Movement.Y + OffSetY) * CellHeight + User.OffSetMove.Y; //Moving OffSet

                for (int x = User.Movement.X - ViewRangeX; x <= User.Movement.X + ViewRangeX; x++)
                {
                    if (x < 0) continue;
                    if (x >= Width) break;
                    drawX = (x - User.Movement.X + OffSetX) * CellWidth - OffSetX + User.OffSetMove.X; //Moving OffSet

                    index = M2CellInfo[x, y].MiddleImage - 1;

                    if ((index < 0) || (M2CellInfo[x, y].MiddleIndex == -1)) continue;
                    if (M2CellInfo[x, y].MiddleIndex >= 0)    //M2P '> 199' changed to '>= 0' to include mir2 libraries. Fixes middle layer tile strips draw. Also changed in 'Draw mir3 middle layer' bellow.
                    {//mir3 mid layer is same level as front layer not real middle + it cant draw index -1 so 2 birds in one stone :p
                        Size s = Libraries.MapLibs[M2CellInfo[x, y].MiddleIndex].GetSize(index);

                        if (s.Width != CellWidth || s.Height != CellHeight) continue;
                    }
                    Libraries.MapLibs[M2CellInfo[x, y].MiddleIndex].Draw(index, drawX, drawY);
                }
            }
            for (int y = User.Movement.Y - ViewRangeY; y <= User.Movement.Y + ViewRangeY + 5; y++)
            {
                if (y <= 0) continue;
                if (y >= Height) break;
                drawY = (y - User.Movement.Y + OffSetY) * CellHeight + User.OffSetMove.Y; //Moving OffSet

                for (int x = User.Movement.X - ViewRangeX; x <= User.Movement.X + ViewRangeX; x++)
                {
                    if (x < 0) continue;
                    if (x >= Width) break;
                    drawX = (x - User.Movement.X + OffSetX) * CellWidth - OffSetX + User.OffSetMove.X; //Moving OffSet

                    index = (M2CellInfo[x, y].FrontImage & 0x7FFF) - 1;
                    if (index == -1) continue;
                    int fileIndex = M2CellInfo[x, y].FrontIndex;
                    if (fileIndex == -1) continue;
                    Size s = Libraries.MapLibs[fileIndex].GetSize(index);
                    if (fileIndex == 200) continue; //fixes random bad spots on old school 4.map
                    if (M2CellInfo[x, y].DoorIndex > 0)
                    {
                        Door DoorInfo = GetDoor(M2CellInfo[x, y].DoorIndex);
                        if (DoorInfo == null)
                        {
                            DoorInfo = new Door() { index = M2CellInfo[x, y].DoorIndex, DoorState = 0, ImageIndex = 0, LastTick = CMain.Time };
                            Doors.Add(DoorInfo);
                        }
                        else
                        {
                            if (DoorInfo.DoorState != 0)
                            {
                                index += (DoorInfo.ImageIndex + 1) * M2CellInfo[x, y].DoorOffset;//'bad' code if you want to use animation but it's gonna depend on the animation > has to be custom designed for the animtion
                            }
                        }
                    }

                    if (index < 0 || ((s.Width != CellWidth || s.Height != CellHeight) && ((s.Width != CellWidth * 2) || (s.Height != CellHeight * 2)))) continue;
                    Libraries.MapLibs[fileIndex].Draw(index, drawX, drawY);
                }
            }

            DXManager.SetSurface(oldSurface);

            FloorValid = true;
        }

        private void DrawBackground()
        {
            string cleanFilename = FileName.Replace(Settings.MapPath, "");

            if(cleanFilename.StartsWith("ID1") || cleanFilename.StartsWith("ID2"))
            {
                Libraries.Background.Draw(10, 0, 0); //mountains
            }
            else if(cleanFilename.StartsWith("ID3_013"))
            {
                Libraries.Background.Draw(22, 0, 0); //desert
            }
            else if (cleanFilename.StartsWith("ID3_015"))
            {
                Libraries.Background.Draw(23, 0, 0); //greatwall
            }
            else if (cleanFilename.StartsWith("ID3_023") || cleanFilename.StartsWith("ID3_025"))
            {
                Libraries.Background.Draw(21, 0, 0); //village entrance
            }
        }

        private void DrawObjects()
        {
            if (Settings.Effect)
            {
                for (int i = Effects.Count - 1; i >= 0; i--)
                {
                    if (!Effects[i].DrawBehind) continue;
                    Effects[i].Draw();
                }
            }

            for (int y = User.Movement.Y - ViewRangeY; y <= User.Movement.Y + ViewRangeY + 25; y++)
            {
                if (y <= 0) continue;
                if (y >= Height) break;
                for (int x = User.Movement.X - ViewRangeX; x <= User.Movement.X + ViewRangeX; x++)
                {
                    if (x < 0) continue;
                    if (x >= Width) break;
                    M2CellInfo[x, y].DrawDeadObjects();
                }
            }

            for (int y = User.Movement.Y - ViewRangeY; y <= User.Movement.Y + ViewRangeY + 25; y++)
            {
                if (y <= 0) continue;
                if (y >= Height) break;
                int drawY = (y - User.Movement.Y + OffSetY + 1) * CellHeight + User.OffSetMove.Y;

                for (int x = User.Movement.X - ViewRangeX; x <= User.Movement.X + ViewRangeX; x++)
                {
                    if (x < 0) continue;
                    if (x >= Width) break;
                    int drawX = (x - User.Movement.X + OffSetX) * CellWidth - OffSetX + User.OffSetMove.X;
                    int index;
                    int backIndex;
                    byte animation;
                    bool blend;
                    Size s;
                    #region Draw shanda's tile animation layer
                    index = M2CellInfo[x, y].TileAnimationImage;
                    animation = M2CellInfo[x, y].TileAnimationFrames;
                    if ((index > 0) & (animation > 0))
                    {
                        index--;
                        int animationoffset = M2CellInfo[x, y].TileAnimationOffset ^ 0x2000;
                        index += animationoffset * (AnimationCount % animation);
                        Libraries.MapLibs[190].DrawUp(index, drawX, drawY);
                    }

                    #endregion

                    #region Draw mir3 middle layer
                    if ((M2CellInfo[x, y].MiddleIndex >= 0) && (M2CellInfo[x, y].MiddleIndex != -1))   //M2P '> 199' changed to '>= 0' to include mir2 libraries. Fixes middle layer tile strips draw. Also changed in 'DrawFloor' above.
                    {
                        index = M2CellInfo[x, y].MiddleImage - 1;
                        if (index > 0)
                        {
                            animation = M2CellInfo[x, y].MiddleAnimationFrame;
                            blend = false;
                            if ((animation > 0) && (animation < 255))
                            {
                                if ((animation & 0x0f) > 0)
                                {
                                    blend = true;
                                    animation &= 0x0f;
                                }
                                if (animation > 0)
                                {
                                    byte animationTick = M2CellInfo[x, y].MiddleAnimationTick;
                                    index += (AnimationCount % (animation + (animation * animationTick))) / (1 + animationTick);

                                    if (blend && (animation == 10 || animation == 8)) //diamond mines, abyss blends
                                    {
                                        Libraries.MapLibs[M2CellInfo[x, y].MiddleIndex].DrawUpBlend(index, new Point(drawX, drawY));
                                    }
                                    else
                                    {
                                        Libraries.MapLibs[M2CellInfo[x, y].MiddleIndex].DrawUp(index, drawX, drawY);
                                    }
                                }
                            }
                            s = Libraries.MapLibs[M2CellInfo[x, y].MiddleIndex].GetSize(index);
                            if ((s.Width != CellWidth || s.Height != CellHeight) && (s.Width != (CellWidth * 2) || s.Height != (CellHeight * 2)) && !blend)
                            {
                                Libraries.MapLibs[M2CellInfo[x, y].MiddleIndex].DrawUp(index, drawX, drawY);
                            }
                        }
                    }
                    #endregion

                    #region Draw front layer
                    index = (M2CellInfo[x, y].FrontImage & 0x7FFF) - 1;
                    backIndex = (M2CellInfo[x, y].BackImage & 0x7FFF) - 1;

                    if (index < 0) continue;

                    int fileIndex = M2CellInfo[x, y].FrontIndex;
                    if (fileIndex == -1) continue;
                    animation = M2CellInfo[x, y].FrontAnimationFrame;

                    if ((animation & 0x80) > 0)
                    {
                        blend = true;
                        animation &= 0x7F;
                    }
                    else
                    {
                        blend = false;
                    }

                    if (animation > 0)
                    {
                        byte animationTick = M2CellInfo[x, y].FrontAnimationTick;
                        index += (AnimationCount % (animation + (animation * animationTick))) / (1 + animationTick);
                    }


                    if (M2CellInfo[x, y].DoorIndex > 0)
                    {
                        Door DoorInfo = GetDoor(M2CellInfo[x, y].DoorIndex);
                        if (DoorInfo == null)
                        {
                            DoorInfo = new Door() { index = M2CellInfo[x, y].DoorIndex, DoorState = 0, ImageIndex = 0, LastTick = CMain.Time };
                            Doors.Add(DoorInfo);
                        }
                        else
                        {
                            if (DoorInfo.DoorState != 0)
                            {
                                index += (DoorInfo.ImageIndex + 1) * M2CellInfo[x, y].DoorOffset;//'bad' code if you want to use animation but it's gonna depend on the animation > has to be custom designed for the animtion
                            }
                        }
                    }
                    s = Libraries.MapLibs[fileIndex].GetSize(index);
                    Point offset = Libraries.MapLibs[fileIndex].GetOffSet(index);

                    if (backIndex == 23175 && index == 7776)
                    {
                        Libraries.MapLibs[fileIndex].Draw(index + 1109, drawX + (2 * CellWidth), drawY - (17 * CellHeight));
                    }
                    if (backIndex == 23081 && index == 7764)
                    {
                        Libraries.MapLibs[fileIndex].Draw(index + 1120, drawX, drawY - (17 * CellHeight));
                    }
                    if (backIndex == 23322 && index == 7623)
                    {
                        Libraries.MapLibs[fileIndex].Draw(index + 1262, drawX, drawY - (21 * CellHeight));
                    }
                    if (backIndex == 18999 && index == 7796)
                    {
                        Libraries.MapLibs[fileIndex].Draw(index + 1069, new Point(drawX + offset.X + (2 * CellWidth), drawY + offset.Y - (21 * CellHeight)), Color.White, true);
                    }

                    if (s.Width == CellWidth && s.Height == CellHeight && animation == 0) continue;
                    if (s.Width == CellWidth * 2 && s.Height == CellHeight * 2 && animation == 0) continue;

                    if (blend)
                    {
                        if (fileIndex > 0 && fileIndex < 199)
                        {
                            Libraries.MapLibs[fileIndex].DrawBlend(index, new Point(drawX, drawY - (3 * CellHeight)), Color.White, true, 1.0f);
                        }
                    }
                    else
                    {
                        if ((fileIndex == 28) && (animation > 0) && (index >= 7610 && index <= 7617))
                        {
                            Libraries.MapLibs[fileIndex].Draw(index + 1230, new Point(drawX + offset.X, drawY + offset.Y - CellHeight), Color.White, true);
                        }
                        else if ((fileIndex == 28 || fileIndex == 90) && animation > 0)
                        {
                            Libraries.MapLibs[fileIndex].Draw(index, drawX + offset.X, drawY + offset.Y - CellHeight);
                        }
                        else
                        {
                            Libraries.MapLibs[fileIndex].Draw(index, drawX, drawY - s.Height);
                        }
                    }
                    #endregion
                }

                for (int x = User.Movement.X - ViewRangeX; x <= User.Movement.X + ViewRangeX; x++)
                {
                    if (x < 0) continue;
                    if (x >= Width) break;
                    M2CellInfo[x, y].DrawObjects();
                }
            }

            DXManager.Sprite.Flush();
            float oldOpacity = DXManager.Opacity;
            DXManager.SetOpacity(0.4F);

            //MapObject.User.DrawMount();
            MapObject.User.DrawTransform();

            MapObject.User.DrawBody();

            if ((MapObject.User.Direction == MirDirection.Up) ||
                (MapObject.User.Direction == MirDirection.UpLeft) ||
                (MapObject.User.Direction == MirDirection.UpRight) ||
                (MapObject.User.Direction == MirDirection.Right) ||
                (MapObject.User.Direction == MirDirection.Left))
            {
                MapObject.User.DrawHead();
                MapObject.User.DrawWings();
            }
            else
            {
                MapObject.User.DrawWings();
                MapObject.User.DrawHead();
            }

            DXManager.SetOpacity(oldOpacity);

            if (Settings.HighlightTarget)
            {
                if (MapObject.MouseObject != null && !MapObject.MouseObject.Dead && MapObject.MouseObject != MapObject.TargetObject && MapObject.MouseObject.Blend)
                    MapObject.MouseObject.DrawBlend();

                if (MapObject.TargetObject != null)
                    MapObject.TargetObject.DrawBlend();
            }

            for (int i = 0; i < Objects.Count; i++)
            {
                Objects[i].DrawEffects(Settings.Effect);

                if (Settings.NameView && !(Objects[i] is ItemObject) && !Objects[i].Dead)
                    Objects[i].DrawName();

                Objects[i].DrawChat();
                Objects[i].DrawHealth();
                Objects[i].DrawPoison();

                Objects[i].DrawDamages();
            }

            if (Settings.Effect)
            {
                for (int i = Effects.Count - 1; i >= 0; i--)
                {
                    if (Effects[i].DrawBehind) continue;
                    Effects[i].Draw();
                }
            }
        }

        private Color GetBlindLight(Color light)
        {
            if (MapObject.User.BlindTime <= CMain.Time && MapObject.User.BlindCount < 25)
            {
                MapObject.User.BlindTime = CMain.Time + 100;
                MapObject.User.BlindCount++;
            }

            int count = MapObject.User.BlindCount;
            light = Color.FromArgb(255, Math.Max(20, light.R - (count * 10)), Math.Max(20, light.G - (count * 10)), Math.Max(20, light.B - (count * 10)));

            return light;
        }

        private void DrawLights(LightSetting setting)
        {
            if (DXManager.Lights == null || DXManager.Lights.Count == 0) return;

            if (DXManager.LightTexture == null || DXManager.LightTexture.Disposed)
            {
                DXManager.LightTexture = new Texture(DXManager.Device, Settings.ScreenWidth, Settings.ScreenHeight, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
                DXManager.LightSurface = DXManager.LightTexture.GetSurfaceLevel(0);
            }

            Surface oldSurface = DXManager.CurrentSurface;
            DXManager.SetSurface(DXManager.LightSurface);

            #region Night Lights
            Color darkness;

            switch (setting)
            {
                case LightSetting.Night:
                    {
                        switch (MapDarkLight)
                        {
                            case 1:
                                darkness = Color.FromArgb(255, 20, 20, 20);
                                break;
                            case 2:
                                darkness = Color.LightSlateGray;
                                break;
                            case 3:
                                darkness = Color.SkyBlue;
                                break;
                            case 4:
                                darkness = Color.Goldenrod;
                                break;
                            default:
                                darkness = Color.Black;
                                break;
                        }
                    }
                    break;
                case LightSetting.Evening:
                case LightSetting.Dawn:
                    darkness = Color.FromArgb(255, 50, 50, 50);
                    break;
                default:
                case LightSetting.Day:
                    darkness = Color.FromArgb(255, 255, 255, 255);
                    break;
            }

            if (MapObject.User.Poison.HasFlag(PoisonType.Blindness))
            {
                darkness = GetBlindLight(darkness);
            }

            DXManager.Device.Clear(ClearFlags.Target, darkness, 0, 0);

            #endregion

            int light;
            Point p;
            DXManager.SetBlend(true);
            DXManager.Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
            DXManager.Device.SetRenderState(RenderState.DestinationBlend, Blend.One);

            #region Object Lights (Player/Mob/NPC)
            for (int i = 0; i < Objects.Count; i++)
            {
                MapObject ob = Objects[i];
                if (ob.Light > 0 && (!ob.Dead || ob == MapObject.User || ob.Race == ObjectType.Spell))
                {
                    light = ob.Light;

                    int lightRange = light % 15;
                    if (lightRange >= DXManager.Lights.Count)
                        lightRange = DXManager.Lights.Count - 1;

                    p = ob.DrawLocation;

                    Color lightColour = ob.LightColour;

                    if (ob.Race == ObjectType.Player)
                    {
                        switch (light / 15)
                        {
                            case 0://no light source
                                lightColour = Color.FromArgb(255, 60, 60, 60);
                                break;
                            case 1:
                                lightColour = Color.FromArgb(255, 120, 120, 120);
                                break;
                            case 2://Candle
                                lightColour = Color.FromArgb(255, 180, 180, 180);
                                break;
                            case 3://Torch
                                lightColour = Color.FromArgb(255, 240, 240, 240);
                                break;
                            default://Peddler Torch
                                lightColour = Color.FromArgb(255, 255, 255, 255);
                                break;
                        }
                    }
                    else if (ob.Race == ObjectType.Merchant)
                    {
                        lightColour = Color.FromArgb(255, 120, 120, 120);
                    }

                    if (MapObject.User.Poison.HasFlag(PoisonType.Blindness))
                    {
                        lightColour = GetBlindLight(lightColour);
                    }

                    if (DXManager.Lights[lightRange] != null && !DXManager.Lights[lightRange].Disposed)
                    {
                        p.Offset(-(DXManager.LightSizes[lightRange].X / 2) - (CellWidth / 2), -(DXManager.LightSizes[lightRange].Y / 2) - (CellHeight / 2) -5);
                        DXManager.Draw(DXManager.Lights[lightRange], null, new Vector3((float)p.X, (float)p.Y, 0.0F), lightColour);
                    }
                }

                #region Object Effect Lights
                if (!Settings.Effect) continue;
                for (int e = 0; e < ob.Effects.Count; e++)
                {
                    Effect effect = ob.Effects[e];
                    if (!effect.Blend || CMain.Time < effect.Start || (!(effect is Missile) && effect.Light < ob.Light)) continue;

                    light = effect.Light;
                    
                    p = effect.DrawLocation;

                    var lightColour = effect.LightColour;

                    if (MapObject.User.Poison.HasFlag(PoisonType.Blindness))
                    {
                        lightColour = GetBlindLight(lightColour);
                    }

                    if (DXManager.Lights[light] != null && !DXManager.Lights[light].Disposed)
                    {
                        p.Offset(-(DXManager.LightSizes[light].X / 2) - (CellWidth / 2), -(DXManager.LightSizes[light].Y / 2) - (CellHeight / 2) - 5);
                        DXManager.Draw(DXManager.Lights[light], null, new Vector3((float)p.X, (float)p.Y, 0.0F), lightColour);
                    }

                }
                #endregion
            }
            #endregion

            #region Map Effect Lights
            if (Settings.Effect)
            {
                for (int e = 0; e < Effects.Count; e++)
                {
                    Effect effect = Effects[e];
                    if (!effect.Blend || CMain.Time < effect.Start) continue;

                    light = effect.Light;
                    if (light == 0) continue;

                    p = effect.DrawLocation;

                    var lightColour = Color.White;

                    if (MapObject.User.Poison.HasFlag(PoisonType.Blindness))
                    {
                        lightColour = GetBlindLight(lightColour);
                    }

                    if (DXManager.Lights[light] != null && !DXManager.Lights[light].Disposed)
                    {
                        p.Offset(-(DXManager.LightSizes[light].X / 2) - (CellWidth / 2), -(DXManager.LightSizes[light].Y / 2) - (CellHeight / 2) - 5);
                        DXManager.Draw(DXManager.Lights[light], null, new Vector3((float)p.X, (float)p.Y, 0.0F), lightColour);
                    }
                }
            }
            #endregion

            #region Map Lights
            for (int y = MapObject.User.Movement.Y - ViewRangeY - 24; y <= MapObject.User.Movement.Y + ViewRangeY + 24; y++)
            {
                if (y < 0) continue;
                if (y >= Height) break;
                for (int x = MapObject.User.Movement.X - ViewRangeX - 24; x < MapObject.User.Movement.X + ViewRangeX + 24; x++)
                {
                    if (x < 0) continue;
                    if (x >= Width) break;
                    int imageIndex = (M2CellInfo[x, y].FrontImage & 0x7FFF) - 1;
                    if (imageIndex == -1) continue;
                    int fileIndex = M2CellInfo[x, y].FrontIndex;
                    if (fileIndex == -1) continue;
                    if (M2CellInfo[x, y].Light <= 0 || M2CellInfo[x, y].Light >= 10) continue;
                    if (M2CellInfo[x, y].Light == 0) continue;

                    Color lightIntensity;

                    light = (M2CellInfo[x, y].Light % 10) * 3;

                    switch (M2CellInfo[x, y].Light / 10)
                    {
                        case 1:
                            lightIntensity = Color.FromArgb(255, 255, 255, 255);
                            break;
                        case 2:
                            lightIntensity = Color.FromArgb(255, 120, 180, 255);
                            break;
                        case 3:
                            lightIntensity = Color.FromArgb(255, 255, 180, 120);
                            break;
                        case 4:
                            lightIntensity = Color.FromArgb(255, 22, 160, 5);
                            break;
                        default:
                            lightIntensity = Color.FromArgb(255, 255, 255, 255);
                            break;
                    }

                    if (MapObject.User.Poison.HasFlag(PoisonType.Blindness))
                    {
                        lightIntensity = GetBlindLight(lightIntensity);
                    }

                    p = new Point(
                        (x + OffSetX - MapObject.User.Movement.X) * CellWidth + MapObject.User.OffSetMove.X,
                        (y + OffSetY - MapObject.User.Movement.Y) * CellHeight + MapObject.User.OffSetMove.Y + 32);


                    if (M2CellInfo[x, y].FrontAnimationFrame > 0)
                        p.Offset(Libraries.MapLibs[fileIndex].GetOffSet(imageIndex));

                    if (light >= DXManager.Lights.Count)
                        light = DXManager.Lights.Count - 1;

                    if (DXManager.Lights[light] != null && !DXManager.Lights[light].Disposed)
                    {
                        p.Offset(-(DXManager.LightSizes[light].X / 2) - (CellWidth / 2) + 10, -(DXManager.LightSizes[light].Y / 2) - (CellHeight / 2) - 5);
                        DXManager.Draw(DXManager.Lights[light], null, new Vector3((float)p.X, (float)p.Y, 0.0F), lightIntensity);
                    }
                }
            }
            #endregion

            DXManager.SetBlend(false);
            DXManager.SetSurface(oldSurface);

            DXManager.Device.SetRenderState(RenderState.SourceBlend, Blend.Zero);
            DXManager.Device.SetRenderState(RenderState.DestinationBlend, Blend.SourceColor);

            DXManager.Draw(DXManager.LightTexture, new Rectangle(0, 0, Settings.ScreenWidth, Settings.ScreenHeight), Vector3.Zero, Color.White);

            DXManager.Sprite.End();
            DXManager.Sprite.Begin(SpriteFlags.AlphaBlend);
        }

        private static void OnMouseClick(object sender, EventArgs e)
        {
            if (!(e is MouseEventArgs me)) return;

            if (AwakeningAction == true) return;
            switch (me.Button)
            {
                case MouseButtons.Left:
                    {
                        AutoRun = false;
                        GameScene.Scene.MapControl.AutoPath = false;
                        if (MapObject.MouseObject == null) return;
                        NPCObject npc = MapObject.MouseObject as NPCObject;
                        if (npc != null)
                        {
                            if (npc.ObjectID == GameScene.NPCID && 
                                (CMain.Time <= GameScene.NPCTime || GameScene.Scene.NPCDialog.Visible))
                            {
                                return;
                            }

                            //GameScene.Scene.NPCDialog.Hide();

                            GameScene.NPCTime = CMain.Time + 5000;
                            GameScene.NPCID = npc.ObjectID;
                            Network.Enqueue(new C.CallNPC { ObjectID = npc.ObjectID, Key = "[@Main]" });
                        }
                    }
                    break;
                case MouseButtons.Right:
                    {
                        AutoRun = false;
                        if (MapObject.MouseObject == null)
                        {
                            if (Settings.NewMove && MapLocation != MapObject.User.CurrentLocation && GameScene.Scene.MapControl.EmptyCell(MapLocation))
                            {
                                var path = GameScene.Scene.MapControl.PathFinder.FindPath(MapObject.User.CurrentLocation, MapLocation, 20);

                                if (path != null && path.Count > 0)
                                {
                                    GameScene.Scene.MapControl.CurrentPath = path;
                                    GameScene.Scene.MapControl.AutoPath = true;
                                    var offset = MouseLocation.Subtract(ToMouseLocation(MapLocation));
                                    Effects.Add(new Effect(Libraries.Magic3, 500, 10, 600, MapLocation) { DrawOffset = offset.Subtract(8, 15) });
                                }
                            }
                            return;
                        }

                        if (CMain.Ctrl)
                        {
                            HeroObject hero = MapObject.MouseObject as HeroObject;

                            if (hero != null &&
                                hero.ObjectID != (Hero is null ? 0 : Hero.ObjectID) &&
                                CMain.Time >= GameScene.InspectTime)
                            {
                                GameScene.InspectTime = CMain.Time + 500;
                                InspectDialog.InspectID = hero.ObjectID;
                                Network.Enqueue(new C.Inspect { ObjectID = hero.ObjectID, Hero = true });
                                return;
                            }

                            PlayerObject player = MapObject.MouseObject as PlayerObject;

                            if (player != null &&
                                player != User &&
                                CMain.Time >= GameScene.InspectTime)
                            {
                                GameScene.InspectTime = CMain.Time + 500;
                                InspectDialog.InspectID = player.ObjectID;
                                Network.Enqueue(new C.Inspect { ObjectID = player.ObjectID });
                                return;
                            }
                        }
                    }
                    break;
                case MouseButtons.Middle:
                    AutoRun = !AutoRun;
                    break;
            }
        }

        private static void OnMouseDown(object sender, MouseEventArgs e)
        {
            MapButtons |= e.Button;
            if (e.Button != MouseButtons.Right || !Settings.NewMove)
                GameScene.CanRun = false;

            if (AwakeningAction == true) return;

            if (e.Button != MouseButtons.Left) return;

            if (GameScene.SelectedCell != null)
            {
                //if (GameScene.SelectedCell.GridType != MirGridType.Inventory)
                //{
                //    GameScene.SelectedCell = null;
                //    return;
                //}

                MirItemCell cell = GameScene.SelectedCell;
                if (cell.Item.Info.Bind.HasFlag(BindMode.DontDrop))
                {
                    MirMessageBox messageBox = new MirMessageBox(string.Format("你不能丢弃 {0}", cell.Item.FriendlyName), MirMessageBoxButtons.OK);
                    messageBox.Show();
                    GameScene.SelectedCell = null;
                    return;
                }
                if (cell.Item.Count == 1)
                {
                    MirMessageBox messageBox = new MirMessageBox(string.Format(GameLanguage.DropTip, cell.Item.FriendlyName), MirMessageBoxButtons.YesNo);

                    messageBox.YesButton.Click += (o, a) =>
                    {
                        Network.Enqueue(new C.DropItem
                        {
                            UniqueID = cell.Item.UniqueID,
                            Count = 1,
                            HeroInventory = cell.GridType == MirGridType.HeroInventory
                        });
                        
                        cell.Locked = true;
                    };
                    messageBox.Show();
                }
                else
                {
                    MirAmountBox amountBox = new MirAmountBox(GameLanguage.DropAmount, cell.Item.Info.Image, cell.Item.Count);

                    amountBox.OKButton.Click += (o, a) =>
                    {
                        if (amountBox.Amount <= 0) return;
                        Network.Enqueue(new C.DropItem
                        {
                            UniqueID = cell.Item.UniqueID,
                            Count = (ushort)amountBox.Amount,
                            HeroInventory = cell.GridType == MirGridType.HeroInventory
                        });

                        cell.Locked = true;
                    };

                    amountBox.Show();
                }
                GameScene.SelectedCell = null;

                return;
            }

            if (GameScene.PickedUpGold)
            {
                MirAmountBox amountBox = new MirAmountBox(GameLanguage.DropAmount, 116, GameScene.Gold);

                amountBox.OKButton.Click += (o, a) =>
                {
                    if (amountBox.Amount > 0)
                    {
                        Network.Enqueue(new C.DropGold { Amount = amountBox.Amount });
                    }
                };

                amountBox.Show();
                GameScene.PickedUpGold = false;
            }

            if (MapObject.MouseObject != null && !MapObject.MouseObject.Dead && !(MapObject.MouseObject is ItemObject) &&
                !(MapObject.MouseObject is NPCObject) && !(MapObject.MouseObject is MonsterObject && MapObject.MouseObject.AI == 970)
                 && !(MapObject.MouseObject is MonsterObject && MapObject.MouseObject.AI == 56))
            {
                MapObject.TargetObjectID = MapObject.MouseObject.ObjectID;
                if (MapObject.MouseObject is MonsterObject && MapObject.MouseObject.AI != 980)
                    MapObject.MagicObjectID = MapObject.TargetObject.ObjectID;
            }
            else
                MapObject.TargetObjectID = 0;
        }

        #region 内挂（自动挂机）

        private static long _autoPlayNextPot;
        private static long _autoPlayNextHeal;
        private static long _autoRoamNextTime;
        private static Point _autoRoamLastPos;          // 跑图时的上次位置（卡住检测）
        private static long _autoRoamLastPosTime;       // 上次位置变化的时间
        private static long _autoRoamWanderUntil;       // 卡住后强制随机游走的截止时间
        private static MirDirection _autoRoamDir;       // 当前游走方向（保持几秒再换）
        private static long _autoRoamDirTime;           // 当前游走方向的换向截止时间
        private static uint _autoStuckTargetID;         // 打不中检测：当前观察的目标
        private static long _autoStuckLastStruck;       // 打不中检测：该目标上次观察到的受击时间
        private static long _autoStuckSince;            // 打不中检测：无受击起始时间
        private static long _autoStuckBlackUntil;       // 打不中目标拉黑截止时间
        private static uint _autoImmuneTargetID;        // 法师/道士：魔免检测的当前目标
        private static long _autoImmuneLastStruck;      // 该目标上次观察到的受击时间
        private static long _autoImmuneMagicStart;      // 首次尝试魔法攻击的时间（0=尚未开始）
        private static long _autoImmuneLastCast;        // 最近一次挂起魔法施法的时间
        private static long _autoImmunePhysicalStart;   // 改物理攻击的起始时间（0=未切换，表示仍在魔法阶段）
        private static string _autoImmuneBlackName;     // 魔法+物理都打不动的怪物名（一段时间内不再选）
        private static long _autoImmuneBlackUntil;      // 该名字的拉黑截止时间
        private static bool _autoImmuneReachedMelee;    // 物理阶段是否真的贴身到过目标（用于决定是否按名字拉黑）
        private static long _autoPlayNextSupport;       // 道士辅助技能（隐身/幽灵盾/神圣战甲术）节流
        private static long _autoPlayNextMageSupport;   // 法师辅助技能（魔法盾）节流
        private static long _autoPlayNextSummon;        // 道士召唤宠物节流
        private static long _autoPlayNextFireWall;      // 法师火墙节流
        private static long _autoPlayNextSaint;         // 法师圣言术节流
        private static long _autoPlayNextGroupHeal;     // 给队友加血的节流
        private static long _autoTaoistMeleeUntil;      // 道士隐身近砍窗口截止时间（窗口内不放技能，交给近攻）
        private static long _autoPlayNextSwap;          // 毒符互换节流
        // 毒粉「一次使用交替一次」：下一次施毒术要装的那种毒粉（1=灰色毒粉(绿毒) 2=黄色毒粉(红毒)）。
        // 初始 2 = 第一次施毒术先装黄色毒粉，之后 黄 → 灰 → 黄 … 交替。
        private static byte _autoPoisonNextShape = 2;
        private static uint _autoPoisonSwapUserID;      // 毒符交替状态所属的角色
        private static ulong _autoSwapLastID;           // 上次换装请求的物品（防止本地状态没同步时反复发包）
        private static long _autoSwapLastTime;
        private static int _manualSpellKey;             // 手动施法因换装被推迟的技能（技能栏键位，0=无）
        private static int _manualSpellWant = -1;       // 该技能需要槽里装什么：0=符 1=灰色毒粉 2=黄色毒粉
        private static bool _manualSpellIsPoison;       // 被推迟的这次手动施法是不是「施毒术」
        private static bool _manualSpellCasting;        // 正在补放（重入 UseSpell），本次不要再判材料
        private static long _manualSpellTime;           // 换装请求发出的时间
        private static long _manualSpellUntil;          // 等待材料就位的截止时间
        private static long _autoManualHoldUntil;       // 手动换装后的静默期（期间自动逻辑不再翻槽）
        private static long _autoPlayNextFury;          // 血龙剑法（自身增益）重试节流
        private static long _autoPlayNextDash;          // 野蛮冲撞节流（服务端冷却 2.5 秒）
        private static long _autoPlayNextToggle;        // 近攻开关技/蓄力技发包节流
        private static long _autoFlameBlockUntil;       // 烈火剑法蓄力后的静默期截止时间（服务端 10 秒内不接受再次蓄力）

        /// <summary>
        /// 内挂主循环。只负责「决策」——索敌、捡物、喝药；
        /// 具体的攻击与追击动作沿用 CheckInput 里已有的逻辑（选中目标后自动处理），
        /// 避免重复实现战斗判定。
        /// </summary>
        private void ProcessAutoPlay()
        {
            if (!Settings.AutoPlay) return;
            if (User == null || User.Dead || GameScene.Observing) return;
            if (AwakeningAction) return;

            if (User.Poison.HasFlag(PoisonType.Paralysis) || User.Poison.HasFlag(PoisonType.LRParalysis) ||
                User.Poison.HasFlag(PoisonType.Frozen) || User.Poison.HasFlag(PoisonType.Stun)) return;

            long now = CMain.Time;

            if (Settings.AutoPotHP || Settings.AutoPotMP)
            {
                if (now >= _autoPlayNextPot)
                {
                    _autoPlayNextPot = now + 300;
                    AutoPlayUsePotion();
                }
            }

            // 血量不足时用治愈术系技能补一口（喝药之外的补充手段）
            //（道士的自身治疗并入 AutoPlayGroupHeal 统一处理：自己 / 宠物 / 队友 一起判断群体还是单体）
            if (User.Class != MirClass.道士 && now >= _autoPlayNextHeal)
            {
                _autoPlayNextHeal = now + 500;
                AutoPlayAutoHeal();
            }

            // 道士辅助：给队伍里的队友加血、维持隐身术 / 幽灵盾 / 神圣战甲术，并自动召唤宠物
            if (User.Class == MirClass.道士)
            {
                // 隐身状态维护：隐身 25 秒没打到怪 / 中毒 25 秒 → 自动解除隐身改为主动攻击
                AutoPlayUpdateHiding(now);

                AutoPlayGroupHeal();
                AutoPlayTaoistSupport();
                AutoPlayTaoistSummon();

                //毒符互换：护身符槽按当前需要自动在「符」和「毒粉」之间切换（黄/灰毒粉一次使用交替一次）
                if (AutoPlaySwapPoisonAmulet()) return;    // 本帧刚发出换装，等服务器回执再继续战斗
            }

            // 法师辅助：自动开魔法盾
            if (User.Class == MirClass.法师)
                AutoPlayMageSupport();

            if (Settings.AutoAttack)
            {
                // 道士隐身后原地不动（走/跑都会解除隐身），只用灵魂火符、施毒术这类远程技能输出
                _autoHoldStill = AutoPlayHoldStill();

                MapObject target = MapObject.TargetObject;

                if (target == null || target.Dead || !(target is MonsterObject) ||
                    (target.Name != null && target.Name.EndsWith(")")) ||
                    target.NameColour == System.Drawing.Color.SkyBlue ||
                    AutoPlayIsIgnored(target.Name) ||
                    !Functions.InRange(target.CurrentLocation, User.CurrentLocation, Settings.AutoSearchRange))
                {
                    MapObject.TargetObjectID = 0;
                    target = AutoPlayFindMonster();
                }

                if (target != null)
                {
                    MapObject.TargetObjectID = target.ObjectID;

                    // 战士专属：血龙剑法 / 野蛮冲撞 / 开关技（刺杀·半月·狂风斩）与蓄力技（烈火·双龙斩）
                    // 不受「自动技能」开关限制——这些技能不替代普通攻击，只是给普通攻击加效果
                    if (User.Class == MirClass.战士 && AutoPlayWarriorSkills(target)) return;

                    if (User.Class == MirClass.法师 || User.Class == MirClass.道士)
                    {
                        // 法师/道士：魔法（道术）无效 → 改物理攻击 → 物理也无效就拉黑换怪
                        if (AutoPlayUpdateMagicImmunity(target, now)) return;
                    }
                    else
                    {
                        // 打不中检测：客户端收到 S.ObjectStruck 才说明目标真的被打中（服务端无效施法照样扣蓝）。
                        // 持续攻击同一目标 6 秒却一次受击广播都没有 → 判定打不中（守卫类 IsAttackTarget=false、
                        // 隔墙空放、超距 FindObject 找不到目标等），放弃并拉黑 8 秒、随机换位重找。
                        if (target.ObjectID != _autoStuckTargetID)
                        {
                            _autoStuckTargetID = target.ObjectID;
                            _autoStuckLastStruck = target.LastStruckTime;
                            _autoStuckSince = now;
                        }
                        else if (target.LastStruckTime != _autoStuckLastStruck)
                        {
                            _autoStuckLastStruck = target.LastStruckTime;
                            _autoStuckSince = now;
                        }
                        else if (now - _autoStuckSince > 6000)
                        {
                            _autoStuckBlackUntil = now + 8000;
                            MapObject.TargetObjectID = 0;

                            if (Settings.AutoMove && !_autoHoldStill)
                                AutoPlayStepTo(Functions.PointMove(User.CurrentLocation, (MirDirection)CMain.Random.Next(8), 2));

                            return;
                        }
                    }

                    // 未开自动技能但开了自动躲避：被围攻时仍然走开（隐身时不动）
                    if (!Settings.AutoSkill && Settings.AutoDodge && !_autoHoldStill &&
                        AutoPlayCountMonsters(User.CurrentLocation, 1) >= 3 && AutoPlayDodge())
                    {
                        MapObject.TargetObjectID = 0;
                        return;
                    }

                    // 弓手/法师走位：够不到就追，太近就拉开（含走位跑步）；走位那一帧不施法，避免施法动作顶掉移动
                    //（道士隐身期间原地不动，只用远程技能输出）
                    bool moved = Settings.AutoMove && !_autoHoldStill && AutoPlayCombatMove(target);

                    // 自动按需用技能：围攻→群攻（不行则躲避），远→远程，近→近攻开关技
                    // 法师没有远程普通攻击（等同弓手的远程攻击），即使没勾「自动技能」也会自动放法术
                    if (!moved && (Settings.AutoSkill || User.Class == MirClass.法师))
                        AutoPlayAutoSkill(target);

                    return;
                }

                // 附近没怪：先捡地上的东西，捡完/没得捡就跑图找怪（隐身期间不动，避免解除隐身）
                if (Settings.AutoPickup && AutoPlayPickUpItem()) return;

                if (!_autoHoldStill) AutoPlayRoam();
                return;
            }

            if (Settings.AutoPickup)
                AutoPlayPickUpItem();
        }

        /// <summary>
        /// 法师 / 道士的「攻击无效」三级降级检测：
        /// ① 一直用魔法（道术）攻击同一目标 AutoPlayMagicProbeTime 毫秒，客户端一次受击广播都没收到
        ///    （服务端只有真正造成伤害才广播 S.ObjectStruck）→ 判定该怪物魔法无效，改用物理攻击；
        /// ② 改用物理攻击（贴身 1 格用近距攻击）再试 AutoPlayPhysicalProbeTime 毫秒，仍无受击 → 物理也无效；
        /// ③ 放弃该目标：拉黑一段时间并随机换位重新索敌（若物理阶段确实贴身过，则连怪物名字一起拉黑，
        ///    避免反复去试同一种打不动的怪）。
        /// 返回 true 表示本帧已放弃目标，调用方应直接 return。
        /// </summary>
        private bool AutoPlayUpdateMagicImmunity(MapObject target, long now)
        {
            if (target.ObjectID != _autoImmuneTargetID)
            {
                _autoImmuneTargetID = target.ObjectID;
                _autoImmuneLastStruck = target.LastStruckByMeTime;
                _autoImmuneMagicStart = 0;
                _autoImmuneLastCast = 0;
                _autoImmuneReachedMelee = false;
                // 法师必定放法术；道士要勾了「自动技能」才会放法术，否则直接进物理阶段
                _autoImmunePhysicalStart = AutoPlayUsesMagic() ? 0 : now;
                return false;
            }

            if (target.LastStruckByMeTime != _autoImmuneLastStruck)
            {
                // 目标被自己的攻击打中 → 当前阶段的攻击有效，重置该阶段计时
                //（已经切到物理的保持物理模式，不再回头试魔法）
                _autoImmuneLastStruck = target.LastStruckByMeTime;
                _autoImmuneMagicStart = 0;
                _autoImmuneLastCast = 0;

                if (_autoImmunePhysicalStart != 0)
                    _autoImmunePhysicalStart = now;

                return false;
            }

            if (_autoImmunePhysicalStart == 0)
            {
                // 第一阶段：魔法攻击中。必须确认「确实一直在施法」才判定无效，
                // 否则跑位、没蓝、材料不够时也会被误判成魔法无效。
                if (_autoImmuneMagicStart != 0 && now - _autoImmuneMagicStart > AutoPlayMagicProbeTime &&
                    now - _autoImmuneLastCast < AutoPlayCastAliveTime)
                {
                    _autoImmunePhysicalStart = now;
                    GameScene.Scene.OutputMessage("魔法对" + AutoPlayTargetName(target) + "无效，改用物理攻击");
                }

                return false;
            }

            // 第二阶段：物理攻击中（先走过去贴脸，到了才开始算物理攻击时长）
            bool inMelee = Functions.MaxDistance(target.CurrentLocation, User.CurrentLocation) <= 1;

            if (!_autoImmuneReachedMelee)
            {
                if (inMelee)
                {
                    _autoImmuneReachedMelee = true;
                    _autoImmunePhysicalStart = now;    // 贴身成功，从这一刻开始计算物理攻击时长
                    return false;
                }

                // 靠近阶段：给 AutoPlayMeleeReachTime 毫秒；超时还没贴身（多半被墙/障碍卡住）也算打不到
                if (now - _autoImmunePhysicalStart <= AutoPlayMeleeReachTime) return false;
            }
            else if (now - _autoImmunePhysicalStart <= AutoPlayPhysicalProbeTime)
            {
                return false;
            }

            string name = AutoPlayTargetName(target);

            _autoStuckTargetID = target.ObjectID;
            _autoStuckBlackUntil = now + AutoPlayImmuneBlackTime;
            _autoImmuneTargetID = 0;

            // 贴身砍都打不动 → 认为是怪物本身免疫，按名字拉黑，避免下一只同类怪再白试一轮
            if (_autoImmuneReachedMelee && !string.IsNullOrEmpty(target.Name))
            {
                _autoImmuneBlackName = target.Name;
                _autoImmuneBlackUntil = now + AutoPlayImmuneBlackTime;
            }

            GameScene.Scene.OutputMessage("物理攻击对" + name + "也无效，放弃该目标");

            MapObject.TargetObjectID = 0;

            if (Settings.AutoMove)
                AutoPlayStepTo(Functions.PointMove(User.CurrentLocation, (MirDirection)CMain.Random.Next(8), 2));

            return true;
        }

        /// <summary>当前目标是否已进入「改物理攻击」阶段（法师/道士专用）</summary>
        private bool AutoPlayIsPhysicalTarget(MapObject target)
        {
            if (target == null) return false;
            if (User.Class != MirClass.法师 && User.Class != MirClass.道士) return false;

            return _autoImmunePhysicalStart != 0 && target.ObjectID == _autoImmuneTargetID;
        }

        /// <summary>本职业当前是否会用魔法（道术）打怪：法师必用；道士需勾选「自动技能」</summary>
        private bool AutoPlayUsesMagic()
        {
            return User.Class == MirClass.法师 || (User.Class == MirClass.道士 && Settings.AutoSkill);
        }

        private static string AutoPlayTargetName(MapObject target)
        {
            return string.IsNullOrEmpty(target.Name) ? "该怪物" : target.Name;
        }

        /// <summary>
        /// 远程职业（弓手 / 法师）的自动走位：目标超出射程时追过去；
        /// 目标贴脸（距离 ≤ 2）时向后拉开，保持放风筝的输出距离（法师绝不贴身近攻）。
        /// 拉开时清空本帧目标，避免被 CheckInput 的自动攻击覆盖掉移动动作。
        /// </summary>
        private bool AutoPlayCombatMove(MapObject target)
        {
            // 魔免目标已切物理攻击：主动走到贴身距离，由 CheckInput 的「近距攻击1」出手（不再放风筝拉开）
            if (AutoPlayIsPhysicalTarget(target))
            {
                if (Functions.MaxDistance(target.CurrentLocation, User.CurrentLocation) > 1)
                {
                    AutoPlayStepTo(target.CurrentLocation);
                    return true;
                }

                return false;
            }

            bool ranged = (User.Class == MirClass.弓箭 && User.HasClassWeapon) || User.Class == MirClass.法师;

            if (!ranged) return false;    // 其他职业的追击由 CheckInput 处理（已支持跑动）

            int dist = Functions.MaxDistance(target.CurrentLocation, User.CurrentLocation);

            if (dist > Globals.MaxAttackRange)
            {
                AutoPlayStepTo(target.CurrentLocation);
                return true;
            }

            // 被怪围攻时法师不后撤——交给「地狱雷光 / 抗拒火环」处理，否则每帧都在往后跑、技能永远轮不到放
            bool surrounded = User.Class == MirClass.法师 &&
                              AutoPlayCountMonsters(User.CurrentLocation, 1) >= AutoPlaySurroundCount;

            if (!surrounded && dist <= 2 && AutoPlayStepAway(target))
            {
                MapObject.TargetObjectID = 0;   // 本帧移动优先，不发起攻击
                return true;
            }

            return false;
        }

        /// <summary>统计 loc 周围 range 格内的怪物数量（排除宠物/忽略名单）</summary>
        private int AutoPlayCountMonsters(Point loc, int range)
        {
            int count = 0;

            for (int i = 0; i < Objects.Count; i++)
            {
                MapObject ob = Objects[i];

                if (ob == null || ob.Dead || !(ob is MonsterObject)) continue;
                if (ob.Race == ObjectType.Creature) continue;
                if (ob.Name != null && ob.Name.EndsWith(")")) continue;   // 玩家宠物
                if (AutoPlayIsIgnored(ob.Name)) continue;
                if (ob.NameColour == System.Drawing.Color.SkyBlue) continue;   // 守卫类怪物（名字天蓝色）不算威胁

                if (Functions.InRange(ob.CurrentLocation, loc, range)) count++;
            }

            return count;
        }

        /// <summary>
        /// 躲避/拉开：在八方向中挑格子移动，支持跑动——开了「走位跑步」且跑得动时，
        /// 每个方向同时评估「走一步」和「跑 2/3 格」两个落点，按安全性择优（跑不通自动降级为走）。
        /// keepNear=true 躲避（被围攻）：选怪物更少、且尽量别远离目标的格子，找不到更安全的格子返回 false；
        /// keepNear=false 拉开（弓手放风筝）：选离目标更远的格子（怪物更少者优先），被堵死返回 false。
        /// </summary>
        private bool AutoPlayDodgeStep(MapObject threat, bool keepNear)
        {
            Point cur = User.CurrentLocation;
            int curCount = AutoPlayCountMonsters(cur, 1);
            int curDist = threat != null ? Functions.MaxDistance(threat.CurrentLocation, cur) : 0;

            // 跑动条件与 AutoPlayStepTo 保持一致
            bool canRun = Settings.AutoMoveRun &&
                (GameScene.CanRun || Settings.NoRunUp) && CMain.Time > GameScene.NextRunTime &&
                User.HP >= 10 && (!User.Sneaking || (User.Sneaking && User.Sprint));

            int runDistance = User.RidingMount || (User.Sprint && !User.Sneaking) ? 3 : 2;

            Point best = Point.Empty;
            MirDirection bestDir = 0;
            int bestCount = 0, bestDist = 0, bestSteps = 0;

            for (int i = 0; i < 8; i++)
            {
                if (!CanWalk((MirDirection)i, out MirDirection outDir)) continue;

                // 候选 0：走一步的格子
                Point walkPoint = Functions.PointMove(cur, outDir, 1);
                if (!CheckDoorOpen(walkPoint)) continue;

                // 候选 1：同方向跑动落点（中间格子必须畅通）
                Point runPoint = Point.Empty;
                if (canRun)
                {
                    bool clear = true;
                    for (int j = 1; j <= runDistance; j++)
                    {
                        Point t = Functions.PointMove(cur, outDir, j);
                        if (!ValidPoint(t) || !CheckDoorOpen(t)) { clear = false; break; }
                    }

                    if (clear && CanRun(outDir)) runPoint = Functions.PointMove(cur, outDir, runDistance);
                }

                for (int k = 0; k < 2; k++)
                {
                    Point cand = k == 0 ? walkPoint : runPoint;
                    if (cand == Point.Empty) continue;

                    int count = AutoPlayCountMonsters(cand, 1);
                    int dist = threat != null ? Functions.MaxDistance(threat.CurrentLocation, cand) : curDist;

                    // 必须严格优于原地才走：躲避看怪物数，拉开看距离
                    bool better = best == Point.Empty
                        ? (keepNear ? count < curCount : dist > curDist || (dist == curDist && count < curCount))
                        : (keepNear ? count < bestCount || (count == bestCount && dist < bestDist)
                                    : dist > bestDist || (dist == bestDist && count < bestCount));

                    if (!better) continue;

                    best = cand;
                    bestDir = outDir;
                    bestCount = count;
                    bestDist = dist;
                    bestSteps = k == 0 ? 1 : runDistance;
                }
            }

            if (best == Point.Empty) return false;

            User.QueuedAction = new QueuedAction
            {
                Action = bestSteps > 1 ? MirAction.跑步动作 : MirAction.行走动作,
                Direction = bestDir,
                Location = best
            };

            return true;
        }

        /// <summary>弓手被贴脸时向后拉开一步（放风筝）</summary>
        private bool AutoPlayStepAway(MapObject target)
        {
            return AutoPlayDodgeStep(target, false);
        }

        /// <summary>主循环里的躲避：被围攻时走开（不指定单个威胁，以全周怪物数为准）</summary>
        private bool AutoPlayDodge()
        {
            // 以当前目标为参照保持距离，目标为空时单纯找怪物最少的格子
            MapObject threat = MapObject.TargetObject is MonsterObject m && !m.Dead ? m : null;
            return AutoPlayDodgeStep(threat, true);
        }

        #region 内挂自动技能

        //技能优先级表：越靠前越优先，只会从「已学会」的技能里挑，没学的自动跳过
        private static readonly Spell[] AutoPlayRangedSpells =     // 远程单体：远怪/放风筝时用
        {
            // 法师
            Spell.ThunderBolt, Spell.FlameDisruptor, Spell.FrostCrunch,
            Spell.GreatFireBallRare, Spell.GreatFireBall, Spell.FireBall,
            // 道士
            Spell.SoulFireBall,
            // 弓手（客户端会校验必须佩戴弓类武器）
            Spell.DoubleShot, Spell.ElementalShot, Spell.NapalmShot,
            Spell.VampireShot, Spell.PoisonShot, Spell.CrippleShot,
            Spell.DelayedExplosion, Spell.StraightShot,
        };

        private static readonly Spell[] AutoPlayAoeSpells =        // 群攻：被围攻时优先使用
        {
            // 法师（雷电风暴为周身范围，其余以目标为中心）
            Spell.ThunderStorm, Spell.IceStorm, Spell.Blizzard,
            Spell.MeteorStrike, Spell.FireBang, Spell.HellFire, Spell.IceThrust,
            // 道士
            Spell.PoisonCloud,
        };

        //道士隐身近砍节奏：隐身后被怪贴身近攻时，每砍 1 秒再继续放远程技能（近攻由 CheckInput 执行）
        private const long AutoTaoistMeleeWindow = 1000;

        //法师/道士的攻击无效降级：魔法（道术）试 4 秒无效 → 改物理；物理再试 4 秒无效 → 放弃该目标
        private const long AutoPlayMagicProbeTime = 4000;      // 魔法阶段判定时长
        private const long AutoPlayPhysicalProbeTime = 4000;   // 物理阶段判定时长（贴身之后开始算）
        private const long AutoPlayMeleeReachTime = 5000;      // 切物理后走过去贴脸的时间上限，超时视为够不到
        private const long AutoPlayCastAliveTime = 4000;       // 最近一次施法在此时间内才算「仍在持续施法」
        private const long AutoPlayImmuneBlackTime = 30000;    // 魔法+物理都无效的目标拉黑时长

        //道士辅助技能：隐身术 / 幽灵盾（魔法防御）/ 神圣战甲术（物理防御）——三者的施法目标都是自己
        private static readonly Spell[] AutoPlayHidingSpells = { Spell.Hiding };
        private static readonly Spell[] AutoPlaySoulShieldSpells = { Spell.SoulShield };
        private static readonly Spell[] AutoPlayBlessedArmourSpells = { Spell.BlessedArmour };

        //法师辅助：魔法盾（吸收伤害的护盾，施法目标是自己）
        private static readonly Spell[] AutoPlayMagicShieldSpells = { Spell.MagicShield };

        //法师按需群攻：地狱雷光（周身范围，服务端与火龙气焰同一处理）
        private static readonly Spell[] AutoPlaySelfAoeSpells = { Spell.ThunderStorm, Spell.FlameField };
        //法师：抗拒火环（把贴脸的怪推开）
        private static readonly Spell[] AutoPlayRepulsorSpells = { Spell.Repulsion };
        //法师：火墙（在怪最密集的地方放，落点是十字 5 格）
        private static readonly Spell[] AutoPlayFireWallSpells = { Spell.FireWall };
        //法师：疾光电影（沿一个方向打穿一条直线，最多 6 格）
        private static readonly Spell[] AutoPlayLineSpells = { Spell.Lightning };
        //法师：圣言术（只对不死系怪物生效）
        private static readonly Spell[] AutoPlayTurnUndeadSpells = { Spell.TurnUndead };

        //道士召唤：骷髅 → 神兽 → 月灵，三种**同时养**（各自数量为 0 就补谁，都齐了就不再召唤）
        private static readonly Spell[] AutoPlaySummonSpells = { Spell.SummonSkeleton, Spell.SummonShinsu, Spell.SummonHolyDeva };

        //识别自己召唤物用的名字关键字（与 AutoPlaySummonSpells 一一对应）：
        //服务端宠物名格式是「怪物名(主人名)」，怪物名取自 Settings.ini 的 SkeletonName / ShinsuName / AngelName
        private static readonly string[][] AutoPlaySummonPetKeywords =
        {
            new[] { "骷髅" },           // 变异骷髅
            new[] { "神兽", "圣兽" },    // 神兽
            new[] { "月灵" },           // 月灵
        };

        //道士：施毒术（消耗槽里的毒粉，灰色毒粉=绿毒持续掉血、黄色毒粉=红毒削弱防御）
        private static readonly Spell[] AutoPlayPoisonSpells = { Spell.Poisoning };

        //战士：血龙剑法（自身增益，攻击速度 +，持续 60 秒 + 每级 10 秒）
        private static readonly Spell[] AutoPlayFurySpells = { Spell.Fury };

        private const int AutoPlaySurroundCount = 3;     // 周身 1 格内怪数 ≥ 此值算「被围攻」
        private const int AutoPlayClusterMinCount = 3;   // 火墙落点十字范围内怪数 ≥ 此值才值得放
        private const int AutoPlayLineMinCount = 3;      // 一条直线上怪数 ≥ 此值才放疾光电影
        private const int AutoPlayLineMaxTiles = 6;      // 疾光电影最大穿透格数（与服务端一致）
        private const int AutoPlaySummonInterval = 3000; // 召唤技能的重试间隔
        private const long AutoPlayFireWallInterval = 5000;  // 火墙节流（墙本身能烧十几秒）
        private const long AutoPlaySaintInterval = 5000;     // 圣言术节流（很费蓝，对高级不死系还容易失败）
        private const long AutoPlaySwapInterval = 700;       // 毒符互换发包节流
        private const long AutoPlayFuryInterval = 15000;     // 血龙剑法没有增益时的重试间隔
        private const long AutoPlayDashInterval = 2500;      // 野蛮冲撞节流（与服务端冷却一致）
        private const long AutoPlayToggleInterval = 1000;    // 近攻开关技发包节流
        private const long AutoPlayFlameBlockTime = 10500;   // 烈火剑法蓄力后的静默期（服务端 10 秒内不接受再次蓄力）

        //给队友加血：单体治愈术 / 群体治疗术
        private static readonly Spell[] AutoPlayAllyHealSpells = { Spell.Healing, Spell.HealingRare };
        private static readonly Spell[] AutoPlayMassHealSpells = { Spell.MassHealing };
        private const int AutoPlayAllyHealPercent = 80;            // 队友血量低于该百分比时自动加血

        /// <summary>
        /// 自动按需用技能：被围攻 → 群攻（放不出且开了躲避就走位）；
        /// 远怪 → 远程单体技能；近怪 → 近战开关技 + 普通攻击（由 CheckInput 执行）。
        /// </summary>
        private void AutoPlayAutoSkill(MapObject target)
        {
            if (User.NextMagic != null) return;          // 玩家手动按的技能优先，不抢

            // 该怪物魔法无效、已切到物理攻击：本阶段不再放技能，贴身用近距攻击（由 CheckInput 执行）
            if (AutoPlayIsPhysicalTarget(target)) return;

            // 1) 被围攻（周身 1 格内 ≥ 3 只怪）
            if (AutoPlayCountMonsters(User.CurrentLocation, 1) >= AutoPlaySurroundCount)
            {
                // 法师优先用周身范围的技能自保：地狱雷光 → 抗拒火环 → 其他群攻
                if (User.Class == MirClass.法师)
                {
                    if (AutoPlayTryCast(AutoPlaySelfAoeSpells, target)) return;
                    if (AutoPlayTryCast(AutoPlayRepulsorSpells, target)) return;
                }

                if (AutoPlayTryCast(AutoPlayAoeSpells, target)) return;

                if (Settings.AutoDodge && !_autoHoldStill && AutoPlayDodge())
                {
                    MapObject.TargetObjectID = 0;        // 本帧移动优先，避免被 CheckInput 的攻击覆盖
                    return;
                }
            }

            // 1.2) 法师专属：群怪抱团放火墙 / 怪排成一条线放疾光电影 / 不死系放圣言术
            if (User.Class == MirClass.法师 && AutoPlayMageCombat(target)) return;

            // 1.5) 道士：隐身状态下被怪物贴身（1 格内）近身攻击时，才用近身砍
            //（近攻由 CheckInput 的「近距攻击1」执行）；其余情况一律以灵魂火符、施毒术等远程技能为主攻
            if (User.Class == MirClass.道士 && _autoHoldStill && AutoPlayMonsterMeleeAdjacent(target))
            {
                if (CMain.Time < _autoTaoistMeleeUntil) return;          // 正在砍的这一小段，别插技能

                _autoTaoistMeleeUntil = CMain.Time + AutoTaoistMeleeWindow;
                _autoHiddenLastAttack = CMain.Time;                     // 近砍也算「攻击怪物」，重置隐身计时
                return;                                                 // 本帧不放技能 → 交给近距攻击
            }

            // 1.8) 道士：目标身上还缺「槽里那种毒」时先补一发施毒术
            //（毒符互换会在需要时把符换成毒，用完再换回符）
            if (User.Class == MirClass.道士 && AutoPlayNeedPoison(target) &&
                AutoPlayTryCast(AutoPlayPoisonSpells, target)) return;

            // 2) 远程技能（战士/刺客没有可放的远程技能时会自然落到普通攻击）
            if (AutoPlayTryCast(AutoPlayRangedSpells, target)) return;

            // 3) 技能就绪但够不到目标（超出射程）→ 移动到打得到的位置，下一帧自动施法
            //（隐身期间不移动，站在原地够不到就不动）
            if (AutoPlayHasReadySpell(AutoPlayRangedSpells))
            {
                if (CMain.Time >= GameScene.SpellTime && Settings.AutoMove && !_autoHoldStill &&
                    Functions.MaxDistance(target.CurrentLocation, User.CurrentLocation) > 1)
                {
                    AutoPlayStepTo(target.CurrentLocation);
                    MapObject.TargetObjectID = 0;    // 本帧移动优先，避免被攻击动作覆盖
                }

                return;
            }

            // 4) 近战职业：确保近攻开关技已打开（刺杀/半月/烈火、风刃术）
            AutoPlayEnableMeleeToggles();
        }

        /// <summary>技能列表里是否存在「已学会、已冷却完毕、蓝够」的技能（不校验射程）</summary>
        private bool AutoPlayHasReadySpell(Spell[] list)
        {
            for (int i = 0; i < list.Length; i++)
            {
                ClientMagic magic = User.GetMagic(list[i]);

                if (magic == null) continue;                                   // 未学会
                if (CMain.Time <= magic.CastTime + magic.Delay) continue;      // 冷却中

                int cost = magic.Level * magic.LevelCost + magic.BaseCost;
                if (cost > User.MP) continue;                                  // 蓝不够
                if (!AutoPlayHasSpellMaterials(list[i])) continue;             // 施法材料不够（道士符系技能）

                return true;
            }

            return false;
        }

        /// <summary>
        /// 按优先级挑第一个可用技能：已学会、不在冷却、蓝够。
        /// 成功则挂到 NextMagic 上，同一帧由 CheckInput 统一走 UseMagic 施放（含射程/目标校验）。
        /// </summary>
        private bool AutoPlayTryCast(Spell[] list, MapObject target)
        {
            for (int i = 0; i < list.Length; i++)
                if (AutoPlayTryCast(list[i], target)) return true;

            return false;
        }

        /// <summary>
        /// 挑单个技能（`AutoPlayTryCast(list, target)` 的单项版，召唤技能按只判断时用）：
        /// 已学会、不在冷却、施法间隔到了、蓝够、材料够、射程够 → 挂到 NextMagic 上。
        /// </summary>
        private bool AutoPlayTryCast(Spell spell, MapObject target)
        {
            ClientMagic magic = User.GetMagic(spell);

            if (magic == null) return false;                               // 未学会
            if (CMain.Time <= magic.CastTime + magic.Delay) return false;  // 冷却中
            if (CMain.Time < GameScene.SpellTime) return false;            // 施法间隔未到，先不挑

            int cost = magic.Level * magic.LevelCost + magic.BaseCost;
            if (cost > User.MP) return false;                              // 蓝不够
            if (!AutoPlayHasSpellMaterials(spell)) return false;           // 施法材料不够（道士符系技能）

            // 射程校验：够不到的技能不放（否则施法失败白白耗掉一帧，人物会原地站着反复施法不移动）
            if (magic.Range != 0 && !Functions.InRange(User.CurrentLocation, target.CurrentLocation, magic.Range)) return false;

            User.NextMagicObject = target;
            User.NextMagicLocation = target.CurrentLocation;
            User.NextMagicDirection = Functions.DirectionFromPoint(User.CurrentLocation, target.CurrentLocation);
            User.NextMagic = magic;

            // 隐身计时用：隐身后最近一次真的对怪物出手（对自己上的辅助技能不算）
            if (target is MonsterObject)
                _autoHiddenLastAttack = CMain.Time;

            // 法师/道士的「攻击无效」检测：记录确实施法过（魔法阶段计时靠这两个时间）
            if (target is MonsterObject && target.ObjectID == _autoImmuneTargetID)
            {
                _autoImmuneLastCast = CMain.Time;

                if (_autoImmuneMagicStart == 0)
                    _autoImmuneMagicStart = CMain.Time;
            }

            return true;
        }

        #endregion

        #region 战士专属：血龙剑法 / 野蛮冲撞 / 开关注能

        /// <summary>
        /// 战士内挂：
        /// ① 自身没有血龙剑法增益时自动补（服务端 buff 60 秒 + 每级 10 秒，冷却 10 分钟）；
        /// ② 被怪围住（贴脸 ≥ 3 只）时用野蛮冲撞朝怪最少的方向撞出去；
        /// ③ 常开型开关技（刺杀剑术 / 半月弯刀 / 狂风斩）保持开启；
        /// ④ 蓄力型一次性技能（烈火剑法 / 双龙斩）每次攻击前重新蓄力（服务端每次攻击消耗一次）。
        /// 返回 true 表示本帧已挂上技能（调用方应直接 return，由 CheckInput 统一施放）。
        /// </summary>
        private bool AutoPlayWarriorSkills(MapObject target)
        {
            long now = CMain.Time;

            // ① 血龙剑法：没增益就补，节流避免一直发包（失败多半是服务端还在冷却）
            if (!AutoPlayHasBuff(BuffType.血龙剑法) && now >= _autoPlayNextFury)
            {
                _autoPlayNextFury = now + AutoPlayFuryInterval;

                if (AutoPlayTryCast(AutoPlayFurySpells, User)) return true;
            }

            // ② 野蛮冲撞：被怪围住时撞出去（服务端会把挡在身前的低等级怪推开）
            if (now >= _autoPlayNextDash && AutoPlayCountMonsters(User.CurrentLocation, 1) >= AutoPlaySurroundCount)
            {
                _autoPlayNextDash = now + AutoPlayDashInterval;

                if (AutoPlayDashAway()) return true;
            }

            // ③④ 开关技与蓄力技
            AutoPlayEnableMeleeToggles();

            return false;
        }

        /// <summary>
        /// 野蛮冲撞突围：八个方向里挑一个「面前 2 格没有怪物」且周围怪最少的方向撞出去。
        /// 没有合适方向（全被堵死）或技能没学会/没蓝时返回 false（不发包，避免白扣蓝）。
        /// </summary>
        private bool AutoPlayDashAway()
        {
            ClientMagic magic = User.GetMagic(Spell.ShoulderDash);

            if (magic == null) return false;
            if (magic.Delay != 0 && CMain.Time <= magic.CastTime + magic.Delay) return false;
            if (CMain.Time < GameScene.SpellTime) return false;

            int cost = magic.Level * magic.LevelCost + magic.BaseCost;
            if (cost > User.MP) return false;

            Point cur = User.CurrentLocation;
            MirDirection best = MirDirection.Up;
            int bestCount = int.MaxValue;

            for (int i = 0; i < 8; i++)
            {
                MirDirection dir = (MirDirection)i;
                Point p1 = Functions.PointMove(cur, dir, 1);
                Point p2 = Functions.PointMove(cur, dir, 2);

                if (!ValidPoint(p1) || !ValidPoint(p2)) continue;
                if (AutoPlayCountMonsters(p1, 1) > 0 || AutoPlayCountMonsters(p2, 1) > 0) continue;

                int count = AutoPlayCountMonsters(p1, 2);

                if (count >= bestCount) continue;

                bestCount = count;
                best = dir;
            }

            if (bestCount == int.MaxValue) return false;      // 四面八方都被怪堵着，撞不动

            User.NextMagicObject = null;
            User.NextMagicLocation = Functions.PointMove(cur, best, 1);
            User.NextMagicDirection = best;
            User.NextMagic = magic;

            return true;
        }

        /// <summary>
        /// 近战职业的近攻技能开关：
        /// 「常开型」（刺杀剑术 / 半月弯刀 / 狂风斩 / 风剑术）状态由服务端保存、登录时回执，
        /// 这里确保客户端处于开启状态（掉线重连、死亡复活后需要重开）。
        /// 「蓄力型」（烈火剑法 / 双龙斩）服务端每次攻击消耗一次，所以每次攻击前重新蓄力；
        /// 两者只蓄一个（都要扣蓝）：烈火剑法优先，它蓄力后服务端 10 秒内不接受再次蓄力，这期间用双龙斩。
        /// </summary>
        private void AutoPlayEnableMeleeToggles()
        {
            if (User.Class == MirClass.战士)
            {
                AutoPlayToggleMeleeSkill(Spell.Thrusting, User.Thrusting);
                AutoPlayToggleMeleeSkill(Spell.HalfMoon, User.HalfMoon);
                AutoPlayToggleMeleeSkill(Spell.CrossHalfMoon, User.CrossHalfMoon);

                if (User.FlamingSword) return;        // 烈火已蓄好，等这一刀打出去，不用再蓄双龙斩

                if (CMain.Time >= _autoFlameBlockUntil && User.GetMagic(Spell.FlamingSword) != null &&
                    AutoPlayChargeMeleeSpell(Spell.FlamingSword))
                {
                    _autoFlameBlockUntil = CMain.Time + AutoPlayFlameBlockTime;
                    return;
                }

                AutoPlayChargeMeleeSpell(Spell.TwinDrakeBlade);
            }
            else if (User.Class == MirClass.刺客)
            {
                AutoPlayToggleMeleeSkill(Spell.DoubleSlash, User.DoubleSlash);
            }
        }

        /// <summary>常开型开关技：没开启就发一次开启请求（开关状态由客户端记录，服务端只保存下来）</summary>
        private void AutoPlayToggleMeleeSkill(Spell spell, bool on)
        {
            if (on) return;

            ClientMagic magic = User.GetMagic(spell);

            if (magic == null) return;                        // 没学会
            if (CMain.Time < _autoPlayNextToggle) return;     // 发包节流

            _autoPlayNextToggle = CMain.Time + AutoPlayToggleInterval;

            switch (spell)
            {
                case Spell.Thrusting: User.Thrusting = true; break;
                case Spell.HalfMoon: User.HalfMoon = true; break;
                case Spell.CrossHalfMoon: User.CrossHalfMoon = true; break;
                case Spell.DoubleSlash: User.DoubleSlash = true; break;
            }

            Network.Enqueue(new C.SpellToggle { Spell = spell, CanUse = true });
        }

        /// <summary>蓄力型一次性技能（烈火剑法 / 双龙斩）：攻击前重新蓄一次，返回是否已发出请求</summary>
        private bool AutoPlayChargeMeleeSpell(Spell spell)
        {
            ClientMagic magic = User.GetMagic(spell);

            if (magic == null) return false;                  // 没学会
            if (CMain.Time < _autoPlayNextToggle) return false;

            int cost = magic.Level * magic.LevelCost + magic.BaseCost;
            if (cost >= User.MP) return false;                // 与服务端一致：蓝必须比消耗多

            _autoPlayNextToggle = CMain.Time + 500;           // 与客户端手动蓄力的节流一致

            if (spell == Spell.TwinDrakeBlade)
                User.TwinDrakeBlade = true;                   // 烈火剑法由服务端回执置位

            Network.Enqueue(new C.SpellToggle { Spell = spell, CanUse = true });

            return true;
        }

        #endregion

        //治愈系技能：血量不足时自动补一口（只会从已学会的技能里挑，法师/道士才有）
        private static readonly Spell[] AutoPlayHealSpells =
        {
            Spell.Healing, Spell.HealingRare,       // 治愈术 / 治愈术-秘籍
            Spell.HealingCircle, Spell.HealingcircleRare,  // 治愈圆环（脚下持续回血）
            Spell.MassHealing,                      // 群体治愈术
        };

        /// <summary>
        /// 血量不足时自动使用治愈术系技能回复（与喝药互补，喝药照常进行）。
        /// 触发阈值与自动喝药的 AutoPotHPPercent 相同；MP 不足没有可用的回蓝技能（本引擎无主动回蓝技），仍靠喝药。
        /// </summary>
        private void AutoPlayAutoHeal()
        {
            if (User.NextMagic != null) return;          // 玩家手动按的技能优先，不抢

            int maxHP = User.Stats[Stat.HP];
            if (maxHP <= 0) return;
            if (User.HP * 100 / maxHP > Settings.AutoPotHPPercent) return;   // 血量充足

            // 以自己为施法目标挂到 NextMagic，同一帧由 CheckInput 走 UseMagic 施放
            AutoPlayTryCast(AutoPlayHealSpells, User);
        }

        /// <summary>
        /// 道士辅助：身上没有对应 buff 时自动给自己补
        /// 隐身术（有召唤宠物且附近怪 ≥2 只时才用）/ 幽灵盾（提高魔法防御）/ 神圣战甲术（提高物理防御）。
        /// 三个技能服务端都要消耗护身符，没有护身符时会静默失败却照样扣蓝，所以先在客户端拦掉。
        /// </summary>
        private void AutoPlayTaoistSupport()
        {
            if (User.NextMagic != null) return;              // 正在施法（含玩家手动），不抢
            if (CMain.Time < _autoPlayNextSupport) return;   // 节流：每次最多补一个 buff
            if (!AutoPlayHasAmulet()) return;                // 没护身符，别白耗蓝

            _autoPlayNextSupport = CMain.Time + 800;

            if (AutoPlayShouldHide() && !AutoPlayHasBuff(BuffType.隐身术) && AutoPlayTryCast(AutoPlayHidingSpells, User)) return;
            if (!AutoPlayHasBuff(BuffType.幽灵盾) && AutoPlayTryCast(AutoPlaySoulShieldSpells, User)) return;
            if (!AutoPlayHasBuff(BuffType.神圣战甲术) && AutoPlayTryCast(AutoPlayBlessedArmourSpells, User)) return;
        }

        /// <summary>
        /// 法师辅助：身上没有魔法盾时自动补一个（服务端为限时 buff，到期会自动续）。
        /// </summary>
        private void AutoPlayMageSupport()
        {
            if (User.NextMagic != null) return;                 // 正在施法（含玩家手动），不抢
            if (CMain.Time < _autoPlayNextMageSupport) return;  // 节流
            if (AutoPlayHasBuff(BuffType.魔法盾)) return;        // 已经有盾了

            _autoPlayNextMageSupport = CMain.Time + 800;

            AutoPlayTryCast(AutoPlayMagicShieldSpells, User);
        }

        /// <summary>
        /// 道士自动召唤：**骷髅 → 神兽 → 月灵** 三种一起养（`AutoPlaySummonSpells` 的顺序），
        /// 哪一只数量为 0 就补哪一只（有召唤骷髅就先召骷髅，骷髅 0 → 召骷髅；神兽 0 → 召神兽；以此类推）。
        /// 三只都在场就不再召唤。服务端在「已有同类宠物」时只会把它召回身边、不消耗材料，
        /// 但为了避免空转，客户端还是先按数量判断。
        /// 服务端在「已有同类宠物 / 宠物已满 / 没有护身符」时会直接返回，
        /// 但法力是在进技能处理前就扣掉的，所以这里先在客户端判断，避免白耗蓝。
        /// </summary>
        private void AutoPlayTaoistSummon()
        {
            if (User.NextMagic != null) return;                 // 正在施法（含玩家手动），不抢
            if (CMain.Time < _autoPlayNextSummon) return;

            for (int i = 0; i < AutoPlaySummonSpells.Length; i++)
            {
                if (User.GetMagic(AutoPlaySummonSpells[i]) == null) continue;   // 没学这种召唤术
                if (AutoPlayPetCount(i) > 0) continue;                          // 这只已经养着了

                _autoPlayNextSummon = CMain.Time + AutoPlaySummonInterval;

                AutoPlayTryCast(AutoPlaySummonSpells[i], User);                 // 缺哪只补哪只
                return;
            }
        }

        /// <summary>场上自己养的这类召唤物的数量（按名字关键字区分：骷髅 / 神兽 / 月灵）</summary>
        private int AutoPlayPetCount(int index)
        {
            if (string.IsNullOrEmpty(User.Name)) return 0;

            string suffix = "(" + User.Name + ")";
            string[] keywords = AutoPlaySummonPetKeywords[index];
            int count = 0;

            for (int i = 0; i < Objects.Count; i++)
            {
                MapObject ob = Objects[i];

                if (ob == null || ob.Dead || !(ob is MonsterObject)) continue;
                if (string.IsNullOrEmpty(ob.Name)) continue;
                if (!ob.Name.EndsWith(suffix)) continue;          // 不是自己的宠物

                for (int k = 0; k < keywords.Length; k++)
                {
                    if (!ob.Name.Contains(keywords[k])) continue;

                    count++;
                    break;
                }
            }

            return count;
        }

        /// <summary>场上是否已有自己的宠物 / 召唤物（服务端把宠物名写成「怪物名(主人名)」）</summary>
        private bool AutoPlayHasOwnPet()
        {
            if (string.IsNullOrEmpty(User.Name)) return false;

            string suffix = "(" + User.Name + ")";

            for (int i = 0; i < Objects.Count; i++)
            {
                MapObject ob = Objects[i];

                if (ob == null || ob.Dead || !(ob is MonsterObject)) continue;
                if (string.IsNullOrEmpty(ob.Name)) continue;

                if (ob.Name.EndsWith(suffix)) return true;
            }

            return false;
        }

        /// <summary>
        /// 法师专属的按需选技能（都不满足时返回 false，交给通用的远程技能/普攻流程）：
        /// ① 群怪抱团（十字范围 ≥ 3 只）→ 火墙，落在怪最密集的地方；
        /// ② 怪排成一条直线（同一方向 6 格内 ≥ 3 只）→ 疾光电影；
        /// ③ 不死系怪 → 圣言术。
        /// </summary>
        private bool AutoPlayMageCombat(MapObject target)
        {
            // ① 群怪：找十字范围里怪最多的那个点放火墙。
            // 火墙落点会持续十几秒，同一处反复放是白耗蓝，所以加个节流。
            if (CMain.Time >= _autoPlayNextFireWall)
            {
                MapObject cluster = AutoPlayFindFireWallSpot();

                if (cluster != null && AutoPlayTryCast(AutoPlayFireWallSpells, cluster))
                {
                    _autoPlayNextFireWall = CMain.Time + AutoPlayFireWallInterval;
                    return true;
                }
            }

            // ② 一条线：疾光电影（方向由目标决定，服务端沿该方向穿透 6 格）
            MapObject lineTarget = AutoPlayFindLineTarget();

            if (lineTarget != null && AutoPlayTryCast(AutoPlayLineSpells, lineTarget)) return true;

            // ③ 不死系：圣言术（服务端只对 Undead 怪物生效，客户端按名字关键字判断）。
            // 这招很费蓝、对高级不死系还容易失败，所以也加节流。
            if (CMain.Time >= _autoPlayNextSaint && AutoPlayIsUndead(target) &&
                AutoPlayTryCast(AutoPlayTurnUndeadSpells, target))
            {
                _autoPlayNextSaint = CMain.Time + AutoPlaySaintInterval;
                return true;
            }

            return false;
        }

        /// <summary>火墙落点：在附近怪里挑一个「十字范围内怪最多」的位置（火墙落点是十字 5 格）</summary>
        private MapObject AutoPlayFindFireWallSpot()
        {
            MapObject best = null;
            int bestCount = 0;

            for (int i = 0; i < Objects.Count; i++)
            {
                MapObject ob = Objects[i];

                if (!AutoPlayIsValidMonster(ob)) continue;
                if (Functions.MaxDistance(ob.CurrentLocation, User.CurrentLocation) > 8) continue;

                int count = AutoPlayCountMonsters(ob.CurrentLocation, 1);

                if (count <= bestCount) continue;

                best = ob;
                bestCount = count;
            }

            return bestCount >= AutoPlayClusterMinCount ? best : null;
        }

        /// <summary>
        /// 找一条「怪最多的直线」上的目标：沿 8 个方向各数 6 格（疾光电影最多穿透 6 格），
        /// 怪数达标（≥ AutoPlayLineMinCount）时返回该方向上最近的那只，方向由它决定。
        /// </summary>
        private MapObject AutoPlayFindLineTarget()
        {
            MapObject best = null;
            int bestCount = 0;

            for (int d = 0; d < 8; d++)
            {
                MirDirection dir = (MirDirection)d;
                int count = 0;
                MapObject first = null;

                for (int i = 1; i <= AutoPlayLineMaxTiles; i++)
                {
                    MapObject ob = AutoPlayMonsterAt(Functions.PointMove(User.CurrentLocation, dir, i));

                    if (ob == null) continue;

                    count++;

                    if (first == null) first = ob;
                }

                if (count < AutoPlayLineMinCount || count <= bestCount) continue;

                best = first;
                bestCount = count;
            }

            return best;
        }

        /// <summary>某个格子上是否有可攻击的怪（用于一条线的判定）</summary>
        private MapObject AutoPlayMonsterAt(Point location)
        {
            for (int i = 0; i < Objects.Count; i++)
            {
                MapObject ob = Objects[i];

                if (ob == null || ob.CurrentLocation != location) continue;
                if (!AutoPlayIsValidMonster(ob)) continue;

                return ob;
            }

            return null;
        }

        /// <summary>是否是可攻击的怪（排除宠物、智能宠物、忽略名单、守卫、AI 970）</summary>
        private bool AutoPlayIsValidMonster(MapObject ob)
        {
            if (ob == null || ob == User) return false;
            if (!(ob is MonsterObject)) return false;
            if (ob.Dead || ob.Hidden) return false;
            if (ob.Race == ObjectType.Creature) return false;                 // 智能宠物

            MonsterObject monster = (MonsterObject)ob;

            if (monster.AI == 970) return false;
            if (ob.Name != null && ob.Name.EndsWith(")")) return false;        // 玩家宠物
            if (AutoPlayIsIgnored(ob.Name)) return false;
            if (ob.NameColour == System.Drawing.Color.SkyBlue) return false;   // 守卫类怪物

            return true;
        }

        /// <summary>
        /// 是否是不死系怪物（圣言术只对不死系生效）。
        /// 客户端拿不到服务端的怪物 Undead 标记，这里按怪物名字里的关键字判断，
        /// 关键字可在 Mir2.ini 的 AutoSaintKeywords 里自行增删（英文逗号分隔）。
        /// </summary>
        private bool AutoPlayIsUndead(MapObject target)
        {
            if (target == null || string.IsNullOrEmpty(target.Name)) return false;

            string[] keywords = AutoPlayKeywords(Settings.AutoSaintKeywords, ref _autoSaintSource, ref _autoSaintKeywords);

            for (int i = 0; i < keywords.Length; i++)
            {
                string k = keywords[i].Trim();

                if (k.Length > 0 && target.Name.Contains(k)) return true;
            }

            return false;
        }

        /// <summary>
        /// 道士治疗：把「自己 / 自己的召唤宠物 / 队伍队友」里血量不足的单位找出来一起处理（射程 9 格）——
        /// 一片区域（3×3）里有 2 个及以上单位掉血 → 用**群体治疗术**（以掉血最集中的那个单位为中心，一次奶一片）；
        /// 只有 1 个单位掉血 → 用**单体治愈术**（治愈术 / 治愈术-秘籍），自己掉血优先治自己。
        /// 服务端的群体治疗术按落点 3×3 范围结算，只要 `IsFriendlyTarget` 为真就能被奶到
        /// （自己的宠物、队友都算），所以宠物也能一起回血。
        /// </summary>
        private void AutoPlayGroupHeal()
        {
            if (User.NextMagic != null) return;
            if (CMain.Time < _autoPlayNextGroupHeal) return;

            // ① 收集掉血到阈值以下的单位：自己 / 自己的召唤宠物 / 队伍队友
            List<MapObject> hurt = new List<MapObject>();

            if (User.PercentHealth < AutoPlayAllyHealPercent) hurt.Add(User);

            string suffix = string.IsNullOrEmpty(User.Name) ? null : "(" + User.Name + ")";
            List<string> group = GroupDialog.GroupList;

            for (int i = 0; i < Objects.Count; i++)
            {
                MapObject ob = Objects[i];

                if (ob == null || ob == User || ob.Dead) continue;
                if (ob.PercentHealth >= AutoPlayAllyHealPercent) continue;             // 血够
                if (!Functions.InRange(ob.CurrentLocation, User.CurrentLocation, 9)) continue;

                if (ob is MonsterObject)
                {
                    // 自己召唤的宠物（服务端把宠物名写成「怪物名(主人名)」）
                    if (suffix != null && ob.Name != null && ob.Name.EndsWith(suffix)) hurt.Add(ob);
                }
                else if (ob is PlayerObject)
                {
                    if (group != null && group.Contains(ob.Name)) hurt.Add(ob);        // 队伍队友
                }
            }

            if (hurt.Count == 0) return;

            // ② 挑「3×3 范围内掉血单位最多」的那个作为群体治疗术的落点（群体治疗术的作用范围）
            MapObject best = null;
            int bestScore = 0;

            for (int i = 0; i < hurt.Count; i++)
            {
                int score = 0;

                for (int j = 0; j < hurt.Count; j++)
                {
                    if (Functions.InRange(hurt[j].CurrentLocation, hurt[i].CurrentLocation, 1)) score++;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = hurt[i];
                }
            }

            if (best == null) return;

            _autoPlayNextGroupHeal = CMain.Time + 1000;

            // ③ 两个及以上挤在一起（全体失血）→ 群体治疗术，一次把大家奶上
            if (bestScore >= 2 && AutoPlayTryCast(AutoPlayMassHealSpells, best)) return;

            // ④ 只有一个单位掉血 → 单体治愈术；自己掉血优先治自己（治愈圆环等自身治疗技能也能用）
            MapObject single = hurt.Contains(User) ? User : best;

            AutoPlayTryCast(single == User ? AutoPlayHealSpells : AutoPlayAllyHealSpells, single);
        }

        /// <summary>自己身上是否挂着指定 buff（读客户端自身 buff 列表）</summary>
        private bool AutoPlayHasBuff(BuffType type)
        {
            var buffs = GameScene.Scene.BuffsDialog.Buffs;

            for (int i = 0; i < buffs.Count; i++)
                if (buffs[i].Type == type) return true;

            return false;
        }

        /// <summary>
        /// 身上是否带着护身符（道士隐身/幽灵盾/神圣战甲术的施法材料）。
        /// 与服务端 GetAmulet 一致：看装备槽（EquipmentSlot.护身符）、要求 Shape=0 且数量够。
        /// </summary>
        private bool AutoPlayHasAmulet()
        {
            return AutoPlayHasItem(ItemType.护身符, 0, 1);
        }

        /// <summary>
        /// 该技能要用的施法材料是否齐备。道士的符系技能服务端都要护身符：
        /// 灵魂火符 / 隐身术 / 幽灵盾 / 神圣战甲术 各 1 张；毒雾要 5 张符 + 5 个毒粉。
        /// 材料不够时服务端会静默失败却照样扣蓝，所以客户端先跳过，避免原地空放。
        /// </summary>
        private bool AutoPlayHasSpellMaterials(Spell spell)
        {
            switch (spell)
            {
                case Spell.SoulFireBall:
                case Spell.Hiding:
                case Spell.MassHiding:
                case Spell.SoulShield:
                case Spell.BlessedArmour:
                    return AutoPlayHasItem(ItemType.护身符, 0, 1);
                case Spell.PoisonCloud:
                    return AutoPlayHasItem(ItemType.护身符, 0, 5) && AutoPlayHasItem(ItemType.护身符, 1, 5);
                case Spell.HealingcircleRare:
                    return AutoPlayHasItem(ItemType.护身符, 0, 3);   // 阴阳五行阵-秘籍要 3 张符
                case Spell.SummonSkeleton:
                    return AutoPlayHasItem(ItemType.护身符, 0, 1);   // 召唤骷髅要 1 张符
                case Spell.SummonHolyDeva:
                    return AutoPlayHasItem(ItemType.护身符, 0, 2);   // 精魂召唤术（月灵）要 2 张符
                case Spell.SummonShinsu:
                    return AutoPlayHasItem(ItemType.护身符, 0, 5);   // 召唤神兽要 5 张符
                case Spell.Poisoning:
                    return AutoPlayHasItem(ItemType.护身符, 1, 1) || AutoPlayHasItem(ItemType.护身符, 2, 1);   // 施毒术要 1 个毒粉（黄/灰都行）
                default:
                    return true;      // 其余技能不需要材料
            }
        }

        /// <summary>装备槽里是否有一件指定类型/形态、剩余数量足够的物品（护身符、毒药等都放在装备槽）</summary>
        private bool AutoPlayHasItem(ItemType type, byte shape, int count)
        {
            for (int i = 0; i < User.Equipment.Length; i++)
            {
                UserItem item = User.Equipment[i];

                if (item == null || item.Info == null) continue;
                if (item.Info.Type != type) continue;
                if (item.Info.Shape != shape) continue;
                if (item.Count < count) continue;

                return true;
            }

            return false;
        }

        #region 毒符互换（道士）

        /// <summary>背包里找一件指定形态的符/毒（shape：0=护身符、1=灰色毒粉(绿毒)、2=黄色毒粉(红毒)），找不到返回 -1</summary>
        private int AutoPlayFindConsumable(byte shape)
        {
            if (User.Inventory == null) return -1;

            for (int i = 0; i < User.Inventory.Length; i++)
            {
                UserItem item = User.Inventory[i];

                if (item == null || item.Info == null) continue;
                if (item.Info.Type != ItemType.护身符) continue;
                if (item.Info.Shape != shape) continue;
                if (item.Count < 1) continue;

                return i;
            }

            return -1;
        }

        /// <summary>
        /// 把背包里的符/毒装到「护身符」槽。槽位被别的物品占着也没关系——
        /// 服务端会把它换回背包（EquipItem 是交换式移动）。返回是否已发出换装请求。
        /// </summary>
        private bool AutoPlayEquipConsumable(byte shape)
        {
            int index = AutoPlayFindConsumable(shape);

            if (index < 0) return false;

            UserItem item = User.Inventory[index];

            if (item == null || item.Info == null) return false;

            // 同一件物品刚换过又要求换（本地状态多半没同步回来）→ 不再重复发包，避免死循环
            if (item.UniqueID == _autoSwapLastID && CMain.Time - _autoSwapLastTime < 2000) return false;

            _autoSwapLastID = item.UniqueID;
            _autoSwapLastTime = CMain.Time;

            Network.Enqueue(new C.EquipItem
            {
                Grid = MirGridType.Inventory,
                UniqueID = item.UniqueID,
                To = (int)EquipmentSlot.护身符
            });

            return true;
        }

        /// <summary>
        /// 毒符互换（自动挂机用）：道士的「护身符（符纸）」和「毒药（黄色毒粉/灰色毒粉）」共用同一个装备槽，
        /// 内挂只在「接下来要放的那个技能用什么材料」发生变化时才换一次：
        ///  · 要放需要毒药的技能（施毒术）→ 槽里换成毒粉（两种毒粉按「一次使用交替一次」轮换）；
        ///  · 要放需要符纸的技能（灵魂火符主攻 / 隐身术 / 幽灵盾 / 神圣战甲术 / 召唤术）→ 槽里换成护身符。
        /// 判定是「粘住」的：槽里已经是当前需要的材料就一律不动，
        /// 所以正常节奏是「怪来了装毒 → 放施毒术 → 换回符 → 一直丢灵魂火符」，
        /// 不会出现两帧之间符/毒来回互切。
        /// 背包里没有对应的物品时不换（不会把槽里的东西卸掉）。
        /// 返回 true 表示本帧刚发出换装请求，调用方应跳过本帧战斗，等服务器回执。
        /// </summary>
        private bool AutoPlaySwapPoisonAmulet()
        {
            if (!Settings.AutoSwapPoison) return false;
            if (User.Class != MirClass.道士) return false;
            if (User.Inventory == null || User.Equipment == null) return false;
            if (User.NextMagic != null) return false;        // 正在施法（含玩家手动），放完再换
            if (CMain.Time < _autoManualHoldUntil) return false;   // 玩家刚手动换过装，这段时间以他为准
            if (CMain.Time < _autoPlayNextSwap) return false;

            if (User.ObjectID != _autoPoisonSwapUserID)
            {
                _autoPoisonSwapUserID = User.ObjectID;
                _autoPoisonNextShape = 2;                 // 新角色第一次先用黄色毒粉，之后黄/灰交替
            }

            // 判定用的目标：开了自动攻击时优先用当前锁定目标，没有就用和战斗同一套索敌找一只
            MapObject target = MapObject.TargetObject;

            if (!AutoPlaySwappableTarget(target) && Settings.AutoAttack) target = AutoPlayFindMonster();

            byte equipped = AutoPlayEquippedPoisonShape();   // 槽里现在是哪种毒（0=不是毒，多半是符）

            // ① 接下来要放「施毒术」这种吃毒药的技能 → 槽里放毒
            byte wantPoison;

            if (AutoPlayWantPoison(target, out wantPoison))
            {
                if (equipped == wantPoison) return false;    // 槽里正好是要用的那种毒：粘住，等施毒术放出去

                // 背包里没有想要的颜色就用另一种；另一种目标身上已经有了也不换（换了也白换）
                byte load = 0;

                if (AutoPlayFindConsumable(wantPoison) >= 0) load = wantPoison;
                else
                {
                    byte other = (byte)(wantPoison == 1 ? 2 : 1);

                    if (AutoPlayFindConsumable(other) >= 0 && !AutoPlayPoisoned(target, other)) load = other;
                }

                // 背包里有能用的毒、且槽里不是这种 → 换毒
                if (load != 0 && equipped != load && AutoPlayEquipConsumable(load))
                {
                    AutoPlayUsePoisonShape(load);                            // 这次用的是它 → 下次换另一种
                    _autoPlayNextSwap = CMain.Time + AutoPlaySwapInterval;
                    return true;
                }

                // 背包里根本没有能用的毒 → 落到下面按「需要符纸的技能」处理，
                // 否则会一直空着毒槽、连灵魂火符都放不出来
            }

            // ② 不需要毒 → 接下来要放「需要符纸」的技能（灵魂火符主攻 / 隐身 / 护盾 / 召唤）才换符
            if (AutoPlayHasAmulet()) return false;                       // 槽里已经是符，不用换
            if (!AutoPlayWantAmulet(target)) return false;               // 眼下既不用毒也不用符 → 保持现状
            if (!AutoPlayEquipConsumable(0)) return false;               // 背包里没有符，不动

            _autoPlayNextSwap = CMain.Time + AutoPlaySwapInterval;
            return true;
        }

        /// <summary>目标身上是否已经中了指定形态的毒（1=灰色毒粉→绿毒 2=黄色毒粉→红毒）</summary>
        private static bool AutoPlayPoisoned(MapObject target, byte shape)
        {
            if (target == null) return false;

            return shape == 1
                ? (target.Poison & PoisonType.Green) == PoisonType.Green
                : (target.Poison & PoisonType.Red) == PoisonType.Red;
        }

        /// <summary>
        /// 下一次「施毒术」要装哪种毒粉（1=灰色毒粉(绿毒) 2=黄色毒粉(红毒)）——
        /// 两种毒粉「一次使用交替一次」，第一次是黄色毒粉。
        /// </summary>
        private static byte AutoPlayNextPoisonShape()
        {
            return _autoPoisonNextShape == 1 ? (byte)1 : (byte)2;
        }

        /// <summary>
        /// 一次「施毒术」真的用掉了某种毒粉 —— 才把下一次要用的毒粉换成另一种（黄 → 灰 → 黄 …）。
        /// 只有「确实要放出去的那一次」才算一次使用，所以不会一个技能被反复判定时来回乱切。
        /// </summary>
        private static void AutoPlayUsePoisonShape(int shape)
        {
            if (shape != 1 && shape != 2) return;

            _autoPoisonNextShape = shape == 2 ? (byte)1 : (byte)2;
        }

        /// <summary>
        /// 眼下「需要毒药」（out shape：槽里应该装那种毒，1=灰色毒粉 2=黄色毒粉）：
        /// 要放「施毒术」——学了施毒术 + 有可下毒的目标 + 目标还缺某种毒。
        /// 两种毒都缺时按「黄/灰交替」的顺序挑一种。
        /// 注意：槽里放的**正好是目标还缺的那种毒**时同样返回 true（shape 就是槽里那种毒），
        /// 表示「还需要毒、保持住」——只有返回 false 才轮到「换符」那一支，
        /// 否则会出现「装上毒 → 立刻换回符 → 又换毒」的来回抖动。
        /// </summary>
        private bool AutoPlayWantPoison(MapObject target, out byte shape)
        {
            shape = 0;

            if (User.GetMagic(Spell.Poisoning) == null) return false;         // 没学施毒术
            if (!AutoPlaySwappableTarget(target)) return false;               // 没有可下毒的目标

            bool needGreen = (target.Poison & PoisonType.Green) != PoisonType.Green;
            bool needRed = (target.Poison & PoisonType.Red) != PoisonType.Red;

            if (!needGreen && !needRed) return false;                         // 两种毒都上了，毒药没用了

            byte equipped = AutoPlayEquippedPoisonShape();

            // 槽里已经是「目标还缺的那种毒」→ 继续保持，等施毒术把它放出去（不要去换符）
            if (equipped == 1 && needGreen) { shape = 1; return true; }
            if (equipped == 2 && needRed) { shape = 2; return true; }

            if (needGreen && needRed) shape = AutoPlayNextPoisonShape();      // 两种都缺 → 按黄/灰交替
            else if (needGreen) shape = 1;
            else shape = 2;

            return true;
        }

        /// <summary>
        /// 眼下要不要把槽里换成护身符（符纸）——符系技能都要它：
        /// 正在打怪时主攻的「灵魂火符」要符；没打怪时补幽灵盾 / 神圣战甲术 / 隐身术 / 召唤也都要符。
        /// </summary>
        private bool AutoPlayWantAmulet(MapObject target)
        {
            if (AutoPlaySwappableTarget(target)) return true;                  // 正在打怪：主攻灵魂火符要符

            if (User.GetMagic(Spell.SoulShield) != null && !AutoPlayHasBuff(BuffType.幽灵盾)) return true;
            if (User.GetMagic(Spell.BlessedArmour) != null && !AutoPlayHasBuff(BuffType.神圣战甲术)) return true;
            if (AutoPlayShouldHide() && !AutoPlayHasBuff(BuffType.隐身术)) return true;
            if (AutoPlayNeedSummon()) return true;                             // 还有召唤物没养齐

            return false;
        }

        /// <summary>是否还有「已学会但场上没有」的召唤物需要补（骷髅 / 神兽 / 月灵）</summary>
        private bool AutoPlayNeedSummon()
        {
            for (int i = 0; i < AutoPlaySummonSpells.Length; i++)
            {
                if (User.GetMagic(AutoPlaySummonSpells[i]) == null) continue;   // 没学这种召唤术
                if (AutoPlayPetCount(i) > 0) continue;                          // 这只已经养着了

                return true;
            }

            return false;
        }

        /// <summary>
        /// 服务端 GetPoison 取装备槽里第一个毒药（可能是右手镯这类槽位），返回它的形态：
        /// 1=灰色毒粉（绿毒）2=黄色毒粉（红毒），0=装备槽里没有毒
        /// </summary>
        private byte AutoPlayEquippedPoisonShape()
        {
            if (User.Equipment == null) return 0;

            for (int i = 0; i < User.Equipment.Length; i++)
            {
                UserItem item = User.Equipment[i];

                if (item == null || item.Info == null) continue;
                if (item.Info.Type != ItemType.护身符) continue;
                if (item.Count < 1) continue;
                if (item.Info.Shape != 1 && item.Info.Shape != 2) continue;

                return (byte)item.Info.Shape;
            }

            return 0;
        }

        /// <summary>毒符互换的判定目标：必须是可以下毒的正常怪物（跳过宠物、守卫、忽略名单）</summary>
        private bool AutoPlaySwappableTarget(MapObject target)
        {
            if (target == null || target.Dead) return false;
            if (!(target is MonsterObject)) return false;
            if (target.Race == ObjectType.Creature) return false;
            if (target.Name != null && target.Name.EndsWith(")")) return false;      // 玩家宠物
            if (target.NameColour == System.Drawing.Color.SkyBlue) return false;     // 守卫类
            if (AutoPlayIsIgnored(target.Name)) return false;

            return true;
        }

        /// <summary>
        /// 道士是否需要对这个目标放施毒术：看装备槽里现在是哪种毒（服务端 GetPoison 取到的那种），
        /// 目标身上缺这种毒才放（与服务端道士英雄的判定一致）。
        /// </summary>
        private bool AutoPlayNeedPoison(MapObject target)
        {
            if (target == null || target.Dead) return false;

            byte shape = AutoPlayEquippedPoisonShape();

            if (shape == 1) return (target.Poison & PoisonType.Green) != PoisonType.Green;
            if (shape == 2) return (target.Poison & PoisonType.Red) != PoisonType.Red;

            return false;
        }

        #endregion

        #region 毒符互换（手动施法按需换装，不依赖内挂总开关）

        /// <summary>
        /// 技能要用的材料：0=护身符（符纸） 1=灰色毒粉(绿毒) 2=黄色毒粉(红毒) 3=任意毒粉（黄/灰交替）
        /// -1=不吃符也不吃毒。
        /// 与服务端《技能消耗》一一对应：
        ///  · 符：灵魂火符 / 隐身术 / 集体隐身术 / 幽灵盾 / 神圣战甲术 / 困魔咒 / 诅咒术 / 强化术 /
        ///        幻影术 / 阴阳五行阵-秘籍(3张) / 召唤骷髅(1) / 召唤神兽(5) / 精魂召唤术(2) / 回生术(3)；
        ///  · 毒：施毒术（黄色毒粉或灰色毒粉都行）；
        ///  · 毒雾、瘟疫术要「符 + 毒」同时消耗，一个槽位满足不了，交给玩家自己安排（返回 -1 不动）。
        /// </summary>
        private static int AutoPlaySpellMaterial(Spell spell)
        {
            switch (spell)
            {
                case Spell.Poisoning:
                    return 3;

                case Spell.SoulFireBall:
                case Spell.Hiding:
                case Spell.MassHiding:
                case Spell.SoulShield:
                case Spell.BlessedArmour:
                case Spell.TrapHexagon:
                case Spell.Curse:
                case Spell.UltimateEnhancer:
                case Spell.Hallucination:
                case Spell.Reincarnation:
                case Spell.HealingcircleRare:
                case Spell.SummonSkeleton:
                case Spell.SummonShinsu:
                case Spell.SummonHolyDeva:
                    return 0;

                default:
                    return -1;
            }
        }

        /// <summary>护身符槽里现在装的是什么：0=符 1=灰色毒粉 2=黄色毒粉 -1=空</summary>
        private int AutoPlayEquippedMaterial()
        {
            byte poison = AutoPlayEquippedPoisonShape();

            if (poison != 0) return poison;

            return AutoPlayHasAmulet() ? 0 : -1;
        }

        /// <summary>
        /// 手动施法前的材料准备——面板勾选「毒符互换」后**不需要开内挂总开关**：
        /// 玩家按技能键 / 点技能栏时，先看这个技能吃什么材料（吃符换符、吃毒换毒），
        /// 材料已经就位就照常施放；需要换装时先把换装请求发出去并记住这个技能，
        /// 等材料到位后由 AutoPlayRetryManualSpell() 自动把它补放出去。
        /// 返回 true 表示本帧先别施放（等换装回执）。
        ///
        /// 关键：**一次技能操作只判定一次材料**。只要上一次的换装还在路上，就直接等它换好，
        /// 绝不再重新算一遍「这次该装哪种毒」——否则黄色毒粉 / 灰色毒粉会被一帧一帧地翻来翻去
        /// （补放重入 UseSpell 时尤其明显），就是「一用需要毒的技能就疯狂换毒」的原因。
        /// </summary>
        public bool AutoPlayPrepareManualSpell(ClientMagic magic)
        {
            // 这是「材料换好后」的补放重入（AutoPlayRetryManualSpell 里回调 UseSpell），
            // 材料已经校验过了，直接放行，不再判材料。
            if (_manualSpellCasting) return false;

            if (!Settings.AutoSwapPoison) return false;
            if (User == null || User.Class != MirClass.道士) return false;
            if (User.Inventory == null || User.Equipment == null) return false;

            // 换角色 / 重新上线：毒粉交替从头开始（第一次施毒术用黄色毒粉）
            if (User.ObjectID != _autoPoisonSwapUserID)
            {
                _autoPoisonSwapUserID = User.ObjectID;
                _autoPoisonNextShape = 2;
            }

            int want = AutoPlaySpellMaterial(magic.Spell);

            if (want < 0) return false;                       // 这个技能不吃符也不吃毒，不动

            // 上一次手动换装还没回来（第一次按下去的那次还没放出去）：
            //  · 还是同一个技能 / 同样是「施毒术」→ 继续等它换好，不要重新判定材料；
            //  · 换成别的技能了 → 放弃上一次的等待，按新技能重新准备。
            if (_manualSpellKey != 0 && CMain.Time <= _manualSpellUntil)
            {
                if (_manualSpellKey == magic.Key) return true;
                if (want == 3 && _manualSpellIsPoison) return true;

                _manualSpellKey = 0;
                _manualSpellIsPoison = false;
            }

            bool isPoison = want == 3;                        // 施毒术：黄色毒粉 / 灰色毒粉都能放

            if (isPoison)
            {
                MapObject target = MapObject.TargetObject;

                if (!AutoPlaySwappableTarget(target) && Settings.AutoAttack) target = AutoPlayFindMonster();

                byte poison;

                // 目标还缺某种毒 → 就用缺的那种；目标不缺毒 / 没目标 → 按「上一次用的另一种」交替
                if (AutoPlayWantPoison(target, out poison)) want = poison;
                else want = AutoPlayNextPoisonShape();
            }

            int load = want;

            // 想要的那种毒背包里没有 → 用另一种颜色的毒粉顶上（施毒术两种毒粉都能放）
            if ((load == 1 || load == 2) && AutoPlayFindConsumable((byte)load) < 0)
            {
                byte other = (byte)(load == 1 ? 2 : 1);

                if (AutoPlayFindConsumable(other) >= 0) load = other;
            }

            if (AutoPlayEquippedMaterial() == load)
            {
                // 槽里已经是要用的材料 → 这一次就算用掉了它，下一次施毒术换另一种毒粉
                if (isPoison) AutoPlayUsePoisonShape(load);

                return false;                                 // 照常施放
            }

            if (!AutoPlayEquipConsumable((byte)load))
                return false;                                 // 背包里没有 → 照常尝试（服务端自己会判失败）

            _manualSpellKey = magic.Key;
            _manualSpellWant = load;
            _manualSpellIsPoison = isPoison;
            _manualSpellTime = CMain.Time;
            _manualSpellUntil = CMain.Time + 3000;            // 3 秒内材料还没到位就放弃这次补放
            _autoManualHoldUntil = CMain.Time + 3000;         // 静默期：自动逻辑这段时间别把槽翻回去
            _autoPlayNextSwap = CMain.Time + AutoPlaySwapInterval;

            return true;                                      // 先别放，等材料到位的回执
        }

        /// <summary>
        /// 手动施法因为要先换装而被推迟时的「补放」：材料真的换到位了就自动把玩家原本要放的那个技能放出去。
        /// （等本地装备槽里看到目标材料才动，一般换装回执 100ms 内就回来了，手动体验几乎无延迟。）
        /// </summary>
        private void AutoPlayRetryManualSpell()
        {
            if (_manualSpellKey == 0) return;

            if (User == null || User.Dead || CMain.Time > _manualSpellUntil || User.NextMagic != null)
            {
                _manualSpellKey = 0;
                _manualSpellIsPoison = false;
                return;
            }

            if (CMain.Time - _manualSpellTime < 120) return;         // 刚发出换装请求，稍等一下
            if (AutoPlayEquippedMaterial() != _manualSpellWant) return;  // 材料还没换到位

            int key = _manualSpellKey;
            bool poison = _manualSpellIsPoison;
            int used = _manualSpellWant;

            _manualSpellKey = 0;
            _manualSpellIsPoison = false;

            // 这一次「施毒术」到这里才算真用掉：下一次自动换另一种毒粉（黄 → 灰 → 黄 …）
            if (poison) AutoPlayUsePoisonShape(used);

            // 补放期间重入 UseSpell 时不要再判材料（见 AutoPlayPrepareManualSpell 开头）
            _manualSpellCasting = true;

            try
            {
                GameScene.Scene.UseSpell(key);                            // 重新执行玩家那次技能操作
            }
            finally
            {
                _manualSpellCasting = false;
            }
        }

        #endregion

        #region 道士隐身（隐身术 / 原地不动 / 贴身近砍）

        //隐身术的触发条件：身上有自己的召唤宠物 + 附近（索敌范围内）有 2 只以上怪物
        private const int AutoPlayHideMonsterCount = 2;
        private const int AutoPlayHideDetectRange = 8;      // 判断「附近有几只怪」的半径（不超过灵魂火符射程 9）

        private const long AutoPlayMeleeAttackerAlive = 2500;   // 最近一次「被谁打」的有效时间窗

        private const long AutoPlayHideIdleBreakTime = 25000;   // 隐身期间连续这么久没打到怪 → 自动解除隐身
        private const long AutoPlayHidePoisonBreakTime = 25000; // 中毒持续这么久 → 自动解除隐身
        private const long AutoPlayHideBanTime = 8000;          // 解除隐身后这段时间内不再隐身

        private static bool _autoHoldStill;             // 本帧是否隐身原地不动（每帧在 ProcessAutoPlay 里刷新）
        private static long _autoHiddenSince;           // 本次隐身的开始时间（0=当前没隐身）
        private static long _autoHiddenLastAttack;      // 隐身后最近一次对怪物出手的时间
        private static long _autoHiddenBreakUntil;      // 强制「解除隐身、放行移动」的截止时间
        private static long _autoSelfPoisonSince;       // 自身中毒的开始时间（0=没中毒）

        /// <summary>隐身术是否生效中（道士）</summary>
        private bool AutoPlayHidden()
        {
            return User.Class == MirClass.道士 && AutoPlayHasBuff(BuffType.隐身术);
        }

        /// <summary>自己是否处于中毒状态（绿毒 / 红毒）</summary>
        private bool AutoPlaySelfPoisoned()
        {
            return User.Poison.HasFlag(PoisonType.Green) || User.Poison.HasFlag(PoisonType.Red);
        }

        /// <summary>
        /// 隐身状态维护（每帧调一次），处理两条「自动解除隐身」规则：
        ///  ① 隐身期间 25 秒没有攻击到怪物（够不到 / 没材料等）→ 解除隐身，主动去打；
        ///  ② 自己处于中毒状态 → 不进入隐身；已经在隐身中又中毒满 25 秒 → 解除隐身，主动去打。
        /// 解除的方式是放开移动限制（服务端走 / 跑会 RemoveBuff(隐身术)），
        /// 并在 AutoPlayHideBanTime 内不再隐身，先把该打的怪打掉。
        /// </summary>
        private void AutoPlayUpdateHiding(long now)
        {
            if (AutoPlaySelfPoisoned())
            {
                if (_autoSelfPoisonSince == 0) _autoSelfPoisonSince = now;
            }
            else
            {
                _autoSelfPoisonSince = 0;
            }

            if (!AutoPlayHidden())
            {
                _autoHiddenSince = 0;
                return;
            }

            if (_autoHiddenSince == 0)
            {
                _autoHiddenSince = now;
                _autoHiddenLastAttack = now;
            }

            // ① 隐身这么久一直没打到怪 → 解除隐身主动去打
            if (now - _autoHiddenLastAttack >= AutoPlayHideIdleBreakTime)
            {
                _autoHiddenBreakUntil = now + AutoPlayHideBanTime;
                return;
            }

            // ② 中毒满 25 秒 → 解除隐身主动去打
            if (_autoSelfPoisonSince != 0 && now - _autoSelfPoisonSince >= AutoPlayHidePoisonBreakTime)
                _autoHiddenBreakUntil = now + AutoPlayHideBanTime;
        }

        /// <summary>判断「附近有几只怪」用的半径：不超过灵魂火符射程（9），也不超过索敌半径</summary>
        private static int AutoPlayHideRange()
        {
            return Math.Min(Settings.AutoSearchRange, AutoPlayHideDetectRange);
        }

        /// <summary>
        /// 要不要用隐身术：道士的经典玩法——召唤宠物顶怪，自己隐身在一旁用灵魂火符远程输出。
        /// 只在「有召唤宠物」并且「附近有 2 只以上怪物」时才用（怪太少没必要，也没宠物配合）；
        /// 处于**中毒状态**时不隐身，刚被解除隐身（AutoPlayHideBanTime）内也不再隐身。
        /// 隐身期间服务端走 / 跑都会立刻解除隐身，所以隐身时不移动（见 AutoPlayHoldStill）。
        /// </summary>
        private bool AutoPlayShouldHide()
        {
            if (User.GetMagic(Spell.Hiding) == null) return false;
            if (!Settings.AutoSkill) return false;                        // 不放技能时隐身没有意义（只会站着挨打）
            if (AutoPlaySelfPoisoned()) return false;                     // 中毒状态不进入隐身
            if (CMain.Time < _autoHiddenBreakUntil) return false;          // 刚解除隐身，先主动打一会儿
            if (!AutoPlayHasOwnPet()) return false;                       // 没有召唤宠物不隐身

            return AutoPlayCountMonsters(User.CurrentLocation, AutoPlayHideRange()) >= AutoPlayHideMonsterCount;
        }

        /// <summary>
        /// 隐身期间原地不动（服务端走一步就会解除隐身），只用灵魂火符、施毒术这类远程技能输出。
        /// 只有「射程内一只可打的怪都没有」或「已判定要解除隐身主动去打」时才放行移动，避免彻底卡死。
        /// </summary>
        private bool AutoPlayHoldStill()
        {
            if (!AutoPlayHidden()) return false;
            if (CMain.Time < _autoHiddenBreakUntil) return false;          // 已判定解除隐身 → 放开移动

            return AutoPlayFindMonster(AutoPlayHideRange()) != null;
        }

        /// <summary>
        /// 是否有怪物贴着身（1 格内）正在近身攻击玩家——隐身后只有这种情况才用近身砍：
        /// 优先用 S.Struck 记下的攻击者（真的打到我身上才算「近攻」），
        /// 退而用怪物自身的攻击目标（客户端远程攻击动作会带 TargetID）。
        /// </summary>
        private bool AutoPlayMonsterMeleeAdjacent(MapObject target)
        {
            if (!AutoPlaySwappableTarget(target)) return false;
            if (Functions.MaxDistance(target.CurrentLocation, User.CurrentLocation) > 1) return false;

            if (target.ObjectID == GameScene.LastStruckAttackerID &&
                CMain.Time - GameScene.LastStruckAttackerTime <= AutoPlayMeleeAttackerAlive) return true;

            MonsterObject monster = target as MonsterObject;

            return monster != null && monster.TargetID == User.ObjectID;
        }

        #endregion

        /// <summary>
        /// 跑图找怪：攻击半径扩大一倍后仍视野内有怪就追过去（有怪自然转入战斗）；
        /// 彻底没怪时每 1.5 秒随机换一个方向游走，实现自动跑图。
        /// </summary>
        private void AutoPlayRoam()
        {
            Point cur = User.CurrentLocation;

            if (cur != _autoRoamLastPos)
            {
                _autoRoamLastPos = cur;
                _autoRoamLastPosTime = CMain.Time;
            }

            // 扩大一倍半径追视野内的怪（进入攻击半径后自然转入战斗）；
            // 直线追怪被地形卡住（2.5 秒位置没动）时，暂时转随机游走绕路，4 秒后再试
            if (CMain.Time >= _autoRoamWanderUntil)
            {
                MapObject far = AutoPlayFindMonster(Settings.AutoSearchRange * 2);

                if (far != null)
                {
                    if (CMain.Time - _autoRoamLastPosTime > 2500)
                    {
                        _autoRoamWanderUntil = CMain.Time + 4000;
                    }
                    else if (Settings.AutoMove)
                    {
                        AutoPlayStepTo(far.CurrentLocation);
                        return;
                    }
                }
            }

            if (!Settings.AutoMove || CMain.Time < _autoRoamNextTime) return;

            // 没怪可追：沿当前方向持续游走跑图，每 3 秒随机换一次方向
            _autoRoamNextTime = CMain.Time + 600;

            if (CMain.Time >= _autoRoamDirTime)
            {
                _autoRoamDirTime = CMain.Time + 3000;
                _autoRoamDir = (MirDirection)CMain.Random.Next(8);
            }

            AutoPlayStepTo(Functions.PointMove(User.CurrentLocation, _autoRoamDir, 3));
        }

        /// <summary>在搜索半径内挑选最近的可攻击怪物（range=0 时取配置的 AutoSearchRange）</summary>
        private MapObject AutoPlayFindMonster(int range = 0)
        {
            if (range <= 0) range = Settings.AutoSearchRange;

            MapObject best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < Objects.Count; i++)
            {
                MapObject ob = Objects[i];

                if (ob == null || ob == User) continue;
                if (!(ob is MonsterObject)) continue;

                MonsterObject monster = (MonsterObject)ob;

                if (monster.Dead || monster.Hidden) continue;
                if (monster.Race == ObjectType.Creature) continue;   // 智能宠物不攻击
                if (monster.AI == 970) continue;                     // 与客户端既有逻辑保持一致：该 AI 不作为目标

                // 打不中检测拉黑期间的目标不重复选中
                if (monster.ObjectID == _autoStuckTargetID && CMain.Time < _autoStuckBlackUntil) continue;

                // 魔法 + 物理都打不动的怪物（同名）一段时间内不再选，直接找下一只
                if (_autoImmuneBlackName != null && CMain.Time < _autoImmuneBlackUntil &&
                    monster.Name == _autoImmuneBlackName) continue;

                // 玩家自己的宠物（名字带括号）不攻击
                if (monster.Name != null && monster.Name.EndsWith(")")) continue;

                // 配置的忽略关键字（守卫、NPC 类怪物等）
                if (AutoPlayIsIgnored(monster.Name)) continue;

                // 守卫类怪物（名字天蓝色）不作为攻击目标
                if (monster.NameColour == System.Drawing.Color.SkyBlue) continue;

                if (!Functions.InRange(monster.CurrentLocation, User.CurrentLocation, range)) continue;

                int distance = Functions.MaxDistance(monster.CurrentLocation, User.CurrentLocation);

                if (distance >= bestDistance) continue;

                best = monster;
                bestDistance = distance;
            }

            return best;
        }

        private static bool AutoPlayIsIgnored(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            string[] keywords = AutoPlayKeywords(Settings.AutoAttackIgnore, ref _autoIgnoreSource, ref _autoIgnoreKeywords);

            for (int i = 0; i < keywords.Length; i++)
            {
                if (name.Contains(keywords[i].Trim())) return true;
            }

            return false;
        }

        //关键字串被拆成数组后会缓存下来（这两个方法每帧都会被调用很多次，避免反复 Split 产生垃圾）
        private static string _autoIgnoreSource;
        private static string[] _autoIgnoreKeywords;
        private static string _autoSaintSource;
        private static string[] _autoSaintKeywords;

        private static string[] AutoPlayKeywords(string value, ref string cachedSource, ref string[] cached)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (cached == null) cached = new string[0];

                return cached;
            }

            if (!string.Equals(value, cachedSource, StringComparison.Ordinal))
            {
                cachedSource = value;
                cached = value.Split(new[] { ',', '，', '|' }, StringSplitOptions.RemoveEmptyEntries);
            }

            return cached ?? new string[0];
        }

        /// <summary>自动捡物：脚下的直接拾取，远处的（开启自动走位时）走过去；有可捡物品返回 true</summary>
        private bool AutoPlayPickUpItem()
        {
            MapObject nearest = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < Objects.Count; i++)
            {
                MapObject ob = Objects[i];

                if (!(ob is ItemObject)) continue;
                if (!Functions.InRange(ob.CurrentLocation, User.CurrentLocation, Settings.AutoSearchRange)) continue;

                int distance = Functions.MaxDistance(ob.CurrentLocation, User.CurrentLocation);

                if (distance >= bestDistance) continue;

                nearest = ob;
                bestDistance = distance;
            }

            if (nearest == null) return false;

            if (nearest.CurrentLocation == User.CurrentLocation)
            {
                if (CMain.Time > GameScene.PickUpTime)
                {
                    GameScene.PickUpTime = CMain.Time + 200;
                    Network.Enqueue(new C.PickUp());
                }

                return true;
            }

            if (Settings.AutoMove)
            {
                AutoPlayStepTo(nearest.CurrentLocation);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 朝目标点移动：优先跑动（开启走位跑步、跑得动、且路程 ≥ 2 格时跑 2/3 格），
        /// 跑不动或被墙挡时降级为走一步（正方向走不通时尝试左右偏移）。
        /// </summary>
        private void AutoPlayStepTo(Point destination)
        {
            MirDirection dir = Functions.DirectionFromPoint(User.CurrentLocation, destination);

            // 跑动：与 CheckInput 的手动跑步条件保持一致
            if (Settings.AutoMoveRun &&
                (GameScene.CanRun || Settings.NoRunUp) && CMain.Time > GameScene.NextRunTime &&
                User.HP >= 10 && (!User.Sneaking || (User.Sneaking && User.Sprint)) &&
                Functions.MaxDistance(User.CurrentLocation, destination) >= 2)
            {
                int distance = User.RidingMount || (User.Sprint && !User.Sneaking) ? 3 : 2;

                bool fail = false;
                for (int i = 1; i <= distance; i++)
                {
                    if (!CheckDoorOpen(Functions.PointMove(User.CurrentLocation, dir, i))) fail = true;
                }

                if (!fail && CanRun(dir))
                {
                    User.QueuedAction = new QueuedAction
                    {
                        Action = MirAction.跑步动作,
                        Direction = dir,
                        Location = Functions.PointMove(User.CurrentLocation, dir, distance)
                    };

                    return;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                MirDirection tryDir = i switch
                {
                    0 => dir,
                    1 => Functions.PreviousDir(dir),
                    _ => Functions.NextDir(dir),
                };

                if (!ValidPoint(Functions.PointMove(User.CurrentLocation, tryDir, 1))) continue;
                if (!CanWalk(tryDir, out MirDirection outDir)) continue;

                Point next = Functions.PointMove(User.CurrentLocation, outDir, 1);

                if (!CheckDoorOpen(next)) continue;

                User.QueuedAction = new QueuedAction
                {
                    Action = MirAction.行走动作,
                    Direction = outDir,
                    Location = next
                };

                return;
            }
        }

        /// <summary>自动喝药：只使用普通药水(Shape 0)与太阳水(Shape 1)</summary>
        private void AutoPlayUsePotion()
        {
            int maxHP = User.Stats[Stat.HP];
            int maxMP = User.Stats[Stat.MP];

            bool needHP = Settings.AutoPotHP && maxHP > 0 && User.HP * 100 / maxHP <= Settings.AutoPotHPPercent;
            bool needMP = Settings.AutoPotMP && maxMP > 0 && User.MP * 100 / maxMP <= Settings.AutoPotMPPercent;

            if (!needHP && !needMP) return;

            UserItem hpItem = null, mpItem = null;

            for (int i = 0; i < User.Inventory.Length; i++)
            {
                UserItem item = User.Inventory[i];

                if (item == null || item.Info == null) continue;
                if (item.Info.Type != ItemType.药水) continue;
                if (item.Info.Shape != 0 && item.Info.Shape != 1) continue;

                if (hpItem == null && item.Info.Stats[Stat.HP] > 0) hpItem = item;
                if (mpItem == null && item.Info.Stats[Stat.MP] > 0) mpItem = item;

                if (hpItem != null && mpItem != null) break;
            }

            UserItem use = needHP ? hpItem : null;

            if (use == null && needMP) use = mpItem;
            if (use == null) return;

            if (CMain.Time < GameScene.UseItemTime) return;

            GameScene.UseItemTime = CMain.Time + 300;

            Network.Enqueue(new C.UseItem { UniqueID = use.UniqueID, Grid = MirGridType.Inventory });
        }

        #endregion

        private void CheckInput()
        {
            if (AwakeningAction == true) return;

            if ((MouseControl == this) && (MapButtons != MouseButtons.None)) AutoHit = false;//mouse actions stop mining even when frozen!
            if (!CanRideAttack()) AutoHit = false;
            
            if (CMain.Time < InputDelay || User.Poison.HasFlag(PoisonType.Paralysis) || User.Poison.HasFlag(PoisonType.LRParalysis) || User.Poison.HasFlag(PoisonType.Frozen) || User.Fishing) return;
            
            if (User.NextMagic != null && !User.RidingMount)
            {
                UseMagic(User.NextMagic, User);
                return;
            }

            if (CMain.Time < User.BlizzardStopTime || CMain.Time < User.GreatFireBallRareStopTime || CMain.Time < User.ReincarnationStopTime) return;

            if (MapObject.TargetObject != null && !MapObject.TargetObject.Dead)
            {
                if (((MapObject.TargetObject.Name.EndsWith(")") || MapObject.TargetObject is PlayerObject) && CMain.Shift) ||
                    (!MapObject.TargetObject.Name.EndsWith(")") && MapObject.TargetObject is MonsterObject))
                {
                    GameScene.LogTime = CMain.Time + Globals.LogDelay;

                    if (User.Class == MirClass.弓箭 && User.HasClassWeapon && !User.RidingMount && !User.Fishing)//ArcherTest - non aggressive targets (player / pets)
                    {
                        if (Functions.InRange(MapObject.TargetObject.CurrentLocation, User.CurrentLocation, Globals.MaxAttackRange))
                        {
                            if (CMain.Time > GameScene.AttackTime)
                            {
                                User.QueuedAction = new QueuedAction { Action = MirAction.远程攻击1, Direction = Functions.DirectionFromPoint(User.CurrentLocation, MapObject.TargetObject.CurrentLocation), Location = User.CurrentLocation, Params = new List<object>() };
                                User.QueuedAction.Params.Add(MapObject.TargetObject != null ? MapObject.TargetObject.ObjectID : (uint)0);
                                User.QueuedAction.Params.Add(MapObject.TargetObject.CurrentLocation);

                                // MapObject.TargetObject = null; //stop constant attack when close up
                            }
                        }
                        else
                        {
                            if (CMain.Time >= OutputDelay)
                            {
                                OutputDelay = CMain.Time + 1000;
                                GameScene.Scene.OutputMessage("目标距离太远");
                            }
                        }
                        //  return;
                    }

                    else if ((!(Settings.AutoPlay && User.Class == MirClass.法师) || AutoPlayIsPhysicalTarget(MapObject.TargetObject)) &&
                             Functions.InRange(MapObject.TargetObject.CurrentLocation, User.CurrentLocation, 1))
                    {
                        // 法师挂机时一般不近攻（改用与弓手相同的远程方式：放法术），贴脸由内挂的走位拉开；
                        // 但若该怪物魔法无效（内挂已切物理阶段），则允许贴身用「近距攻击1」出手。
                        if (CMain.Time > GameScene.AttackTime && CanRideAttack() && !User.Poison.HasFlag(PoisonType.Dazed))
                        {
                            User.QueuedAction = new QueuedAction { Action = MirAction.近距攻击1, Direction = Functions.DirectionFromPoint(User.CurrentLocation, MapObject.TargetObject.CurrentLocation), Location = User.CurrentLocation };
                            return;
                        }
                    }
                }
            }
            if (AutoHit && !User.RidingMount)
            {
                if (CMain.Time > GameScene.AttackTime)
                {
                    User.QueuedAction = new QueuedAction { Action = MirAction.挖矿动作, Direction = User.Direction, Location = User.CurrentLocation };
                    return;
                }
            }

            
            MirDirection direction;
            if (MouseControl == this)
            {
                direction = MouseDirection();
                if (AutoRun)
                {
                    if ((GameScene.CanRun || Settings.NoRunUp) && CanRun(direction) && CMain.Time > GameScene.NextRunTime && User.HP >= 10 && (!User.Sneaking || (User.Sneaking && User.Sprint))) //slow remove
                    {
                        int distance = User.RidingMount || User.Sprint && !User.Sneaking ? 3 : 2;
                        bool fail = false;
                        for (int i = 1; i <= distance; i++ )
                        {
                            if (!CheckDoorOpen(Functions.PointMove(User.CurrentLocation, direction, i)))
                                fail = true;
                        }
                        if (!fail)
                        {
                            User.QueuedAction = new QueuedAction { Action = MirAction.跑步动作, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, distance) };
                            return;
                        }
                    }
                    if ((CanWalk(direction, out direction)) && (CheckDoorOpen(Functions.PointMove(User.CurrentLocation, direction, 1))))
                    {
                        User.QueuedAction = new QueuedAction { Action = MirAction.行走动作, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, 1) };
                        return;
                    }
                    if (direction != User.Direction)
                    {
                        User.QueuedAction = new QueuedAction { Action = MirAction.站立动作, Direction = direction, Location = User.CurrentLocation };
                        return;
                    }
                    return;
                }

                switch (MapButtons)
                {
                    case MouseButtons.Left:
                        if (MapObject.MouseObject is NPCObject || (MapObject.MouseObject is PlayerObject && MapObject.MouseObject != User)) break;
                        if (MapObject.MouseObject is MonsterObject && MapObject.MouseObject.AI == 56) break;
 
                        if (CMain.Alt && !User.RidingMount)
                        {
                            User.QueuedAction = new QueuedAction { Action = MirAction.挖矿展示, Direction = direction, Location = User.CurrentLocation };
                            return;
                        }

                        if (CMain.Shift)
                        {
                            if (CMain.Time > GameScene.AttackTime && CanRideAttack()) //ArcherTest - shift click
                            {
                                MapObject target = null;
                                if (MapObject.MouseObject is MonsterObject || MapObject.MouseObject is PlayerObject) target = MapObject.MouseObject;

                                if (User.Class == MirClass.弓箭 && User.HasClassWeapon && !User.RidingMount && !User.Poison.HasFlag(PoisonType.Dazed))
                                {
                                    if (target != null)
                                    {
                                        if (!Functions.InRange(MapObject.MouseObject.CurrentLocation, User.CurrentLocation, Globals.MaxAttackRange))
                                        {
                                            if (CMain.Time >= OutputDelay)
                                            {
                                                OutputDelay = CMain.Time + 1000;
                                                GameScene.Scene.OutputMessage("目标距离太远");
                                            }
                                            return;
                                        }
                                    }

                                    User.QueuedAction = new QueuedAction { Action = MirAction.远程攻击1, Direction = MouseDirection(), Location = User.CurrentLocation, Params = new List<object>() };
                                    User.QueuedAction.Params.Add(target != null ? target.ObjectID : (uint)0);
                                    User.QueuedAction.Params.Add(Functions.PointMove(User.CurrentLocation, MouseDirection(), 9));
                                    return;
                                }
                                
                                //stops double slash from being used without empty hand or assassin weapon (otherwise bugs on second swing)
                                if (GameScene.User.DoubleSlash && (!User.HasClassWeapon && User.Weapon > -1)) return;
                                if (User.Poison.HasFlag(PoisonType.Dazed)) return;

                                User.QueuedAction = new QueuedAction { Action = MirAction.近距攻击1, Direction = direction, Location = User.CurrentLocation };
                            }
                            return;
                        }

                        if (MapObject.MouseObject is MonsterObject && User.Class == MirClass.弓箭 && MapObject.TargetObject != null && !MapObject.TargetObject.Dead && User.HasClassWeapon && !User.RidingMount) //ArcherTest - range attack
                        {
                            if (Functions.InRange(MapObject.MouseObject.CurrentLocation, User.CurrentLocation, Globals.MaxAttackRange))
                            {
                                if (CMain.Time > GameScene.AttackTime)
                                {
                                    User.QueuedAction = new QueuedAction { Action = MirAction.远程攻击1, Direction = direction, Location = User.CurrentLocation, Params = new List<object>() };
                                    User.QueuedAction.Params.Add(MapObject.TargetObject.ObjectID);
                                    User.QueuedAction.Params.Add(MapObject.TargetObject.CurrentLocation);
                                }
                            }
                            else
                            {
                                if (CMain.Time >= OutputDelay)
                                {
                                    OutputDelay = CMain.Time + 1000;
                                    GameScene.Scene.OutputMessage("目标距离太远");
                                }
                            }
                            return;
                        }

                        if (MapLocation == User.CurrentLocation)
                        {
                            if (CMain.Time > GameScene.PickUpTime)
                            {
                                GameScene.PickUpTime = CMain.Time + 200;
                                Network.Enqueue(new C.PickUp());
                            }
                            return;
                        }

                        //mine
                        if (!ValidPoint(Functions.PointMove(User.CurrentLocation, direction, 1)))
                        {
                            if ((MapObject.User.Equipment[(int)EquipmentSlot.武器] != null) && (MapObject.User.Equipment[(int)EquipmentSlot.武器].Info.CanMine))
                            {
                                if (direction != User.Direction)
                                {
                                    User.QueuedAction = new QueuedAction { Action = MirAction.站立动作, Direction = direction, Location = User.CurrentLocation };
                                    return;
                                }
                                AutoHit = true;
                                return;
                            }
                        }
                        if ((CanWalk(direction, out direction)) && (CheckDoorOpen(Functions.PointMove(User.CurrentLocation, direction, 1))))
                        {

                            User.QueuedAction = new QueuedAction { Action = MirAction.行走动作, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, 1) };
                            return;
                        }
                        if (direction != User.Direction)
                        {
                            User.QueuedAction = new QueuedAction { Action = MirAction.站立动作, Direction = direction, Location = User.CurrentLocation };
                            return;
                        }

                        if (CanFish(direction))
                        {
                            User.FishingTime = CMain.Time;
                            Network.Enqueue(new C.FishingCast { CastOut = true });
                            return;
                        }

                        break;
                    case MouseButtons.Right:
                        if (MapObject.MouseObject is PlayerObject && MapObject.MouseObject != User && CMain.Ctrl) break;
                        if (Settings.NewMove) break;

                        if (Functions.InRange(MapLocation, User.CurrentLocation, 2))
                        {
                            if (direction != User.Direction)
                            {
                                User.QueuedAction = new QueuedAction { Action = MirAction.站立动作, Direction = direction, Location = User.CurrentLocation };
                            }
                            return;
                        }

                        GameScene.CanRun = User.FastRun ? true : GameScene.CanRun;

                        if ((GameScene.CanRun || Settings.NoRunUp) && CanRun(direction) && CMain.Time > GameScene.NextRunTime && User.HP >= 10 && (!User.Sneaking || (User.Sneaking && User.Sprint))) //slow removed
                        {
                            int distance = User.RidingMount || User.Sprint && !User.Sneaking ? 3 : 2;
                            bool fail = false;
                            for (int i = 0; i <= distance; i++ )
                            {
                                if (!CheckDoorOpen(Functions.PointMove(User.CurrentLocation, direction, i)))
                                    fail = true;
                            }
                            if (!fail)
                            {
                                User.QueuedAction = new QueuedAction { Action = MirAction.跑步动作, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, User.RidingMount || (User.Sprint && !User.Sneaking) ? 3 : 2) };
                                return;
                            }
                        }
                        if ((CanWalk(direction, out direction)) && (CheckDoorOpen(Functions.PointMove(User.CurrentLocation, direction, 1))))
                        {
                            User.QueuedAction = new QueuedAction { Action = MirAction.行走动作, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, 1) };
                            return;
                        }
                        if (direction != User.Direction)
                        {
                            User.QueuedAction = new QueuedAction { Action = MirAction.站立动作, Direction = direction, Location = User.CurrentLocation };
                            return;
                        }
                        break;
                }
            }

            if (AutoPath)
            {
                if (CurrentPath == null || CurrentPath.Count == 0)
                {
                    AutoPath = false;
                    return;
                }

                var path = GameScene.Scene.MapControl.PathFinder.FindPath(MapObject.User.CurrentLocation, CurrentPath.Last().Location);

                if (path != null && path.Count > 0)
                    GameScene.Scene.MapControl.CurrentPath = path;
                else
                {
                    AutoPath = false;
                    return;
                }

                Node currentNode = CurrentPath.SingleOrDefault(x => User.CurrentLocation == x.Location);
                if (currentNode != null)
                {
                    while (true)
                    {
                        Node first = CurrentPath.First();
                        CurrentPath.Remove(first);

                        if (first == currentNode)
                            break;
                    }
                }

                if (CurrentPath.Count > 0)
                {
                    MirDirection dir = Functions.DirectionFromPoint(User.CurrentLocation, CurrentPath.First().Location);

                    if ((GameScene.CanRun || Settings.NoRunUp) && CanRun(dir) && CMain.Time > GameScene.NextRunTime && User.HP >= 10 && CurrentPath.Count > (User.RidingMount ? 2 : 1))
                    {
                        User.QueuedAction = new QueuedAction { Action = MirAction.跑步动作, Direction = dir, Location = Functions.PointMove(User.CurrentLocation, dir, User.RidingMount ? 3 : 2) };
                        return;
                    }
                    if (CanWalk(dir))
                    {
                        User.QueuedAction = new QueuedAction { Action = MirAction.行走动作, Direction = dir, Location = Functions.PointMove(User.CurrentLocation, dir, 1) };

                        return;
                    }
                }
            }

            if (MapObject.TargetObject == null || MapObject.TargetObject.Dead) return;
            if (((!MapObject.TargetObject.Name.EndsWith(")") && !(MapObject.TargetObject is PlayerObject)) || !CMain.Shift) &&
                (MapObject.TargetObject.Name.EndsWith(")") || !(MapObject.TargetObject is MonsterObject))) return;
            if (Functions.InRange(MapObject.TargetObject.CurrentLocation, User.CurrentLocation, 1)) return;
            if ((User.Class == MirClass.弓箭 && User.HasClassWeapon ||
                 (Settings.AutoPlay && User.Class == MirClass.法师 && !AutoPlayIsPhysicalTarget(MapObject.TargetObject))) &&
                (MapObject.TargetObject is MonsterObject || MapObject.TargetObject is PlayerObject)) return; //ArcherTest - stop walking（弓手/法师走位由 ProcessAutoPlay 的 AutoPlayCombatMove 处理，法师不贴身；切物理攻击后可追）
            direction = Functions.DirectionFromPoint(User.CurrentLocation, MapObject.TargetObject.CurrentLocation);

            // 内挂自动走位的追击跑动：路况良好时直接跑 2/3 格；
            // 战士要贴身砍，仍按 ≥3 格才跑（近身逐步走），其他职业（法师/道士/刺客/弓手）≥2 格就跑——
            // 跑 2 格正好贴脸停下，刺客随后正常近攻，不影响近攻技能的施展。
            int runGap = User.Class == MirClass.战士 ? 3 : 2;

            if (Settings.AutoPlay && Settings.AutoMove && Settings.AutoMoveRun &&
                (GameScene.CanRun || Settings.NoRunUp) && CMain.Time > GameScene.NextRunTime &&
                User.HP >= 10 && (!User.Sneaking || (User.Sneaking && User.Sprint)) &&
                Functions.MaxDistance(MapObject.TargetObject.CurrentLocation, User.CurrentLocation) >= runGap)
            {
                int distance = User.RidingMount || (User.Sprint && !User.Sneaking) ? 3 : 2;

                bool fail = false;
                for (int i = 1; i <= distance; i++)
                {
                    if (!CheckDoorOpen(Functions.PointMove(User.CurrentLocation, direction, i))) fail = true;
                }

                if (!fail && CanRun(direction))
                {
                    User.QueuedAction = new QueuedAction
                    {
                        Action = MirAction.跑步动作,
                        Direction = direction,
                        Location = Functions.PointMove(User.CurrentLocation, direction, distance)
                    };

                    return;
                }
            }

            if (!CanWalk(direction, out direction)) return;

            User.QueuedAction = new QueuedAction { Action = MirAction.行走动作, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, 1) };
        }

        public void UseMagic(ClientMagic magic, UserObject actor)
        {
            if (User.Dead) return;
            if (CMain.Time < GameScene.SpellTime || actor.Poison.HasFlag(PoisonType.Stun))
            {
                actor.ClearMagic();
                return;
            }

            if ((CMain.Time <= magic.CastTime + magic.Delay))
            {
                if (CMain.Time >= OutputDelay)
                {
                    OutputDelay = CMain.Time + 1000;
                    GameScene.Scene.OutputMessage(string.Format("技能冷却时间 {1} 秒", magic.Spell.ToString(), ((magic.CastTime + magic.Delay) - CMain.Time - 1) / 1000 + 1));
                }

                actor.ClearMagic();
                return;
            }

            int cost = magic.Level * magic.LevelCost + magic.BaseCost;

            if (magic.Spell == Spell.Teleport || magic.Spell == Spell.Blink || magic.Spell == Spell.StormEscape || magic.Spell == Spell.StormEscapeRare)
            {
                if (actor.Stats[Stat.传送技法力消耗数率] > 0)
                {
                    cost += (cost * actor.Stats[Stat.传送技法力消耗数率]) / 100;
                }
            }

            if (actor.Stats[Stat.法力值消耗数率] > 0)
            {
                cost += (cost * actor.Stats[Stat.法力值消耗数率]) / 100;
            }

            if (cost > actor.MP)
            {
                if (CMain.Time >= OutputDelay)
                {
                    OutputDelay = CMain.Time + 1000;
                    GameScene.Scene.OutputMessage(GameLanguage.LowMana);
                }
                actor.ClearMagic();
                return;
            }

            //bool isTargetSpell = true;

            MapObject target = null;

            //Targeting
            switch (magic.Spell)
            {
                case Spell.FireBall:
                case Spell.GreatFireBall:
                case Spell.GreatFireBallRare:
                case Spell.ElectricShock:
                case Spell.Poisoning:
                case Spell.ThunderBolt:
                case Spell.FlameDisruptor:
                case Spell.SoulFireBall:
                case Spell.TurnUndead:
                case Spell.FrostCrunch:
                case Spell.Vampirism:
                case Spell.Revelation:
                case Spell.Entrapment:
                case Spell.EntrapmentRare:
                case Spell.Hallucination:
                case Spell.DarkBody:
                case Spell.FireBounce:
                case Spell.MeteorShower:
                case Spell.DimensionalSword:
                case Spell.DimensionalSwordRare:
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && actor.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }

                    if (target == null) target = MapObject.MagicObject;

                    if (target != null && target.Race == ObjectType.Monster) MapObject.MagicObjectID = target.ObjectID;
                    break;
                case Spell.StraightShot:
                case Spell.DoubleShot:
                case Spell.ElementalShot:
                case Spell.DelayedExplosion:
                case Spell.BindingShot:
                case Spell.VampireShot:
                case Spell.PoisonShot:
                case Spell.CrippleShot:
                case Spell.NapalmShot:
                case Spell.SummonVampire:
                case Spell.SummonToad:
                case Spell.SummonSnakes:
                    if (!actor.HasClassWeapon)
                    {
                        GameScene.Scene.OutputMessage("必须戴着弓箭才能施展此项技能");
                        actor.ClearMagic();
                        return;
                    }
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && actor.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }

                    if (target == null) target = MapObject.MagicObject;

                    if (target != null && target.Race == ObjectType.Monster) MapObject.MagicObjectID = target.ObjectID;
                    break;
                case Spell.Stonetrap:
                    if (!User.HasClassWeapon)
                    {
                        GameScene.Scene.OutputMessage("必须佩戴弓类武器才能施展此技能");
                        User.ClearMagic();
                        return;
                    }
                    if (User.NextMagicObject != null)
                    {
                        if (!User.NextMagicObject.Dead && User.NextMagicObject.Race != ObjectType.Item && User.NextMagicObject.Race != ObjectType.Merchant)
                            target = User.NextMagicObject;
                    }

                    //if(magic.Spell == Spell.ElementalShot)
                    //{
                    //    isTargetSpell = User.HasElements;
                    //}

                    //switch(magic.Spell)
                    //{
                    //    case Spell.SummonVampire:
                    //    case Spell.SummonToad:
                    //    case Spell.SummonSnakes:
                    //        isTargetSpell = false;
                    //        break;
                    //}

                    break;
                case Spell.Purification:
                case Spell.Healing:
                case Spell.HealingRare:
                case Spell.UltimateEnhancer:
                case Spell.EnergyShield:
                case Spell.PetEnhancer:
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && actor.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }

                    if (target == null) target = User;
                    break;
                case Spell.FireBang:
                case Spell.MassHiding:
                case Spell.FireWall:
                case Spell.TrapHexagon:
                case Spell.HealingCircle:
                case Spell.CatTongue:
				case Spell.HealingcircleRare:
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && actor.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }
                    break;
                case Spell.PoisonCloud:
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && actor.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }
                    break;
                case Spell.Blizzard:
                case Spell.MeteorStrike:
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && actor.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }
                    break;
                case Spell.Reincarnation:
                    if (actor == Hero && actor.NextMagicObject == null)
                        actor.NextMagicObject = User;
                    if (actor.NextMagicObject != null)
                    {
                        target = actor.NextMagicObject;

                        if (target == null && target.Dead)
                        {
                            for (int i = Objects.Count - 1; i >= 0; i--)
                            {
                                if (Objects[i] is PlayerObject && Objects[i].Dead)
                                {
                                    target = (PlayerObject)Objects[i];
                                    target.ObjectID = actor.NextMagicObject.ObjectID;
                                    target = null;
                                    break;
                                }
                            }
                        }
                    }
                    break;
                case Spell.Trap:
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && User.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }
                    break;
                case Spell.FlashDash:
                    if (actor.GetMagic(Spell.FlashDash).Level <= 1 && actor.IsDashAttack() == false)
                    {
                        actor.ClearMagic();
                        return;
                    }
                    //isTargetSpell = false;
                    break;
                default:
                    //isTargetSpell = false;
                        break;
            }

            MirDirection dir = (target == null || target == User) ? actor.NextMagicDirection : Functions.DirectionFromPoint(actor.CurrentLocation, target.CurrentLocation);

            Point location = target != null ? target.CurrentLocation : actor.NextMagicLocation;

            uint targetID = target != null ? target.ObjectID : 0;

            if (magic.Spell == Spell.FlashDash)
                dir = actor.Direction;

            if ((magic.Range != 0) && (!Functions.InRange(actor.CurrentLocation, location, magic.Range)))
            {
                if (CMain.Time >= OutputDelay)
                {
                    OutputDelay = CMain.Time + 1000;
                    GameScene.Scene.OutputMessage("目标距离太远");
                }
                actor.ClearMagic();
                return;
            }

            GameScene.LogTime = CMain.Time + Globals.LogDelay;

            if (actor == User)
            {
                User.QueuedAction = new QueuedAction { Action = MirAction.施法动作, Direction = dir, Location = User.CurrentLocation, Params = new List<object>() };
                User.QueuedAction.Params.Add(magic.Spell);
                User.QueuedAction.Params.Add(targetID);
                User.QueuedAction.Params.Add(location);
                User.QueuedAction.Params.Add(magic.Level);
            }
            else
            {
                Network.Enqueue(new C.Magic { ObjectID = actor.ObjectID, Spell = magic.Spell, Direction = dir, TargetID = targetID, Location = location, SpellTargetLock = CMain.SpellTargetLock });
            }
        }

        public static MirDirection MouseDirection(float ratio = 45F) //22.5 = 16
        {
            Point p = new Point(MouseLocation.X / CellWidth, MouseLocation.Y / CellHeight);
            if (Functions.InRange(new Point(OffSetX, OffSetY), p, 2))
                return Functions.DirectionFromPoint(new Point(OffSetX, OffSetY), p);

            PointF c = new PointF(OffSetX * CellWidth + CellWidth / 2F, OffSetY * CellHeight + CellHeight / 2F);
            PointF a = new PointF(c.X, 0);
            PointF b = MouseLocation;
            float bc = (float)Distance(c, b);
            float ac = bc;
            b.Y -= c.Y;
            c.Y += bc;
            b.Y += bc;
            float ab = (float)Distance(b, a);
            double x = (ac * ac + bc * bc - ab * ab) / (2 * ac * bc);
            double angle = Math.Acos(x);

            angle *= 180 / Math.PI;

            if (MouseLocation.X < c.X) angle = 360 - angle;
            angle += ratio / 2;
            if (angle > 360) angle -= 360;

            return (MirDirection)(angle / ratio);
        }

        public static int Direction16(Point source, Point destination)
        {
            PointF c = new PointF(source.X, source.Y);
            PointF a = new PointF(c.X, 0);
            PointF b = new PointF(destination.X, destination.Y);
            float bc = (float)Distance(c, b);
            float ac = bc;
            b.Y -= c.Y;
            c.Y += bc;
            b.Y += bc;
            float ab = (float)Distance(b, a);
            double x = (ac * ac + bc * bc - ab * ab) / (2 * ac * bc);
            double angle = Math.Acos(x);

            angle *= 180 / Math.PI;

            if (destination.X < c.X) angle = 360 - angle;
            angle += 11.25F;
            if (angle > 360) angle -= 360;

            return (int)(angle / 22.5F);
        }

        public static double Distance(PointF p1, PointF p2)
        {
            double x = p2.X - p1.X;
            double y = p2.Y - p1.Y;
            return Math.Sqrt(x * x + y * y);
        }

        public bool EmptyCell(Point p)
        {
            if ((M2CellInfo[p.X, p.Y].BackImage & 0x20000000) != 0 || (M2CellInfo[p.X, p.Y].FrontImage & 0x8000) != 0) // + (M2CellInfo[P.X, P.Y].FrontImage & 0x7FFF) != 0)
                return false;

            for (int i = 0; i < Objects.Count; i++)
            {
                MapObject ob = Objects[i];

                if (ob.CurrentLocation == p && ob.Blocking)
                {
                    // 穿人（含穿怪、穿 NPC）：开启时客户端跳过玩家/英雄/怪物/NPC 的阻挡，
                    // 否则客户端不会发起移动动作，服务端放行也无效
                    if (Settings.WalkThrough && ob != User && (ob is PlayerObject || ob is MonsterObject || ob is NPCObject))
                        continue;
                    return false;
                }
            }

            return true;
        }

        private bool CanWalk(MirDirection dir)
        {
            return EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 1)) && !User.InTrapRock;
        }

        private bool CanWalk(MirDirection dir, out MirDirection outDir)
        {
            outDir = dir;
            if (User.InTrapRock) return false;            
            
            if (EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 1)))
                return true;

            dir = Functions.NextDir(outDir);
            if (EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 1)))
            {
                outDir = dir;
                return true;
            }

            dir = Functions.PreviousDir(outDir);
            if (EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 1)))
            {
                outDir = dir;
                return true;
            }

            return false;
        }

        private bool CheckDoorOpen(Point p)
        {
            if (M2CellInfo[p.X, p.Y].DoorIndex == 0) return true;
            Door DoorInfo = GetDoor(M2CellInfo[p.X, p.Y].DoorIndex);
            if (DoorInfo == null) return false;//if the door doesnt exist then it isnt even being shown on screen (and cant be open lol)
            //if ((DoorInfo.DoorState == DoorState.Closed) || (DoorInfo.DoorState == DoorState.Closing))
			if ((DoorInfo.DoorState == 0) || (DoorInfo.DoorState == DoorState.Closing))
            {
                if (CMain.Time > _doorTime)
                {
                    _doorTime = CMain.Time + 4000;
                    Network.Enqueue(new C.Opendoor() { DoorIndex = DoorInfo.index });
                }

                return false;
            }
            if ((DoorInfo.DoorState == DoorState.Open) && (DoorInfo.LastTick + 4000 > CMain.Time))
            {
                if (CMain.Time > _doorTime)
                {
                    _doorTime = CMain.Time + 4000;
                    Network.Enqueue(new C.Opendoor() { DoorIndex = DoorInfo.index });
                }
            }
            return true;
        }

        private long _doorTime = 0;


        private bool CanRun(MirDirection dir)
        {
            if (User.InTrapRock) return false;

            // 超负重：负重超限时不再禁止奔跑（与服务器 HumanObject.CanRun 保持一致）
            if (!Settings.OverWeight)
            {
                if (User.CurrentBagWeight > User.Stats[Stat.背包负重]) return false;
                if (User.CurrentWearWeight > User.Stats[Stat.背包负重]) return false;
            }

            if (CanWalk(dir) && EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 2)))
            {
                if (User.RidingMount || User.Sprint && !User.Sneaking)
                {
                    return EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 3));
                }

                return true;
            }

            return false;
        }

        private bool CanRideAttack()
        {
            if (GameScene.User.RidingMount)
            {
                UserItem item = GameScene.User.Equipment[(int)EquipmentSlot.坐骑];
                if (item == null || item.Slots.Length < 4 || item.Slots[(int)MountSlot.Bells] == null) return false;
            }

            return true;
        }

        public bool CanFish(MirDirection dir)
        {
            if (!GameScene.User.HasFishingRod || GameScene.User.FishingTime + 1000 > CMain.Time) return false;
            if (GameScene.User.CurrentAction != MirAction.站立动作) return false;
            if (GameScene.User.Direction != dir) return false;

            switch (GameScene.User.TransformType)
            {
                case 6:
                case 7:
                case 8:
                case 9:
                case 33:
                case 34:
                case 35:
                case 36:
                case 37:
                case 38:
                    return false;
            }

            Point point = Functions.PointMove(User.CurrentLocation, dir, 3);

            if (!M2CellInfo[point.X, point.Y].FishingCell) return false;

            return true;
        }

        public bool CanFly(Point target)
        {
            Point location = User.CurrentLocation;
            while (location != target)
            {
                MirDirection dir = Functions.DirectionFromPoint(location, target);

                location = Functions.PointMove(location, dir, 1);

                if (location.X < 0 || location.Y < 0 || location.X >= GameScene.Scene.MapControl.Width || location.Y >= GameScene.Scene.MapControl.Height) return false;

                if (!GameScene.Scene.MapControl.ValidPoint(location)) return false;
            }

            return true;
        }


        public bool ValidPoint(Point p)
        {
            //GameScene.Scene.ChatDialog.ReceiveChat(string.Format("cell: {0}", (M2CellInfo[p.X, p.Y].BackImage & 0x20000000)), ChatType.Hint);
            return (M2CellInfo[p.X, p.Y].BackImage & 0x20000000) == 0;
        }
        public bool HasTarget(Point p)
        {
            for (int i = 0; i < Objects.Count; i++)
            {
                MapObject ob = Objects[i];

                if (ob.CurrentLocation == p && ob.Blocking)
                    return true;
            }
            return false;
        }
        public bool CanHalfMoon(Point p, MirDirection d)
        {
            d = Functions.PreviousDir(d);
            for (int i = 0; i < 4; i++)
            {
                if (HasTarget(Functions.PointMove(p, d, 1))) return true;
                d = Functions.NextDir(d);
            }
            return false;
        }
        public bool CanCrossHalfMoon(Point p)
        {
            MirDirection dir = MirDirection.Up;
            for (int i = 0; i < 8; i++)
            {
                if (HasTarget(Functions.PointMove(p, dir, 1))) return true;
                dir = Functions.NextDir(dir);
            }
            return false;
        }

        #region Disposable

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Objects.Clear();

                MapButtons = 0;
                MouseLocation = Point.Empty;
                InputDelay = 0;
                NextAction = 0;

                M2CellInfo = null;
                Width = 0;
                Height = 0;

                Index = 0;
                FileName = String.Empty;
                Title = String.Empty;
                MiniMap = 0;
                BigMap = 0;
                Lights = 0;
                FloorValid = false;
                LightsValid = false;
                MapDarkLight = 0;
                Music = 0;

                AnimationCount = 0;
                Effects.Clear();
            }

            base.Dispose(disposing);
        }

        #endregion

        public void UpdateWeather()
        {
            for (int i = GameScene.Scene.ParticleEngines.Count - 1; i > 0; i--)
                GameScene.Scene.ParticleEngines[i].Dispose();

            GameScene.Scene.ParticleEngines.Clear();
            List<ParticleImageInfo> textures = new List<ParticleImageInfo>();
            foreach (WeatherSetting itemWeather in Enum.GetValues(typeof(WeatherSetting)).Cast<object>().ToArray())
            {
                if ((Weather & itemWeather) != itemWeather)
                    continue;

                switch (itemWeather)
                {
                    case WeatherSetting.黄色花瓣:
                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 359, 170, 50));
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 531, 55, 50));
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 587, 200, 50));

                        ParticleEngine LeavesEngine2 = new ParticleEngine(textures, new Vector2(2f, 0), ParticleType.Leaves);
                        Vector2 lVelocity = new Vector2(0F, 0F);
                        for (int y = 512 * -1; y < Settings.ScreenHeight + 512; y += 512)
                            for (int x = 512 * -1; x < Settings.ScreenWidth + 512; x += 512)
                            {
                                Particle part = LeavesEngine2.GenerateNewParticle(ParticleType.Leaves);
                                part.Position = new Vector2(x, y);
                                part.Velocity = lVelocity;
                            }
                        LeavesEngine2.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(LeavesEngine2);
                        break;

                    case WeatherSetting.红色花瓣:
                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 359, 170, 50));
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 531, 55, 50));
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 587, 200, 50));

                        ParticleEngine FLeavesEngine2 = new ParticleEngine(textures, new Vector2(2f, 0), ParticleType.FireyLeaves);
                        Vector2 FlVelocity = new Vector2(0F, 0F);
                        for (int y = 512 * -1; y < Settings.ScreenHeight + 512; y += 512)
                            for (int x = 512 * -1; x < Settings.ScreenWidth + 512; x += 512)
                            {
                                Particle part = FLeavesEngine2.GenerateNewParticle(ParticleType.FireyLeaves);
                                part.Position = new Vector2(x, y);
                                part.Velocity = FlVelocity;
                            }
                        FLeavesEngine2.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(FLeavesEngine2);
                        break;

                    case WeatherSetting.雨天:
                        textures = new List<ParticleImageInfo>();
                        //Rain
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 164, 150, 50));

                        ParticleEngine RainEngine2 = new ParticleEngine(textures, new Vector2(2f, 0), ParticleType.Rain);
                        Vector2 rsevelocity = new Vector2(0F, 0F);
                        var xVar = 512;
                        var yVar = 512;
                        for (int y = yVar * -1; y < Settings.ScreenHeight + yVar; y += yVar)
                            for (int x = xVar * -1; x < Settings.ScreenWidth + xVar; x += xVar)
                            {
                                Particle part = RainEngine2.GenerateNewParticle(ParticleType.Rain);
                                part.Position = new Vector2(x, y);
                                part.Velocity = rsevelocity;
                            }
                        RainEngine2.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(RainEngine2);
                        break;

                    case WeatherSetting.落雪:
                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 790, 40, 50));

                        ParticleEngine snowfall = new ParticleEngine(textures, new Vector2(2f, 0), ParticleType.Snow);
                        Vector2 snowfallVelocity = new Vector2(0F, 0F);
                        var xSnowfallVar = 800;
                        var ySnowfallVar = 600;
                        for (int y = ySnowfallVar * -1; y < Settings.ScreenHeight + ySnowfallVar; y += ySnowfallVar)
                            for (int x = xSnowfallVar * -1; x < Settings.ScreenWidth + xSnowfallVar; x += xSnowfallVar)
                            {
                                Particle part = snowfall.GenerateNewParticle(ParticleType.Snow);
                                part.Position = new Vector2(x, y);
                                part.Velocity = snowfallVelocity;
                            }
                        snowfall.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(snowfall);
                        break;

                    case WeatherSetting.飘雪:
                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 43, 20, 50));

                        ParticleEngine snowFlurry = new ParticleEngine(textures, new Vector2(0, 0), ParticleType.Snow);
                        Vector2 snowFlurryVelocity = new Vector2(1F, -1F);

                        for (int y = -400; y < Settings.ScreenHeight + 400; y += 400)
                            for (int x = -400; x < Settings.ScreenWidth + 400; x += 400)
                            {
                                Particle part = snowFlurry.GenerateNewParticle(ParticleType.Snow);
                                part.Position = new Vector2(x, y);
                                part.Velocity = snowFlurryVelocity;
                            }
                        snowFlurry.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(snowFlurry);
                        break;

                    case WeatherSetting.雾天:
                        List<ParticleImageInfo> ftextures = new List<ParticleImageInfo>();
                        ftextures.Add(new ParticleImageInfo(Libraries.Weather, 0));
                        ParticleEngine fengine = new ParticleEngine(ftextures, new Vector2(0, 0), ParticleType.Fog);
                        fengine.UpdateDelay = TimeSpan.FromMilliseconds(20);

                        Vector2 fvelocity = new Vector2(2F, -2F);
                        for (int y = -512; y < Settings.ScreenHeight + 512; y += 512)
                            for (int x = -512; x < Settings.ScreenWidth + 512; x += 512)
                            {
                                Particle part = fengine.GenerateNewParticle(ParticleType.Fog);
                                part.Position = new Vector2(x, y);
                                part.Velocity = fvelocity;
                            }

                        fengine.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(fengine);
                        break;

                    case WeatherSetting.红色余烬:
                        var rtextures = new List<ParticleImageInfo>();
                        rtextures.Add(new ParticleImageInfo(Libraries.Weather, 1, 9, 150));

                        var rengine = new ParticleEngine(rtextures, new Vector2(0, 0), ParticleType.RedFogEmber);
                        GameScene.Scene.ParticleEngines.Add(rengine);
                        break;

                    case WeatherSetting.白色余烬:
                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 1, 9, 150));
                        var whiteEmberEngine = new ParticleEngine(textures, new Vector2(0, 0), ParticleType.WhiteEmber);
                        GameScene.Scene.ParticleEngines.Add(whiteEmberEngine);
                        break;

                    case WeatherSetting.粉色花瓣:
                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 359, 172, 50));
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 531, 117, 100));
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 648, 140, 80));
                        //textures.Add(new ParticleImageInfo(Libraries.Weather, 10, 20, 50));

                        var pEmberEngine = new ParticleEngine(textures, new Vector2(0, 0), ParticleType.PurpleLeaves);

                        for (int y = 512 * -1; y < Settings.ScreenHeight + 512; y += 512)
                            for (int x = 512 * -1; x < Settings.ScreenWidth + 512; x += 512)
                            {
                                Particle part = pEmberEngine.GenerateNewParticle(ParticleType.PurpleLeaves);
                                part.Position = new Vector2(x, y);
                                part.Velocity = new Vector2(0, 0);
                            }
                        pEmberEngine.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(pEmberEngine);
                        break;

                    case WeatherSetting.黄色余烬:
                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 1, 9, 100));

                        var yellowEmberEngine = new ParticleEngine(textures, new Vector2(0, 0), ParticleType.YellowEmber);
                        GameScene.Scene.ParticleEngines.Add(yellowEmberEngine);
                        break;

                    case WeatherSetting.沙尘:
                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 830, 20, 100));

                        ParticleEngine sandstorm = new ParticleEngine(textures, new Vector2(0, 0), ParticleType.Sand);

                        for (int y = -400; y < Settings.ScreenHeight + 400; y += 400)
                            for (int x = -400; x < Settings.ScreenWidth + 400; x += 400)
                            {
                                Particle part = sandstorm.GenerateNewParticle(ParticleType.Sand);
                                part.Position = new Vector2(x, y);
                            }

                        sandstorm.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(sandstorm);
                        break;

                    case WeatherSetting.沙雾:
                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 850, 20, 200));

                        ParticleEngine sandfogstorm = new ParticleEngine(textures, new Vector2(0, 0), ParticleType.Fog);

                        for (int y = -400; y < Settings.ScreenHeight + 400; y += 400)
                            for (int x = -400; x < Settings.ScreenWidth + 400; x += 400)
                            {
                                Particle part = sandfogstorm.GenerateNewParticle(ParticleType.Fog);
                                part.Position = new Vector2(x, y);
                            }

                        sandfogstorm.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(sandfogstorm);
                        break;
                }
            }
        }

        public void RemoveObject(MapObject ob)
        {
            M2CellInfo[ob.MapLocation.X, ob.MapLocation.Y].RemoveObject(ob);
        }
        public void AddObject(MapObject ob)
        {
            M2CellInfo[ob.MapLocation.X, ob.MapLocation.Y].AddObject(ob);
        }
        public MapObject FindObject(uint ObjectID, int x, int y)
        {
            return M2CellInfo[x, y].FindObject(ObjectID);
        }
        public void SortObject(MapObject ob)
        {
            M2CellInfo[ob.MapLocation.X, ob.MapLocation.Y].Sort();
        }

        public Door GetDoor(byte Index)
        {
            for (int i = 0; i < Doors.Count; i++)
            {
                if (Doors[i].index == Index)
                    return Doors[i];
            }
            return null;
        }
        public void Processdoors()
        {
            for (int i = 0; i < Doors.Count; i++)
            {
                if ((Doors[i].DoorState == DoorState.Opening) || (Doors[i].DoorState == DoorState.Closing))
                {
                    if (Doors[i].LastTick + 50 < CMain.Time)
                    {
                        Doors[i].LastTick = CMain.Time;
                        Doors[i].ImageIndex++;

                        if (Doors[i].ImageIndex == 1)//change the 1 if you want to animate doors opening/closing
                        {
                            Doors[i].ImageIndex = 0;
                            Doors[i].DoorState = (DoorState)Enum.ToObject(typeof(DoorState), ((byte)++Doors[i].DoorState % 4));
                        }

                        FloorValid = false;
                    }
                }
                if (Doors[i].DoorState == DoorState.Open)
                {
                    if (Doors[i].LastTick + 5000 < CMain.Time)
                    {
                        Doors[i].LastTick = CMain.Time;
                        Doors[i].DoorState = DoorState.Closing;
                        FloorValid = false;
                    }
                }
            }
        }
        public void OpenDoor(byte Index, bool closed)
        {
            Door Info = GetDoor(Index);
            if (Info == null) return;
            Info.DoorState = (closed ? DoorState.Closing : Info.DoorState == DoorState.Open ? DoorState.Open : DoorState.Opening);
            Info.ImageIndex = 0;
            Info.LastTick = CMain.Time;
        }
    }
}

