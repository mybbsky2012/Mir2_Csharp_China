using System.IO;

namespace ServerPackets;

public sealed class NPCCollectRefine : Packet
{
	public bool Success;

	public override short Index => 106;

	protected override void ReadPacket(BinaryReader reader)
	{
		Success = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Success);
	}
}
