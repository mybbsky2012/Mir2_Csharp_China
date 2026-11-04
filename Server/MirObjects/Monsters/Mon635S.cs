using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using ServerPackets;

namespace Server.MirObjects.Monsters
{

public class Mon635S : MonsterObject
{
	public bool Stoned = true;

	protected override bool CanMove
	{
		get
		{
			if (base.CanMove)
			{
				return !Stoned;
			}
			return false;
		}
	}

	protected override bool CanAttack
	{
		get
		{
			if (base.CanAttack)
			{
				return !Stoned;
			}
			return false;
		}
	}

	protected internal Mon635S(MonsterInfo info)
		: base(info)
	{
	}

	public override int Pushed(MapObject pusher, MirDirection dir, int distance)
	{
		if (!Stoned)
		{
			return base.Pushed(pusher, dir, distance);
		}
		return 0;
	}

	public override void ApplyPoison(Poison p, MapObject Caster = null, bool NoResist = false, bool ignoreDefence = true)
	{
		if (!Stoned)
		{
			base.ApplyPoison(p, Caster, NoResist, ignoreDefence);
		}
	}

	public override Buff AddBuff(BuffType type, MapObject owner, int duration, Stats stats, bool refreshStats = true, bool updateOnly = false, params int[] values)
	{
		if (Stoned)
		{
			return null;
		}
		return base.AddBuff(type, owner, duration, stats, refreshStats, updateOnly, values);
	}

	public override bool IsFriendlyTarget(HumanObject ally)
	{
		if (Stoned)
		{
			return false;
		}
		return base.IsFriendlyTarget(ally);
	}

	protected override void ProcessAI()
	{
		if (!Dead && MapObject.Envir.Time > ActionTime)
		{
			bool flag = !FindNearby(2);
			if (Stoned && !flag)
			{
				Wake();
			}
		}
		base.ProcessAI();
	}

	public void Wake()
	{
		if (Stoned)
		{
			Stoned = false;
			Broadcast(new ObjectShow
			{
				ObjectID = ObjectID
			});
			ActionTime = MapObject.Envir.Time + 1000;
		}
	}

	public override bool IsAttackTarget(MonsterObject attacker)
	{
		if (!Stoned)
		{
			return base.IsAttackTarget(attacker);
		}
		return false;
	}

	public override bool IsAttackTarget(HumanObject attacker)
	{
		if (!Stoned)
		{
			return base.IsAttackTarget(attacker);
		}
		return false;
	}

	public override Packet GetInfo()
	{
		return new ObjectMonster
		{
			ObjectID = ObjectID,
			Name = Name,
			NameColour = NameColour,
			Location = CurrentLocation,
			Image = Info.Image,
			Direction = Direction,
			Effect = Info.Effect,
			AI = Info.AI,
			Light = Info.Light,
			Dead = Dead,
			Skeleton = Harvested,
			Poison = CurrentPoison,
			Hidden = base.Hidden,
			Extra = Stoned
		};
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
			ActionTime = MapObject.Envir.Time + 600;
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
					if (attackPower2 != 0 && (MapObject.Envir.Random.Next(100) >= 80 || !HalfmoonAttack(attackPower2, 1200, DefenceType.AC)))
					{
						DelayedAction item2 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower2, DefenceType.AC, false);
						ActionList.Add(item2);
					}
					break;
				}
				case 1:
				{
					Broadcast(new ObjectAttack
					{
						ObjectID = ObjectID,
						Direction = Direction,
						Location = CurrentLocation,
						Type = 0
					});
					int attackPower = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
					if (attackPower != 0)
					{
						PoisonTarget(Target, 5, 5L, PoisonType.Frozen, 1000L, noResist: true, ignoreDefence: false);
						if (MapObject.Envir.Random.Next(100) >= 80 || !HalfmoonAttack(attackPower, 1200))
						{
							DelayedAction item = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower, DefenceType.AC, false);
							ActionList.Add(item);
						}
					}
					break;
				}
				}
				return;
			}
			switch (MapObject.Envir.Random.Next(2))
			{
			case 0:
			{
				Broadcast(new ObjectRangeAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					TargetID = Target.ObjectID,
					Type = 0
				});
				int attackPower4 = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]);
				if (attackPower4 > 0)
				{
					TargetAreaAttack(Spell.Mon635SIceWhirlwind, attackPower4, 3, 500, 3000, 1500, DefenceType.MAC);
					int num3 = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation) * 60 + 1200;
					DelayedAction item4 = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + num3, Target, attackPower4, DefenceType.MACAgility, false);
					ActionList.Add(item4);
				}
				break;
			}
			case 1:
			{
				Broadcast(new ObjectRangeAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					TargetID = Target.ObjectID,
					Type = 1
				});
				int attackPower3 = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]);
				if (attackPower3 > 0)
				{
					int num2 = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation) * 60 + 1200;
					DelayedAction item3 = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + num2, Target, attackPower3, DefenceType.MACAgility, false);
					ActionList.Add(item3);
				}
				break;
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
				MoveTo(Target.Front);
			}
		}
	}

	public override void Die()
	{
		base.Die();
	}
}
}
