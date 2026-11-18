using System;
using System.IO;

namespace ServerPackets;

public sealed class ItemSealChanged : Packet
{
	public ulong UniqueID;

	public DateTime ExpiryDate;

	public override short Index => 114;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		ExpiryDate = DateTime.FromBinary(reader.ReadInt64());
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(ExpiryDate.ToBinary());
	}
}
