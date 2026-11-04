using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using ServerPackets;

namespace Server.MirObjects.Monsters
{

public class Mon621N : MonsterObject
{
	protected internal Mon621N(MonsterInfo info)
		: base(info)
	{
	}

	protected override bool InAttackRange()
	{
		if (Target.CurrentMap != base.CurrentMap)
		{
			return false;
		}
		if (base.CurrentMap == Target.CurrentMap)
		{
			return Functions.InRange(CurrentLocation, Target.CurrentLocation, Info.ViewRange);
		}
		return false;
	}

	protected override void Attack()
	{
		if (!Target.IsAttackTarget(this))
		{
			Target = null;
			return;
		}
		ShockTime = 0L;
		ActionTime = MapObject.Envir.Time + 600;
		AttackTime = MapObject.Envir.Time + AttackSpeed;
		Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
		if (MapObject.Envir.Random.Next(7) == 0)
		{
			Broadcast(new ObjectRangeAttack
			{
				ObjectID = ObjectID,
				Direction = Direction,
				Location = CurrentLocation,
				TargetID = Target.ObjectID,
				Type = 0
			});
			int attackPower = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]);
			if (attackPower != 0)
			{
				DelayedAction item = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + 300, Target, attackPower, DefenceType.MAC, true);
				ActionList.Add(item);
			}
			return;
		}
		Broadcast(new ObjectAttack
		{
			ObjectID = ObjectID,
			Direction = Direction,
			Location = CurrentLocation,
			Type = 0
		});
		int attackPower2 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
		if (attackPower2 != 0)
		{
			Broadcast(new ObjectEffect
			{
				ObjectID = Target.ObjectID,
				Effect = SpellEffect.Mon621NSpikes
			});
			DelayedAction item2 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 300, Target, attackPower2, DefenceType.ACAgility, false);
			ActionList.Add(item2);
		}
	}

	protected override void ProcessTarget()
	{
		if (Target == null || !CanAttack || MapObject.Envir.Time < ShockTime)
		{
			return;
		}
		int num = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation);
		if (num > 6)
		{
			MoveTo(Target.CurrentLocation);
			return;
		}
		if (num >= 3 && num <= 6)
		{
			if (InAttackRange())
			{
				Attack();
			}
			return;
		}
		MirDirection mirDirection = Functions.DirectionFromPoint(Target.CurrentLocation, CurrentLocation);
		if (Walk(mirDirection))
		{
			return;
		}
		bool flag = MapObject.Envir.Random.Next(2) == 0;
		for (int i = 0; i < 7; i++)
		{
			mirDirection = (flag ? Functions.NextDir(mirDirection) : Functions.PreviousDir(mirDirection));
			if (Walk(mirDirection))
			{
				break;
			}
		}
	}
}
}
