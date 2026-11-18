using System.Collections.Generic;

public class ItemSets
{
	public ItemSet Set;

	public List<ItemType> Type;

	public byte Count;

	private byte Amount
	{
		get
		{
			switch (Set)
			{
			case ItemSet.世轮套装:
			case ItemSet.绿翠套装:
			case ItemSet.道护套装:
			case ItemSet.贵人战套:
			case ItemSet.贵人法套:
			case ItemSet.贵人道套:
			case ItemSet.贵人刺套:
			case ItemSet.贵人弓套:
				return 2;
			case ItemSet.赤兰套装:
			case ItemSet.密火套装:
			case ItemSet.破碎套装:
			case ItemSet.幻魔石套:
			case ItemSet.灵玉套装:
			case ItemSet.五玄套装:
			case ItemSet.白骨套装:
			case ItemSet.虫血套装:
			case ItemSet.鏃未套装:
				return 3;
			case ItemSet.记忆套装:
			case ItemSet.神龙套装:
				return 4;
			case ItemSet.祈祷套装:
			case ItemSet.白金套装:
			case ItemSet.强白金套:
			case ItemSet.红玉套装:
			case ItemSet.强红玉套:
			case ItemSet.软玉套装:
			case ItemSet.强软玉套:
			case ItemSet.龙血套装:
			case ItemSet.监视套装:
			case ItemSet.暴压套装:
			case ItemSet.贝玉套装:
			case ItemSet.黑术套装:
			case ItemSet.青玉套装:
			case ItemSet.强青玉套:
			case ItemSet.圣龙套装:
				return 5;
			case ItemSet.天龙套装:
				return 8;
			default:
				return 0;
			}
		}
	}

	public bool SetComplete => Count >= Amount;
}
