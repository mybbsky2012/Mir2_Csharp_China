using Server.MirNetwork;
using Server.MirEnvir;
using Server.Utils;
using C = ClientPackets;

namespace Server.MirDatabase
{
    public class AccountInfo
    {       
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }
        protected static MessageQueue MessageQueue => MessageQueue.Instance;

        public int Index;

        public string AccountID = string.Empty;

        private string password = string.Empty;
        public string Password
        {
            get { return password; }
            set
            {                
                Salt = Crypto.GenerateSalt();
                password = Crypto.HashPassword(value, Salt);
                
            }
        }

        public byte[] Salt = new byte[24];

        public string UserName = string.Empty;
        public DateTime BirthDate;
        public string SecretQuestion = string.Empty;
        public string SecretAnswer = string.Empty;
        public string EMailAddress = string.Empty;

        public string CreationIP = string.Empty;
        public DateTime CreationDate;

        public bool Banned;
        public bool RequirePasswordChange;
        public string BanReason = string.Empty;
        public DateTime ExpiryDate;
        public int WrongPasswordCount;

        public string LastIP = string.Empty;
        public DateTime LastDate;

        public List<CharacterInfo> Characters = new List<CharacterInfo>();

        /// <summary>
        /// 本次加载中被自动归档掉的角色数（纯运行时标记，不落盘）。
        /// ★Envir.LoadAccounts 用它区分两种「0 角色账号」：
        ///   读进来就是 0 角色的真空号可以丢；因为归档才变空的账号必须留着 ——
        ///   账号仓库 Storage、金币 Gold、充值 Credit 都在账号上，.MirCA 里没有这些。
        /// </summary>
        public int ArchivedCharacters;

        public UserItem[] Storage = new UserItem[80];
        public bool HasExpandedStorage;
        public DateTime ExpandedStorageExpiryDate;
        public uint Gold;
        public uint Credit;

        public MirConnection Connection;
        
        public LinkedList<AuctionInfo> Auctions = new LinkedList<AuctionInfo>();
        public bool AdminAccount;

        public AccountInfo()
        {

        }

        public AccountInfo(C.NewAccount p)
        {
            AccountID = p.AccountID;

            Password = p.Password;
            UserName = p.UserName;
            SecretQuestion = p.SecretQuestion;
            SecretAnswer = p.SecretAnswer;
            EMailAddress = p.EMailAddress;

            BirthDate = p.BirthDate;
            CreationDate = Envir.Now;
        }
        public AccountInfo(BinaryReader reader)
        {
            Index = reader.ReadInt32();

            AccountID = reader.ReadString();
            if (Envir.LoadVersion < 94)
                Password = reader.ReadString();
            else
                password = reader.ReadString();

            if (Envir.LoadVersion > 93)
                Salt = reader.ReadBytes(reader.ReadInt32());

            if (Envir.LoadVersion > 97)
                RequirePasswordChange = reader.ReadBoolean();

            UserName = reader.ReadString();
            BirthDate = DateTime.FromBinary(reader.ReadInt64());
            SecretQuestion = reader.ReadString();
            SecretAnswer = reader.ReadString();
            EMailAddress = reader.ReadString();

            CreationIP = reader.ReadString();
            CreationDate = DateTime.FromBinary(reader.ReadInt64());

            Banned = reader.ReadBoolean();
            BanReason = reader.ReadString();
            ExpiryDate = DateTime.FromBinary(reader.ReadInt64());

            LastIP = reader.ReadString();
            LastDate = DateTime.FromBinary(reader.ReadInt64());

            int count = reader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                var info = new CharacterInfo(reader, Envir.LoadVersion, Envir.LoadCustomVersion) { AccountInfo = this };

                if (ShouldArchive(info, out string archiveReason))
                {
                    MessageQueue.Enqueue($"玩家 {info.Name} 由于{archiveReason}已归档处理");

                    //★11-06：自动归档改成幂等，重复启动不会再堆出第 2、3 份副本
                    Envir.ArchiveCharacterOnce(info);

                    ArchivedCharacters++;
                    continue;
                }

                Characters.Add(info);
            }

