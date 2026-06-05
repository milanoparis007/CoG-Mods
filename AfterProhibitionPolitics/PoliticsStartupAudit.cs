using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Game.Core;
using Game.Services.Store;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.Session.Tutorial;

namespace AfterProhibitionPolitics
{
	internal static class PoliticsStartupAudit
	{
		internal static void LogStartupAudit(string source, ManualLogSource logger, int sampleLimit)
		{
			if (logger == null)
			{
				return;
			}

			try
			{
				PoliticsManager politics = global::Game.Game.ctx?.simman?.politics;
				if (politics == null)
				{
					logger.LogInfo("politics-audit source=" + source + " unavailable reason=missing-politics-manager");
					return;
				}

				List<Ward> wards = SafeGetWards(politics);
				PoliticsCounts counts = AuditWards(politics, wards, logger, source, sampleLimit);
				StarterQuestAudit starter = AuditStarterQuest(politics, wards);
				LawOfficeAudit lawOffice = AuditLawOffices();
				string mapId = global::Game.Game.ctx?.session?.mapconfig?.id ?? "(null)";
				string cityName = global::Game.Game.ctx?.session?.mapconfig?.CityName ?? "(null)";
				int turn = SafeCurrentTurn();
				bool procgenReady = SafeProcgenReady();
				bool shadowGovernment = SafeIsShadowGovernmentInstalled();
				string electionStage = SafeElectionStage(politics);

				logger.LogInfo(
					"politics-audit source=" + source +
					" map=" + mapId +
					" city=" + cityName +
					" turn=" + turn +
					" procgenReady=" + procgenReady +
					" shadowGovernment=" + shadowGovernment +
					" manager=True" +
					" wards=" + counts.Wards +
					" currentPoliticians=" + counts.CurrentPoliticians +
					" validCurrentPoliticians=" + counts.ValidCurrentPoliticians +
					" localPoliticians=" + counts.LocalPoliticians +
					" wardsNoCurrent=" + counts.WardsNoCurrent +
					" wardsNoLocal=" + counts.WardsNoLocal +
					" electionWards=" + counts.ElectionWards +
					" electionCandidates=" + counts.ElectionCandidates +
					" electionStage=" + electionStage +
					" scanErrors=" + counts.ScanErrors);

				logger.LogInfo(
					"politics-starter-audit source=" + source +
					" map=" + mapId +
					" questId=" + starter.QuestId +
					" active=" + starter.Active +
					" waiting=" + starter.Waiting +
					" completed=" + starter.Completed +
					" known=" + starter.Known +
					" safehouseWard=" + starter.SafehouseWard +
					" safehousePolitician=" + starter.SafehousePolitician +
					" safehousePoliticianValid=" + starter.SafehousePoliticianValid +
					" fallbackPolitician=" + starter.FallbackPolitician +
					" fallbackPoliticianValid=" + starter.FallbackPoliticianValid +
					" readyForStarter=" + starter.ReadyForStarter +
					" reason=" + starter.Reason);

				logger.LogInfo(
					"law-office-audit source=" + source +
					" scanned=" + lawOffice.Scanned +
					" templateMatches=" + lawOffice.TemplateMatches +
					" moduleMatches=" + lawOffice.ModuleMatches +
					" playerOwned=" + lawOffice.PlayerOwned +
					" scanErrors=" + lawOffice.ScanErrors);
			}
			catch (Exception ex)
			{
				logger.LogWarning("politics-audit failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static List<Ward> SafeGetWards(PoliticsManager politics)
		{
			try
			{
				return politics?.GetWards()?.Where(ward => ward != null).ToList() ?? new List<Ward>();
			}
			catch
			{
				return new List<Ward>();
			}
		}

		private static PoliticsCounts AuditWards(PoliticsManager politics, List<Ward> wards, ManualLogSource logger, string source, int sampleLimit)
		{
			PoliticsCounts counts = new PoliticsCounts();
			int logged = 0;

			foreach (Ward ward in wards)
			{
				try
				{
					counts.Wards++;
					bool currentValid = IsValidPolitician(politics, ward.currentPolitician);
					if (ward.currentPolitician.IsValid)
					{
						counts.CurrentPoliticians++;
					}
					else
					{
						counts.WardsNoCurrent++;
					}
					if (currentValid)
					{
						counts.ValidCurrentPoliticians++;
					}

					int localCount = ward.localPoliticians?.Count ?? 0;
					counts.LocalPoliticians += localCount;
					if (localCount <= 0)
					{
						counts.WardsNoLocal++;
					}
					if (ward.ElectionOngoing)
					{
						counts.ElectionWards++;
						counts.ElectionCandidates += ward.currElection?.allCandidates?.Count ?? 0;
					}

					if (logged < sampleLimit && (!currentValid || localCount <= 0 || ward.ElectionOngoing))
					{
						logged++;
						logger.LogInfo(
							"politics-ward-sample source=" + source +
							" ward=" + ward.id.id +
							" current=" + IdText(ward.currentPolitician) +
							" currentValid=" + currentValid +
							" local=" + localCount +
							" election=" + ward.ElectionOngoing +
							" stage=" + (ward.currElection != null ? ward.currElection.stage.ToString() : "None") +
							" candidates=" + (ward.currElection?.allCandidates?.Count ?? 0));
					}
				}
				catch
				{
					counts.ScanErrors++;
				}
			}

			return counts;
		}

		private static StarterQuestAudit AuditStarterQuest(PoliticsManager politics, List<Ward> wards)
		{
			StarterQuestAudit audit = new StarterQuestAudit
			{
				QuestId = TutorialManager.QUEST_NY_POLITICS_STARTER,
				SafehouseWard = -1,
				SafehousePolitician = "invalid",
				FallbackPolitician = "invalid",
				Reason = "unknown"
			};

			try
			{
				audit.Active = global::Game.Game.ctx.quests.IsQuestActiveByID(audit.QuestId, EntityID.INVALID);
				audit.Waiting = global::Game.Game.ctx.quests.IsQuestWaitingByID(audit.QuestId, EntityID.INVALID);
				audit.Completed = global::Game.Game.ctx.quests.IsQuestCompletedByID(audit.QuestId, EntityID.INVALID);
				audit.Known = audit.Active || audit.Waiting || audit.Completed;
			}
			catch
			{
				audit.Reason = "quest-state-unreadable";
				return audit;
			}

			Ward safehouseWard = ResolveSafehouseWard(politics);
			if (safehouseWard != null)
			{
				audit.SafehouseWard = safehouseWard.id.id;
				audit.SafehousePolitician = IdText(safehouseWard.currentPolitician);
				audit.SafehousePoliticianValid = IsValidPolitician(politics, safehouseWard.currentPolitician);
			}

			EntityID fallback = ResolveFallbackStarterPolitician(politics, wards);
			audit.FallbackPolitician = IdText(fallback);
			audit.FallbackPoliticianValid = IsValidPolitician(politics, fallback);

			string mapId = global::Game.Game.ctx?.session?.mapconfig?.id;
			bool isNewYork = string.Equals(mapId, "new-york", StringComparison.OrdinalIgnoreCase);
			bool procgenReady = SafeProcgenReady();
			audit.ReadyForStarter = isNewYork && procgenReady && !audit.Known && audit.FallbackPoliticianValid;

			if (!isNewYork)
			{
				audit.Reason = "not-new-york";
			}
			else if (!procgenReady)
			{
				audit.Reason = "procgen-not-ready";
			}
			else if (audit.Known)
			{
				audit.Reason = "quest-known";
			}
			else if (!audit.FallbackPoliticianValid)
			{
				audit.Reason = "no-valid-politician";
			}
			else
			{
				audit.Reason = "starter-ready";
			}

			return audit;
		}

		private static Ward ResolveSafehouseWard(PoliticsManager politics)
		{
			try
			{
				Entity safehouse = global::Game.Game.ctx?.players?.Human?.territory?.Safehouse.FindEntity();
				PrecinctID precinct = safehouse?.data?.board?.bead.nodeId.FindNode()?.precinctId ?? PrecinctID.INVALID;
				return politics.GetWardForID(precinct);
			}
			catch
			{
				return null;
			}
		}

		private static EntityID ResolveFallbackStarterPolitician(PoliticsManager politics, List<Ward> wards)
		{
			Ward safehouseWard = ResolveSafehouseWard(politics);
			if (safehouseWard != null && IsValidPolitician(politics, safehouseWard.currentPolitician))
			{
				return safehouseWard.currentPolitician;
			}

			foreach (Ward ward in wards)
			{
				if (IsValidPolitician(politics, ward.currentPolitician))
				{
					return ward.currentPolitician;
				}
			}

			foreach (Ward ward in wards)
			{
				if (ward.localPoliticians == null)
				{
					continue;
				}
				foreach (EntityID politician in ward.localPoliticians)
				{
					if (IsValidPolitician(politics, politician))
					{
						return politician;
					}
				}
			}

			return EntityID.INVALID;
		}

		private static bool IsValidPolitician(PoliticsManager politics, EntityID politician)
		{
			try
			{
				return politician.IsValid
					&& politician.FindEntity() != null
					&& politics.GetPoliticianData(politician) != null;
			}
			catch
			{
				return false;
			}
		}

		private static LawOfficeAudit AuditLawOffices()
		{
			LawOfficeAudit audit = new LawOfficeAudit();

			try
			{
				IEnumerable<Entity> buildings = global::Game.Game.ctx?.entityman?.GetCachedEntitiesBuildingsUnsafe();
				if (buildings == null)
				{
					return audit;
				}

				foreach (Entity building in buildings)
				{
					try
					{
						audit.Scanned++;
						Entity biz = BuildingUtil.FindBizForBuilding(building);
						string buildingTemplate = GetTemplateString(building);
						string bizTemplate = GetTemplateString(biz);
						bool templateMatch = ContainsAny(buildingTemplate, "lawoffice", "law-office", "law_office")
							|| ContainsAny(bizTemplate, "lawoffice", "law-office", "law_office");
						bool moduleMatch = HasLawOfficeModule(building.components?.modules);

						if (templateMatch)
						{
							audit.TemplateMatches++;
						}
						if (moduleMatch)
						{
							audit.ModuleMatches++;
						}
						if ((templateMatch || moduleMatch) && building.data?.building != null && building.data.building.controlled.Get().IsHumanPlayer)
						{
							audit.PlayerOwned++;
						}
					}
					catch
					{
						audit.ScanErrors++;
					}
				}
			}
			catch
			{
				audit.ScanErrors++;
			}

			return audit;
		}

		private static bool HasLawOfficeModule(ModulesComponent modules)
		{
			try
			{
				List<IModule> slots = modules?.GetAllSlotsUnsafe();
				if (slots == null)
				{
					return false;
				}

				foreach (IModule module in slots)
				{
					string id = module?.ModuleConfig?.Id.String;
					if (ContainsAny(id, "lawoffice", "law-office", "law_office", "legal-lawoffice"))
					{
						return true;
					}
				}
			}
			catch
			{
			}

			return false;
		}

		private static int SafeCurrentTurn()
		{
			try
			{
				return global::Game.Game.ctx?.clock?.CurrentTurn ?? -1;
			}
			catch
			{
				return -1;
			}
		}

		private static bool SafeProcgenReady()
		{
			try
			{
				return global::Game.Game.ctx.clock.CurrentTurn >= 1
					&& global::Game.Game.ctx.clock.Now >= global::Game.Game.ctx.clock.LastDayOfProcGen;
			}
			catch
			{
				return false;
			}
		}

		private static bool SafeIsShadowGovernmentInstalled()
		{
			try
			{
				return global::Game.Game.serv?.store != null && global::Game.Game.serv.store.IsPackInstalled(PackID.ShadowGovernment);
			}
			catch
			{
				return false;
			}
		}

		private static string SafeElectionStage(PoliticsManager politics)
		{
			try
			{
				return politics.GetElectionStage().ToString();
			}
			catch (Exception ex)
			{
				return "unreadable-" + ex.GetType().Name;
			}
		}

		private static bool ContainsAny(string value, params string[] needles)
		{
			if (string.IsNullOrEmpty(value) || needles == null)
			{
				return false;
			}

			foreach (string needle in needles)
			{
				if (!string.IsNullOrEmpty(needle) && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			return false;
		}

		private static string GetTemplateString(Entity entity)
		{
			return entity?.config?.Template.String ?? "(null)";
		}

		private static string IdText(EntityID id)
		{
			return id.IsValid ? id.id.ToString() : "invalid";
		}

		private sealed class PoliticsCounts
		{
			public int Wards;
			public int CurrentPoliticians;
			public int ValidCurrentPoliticians;
			public int LocalPoliticians;
			public int WardsNoCurrent;
			public int WardsNoLocal;
			public int ElectionWards;
			public int ElectionCandidates;
			public int ScanErrors;
		}

		private sealed class StarterQuestAudit
		{
			public string QuestId;
			public bool Active;
			public bool Waiting;
			public bool Completed;
			public bool Known;
			public int SafehouseWard;
			public string SafehousePolitician;
			public bool SafehousePoliticianValid;
			public string FallbackPolitician;
			public bool FallbackPoliticianValid;
			public bool ReadyForStarter;
			public string Reason;
		}

		private sealed class LawOfficeAudit
		{
			public int Scanned;
			public int TemplateMatches;
			public int ModuleMatches;
			public int PlayerOwned;
			public int ScanErrors;
		}
	}
}
