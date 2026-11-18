using System.IO;

namespace ServerPackets;

public sealed class StartGame : Packet
{
	public byte Result;

	public int Resolution;

	public override short Index => 14;

	protected override void ReadPacket(BinaryReader reader)
	{
		Result = reader.ReadByte();
		Resolution = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Result);
		writer.Write(Resolution);
	}
}
