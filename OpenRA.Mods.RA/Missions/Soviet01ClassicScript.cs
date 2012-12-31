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
using OpenRA.Network;
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
		const string Destroy = "A pitiful excuse for resistance has blockaded itself in this village. Stalin has decided to make an example of them. Kill them all and destroy their homes. You will have Yak aircraft to use in teaching these rebels a lesson.";

		World world;

		Player ussr;
		Player france;

		Actor startJeep;
		Actor startJeepMovePoint;

		Actor airfield1;
		Actor airfield2;
		Actor airfield3;
		Actor[] airfields;

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
			startJeep.QueueActivity(new MoveAdjacentTo(Target.FromActor(startJeepMovePoint)));
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
			airfield1 = actors["Airfield1"];
			airfield2 = actors["Airfield2"];
			airfield3 = actors["Airfield3"];
			airfields = new[] { airfield1, airfield2, airfield3 };
			Game.MoveViewport(startJeep.Location.ToFloat2());
			Game.ConnectionStateChanged += StopMusic;
			Media.PlayFMVFullscreen(w, "soviet1.vqa", () =>
			{
				PlayMusic();
				LandYaks();
				MoveJeep();
			});
		}

		void PlayMusic()
		{
			if (!Rules.InstalledMusic.Any())
			{
				return;
			}
			var track = Rules.InstalledMusic.Random(Game.CosmeticRandom);
			Sound.PlayMusicThen(track.Value, PlayMusic);
		}

		void StopMusic(OrderManager orderManager)
		{
			if (!orderManager.GameStarted)
			{
				Sound.StopMusic();
				Game.ConnectionStateChanged -= StopMusic;
			}
		}
	}
}
