using Client.MirControls;
using Client.MirGraphics;
using Client.MirScenes;
using Client.MirSounds;
using Client.MirScenes.Dialogs;
using SlimDX;

namespace Client.MirObjects
{
    public abstract class MapObject
    {
        public static Font ChatFont = new Font(Settings.FontName, 10F);
        public static List<MirLabel> LabelList = new List<MirLabel>();

        public static UserObject User;
        public static UserHeroObject Hero;
        public static HeroObject HeroObject;
        public static MapObject MouseObject, TargetObject, MagicObject;

        //魔法锁定（内挂面板勾选）：被锁定的对象。锁定期间 TargetObject/MagicObject 每帧强制指回它，
        //鼠标移开不丢目标，指向新的玩家/怪物/NPC 自动换锁；对象从视野消失（Remove）即解除锁定。
        public static MapObject LockObject;

        private static uint lockObjectID;
        public static uint LockObjectID
        {
            get { return lockObjectID; }
            set
            {
                if (lockObjectID == value) return;
                lockObjectID = value;
                LockObject = value == 0 ? null : MapControl.Objects.Find(x => x.ObjectID == value);
            }
        }

        private static uint mouseObjectID;
        public static uint MouseObjectID
        {
            get { return mouseObjectID; }
            set
            {
                if (mouseObjectID == value) return;
                mouseObjectID = value;
                MouseObject = MapControl.Objects.Find(x => x.ObjectID == value);
            }
        }

        private static uint lastTargetObjectId;
        private static uint targetObjectID;
        public static uint TargetObjectID
        {
            get { return targetObjectID; }
            set
            {
                if (targetObjectID == value) return;
                lastTargetObjectId = value;
                targetObjectID = value;
                TargetObject = value == 0 ? null : MapControl.Objects.Find(x => x.ObjectID == value);
            }
        }

        private static uint magicObjectID;
        public static uint MagicObjectID
        {
            get { return magicObjectID; }
            set
            {
                if (magicObjectID == value) return;
                magicObjectID = value;
                MagicObject = MapControl.Objects.Find(x => x.ObjectID == value);
            }
        }

        public abstract ObjectType Race { get; }
        public abstract bool Blocking { get; }

        public uint ObjectID;
        public string Name = string.Empty;
        public Point CurrentLocation, MapLocation;
        public MirDirection Direction;
        public bool Dead, Hidden, SitDown, Sneaking;
        public PoisonType Poison;
        public long DeadTime;
        public uint AI;
        public bool InTrapRock;
        public int JumpDistance;

        public bool Blend = true;

        public long BlindTime;
        public byte BlindCount;

        private byte percentHealth;
        public virtual byte PercentHealth
        {
            get { return percentHealth; }
            set
            {
                if (percentHealth == value) return;

                percentHealth = value;
            }
        }
        public long HealthTime;

        private byte percentMana;
        public virtual byte PercentMana
        {
            get { return percentMana; }
            set
            {
                if (percentMana == value) return;

                percentMana = value;
            }
        }

        public uint LastTargetObjectId => lastTargetObjectId;

        public List<QueuedAction> ActionFeed = new List<QueuedAction>();
        public QueuedAction NextAction
        {
            get { return ActionFeed.Count > 0 ? ActionFeed[0] : null; }
        }

        public List<Effect> Effects = new List<Effect>();
        public List<BuffType> Buffs = new List<BuffType>();

        public MLibrary BodyLibrary;
        public Color DrawColour = Color.White, NameColour = Color.White, LightColour = Color.White;
        public MirLabel NameLabel, ChatLabel, GuildLabel, HealthNumberLabel;
        public long ChatTime;
        public int DrawFrame, DrawWingFrame;
        public Point DrawLocation, Movement, FinalDrawLocation, OffSetMove;
        public Rectangle DisplayRectangle;
        public int Light, DrawY;
        public long NextMotion, NextMotion2;
        public MirAction CurrentAction;
        public byte CurrentActionLevel;
        public bool SkipFrames;
        public FrameLoop FrameLoop = null;

