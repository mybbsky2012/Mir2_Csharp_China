using Server.MirEnvir;
using Server.MirObjects;

namespace Server.MirDatabase
{
    public class RecipeInfo
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        protected static MessageQueue MessageQueue
        {
            get { return MessageQueue.Instance; }
        }

        public UserItem Item;
        public List<UserItem> Ingredients;
        public List<UserItem> Tools;

        public List<int> RequiredFlag = new List<int>();
        public ushort? RequiredLevel = null;
        public List<int> RequiredQuest = new List<int>();
        public List<MirClass> RequiredClass = new List<MirClass>();
        public MirGender? RequiredGender = null;

        public byte Chance = 100;
        public uint Gold = 0;

        /// <summary>
        /// 加载过程中是否有任何物品无法解析（本体物品 / 工具 / 材料在数据库中不存在）。
        /// 例如 Envir\Recipe 文本比数据库新的时候就会发生。
        /// </summary>
        private bool _loadFailed;

        /// <summary>
        /// 配方是否可用。任何一环物品缺失都视为无效配方，加载阶段直接跳过。
        /// 这样既避免玩家登录时推送配方列表崩溃，也避免配方少了材料后被「白嫖合成」。
        /// </summary>
        public bool IsValid
        {
            get { return !_loadFailed && Item != null && Item.Info != null; }
        }

        public RecipeInfo(string name)
        {
            // 先把集合建好：即使后面解析失败提前 return，也不会留下 null 集合
            Tools = new List<UserItem>();
            Ingredients = new List<UserItem>();

            ItemInfo itemInfo = Envir.GetItemInfo(name);
            if (itemInfo == null)
            {
                MessageQueue.Enqueue(string.Format("缺少物品: {0}", name));
                _loadFailed = true;
                return;
            }

            Item = Envir.CreateShopItem(itemInfo, ++Envir.NextRecipeID);

            if (Item == null)
            {
                MessageQueue.Enqueue(string.Format("无法创建配方物品: {0}", name));
                _loadFailed = true;
                return;
            }

            LoadIngredients(name);
        }

        private void LoadIngredients(string recipe)
        {
            List<string> lines = File.ReadAllLines(Path.Combine(Settings.RecipePath, recipe + ".txt")).ToList();

            Tools = new List<UserItem>();
            Ingredients = new List<UserItem>();

            var mode = "ingredients";

            for (int i = 0; i < lines.Count; i++)
            {
                if (String.IsNullOrEmpty(lines[i])) continue;

                if (lines[i].StartsWith("["))
                {
                    mode = lines[i].Substring(1, lines[i].Length - 2).ToLower();
                    continue;
                }

                switch (mode)
                {
                    case "recipe":
                        {
                            var data = lines[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                            if (data.Length < 2) continue;

                            switch (data[0].ToLower())
                            {
                                case "amount":
                                    Item.Count = ushort.Parse(data[1]);
                                    break;
                                case "chance":
                                    Chance = byte.Parse(data[1]);

                                    if (Chance > 100)
                                    {
                                        Chance = 100;
                                    }
                                    break;
                                case "gold":
                                    Gold = uint.Parse(data[1]);
                                    break;
                                default:
                                    break;
                            }
                        }
                        break;
                    case "tools":
                        {
                            var data = lines[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                            ItemInfo info = Envir.GetItemInfo(data[0]);

                            if (info == null)
                            {
                                MessageQueue.Enqueue(string.Format("合成配方: {1} 中缺少工具: {0}", lines[i], recipe));
                                _loadFailed = true;
                                continue;
                            }

                            UserItem tool = Envir.CreateShopItem(info, 0);

                            Tools.Add(tool);
                        }
                        break;
                    case "ingredients":
                        {
                            var data = lines[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                            ItemInfo info = Envir.GetItemInfo(data[0]);

                            if (info == null)
                            {
                                MessageQueue.Enqueue(string.Format("合成配方: {1} 中缺少材料: {0}", lines[i], recipe));
                                _loadFailed = true;
                                continue;
                            }

                            UserItem ingredient = Envir.CreateShopItem(info, 0);

                            ushort count = 1;
                            if (data.Length >= 2)
                                ushort.TryParse(data[1], out count);

                            if (data.Length >= 3)
                                ushort.TryParse(data[2], out ingredient.CurrentDura);

                            ingredient.Count = count > info.StackSize ? info.StackSize : count;

                            Ingredients.Add(ingredient);
                        }
                        break;
                    case "criteria":
                        {
                            var data = lines[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                            if (data.Length < 2) continue;

                            try
                            {
                                switch (data[0].ToLower())
                                {
                                    case "level":
                                        RequiredLevel = ushort.Parse(data[1]);
                                        break;
                                    case "class":
                                        if (Enum.TryParse<MirClass>(data[1], true, out MirClass cls))
                                        {
                                            RequiredClass.Add(cls);
                                        }
                                        else
                                        {
                                            RequiredClass.Add((MirClass)byte.Parse(data[1]));
                                        }
                                        break;
                                    case "gender":
                                        if (Enum.TryParse<MirGender>(data[1], true, out MirGender gender))
                                        {
                                            RequiredGender = gender;
                                        }
                                        else
                                        {
                                            RequiredGender = (MirGender)byte.Parse(data[1]);
                                        }
                                        break;
                                    case "flag":
                                        RequiredFlag.Add(int.Parse(data[1]));
                                        break;
                                    case "quest":
                                        RequiredQuest.Add(int.Parse(data[1]));
                                        break;
                                }
                            }
                            catch
                            {
                                MessageQueue.Enqueue(string.Format("无法分析 {0}, 的值: {1}", data[0], data[1]));
                                continue;
                            }
                        }
                        break;
                }
            }
        }

        public bool MatchItem(int index)
        {
            return Item != null && Item.ItemIndex == index;
        }

        public bool CanCraft(PlayerObject player)
        {
            if (RequiredLevel != null && RequiredLevel.Value > player.Level)
                return false;

            if (RequiredGender != null && RequiredGender.Value != player.Gender)
                return false;

            if (RequiredClass.Count > 0 && !RequiredClass.Contains(player.Class))
                return false;

            if (RequiredFlag.Count > 0)
            {
                foreach (var flag in RequiredFlag)
                {
                     if(!player.Info.Flags[flag])
                        return false;
                }
            }

            if (RequiredQuest.Count > 0)
            {
                foreach (var quest in RequiredQuest)
                {
                    if (!player.Info.CompletedQuests.Contains(quest))
                        return false;
                }
            }

            return true;
        }

        public ClientRecipeInfo CreateClientRecipeInfo()
        {
            if (!IsValid) return null;

            ClientRecipeInfo clientInfo = new ClientRecipeInfo
            {
                Gold = Gold,
                Chance = Chance,
                Item = Item.Clone(),
                Tools = Tools ?? new List<UserItem>(),
                Ingredients = Ingredients ?? new List<UserItem>()
            };

            return clientInfo;
        }
    }
}


