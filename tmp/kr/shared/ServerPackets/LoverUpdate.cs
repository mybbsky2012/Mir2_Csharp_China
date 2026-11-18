using System;
using System.IO;

namespace ServerPackets;

public sealed class LoverUpdate : Packet
{
	public string Name;

	public DateTime Date;

	public string MapName;

	public short MarriedDays;

	public override short Index => 244;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
		Date = DateTime.FromBinary(reader.ReadInt64());
		MapName = reader.ReadString();
		MarriedDays = reader.ReadInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
		writer.Write(Date.ToBinary());
		writer.Write(MapName);
		writer.Write(MarriedDays);
	}
}
