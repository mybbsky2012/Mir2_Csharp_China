using Server.MirDatabase;

namespace Server.MirEnvir
{
    public class MailInfo
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        public ulong MailID;

        public string Sender;

        public int RecipientIndex;
        public CharacterInfo RecipientInfo;

        public string Message = string.Empty;
        public uint Gold = 0;
        public List<UserItem> Items = new List<UserItem>();

        public DateTime DateSent, DateOpened;

        public bool Sent
        {
            get { return DateSent > DateTime.MinValue; }
        }

        public bool Opened
        {
            get { return DateOpened > DateTime.MinValue; }
        }

        public bool Locked;

        public bool Collected;

        public bool Parcel //parcel if item contains gold or items.
        {
            get { return Gold > 0 || Items.Count > 0; }
        }

        public bool CanReply;

        public MailInfo(int recipientIndex, bool canReply = false)
        {
            MailID = ++Envir.NextMailID;
            RecipientIndex = recipientIndex;

            CanReply = canReply;
        }

        public MailInfo(BinaryReader reader, int version, int customversion)
        {
            MailID = reader.ReadUInt64();
            Sender = reader.ReadString();
            RecipientIndex = reader.ReadInt32();
            Message = reader.ReadString();
            Gold = reader.ReadUInt32();

            int count = reader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                UserItem item = new UserItem(reader, version, customversion);
                if (Envir.BindItem(item))
                    Items.Add(item);
            }

            DateSent = DateTime.FromBinary(reader.ReadInt64());
            DateOpened = DateTime.FromBinary(reader.ReadInt64());

            Locked = reader.ReadBoolean();
            Collected = reader.ReadBoolean();
            CanReply = reader.ReadBoolean();
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(MailID);
            // null 会让 BinaryWriter 写 -1，读回来 ReadString 直接抛 IOException → 整个账号库读不完
            writer.Write(Sender ?? string.Empty);
            writer.Write(RecipientIndex);
            writer.Write(Message ?? string.Empty);
            writer.Write(Gold);

            writer.Write(Items.Count);
            for (int i = 0; i < Items.Count; i++)
                Items[i].Save(writer);

            writer.Write(DateSent.ToBinary());
            writer.Write(DateOpened.ToBinary());

            writer.Write(Locked);
            writer.Write(Collected);
            writer.Write(CanReply);
        }

        /// <summary>
        /// 投递这封信。返回 false ＝ 收件人角色已不存在（被归档/删除），信没有投出去。
        /// </summary>
        public bool Send()
        {
            if (Sent) return true;

            Collected = true;

            if (Parcel)
            {
                if(Items.Count > 0 && Gold > 0)
                {
                    if(!Settings.MailAutoSendGold || !Settings.MailAutoSendItems)
                    {
                        Collected = false;
                    }
                }
                if(Items.Count > 0)
                {
                    if (!Settings.MailAutoSendItems)
                    {
                        Collected = false;
                    }
                }
                else
                {
                    if (!Settings.MailAutoSendGold)
                    {
                        Collected = false;
                    }
                }
            }

            if (RecipientInfo == null)
            {
                RecipientInfo = Envir.GetCharacterInfo(RecipientIndex);
            }

            //★11-06：收件人角色可能已经被归档/删除（不在 CharacterList 里），GetCharacterInfo 返回 null。
            //  原来这里直接 RecipientInfo.Mail.Add → NullReferenceException。
            //  拍卖结算、装备租赁到期退回、交易退款(MailCharacter) 都会走到这里，
            //  而它们都在主循环/对象线程上跑 → 一崩就是整个 Envir 重启，全服掉线。
            //  投不出去时把「谁发给谁、多少金币、几件东西」写进日志，别让物品静默消失。
            if (RecipientInfo == null)
            {
                MessageQueue.Instance.Enqueue(
                    $"邮件投递失败 #{MailID}（{Sender ?? "(无)"} → 角色ID {RecipientIndex}）：" +
                    $"收件人已不在角色列表（可能已归档/删除），未投出 金币 {Gold}、物品 {Items.Count} 件");
                return false;
            }

            RecipientInfo.Mail.Add(this); //add to players inbox

            if (RecipientInfo.Player != null)
            {
                RecipientInfo.Player.NewMail = true; //notify player of new mail  --check in player process
            }

            DateSent = Envir.Now;
            return true;
        }

        public ClientMail CreateClientMail()
        {
            return new ClientMail
            {
                MailID = MailID,
                SenderName = Sender,
                Message = Message,
                Locked = Locked,
                CanReply = CanReply,
                Gold = Gold,
                Items = Items,
                Opened = Opened,
                Collected = Collected,
                DateSent = DateSent
            };
        }
    }

    // player bool NewMail (process in envir loop) - send all mail on login

    // Send mail from player (auto from player)
    // Send mail from Envir (mir administrator)
}
