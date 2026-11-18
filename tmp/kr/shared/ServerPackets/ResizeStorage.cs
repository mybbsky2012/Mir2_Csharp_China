using System;
using System.IO;

namespace ServerPackets;

public sealed class ResizeStorage : Packet
{
	public int Size;

	public bool HasExpandedStorage;

	public DateTime ExpiryTime;

	public override short Index => 236;

	protected override void ReadPacket(BinaryReader reader)
	{
		Size = reader.ReadInt32();
		HasExpandedStorage = reader.ReadBoolean();
		ExpiryTime = DateTime.FromBinary(reader.ReadInt64());
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Size);
		writer.Write(HasExpandedStorage);
		writer.Write(ExpiryTime.ToBinary());
	}
}
