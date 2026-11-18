using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class ReceiveMail : Packet
{
	public List<ClientMail> Mail = new List<ClientMail>();

	public override short Index => 229;

	protected override void ReadPacket(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Mail.Add(new ClientMail(reader));
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Mail.Count);
		for (int i = 0; i < Mail.Count; i++)
		{
			Mail[i].Save(writer);
		}
	}
}
