using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class NPCGoods : Packet
{
	public List<UserItem> List = new List<UserItem>();

	public float Rate;

	public PanelType Type;

	public bool HideAddedStats;

	public override short Index => 100;

	protected override void ReadPacket(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			List.Add(new UserItem(reader));
		}
		Rate = reader.ReadSingle();
		Type = (PanelType)reader.ReadByte();
		HideAddedStats = reader.ReadBoolean();
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
		writer.Write(HideAddedStats);
	}
}
