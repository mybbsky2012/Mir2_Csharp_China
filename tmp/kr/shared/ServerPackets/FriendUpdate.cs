using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class FriendUpdate : Packet
{
	public List<ClientFriend> Friends = new List<ClientFriend>();

	public override short Index => 243;

	protected override void ReadPacket(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Friends.Add(new ClientFriend(reader));
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Friends.Count);
		for (int i = 0; i < Friends.Count; i++)
		{
			Friends[i].Save(writer);
		}
	}
}
