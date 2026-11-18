using System.IO;

public class GameLanguage
{
	public static string PetMode_Both = "[自由攻击]";

	public static string PetMode_MoveOnly = "[跟随模式]";

	public static string PetMode_AttackOnly = "[守护模式]";

	public static string PetMode_None = "[休息模式]";

	public static string PetMode_FocusMasterTarget = "[指定模式]";

	public static string AttackMode_Peace = "[和平模式]";

	public static string AttackMode_Group = "[组队模式]";

	public static string AttackMode_Guild = "[行会模式]";

	public static string AttackMode_EnemyGuild = "[行会战争]";

	public static string AttackMode_RedBrown = "[红名模式]";

	public static string AttackMode_All = "[全体模式]";

	public static string LogOutTip = "是否要返回到人物选择界面";

	public static string ExitTip = "是否要退出游戏";

	public static string DiedTip = "选择在安全区复活";

	public static string DropTip = "是否要丢弃 {0}?";

	public static string Inventory = "背包 ({0})";

	public static string Character = "属性 ({0})";

	public static string Skills = "技能 ({0})";

	public static string Quests = "任务 ({0})";

	public static string Options = "选项 ({0})";

	public static string Menu = "菜单";

	public static string GameShop = "商城 ({0})";

	public static string BigMap = "大地图 ({0})";

	public static string MiniMap = "小地图 ({0})";

	public static string DuraPanel = "装备持久度";

	public static string Mail = "邮件";

	public static string Exit = "退出 ({0})";

	public static string LogOut = "小退 ({0})";

	public static string Help = "帮助 ({0})";

	public static string Keybinds = "键盘 ({0})";

	public static string Ranking = "排名 ({0})";

	public static string Creatures = "宠物 ({0})";

	public static string Mount = "坐骑 ({0})";

	public static string Fishing = "钓鱼 ({0})";

	public static string Friends = "好友 ({0})";

	public static string Mentor = "师徒 ({0})";

	public static string Relationship = "夫妻 ({0})";

	public static string Groups = "组队 ({0})";

	public static string Guild = "行会 ({0})";

	public static string Expire = "失效期: {0}";

	public static string ExpireNever = "永不过期";

	public static string ExpirePaused = "有效期: 安全区暂停计时";

	public static string Never = "永不";

	public static string Trade = "交易 ({0})";

	public static string Size = "窗口调整";

	public static string ChatSettings = "聊天设置";

	public static string Rotate = "转动";

	public static string Close = "关闭 ({0})";

	public static string GameMaster = "管理员";

	public static string Report = "公告";

	public static string PatchErr = "无法获取补丁程序";

	public static string LastOnline = "上次连线";

	public static string Gold = "金币";

	public static string Credit = "信用币";

	public static string YouGained = "你获得了 {0}";

	public static string YouGained2 = "你获得了 {0:###,###,###} {1}";

	public static string ExperienceGained = "获得经验 {0}";

	public static string HeroInventory = "英雄背包 ({0})";

	public static string HeroCharacter = "英雄装备 ({0})";

	public static string HeroSkills = "英雄技能 ({0})";

	public static string HeroSummon = "英雄(召唤/收起)";

	public static string HeroExperienceGained = "英雄获得经验 {0}";

	public static string ItemDescription = "物品说明";

	public static string RequiredLevel = "需要等级: {0}";

	public static string RequiredDC = "需要物理攻击: {0}";

	public static string RequiredMC = "需要魔法攻击: {0}";

	public static string RequiredSC = "需要道术攻击: {0}";

	public static string ClassRequired = "需要职业: {0}";

	public static string Holy = "神圣: + {0} (+{1})";

	public static string Holy2 = "神圣: + {0}";

	public static string Accuracy = "准确: + {0} (+{1})";

	public static string Accuracy2 = "准确: + {0}";

	public static string Agility = "敏捷: + {0} (+{1})";

	public static string Agility2 = "敏捷: + {0}";

	public static string DC = "物理攻击 + {0}~{1} (+{2})";

	public static string DC2 = "物理攻击 + {0}~{1}";

	public static string MC = "魔法攻击 + {0}~{1} (+{2})";

	public static string MC2 = "魔法攻击 + {0}~{1}";

	public static string SC = "道术攻击 + {0}~{1} (+{2})";

	public static string SC2 = "道术攻击 + {0}~{1}";

	public static string Durability = "持久度:";

	public static string Weight = "重量:";

	public static string AC = "物理防御 + {0}~{1} (+{2})";

	public static string AC2 = "物理防御 + {0}~{1}";

	public static string MAC = "魔法防御 + {0}~{1} (+{2})";

	public static string MAC2 = "魔法防御 + {0}~{1}";

	public static string Luck = "幸运 + {0}";

	public static string DeleteCharacter = "确定要删除角色 {0}";

	public static string CharacterDeleted = "角色删除成功";

	public static string CharacterCreated = "角色创建成功";

	public static string Resolution = "显示设置";

	public static string Autostart = "自动登录";

	public static string Usrname = "账号名";

	public static string Password = "密码";

	public static string ShuttingDown = "断开连接：服务器正在关闭";

	public static string MaxCombine = "叠加数量 {0}{1}按Shift+鼠标左键拆分";

	public static string Count = " 计数 {0}";

	public static string ExtraSlots8 = "你确定用1000000金币购买8个额外背包空间继续购买你可以解锁4个背包空间，最购买40个空间";

	public static string ExtraSlots4 = "确定要解锁4个额外的背包吗? 费用: {0:###,###}";

	public static string Chat_All = "全部";

	public static string Chat_Short = "喊话";

	public static string Chat_Whisper = "私聊";

	public static string Chat_Lover = "夫妻";

	public static string Chat_Mentor = "师徒";

	public static string Chat_Group = "组队";

	public static string Chat_Guild = "公会";

	public static string ExpandedStorageLocked = "扩展仓库锁定";

	public static string ExtraStorage = "你想租用10天的扩展仓库？ 1,000,000 金币";

	public static string ExtendYourRentalPeriod = "你愿意以1000000金币延长10天租期？";

	public static string CannotLeaveGame = "游戏退出倒计时 {0} 秒";

	public static string SelectKey = "快捷键 {0}";

	public static string WeaponSpiritFire = "你的武器已经被火精灵灼热";

	public static string SpiritsFireDisappeared = "火精灵消失";

	public static string WeddingRing = "结婚戒指";

	public static string ItemTextFormat = "{0}{1}{2} {3}";

	public static string DropAmount = "丢弃数量";

	public static string LowMana = "法力值不足";

	public static string NoCreatures = "没有宠物";

	public static string NoMount = "没有坐骑";

	public static string NoFishingRod = "没有鱼竿";

	public static string AttemptingConnect = "正在连接到服务器{0}连接次数:{1}";

	public static string CreatingCharactersDisabled = "服务器当前禁止创建新账户";

	public static string InvalidCharacterName = "角色名不可用";

	public static string NoClass = "职业未开放，请联系管理员";

	public static string ToManyCharacters = "不能再创建角色 {0}";

