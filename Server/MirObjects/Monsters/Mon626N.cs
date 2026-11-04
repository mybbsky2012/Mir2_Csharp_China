using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using ServerPackets;

namespace Server.MirObjects.Monsters
{

public class Mon626N : MonsterObject
{
	protected internal Mon626N(MonsterInfo info)
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
		if (Target == null || !Target.IsAttackTarget(this))
		{
			Target = null;
		}
		else
		{
			if (!CanAttack)
			{
				return;
			}
			ShockTime = 0L;
			Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
			bool num = CurrentLocation == Target.CurrentLocation || !Functions.InRange(CurrentLocation, Target.CurrentLocation, 1);
			ActionTime = MapObject.Envir.Time + 500;
			AttackTime = MapObject.Envir.Time + AttackSpeed;
			if (!num)
			{
				switch (MapObject.Envir.Random.Next(2))
				{
				case 0:
				{
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
						ActionList.Add(new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower2, DefenceType.AC, false));
					}
					break;
				}
				case 1:
				{
					JumpBack(3, random: true);
					int attackPower = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
					if (attackPower != 0)
					{
						ActionList.Add(new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower, DefenceType.AC, false));
					}
					break;
				}
				}
			}
			else
			{
				int attackPower3 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
				if (attackPower3 != 0 && LineCharge(7, broadcast: true))
				{
					WideLineAttack(attackPower3, 3, 1200);
					ActionList.Add(new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower3, DefenceType.AC, false));
				}
			}
		}
	}

	protected override void ProcessTarget()
	{
		if (Target != null)
		{
			if (InAttackRange() && CanAttack)
			{
				Attack();
			}
			else if (MapObject.Envir.Time < ShockTime)
			{
				Target = null;
			}
			else
			{
				MoveTo(Target.CurrentLocation);
			}
		}
	}

	public override void Die()
	{
		base.Die();
	}
}
}
