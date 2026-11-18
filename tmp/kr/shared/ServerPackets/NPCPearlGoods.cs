using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class NPCPearlGoods : Packet
{
	public List<UserItem> List = new List<UserItem>();

	public float Rate;

	public PanelType Type;

	public override short Index => 241;

	protected override void ReadPacket(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			List.Add(new UserItem(reader));
		}
		Rate = reader.ReadSingle();
		Type = (PanelType)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(List.Count);
		for (int i = 0; i < List.Count; i++)
		{
			List[i].Save(writer);
		}
		writer.Write(Rate);
		writer.Write((byte)Type);
	}
}
