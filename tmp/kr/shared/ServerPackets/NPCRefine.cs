using System.IO;

namespace ServerPackets;

public sealed class NPCRefine : Packet
{
	public float Rate;

	public bool Refining;

	public override short Index => 104;

	protected override void ReadPacket(BinaryReader reader)
	{
		Rate = reader.ReadSingle();
		Refining = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Rate);
		writer.Write(Refining);
	}
}
