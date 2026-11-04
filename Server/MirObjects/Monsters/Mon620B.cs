using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using ServerPackets;

namespace Server.MirObjects.Monsters
{

public class Mon620B : MonsterObject
{
	private byte _stage;

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

	protected internal Mon620B(MonsterInfo info)
		: base(info)
	{
	}

	protected override bool InAttackRange()
	{
		if (base.CurrentMap == Target.CurrentMap && CanFly(Target.CurrentLocation))
		{
			return Functions.InRange(CurrentLocation, Target.CurrentLocation, Info.ViewRange);
		}
		return false;
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
			if (Stats[Stat.HP] >= 5)
			{
				byte b = (byte)(5 - HP / (Stats[Stat.HP] / 5));
				if (b > _stage)
				{
					_stage = b;
					Broadcast(GetInfo());
				}
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
			Extra = Stoned,
			ExtraByte = _stage
		};
	}

	public override void Die()
	{
		base.Die();
	}

	protected override void Attack()
	{
		if (!Target.IsAttackTarget(this))
		{
			Target = null;
			return;
		}
		ShockTime = 0L;
		Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
		bool num = CurrentLocation == Target.CurrentLocation || !Functions.InRange(CurrentLocation, Target.CurrentLocation, 1);
		ActionTime = MapObject.Envir.Time + 300;
		AttackTime = MapObject.Envir.Time + AttackSpeed;
		if (!num && MapObject.Envir.Random.Next(2) > 0)
		{
			switch (MapObject.Envir.Random.Next(5))
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
					DelayedAction item2 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower2, DefenceType.AC);
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
					Type = 1
				});
				int attackPower4 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
				if (attackPower4 != 0)
				{
					PoisonTarget(Target, 5, 10L, PoisonType.Red, 1000L);
					DelayedAction item4 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower4, DefenceType.ACAgility);
					ActionList.Add(item4);
				}
				break;
			}
			case 2:
			{
				Broadcast(new ObjectAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					Type = 2
				});
				int attackPower5 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
				if (attackPower5 != 0)
				{
					FullmoonAttack(attackPower5);
					DelayedAction item5 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower5, DefenceType.ACAgility);
					ActionList.Add(item5);
				}
				break;
			}
			case 3:
			{
				Broadcast(new ObjectAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					Type = 3
				});
				int attackPower3 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
				if (attackPower3 != 0)
				{
					FullmoonAttack(attackPower3);
					DelayedAction item3 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower3, DefenceType.ACAgility);
					ActionList.Add(item3);
				}
				break;
			}
			case 4:
			{
				Broadcast(new ObjectAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					Type = 4
				});
				int attackPower = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
				if (attackPower != 0)
				{
					FullmoonAttack(attackPower);
					DelayedAction item = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower, DefenceType.ACAgility);
					ActionList.Add(item);
				}
				break;
			}
			}
		}
		else
		{
			Broadcast(new ObjectRangeAttack
			{
				ObjectID = ObjectID,
				Direction = Direction,
				Location = CurrentLocation,
				TargetID = Target.ObjectID
			});
			int attackPower6 = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]);
			if (attackPower6 != 0)
			{
				PoisonTarget(Target, 5, 7L, PoisonType.Green, 1000L);
				DelayedAction item6 = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + 300, Target, attackPower6, DefenceType.MACAgility, false);
				ActionList.Add(item6);
			}
		}
	}
}
}