        //Sound
        public int StruckWeapon;

        //最近一次收到受击广播的时间（内挂「打不中检测」用：长时间无受击 = 打不到该目标）
        public long LastStruckTime;

        //最近一次被「自己或自己的英雄」打出伤害的时间（内挂「攻击无效检测」用：
        //服务端只有真正造成伤害才广播受击，所以这个时间不变 = 自己的攻击打不动它）
        public long LastStruckByMeTime;

        public MirLabel TempLabel;

        public static List<MirLabel> DamageLabelList = new List<MirLabel>();

        //数字显血用的当前/最大血量绝对值（服务端 S.ObjectHealth 下发；MaxHP==0 表示服务端没给，退回百分比）
        public int CurrentHP, MaxHP;
        public List<Damage> Damages = new List<Damage>();

        protected Point GlobalDisplayLocationOffset
        {
            get { return new Point(0, 0); }
        }

        /// <summary>
        /// 内挂「只显示 1 格内玩家」过滤（纯本地渲染，不发包、不影响服务端判定）。
        ///
        /// 勾选后，除自己以外的玩家只要超出身边 1 格（含斜向 8 格，切比雪夫距离 > 1）
        /// 就整条不画：身体、武器、翅膀、坐骑、品质光效、名字、对话气泡、血条、毒、伤害数字。
        /// 怪物 / NPC / 地面物品 / 宝宝 一律不受影响（它们不是"大量玩家"的来源，
        /// 跟着一起藏会让打怪、看掉落全乱）。
        ///
        /// 英雄（自己和其他人的）不参与过滤：自己的英雄走远两步就消失会直接影响操作。
        ///
        /// 例外常显（NearPlayerKeepVisible，超出 1 格也照常画）：
        /// 队友、同会成员、鼠标指向的人、当前目标 / 魔法锁定 / 施法对象。
        /// 队友和同会看不见会直接打乱跑位和救人；目标看不见则锁定后凭空消失，反而不安全。
        ///
        /// 注意：只改绘制，不动对象本身 —— 鼠标点选、内挂索敌、服务端逻辑全都照旧，
        /// 关掉勾选立刻恢复正常显示（不需要重登）。
        /// </summary>
        public bool NearPlayerFiltered
        {
            get
            {
                if (!Settings.NearPlayerOnly) return false;
                if (User == null) return false;
                if (!(this is PlayerObject) || this == User) return false;
                if (this is HeroObject || this is UserHeroObject) return false;
                if (NearPlayerKeepVisible) return false;

                return Math.Abs(Movement.X - User.Movement.X) > 1
                    || Math.Abs(Movement.Y - User.Movement.Y) > 1;
            }
        }

        /// <summary>
        /// 「只显示 1 格内玩家」的常显白名单：队友 / 同会 / 鼠标指向 / 当前目标（含魔法锁定与施法对象）。
        /// 单独拆出来是为了让判定顺序一目了然：先看该不该留，再看距离。
        /// 队友查的是 GroupDialog.GroupList（服务端下发的队员名，最多 MaxGroup 个，成本可忽略）；
        /// 行会比对双方 GuildName 且要求非空 —— 两个没行会的人不能因为都是空串就互相常显。
        /// </summary>
        public bool NearPlayerKeepVisible
        {
            get
            {
                if (!(this is PlayerObject)) return false;
                if (this is HeroObject || this is UserHeroObject) return false;

                if (this == MouseObject || this == TargetObject || this == LockObject || this == MagicObject)
                    return true;

                if (!string.IsNullOrEmpty(Name) && GroupDialog.GroupList.Contains(Name))
                    return true;

                string guild = ((PlayerObject)this).GuildName;
                if (string.IsNullOrEmpty(guild)) return false;

                return User != null && !string.IsNullOrEmpty(User.GuildName) && guild == User.GuildName;
            }
        }

        protected MapObject() { }

