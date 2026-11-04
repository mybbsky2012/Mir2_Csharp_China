using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using ServerPackets;

namespace Server.MirObjects.Monsters
{

public class Mon615B : MonsterObject
{
	private long _mon615BShieldTime;

	protected internal Mon615B(MonsterInfo info)
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
		ActionTime = MapObject.Envir.Time + 300;
		AttackTime = MapObject.Envir.Time + AttackSpeed;
		ShockTime = 0L;
		Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
		if (!(CurrentLocation == Target.CurrentLocation) && Functions.InRange(CurrentLocation, Target.CurrentLocation, 1))
		{
			switch (MapObject.Envir.Random.Next(3))
			{
			case 0:
			{
				DefenceType defenceType2 = ((MapObject.Envir.Random.Next(2) == 0) ? DefenceType.AC : DefenceType.ACAgility);
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
					HalfmoonAttack(attackPower2, 300, defenceType2);
					DelayedAction item2 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 600, Target, attackPower2, defenceType2, false);
					ActionList.Add(item2);
				}
				break;
			}
			case 1:
				if (MapObject.Envir.Time >= _mon615BShieldTime + 90000)
				{
					Stats stats = new Stats();
					int duration;
					switch (base.HealthPercent)
					{
					default:
						return;
					case 61:
					case 62:
					case 63:
					case 64:
					case 65:
					case 66:
					case 67:
					case 68:
					case 69:
					case 70:
					case 71:
					case 72:
					case 73:
					case 74:
					case 75:
					case 76:
					case 77:
					case 78:
					case 79:
					case 80:
						stats[Stat.MaxAC] = 30;
						stats[Stat.MinAC] = 30;
						duration = 30000;
						break;
					case 31:
					case 32:
					case 33:
					case 34:
					case 35:
					case 36:
					case 37:
					case 38:
					case 39:
					case 40:
					case 41:
					case 42:
					case 43:
					case 44:
					case 45:
					case 46:
					case 47:
					case 48:
					case 49:
					case 50:
						stats[Stat.MaxAC] = 60;
						stats[Stat.MinAC] = 60;
						duration = 35000;
						break;
					case 20:
					case 21:
					case 22:
					case 23:
					case 24:
					case 25:
					case 26:
					case 27:
					case 28:
					case 29:
					case 30:
						stats[Stat.MaxAC] = 90;
						stats[Stat.MinAC] = 90;
						duration = 40000;
						break;
					}
					if (Target != null)
					{
						Broadcast(new ObjectAttack
						{
							ObjectID = ObjectID,
							Direction = Direction,
							Location = CurrentLocation,
							Type = 1
						});
						AddBuff(BuffType.Mon615BShield, this, duration, stats, true, false);
						_mon615BShieldTime = MapObject.Envir.Time;
					}
				}
				break;
			case 2:
			{
				DefenceType defenceType = ((MapObject.Envir.Random.Next(2) == 0) ? DefenceType.AC : DefenceType.ACAgility);
				Broadcast(new ObjectAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					Type = 2
				});
				int attackPower = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
				if (attackPower != 0)
				{
					FullmoonAttack(attackPower, 1200, defenceType, -1, 2);
					DelayedAction item = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 600, Target, attackPower, defenceType, false);
					ActionList.Add(item);
				}
				break;
			}
			}
		}
		else if (MapObject.Envir.Random.Next(8) != 7)
		{
			Broadcast(new ObjectRangeAttack
			{
				ObjectID = ObjectID,
				Direction = Direction,
				Location = CurrentLocation,
				TargetID = Target.ObjectID,
				Type = 0
			});
			int attackPower3 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
			if (attackPower3 != 0)
			{
				DelayedAction item3 = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + 800, Target, attackPower3, DefenceType.MACAgility, false);
				ActionList.Add(item3);
			}
		}
		else
		{
			Broadcast(new ObjectRangeAttack
			{
				ObjectID = ObjectID,
				Direction = Direction,
				Location = CurrentLocation,
				TargetID = Target.ObjectID,
				Type = 1
			});
			int attackPower4 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
			if (attackPower4 != 0)
			{
				PoisonTarget(Target, 7, 5L, PoisonType.Green, 1000L);
				DelayedAction item4 = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + 800, Target, attackPower4, DefenceType.MACAgility, false);
				ActionList.Add(item4);
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
}
}