	public static string CharacterNameExists = "角色名已存在";

	public static string WarriorsDes = "以强有力的体格为基础，特殊之处在于用剑法及刀法等技术。即便穿戴沉重的武器及对打猎、战斗比较适用。 体力强的战士能带许多东西，铠甲也可以自由活动。 但战士所戴的铠甲对魔法的防御能力相对较弱。";

	public static string WizardDes = "以长时间锻炼的内功为基础，能发挥强大的攻击型魔法。魔法攻击力卓越，但体力较弱。对体力上直接受到攻击的防御能力较低，另外，发挥高水平的魔法时需要较长时间，此时可能受到对方的快速攻击。 魔法师的魔法比任何攻击能力都强大，能有效的威胁对方。";

	public static string TaoistDes = "以强大的精神力作为基础，可以使用治疗术帮助别人。 对自然很熟悉，在用毒方面的能力最强。 博学多知，能使用剑术和魔法，所以每时每刻都能发挥多样的法术，随机应变性强。";

	public static string AssassinDes = "以敏捷快速的攻击为基础，矫健的刺客还拥有超强的爆发性他们熟悉各种技能 尤其擅长瞬移、潜行技能！ 他们是暗夜的主人，是绝对的伤害高、攻击高、爆发型的职业。";

	public static string ArcherDes = "强大的远程输出：作为一个名副其实的远程物理输出职业，弓箭最擅长在敌人攻击范围之外对敌人造成致命打击。 多变：弓箭手永远是战场上的未知数，就必须练就准确的判断力，熟练掌握其操作技巧 华丽：鲜艳的服装、优雅的射击动作和绚美的特效，非弓箭手莫属!";

	public static string DateSent = "发送日期: {0}";

	public static string Send = "发送";

	public static string Reply = "回复";

	public static string Read = "阅读";

	public static string Delete = "删除";

	public static string BlockList = "阻止列表";

	public static string EnterMailToName = "输入要邮寄的人的姓名";

	public static string AddFriend = "添加好友";

	public static string RemoveFriend = "删除好友";

	public static string FriendMemo = "好友名单";

	public static string FriendMail = "好友邮箱";

	public static string FriendWhisper = "私聊好友";

	public static string FriendEnterAddName = "输入要添加好友名字";

	public static string FriendEnterBlockName = "输入要屏蔽的好友名字";

	public static string AddMentor = "拜师";

	public static string RemoveMentorMentee = "解除师徒关系";

	public static string MentorRequests = "允许/拒绝拜师";

	public static string MentorEnterName = "输入想拜师的名字";

	public static string RestedBuff = "精力充沛{0}增加经验值{1}%{2}";

	public static string ItemTypeNothing = "物品";

	public static string ItemTypeWeapon = "武器";

	public static string ItemTypeArmour = "盔甲";

	public static string ItemTypeHelmet = "头盔";

	public static string ItemTypeNecklace = "项链";

	public static string ItemTypeBracelet = "手镯";

	public static string ItemTypeRing = "戒指";

	public static string ItemTypeAmulet = "护身符";

	public static string ItemTypeBelt = "腰带";

	public static string ItemTypeBoots = "靴子";

	public static string ItemTypeStone = "守护石";

	public static string ItemTypeTorch = "照明物";

	public static string ItemTypePotion = "药水";

	public static string ItemTypeOre = "矿石";

	public static string ItemTypeMeat = "肉类";

	public static string ItemTypeCraftingMaterial = "工艺材料";

	public static string ItemTypeScroll = "卷轴";

	public static string ItemTypeGem = "宝玉神珠";

	public static string ItemTypeMount = "坐骑";

	public static string ItemTypeBook = "技能书";

	public static string ItemTypeScript = "特殊消耗品";

	public static string ItemTypeReins = "缰绳";

	public static string ItemTypeBells = "铃铛";

	public static string ItemTypeSaddle = "马鞍";

	public static string ItemTypeRibbon = "蝴蝶结";

	public static string ItemTypeMask = "面甲";

	public static string ItemTypeFood = "坐骑食物";

	public static string ItemTypeHook = "鱼钩";

	public static string ItemTypeFloat = "鱼漂";

	public static string ItemTypeBait = "鱼饵";

	public static string ItemTypeFinder = "探鱼器";

	public static string ItemTypeReel = "摇轮";

	public static string ItemTypeFish = "鱼";

	public static string ItemTypeQuest = "任务";

	public static string ItemTypeAwakening = "觉醒";

	public static string ItemTypePets = "宠物";

	public static string ItemTypeTransform = "外形变幻";

	public static string ItemTypeDeco = "装饰";

	public static string ItemTypeMonsterSpawn = "怪物蛋";

	public static string ItemTypeSealedHero = "封印英雄";

	public static string ItemGradeCommon = "普通";

	public static string ItemGradeRare = "宝物";

	public static string ItemGradeLegendary = "圣物";

	public static string ItemGradeMythical = "神物";

	public static string ItemGradeHeroic = "英雄";

	public static string NoAccountID = "帐户不存在";

	public static string IncorrectPasswordAccountID = "帐户密码不正确";

	public static string GroupSwitch = "允许/禁止组队";

	public static string GroupAdd = "组队添加";

	public static string GroupRemove = "组队删除";

	public static string GroupAddEnterName = "输入要组队玩家的名称";

	public static string GroupRemoveEnterName = "输入要退组玩家的名称";

	public static string TooHeavyToHold = "腕力不足";

	public static string SwitchMarriage = "允许/禁止结婚";

	public static string RequestMarriage = "结婚请求";

	public static string RequestDivorce = "离婚请求";

	public static string MailLover = "夫妻邮件";

	public static string WhisperLover = "夫妻私聊";

	public static string Welcome = "欢迎来到 {0} 服务器";

	public static string OnlinePlayers = "在线玩家 {0}";

	public static string WeaponLuck = "武器获得幸运";

	public static string WeaponCurse = "武器受到诅咒";

	public static string WeaponNoEffect = "无效";

	public static string InventoryIncreased = "背包容量增加";

	public static string FaceToTrade = "必须面对玩家交易";

	public static string NoTownTeleport = "回城禁用";

	public static string CanNotRandom = "随机禁用";

	public static string CanNotDungeon = "地牢禁用";

	public static string CannotResurrection = "当前状态下复活禁用";

	public static string CanNotDrop = "丢弃禁止";

	public static string NewMail = "新邮件到达";

	public static string CouldNotFindPlayer = "未发现玩家{0}";

	public static string BeenPoisoned = "你中毒了";

	public static string AllowingMentorRequests = "拜师请求被认可";

	public static string BlockingMentorRequests = "拜师请求被拒绝";

	public static string LowLevel = "等级不足";

	public static string LowGold = "金币不足";

	public static string LevelUp = "升级！！！";

	public static string HeroLevelUp = "英雄等级提升！";

	public static string LowDC = "物理攻击不足";

	public static string LowMC = "魔法攻击不足";

	public static string LowSC = "道术攻击不足";

	public static string GameName = "Legend of Mir 2";

	public static string ExpandedStorageExpiresOn = "扩展仓库过期";

