using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using ServerPackets;

namespace Server.MirObjects.Monsters
{

public class Mon649B : MonsterObject
{
	protected internal Mon649B(MonsterInfo info)
		: base(info)
	{
	}

	protected override bool InAttackRange()
	{
		if (Target == null || Target.CurrentMap != base.CurrentMap)
		{
			return false;
		}
		return Functions.MaxDistance(CurrentLocation, Target.CurrentLocation) <= 3;
	}

	protected override void Attack()
	{
		if (Target == null || !Target.IsAttackTarget(this))
		{
			Target = null;
			return;
		}
		int num = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation);
		if (num > 5)
		{
			return;
		}
		ShockTime = 0L;
		ActionTime = MapObject.Envir.Time + 300;
		AttackTime = MapObject.Envir.Time + AttackSpeed;
		Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
		int attackPower = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
		if (attackPower <= 0)
		{
			return;
		}
		if (num <= 1)
		{
			Broadcast(new ObjectAttack
			{
				ObjectID = ObjectID,
				Direction = Direction,
				Location = CurrentLocation,
				Type = 0
			});
			switch (MapObject.Envir.Random.Next(2))
			{
			case 0:
			{
				DelayedAction item = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 600, Target, attackPower, DefenceType.AC, false);
				ActionList.Add(item);
				break;
			}
			case 1:
				HalfmoonAttack(attackPower, 600);
				break;
			}
			return;
		}
		if (num >= 2 && num <= 3)
		{
			Broadcast(new ObjectRangeAttack
			{
				ObjectID = ObjectID,
				Direction = Direction,
				Location = CurrentLocation,
				TargetID = Target.ObjectID,
				Type = 0
			});
			switch (MapObject.Envir.Random.Next(3))
			{
			case 0:
				TriangleAttack(attackPower, 3, 1, 600);
				break;
			case 1:
				WideLineAttack(attackPower, 3, 600, DefenceType.AC);
				break;
			case 2:
				WideLineAttack(attackPower, 5, 600);
				break;
			}
		}
		if (num >= 2 && num <= 4)
		{
			Broadcast(new ObjectRangeAttack
			{
				ObjectID = ObjectID,
				Direction = Direction,
				Location = CurrentLocation,
				TargetID = Target.ObjectID,
				Type = 1
			});
			switch (MapObject.Envir.Random.Next(3))
			{
			case 0:
				TriangleAttack(attackPower, 3, 1, 600);
				break;
			case 1:
				WideLineAttack(attackPower, 3, 600, DefenceType.AC);
				break;
			case 2:
				WideLineAttack(attackPower, 5, 600);
				break;
			}
		}
		if (num >= 2 && num <= 5)
		{
			Broadcast(new ObjectRangeAttack
			{
				ObjectID = ObjectID,
				Direction = Direction,
				Location = CurrentLocation,
				TargetID = Target.ObjectID,
				Type = 2
			});
			switch (MapObject.Envir.Random.Next(3))
			{
			case 0:
				TriangleAttack(attackPower, 3, 1, 600);
				break;
			case 1:
				WideLineAttack(attackPower, 3, 600, DefenceType.AC);
				break;
			case 2:
				WideLineAttack(attackPower, 5, 600);
				break;
			}
		}
	}
}
}
