using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using Client.MirSounds;

namespace Client.MirScenes.Dialogs
{
    public sealed class CharacterDialog : MirImageControl
    {
        public MirButton CloseButton, CharacterButton, StatusButton, StateButton, SkillButton;
        public MirImageControl CharacterPage, StatusPage, StatePage, SkillPage, ClassImage;

        public MirLabel NameLabel, GuildLabel, LoverLabel;
        public MirLabel ACLabel, MACLabel, DCLabel, MCLabel, SCLabel, HealthLabel, ManaLabel;
        public MirLabel CritRLabel, CritDLabel, LuckLabel, AttkSpdLabel, AccLabel, AgilLabel;
        public MirLabel ExpPLabel, BagWLabel, WearWLabel, HandWLabel, MagicRLabel, PoisonRecLabel, HealthRLabel, ManaRLabel, PoisonResLabel, HolyTLabel, FreezeLabel, PoisonAtkLabel, ReflectAtkLabel, HPDrainRatePercentLabel;
        public MirLabel HeadingLabel, StatLabel;
        public MirButton NextButton, BackButton;

        public MirItemCell[] Grid;
        private MirGridType GridType;
        public MagicButton[] Magics;

        public int StartIndex;
        private UserObject Actor;

        public CharacterDialog(MirGridType gridType, UserObject actor)
        {
            Actor = actor;
            GridType = gridType;

            Index = gridType == MirGridType.HeroEquipment ? 505 : 504;
            Library = Libraries.Title;
            Location = new Point(Settings.ScreenWidth - 264, 0);
            Movable = true;
            Sort = true;            

            BeforeDraw += (o, e) => RefreshInterface();

            CharacterPage = new MirImageControl
            {
                Index = 340,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(8, 90),
            };
            CharacterPage.AfterDraw += (o, e) =>
            {
                //★注意：底板的 Index 必须在 RefreshInterface()（BeforeDraw）里决定。
                //  在这里改是**无效**的——控件是先画完自己的 Index 图再触发 AfterDraw，
                //  而且 RefreshInterface 每帧都会重置 Index（11-09 教训：改在这里等于没改）。

                if (Libraries.StateItems == null) return;
                ItemInfo RealItem = null;

                //★登仙内观：羽化时头盔/发型层要用补偿量对齐头部。
                //  羽化未穿甲（画素体）→ GetHumUpHeadOffset（素体表，11-09 标定）；
                //  羽化穿甲（画职业立绘）→ GetHumUpPortraitHeadOffset（立绘表，11-10 逐职业标定）。
                //  具体取值在下方 hairPortraitShift 算出来之后（headDx 声明移到那里）。

                //★登仙内观：羽化角色的盔甲层改画 Prguse 裸体素体（320/321 弓手，330~337 战/法/道/刺男女）。
                //  与普通盔甲立绘互斥（else if），普通盔甲/翅膀/品质描边在羽化时整段跳过；
                //  武器、头盔/发型层仍走原逻辑，不受影响。
                //★登仙内观（11-10 三改，用户定稿）：羽化角色内观**不显示实际装备的内外观**，
                //  盔甲层统一画分职业/性别的 StateItem 固定立绘（衣服帧，完整人物含头脸）；
                //  战法道通用装备 → 走 StateItem_Common_* 库。因此下方头盔/发型层羽化时整段跳过。
                bool humUpState = actor.HumUpStage > 0;
                bool humUpStateCommon = false;
                //★羽化内观"褪下"逻辑（11-10）：没穿盔甲 → 回 Prguse 裸体素体（不顶着仙兵盔甲立绘），
                //  穿了才画分职业 StateItem 固定衣服帧；武器同理，没拿就不画武器帧。
                bool humUpHasArmour = Grid[(int)EquipmentSlot.盔甲].Item != null;
                if (humUpState)
                {
                    ItemInfo armourInfo = humUpHasArmour ? Grid[(int)EquipmentSlot.盔甲].Item.Info : null;
                    ItemInfo weaponInfo = Grid[(int)EquipmentSlot.武器].Item != null ? Grid[(int)EquipmentSlot.武器].Item.Info : null;
                    //★按「这件装备自己的 RequiredClass」判，且只对 战/法/道 生效（11-10 修正）。
                    //  原来按 Shape 反查会把弓箭/刺客的盔甲（shape 10/11/19/29/30/57/58 与通用同号）
                    //  误判成通用装备 → 弓手武器帧画成通用 2430（弯刀），内观里弓手在拿刀。
                    humUpStateCommon = PlayerObject.IsCommonAppearance(actor.Class, armourInfo, weaponInfo);

                    if (humUpHasArmour)
                    {
                        //★羽化专属装备（Shape 1000~1005）：直接拿装备自己的 Image 当帧号，
                        //  去**该装备所属职业**的 StateItem 库取整身立绘（通用装备走 StateItem_Common_*）。
                        //  非羽化装备 / 空帧 → 自动回落到固定帧表，行为与之前完全一致。
                        PlayerObject.ResolveHumUpArmourFrame(armourInfo, actor.Class, actor.Gender, humUpStateCommon,
                                                             out MLibrary stateLib, out int humUpArmourFrame);
                        if (stateLib != null)
                            stateLib.Draw(humUpArmourFrame, DisplayLocation, Color.White, true, 1F);
                    }
                    else
                    {
                        int bodyIndex, bodyOffX, bodyOffY;
                        GetHumUpBody(actor.Class, actor.Gender, out bodyIndex, out bodyOffX, out bodyOffY);
                        Libraries.Prguse.Draw(bodyIndex, new Point(DisplayLocation.X + bodyOffX, DisplayLocation.Y + bodyOffY), Color.White, true, 1F);
                    }
                }
                else if (Grid[(int)EquipmentSlot.盔甲].Item != null)
                {
                    RealItem = Functions.GetRealItem(Grid[(int)EquipmentSlot.盔甲].Item.Info, actor.Level, actor.Class, GameScene.ItemInfoList);

                    // 衣服品质描边发光（内观静态立绘：只描边，不冒火，避免三套光叠成脏画面）
                    QualityOutlineGlow.DrawPortrait(Libraries.StateItems, RealItem.Image, DisplayLocation,
                                                    (byte)Grid[(int)EquipmentSlot.盔甲].Item.Info.Grade, false);

                    Libraries.StateItems.Draw(RealItem.Image, DisplayLocation, Color.White, true, 1F);
                    {
                        if (actor.WingEffect > 0)
                        {
                            int genderOffset = actor.Gender == MirGender.男性 ? 0 : 1;

                            switch (actor.WingEffect)
                            {
                                case 1:
                                    if (actor.WingEffect == 1)
                                        Libraries.Prguse2.DrawBlend(1202 + genderOffset, DisplayLocation, Color.White, true, 1F);
                                    break;
                                case 2:
                                    if (actor.WingEffect == 2)
                                        Libraries.Prguse2.DrawBlend(1204 + genderOffset, DisplayLocation, Color.White, true, 1F);
                                    break;
                                case 58:
                                    if (actor.WingEffect == 58)
                                        Libraries.Prguse2.DrawBlend(1530 + genderOffset, DisplayLocation, Color.White, true, 1F);
                                    break;
                                case 59:
                                    if (actor.WingEffect == 59)
                                        Libraries.Prguse2.DrawBlend(1532 + genderOffset, DisplayLocation, Color.White, true, 1F);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }

                //★登仙内观（11-10）：羽化角色穿了武器才画分职业固定武器帧（"褪下"后空手不画）。
                if (humUpState && Grid[(int)EquipmentSlot.武器].Item != null)
                {
                    ItemInfo humUpWeaponInfo = Grid[(int)EquipmentSlot.武器].Item.Info;
                    PlayerObject.ResolveHumUpWeaponFrame(humUpWeaponInfo, actor.Class, actor.Gender, humUpStateCommon,
                                                         out MLibrary stateLib, out int humUpWeaponFrame);
                    if (stateLib != null)
                        stateLib.Draw(humUpWeaponFrame, DisplayLocation, Color.White, true, 1F);
                }
                else if (Grid[(int)EquipmentSlot.武器].Item != null)
                {
                    RealItem = Functions.GetRealItem(Grid[(int)EquipmentSlot.武器].Item.Info, actor.Level, actor.Class, GameScene.ItemInfoList);

                    // 武器品质火焰：先画光再画武器，火只在武器轮廓外沿（不会糊住武器本体）
                    QualityOutlineGlow.DrawPortrait(Libraries.StateItems, RealItem.Image, DisplayLocation,
                                                    (byte)Grid[(int)EquipmentSlot.武器].Item.Info.Grade, true);

                    Libraries.StateItems.Draw(RealItem.Image, DisplayLocation, Color.White, true, 1F);

                    if (actor.WeaponEffect > 0)
                    {
                        switch (actor.WeaponEffect)
                        {
                            case 21:
                                if (actor.WeaponEffect == 21)
                                    Libraries.StateitemEffect.DrawBlend(4, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 22:
                                if (actor.WeaponEffect == 22)
                                    Libraries.StateitemEffect.DrawBlend(20, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 23:
                                if (actor.WeaponEffect == 23)
                                    Libraries.StateitemEffect.DrawBlend(0, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 26:
                                if (actor.WeaponEffect == 26)
                                    Libraries.StateitemEffect.DrawBlend(24, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 27:
                                if (actor.WeaponEffect == 27)
                                    Libraries.StateitemEffect.DrawBlend(28, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 28:
                                if (actor.WeaponEffect == 28)
                                    Libraries.StateitemEffect.DrawBlend(32, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 29:
                                if (actor.WeaponEffect == 29)
                                    Libraries.StateitemEffect.DrawBlend(12, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 30:
                                if (actor.WeaponEffect == 30)
                                    Libraries.StateitemEffect.DrawBlend(16, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 31:
                                if (actor.WeaponEffect == 31)
                                    Libraries.StateitemEffect.DrawBlend(8, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 32:
                                if (actor.WeaponEffect == 32)
                                    Libraries.StateitemEffect.DrawBlend(36, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 33:
                                if (actor.WeaponEffect == 33)
                                    Libraries.StateitemEffect.DrawBlend(40, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 34:
                                if (actor.WeaponEffect == 34)
                                    Libraries.StateitemEffect.DrawBlend(44, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 35:
                                if (actor.WeaponEffect == 35)
                                    Libraries.StateitemEffect.DrawBlend(52, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 36:
                                if (actor.WeaponEffect == 36)
                                    Libraries.StateitemEffect.DrawBlend(60, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 37:
                                if (actor.WeaponEffect == 37)
                                    Libraries.StateitemEffect.DrawBlend(56, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 38:
                                if (actor.WeaponEffect == 38)
                                    Libraries.StateitemEffect.DrawBlend(64, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 39:
                                if (actor.WeaponEffect == 39)
                                    Libraries.StateitemEffect.DrawBlend(68, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 40:
                                if (actor.WeaponEffect == 40)
                                    Libraries.StateitemEffect.DrawBlend(72, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 41:
                                if (actor.WeaponEffect == 41)
                                    Libraries.StateitemEffect.DrawBlend(48, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 43:
                                if (actor.WeaponEffect == 43)
                                    Libraries.StateItems.DrawBlend(922, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 45:
                                if (actor.WeaponEffect == 45)
                                    Libraries.StateitemEffect.DrawBlend(76, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 51:
                                if (actor.WeaponEffect == 51)
                                    Libraries.StateitemEffect.DrawBlend(108, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 52:
                                if (actor.WeaponEffect == 52)
                                    Libraries.StateitemEffect.DrawBlend(124, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 53:
                                if (actor.WeaponEffect == 53)
                                    Libraries.StateitemEffect.DrawBlend(104, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 56:
                                if (actor.WeaponEffect == 56)
                                    Libraries.StateitemEffect.DrawBlend(128, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 57:
                                if (actor.WeaponEffect == 57)
                                    Libraries.StateitemEffect.DrawBlend(132, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 58:
                                if (actor.WeaponEffect == 58)
                                    Libraries.StateitemEffect.DrawBlend(136, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 59:
                                if (actor.WeaponEffect == 59)
                                    Libraries.StateitemEffect.DrawBlend(116, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 60:
                                if (actor.WeaponEffect == 60)
                                    Libraries.StateitemEffect.DrawBlend(120, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 61:
                                if (actor.WeaponEffect == 61)
                                    Libraries.StateitemEffect.DrawBlend(112, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 62:
                                if (actor.WeaponEffect == 62)
                                    Libraries.StateitemEffect.DrawBlend(140, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 63:
                                if (actor.WeaponEffect == 63)
                                    Libraries.StateitemEffect.DrawBlend(144, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 64:
                                if (actor.WeaponEffect == 64)
                                    Libraries.StateitemEffect.DrawBlend(148, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 65:
                                if (actor.WeaponEffect == 65)
                                    Libraries.StateitemEffect.DrawBlend(156, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 66:
                                if (actor.WeaponEffect == 66)
                                    Libraries.StateitemEffect.DrawBlend(164, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 67:
                                if (actor.WeaponEffect == 67)
                                    Libraries.StateitemEffect.DrawBlend(160, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 68:
                                if (actor.WeaponEffect == 68)
                                    Libraries.StateitemEffect.DrawBlend(168, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 69:
                                if (actor.WeaponEffect == 69)
                                    Libraries.StateitemEffect.DrawBlend(172, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 70:
                                if (actor.WeaponEffect == 70)
                                    Libraries.StateitemEffect.DrawBlend(176, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 71:
                                if (actor.WeaponEffect == 71)
                                    Libraries.StateitemEffect.DrawBlend(152, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 75:
                                if (actor.WeaponEffect == 75)
                                    Libraries.StateitemEffect.DrawBlend(180, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 104:
                                if (actor.WeaponEffect == 104)
                                    Libraries.StateitemEffect.DrawBlend(80, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 105:
                                if (actor.WeaponEffect == 105)
                                    Libraries.StateitemEffect.DrawBlend(84, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 106:
                                if (actor.WeaponEffect == 106)
                                    Libraries.StateitemEffect.DrawBlend(88, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 107:
                                if (actor.WeaponEffect == 107)
                                    Libraries.StateitemEffect.DrawBlend(92, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 108:
                                if (actor.WeaponEffect == 108)
                                    Libraries.StateitemEffect.DrawBlend(96, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 109:
                                if (actor.WeaponEffect == 109)
                                    Libraries.StateitemEffect.DrawBlend(100, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 114:
                                if (actor.WeaponEffect == 114)
                                    Libraries.StateitemEffect.DrawBlend(184, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 115:
                                if (actor.WeaponEffect == 115)
                                    Libraries.StateitemEffect.DrawBlend(188, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 116:
                                if (actor.WeaponEffect == 116)
                                    Libraries.StateitemEffect.DrawBlend(192, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 117:
                                if (actor.WeaponEffect == 117)
                                    Libraries.StateitemEffect.DrawBlend(196, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 118:
                                if (actor.WeaponEffect == 118)
                                    Libraries.StateitemEffect.DrawBlend(200, DisplayLocation, Color.White, true, 1F);
                                break;
                            case 119:
                                if (actor.WeaponEffect == 119)
                                    Libraries.StateitemEffect.DrawBlend(204, DisplayLocation, Color.White, true, 1F);
                                break;
                            default:
                                break;
                        }
                    }
                }

                //★登仙内观（11-10 修正，用户纠正）：今天只要求盔甲/武器不显示普通内观，
                //  头盔/发型**照常显示**，不能整段跳过。
                //★登仙内观（11-10 四改，逐职业标定）：各职业 StateItem 立绘里人物的头位置都不同，
                //  不能再共用一个常数。用 Tools/HumUpHeadFit2 逐职业像素标定 + 像素级验证
                //  （发型/头盔掩码顶行-头顶行距离、物件重心-头心相对差，10 职业×2 层全部与素体基准一致）：
                //  立绘偏移 = 素体表 GetHumUpHeadOffset + (立绘头落点 - 素体头落点)，合成见立绘表。
                //  头盔与发型锚点差已烘焙在帧 XY 里，两层共用同一张表（素体基准已如此，立绘同验证）。
                bool hairPortraitShift = humUpState && humUpHasArmour;
                int headDx = 0, headDy = 0;
                if (humUpState)
                {
                    if (hairPortraitShift)
                        GetHumUpPortraitHeadOffset(actor.Class, actor.Gender, humUpStateCommon, out headDx, out headDy);
                    else
                        GetHumUpHeadOffset(actor.Class, actor.Gender, out headDx, out headDy);
                }
                int helmDx = headDx, helmDy = headDy;
                if (Grid[(int)EquipmentSlot.头盔].Item != null)
                {
                    // 头饰品质描边发光（硬头盔才有；没戴头盔时画的是发型，本来就没品质）
                    QualityOutlineGlow.DrawPortrait(Libraries.StateItems, Grid[(int)EquipmentSlot.头盔].Item.Info.Image,
                                                    new Point(DisplayLocation.X + helmDx, DisplayLocation.Y + helmDy),
                                                    (byte)Grid[(int)EquipmentSlot.头盔].Item.Info.Grade, false);

                    Libraries.StateItems.Draw(Grid[(int)EquipmentSlot.头盔].Item.Info.Image,
                                              new Point(DisplayLocation.X + helmDx, DisplayLocation.Y + helmDy), Color.White, true, 1F);
                }
                else
                {
                    int hair = 441 + actor.Hair + (actor.Class == MirClass.刺客 ? 20 : 0) + (actor.Gender == MirGender.男性 ? 0 : 40);

                    int offSetX = actor.Class == MirClass.刺客 ? (actor.Gender == MirGender.男性 ? 6 : 4) : 0;
                    int offSetY = actor.Class == MirClass.刺客 ? (actor.Gender == MirGender.男性 ? 25 : 18) : 0;

                    int hairDx = headDx;
                    int hairDy = headDy;

                    Libraries.Prguse.Draw(hair, new Point(DisplayLocation.X + offSetX + hairDx, DisplayLocation.Y + offSetY + hairDy), Color.White, true, 1F);
                }
            };

            StatusPage = new MirImageControl
            {
                Index = 506,
                Parent = this,
                Library = Libraries.Title,
                Location = new Point(8, 90),
                Visible = false,
            };
            StatusPage.BeforeDraw += (o, e) =>
            {
                ACLabel.Text = string.Format("{0}-{1}", actor.Stats[Stat.MinAC], actor.Stats[Stat.MaxAC]);
                MACLabel.Text = string.Format("{0}-{1}", actor.Stats[Stat.MinMAC], actor.Stats[Stat.MaxMAC]);
                DCLabel.Text = string.Format("{0}-{1}", actor.Stats[Stat.MinDC], actor.Stats[Stat.MaxDC]);
                MCLabel.Text = string.Format("{0}-{1}", actor.Stats[Stat.MinMC], actor.Stats[Stat.MaxMC]);
                SCLabel.Text = string.Format("{0}-{1}", actor.Stats[Stat.MinSC], actor.Stats[Stat.MaxSC]);
                HealthLabel.Text = string.Format("{0}/{1}", actor.HP, actor.Stats[Stat.HP]);
                ManaLabel.Text = string.Format("{0}/{1}", actor.MP, actor.Stats[Stat.MP]);
                CritRLabel.Text = string.Format("{0}%", actor.Stats[Stat.暴击倍率]);
                CritDLabel.Text = string.Format("{0}", actor.Stats[Stat.暴击伤害]);
                AttkSpdLabel.Text = string.Format("{0}", actor.Stats[Stat.攻击速度]);
                AccLabel.Text = string.Format("+{0}", actor.Stats[Stat.准确]);
                AgilLabel.Text = string.Format("+{0}", actor.Stats[Stat.敏捷]);
                LuckLabel.Text = string.Format("{0}", actor.Stats[Stat.幸运]);
            };

            StatePage = new MirImageControl
            {
                Index = 507,
                Parent = this,
                Library = Libraries.Title,
                Location = new Point(8, 90),
                Visible = false
            };
            StatePage.BeforeDraw += (o, e) =>
            {
                // MaxExperience 为 0（等级超出经验表）时避免显示 NaN%
                ExpPLabel.Text = actor.MaxExperience <= 0
                    ? "100%"
                    : string.Format("{0:0.##%}", actor.Experience / (double)actor.MaxExperience);
                BagWLabel.Text = string.Format("{0}/{1}", actor.CurrentBagWeight, actor.Stats[Stat.背包负重]);
                WearWLabel.Text = string.Format("{0}/{1}", actor.CurrentWearWeight, actor.Stats[Stat.装备负重]);
                HandWLabel.Text = string.Format("{0}/{1}", actor.CurrentHandWeight, actor.Stats[Stat.腕力负重]);
                MagicRLabel.Text = string.Format("+{0}", actor.Stats[Stat.魔法躲避]);
                PoisonResLabel.Text = string.Format("+{0}", actor.Stats[Stat.毒物躲避]);
                HealthRLabel.Text = string.Format("+{0}", actor.Stats[Stat.生命恢复]);
                ManaRLabel.Text = string.Format("+{0}", actor.Stats[Stat.法力恢复]);
                PoisonRecLabel.Text = string.Format("+{0}", actor.Stats[Stat.中毒恢复]);
                HolyTLabel.Text = string.Format("+{0}", actor.Stats[Stat.神圣]);
                FreezeLabel.Text = string.Format("+{0}", actor.Stats[Stat.冰冻伤害]);
                PoisonAtkLabel.Text = string.Format("+{0}", actor.Stats[Stat.毒素伤害]);
                ReflectAtkLabel.Text = string.Format("+{0}", actor.Stats[Stat.反弹伤害]);
                HPDrainRatePercentLabel.Text = string.Format("+ {0}%", actor.Stats[Stat.吸血数率]);
            };


            SkillPage = new MirImageControl
            {
                Index = 508,
                Parent = this,
                Library = Libraries.Title,
                Location = new Point(8, 90),
                Visible = false
            };


            CharacterButton = new MirButton
            {
                Index = 500,
                Library = Libraries.Title,
                Location = new Point(8, 70),
                Parent = this,
                PressedIndex = 500,
                Size = new Size(64, 20),
                Sound = SoundList.ButtonA,
            };
            CharacterButton.Click += (o, e) => ShowCharacterPage();
            StatusButton = new MirButton
            {
                Library = Libraries.Title,
                Location = new Point(70, 70),
                Parent = this,
                PressedIndex = 501,
                Size = new Size(64, 20),
                Sound = SoundList.ButtonA
            };
            StatusButton.Click += (o, e) => ShowStatusPage();

            StateButton = new MirButton
            {
                Library = Libraries.Title,
                Location = new Point(132, 70),
                Parent = this,
                PressedIndex = 502,
                Size = new Size(64, 20),
                Sound = SoundList.ButtonA
            };
            StateButton.Click += (o, e) => ShowStatePage();

            SkillButton = new MirButton
            {
                Library = Libraries.Title,
                Location = new Point(194, 70),
                Parent = this,
                PressedIndex = 503,
                Size = new Size(64, 20),
                Sound = SoundList.ButtonA
            };
            SkillButton.Click += (o, e) => ShowSkillPage();

            CloseButton = new MirButton
            {
                HoverIndex = 361,
                Index = 360,
                Location = new Point(241, 3),
                Library = Libraries.Prguse2,
                Parent = this,
                PressedIndex = 362,
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            NameLabel = new MirLabel
            {
                DrawFormat = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter,
                Parent = this,
                Location = new Point(0, 12),
                Size = new Size(264, 20),
                NotControl = true,
            };
            GuildLabel = new MirLabel
            {
                DrawFormat = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter,
                Parent = this,
                Location = new Point(0, 33),
                Size = new Size(264, 30),
                NotControl = true,
            };
            ClassImage = new MirImageControl
            {
                Index = 100,
                Library = Libraries.Prguse,
                Location = new Point(15, 33),
                Parent = this,
                NotControl = true,
            };

            Grid = new MirItemCell[Enum.GetNames(typeof(EquipmentSlot)).Length];

            Grid[(int)EquipmentSlot.武器] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.武器,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(125, 8), //(123, 7)
            };


            Grid[(int)EquipmentSlot.盔甲] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.盔甲,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(165, 8), //(163, 7)
            };


            Grid[(int)EquipmentSlot.头盔] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.头盔,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(205, 8), //(203, 7)
            };



            Grid[(int)EquipmentSlot.照明物] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.照明物,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(205, 135), //203, 134
            };


            Grid[(int)EquipmentSlot.项链] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.项链,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(205, 99), //203, 98
            };


            Grid[(int)EquipmentSlot.左手镯] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.左手镯,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(10, 170), //(8, 170)
            };

            Grid[(int)EquipmentSlot.右手镯] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.右手镯,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(205, 171), //(203, 170)
            };

            Grid[(int)EquipmentSlot.左戒指] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.左戒指,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(10, 207), //(8, 206)
            };

            Grid[(int)EquipmentSlot.右戒指] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.右戒指,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(205, 207), //(203, 206)
            };


            Grid[(int)EquipmentSlot.护身符] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.护身符,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(10, 242), //(8, 242)
            };


            Grid[(int)EquipmentSlot.靴子] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.靴子,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(50, 242), //(48, 242)
            };

            Grid[(int)EquipmentSlot.腰带] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.腰带,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(90, 242), //(88, 242)
            };


            Grid[(int)EquipmentSlot.守护石] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.守护石,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(129, 242), //(130, 242)
            };

            Grid[(int)EquipmentSlot.坐骑] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.坐骑,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(204, 63), //(205, 63)
            };

            // STATS I
            HealthLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 20),
                NotControl = true,
                Text = "0-0",
            };

            ManaLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 38),
                NotControl = true,
                Text = "0-0",
            };

            ACLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 56),
                NotControl = true,
                Text = "0-0",
            };

            MACLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 74),
                NotControl = true,
                Text = "0-0",
            };
            DCLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 92),
                NotControl = true,
                Text = "0-0"
            };
            MCLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 110),
                NotControl = true,
                Text = "0/0"
            };
            SCLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 128),
                NotControl = true,
                Text = "0/0"
            };
            //Breezer - New Labels
            CritRLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 146),
                NotControl = true
            };
            CritDLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 164),
                NotControl = true
            };
            AttkSpdLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 182),
                NotControl = true
            };
            AccLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 200),
                NotControl = true
            };
            AgilLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 218),
                NotControl = true
            };
            LuckLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 236),
                NotControl = true
            };
            // STATS II 
            ExpPLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 20),
                NotControl = true,
                Text = "0-0",
            };

            BagWLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 38),
                NotControl = true,
                Text = "0-0",
            };

            WearWLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 56),
                NotControl = true,
                Text = "0-0",
            };

            HandWLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 74),
                NotControl = true,
                Text = "0-0",
            };
            MagicRLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 92),
                NotControl = true,
                Text = "0-0"
            };
            PoisonResLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 110),
                NotControl = true,
                Text = "0/0"
            };
            HealthRLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 128),
                NotControl = true,
                Text = "0/0"
            };
            //Breezer
            ManaRLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 146),
                NotControl = true
            };
            PoisonRecLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 164),
                NotControl = true
            };
            HolyTLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 182),
                NotControl = true
            };
            FreezeLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 200),
                NotControl = true
            };
            PoisonAtkLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 218),
                NotControl = true
            };
            ReflectAtkLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 236),
                NotControl = true
            };
            HPDrainRatePercentLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 254),
                NotControl = true
            };

            Magics = new MagicButton[7];

            for (int i = 0; i < Magics.Length; i++)
                Magics[i] = new MagicButton 
                { 
                    Parent = SkillPage, 
                    Visible = false, 
                    Location = new Point(8, 8 + i * 33),
                    HeroMagic = gridType == MirGridType.HeroEquipment
                };

            NextButton = new MirButton
            {
                Index = 396,
                Location = new Point(140, 250),
                Library = Libraries.Prguse,
                Parent = SkillPage,
                PressedIndex = 397,
                Sound = SoundList.ButtonA,
            };
            NextButton.Click += (o, e) =>
            {
                if (StartIndex + 7 >= actor.Magics.Count) return;

                StartIndex += 7;
                RefreshInterface();
            };

            BackButton = new MirButton
            {
                Index = 398,
                Location = new Point(90, 250),
                Library = Libraries.Prguse,
                Parent = SkillPage,
                PressedIndex = 399,
                Sound = SoundList.ButtonA,
            };
            BackButton.Click += (o, e) =>
            {
                if (StartIndex - 7 < 0) return;

                StartIndex -= 7;
                RefreshInterface();
            };
        }

        public override void Show()
        {
            if (Visible) return;
            Visible = true;
        }

        public override void Hide()
        {
            GameScene.Scene.SocketDialog.Hide();
            base.Hide();
        }

        public void ShowCharacterPage()
        {
            CharacterPage.Visible = true;
            StatusPage.Visible = false;
            StatePage.Visible = false;
            SkillPage.Visible = false;
            CharacterButton.Index = 500;
            StatusButton.Index = -1;
            StateButton.Index = -1;
            SkillButton.Index = -1;
        }

        private void ShowStatusPage()
        {
            CharacterPage.Visible = false;
            StatusPage.Visible = true;
            StatePage.Visible = false;
            SkillPage.Visible = false;
            CharacterButton.Index = -1;
            StatusButton.Index = 501;
            StateButton.Index = -1;
            SkillButton.Index = -1;
        }

        private void ShowStatePage()
        {
            CharacterPage.Visible = false;
            StatusPage.Visible = false;
            StatePage.Visible = true;
            SkillPage.Visible = false;
            CharacterButton.Index = -1;
            StatusButton.Index = -1;
            StateButton.Index = 502;
            SkillButton.Index = -1;
        }

        public void ShowSkillPage()
        {
            CharacterPage.Visible = false;
            StatusPage.Visible = false;
            StatePage.Visible = false;
            SkillPage.Visible = true;
            CharacterButton.Index = -1;
            StatusButton.Index = -1;
            StateButton.Index = -1;
            SkillButton.Index = 503;
            StartIndex = 0;
        }

        private void RefreshInterface()
        {
            int offSet = Actor.Gender == MirGender.男性 ? 0 : 1;

            Index = GridType == MirGridType.HeroEquipment ? 505 : 504;

            //★登仙内观底板（11-09）：Prguse 340/341 的底图里**烘焙着一个原版裸体**，
            //  羽化角色再往上画羽化素体，就成了「原版裸体 + 羽化素体」两个人叠在一起。
            //  378/379 是同一张面板的「无裸体」版本（装备格位置与原图逐像素一致），
            //  所以羽化时换 378(男)/379(女)；未羽化仍用 340(男)/341(女)——
            //  顺便修掉了原来写死 340、女角色内观也画男裸体的老毛病。
            //  ★必须在 BeforeDraw 里设置：控件先画自己的图再触发 AfterDraw，而且本方法每帧重置 Index。
            CharacterPage.Index = Actor.HumUpStage > 0
                ? (Actor.Gender == MirGender.男性 ? 378 : 379)
                : 340 + offSet;

            switch (Actor.Class)
            {
                case MirClass.战士:
                    ClassImage.Index = 100;// + offSet * 5;
                    break;
                case MirClass.法师:
                    ClassImage.Index = 101;// + offSet * 5;
                    break;
                case MirClass.道士:
                    ClassImage.Index = 102;// + offSet * 5;
                    break;
                case MirClass.刺客:
                    ClassImage.Index = 103;// + offSet * 5;
                    break;
                case MirClass.弓箭:
                    ClassImage.Index = 104;// + offSet * 5;
                    break;
            }

            NameLabel.Text = Actor.Name;
            GuildLabel.Text = Actor.GuildName + " " + Actor.GuildRankName;

            for (int i = 0; i < Magics.Length; i++)
            {
                if (i + StartIndex >= Actor.Magics.Count)
                {
                    Magics[i].Visible = false;
                    continue;
                }

                Magics[i].Visible = true;
                Magics[i].Update(Actor.Magics[i + StartIndex]);
            }
        }

        public MirItemCell GetCell(ulong id)
        {

            for (int i = 0; i < Grid.Length; i++)
            {
                if (Grid[i].Item == null || Grid[i].Item.UniqueID != id) continue;
                return Grid[i];
            }
            return null;
        }

        /// <summary>
        /// 登仙内观素体：按职业/性别返回 Prguse.Lib 素体帧号，以及相对内观统一脚底锚点的站位补偿。
        /// 帧号：320/321 = 弓手男/女，330/331 = 战士，332/333 = 法师，334/335 = 道士，336/337 = 刺客。
        /// 补偿值是按「与普通盔甲立绘脚底(138,324)/女(139,321)对齐」实测得出，各帧在库里的烘焙偏移不同，故逐个微调。
        /// </summary>
        public static void GetHumUpBody(MirClass cls, MirGender gender, out int index, out int offX, out int offY)
        {
            bool male = gender == MirGender.男性;

            switch (cls)
            {
                case MirClass.战士: index = male ? 330 : 331; break;
                case MirClass.法师: index = male ? 332 : 333; break;
                case MirClass.道士: index = male ? 334 : 335; break;
                case MirClass.刺客: index = male ? 336 : 337; break;
                default: index = male ? 320 : 321; break;   // 弓箭
            }

            switch (index)
            {
                case 320: offX = 2; offY = 7; break;
                case 321: offX = 1; offY = 8; break;
                case 330: offX = 0; offY = 7; break;
                case 331: offX = 6; offY = 9; break;
                case 332: offX = 9; offY = 7; break;
                case 333: offX = 4; offY = 10; break;
                case 334: offX = 10; offY = 7; break;
                case 335: offX = 4; offY = 6; break;
                case 336: offX = 1; offY = 10; break;
                default: offX = 4; offY = 6; break;         // 337
            }
        }

        /// <summary>
        /// 登仙内观：头盔/发型层跟着羽化素体头部走的补偿量（11-09 追加，11-09 二次标定）。
        /// ★原因：素体是按「脚底对齐」放进内观的，它比原版立绘高 20 来像素，头部就比原版立绘的头顶高 14~17 像素；
        ///   而头盔/发型是按**原版立绘**的头部锚点画的（Prguse 441+/StateItem 的 frameXY），
        ///   所以羽化时这两层会掉到下巴/脖子上，必须整体跟着素体的头挪上去。
        /// ★X 标定法（Tools/HumUpHeadFit）：340/341 是「整块内观底板 + 烘焙裸体」，直接量会被顶部装备槽污染，
        ///   所以拿 378/379（同款无裸体底板）与它逐像素做差取出裸体掩码，只在中央身体列(80~165)里量
        ///   「头顶行 + 头顶以下 16 行的重心 x」；素体同口径量一遍，两者相减就是这里要的值。
        ///   11-09 首版 X 全部偏右 2~4px（用的是含右侧装备槽的掩码），本次按实测重算。
        /// 未羽化时**不要**调用（调用方用 HumUpStage > 0 判断），返回值不会影响原逻辑。
        /// </summary>
        public static void GetHumUpHeadOffset(MirClass cls, MirGender gender, out int offX, out int offY)
        {
            bool male = gender == MirGender.男性;

            if (male)
            {
                switch (cls)
                {
                    case MirClass.战士: offX = -6; offY = -17; break;
                    case MirClass.法师: offX = 3; offY = -17; break;
                    case MirClass.道士: offX = 4; offY = -17; break;
                    case MirClass.刺客: offX = -5; offY = -14; break;
                    default: offX = -4; offY = -17; break;    // 弓箭
                }
            }
            else
            {
                switch (cls)
                {
                    case MirClass.战士: offX = 4; offY = -8; break;
                    case MirClass.法师: offX = 1; offY = -7; break;
                    case MirClass.道士: offX = 2; offY = -11; break;
                    case MirClass.刺客: offX = 2; offY = -11; break;
                    default: offX = -1; offY = -9; break;     // 弓箭
                }
            }
        }

        /// <summary>
        /// 登仙内观·立绘表（11-10 四改）：羽化**穿盔甲**（盔甲层画分职业 StateItem 立绘）时
        /// 头盔/发型的补偿量。来源：Tools/HumUpHeadFit2 逐职业像素标定——
        /// 立绘头落点(肤色头顶行/脸心X) - 素体头落点(含 GetHumUpBody 补偿) = extra，
        /// 本表 = 素体表 GetHumUpHeadOffset + extra 合成，并经像素级验证
        /// （立绘上「物件掩码顶-头顶行」「物件重心-头心」与素体基准完全一致，误差 ≤1px）。
        /// 头盔与发型共用本表（锚点差烘焙在帧 XY 里）。
        /// common=true：战法道通用装备画 StateItem_Common_* 库（2450/2460），其头位置与
        /// 职业库有 1~2px 差，按 Common 库实测头落点另行合成。
        /// </summary>
        public static void GetHumUpPortraitHeadOffset(MirClass cls, MirGender gender, bool common, out int offX, out int offY)
        {
            bool male = gender == MirGender.男性;

            if (common)
            {
                if (male)
                {
                    switch (cls)
                    {
                        case MirClass.战士: offX = -5; offY = -25; break;
                        default: offX = -6; offY = -25; break;    // 法/道/刺/弓
                    }
                }
                else
                {
                    switch (cls)
                    {
                        case MirClass.刺客: offX = -1; offY = -17; break;
                        default: offX = -3; offY = -17; break;    // 战/法/道/弓
                    }
                }
                return;
            }

            if (male)
            {
                switch (cls)
                {
                    case MirClass.战士: offX = -8; offY = -24; break;
                    case MirClass.法师: offX = -6; offY = -26; break;   // 11-10 用户游戏实测：右2、上2
                    case MirClass.道士: offX = -6; offY = -24; break;
                    case MirClass.刺客: offX = -6; offY = -24; break;
                    default: offX = -6; offY = -24; break;    // 弓箭
                }
            }
            else
            {
                switch (cls)
                {
                    case MirClass.战士: offX = -2; offY = -17; break;
                    case MirClass.法师: offX = -3; offY = -17; break;
                    case MirClass.道士: offX = -2; offY = -17; break;
                    case MirClass.刺客: offX = 0; offY = -17; break;
                    default: offX = -2; offY = -17; break;    // 弓箭
                }
            }
        }

    }
}