	public static string NotFemale = "非女性";

	public static string NotMale = "非男性";

	public static string NotInGuild = "未加入公会";

	public static string NoMentorship = "未有可解除的师徒关系";

	public static string NoBagSpace = "背包空间不足";

	public static void LoadClientLanguage(string languageIniPath)
	{
		if (!File.Exists(languageIniPath))
		{
			SaveClientLanguage(languageIniPath);
			return;
		}
		InIReader inIReader = new InIReader(languageIniPath);
		PetMode_Both = inIReader.ReadString("Language", "PetMode_Both", PetMode_Both);
		PetMode_MoveOnly = inIReader.ReadString("Language", "PetMode_MoveOnly", PetMode_MoveOnly);
		PetMode_AttackOnly = inIReader.ReadString("Language", "PetMode_AttackOnly", PetMode_AttackOnly);
		PetMode_None = inIReader.ReadString("Language", "PetMode_None", PetMode_None);
		PetMode_FocusMasterTarget = inIReader.ReadString("Language", "PetMode_FocusMasterTarget", PetMode_FocusMasterTarget);
		AttackMode_Peace = inIReader.ReadString("Language", "AttackMode_Peace", AttackMode_Peace);
		AttackMode_Group = inIReader.ReadString("Language", "AttackMode_Group", AttackMode_Group);
		AttackMode_Guild = inIReader.ReadString("Language", "AttackMode_Guild", AttackMode_Guild);
		AttackMode_EnemyGuild = inIReader.ReadString("Language", "AttackMode_EnemyGuild", AttackMode_EnemyGuild);
		AttackMode_RedBrown = inIReader.ReadString("Language", "AttackMode_RedBrown", AttackMode_RedBrown);
		AttackMode_All = inIReader.ReadString("Language", "AttackMode_All", AttackMode_All);
		LogOutTip = inIReader.ReadString("Language", "LogOutTip", LogOutTip);
		ExitTip = inIReader.ReadString("Language", "ExitTip", ExitTip);
		DiedTip = inIReader.ReadString("Language", "DiedTip", DiedTip);
		DropTip = inIReader.ReadString("Language", "DropTip", DropTip);
		Inventory = inIReader.ReadString("Language", "Inventory", Inventory);
		Character = inIReader.ReadString("Language", "Character", Character);
		Skills = inIReader.ReadString("Language", "Skills", Skills);
		Quests = inIReader.ReadString("Language", "Quests", Quests);
		Options = inIReader.ReadString("Language", "Options", Options);
		Menu = inIReader.ReadString("Language", "Menu", Menu);
		GameShop = inIReader.ReadString("Language", "GameShop", GameShop);
		BigMap = inIReader.ReadString("Language", "BigMap", BigMap);
		MiniMap = inIReader.ReadString("Language", "MiniMap", MiniMap);
		DuraPanel = inIReader.ReadString("Language", "DuraPanel", DuraPanel);
		Mail = inIReader.ReadString("Language", "Mail", Mail);
		Exit = inIReader.ReadString("Language", "Exit", Exit);
		LogOut = inIReader.ReadString("Language", "LogOut", LogOut);
		Help = inIReader.ReadString("Language", "Help", Help);
		Keybinds = inIReader.ReadString("Language", "Keybinds", Keybinds);
		Ranking = inIReader.ReadString("Language", "Ranking", Ranking);
		Creatures = inIReader.ReadString("Language", "Creatures", Creatures);
		Mount = inIReader.ReadString("Language", "Mount", Mount);
		Fishing = inIReader.ReadString("Language", "Fishing", Fishing);
		Friends = inIReader.ReadString("Language", "Friends", Friends);
		Mentor = inIReader.ReadString("Language", "Mentor", Mentor);
		Relationship = inIReader.ReadString("Language", "Relationship", Relationship);
		Groups = inIReader.ReadString("Language", "Groups", Groups);
		Guild = inIReader.ReadString("Language", "Guild", Guild);
		Trade = inIReader.ReadString("Language", "Trade", Trade);
		Size = inIReader.ReadString("Language", "Size", Size);
		ChatSettings = inIReader.ReadString("Language", "ChatSettings", ChatSettings);
		Rotate = inIReader.ReadString("Language", "Rotate", Rotate);
		Close = inIReader.ReadString("Language", "Close", Close);
		GameMaster = inIReader.ReadString("Language", "GameMaster", GameMaster);
		Report = inIReader.ReadString("Language", "Report", Report);
		Expire = inIReader.ReadString("Language", "Expire", Expire);
		ExpireNever = inIReader.ReadString("Language", "ExpireNever", ExpireNever);
		ExpirePaused = inIReader.ReadString("Language", "ExpirePaused", ExpirePaused);
		Never = inIReader.ReadString("Language", "Never", Never);
		PatchErr = inIReader.ReadString("Language", "PatchErr", PatchErr);
		LastOnline = inIReader.ReadString("Language", "LastOnline", LastOnline);
		LowLevel = inIReader.ReadString("Language", "LowLevel", LowLevel);
		LowGold = inIReader.ReadString("Language", "LowGold", LowGold);
		LowDC = inIReader.ReadString("Language", "LowDC", LowDC);
		LowMC = inIReader.ReadString("Language", "LowMC", LowMC);
		LowSC = inIReader.ReadString("Language", "LowSC", LowSC);
		Gold = inIReader.ReadString("Language", "Gold", Gold);
		Credit = inIReader.ReadString("Language", "Credit", Credit);
		YouGained = inIReader.ReadString("Language", "YouGained", YouGained);
		YouGained2 = inIReader.ReadString("Language", "YouGained2", YouGained2);
		ExperienceGained = inIReader.ReadString("Language", "ExperienceGained", ExperienceGained);
		LevelUp = inIReader.ReadString("Language", "LevelUp", LevelUp);
		HeroLevelUp = inIReader.ReadString("Language", "HeroLevelUp", HeroLevelUp);
		HeroInventory = inIReader.ReadString("Language", "HeroInventory", HeroInventory);
		HeroCharacter = inIReader.ReadString("Language", "HeroCharacter", HeroCharacter);
		HeroSkills = inIReader.ReadString("Language", "HeroSkills", HeroSkills);
		HeroSummon = inIReader.ReadString("Language", "HeroSummon", HeroSummon);
		HeroExperienceGained = inIReader.ReadString("Language", "HeroExperienceGained", HeroExperienceGained);
		ItemDescription = inIReader.ReadString("Language", "ItemDescription", ItemDescription);
		RequiredLevel = inIReader.ReadString("Language", "RequiredLevel", RequiredLevel);
		RequiredDC = inIReader.ReadString("Language", "RequiredDC", RequiredDC);
		RequiredMC = inIReader.ReadString("Language", "RequiredMC", RequiredMC);
		RequiredSC = inIReader.ReadString("Language", "RequiredSC", RequiredSC);
		ClassRequired = inIReader.ReadString("Language", "ClassRequired", ClassRequired);
		Holy = inIReader.ReadString("Language", "Holy", Holy);
		Holy2 = inIReader.ReadString("Language", "Holy2", Holy2);
		Accuracy = inIReader.ReadString("Language", "Accuracy", Accuracy);
		Accuracy2 = inIReader.ReadString("Language", "Accuracy2", Accuracy2);
		Agility = inIReader.ReadString("Language", "Agility", Agility);
		Agility2 = inIReader.ReadString("Language", "Agility2", Agility2);
		DC = inIReader.ReadString("Language", "DC", DC);
		DC2 = inIReader.ReadString("Language", "DC2", DC2);
		MC = inIReader.ReadString("Language", "MC", MC);
		MC2 = inIReader.ReadString("Language", "MC2", MC2);
		SC = inIReader.ReadString("Language", "SC", SC);
		SC2 = inIReader.ReadString("Language", "SC2", SC2);
		Durability = inIReader.ReadString("Language", "Durability", Durability);
		Weight = inIReader.ReadString("Language", "Weight", Weight);
		AC = inIReader.ReadString("Language", "AC", AC);
		AC2 = inIReader.ReadString("Language", "AC2", AC2);
		MAC = inIReader.ReadString("Language", "MAC", MAC);
		MAC2 = inIReader.ReadString("Language", "MAC2", MAC2);
		Luck = inIReader.ReadString("Language", "Luck", Luck);
		DeleteCharacter = inIReader.ReadString("Language", "DeleteCharacter", DeleteCharacter);
		CharacterDeleted = inIReader.ReadString("Language", "CharacterDeleted", CharacterDeleted);
		CharacterCreated = inIReader.ReadString("Language", "CharacterCreated", CharacterCreated);
		Resolution = inIReader.ReadString("Language", "Resolution", Resolution);
		Autostart = inIReader.ReadString("Language", "Autostart", Autostart);
		Usrname = inIReader.ReadString("Language", "Usrname", Usrname);
		Password = inIReader.ReadString("Language", "Password", Password);
		ShuttingDown = inIReader.ReadString("Language", "ShuttingDown", ShuttingDown);
		MaxCombine = inIReader.ReadString("Language", "MaxCombine", MaxCombine);
		Count = inIReader.ReadString("Language", "Count", Count);
		ExtraSlots8 = inIReader.ReadString("Language", "ExtraSlots8", ExtraSlots8);
		ExtraSlots4 = inIReader.ReadString("Language", "ExtraSlots4", ExtraSlots4);
		Chat_All = inIReader.ReadString("Language", "Chat_All", Chat_All);
		Chat_Short = inIReader.ReadString("Language", "Chat_Short", Chat_Short);
		Chat_Whisper = inIReader.ReadString("Language", "Chat_Whisper", Chat_Whisper);
		Chat_Lover = inIReader.ReadString("Language", "Chat_Lover", Chat_Lover);
		Chat_Mentor = inIReader.ReadString("Language", "Chat_Mentor", Chat_Mentor);
		Chat_Group = inIReader.ReadString("Language", "Chat_Group", Chat_Group);
		Chat_Guild = inIReader.ReadString("Language", "Chat_Guild", Chat_Guild);
		ExpandedStorageLocked = inIReader.ReadString("Language", "ExpandedStorageLocked", ExpandedStorageLocked);
		ExtraStorage = inIReader.ReadString("Language", "ExtraStorage", ExtraStorage);
		ExtendYourRentalPeriod = inIReader.ReadString("Language", "ExtendYourRentalPeriod", ExtendYourRentalPeriod);
		ExpandedStorageExpiresOn = inIReader.ReadString("Language", "ExpandedStorageExpiresOn", ExpandedStorageExpiresOn);
		GameName = inIReader.ReadString("Language", "GameName", GameName);
		CannotLeaveGame = inIReader.ReadString("Language", "CannotLeaveGame", CannotLeaveGame);
		SelectKey = inIReader.ReadString("Language", "SelectKey", SelectKey);
		WeaponSpiritFire = inIReader.ReadString("Language", "WeaponSpiritFire", WeaponSpiritFire);
		SpiritsFireDisappeared = inIReader.ReadString("Language", "SpiritsFireDisappeared", SpiritsFireDisappeared);
		WeddingRing = inIReader.ReadString("Language", "WeddingRing", WeddingRing);
		ItemTextFormat = inIReader.ReadString("Language", "ItemTextFormat", ItemTextFormat);
		DropAmount = inIReader.ReadString("Language", "DropAmount", DropAmount);
		LowMana = inIReader.ReadString("Language", "LowMana", LowMana);
		NotFemale = inIReader.ReadString("Language", "NotFemale", NotFemale);
		NotMale = inIReader.ReadString("Language", "NotMale", NotMale);
		NoCreatures = inIReader.ReadString("Language", "NoCreatures", NoCreatures);
		NoMount = inIReader.ReadString("Language", "NoMount", NoMount);
		NoFishingRod = inIReader.ReadString("Language", "NoFishingRod", NoFishingRod);
		NotInGuild = inIReader.ReadString("Language", "NotInGuild", NotInGuild);
		NoBagSpace = inIReader.ReadString("Language", "NoBagSpace", NoBagSpace);
		AttemptingConnect = inIReader.ReadString("Language", "AttemptingConnect", AttemptingConnect);
		CreatingCharactersDisabled = inIReader.ReadString("Language", "CreatingCharactersDisabled", CreatingCharactersDisabled);
		InvalidCharacterName = inIReader.ReadString("Language", "InvalidCharacterName", InvalidCharacterName);
		NoClass = inIReader.ReadString("Language", "NoClass", NoClass);
		ToManyCharacters = inIReader.ReadString("Language", "ToManyCharacters", ToManyCharacters);
		CharacterNameExists = inIReader.ReadString("Language", "CharacterNameExists", CharacterNameExists);
		WarriorsDes = inIReader.ReadString("Language", "WarriorsDes", WarriorsDes);
		WizardDes = inIReader.ReadString("Language", "WizardDes", WizardDes);
		TaoistDes = inIReader.ReadString("Language", "TaoistDes", TaoistDes);
		AssassinDes = inIReader.ReadString("Language", "AssassinDes", AssassinDes);
		ArcherDes = inIReader.ReadString("Language", "ArcherDes", ArcherDes);
		DateSent = inIReader.ReadString("Language", "DateSent", DateSent);
		Send = inIReader.ReadString("Language", "Send", Send);
		Reply = inIReader.ReadString("Language", "Reply", Reply);
		Read = inIReader.ReadString("Language", "Read", Read);
		Delete = inIReader.ReadString("Language", "Delete", Delete);
		BlockList = inIReader.ReadString("Language", "BlockList", BlockList);
		EnterMailToName = inIReader.ReadString("Language", "EnterMailToName", EnterMailToName);
		BeenPoisoned = inIReader.ReadString("Language", "BeenPoisoned", BeenPoisoned);
		AddFriend = inIReader.ReadString("Language", "AddFriend", AddFriend);
		RemoveFriend = inIReader.ReadString("Language", "RemoveFriend", RemoveFriend);
		FriendMemo = inIReader.ReadString("Language", "FriendMemo", FriendMemo);
		FriendMail = inIReader.ReadString("Language", "FriendMail", FriendMail);
		FriendWhisper = inIReader.ReadString("Language", "FriendWhisper", FriendWhisper);
		FriendEnterAddName = inIReader.ReadString("Language", "FriendEnterAddName", FriendEnterAddName);
		FriendEnterBlockName = inIReader.ReadString("Language", "FriendEnterBlockName", FriendEnterBlockName);
		AddMentor = inIReader.ReadString("Language", "AddMentor", AddMentor);
		RemoveMentorMentee = inIReader.ReadString("Language", "RemoveMentorMentee", RemoveMentorMentee);
		MentorRequests = inIReader.ReadString("Language", "MentorRequests", MentorRequests);
		MentorEnterName = inIReader.ReadString("Language", "MentorEnterName", MentorEnterName);
		NoMentorship = inIReader.ReadString("Language", "NoMentorship", NoMentorship);
		RestedBuff = inIReader.ReadString("Language", "RestedBuff", RestedBuff);
		ItemTypeNothing = inIReader.ReadString("Language", "ItemTypeNothing", ItemTypeNothing);
		ItemTypeWeapon = inIReader.ReadString("Language", "ItemTypeWeapon", ItemTypeWeapon);
		ItemTypeArmour = inIReader.ReadString("Language", "ItemTypeArmour", ItemTypeArmour);
		ItemTypeHelmet = inIReader.ReadString("Language", "ItemTypeHelmet", ItemTypeHelmet);
		ItemTypeNecklace = inIReader.ReadString("Language", "ItemTypeNecklace", ItemTypeNecklace);
		ItemTypeBracelet = inIReader.ReadString("Language", "ItemTypeBracelet", ItemTypeBracelet);
		ItemTypeRing = inIReader.ReadString("Language", "ItemTypeRing", ItemTypeRing);
		ItemTypeAmulet = inIReader.ReadString("Language", "ItemTypeAmulet", ItemTypeAmulet);
		ItemTypeBelt = inIReader.ReadString("Language", "ItemTypeBelt", ItemTypeBelt);
		ItemTypeBoots = inIReader.ReadString("Language", "ItemTypeBoots", ItemTypeBoots);
		ItemTypeStone = inIReader.ReadString("Language", "ItemTypeStone", ItemTypeStone);
		ItemTypeTorch = inIReader.ReadString("Language", "ItemTypeTorch", ItemTypeTorch);
		ItemTypePotion = inIReader.ReadString("Language", "ItemTypePotion", ItemTypePotion);
		ItemTypeOre = inIReader.ReadString("Language", "ItemTypeOre", ItemTypeOre);
		ItemTypeMeat = inIReader.ReadString("Language", "ItemTypeMeat", ItemTypeMeat);
		ItemTypeCraftingMaterial = inIReader.ReadString("Language", "ItemTypeCraftingMaterial", ItemTypeCraftingMaterial);
		ItemTypeScroll = inIReader.ReadString("Language", "ItemTypeScroll", ItemTypeScroll);
		ItemTypeGem = inIReader.ReadString("Language", "ItemTypeGem", ItemTypeGem);
		ItemTypeMount = inIReader.ReadString("Language", "ItemTypeMount", ItemTypeMount);
		ItemTypeBook = inIReader.ReadString("Language", "ItemTypeBook", ItemTypeBook);
		ItemTypeScript = inIReader.ReadString("Language", "ItemTypeScript", ItemTypeScript);
		ItemTypeReins = inIReader.ReadString("Language", "ItemTypeReins", ItemTypeReins);
		ItemTypeBells = inIReader.ReadString("Language", "ItemTypeBells", ItemTypeBells);
		ItemTypeSaddle = inIReader.ReadString("Language", "ItemTypeSaddle", ItemTypeSaddle);
		ItemTypeRibbon = inIReader.ReadString("Language", "ItemTypeRibbon", ItemTypeRibbon);
		ItemTypeMask = inIReader.ReadString("Language", "ItemTypeMask", ItemTypeMask);
		ItemTypeFood = inIReader.ReadString("Language", "ItemTypeFood", ItemTypeFood);
		ItemTypeHook = inIReader.ReadString("Language", "ItemTypeHook", ItemTypeHook);
		ItemTypeFloat = inIReader.ReadString("Language", "ItemTypeFloat", ItemTypeFloat);
		ItemTypeBait = inIReader.ReadString("Language", "ItemTypeBait", ItemTypeBait);
		ItemTypeFinder = inIReader.ReadString("Language", "ItemTypeFinder", ItemTypeFinder);
		ItemTypeReel = inIReader.ReadString("Language", "ItemTypeReel", ItemTypeReel);
		ItemTypeFish = inIReader.ReadString("Language", "ItemTypeFish", ItemTypeFish);
		ItemTypeQuest = inIReader.ReadString("Language", "ItemTypeQuest", ItemTypeQuest);
		ItemTypeAwakening = inIReader.ReadString("Language", "ItemTypeAwakening", ItemTypeAwakening);
		ItemTypePets = inIReader.ReadString("Language", "ItemTypePets", ItemTypePets);
		ItemTypeTransform = inIReader.ReadString("Language", "ItemTypeTransform", ItemTypeTransform);
		ItemTypeSealedHero = inIReader.ReadString("Language", "ItemTypeSealedHero", ItemTypeSealedHero);
		ItemGradeCommon = inIReader.ReadString("Language", "ItemGradeCommon", ItemGradeCommon);
		ItemGradeRare = inIReader.ReadString("Language", "ItemGradeRare", ItemGradeRare);
		ItemGradeLegendary = inIReader.ReadString("Language", "ItemGradeLegendary", ItemGradeLegendary);
		ItemGradeMythical = inIReader.ReadString("Language", "ItemGradeMythical", ItemGradeMythical);
		ItemGradeHeroic = inIReader.ReadString("Language", "ItemGradeHeroic", ItemGradeHeroic);
		NoAccountID = inIReader.ReadString("Language", "NoAccountID", NoAccountID);
		IncorrectPasswordAccountID = inIReader.ReadString("Language", "IncorrectPasswordAccountID", IncorrectPasswordAccountID);
		GroupSwitch = inIReader.ReadString("Language", "GroupSwitch", GroupSwitch);
		GroupAdd = inIReader.ReadString("Language", "GroupAdd", GroupAdd);
		GroupRemove = inIReader.ReadString("Language", "GroupRemove", GroupRemove);
		GroupAddEnterName = inIReader.ReadString("Language", "GroupAddEnterName", GroupAddEnterName);
		GroupRemoveEnterName = inIReader.ReadString("Language", "GroupRemoveEnterName", GroupRemoveEnterName);
		TooHeavyToHold = inIReader.ReadString("Language", "TooHeavyToHold", TooHeavyToHold);
		SwitchMarriage = inIReader.ReadString("Language", "SwitchMarriage", SwitchMarriage);
		RequestMarriage = inIReader.ReadString("Language", "RequestMarriage", RequestMarriage);
		RequestDivorce = inIReader.ReadString("Language", "RequestDivorce", RequestDivorce);
		MailLover = inIReader.ReadString("Language", "MailLover", MailLover);
		WhisperLover = inIReader.ReadString("Language", "WhisperLover", WhisperLover);
	}

