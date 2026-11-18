using System.IO;

namespace ServerPackets;

public sealed class HeroCreateRequest : Packet
{
	public bool[] CanCreateClass;

	public override short Index => 174;

	protected override void ReadPacket(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		CanCreateClass = new bool[num];
		for (int i = 0; i < num; i++)
		{
			CanCreateClass[i] = reader.ReadBoolean();
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(CanCreateClass.Length);
		for (int i = 0; i < CanCreateClass.Length; i++)
		{
			writer.Write(CanCreateClass[i]);
		}
	}
}