        protected MapObject(uint objectID)
        {
            ObjectID = objectID;

            for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob.ObjectID != ObjectID) continue;
                ob.Remove();
            }

            MapControl.Objects.Add(this);

            RestoreTargetStates();
        }
        public void Remove()
        {
            if (MouseObject == this) MouseObjectID = 0;
            if (LockObject == this) LockObjectID = 0;   // 魔法锁定：目标离开视野即解除
            if (TargetObject == this)
            {
                TargetObjectID = 0;
                lastTargetObjectId = ObjectID;
            }
            if (MagicObject == this) MagicObjectID = 0;

            if (this == User.NextMagicObject)
                User.ClearMagic();

            MapControl.Objects.Remove(this);
            GameScene.Scene.MapControl.RemoveObject(this);

            if (ObjectID == Hero?.ObjectID)
                HeroObject = null;

            if (ObjectID != GameScene.NPCID) return;

            GameScene.NPCID = 0;
            GameScene.Scene.NPCDialog.Hide();
        }

        public abstract void Process();
        public abstract void Draw();
        public abstract bool MouseOver(Point p);

        private void RestoreTargetStates()
        {
            if (MouseObjectID == ObjectID)
                MouseObject = this;

            if (TargetObjectID == ObjectID)
                TargetObject = this;

            if (MagicObjectID == ObjectID)
                MagicObject = this;

            if (!this.Dead &&
                TargetObject == null &&
                LastTargetObjectId == ObjectID)
            {
                switch (Race)
                {
                    case ObjectType.Player:
                    case ObjectType.Monster:
                    case ObjectType.Hero:
                        targetObjectID = ObjectID;
                        TargetObject = this;
                        break;
                }
            }
        }

        public void AddBuffEffect(BuffType type)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                if (!(Effects[i] is BuffEffect)) continue;
                if (((BuffEffect)(Effects[i])).BuffType == type) return;
            }

            PlayerObject ob = null;

            if (Race == ObjectType.Player)
            {
                ob = (PlayerObject)this;
            }

            switch (type)
            {
                case BuffType.血龙剑法:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 190, 7, 1400, this, true, type) { Repeat = true });
                    break;
                case BuffType.天务:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 130, 8, 900, this, true, type) { Repeat = false });
                    SoundManager.PlaySound(20000 + (ushort)Spell.CounterAttack * 10 + 0);
                    Effects.Add(new BuffEffect(Libraries.Magic3, 140, 2, 800, this, true, type) { Repeat = true });
                    break;
                case BuffType.金刚不坏:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 570, 5, 1400, this, true, type) { Repeat = true });
                    break;
                case BuffType.金刚不坏秘籍:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 570, 5, 1400, this, true, type) { Repeat = true });
                    break;
                case BuffType.天上秘术:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 210, 7, 1400, this, true, type) { Repeat = true });
                    break;
                case BuffType.轻身步:
                    if (ob != null) ob.Sprint = true;
                    break;
                case BuffType.月影术:
                case BuffType.烈火身:
                    if (ob != null) ob.Sneaking = true;
                    break;
                case BuffType.吸血地闪:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 2110, 6, 1400, this, true, type) { Repeat = false });
                    break;
                case BuffType.毒魔闪:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 2310, 7, 1400, this, true, type) { Repeat = false });
                    break;
                case BuffType.先天气功:
                    BuffEffect effect;

                    Effects.Add(effect = new BuffEffect(Libraries.Magic2, 1880, 9, 900, this, true, type) { Repeat = false });
                    SoundManager.PlaySound(20000 + (ushort)Spell.EnergyShield * 10 + 0);

                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Magic2, 1900, 2, 800, this, true, type) { Repeat = true });
                    };
                    break;
                case BuffType.深延术:
					Effects.Add(new BuffEffect(Libraries.Magic3, 90, 6, 1200, this, true, type) { Repeat = true });
                    break;
                case BuffType.血龙水:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 230, 6, 1200, this, true, type) { Repeat = true });
                    break;
				case BuffType.游戏管理:
					Effects.Add(new BuffEffect(Libraries.CHumEffect[5], 0, 1, 1200, this, true, type) { Repeat = true });
					break;
                case BuffType.华丽雨光:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 3470, 8, 1400, this, true, type) { Repeat = true });
                    break;
                case BuffType.龙之特效:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 7040, 32, 1600, this, true, type, Color.FromArgb(158, 255, 158, 98)) { Blend = true, Repeat = true, Delay = 60000 });
                    Effects.Add(new BuffEffect(Libraries.Magic3, 7080, 25, 1200, this, true, type, Color.FromArgb(158, 255, 255, 255)) { Repeat = true, DrawBehind = true , Delay = 10000 });
                    break;
                case BuffType.龙的特效:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 7040, 32, 1600, this, true, type, Color.FromArgb(158, 98, 255, 255)) { Blend = true, Repeat = true, Delay = 60000 });
                    Effects.Add(new BuffEffect(Libraries.Magic3, 7080, 25, 1200, this, true, type, Color.FromArgb(158, 255, 255, 255)) { Repeat = true, DrawBehind = true, Delay = 10000 });
                    break;
                case BuffType.GeneralMeowMeowShield:
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.GeneralMeowMeow], 569, 7, 700, this, true, type) { Repeat = true, Light = 1 });
                    SoundManager.PlaySound(8322);
                    break;
                case BuffType.御体之力:
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.PowerUpBead], 62, 6, 600, this, true, type) { Blend = true, Repeat = true });
                    break;
                case BuffType.HornedArcherBuff:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedArcher], 524, 6, 600, this, true, type) { Repeat = false });
                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedArcher], 530, 3, 1000, this, true, type) { Blend = true, Repeat = true });
                    };
                    break;
                case BuffType.ColdArcherBuff:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedArcher], 533, 7, 700, this, true, type) { Repeat = false });
                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedArcher], 540, 3, 1000, this, true, type) { Blend = true, Repeat = true });
                    };
                    break;
                case BuffType.HornedColdArcherBuff:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedArcher], 543, 7, 700, this, true, type) { Repeat = false });
                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedArcher], 550, 3, 1000, this, true, type) { Blend = true, Repeat = true });
                    };
                    break;
                case BuffType.HornedWarriorShield:
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedWarrior], 960, 18, 1800, this, true, type) { Repeat = true });
                    break;
                case BuffType.HornedCommanderShield:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedCommander], 1341, 1, 100, this, true, type) { Repeat = false, Light = 1 });
                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedCommander], 1342, 16, 1600, this, true, type) { Repeat = true, Light = 1 });
                    };
                    break;		
                case BuffType.ChieftainSwordBuff:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.ChieftainSword], 1331, 6, 100, this, true, type) { Repeat = false, Light = 1 });
                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.ChieftainSword], 1337, 3, 600, this, true, type) { Repeat = true, Light = 1 });
                    };
                    break;
                case BuffType.寒冰护甲:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.ShardGuardian], 517, 11, 800, this, true, type) { Repeat = false, Light = 1, DrawBehind = true });
                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.ShardGuardian], 528, 8, 1600, this, true, type) { Repeat = true, Light = 1 });
                    };
                    break;
                case BuffType.ReaperPriestBuff:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.ReaperPriest], 497, 16, 800, this, true, type) { Repeat = false, Light = 1, DrawBehind = true });
                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.ReaperPriest], 479, 18, 1600, this, true, type) { Repeat = true, Light = 1 });
                    };
                    break;
                case BuffType.至尊威严:
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.NobleWarrior], 472, 6, 600, this, true, type) { Repeat = true, Light = 1 });
                    break;
                case BuffType.伤口加深:
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.NobleAssassin], 544, 6, 600, this, true, type) { Repeat = true, Light = 1 });
                    break;
                case BuffType.死亡印记:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.Butcher], 690, 1, 100, this, true, type) { Repeat = false, Light = 1 });
                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.Butcher], 690, 10, 1600, this, true, type) { Repeat = true, Light = 1 });
                    };
                    break;
                case BuffType.RiklebitesShield:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.Riklebites], 1084, 3, 500, this, true, type) { Repeat = false, Light = 1 });
                    effect.Complete += (o, e) =>
                    {
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.Riklebites], 1087, 5, 800, this, true, type) { Repeat = true, Light = 1 });
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.Riklebites], 1094, 8, 800, this, true, type) { Repeat = true, DrawBehind = true });
                    };
                    break;
                case BuffType.绝对封锁:
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.Mon562N], 648, 10, 600, this, true, type) { Repeat = true, Light = 1 });
                    break;
                case BuffType.Mon564NSealing:
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.Mon564N], 1016, 10, 600, this, true, type) { Repeat = true, Light = 1 });
                    break;
                case BuffType.烈火焚烧:
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.Mon571N], 574, 10, 600, this, true, type) { Repeat = true, Light = 1 });
                    break;
                case BuffType.防御诅咒:
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.Mon573N], 562, 6, 600, this, true, type) { Repeat = true, Light = 1 });
                    break;
                // ==== 20261103 韩服20261001移植：615B护盾（起手3帧→循环8帧，库号615；服务端BuffType顺延值254） ====
                case BuffType.Mon615BShield:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.Mon615B], 1133, 3, 300, this, true, type) { Repeat = false, Light = 1 });
                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.Mon615B], 1135, 8, 800, this, true, type) { Repeat = true, Light = 1 });
                    };
                    break;
            }
        }
        public void RemoveBuffEffect(BuffType type)
        {
            PlayerObject ob = null;

            if (Race == ObjectType.Player)
            {
                ob = (PlayerObject)this;
            }

            for (int i = 0; i < Effects.Count; i++)
            {
                if (!(Effects[i] is BuffEffect)) continue;
                if (((BuffEffect)(Effects[i])).BuffType != type) continue;
                Effects[i].Repeat = false;
            }

            switch (type)
            {
                case BuffType.轻身步:
                    if (ob != null) ob.Sprint = false;
                    break;
                case BuffType.月影术:
                case BuffType.烈火身:
                    if (ob != null) ob.Sneaking = false;
                    break;
            }
        }

        public Color ApplyDrawColour()
        {
            Color drawColour = DrawColour;
            if (drawColour == Color.Gray)
            {
                drawColour = Color.White;
                DXManager.SetGrayscale(true);
            }
            return drawColour;
        }

        public virtual Missile CreateProjectile(int baseIndex, MLibrary library, bool blend, int count, int interval, int skip, int lightDistance = 6, bool direction16 = true, Color? lightColour = null, uint targetID = 0)
        {
            return null;
        }

        public void Chat(string text)
        {
            if (ChatLabel != null && !ChatLabel.IsDisposed)
            {
                ChatLabel.Dispose();
                ChatLabel = null;
            }

            const int chatWidth = 200;
            List<string> chat = new List<string>();

            int index = 0;
            for (int i = 1; i < text.Length; i++)
                if (TextRenderer.MeasureText(CMain.Graphics, text.Substring(index, i - index), ChatFont).Width > chatWidth)
                {
                    chat.Add(text.Substring(index, i - index - 1));
                    index = i - 1;
                }
            chat.Add(text.Substring(index, text.Length - index));

            text = chat[0];
            for (int i = 1; i < chat.Count; i++)
                text += string.Format("\n{0}", chat[i]);

            ChatLabel = new MirLabel
            {
                AutoSize = true,
                BackColour = Color.Transparent,
                ForeColour = Color.White,
                OutLine = true,
                OutLineColour = Color.Black,
                DrawFormat = TextFormatFlags.HorizontalCenter,
                Text = text,
            };
            ChatTime = CMain.Time + 5000;
        }
        public virtual void DrawChat()
        {
            if (ChatLabel == null || ChatLabel.IsDisposed) return;

            if (CMain.Time > ChatTime)
            {
                ChatLabel.Dispose();
                ChatLabel = null;
                return;
            }

            ChatLabel.ForeColour = Dead ? Color.Gray : Color.White;
            ChatLabel.Location = new Point(DisplayRectangle.X + (48 - ChatLabel.Size.Width) / 2, DisplayRectangle.Y - (60 + ChatLabel.Size.Height) - (Dead ? 35 : 0));
            ChatLabel.Draw();
        }

        public virtual void CreateLabel()
        {
            NameLabel = null;

            for (int i = 0; i < LabelList.Count; i++)
            {
                if (LabelList[i].Text != Name || LabelList[i].ForeColour != NameColour) continue;
                NameLabel = LabelList[i];
                break;
            }


            if (NameLabel != null && !NameLabel.IsDisposed) return;

            NameLabel = new MirLabel
            {
                AutoSize = true,
                BackColour = Color.Transparent,
                ForeColour = NameColour,
                OutLine = true,
                OutLineColour = Color.Black,
                Text = Name,
            };
            NameLabel.Disposing += (o, e) => LabelList.Remove(NameLabel);
            LabelList.Add(NameLabel);



        }
        public virtual void DrawName()
        {
            CreateLabel();

            if (NameLabel == null) return;
            
            NameLabel.Text = Name;
            NameLabel.Location = new Point(DisplayRectangle.X + (50 - NameLabel.Size.Width) / 2, DisplayRectangle.Y - (32 - NameLabel.Size.Height / 2) + (Dead ? 35 : 8)); //was 48 -
            NameLabel.Draw();
        }
        public virtual void DrawBlend()
        {
            DXManager.SetBlend(true, 0.3F); //0.8
            Draw();
            DXManager.SetBlend(false);
        }
        public void DrawDamages()
        {
            for (int i = Damages.Count - 1; i >= 0; i--)
            {
                Damage info = Damages[i];
                if (CMain.Time > info.ExpireTime)
                {
                    if (info.DamageLabel != null)
                    {
                        info.DamageLabel.Dispose();
                    }

                    Damages.RemoveAt(i);
                }
                else
                {
                    info.Draw(DisplayRectangle.Location);
                }
            }
        }
        public virtual bool ShouldDrawHealth()
        {
            return false;
        }
        public void DrawHealth()
        {
            string name = Name;            
            if (Name.Contains("(")) name = Name.Substring(Name.IndexOf("(") + 1, Name.Length - Name.IndexOf("(") - 2);

            if (Dead) return;
            if (Race != ObjectType.Player && Race != ObjectType.Monster && Race != ObjectType.Hero) return;

            if (CMain.Time >= HealthTime)
            {
                if (!ShouldDrawHealth()) return;
            }

            Libraries.Prguse2.Draw(0, DisplayRectangle.X + 8, DisplayRectangle.Y - 64);
            int index = 1;

            switch (Race)
            {
                case ObjectType.Player:
                    if (GroupDialog.GroupList.Contains(name)) index = 10;
                    break;
                case ObjectType.Monster:
                    if (GroupDialog.GroupList.Contains(name) || name == User.Name) index = 11;
                    break;
                case ObjectType.Hero:
                    if (GroupDialog.GroupList.Contains(MapObject.HeroObject?.OwnerName)) // Fails but not game breaking
                    {
                            index = 11; 
                    }
                    if (HeroObject.HeroObject?.OwnerName == User.Name)
                    {
                        index = 1; 
                        if ((MapObject.HeroObject.Class != MirClass.战士 && HeroObject.Level > 7) || (MapObject.HeroObject.Class == MirClass.战士 && HeroObject.Level > 25))
                        {
                           Libraries.Prguse2.Draw(10, new Rectangle(0, 0, (int)(32 * PercentMana / 100F), 4), new Point(DisplayRectangle.X + 8, DisplayRectangle.Y - 60), Color.White, false);
                        }
                    }
                    break;
            }

            Libraries.Prguse2.Draw(index, new Rectangle(0, 0, (int)(32 * PercentHealth / 100F), 4), new Point(DisplayRectangle.X + 8, DisplayRectangle.Y - 64), Color.White, false);

            //数字显血（内挂面板勾选）：在血条下方显示百分比数字，只有血条本身显示时才显示
            if (Settings.HealthNumber) DrawHealthNumber();
        }

        /// <summary>
        /// 在血条下方绘制血量数字：服务端给了绝对值就显示「当前/最大」，没给（MaxHP=0）才退回百分比。
        /// 前面带对象等级 Lv.xx（玩家/英雄走自带 Level；怪物走 ObjectMonster 包尾追加的 Level，旧服务端没发就不带）。
        /// 位置固定在 32x4 血条正下方（Y-54，避开英雄蓝条 Y-60~Y-56）。
        /// </summary>
        private void DrawHealthNumber()
        {
            string text = MaxHP > 0
                ? string.Format("{0}/{1}", CurrentHP, MaxHP)
                : string.Format("{0}%", PercentHealth);

            //等级前缀：拿不到等级（旧服务端/未知类型）就保持空串，显示原样
            string lv = GetHealthLevelText();
            if (lv.Length > 0) text = lv + " " + text;

            if (HealthNumberLabel == null || HealthNumberLabel.IsDisposed)
            {
                HealthNumberLabel = new MirLabel
                {
                    AutoSize = true,
                    BackColour = Color.Transparent,
                    ForeColour = Color.White,
                    OutLine = true,
                    OutLineColour = Color.Black,
                };
            }

            //Text 相同时 MirLabel 内部不会重建纹理
            HealthNumberLabel.Text = text;

            HealthNumberLabel.Location = new Point(DisplayRectangle.X + 8 + (32 - HealthNumberLabel.Size.Width) / 2, DisplayRectangle.Y - 54);
            HealthNumberLabel.Draw();
        }

        /// <summary>数字显血的等级前缀（Lv.xx）：玩家/英雄用自带 Level，怪物用 ObjectMonster 下发的 Level；拿不到返回空串。</summary>
        private string GetHealthLevelText()
        {
            switch (Race)
            {
                case ObjectType.Player:
                case ObjectType.Hero:
                    return string.Format("Lv.{0}", ((PlayerObject)this).Level);
                case ObjectType.Monster:
                {
                    ushort mobLevel = ((MonsterObject)this).Level;
                    return mobLevel > 0 ? string.Format("Lv.{0}", mobLevel) : string.Empty;
                }
                default:
                    return string.Empty;
            }
        }

        public void DrawPoison()
        {
            byte poisoncount = 0;
            if (Poison != PoisonType.None)
            {
                if (Poison.HasFlag(PoisonType.Green))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), Color.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), Color.Green);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Red))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), Color.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), Color.Red);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Bleeding))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), Color.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), Color.DarkRed);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Slow))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), Color.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), Color.Purple);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Stun) || Poison.HasFlag(PoisonType.Dazed))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), Color.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), Color.Yellow);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Blindness))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), Color.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), Color.MediumVioletRed);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Frozen))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), Color.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), Color.Blue);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Paralysis) || Poison.HasFlag(PoisonType.LRParalysis))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), Color.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), Color.Gray);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.DelayedExplosion))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), Color.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), Color.Orange);
                    poisoncount++;
                }
            }
        }

        public abstract void DrawBehindEffects(bool effectsEnabled);

        public abstract void DrawEffects(bool effectsEnabled);

        protected void LoopFrame(int start, int frameCount, int frameInterval, int duration)
        {
            if (FrameLoop == null)
            {
                FrameLoop = new FrameLoop
                {
                    Start = start,
                    End = start + frameCount - 1,
                    Loops = (duration / (frameInterval * frameCount)) - 1 //Remove 1 count as we've already done a loop before this is checked
                };
            }
        }
    }

    public class FrameLoop
    {
        public MirAction Action { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public int Loops { get; set; }

        public int CurrentCount { get; set; }
    }

}