	public static void SaveClientLanguage(string languageIniPath)
	{
		File.Delete(languageIniPath);
		InIReader inIReader = new InIReader(languageIniPath);
		inIReader.Write("Language", "PetMode_Both", PetMode_Both);
		inIReader.Write("Language", "PetMode_MoveOnly", PetMode_MoveOnly);
		inIReader.Write("Language", "PetMode_AttackOnly", PetMode_AttackOnly);
		inIReader.Write("Language", "PetMode_None", PetMode_None);
		inIReader.Write("Language", "PetMode_FocusMasterTarget", PetMode_FocusMasterTarget);
		inIReader.Write("Language", "AttackMode_Peace", AttackMode_Peace);
		inIReader.Write("Language", "AttackMode_Group", AttackMode_Group);
		inIReader.Write("Language", "AttackMode_Guild", AttackMode_Guild);
		inIReader.Write("Language", "AttackMode_EnemyGuild", AttackMode_EnemyGuild);
		inIReader.Write("Language", "AttackMode_RedBrown", AttackMode_RedBrown);
		inIReader.Write("Language", "AttackMode_All", AttackMode_All);
		inIReader.Write("Language", "LogOutTip", LogOutTip);
		inIReader.Write("Language", "ExitTip", ExitTip);
		inIReader.Write("Language", "DiedTip", DiedTip);
		inIReader.Write("Language", "DropTip", DropTip);
		inIReader.Write("Language", "Inventory", Inventory);
		inIReader.Write("Language", "Character", Character);
		inIReader.Write("Language", "Skills", Skills);
		inIReader.Write("Language", "Quests", Quests);
		inIReader.Write("Language", "Options", Options);
		inIReader.Write("Language", "Menu", Menu);
		inIReader.Write("Language", "GameShop", GameShop);
		inIReader.Write("Language", "BigMap", BigMap);
		inIReader.Write("Language", "MiniMap", MiniMap);
		inIReader.Write("Language", "DuraPanel", DuraPanel);
		inIReader.Write("Language", "Mail", Mail);
		inIReader.Write("Language", "Exit", Exit);
		inIReader.Write("Language", "LogOut", LogOut);
		inIReader.Write("Language", "Help", Help);
		inIReader.Write("Language", "Keybinds", Keybinds);
		inIReader.Write("Language", "Ranking", Ranking);
		inIReader.Write("Language", "Creatures", Creatures);
		inIReader.Write("Language", "Mount", Mount);
		inIReader.Write("Language", "Fishing", Fishing);
		inIReader.Write("Language", "Friends", Friends);
		inIReader.Write("Language", "Mentor", Mentor);
		inIReader.Write("Language", "Relationship", Relationship);
		inIReader.Write("Language", "Groups", Groups);
		inIReader.Write("Language", "Guild", Guild);
		inIReader.Write("Language", "Trade", Trade);
		inIReader.Write("Language", "Size", Size);
		inIReader.Write("Language", "ChatSettings", ChatSettings);
		inIReader.Write("Language", "Rotate", Rotate);
		inIReader.Write("Language", "Close", Close);
		inIReader.Write("Language", "GameMaster", GameMaster);
		inIReader.Write("Language", "Report", Report);
		inIReader.Write("Language", "Expire", Expire);
		inIReader.Write("Language", "ExpireNever", ExpireNever);
		inIReader.Write("Language", "ExpirePaused", ExpirePaused);
		inIReader.Write("Language", "Never", Never);
		inIReader.Write("Language", "PatchErr", PatchErr);
		inIReader.Write("Language", "LastOnline", LastOnline);
		inIReader.Write("Language", "LowLevel", LowLevel);
		inIReader.Write("Language", "LowGold", LowGold);
		inIReader.Write("Language", "LowDC", LowDC);
		inIReader.Write("Language", "LowMC", LowMC);
		inIReader.Write("Language", "LowSC", LowSC);
		inIReader.Write("Language", "Gold", Gold);
		inIReader.Write("Language", "Credit", Credit);
		inIReader.Write("Language", "YouGained", YouGained);
		inIReader.Write("Language", "YouGained2", YouGained2);
		inIReader.Write("Language", "ExperienceGained", ExperienceGained);
		inIReader.Write("Language", "LevelUp", LevelUp);
		inIReader.Write("Language", "HeroLevelUp", HeroLevelUp);
		inIReader.Write("Language", "HeroInventory", Inventory);
		inIReader.Write("Language", "HeroCharacter", Character);
		inIReader.Write("Language", "HeroSkills", Skills);
		inIReader.Write("Language", "HeroSummon", HeroSummon);
		inIReader.Write("Language", "HeroExperienceGained", HeroExperienceGained);
		inIReader.Write("Language", "ItemDescription", ItemDescription);
		inIReader.Write("Language", "RequiredLevel", RequiredLevel);
		inIReader.Write("Language", "RequiredDC", RequiredDC);
		inIReader.Write("Language", "RequiredMC", RequiredMC);
		inIReader.Write("Language", "RequiredSC", RequiredSC);
		inIReader.Write("Language", "ClassRequired", ClassRequired);
		inIReader.Write("Language", "Holy", Holy);
		inIReader.Write("Language", "Accuracy", Accuracy);
		inIReader.Write("Language", "Agility", Agility);
		inIReader.Write("Language", "DC", DC);
		inIReader.Write("Language", "MC", MC);
		inIReader.Write("Language", "SC", SC);
		inIReader.Write("Language", "Durability", Durability);
		inIReader.Write("Language", "Weight", Weight);
		inIReader.Write("Language", "AC", AC);
		inIReader.Write("Language", "MAC", MAC);
		inIReader.Write("Language", "Luck", Luck);
		inIReader.Write("Language", "DeleteCharacter", DeleteCharacter);
		inIReader.Write("Language", "CharacterDeleted", CharacterDeleted);
		inIReader.Write("Language", "CharacterCreated", CharacterCreated);
		inIReader.Write("Language", "Resolution", Resolution);
		inIReader.Write("Language", "Autostart", Autostart);
		inIReader.Write("Language", "Usrname", Usrname);
		inIReader.Write("Language", "Password", Password);
		inIReader.Write("Language", "ShuttingDown", ShuttingDown);
		inIReader.Write("Language", "MaxCombine", MaxCombine);
		inIReader.Write("Language", "Count", Count);
		inIReader.Write("Language", "ExtraSlots8", ExtraSlots8);
		inIReader.Write("Language", "ExtraSlots4", ExtraSlots4);
		inIReader.Write("Language", "Chat_All", Chat_All);
		inIReader.Write("Language", "Chat_Short", Chat_Short);
		inIReader.Write("Language", "Chat_Whisper", Chat_Whisper);
		inIReader.Write("Language", "Chat_Lover", Chat_Lover);
		inIReader.Write("Language", "Chat_Mentor", Chat_Mentor);
		inIReader.Write("Language", "Chat_Group", Chat_Group);
		inIReader.Write("Language", "Chat_Guild", Chat_Guild);
		inIReader.Write("Language", "ExpandedStorageLocked", ExpandedStorageLocked);
		inIReader.Write("Language", "ExtraStorage", ExtraStorage);
		inIReader.Write("Language", "ExtendYourRentalPeriod", ExtendYourRentalPeriod);
		inIReader.Write("Language", "ExpandedStorageExpiresOn", ExpandedStorageExpiresOn);
		inIReader.Write("Language", "GameName", GameName);
		inIReader.Write("Language", "CannotLeaveGame", CannotLeaveGame);
		inIReader.Write("Language", "SelectKey", SelectKey);
		inIReader.Write("Language", "WeaponSpiritFire", WeaponSpiritFire);
		inIReader.Write("Language", "SpiritsFireDisappeared", SpiritsFireDisappeared);
		inIReader.Write("Language", "WeddingRing", WeddingRing);
		inIReader.Write("Language", "ItemTextFormat", ItemTextFormat);
		inIReader.Write("Language", "DropAmount", DropAmount);
		inIReader.Write("Language", "LowMana", LowMana);
		inIReader.Write("Language", "NotFemale", NotFemale);
		inIReader.Write("Language", "NotMale", NotMale);
		inIReader.Write("Language", "NoCreatures", NoCreatures);
		inIReader.Write("Language", "NoMount", NoMount);
		inIReader.Write("Language", "NoFishingRod", NoFishingRod);
		inIReader.Write("Language", "NotInGuild", NotInGuild);
		inIReader.Write("Language", "AttemptingConnect", AttemptingConnect);
		inIReader.Write("Language", "NoBagSpace", NoBagSpace);
		inIReader.Write("Language", "CreatingCharactersDisabled", CreatingCharactersDisabled);
		inIReader.Write("Language", "InvalidCharacterName", InvalidCharacterName);
		inIReader.Write("Language", "NoClass", NoClass);
		inIReader.Write("Language", "ToManyCharacters", ToManyCharacters);
		inIReader.Write("Language", "CharacterNameExists", CharacterNameExists);
		inIReader.Write("Language", "WarriorsDes", WarriorsDes);
		inIReader.Write("Language", "WizardDes", WizardDes);
		inIReader.Write("Language", "TaoistDes", TaoistDes);
		inIReader.Write("Language", "AssassinDes", AssassinDes);
		inIReader.Write("Language", "ArcherDes", ArcherDes);
		inIReader.Write("Language", "DateSent", DateSent);
		inIReader.Write("Language", "Send", Send);
		inIReader.Write("Language", "Reply", Reply);
		inIReader.Write("Language", "Read", Read);
		inIReader.Write("Language", "Delete", Delete);
		inIReader.Write("Language", "BlockList", BlockList);
		inIReader.Write("Language", "EnterMailToName", EnterMailToName);
		inIReader.Write("Language", "BeenPoisoned", BeenPoisoned);
		inIReader.Write("Language", "AddFriend", AddFriend);
		inIReader.Write("Language", "RemoveFriend", RemoveFriend);
		inIReader.Write("Language", "FriendMemo", FriendMemo);
		inIReader.Write("Language", "FriendMail", FriendMail);
		inIReader.Write("Language", "FriendWhisper", FriendWhisper);
		inIReader.Write("Language", "FriendEnterAddName", FriendEnterAddName);
		inIReader.Write("Language", "FriendEnterBlockName", FriendEnterBlockName);
		inIReader.Write("Language", "AddMentor", AddMentor);
		inIReader.Write("Language", "RemoveMentorMentee", RemoveMentorMentee);
		inIReader.Write("Language", "MentorRequests", MentorRequests);
		inIReader.Write("Language", "MentorEnterName", MentorEnterName);
		inIReader.Write("Language", "NoMentorship", NoMentorship);
		inIReader.Write("Language", "RestedBuff", RestedBuff);
		inIReader.Write("Language", "ItemTypeNothing", ItemTypeNothing);
		inIReader.Write("Language", "ItemTypeWeapon", ItemTypeWeapon);
		inIReader.Write("Language", "ItemTypeArmour", ItemTypeArmour);
		inIReader.Write("Language", "ItemTypeHelmet", ItemTypeHelmet);
		inIReader.Write("Language", "ItemTypeNecklace", ItemTypeNecklace);
		inIReader.Write("Language", "ItemTypeBracelet", ItemTypeBracelet);
		inIReader.Write("Language", "ItemTypeRing", ItemTypeRing);
		inIReader.Write("Language", "ItemTypeAmulet", ItemTypeAmulet);
		inIReader.Write("Language", "ItemTypeBelt", ItemTypeBelt);
		inIReader.Write("Language", "ItemTypeBoots", ItemTypeBoots);
		inIReader.Write("Language", "ItemTypeStone", ItemTypeStone);
		inIReader.Write("Language", "ItemTypeTorch", ItemTypeTorch);
		inIReader.Write("Language", "ItemTypePotion", ItemTypePotion);
		inIReader.Write("Language", "ItemTypeOre", ItemTypeOre);
		inIReader.Write("Language", "ItemTypeMeat", ItemTypeMeat);
		inIReader.Write("Language", "ItemTypeCraftingMaterial", ItemTypeCraftingMaterial);
		inIReader.Write("Language", "ItemTypeScroll", ItemTypeScroll);
		inIReader.Write("Language", "ItemTypeGem", ItemTypeGem);
		inIReader.Write("Language", "ItemTypeMount", ItemTypeMount);
		inIReader.Write("Language", "ItemTypeBook", ItemTypeBook);
		inIReader.Write("Language", "ItemTypeScript", ItemTypeScript);
		inIReader.Write("Language", "ItemTypeReins", ItemTypeReins);
		inIReader.Write("Language", "ItemTypeBells", ItemTypeBells);
		inIReader.Write("Language", "ItemTypeSaddle", ItemTypeSaddle);
		inIReader.Write("Language", "ItemTypeRibbon", ItemTypeRibbon);
		inIReader.Write("Language", "ItemTypeMask", ItemTypeMask);
		inIReader.Write("Language", "ItemTypeFood", ItemTypeFood);
		inIReader.Write("Language", "ItemTypeHook", ItemTypeHook);
		inIReader.Write("Language", "ItemTypeFloat", ItemTypeFloat);
		inIReader.Write("Language", "ItemTypeBait", ItemTypeBait);
		inIReader.Write("Language", "ItemTypeFinder", ItemTypeFinder);
		inIReader.Write("Language", "ItemTypeReel", ItemTypeReel);
		inIReader.Write("Language", "ItemTypeFish", ItemTypeFish);
		inIReader.Write("Language", "ItemTypeQuest", ItemTypeQuest);
		inIReader.Write("Language", "ItemTypeAwakening", ItemTypeAwakening);
		inIReader.Write("Language", "ItemTypePets", ItemTypePets);
		inIReader.Write("Language", "ItemTypeTransform", ItemTypeTransform);
		inIReader.Write("Language", "ItemTypeSealedHero", ItemTypeSealedHero);
		inIReader.Write("Language", "ItemGradeCommon", ItemGradeCommon);
		inIReader.Write("Language", "ItemGradeRare", ItemGradeRare);
		inIReader.Write("Language", "ItemGradeLegendary", ItemGradeLegendary);
		inIReader.Write("Language", "ItemGradeMythical", ItemGradeMythical);
		inIReader.Write("Language", "ItemGradeHeroic", ItemGradeHeroic);
		inIReader.Write("Language", "NoAccountID", NoAccountID);
		inIReader.Write("Language", "IncorrectPasswordAccountID", IncorrectPasswordAccountID);
		inIReader.Write("Language", "GroupSwitch", GroupSwitch);
		inIReader.Write("Language", "GroupAdd", GroupAdd);
		inIReader.Write("Language", "GroupRemove", GroupRemove);
		inIReader.Write("Language", "GroupAddEnterName", GroupAddEnterName);
		inIReader.Write("Language", "GroupRemoveEnterName", GroupRemoveEnterName);
		inIReader.Write("Language", "TooHeavyToHold", TooHeavyToHold);
		inIReader.Write("Language", "SwitchMarriage", SwitchMarriage);
		inIReader.Write("Language", "RequestMarriage", RequestMarriage);
		inIReader.Write("Language", "RequestDivorce", RequestDivorce);
		inIReader.Write("Language", "MailLover", MailLover);
		inIReader.Write("Language", "WhisperLover", WhisperLover);
	}

