using System.IO;

namespace ClientPackets;

public sealed class GetRanking : Packet
{
	public byte RankType;

	public int RankIndex;

	public bool OnlineOnly;

	public override short Index => 132;

	protected override void ReadPacket(BinaryReader reader)
	{
		RankType = reader.ReadByte();
		RankIndex = reader.ReadInt32();
		OnlineOnly = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(RankType);
		writer.Write(RankIndex);
		writer.Write(OnlineOnly);
	}
}
