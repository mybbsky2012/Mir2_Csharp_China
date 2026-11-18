using System;
using System.IO;

namespace ServerPackets;

public sealed class LoginBanned : Packet
{
	public string Reason = string.Empty;

	public DateTime ExpiryDate;

	public override short Index => 8;

	protected override void ReadPacket(BinaryReader reader)
	{
		Reason = reader.ReadString();
		ExpiryDate = DateTime.FromBinary(reader.ReadInt64());
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Reason);
		writer.Write(ExpiryDate.ToBinary());
	}
}