	public static void LoadServerLanguage(string languageIniPath)
	{
		if (!File.Exists(languageIniPath))
		{
			SaveServerLanguage(languageIniPath);
			return;
		}
		InIReader inIReader = new InIReader(languageIniPath);
		Welcome = inIReader.ReadString("Language", "Welcome", Welcome);
		OnlinePlayers = inIReader.ReadString("Language", "OnlinePlayers", OnlinePlayers);
		LowLevel = inIReader.ReadString("Language", "LowLevel", LowLevel);
		LowGold = inIReader.ReadString("Language", "LowGold", LowGold);
		LowDC = inIReader.ReadString("Language", "LowDC", LowDC);
		LowMC = inIReader.ReadString("Language", "LowMC", LowMC);
		LowSC = inIReader.ReadString("Language", "LowSC", LowSC);
		LevelUp = inIReader.ReadString("Language", "LevelUp", LevelUp);
		HeroLevelUp = inIReader.ReadString("Language", "HeroLevelUp", HeroLevelUp);
		WeaponLuck = inIReader.ReadString("Language", "WeaponLuck", WeaponLuck);
		WeaponCurse = inIReader.ReadString("Language", "WeaponCurse", WeaponCurse);
		WeaponNoEffect = inIReader.ReadString("Language", "WeaponNoEffect", WeaponNoEffect);
		InventoryIncreased = inIReader.ReadString("Language", "InventoryIncreased", InventoryIncreased);
		ExpandedStorageExpiresOn = inIReader.ReadString("Language", "ExpandedStorageExpiresOn", ExpandedStorageExpiresOn);
		GameName = inIReader.ReadString("Language", "GameName", GameName);
		FaceToTrade = inIReader.ReadString("Language", "FaceToTrade", FaceToTrade);
		NoTownTeleport = inIReader.ReadString("Language", "NoTownTeleport", NoTownTeleport);
		CanNotRandom = inIReader.ReadString("Language", "CanNotRandom", CanNotRandom);
		CanNotDungeon = inIReader.ReadString("Language", "CanNotDungeon", CanNotDungeon);
		CannotResurrection = inIReader.ReadString("Language", "CannotResurrection", CannotResurrection);
		CanNotDrop = inIReader.ReadString("Language", "CanNotDrop", CanNotDrop);
		NotFemale = inIReader.ReadString("Language", "NotFemale", NotFemale);
		NotMale = inIReader.ReadString("Language", "NotMale", NotMale);
		NotInGuild = inIReader.ReadString("Language", "NotInGuild", NotInGuild);
		NewMail = inIReader.ReadString("Language", "NewMail", NewMail);
		CouldNotFindPlayer = inIReader.ReadString("Language", "CouldNotFindPlayer", CouldNotFindPlayer);
		NoMentorship = inIReader.ReadString("Language", "NoMentorship", NoMentorship);
		NoBagSpace = inIReader.ReadString("Language", "NoBagSpace", NoBagSpace);
		AllowingMentorRequests = inIReader.ReadString("Language", "AllowingMentorRequests", AllowingMentorRequests);
		BlockingMentorRequests = inIReader.ReadString("Language", "BlockingMentorRequests", BlockingMentorRequests);
	}

