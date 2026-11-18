using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class LogOutSuccess : Packet
{
	public List<SelectInfo> Characters = new List<SelectInfo>();

	public override short Index => 56;

	public override bool Observable => false;

	protected override void ReadPacket(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Characters.Add(new SelectInfo(reader));
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Characters.Count);
		for (int i = 0; i < Characters.Count; i++)
		{
			Characters[i].Save(writer);
		}
	}
}
