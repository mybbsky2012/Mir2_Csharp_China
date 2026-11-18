using System.IO;

namespace ClientPackets;

public sealed class RequestUserName : Packet
{
	public uint UserID;

	public override short Index => 74;

	protected override void ReadPacket(BinaryReader reader)
	{
		UserID = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UserID);
	}
}
