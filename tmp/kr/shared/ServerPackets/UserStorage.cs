using System.IO;

namespace ServerPackets;

public sealed class UserStorage : Packet
{
	public UserItem[] Storage;

	public override short Index => 128;

	protected override void ReadPacket(BinaryReader reader)
	{
		if (!reader.ReadBoolean())
		{
			return;
		}
		Storage = new UserItem[reader.ReadInt32()];
		for (int i = 0; i < Storage.Length; i++)
		{
			if (reader.ReadBoolean())
			{
				Storage[i] = new UserItem(reader);
			}
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Storage != null);
		if (Storage == null)
		{
			return;
		}
		writer.Write(Storage.Length);
		for (int i = 0; i < Storage.Length; i++)
		{
			writer.Write(Storage[i] != null);
			if (Storage[i] != null)
			{
				Storage[i].Save(writer);
			}
		}
	}
}
