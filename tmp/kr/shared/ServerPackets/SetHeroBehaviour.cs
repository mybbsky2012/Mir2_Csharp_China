using System.IO;

namespace ServerPackets;

public sealed class SetHeroBehaviour : Packet
{
	public HeroBehaviour Behaviour;

	public override short Index => 181;

	protected override void ReadPacket(BinaryReader reader)
	{
		Behaviour = (HeroBehaviour)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Behaviour);
	}
}