            if (Envir.LoadVersion > 75)
            {
                HasExpandedStorage = reader.ReadBoolean();
                ExpandedStorageExpiryDate = DateTime.FromBinary(reader.ReadInt64());
            }
            
            Gold = reader.ReadUInt32();
            if (Envir.LoadVersion >= 63) Credit = reader.ReadUInt32();

            count = reader.ReadInt32();

            Array.Resize(ref Storage, count);

            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadBoolean()) continue;
                UserItem item = new UserItem(reader, Envir.LoadVersion, Envir.LoadCustomVersion);
                if (Envir.BindItem(item) && i < Storage.Length)
                    Storage[i] = item;
            }

            if (Envir.LoadVersion >= 10) AdminAccount = reader.ReadBoolean();
            if (!AdminAccount)
            {
                for (int i = 0; i < Characters.Count; i++)
                {
                    if (Characters[i] == null) continue;
                    if (Characters[i].Deleted) continue;
                    if ((Envir.Now - Characters[i].LastLogoutDate).TotalDays > 13) continue;
                    Envir.CheckRankUpdate(Characters[i]);
                }
            }
        }

        /// <summary>
        /// 判定一个角色是否该在加载时自动归档（三条原规则合并，行为不变，只是把原因一并带出来写日志）。
        /// </summary>
        private static bool ShouldArchive(CharacterInfo info, out string reason)
        {
            reason = null;

            if (info.Deleted && info.DeleteDate.AddMonths(Settings.ArchiveDeletedCharacterAfterMonths) <= Envir.Now)
            {
                reason = $"已删除角色 {Settings.ArchiveDeletedCharacterAfterMonths} 月";
                return true;
            }

            if (info.LastLoginDate == DateTime.MinValue && info.CreationDate.AddMonths(Settings.ArchiveInactiveCharacterAfterMonths) <= Envir.Now)
            {
                reason = $"{Settings.ArchiveInactiveCharacterAfterMonths} 月未登录";
                return true;
            }

            if (info.LastLoginDate > DateTime.MinValue && info.LastLoginDate.AddMonths(Settings.ArchiveInactiveCharacterAfterMonths) <= Envir.Now)
            {
                reason = $"{Settings.ArchiveInactiveCharacterAfterMonths} 月未激活";
                return true;
            }

            return false;
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(Index);
            writer.Write(AccountID);
            writer.Write(Password);
            writer.Write(Salt.Length);
            writer.Write(Salt);
            writer.Write(RequirePasswordChange);

            writer.Write(UserName);
            writer.Write(BirthDate.ToBinary());
            writer.Write(SecretQuestion);
            writer.Write(SecretAnswer);
            writer.Write(EMailAddress);

            writer.Write(CreationIP);
            writer.Write(CreationDate.ToBinary());

            writer.Write(Banned);
            writer.Write(BanReason);
            writer.Write(ExpiryDate.ToBinary());

            writer.Write(LastIP);
            writer.Write(LastDate.ToBinary());

            writer.Write(Characters.Count);
            for (int i = 0; i < Characters.Count; i++)
            {
                Characters[i].Save(writer);
            }

            writer.Write(HasExpandedStorage);
            writer.Write(ExpandedStorageExpiryDate.ToBinary());
            writer.Write(Gold);
            writer.Write(Credit);
            writer.Write(Storage.Length);
            for (int i = 0; i < Storage.Length; i++)
            {
                writer.Write(Storage[i] != null);
                if (Storage[i] == null) continue;

                Storage[i].Save(writer);
            }
            writer.Write(AdminAccount);
        }

        public List<SelectInfo> GetSelectInfo()
        {
            List<SelectInfo> list = new List<SelectInfo>();

            for (int i = 0; i < Characters.Count; i++)
            {
                if (Characters[i].Deleted) continue;
                list.Add(Characters[i].ToSelectInfo());
                if (list.Count >= Globals.MaxCharacterCount) break;
            }

            return list;
        }

        public int ExpandStorage()
        {
            if (Storage.Length == 80)
                Array.Resize(ref Storage, Storage.Length + 80);

            return Storage.Length;
        }
    }
}
