using System.IO;

namespace ClientPackets;

public sealed class ClientVersion : Packet
{
	public byte[] VersionHash;

	public override short Index => 0;

	protected override void ReadPacket(BinaryReader reader)
	{
		VersionHash = reader.ReadBytes(reader.ReadInt32());
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(VersionHash.Length);
		writer.Write(VersionHash);
	}
}