	public static void SaveServerLanguage(string languageIniPath)
	{
		File.Delete(languageIniPath);
		InIReader inIReader = new InIReader(languageIniPath);
		inIReader.Write("Language", "Welcome", Welcome);
		inIReader.Write("Language", "OnlinePlayers", OnlinePlayers);
		inIReader.Write("Language", "LowLevel", LowLevel);
		inIReader.Write("Language", "LowGold", LowGold);
		inIReader.Write("Language", "LowDC", LowDC);
		inIReader.Write("Language", "LowMC", LowMC);
		inIReader.Write("Language", "LowSC", LowSC);
		inIReader.Write("Language", "LevelUp", LevelUp);
		inIReader.Write("Language", "HeroLevelUp", HeroLevelUp);
		inIReader.Write("Language", "WeaponLuck", WeaponLuck);
		inIReader.Write("Language", "WeaponCurse", WeaponCurse);
		inIReader.Write("Language", "WeaponNoEffect", WeaponNoEffect);
		inIReader.Write("Language", "InventoryIncreased", InventoryIncreased);
		inIReader.Write("Language", "ExpandedStorageExpiresOn", ExpandedStorageExpiresOn);
		inIReader.Write("Language", "GameName", GameName);
		inIReader.Write("Language", "FaceToTrade", FaceToTrade);
		inIReader.Write("Language", "NoTownTeleport", NoTownTeleport);
		inIReader.Write("Language", "CanNotRandom", CanNotRandom);
		inIReader.Write("Language", "CanNotDungeon", CanNotDungeon);
		inIReader.Write("Language", "CannotResurrection", CannotResurrection);
		inIReader.Write("Language", "CanNotDrop", CanNotDrop);
		inIReader.Write("Language", "NotFemale", NotFemale);
		inIReader.Write("Language", "NotMale", NotMale);
		inIReader.Write("Language", "NotInGuild", NotInGuild);
		inIReader.Write("Language", "NewMail", NewMail);
		inIReader.Write("Language", "CouldNotFindPlayer", CouldNotFindPlayer);
		inIReader.Write("Language", "NoMentorship", NoMentorship);
		inIReader.Write("Language", "NoBagSpace", NoBagSpace);
		inIReader.Write("Language", "AllowingMentorRequests", AllowingMentorRequests);
		inIReader.Write("Language", "BlockingMentorRequests", BlockingMentorRequests);
	}
}
