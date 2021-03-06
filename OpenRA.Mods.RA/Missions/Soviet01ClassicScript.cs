#region Copyright & License Information
/*
 * Copyright 2007-2012 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.Mods.RA.Activities;
using OpenRA.Mods.RA.Air;
using OpenRA.Mods.RA.Buildings;
using OpenRA.Mods.RA.Move;
using OpenRA.Scripting;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Missions
{
	class Soviet01ClassicScriptInfo : TraitInfo<Soviet01ClassicScript>, Requires<SpawnMapActorsInfo> { }

	class Soviet01ClassicScript : IHasObjectives, IWorldLoaded, ITick
	{
		public event ObjectivesUpdatedEventHandler OnObjectivesUpdated = notify => { };

		public IEnumerable<Objective> Objectives { get { return objectives.Values; } }

		Dictionary<int, Objective> objectives = new Dictionary<int, Objective>
		{
			{ DestroyID, new Objective(ObjectiveType.Primary, Destroy, ObjectiveStatus.InProgress) }
		};

		const int DestroyID = 0;
		const string Destroy = "A pitiful excuse for resistance has blockaded itself in this village."
							+ " Stalin has decided to make an example of them. Kill them all and destroy their homes."
							+ " You will have Yak aircraft to use in teaching these rebels a lesson.";

		World world;

		Player ussr;
		Player france;

		Actor startJeep;
		Actor startJeepMovePoint;
		Actor church;
		bool startJeepParadropped;
		bool churchParadropped;

		Actor paradropPoint1;
		Actor paradropEntryPoint1;
		Actor paradropPoint2;
		Actor paradropEntryPoint2;

		Actor airfield1;
		Actor airfield2;
		Actor airfield3;
		Actor[] airfields;

		const string BadgerName = "badr";
		static readonly string[] Reinforcements = { "e1", "e1", "e1", "e2", "e2" };

		void MissionFailed()
		{
			if (ussr.WinState != WinState.Undefined)
			{
				return;
			}
			ussr.WinState = WinState.Lost;
			foreach (var actor in world.Actors.Where(a => a.IsInWorld && a.Owner == ussr && !a.IsDead()))
			{
				actor.Kill(actor);
			}
			Sound.Play("misnlst1.aud");
		}

		void MissionAccomplished()
		{
			if (ussr.WinState != WinState.Undefined)
			{
				return;
			}
			ussr.WinState = WinState.Won;
			Sound.Play("misnwon1.aud");
		}

		public void Tick(Actor self)
		{
			var unitsAndBuildings = world.Actors.Where(a => !a.IsDead() && a.IsInWorld && (a.HasTrait<Mobile>() || (a.HasTrait<Building>() && !a.HasTrait<Wall>())));
			if (!unitsAndBuildings.Any(a => a.Owner == france))
			{
				objectives[DestroyID].Status = ObjectiveStatus.Completed;
				MissionAccomplished();
			}
			else if (!unitsAndBuildings.Any(a => a.Owner == ussr))
			{
				objectives[DestroyID].Status = ObjectiveStatus.Failed;
				MissionFailed();
			}
			if (!startJeepParadropped && startJeep.IsDead())
			{
				Sound.Play("reinfor1.aud");
				MissionUtils.Paradrop(world, ussr, Reinforcements, paradropEntryPoint1.Location, paradropPoint1.Location);
				startJeepParadropped = true;
			}
			if (!churchParadropped && church.IsDead())
			{
				Sound.Play("reinfor1.aud");
				MissionUtils.Paradrop(world, ussr, Reinforcements, paradropEntryPoint2.Location, paradropPoint2.Location);
				churchParadropped = true;
			}
		}

		void LandYaks()
		{
			foreach (var airfield in airfields)
			{
				var entry = airfield.Location - new CVec(10, 0);
				var yak = world.CreateActor("yak", new TypeDictionary 
				{
					new OwnerInit(ussr),
					new LocationInit(entry),
					new FacingInit(Util.GetFacing(airfield.Location - entry, 0)),
					new AltitudeInit(Rules.Info["yak"].Traits.Get<PlaneInfo>().CruiseAltitude)
				});
				while (yak.Trait<LimitedAmmo>().TakeAmmo()) { }
				yak.QueueActivity(new ReturnToBase(yak, airfield));
				yak.QueueActivity(new ResupplyAircraft());
			}
		}

		void MoveJeep()
		{
			startJeep.QueueActivity(new Move.Move(startJeepMovePoint.Location, 0));
			startJeep.QueueActivity(new Turn(128));
			startJeep.QueueActivity(new CallFunc(() =>
			{
				var bridge = world.Actors
					.Where(a => a.HasTrait<Bridge>() && !a.IsDead())
					.OrderBy(a => (startJeep.CenterLocation - a.CenterLocation).LengthSquared)
					.First();
				Combat.DoExplosion(bridge, "Demolish", bridge.CenterLocation, 0);
				world.WorldActor.Trait<ScreenShaker>().AddEffect(15, bridge.CenterLocation.ToFloat2(), 6);
				bridge.Kill(bridge);
			}));
		}

		public void WorldLoaded(World w)
		{
			world = w;

			ussr = w.Players.Single(p => p.InternalName == "USSR");
			france = w.Players.Single(p => p.InternalName == "France");

			var actors = w.WorldActor.Trait<SpawnMapActors>().Actors;
			startJeep = actors["StartJeep"];
			startJeepMovePoint = actors["StartJeepMovePoint"];
			paradropPoint1 = actors["ParadropPoint1"];
			paradropEntryPoint1 = actors["ParadropEntryPoint1"];
			paradropPoint2 = actors["ParadropPoint2"];
			paradropEntryPoint2 = actors["ParadropEntryPoint2"];
			church = actors["Church"];
			airfield1 = actors["Airfield1"];
			airfield2 = actors["Airfield2"];
			airfield3 = actors["Airfield3"];
			airfields = new[] { airfield1, airfield2, airfield3 };

			Game.MoveViewport(startJeep.Location.ToFloat2());

			if (MissionUtils.IsSingleClient(world))
			{
				Media.PlayFMVFullscreen(w, "soviet1.vqa", () =>
				{
					LandYaks();
					MoveJeep();
					MissionUtils.PlayMissionMusic();
				});
			}
			else
			{
				LandYaks();
				MoveJeep();
				MissionUtils.PlayMissionMusic();
			}
		}
	}

	class Soviet01ClassicContainsActorsInfo : ITraitInfo
	{
		public readonly string[] Actors = { };

		public object Create(ActorInitializer init) { return new Soviet01ClassicContainsActors(this); }
	}

	class Soviet01ClassicContainsActors : INotifyDamage
	{
		bool spawned;
		Soviet01ClassicContainsActorsInfo info;

		public Soviet01ClassicContainsActors(Soviet01ClassicContainsActorsInfo info)
		{
			this.info = info;
		}

		public void Damaged(Actor self, AttackInfo e)
		{
			if (spawned || self.IsDead())
			{
				return;
			}
			foreach (var actor in info.Actors)
			{
				var unit = self.World.CreateActor(actor, new TypeDictionary
				{
					new OwnerInit(self.Owner),
					new LocationInit(self.Location)
				});
				unit.Trait<Mobile>().Nudge(unit, unit, true);
			}
			spawned = true;
		}
	}
}
