using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Game.Core;
using Game.Services;
using Game.Session.Player;
using Game.Session.Sim;

namespace AfterProhibitionPolitics
{
	internal static class CampaignElectionStateBridge
	{
		internal static CampaignElectionStateSummary Capture()
		{
			CampaignElectionStateSummary summary = new CampaignElectionStateSummary
			{
				Today = SafeToday(),
				Reason = "unreadable"
			};

			try
			{
				PoliticsManager politics = global::Game.Game.ctx?.simman?.politics;
				if (politics == null)
				{
					summary.Reason = "missing-politics-manager";
					return summary;
				}

				summary.ManagerFound = true;
				summary.Stage = SafeElectionStage(politics);
				AuditWards(politics, summary);
				AuditCampaignActionSettings(summary);
				summary.Reason = "readable";
				return summary;
			}
			catch (Exception ex)
			{
				summary.Reason = "error-" + ex.GetType().Name;
				return summary;
			}
		}

		internal static void LogStartupAudit(string source, ManualLogSource logger)
		{
			if (logger == null)
			{
				return;
			}

			try
			{
				CampaignElectionStateSummary summary = Capture();
				logger.LogInfo(
					"campaign-election-audit source=" + source +
					" manager=" + summary.ManagerFound +
					" today=" + summary.Today +
					" stage=" + summary.Stage +
					" wards=" + summary.Wards +
					" electionActive=" + summary.ElectionActive +
					" electionWards=" + summary.ElectionWards +
					" nominationWards=" + summary.NominationWards +
					" campaignWards=" + summary.CampaignWards +
					" electionWeekWards=" + summary.ElectionWeekWards +
					" postElectionWards=" + summary.PostElectionWards +
					" legislationWards=" + summary.LegislationWards +
					" candidates=" + summary.Candidates +
					" candidateStats=" + summary.CandidateStats +
					" humanSponsored=" + summary.HumanSponsoredCandidates +
					" humanSupported=" + summary.HumanSupportedCandidates +
					" totalVotes=" + summary.TotalVotes +
					" nextActions=" + summary.NextActionEntries +
					" electionEvents=" + summary.ElectionEvents +
					" playerActions=" + summary.PlayerActions +
					" playerActionsDisplay=" + summary.PlayerActionsDisplay +
					" playerActionsNpcOnly=" + summary.PlayerActionsNpcOnly +
					" playerActionsOncePerElection=" + summary.PlayerActionsOncePerElection +
					" playerActionsOncePerCandidate=" + summary.PlayerActionsOncePerCandidate +
					" largeActions=" + summary.LargeActions +
					" smallActions=" + summary.SmallActions +
					" scanErrors=" + summary.ScanErrors +
					" reason=" + summary.Reason);
			}
			catch (Exception ex)
			{
				logger.LogWarning("campaign-election-audit failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void AuditWards(PoliticsManager politics, CampaignElectionStateSummary summary)
		{
			IEnumerable<Ward> wards = politics.GetWards();
			if (wards == null)
			{
				return;
			}

			foreach (Ward ward in wards)
			{
				try
				{
					summary.Wards++;
					Election election = ward?.currElection;
					if (election == null)
					{
						continue;
					}

					summary.ElectionActive = true;
					summary.ElectionWards++;
					CountWardStage(election.stage, summary);

					if (election.allCandidates != null)
					{
						summary.Candidates += election.allCandidates.Count;
					}
					if (election.candidateStats != null)
					{
						foreach (KeyValuePair<EntityID, Election.CandidateInfo> entry in election.candidateStats)
						{
							Election.CandidateInfo stats = entry.Value;
							if (stats == null)
							{
								continue;
							}

							summary.CandidateStats++;
							summary.TotalVotes += stats.votes;
							if (stats.sponsor == PlayerID.HumanPlayer)
							{
								summary.HumanSponsoredCandidates++;
							}
							if (stats.sponsor == PlayerID.HumanPlayer && stats.support != Election.SupportLevel.None)
							{
								summary.HumanSupportedCandidates++;
							}
						}
					}
					if (election.nextAction != null)
					{
						summary.NextActionEntries += election.nextAction.Count;
					}
					if (election.electionLog != null)
					{
						summary.ElectionEvents += election.electionLog.Count;
					}
				}
				catch
				{
					summary.ScanErrors++;
				}
			}
		}

		private static void CountWardStage(Election.ElectionStage stage, CampaignElectionStateSummary summary)
		{
			if (stage == Election.ElectionStage.Nomination)
			{
				summary.NominationWards++;
			}
			else if (stage == Election.ElectionStage.Campaign)
			{
				summary.CampaignWards++;
			}
			else if (stage == Election.ElectionStage.ElectionWeek)
			{
				summary.ElectionWeekWards++;
			}
			else if (stage == Election.ElectionStage.PostElection)
			{
				summary.PostElectionWards++;
			}
			else if (stage == Election.ElectionStage.Legislation)
			{
				summary.LegislationWards++;
			}
		}

		private static void AuditCampaignActionSettings(CampaignElectionStateSummary summary)
		{
			try
			{
				PoliticsSettings.ActionSettings actions = global::Game.Game.serv?.globals?.settings?.politics?.npcCandidateAI;
				if (actions == null)
				{
					return;
				}

				summary.LargeActions = actions.largeActionDefs?.Count ?? 0;
				summary.SmallActions = actions.smallActionDefs?.Count ?? 0;
				summary.PlayerActions = actions.playerActionDefs?.Count ?? 0;

				if (actions.playerActionDefs == null)
				{
					return;
				}

				foreach (PoliticsSettings.CandidateAction action in actions.playerActionDefs)
				{
					if (action == null)
					{
						continue;
					}
					if (action.displayInConvo)
					{
						summary.PlayerActionsDisplay++;
					}
					if (action.NPCOnly)
					{
						summary.PlayerActionsNpcOnly++;
					}
					if (action.oncePerElection)
					{
						summary.PlayerActionsOncePerElection++;
					}
					if (action.oncePerCandidate)
					{
						summary.PlayerActionsOncePerCandidate++;
					}
				}
			}
			catch
			{
				summary.ScanErrors++;
			}
		}

		private static string SafeElectionStage(PoliticsManager politics)
		{
			try
			{
				return politics.GetElectionStage().ToString();
			}
			catch
			{
				return "unknown";
			}
		}

		private static int SafeToday()
		{
			try
			{
				return global::Game.Game.ctx?.clock?.Now.days ?? -1;
			}
			catch
			{
				return -1;
			}
		}
	}

	public sealed class CampaignElectionStateSummary
	{
		public bool ManagerFound { get; internal set; }
		public int Today { get; internal set; }
		public string Stage { get; internal set; }
		public int Wards { get; internal set; }
		public bool ElectionActive { get; internal set; }
		public int ElectionWards { get; internal set; }
		public int NominationWards { get; internal set; }
		public int CampaignWards { get; internal set; }
		public int ElectionWeekWards { get; internal set; }
		public int PostElectionWards { get; internal set; }
		public int LegislationWards { get; internal set; }
		public int Candidates { get; internal set; }
		public int CandidateStats { get; internal set; }
		public int HumanSponsoredCandidates { get; internal set; }
		public int HumanSupportedCandidates { get; internal set; }
		public int TotalVotes { get; internal set; }
		public int NextActionEntries { get; internal set; }
		public int ElectionEvents { get; internal set; }
		public int PlayerActions { get; internal set; }
		public int PlayerActionsDisplay { get; internal set; }
		public int PlayerActionsNpcOnly { get; internal set; }
		public int PlayerActionsOncePerElection { get; internal set; }
		public int PlayerActionsOncePerCandidate { get; internal set; }
		public int LargeActions { get; internal set; }
		public int SmallActions { get; internal set; }
		public int ScanErrors { get; internal set; }
		public string Reason { get; internal set; }

		public string FormatBridgeSummary()
		{
			return "campaign-election manager=" + ManagerFound +
				" today=" + Today +
				" stage=" + (Stage ?? "unknown") +
				" wards=" + Wards +
				" electionActive=" + ElectionActive +
				" electionWards=" + ElectionWards +
				" candidates=" + Candidates +
				" candidateStats=" + CandidateStats +
				" humanSponsored=" + HumanSponsoredCandidates +
				" humanSupported=" + HumanSupportedCandidates +
				" playerActions=" + PlayerActions +
				" playerActionsDisplay=" + PlayerActionsDisplay +
				" largeActions=" + LargeActions +
				" smallActions=" + SmallActions +
				" reason=" + (Reason ?? "unknown");
		}
	}
}
