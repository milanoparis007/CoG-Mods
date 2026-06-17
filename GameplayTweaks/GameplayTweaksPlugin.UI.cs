using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session.Convo;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{

	private static class CrewOutingEvent
	{
		private static GangMeetingTier GetCurrentTier()
		{
			int gangMeetingTier = SaveData.GangMeetingTier;
			if (gangMeetingTier < 0 || gangMeetingTier > 2)
			{
				gangMeetingTier = 0;
				SaveData.GangMeetingTier = gangMeetingTier;
			}
			return (GangMeetingTier)gangMeetingTier;
		}

		private static string GetTierLabel(GangMeetingTier tier)
		{
			switch (tier)
			{
			case GangMeetingTier.Standard:
				return "Standard";
			case GangMeetingTier.Luxury:
				return "Luxury";
			default:
				return "Low";
			}
		}

		private static int GetPerCrewCost(GangMeetingTier tier)
		{
			switch (tier)
			{
			case GangMeetingTier.Standard:
				return 25;
			case GangMeetingTier.Luxury:
				return 50;
			default:
				return 10;
			}
		}

		private static float GetHappinessGain(GangMeetingTier tier)
		{
			switch (tier)
			{
			case GangMeetingTier.Standard:
				return GANG_MEETING_HAPPINESS_GAIN_STANDARD;
			case GangMeetingTier.Luxury:
				return GANG_MEETING_HAPPINESS_GAIN_LUXURY;
			default:
				return GANG_MEETING_HAPPINESS_GAIN_LOW;
			}
		}

		internal static string GetCurrentTierLabelForUi()
		{
			return GetTierLabel(GetCurrentTier());
		}

		internal static int GetCurrentTierPerCrewCostForUi()
		{
			return GetPerCrewCost(GetCurrentTier());
		}

		internal static bool CanAffordCurrentTierMeeting(PlayerInfo player, out int crewCount, out int cost, out int cleanCash, out int dirtyCash, out int totalCash)
		{
			crewCount = 0;
			cost = 0;
			cleanCash = 0;
			dirtyCash = 0;
			totalCash = 0;
			if (player == null || player.crew == null)
			{
				return false;
			}
			crewCount = Mathf.Max(0, player.crew.LivingCrewCount);
			cost = crewCount * GetPerCrewCost(GetCurrentTier());
			cleanCash = GetPlayerCleanCash();
			dirtyCash = ShouldDeferDirtyCashRuntime() ? 0 : GetTotalDirtyCash();
			totalCash = cleanCash + dirtyCash;
			return crewCount > 0 && totalCash >= cost;
		}

		internal static void TryRunGangMeetingCycle(PlayerInfo humanPlayer, bool showPrompt, string sourceTag)
		{
			int nowDay = G.GetNow().days;
			int crewCount;
			int cost;
			int cleanCash;
			int dirtyCash;
			int totalCash;
			bool affordable = CanAffordCurrentTierMeeting(humanPlayer, out crewCount, out cost, out cleanCash, out dirtyCash, out totalCash);
			CrewRelationshipHandlerPatch.LogGangMeetingCheckResult(nowDay, affordable, crewCount, cost, cleanCash, dirtyCash, totalCash, affordable ? "eligible" : "insufficient-funds");
			if (!affordable)
			{
				string reason = showPrompt ? "promptSkipped" : "autoSkipped";
				VerificationLog("GangMeeting", $"{reason} reason=insufficient-funds day={nowDay}");
				return;
			}
			if (showPrompt)
			{
				try
				{
					ShowOutingPrompt();
					VerificationLog("GangMeeting", $"promptShown reason=eligible day={nowDay}");
				}
				catch (Exception arg)
				{
					Debug.LogError($"[GameplayTweaks] Outing prompt failed: {arg}");
				}
				return;
			}
			if (TryExecuteGangMeetingEffects(humanPlayer, sourceTag, showSnitchPopup: true))
			{
				VerificationLog("GangMeeting", $"autoApplied source={sourceTag} day={nowDay} crew={crewCount} cost={cost}");
			}
			else
			{
				VerificationLog("GangMeeting", $"autoSkipped reason=execution-failed source={sourceTag} day={nowDay}");
			}
		}

		internal static bool TryExecuteGangMeetingEffects(PlayerInfo humanPlayer, string sourceTag, bool showSnitchPopup)
		{
			try
			{
				if (humanPlayer == null || humanPlayer.crew == null)
				{
					return false;
				}
				PlayerCrew crew = humanPlayer.crew;
				int livingCrewCount = crew.LivingCrewCount;
				GangMeetingTier currentTier = GetCurrentTier();
				int perCrewCost = GetPerCrewCost(currentTier);
				float happinessGain = GetHappinessGain(currentTier);
				int totalCost = livingCrewCount * perCrewCost;
				int playerCleanCash = GetPlayerCleanCash();
				int totalDirtyCash = ShouldDeferDirtyCashRuntime() ? 0 : GetTotalDirtyCash();
				int totalAvailable = playerCleanCash + totalDirtyCash;
				if (livingCrewCount <= 0 || totalAvailable < totalCost)
				{
					Debug.Log($"[GameplayTweaks] Can't afford gang meeting: need ${totalCost}, have ${totalAvailable} (clean=${playerCleanCash}, dirty=${totalDirtyCash})");
					return false;
				}
				int cleanSpend = Math.Min(playerCleanCash, totalCost);
				if (cleanSpend > 0)
				{
					humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-cleanSpend)), (MoneyReason)1);
				}
				int dirtySpend = totalCost - cleanSpend;
				if (dirtySpend > 0 && !ShouldDeferDirtyCashRuntime())
				{
					EntityID safehouse = humanPlayer.territory.Safehouse;
					if (!safehouse.IsNotValid)
					{
						Entity safehouseEntity = EntityIDExtensions.FindEntity(safehouse);
						if (safehouseEntity != null)
						{
							RemoveDirtyCash(safehouseEntity, dirtySpend);
						}
					}
				}
				foreach (CrewAssignment living in crew.GetLiving())
				{
					Entity peep = living.GetPeep();
					if (peep == null)
					{
						continue;
					}
					CrewModState state = GetOrCreateCrewState(peep.Id);
					if (state == null)
					{
						continue;
					}
					float previousHappiness = state.HappinessValue;
					state.HappinessValue = Mathf.Clamp01(state.HappinessValue + happinessGain);
					if (previousHappiness <= ModConstants.UNHAPPY_THRESHOLD && state.HappinessValue > ModConstants.UNHAPPY_THRESHOLD)
					{
						state.TurnsUnhappy = 0;
					}
				}
				List<Entity> exposedSnitches = TryRevealSnitchesFromGangMeeting(humanPlayer);
				if (exposedSnitches.Count > 0)
				{
					List<string> exposedNames = exposedSnitches.Where((Entity p) => p != null && p.data?.person != null).Select((Entity p) => p.data.person.FullName).Where((string n) => !string.IsNullOrEmpty(n)).ToList();
					if (showSnitchPopup && exposedNames.Count > 0)
					{
						CrewRelationshipHandlerPatch.ShowSnitchFoundAlert(exposedNames);
					}
					VerificationLog("SnitchReveal", $"exposed count={exposedSnitches.Count} source={sourceTag}");
				}
				Debug.Log($"[GameplayTweaks] Gang meeting applied source={sourceTag} tier={GetTierLabel(currentTier)} spent=${totalCost} crew={livingCrewCount} gain={happinessGain:0.00}");
				return true;
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] Gang meeting execution failed source={sourceTag}: {arg}");
				return false;
			}
		}

		/// <summary>Execute gang meeting effects for any gang (e.g. AI) using that gang's cash; no UI popup.</summary>
		internal static bool TryExecuteGangMeetingEffectsForGang(PlayerInfo gang, string sourceTag, bool runSnitchReveal)
		{
			try
			{
				if (gang == null || gang.crew == null)
				{
					return false;
				}
				PlayerCrew crew = gang.crew;
				int livingCrewCount = crew.LivingCrewCount;
				GangMeetingTier currentTier = GetCurrentTier();
				int perCrewCost = GetPerCrewCost(currentTier);
				float happinessGain = GetHappinessGain(currentTier);
				int totalCost = livingCrewCount * perCrewCost;
				int cleanCash = GetGangCleanCash(gang);
				int dirtyCash = ShouldDeferDirtyCashRuntime() ? 0 : GetGangDirtyCash(gang);
				int totalAvailable = cleanCash + dirtyCash;
				if (livingCrewCount <= 0 || totalAvailable < totalCost)
				{
					return false;
				}
				int cleanSpend = Math.Min(cleanCash, totalCost);
				if (cleanSpend > 0)
				{
					gang.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-cleanSpend)), (MoneyReason)1);
				}
				int dirtySpend = totalCost - cleanSpend;
				if (dirtySpend > 0 && !ShouldDeferDirtyCashRuntime() && gang.territory != null)
				{
					EntityID safehouse = gang.territory.Safehouse;
					if (!safehouse.IsNotValid)
					{
						Entity safehouseEntity = EntityIDExtensions.FindEntity(safehouse);
						if (safehouseEntity != null)
						{
							RemoveDirtyCash(safehouseEntity, dirtySpend);
						}
					}
				}
				foreach (CrewAssignment living in crew.GetLiving())
				{
					Entity peep = living.GetPeep();
					if (peep == null) continue;
					CrewModState state = GetOrCreateCrewState(peep.Id);
					if (state == null) continue;
					float previousHappiness = state.HappinessValue;
					state.HappinessValue = Mathf.Clamp01(state.HappinessValue + happinessGain);
					if (previousHappiness <= ModConstants.UNHAPPY_THRESHOLD && state.HappinessValue > ModConstants.UNHAPPY_THRESHOLD)
					{
						state.TurnsUnhappy = 0;
					}
				}
				if (runSnitchReveal)
				{
					TryRevealSnitchesFromGangMeetingForGang(gang, logGrapevine: false);
				}
				Debug.Log($"[GameplayTweaks] AI gang meeting applied gang={gang.PID.id} source={sourceTag} tier={GetTierLabel(currentTier)} spent={totalCost} crew={livingCrewCount}");
				return true;
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] TryExecuteGangMeetingEffectsForGang failed gang={gang?.PID.id} source={sourceTag}: {arg}");
				return false;
			}
		}

		public static void ShowOutingPrompt(GameObject ignored = null)
		{

			if ((UnityEngine.Object)(object)CrewRelationshipHandlerPatch._outingPopup != (UnityEngine.Object)null)
			{
				CrewRelationshipHandlerPatch._outingPopup.SetActive(true);
				ForceDockPopupFarLeft(CrewRelationshipHandlerPatch._outingPopup);
				CrewRelationshipHandlerPatch._outingPopup.transform.SetAsLastSibling();
				RefreshOutingText();
				return;
			}
			Canvas orCreateOverlayCanvas = GetOrCreateOverlayCanvas();
			if ((UnityEngine.Object)(object)orCreateOverlayCanvas == (UnityEngine.Object)null)
			{
				Debug.LogWarning("[GameplayTweaks] Could not create overlay canvas for outing popup");
				return;
			}
			GameObject val = new GameObject("OutingEventPopup", new Type[5]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(VerticalLayoutGroup),
				typeof(Canvas),
				typeof(GraphicRaycaster)
			});
			val.transform.SetParent(((Component)orCreateOverlayCanvas).transform, false);
			Canvas component = val.GetComponent<Canvas>();
			component.overrideSorting = true;
			component.sortingOrder = 999;
			RectTransform component2 = val.GetComponent<RectTransform>();
			component2.anchorMin = new Vector2(0.5f, 0.7f);
			component2.anchorMax = new Vector2(0.5f, 0.7f);
			component2.pivot = new Vector2(0.5f, 0.5f);
			component2.sizeDelta = ScalePopupSize(380f, 198f);
			CrewRelationshipHandlerPatch.ApplyPopupPanelTheme(val, new Color(0.11f, 0.12f, 0.14f, 0.98f));
			VerticalLayoutGroup component3 = val.GetComponent<VerticalLayoutGroup>();
			((LayoutGroup)component3).padding = new RectOffset(14, 14, 12, 12);
			((HorizontalOrVerticalLayoutGroup)component3).spacing = 8f;
			((HorizontalOrVerticalLayoutGroup)component3).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component3).childForceExpandHeight = false;
			Text obj2 = CrewRelationshipHandlerPatch.CreateLabel(val.transform, "OE_Title", "-- Gang Meeting Proposal --", 15, (FontStyle)1);
			obj2.alignment = (TextAnchor)4;
			((Graphic)obj2).color = new Color(0.95f, 0.85f, 0.4f);
			CrewRelationshipHandlerPatch._outingText = CrewRelationshipHandlerPatch.CreateLabel(val.transform, "OE_Desc", "", 12, (FontStyle)0);
			CrewRelationshipHandlerPatch._outingText.alignment = (TextAnchor)4;
			((Graphic)CrewRelationshipHandlerPatch._outingText).color = new Color(0.9f, 0.85f, 0.7f);
			RefreshOutingText();
			GameObject obj3 = CrewRelationshipHandlerPatch.CreateHorizontalRow(val.transform, "OE_Buttons");
			obj3.GetComponent<LayoutElement>().minHeight = 38f;
			((Graphic)((Component)CrewRelationshipHandlerPatch.CreateButton(obj3.transform, "OE_Accept", "Accept", OnAcceptOuting)).GetComponent<Image>()).color = new Color(0.2f, 0.45f, 0.2f, 0.95f);
			((Graphic)((Component)CrewRelationshipHandlerPatch.CreateButton(obj3.transform, "OE_Deny", "Deny", OnDenyOuting)).GetComponent<Image>()).color = new Color(0.45f, 0.2f, 0.2f, 0.95f);
			CrewRelationshipHandlerPatch.RethemeMenuHierarchy(val);
			CrewRelationshipHandlerPatch._outingPopup = val;
			ForceDockPopupFarLeft(val);
			val.transform.SetAsLastSibling();
			Debug.Log("[GameplayTweaks] Outing event popup created and shown");
		}

		private static void RefreshOutingText()
		{
			if (!((UnityEngine.Object)(object)CrewRelationshipHandlerPatch._outingText == (UnityEngine.Object)null))
			{
				PlayerCrew humanCrew = G.GetHumanCrew();
				int num = ((humanCrew != null) ? humanCrew.LivingCrewCount : 0);
				GangMeetingTier currentTier = GetCurrentTier();
				int perCrewCost = GetPerCrewCost(currentTier);
				float happinessGain = GetHappinessGain(currentTier);
				int num2 = num * perCrewCost;
				int playerCleanCash = GetPlayerCleanCash();
				int totalDirtyCash = ShouldDeferDirtyCashRuntime() ? 0 : GetTotalDirtyCash();
				int num3 = playerCleanCash + totalDirtyCash;
				string arg = ShouldDeferDirtyCashRuntime() ? $"Available: ${playerCleanCash} clean" : ((totalDirtyCash > 0) ? $"Available: ${playerCleanCash} clean + ${totalDirtyCash} dirty = ${num3}" : $"Available: ${playerCleanCash}");
				CrewRelationshipHandlerPatch._outingText.text = $"Your crew is calling a gang meeting!\n\nTier: {GetTierLabel(currentTier)}\nCost: ${num2} (${perCrewCost} x {num} members)\n{arg}\nEffect: +{Mathf.RoundToInt(happinessGain * 100f)}% happiness";
			}
		}

		private static void OnAcceptOuting()
		{

			try
			{
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null)
				{
					return;
				}
				if (!TryExecuteGangMeetingEffects(humanPlayer, "prompt", showSnitchPopup: true))
				{
					int crewCount;
					int cost;
					int cleanCash;
					int dirtyCash;
					int totalCash;
					CanAffordCurrentTierMeeting(humanPlayer, out crewCount, out cost, out cleanCash, out dirtyCash, out totalCash);
					if ((UnityEngine.Object)(object)CrewRelationshipHandlerPatch._outingText != (UnityEngine.Object)null)
					{
						CrewRelationshipHandlerPatch._outingText.text = $"Not enough money! Need ${cost}, have ${totalCash}";
					}
					return;
				}
				if ((UnityEngine.Object)(object)CrewRelationshipHandlerPatch._outingPopup != (UnityEngine.Object)null)
				{
					CrewRelationshipHandlerPatch._outingPopup.SetActive(false);
				}
				VerificationLog("GangMeeting", $"promptAccepted day={G.GetNow().days}");
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] Outing accept failed: {arg}");
			}
		}

		private static void OnDenyOuting()
		{
			if ((UnityEngine.Object)(object)CrewRelationshipHandlerPatch._outingPopup != (UnityEngine.Object)null)
			{
				CrewRelationshipHandlerPatch._outingPopup.SetActive(false);
			}
			Debug.Log("[GameplayTweaks] Gang meeting denied");
		}
	}

	private static class PactVotePrompt
	{
		private static GameObject _popup;

		private static Text _descriptionText;

		private static int _activePactColorIndex = -1;

		private static readonly HashSet<int> _loggedPendingPromptColorIndices = new HashSet<int>();

		internal static bool ShowPrompt(AlliancePact pact)
		{
			if (pact == null)
			{
				return false;
			}
			try
			{
				_activePactColorIndex = pact.ColorIndex;
				EnsurePopup();
				RefreshText();
				if ((UnityEngine.Object)(object)_popup != (UnityEngine.Object)null)
				{
					_popup.SetActive(true);
					ForceDockPopupFarLeft(_popup);
					_popup.transform.SetAsLastSibling();
					_loggedPendingPromptColorIndices.Remove(pact.ColorIndex);
					return true;
				}
			}
			catch (Exception ex)
			{
				HandlePromptFailure(pact, "ShowPrompt", ex);
			}
			return false;
		}

		internal static void Tick()
		{
			try
			{
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				int humanGangId = humanPlayer?.PID.id ?? -1;
				if (humanGangId < 0 || humanPlayer?.crew == null || SaveData?.Pacts == null || SaveData.Pacts.Count == 0 || !AreRuntimePromptsReady())
				{
					return;
				}

				foreach (AlliancePact pact in SaveData.Pacts)
				{
					if (pact == null || !pact.IsActive || pact.PendingVoteCycleDay < 0 || pact.VotePromptAnswered)
					{
						continue;
					}

					List<int> members = GetPactMemberGangIds(pact);
					if (SaveData.PlayerJoinedPactIndex == pact.ColorIndex && !members.Contains(humanGangId))
					{
						members.Add(humanGangId);
					}
					if (!members.Contains(humanGangId))
					{
						continue;
					}

					if ((UnityEngine.Object)(object)_popup != (UnityEngine.Object)null && _popup.activeSelf && _activePactColorIndex == pact.ColorIndex)
					{
						ForceDockPopupFarLeft(_popup);
						return;
					}

					if (ShowPrompt(pact))
					{
						pact.VotePromptShown = true;
						return;
					}

					if (_loggedPendingPromptColorIndices.Add(pact.ColorIndex))
					{
						LogGrapevine($"PACT: Annual vote pending for {pact.DisplayName}. A response prompt is waiting to appear.");
					}
					return;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] PactVotePrompt.Tick failed: " + ex);
				if ((UnityEngine.Object)(object)_popup != (UnityEngine.Object)null)
				{
					_popup.SetActive(false);
				}
				_activePactColorIndex = -1;
			}
		}

		private static AlliancePact GetActivePact()
		{
			return SaveData?.Pacts?.FirstOrDefault(p => p != null && p.ColorIndex == _activePactColorIndex);
		}

		private static void EnsurePopup()
		{
			if ((UnityEngine.Object)(object)_popup != (UnityEngine.Object)null)
			{
				return;
			}

			Canvas overlay = GetOrCreateOverlayCanvas();
			if ((UnityEngine.Object)(object)overlay == (UnityEngine.Object)null)
			{
				return;
			}

			_popup = new GameObject("PactVotePrompt", new Type[5]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(VerticalLayoutGroup),
				typeof(Canvas),
				typeof(GraphicRaycaster)
			});
			_popup.transform.SetParent(((Component)overlay).transform, false);
			Canvas canvas = _popup.GetComponent<Canvas>();
			canvas.overrideSorting = true;
			canvas.sortingOrder = 999;
			RectTransform rt = _popup.GetComponent<RectTransform>();
			rt.anchorMin = new Vector2(0.5f, 0.68f);
			rt.anchorMax = new Vector2(0.5f, 0.68f);
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.sizeDelta = ScalePopupSize(430f, 250f);
			CrewRelationshipHandlerPatch.ApplyPopupPanelTheme(_popup, new Color(0.11f, 0.12f, 0.14f, 0.98f));
			VerticalLayoutGroup layout = _popup.GetComponent<VerticalLayoutGroup>();
			((LayoutGroup)layout).padding = new RectOffset(14, 14, 12, 12);
			((HorizontalOrVerticalLayoutGroup)layout).spacing = 8f;
			((HorizontalOrVerticalLayoutGroup)layout).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)layout).childForceExpandHeight = false;

			GameObject header = CrewRelationshipHandlerPatch.CreateHorizontalRow(_popup.transform, "PV_Header");
			Text title = CrewRelationshipHandlerPatch.CreateLabel(header.transform, "PV_Title", "-- Pact Vote --", 15, (FontStyle)1);
			title.alignment = (TextAnchor)3;
			((Graphic)title).color = new Color(0.95f, 0.85f, 0.4f);
			Button close = CrewRelationshipHandlerPatch.CreateButton(header.transform, "PV_Close", "X", OnSkipVote);
			((Graphic)((Component)close).GetComponent<Image>()).color = new Color(0.45f, 0.2f, 0.2f, 0.95f);

			_descriptionText = CrewRelationshipHandlerPatch.CreateLabel(_popup.transform, "PV_Desc", string.Empty, 12, (FontStyle)0);
			_descriptionText.alignment = (TextAnchor)0;
			((Graphic)_descriptionText).color = new Color(0.9f, 0.85f, 0.7f);

			GameObject row1 = CrewRelationshipHandlerPatch.CreateHorizontalRow(_popup.transform, "PV_Row1");
			CreateVoteButton(row1.transform, "Earn +2%", PACT_VOTE_EARNINGS_UP);
			CreateVoteButton(row1.transform, "Earn -2%", PACT_VOTE_EARNINGS_DOWN);
			GameObject row2 = CrewRelationshipHandlerPatch.CreateHorizontalRow(_popup.transform, "PV_Row2");
			CreateVoteButton(row2.transform, "Crew +1", PACT_VOTE_CREW_UP);
			CreateVoteButton(row2.transform, "Crew =", PACT_VOTE_CREW_STAY);
			GameObject row3 = CrewRelationshipHandlerPatch.CreateHorizontalRow(_popup.transform, "PV_Row3");
			Button skip = CrewRelationshipHandlerPatch.CreateButton(row3.transform, "PV_Skip", "Skip This Vote", OnSkipVote);
			((Graphic)((Component)skip).GetComponent<Image>()).color = new Color(0.35f, 0.22f, 0.18f, 0.95f);

			CrewRelationshipHandlerPatch.RethemeMenuHierarchy(_popup);
			_popup.SetActive(false);
		}

		private static void CreateVoteButton(Transform parent, string label, int voteType)
		{
			Button button = CrewRelationshipHandlerPatch.CreateButton(parent, "PV_" + voteType, label, null);
			((Graphic)((Component)button).GetComponent<Image>()).color = new Color(0.2f, 0.4f, 0.24f, 0.95f);
			((UnityEvent)button.onClick).AddListener((UnityAction)delegate
			{
				OnVoteSelected(voteType);
			});
		}

		private static void RefreshText()
		{
			if ((UnityEngine.Object)(object)_descriptionText == (UnityEngine.Object)null)
			{
				return;
			}

			AlliancePact pact = GetActivePact();
			if (pact == null)
			{
				_descriptionText.text = "No active pact vote is pending.";
				return;
			}

			PlayerInfo humanPlayer = G.GetHumanPlayer();
			int humanGangId = humanPlayer?.PID.id ?? -1;
			List<int> members = GetPactMemberGangIds(pact);
			EnsurePactVotePreferences(pact, members, humanGangId);
			List<string> otherVotes = members
				.Where(id => id != humanGangId)
				.Select(id => $"{GetGangDisplayName(id)} wants {GetPactVoteLabel(GetOrAssignPactVotePreference(pact, id, isHumanMember: false, humanGangId))}")
				.ToList();
			string otherText = otherVotes.Count > 0
				? string.Join("\n", otherVotes)
				: "No other gangs have stated a preference yet.";
			_descriptionText.text = $"{pact.DisplayName} is holding its annual vote.\nChoose what your outfit supports.\n\nOther gangs:\n{otherText}";
			CrewRelationshipHandlerPatch.SanitizeMissingGlyphsInHierarchy(_popup, aggressive: true);
		}

		private static void HandlePromptFailure(AlliancePact pact, string phase, Exception ex)
		{
			if (pact != null)
			{
				pact.PlayerProposedVote = -1;
				pact.VotePromptShown = true;
				pact.VotePromptAnswered = true;
			}
			if ((UnityEngine.Object)(object)_popup != (UnityEngine.Object)null)
			{
				_popup.SetActive(false);
			}
			_activePactColorIndex = -1;
			string pactName = pact?.DisplayName ?? "unknown";
			Debug.LogWarning($"[GameplayTweaks] PactVotePrompt failure phase={phase} pact={pactName}: {ex}");
		}

		private static void OnVoteSelected(int voteType)
		{
			AlliancePact pact = GetActivePact();
			if (pact == null)
			{
				return;
			}

			pact.PlayerProposedVote = ClampPactVoteChoice(voteType, allowNone: false);
			pact.VotePromptShown = true;
			pact.VotePromptAnswered = true;
			_loggedPendingPromptColorIndices.Remove(pact.ColorIndex);
			LogGrapevine($"PACT: Your outfit voted for '{GetPactVoteLabel(voteType)}'.");
			if ((UnityEngine.Object)(object)_popup != (UnityEngine.Object)null)
			{
				_popup.SetActive(false);
			}
		}

		private static void OnSkipVote()
		{
			AlliancePact pact = GetActivePact();
			if (pact != null)
			{
				pact.PlayerProposedVote = -1;
				pact.VotePromptShown = true;
				pact.VotePromptAnswered = true;
				_loggedPendingPromptColorIndices.Remove(pact.ColorIndex);
				LogGrapevine($"PACT: Your outfit skipped the annual vote for {pact.DisplayName}.");
			}
			if ((UnityEngine.Object)(object)_popup != (UnityEngine.Object)null)
			{
				_popup.SetActive(false);
			}
		}
	}


	private static class PactOpsHud
	{
		private static GameObject _hudButton;

		private static GameObject _popup;

		private static Transform _content;

		private static Text _diagnosticsText;

		private static string _lastConfigActionStatus = string.Empty;

		private static bool _visible;

		private static GangOpsChannel _activeChannel = GangOpsChannel.Pact;

		private static Button _pactsTabButton;

		private static Button _independentTabButton;

		internal static void EnsureHudButton()
		{
			if ((UnityEngine.Object)(object)_hudButton != (UnityEngine.Object)null)
			{
				return;
			}
			Canvas orCreateOverlayCanvas = GetOrCreateOverlayCanvas();
			if ((UnityEngine.Object)(object)orCreateOverlayCanvas == (UnityEngine.Object)null)
			{
				return;
			}
			GameObject val = new GameObject("GangOpsHudButton", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(Button),
				typeof(LayoutElement)
			});
			val.transform.SetParent(((Component)orCreateOverlayCanvas).transform, false);
			RectTransform component = val.GetComponent<RectTransform>();
			component.anchorMin = new Vector2(1f, 1f);
			component.anchorMax = new Vector2(1f, 1f);
			component.pivot = new Vector2(1f, 1f);
			component.sizeDelta = new Vector2(128f, 32f);
			component.anchoredPosition = new Vector2(-22f, -18f);
			Button component2 = val.GetComponent<Button>();
			((UnityEvent)component2.onClick).AddListener((UnityAction)TogglePopup);
			CrewRelationshipHandlerPatch.ApplyStandardButtonTheme(component2, UiButtonVariant.Accent);
			GameObject val2 = new GameObject("Text", new Type[2]
			{
				typeof(RectTransform),
				typeof(Text)
			});
			val2.transform.SetParent(val.transform, false);
			RectTransform component3 = val2.GetComponent<RectTransform>();
			component3.anchorMin = Vector2.zero;
			component3.anchorMax = Vector2.one;
			component3.offsetMin = Vector2.zero;
			component3.offsetMax = Vector2.zero;
			Text component4 = val2.GetComponent<Text>();
			component4.text = "Gang Ops";
			component4.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			component4.fontSize = 13;
			component4.fontStyle = (FontStyle)1;
			component4.alignment = (TextAnchor)4;
			((Graphic)component4).color = new Color(0.9f, 0.95f, 1f);
			_hudButton = val;
		}

		internal static bool IsPopupVisible()
		{
			return _visible;
		}

		internal static bool HidePopupIfVisible()
		{
			bool flag = _visible || ((UnityEngine.Object)(object)_popup != (UnityEngine.Object)null && _popup.activeSelf);
			if (!flag)
			{
				return false;
			}
			_visible = false;
			if ((UnityEngine.Object)(object)_popup != (UnityEngine.Object)null)
			{
				_popup.SetActive(false);
			}
			return true;
		}

		internal static void TogglePopup()
		{
			if ((UnityEngine.Object)(object)_popup == (UnityEngine.Object)null)
			{
				CreatePopup();
			}
			_visible = !_visible;
			if ((UnityEngine.Object)(object)_popup != (UnityEngine.Object)null)
			{
				_popup.SetActive(_visible);
				if (_visible)
				{
					ForceDockPopupFarLeft(_popup);
					RefreshPopup();
				}
			}
		}

		internal static void Tick()
		{
			if (_visible && (UnityEngine.Object)(object)_popup != (UnityEngine.Object)null && (UnityEngine.Object)(object)_diagnosticsText != (UnityEngine.Object)null)
			{
				UpdateDiagnosticsText();
			}
		}

		private static void CreatePopup()
		{
			Canvas orCreateOverlayCanvas = GetOrCreateOverlayCanvas();
			if ((UnityEngine.Object)(object)orCreateOverlayCanvas == (UnityEngine.Object)null)
			{
				return;
			}
			_popup = new GameObject("GangOpsPopup", new Type[5]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(VerticalLayoutGroup),
				typeof(Canvas),
				typeof(GraphicRaycaster)
			});
			_popup.transform.SetParent(((Component)orCreateOverlayCanvas).transform, false);
			Canvas component = _popup.GetComponent<Canvas>();
			component.overrideSorting = true;
			component.sortingOrder = 999;
			RectTransform component2 = _popup.GetComponent<RectTransform>();
			component2.anchorMin = new Vector2(1f, 1f);
			component2.anchorMax = new Vector2(1f, 1f);
			component2.pivot = new Vector2(1f, 1f);
			component2.sizeDelta = ScalePopupSize(430f, 560f);
			component2.anchoredPosition = new Vector2(-20f, -58f);
			CrewRelationshipHandlerPatch.ApplyPopupPanelTheme(_popup, new Color(0.11f, 0.12f, 0.14f, 0.98f));
			VerticalLayoutGroup component3 = _popup.GetComponent<VerticalLayoutGroup>();
			((LayoutGroup)component3).padding = new RectOffset(10, 10, 8, 8);
			((HorizontalOrVerticalLayoutGroup)component3).spacing = 5f;
			((HorizontalOrVerticalLayoutGroup)component3).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component3).childForceExpandHeight = false;
			GameObject obj = CrewRelationshipHandlerPatch.CreateHorizontalRow(_popup.transform, "GangOps_Header");
			obj.GetComponent<LayoutElement>().minHeight = 26f;
			Text obj2 = CrewRelationshipHandlerPatch.CreateLabel(obj.transform, "GangOps_Title", "Gang Ops Automation", 14, (FontStyle)1);
			obj2.alignment = (TextAnchor)3;
			((Graphic)obj2).color = new Color(0.92f, 0.95f, 1f);
			LayoutElement component4 = ((Component)obj2).GetComponent<LayoutElement>();
			component4.flexibleWidth = 1f;
			Button obj3 = CrewRelationshipHandlerPatch.CreateButton(obj.transform, "GangOps_Close", "X", delegate
			{
				_visible = false;
				if ((UnityEngine.Object)(object)_popup != (UnityEngine.Object)null)
				{
					_popup.SetActive(false);
				}
			});
			LayoutElement component4b = ((Component)obj3).GetComponent<LayoutElement>();
			if ((UnityEngine.Object)(object)component4b != (UnityEngine.Object)null)
			{
				component4b.minWidth = 32f;
				component4b.preferredWidth = 32f;
				component4b.flexibleWidth = 0f;
				component4b.minHeight = 20f;
				component4b.preferredHeight = 20f;
			}
			GameObject val = new GameObject("GangOps_Scroll", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(ScrollRect),
				typeof(LayoutElement)
			});
			val.transform.SetParent(_popup.transform, false);
			LayoutElement component5 = val.GetComponent<LayoutElement>();
			component5.flexibleHeight = 1f;
			component5.minHeight = 470f;
			((Graphic)val.GetComponent<Image>()).color = new Color(0.12f, 0.13f, 0.16f, 0.86f);
			GameObject val2 = new GameObject("Viewport", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(Mask)
			});
			val2.transform.SetParent(val.transform, false);
			RectTransform component6 = val2.GetComponent<RectTransform>();
			component6.anchorMin = Vector2.zero;
			component6.anchorMax = Vector2.one;
			component6.offsetMin = new Vector2(2f, 2f);
			component6.offsetMax = new Vector2(-2f, -2f);
			((Graphic)val2.GetComponent<Image>()).color = new Color(1f, 1f, 1f, 0.01f);
			val2.GetComponent<Mask>().showMaskGraphic = false;
			GameObject val3 = new GameObject("Content", new Type[3]
			{
				typeof(RectTransform),
				typeof(VerticalLayoutGroup),
				typeof(ContentSizeFitter)
			});
			val3.transform.SetParent(val2.transform, false);
			RectTransform component7 = val3.GetComponent<RectTransform>();
			component7.anchorMin = new Vector2(0f, 1f);
			component7.anchorMax = new Vector2(1f, 1f);
			component7.pivot = new Vector2(0.5f, 1f);
			component7.sizeDelta = new Vector2(0f, 0f);
			VerticalLayoutGroup component8 = val3.GetComponent<VerticalLayoutGroup>();
			((LayoutGroup)component8).padding = new RectOffset(5, 5, 5, 5);
			((HorizontalOrVerticalLayoutGroup)component8).spacing = 4f;
			((HorizontalOrVerticalLayoutGroup)component8).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component8).childForceExpandHeight = false;
			val3.GetComponent<ContentSizeFitter>().verticalFit = (ContentSizeFitter.FitMode)2;
			_content = val3.transform;
			ScrollRect component9 = val.GetComponent<ScrollRect>();
			component9.viewport = component6;
			component9.content = component7;
			component9.horizontal = false;
			component9.vertical = true;
			component9.scrollSensitivity = 30f;
			GameObject obj4 = CrewRelationshipHandlerPatch.CreateHorizontalRow(_popup.transform, "GangOps_TabRow");
			obj4.GetComponent<LayoutElement>().minHeight = 30f;
			_pactsTabButton = CrewRelationshipHandlerPatch.CreateButton(obj4.transform, "GangOps_Tab_Pacts", "Pacts", delegate
			{
				_activeChannel = GangOpsChannel.Pact;
				RefreshPopup();
			});
			((Component)_pactsTabButton).GetComponent<LayoutElement>().preferredWidth = 100f;
			_independentTabButton = CrewRelationshipHandlerPatch.CreateButton(obj4.transform, "GangOps_Tab_Independent", "Independent", delegate
			{
				_activeChannel = GangOpsChannel.Independent;
				RefreshPopup();
			});
			((Component)_independentTabButton).GetComponent<LayoutElement>().preferredWidth = 120f;
			_popup.SetActive(false);
			CrewRelationshipHandlerPatch.RethemeMenuHierarchy(_popup);
		}

		private static void RefreshPopup()
		{
			if ((UnityEngine.Object)(object)_content == (UnityEngine.Object)null)
			{
				return;
			}
			RefreshTabButtons();
			for (int num = _content.childCount - 1; num >= 0; num--)
			{
				UnityEngine.Object.Destroy((UnityEngine.Object)(object)((Component)_content.GetChild(num)).gameObject);
			}
			PactOpsSettings pactOpsSettings = EnsureGangOpsSettings(_activeChannel);
			VerificationLog($"GangOps.{GetGangOpsChannelTag(_activeChannel)}.UiProfile", $"enabled={pactOpsSettings.Enabled} coordAuto={pactOpsSettings.CoordinatedAttackAutoEnabled} cd={pactOpsSettings.CoordinatedAttackCooldownDays} minCrew={pactOpsSettings.CoordinatedAttackMinCrew} maxCrew={pactOpsSettings.CoordinatedAttackMaxCrew} aggro={pactOpsSettings.TerritoryTakeoverAggressionPercent}/{pactOpsSettings.FrontClosureAggressionPercent}/{pactOpsSettings.OverallAggroPercent} top={pactOpsSettings.TopGangCrewBonusMin}-{pactOpsSettings.TopGangCrewBonusMax} mid={pactOpsSettings.MidGangCrewBonus} bottom={pactOpsSettings.BottomGangCrewBonus} chBonus={pactOpsSettings.PactCrewBonus}");
			AddSectionLabel("Channel: " + GetGangOpsChannelTag(_activeChannel));
			AddSectionLabel("Automation Master");
			AddToggleRow("Enabled", () => pactOpsSettings.Enabled, delegate(bool v)
			{
				pactOpsSettings.Enabled = v;
			});
			AddSectionLabel("AutoProtect");
			AddToggleRow("AutoProtect", () => pactOpsSettings.AutoProtectEnabled, delegate(bool v)
			{
				pactOpsSettings.AutoProtectEnabled = v;
			});
			AddIntAdjustRow("Interval Days", () => pactOpsSettings.AutoProtectIntervalDays, delegate(int v)
			{
				pactOpsSettings.AutoProtectIntervalDays = v;
			}, 1, 1, 365);
			AddIntAdjustRow("Range", () => pactOpsSettings.AutoProtectRange, delegate(int v)
			{
				pactOpsSettings.AutoProtectRange = v;
			}, 1, 1, 6);
			AddSectionLabel("Coordinated Attacks");
			AddToggleRow("Auto", () => pactOpsSettings.CoordinatedAttackAutoEnabled, delegate(bool v)
			{
				pactOpsSettings.CoordinatedAttackAutoEnabled = v;
			});
			AddIntAdjustRow("Cooldown Days", () => pactOpsSettings.CoordinatedAttackCooldownDays, delegate(int v)
			{
				pactOpsSettings.CoordinatedAttackCooldownDays = v;
			}, 1, 1, 180);
			AddIntAdjustRow("Min Crew", () => pactOpsSettings.CoordinatedAttackMinCrew, delegate(int v)
			{
				pactOpsSettings.CoordinatedAttackMinCrew = v;
			}, 1, 1, 10);
			AddIntAdjustRow("Max Crew", () => pactOpsSettings.CoordinatedAttackMaxCrew, delegate(int v)
			{
				pactOpsSettings.CoordinatedAttackMaxCrew = v;
			}, 1, 1, 12);
			AddActionRow("Force Coordinated Attacks Now", delegate
			{
				int num2 = TriggerCoordinatedAttacksNow(_activeChannel, "manual", autoMode: false);
				VerificationLog($"GangOps.{GetGangOpsChannelTag(_activeChannel)}.CoordAttack", $"manual trigger total={num2}");
				RefreshPopup();
			});
			AddSectionLabel("Revenge");
			AddToggleRow("Enabled", () => pactOpsSettings.RevengeEnabled, delegate(bool v)
			{
				pactOpsSettings.RevengeEnabled = v;
			});
			AddIntAdjustRow("Delay Days", () => pactOpsSettings.RevengeDelayDays, delegate(int v)
			{
				pactOpsSettings.RevengeDelayDays = v;
			}, 1, 1, 180);
			AddSectionLabel("WarHeat");
			AddFloatAdjustRow("Decay/Week", () => pactOpsSettings.WarHeatDecayPerWeek, delegate(float v)
			{
				pactOpsSettings.WarHeatDecayPerWeek = v;
			}, 1f, 0f, 100f);
			AddFloatAdjustRow("Attack Gain", () => pactOpsSettings.WarHeatAttackGain, delegate(float v)
			{
				pactOpsSettings.WarHeatAttackGain = v;
			}, 1f, 0f, 100f);
			AddFloatAdjustRow("Kill Gain", () => pactOpsSettings.WarHeatKillGain, delegate(float v)
			{
				pactOpsSettings.WarHeatKillGain = v;
			}, 1f, 0f, 100f);
			AddSectionLabel("Aggression");
			AddIntAdjustRow("Take Turf %", () => pactOpsSettings.TerritoryTakeoverAggressionPercent, delegate(int v)
			{
				pactOpsSettings.TerritoryTakeoverAggressionPercent = v;
			}, 10, 10, 300);
			AddIntAdjustRow("Close Fronts %", () => pactOpsSettings.FrontClosureAggressionPercent, delegate(int v)
			{
				pactOpsSettings.FrontClosureAggressionPercent = v;
			}, 10, 10, 300);
			AddIntAdjustRow("Overall Aggro %", () => pactOpsSettings.OverallAggroPercent, delegate(int v)
			{
				pactOpsSettings.OverallAggroPercent = v;
			}, 10, 10, 300);
			string hireLimitsLabel = _activeChannel == GangOpsChannel.Pact ? "AI Hire Limits - Pact" : "AI Hire Limits - Independent";
			string channelBonusLabel = _activeChannel == GangOpsChannel.Pact ? "Pact Member Bonus" : "Independent Bonus";
			AddSectionLabel(hireLimitsLabel);
			AddToggleRow("Enabled", () => pactOpsSettings.HireAutomationEnabled, delegate(bool v)
			{
				pactOpsSettings.HireAutomationEnabled = v;
			});
			AddIntAdjustRow("Top Bonus Min", () => pactOpsSettings.TopGangCrewBonusMin, delegate(int v)
			{
				pactOpsSettings.TopGangCrewBonusMin = v;
			}, 1, 0, 100);
			AddIntAdjustRow("Top Bonus Max", () => pactOpsSettings.TopGangCrewBonusMax, delegate(int v)
			{
				pactOpsSettings.TopGangCrewBonusMax = v;
			}, 1, 0, 150);
			AddIntAdjustRow("Mid Bonus", () => pactOpsSettings.MidGangCrewBonus, delegate(int v)
			{
				pactOpsSettings.MidGangCrewBonus = v;
			}, 1, 0, 100);
			AddIntAdjustRow("Bottom Bonus", () => pactOpsSettings.BottomGangCrewBonus, delegate(int v)
			{
				pactOpsSettings.BottomGangCrewBonus = v;
			}, 1, 0, 100);
			AddIntAdjustRow(channelBonusLabel, () => pactOpsSettings.PactCrewBonus, delegate(int v)
			{
				pactOpsSettings.PactCrewBonus = v;
			}, 1, 0, 100);
			AddActionRow("Reset Channel To Defaults", delegate
			{
				if (SaveData == null)
				{
					SaveData = new ModSaveData();
				}
				PactOpsSettings defaults = BuildGangOpsDefaultsFromConfig(_activeChannel);
				if (_activeChannel == GangOpsChannel.Pact)
				{
					SaveData.PactOps = defaults;
				}
				else
				{
					SaveData.IndependentOps = defaults;
				}
				NormalizePactOpsSettings(defaults);
				SaveData.GangOpsDefaultsProfileVersion = GangOpsDefaultsProfileVersionCurrent;
				VerificationLog($"GangOps.{GetGangOpsChannelTag(_activeChannel)}.Reset", $"cd={defaults.CoordinatedAttackCooldownDays} minCrew={defaults.CoordinatedAttackMinCrew} maxCrew={defaults.CoordinatedAttackMaxCrew} aggro={defaults.TerritoryTakeoverAggressionPercent}/{defaults.FrontClosureAggressionPercent}/{defaults.OverallAggroPercent} top={defaults.TopGangCrewBonusMin}-{defaults.TopGangCrewBonusMax} mid={defaults.MidGangCrewBonus} bottom={defaults.BottomGangCrewBonus} chBonus={defaults.PactCrewBonus}");
				RefreshPopup();
			});
			AddSectionLabel("Config Presets");
			AddActionRow("Save Channel To Config", delegate
			{
				PactOpsSettings current = EnsureGangOpsSettings(_activeChannel);
				if (TrySaveGangOpsSettingsToConfig(_activeChannel, current, out string message))
				{
					_lastConfigActionStatus = message;
				}
				else
				{
					_lastConfigActionStatus = message;
				}
				RefreshPopup();
			});
			AddActionRow("Save Both To Config", delegate
			{
				bool savedPact = TrySaveGangOpsSettingsToConfig(GangOpsChannel.Pact, EnsureGangOpsSettings(GangOpsChannel.Pact), out string pactMessage);
				bool savedIndependent = TrySaveGangOpsSettingsToConfig(GangOpsChannel.Independent, EnsureGangOpsSettings(GangOpsChannel.Independent), out string independentMessage);
				_lastConfigActionStatus = savedPact && savedIndependent
					? "Saved both channel defaults to config."
					: pactMessage + " " + independentMessage;
				RefreshPopup();
			});
			AddSectionLabel("Diagnostics");
			AddActionRow("Run Runtime Verify", delegate
			{
				RunRuntimeVerificationChecklist();
			});
			AddActionRow("Copy Config/Save Paths", delegate
			{
				string pathsText = GetGangOpsConfigAndSavePathsText();
				try
				{
					GUIUtility.systemCopyBuffer = pathsText;
					_lastConfigActionStatus = "Copied config/save paths.";
				}
				catch (Exception ex)
				{
					_lastConfigActionStatus = "Path copy failed: " + ex.Message;
				}
				VerificationLog("GangOps.Paths", pathsText.Replace("\n", " | "));
				RefreshPopup();
			});
			_diagnosticsText = CrewRelationshipHandlerPatch.CreateLabel(_content, "GangOps_Diagnostics", string.Empty, 11, (FontStyle)0);
			((Graphic)_diagnosticsText).color = new Color(0.85f, 0.9f, 0.95f);
			UpdateDiagnosticsText();
			NormalizePactOpsSettings(pactOpsSettings);
		}

		private static void RefreshTabButtons()
		{
			if ((UnityEngine.Object)(object)_pactsTabButton != (UnityEngine.Object)null)
			{
				Graphic component = ((Component)_pactsTabButton).GetComponent<Graphic>();
				if ((UnityEngine.Object)(object)component != (UnityEngine.Object)null)
				{
					component.color = ((_activeChannel == GangOpsChannel.Pact) ? new Color(0.25f, 0.38f, 0.58f, 0.98f) : GameplayTweaksPlugin.UI_BUTTON_DARK_GRAY);
				}
			}
			if ((UnityEngine.Object)(object)_independentTabButton == (UnityEngine.Object)null)
			{
				return;
			}
			Graphic component2 = ((Component)_independentTabButton).GetComponent<Graphic>();
			if (!((UnityEngine.Object)(object)component2 == (UnityEngine.Object)null))
			{
				component2.color = ((_activeChannel == GangOpsChannel.Independent) ? new Color(0.25f, 0.38f, 0.58f, 0.98f) : GameplayTweaksPlugin.UI_BUTTON_DARK_GRAY);
			}
		}

		private static void AddSectionLabel(string text)
		{
			Text obj = CrewRelationshipHandlerPatch.CreateLabel(_content, "GangOps_Section_" + text.Replace(" ", string.Empty), text, 12, (FontStyle)1);
			((Graphic)obj).color = new Color(0.95f, 0.87f, 0.6f);
			((Component)obj).GetComponent<LayoutElement>().minHeight = 18f;
		}

		private static void AddToggleRow(string label, Func<bool> getter, Action<bool> setter)
		{
			GameObject obj = CrewRelationshipHandlerPatch.CreateHorizontalRow(_content, "GangOps_Toggle_" + label.Replace(" ", string.Empty));
			obj.GetComponent<LayoutElement>().minHeight = 30f;
			Text obj2 = CrewRelationshipHandlerPatch.CreateLabel(obj.transform, "Lbl", label, 11, (FontStyle)0);
			((Component)obj2).GetComponent<LayoutElement>().flexibleWidth = 1f;
			string text = (getter() ? "ON" : "OFF");
			Button obj3 = CrewRelationshipHandlerPatch.CreateButton(obj.transform, "Btn", text, delegate
			{
				setter(!getter());
				NormalizePactOpsSettings(EnsureGangOpsSettings(_activeChannel));
				RefreshPopup();
			});
			((Component)obj3).GetComponent<LayoutElement>().preferredWidth = 80f;
		}

		private static void AddIntAdjustRow(string label, Func<int> getter, Action<int> setter, int step, int min, int max)
		{
			GameObject obj = CrewRelationshipHandlerPatch.CreateHorizontalRow(_content, "GangOps_Int_" + label.Replace(" ", string.Empty));
			obj.GetComponent<LayoutElement>().minHeight = 30f;
			CrewRelationshipHandlerPatch.CreateLabel(obj.transform, "Lbl", label, 11, (FontStyle)0).GetComponent<LayoutElement>().preferredWidth = 138f;
			Button obj2 = CrewRelationshipHandlerPatch.CreateButton(obj.transform, "Minus", "-", null);
			((UnityEvent)obj2.onClick).AddListener((UnityAction)delegate
			{
				setter(Mathf.Clamp(getter() - step, min, max));
				NormalizePactOpsSettings(EnsureGangOpsSettings(_activeChannel));
				RefreshPopup();
			});
			((Component)obj2).GetComponent<LayoutElement>().preferredWidth = 28f;
			CrewRelationshipHandlerPatch.CreateLabel(obj.transform, "Val", getter().ToString(CultureInfo.InvariantCulture), 11, (FontStyle)1).GetComponent<LayoutElement>().preferredWidth = 48f;
			Button obj3 = CrewRelationshipHandlerPatch.CreateButton(obj.transform, "Plus", "+", null);
			((UnityEvent)obj3.onClick).AddListener((UnityAction)delegate
			{
				setter(Mathf.Clamp(getter() + step, min, max));
				NormalizePactOpsSettings(EnsureGangOpsSettings(_activeChannel));
				RefreshPopup();
			});
			((Component)obj3).GetComponent<LayoutElement>().preferredWidth = 28f;
		}

		private static void AddFloatAdjustRow(string label, Func<float> getter, Action<float> setter, float step, float min, float max)
		{
			GameObject obj = CrewRelationshipHandlerPatch.CreateHorizontalRow(_content, "GangOps_Float_" + label.Replace(" ", string.Empty));
			obj.GetComponent<LayoutElement>().minHeight = 30f;
			CrewRelationshipHandlerPatch.CreateLabel(obj.transform, "Lbl", label, 11, (FontStyle)0).GetComponent<LayoutElement>().preferredWidth = 138f;
			Button obj2 = CrewRelationshipHandlerPatch.CreateButton(obj.transform, "Minus", "-", null);
			((UnityEvent)obj2.onClick).AddListener((UnityAction)delegate
			{
				setter(Mathf.Clamp(getter() - step, min, max));
				NormalizePactOpsSettings(EnsureGangOpsSettings(_activeChannel));
				RefreshPopup();
			});
			((Component)obj2).GetComponent<LayoutElement>().preferredWidth = 28f;
			CrewRelationshipHandlerPatch.CreateLabel(obj.transform, "Val", getter().ToString("0.0", CultureInfo.InvariantCulture), 11, (FontStyle)1).GetComponent<LayoutElement>().preferredWidth = 48f;
			Button obj3 = CrewRelationshipHandlerPatch.CreateButton(obj.transform, "Plus", "+", null);
			((UnityEvent)obj3.onClick).AddListener((UnityAction)delegate
			{
				setter(Mathf.Clamp(getter() + step, min, max));
				NormalizePactOpsSettings(EnsureGangOpsSettings(_activeChannel));
				RefreshPopup();
			});
			((Component)obj3).GetComponent<LayoutElement>().preferredWidth = 28f;
		}

		private static void AddActionRow(string label, Action action)
		{
			GameObject obj = CrewRelationshipHandlerPatch.CreateHorizontalRow(_content, "GangOps_Action_" + label.Replace(" ", string.Empty));
			obj.GetComponent<LayoutElement>().minHeight = 32f;
			Button button = CrewRelationshipHandlerPatch.CreateButton(obj.transform, "Act", label, null);
			((UnityEvent)button.onClick).AddListener((UnityAction)delegate
			{
				action?.Invoke();
			});
			((Component)button).GetComponent<LayoutElement>().flexibleWidth = 1f;
		}

		private static void UpdateDiagnosticsText()
		{
			if ((UnityEngine.Object)(object)_diagnosticsText == (UnityEngine.Object)null)
			{
				return;
			}
			Dictionary<string, WarHeatEntry> warHeatStore = GetWarHeatStore(_activeChannel);
			Dictionary<string, RevengeEntry> revengeStore = GetRevengeStore(_activeChannel);
			int count = warHeatStore?.Count ?? 0;
			int count2 = revengeStore?.Count((KeyValuePair<string, RevengeEntry> kv) => kv.Value != null && !kv.Value.Executed) ?? 0;
			WarHeatEntry warHeatEntry = warHeatStore?.Values.Where((WarHeatEntry w) => w != null).OrderByDescending((WarHeatEntry w) => w.Heat).FirstOrDefault();
			string topHeat = (warHeatEntry != null) ? $"{warHeatEntry.AttackerPid}->{warHeatEntry.DefenderPid} {warHeatEntry.Heat:0.0}" : "--";
			PactOpsSettings settings = EnsureGangOpsSettings(_activeChannel);
			string channelTag = GetGangOpsChannelTag(_activeChannel);
			string ca = settings.CoordinatedAttackAutoEnabled ? "on" : "off";
			string hire = settings.HireAutomationEnabled ? "on" : "off";
			string opsLine = $"Ops: CA={ca} {settings.CoordinatedAttackCooldownDays}d {settings.CoordinatedAttackMinCrew}-{settings.CoordinatedAttackMaxCrew} | Aggro T/F/O={settings.TerritoryTakeoverAggressionPercent}/{settings.FrontClosureAggressionPercent}/{settings.OverallAggroPercent}% | Hire={hire} T{settings.TopGangCrewBonusMin}-{settings.TopGangCrewBonusMax}/M{settings.MidGangCrewBonus}/B{settings.BottomGangCrewBonus}/C+{settings.PactCrewBonus}";
			string statusLine = string.IsNullOrWhiteSpace(_lastConfigActionStatus) ? string.Empty : "\nLast: " + _lastConfigActionStatus;
			_diagnosticsText.text = "Channel: " + channelTag
				+ "\nWarHeat entries: " + count + " | Pending revenge: " + count2
				+ "\nTop heat: " + topHeat
				+ "\n" + opsLine
				+ statusLine
				+ "\nMode: native dual-channel (GangWars fallback optional)\nHotkey: F10";
			LayoutElement layout = ((Component)_diagnosticsText).GetComponent<LayoutElement>();
			if ((UnityEngine.Object)(object)layout != (UnityEngine.Object)null)
			{
				int lineCount = _diagnosticsText.text.Count(c => c == '\n') + 1;
				layout.minHeight = Mathf.Max(layout.minHeight, lineCount * 18f);
				layout.preferredHeight = layout.minHeight;
			}
		}
	}


	private static class GangWarMediationPopup
	{
		private static GameObject _popup;

		private static Transform _content;

		private static Text _bodyText;

		private static PlayerInfo _requestGang;

		private static VisitState _visit;

		private static string _lastResult = string.Empty;

		private static readonly Dictionary<string, int> _cashOffersByPair = new Dictionary<string, int>(StringComparer.Ordinal);

		internal static void Show(PlayerInfo requestGang, VisitState visit = null)
		{
			_requestGang = requestGang;
			_visit = visit;
			if ((UnityEngine.Object)(object)_popup == (UnityEngine.Object)null)
			{
				CreatePopup();
			}
			if ((UnityEngine.Object)(object)_popup == (UnityEngine.Object)null)
			{
				return;
			}
			_popup.SetActive(true);
			ForceDockPopupFarLeft(_popup);
			_popup.transform.SetAsLastSibling();
			RefreshPopup();
		}

		private static void CreatePopup()
		{
			Canvas orCreateOverlayCanvas = GetOrCreateOverlayCanvas();
			if ((UnityEngine.Object)(object)orCreateOverlayCanvas == (UnityEngine.Object)null)
			{
				return;
			}
			_popup = new GameObject("GangWarMediationPopup", new Type[5]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(VerticalLayoutGroup),
				typeof(Canvas),
				typeof(GraphicRaycaster)
			});
			_popup.transform.SetParent(((Component)orCreateOverlayCanvas).transform, false);
			Canvas component = _popup.GetComponent<Canvas>();
			component.overrideSorting = true;
			component.sortingOrder = 999;
			RectTransform component2 = _popup.GetComponent<RectTransform>();
			component2.anchorMin = new Vector2(1f, 1f);
			component2.anchorMax = new Vector2(1f, 1f);
			component2.pivot = new Vector2(1f, 1f);
			component2.sizeDelta = ScalePopupSize(440f, 470f);
			component2.anchoredPosition = new Vector2(-20f, -70f);
			CrewRelationshipHandlerPatch.ApplyPopupPanelTheme(_popup, new Color(0.11f, 0.12f, 0.14f, 0.98f));
			VerticalLayoutGroup component3 = _popup.GetComponent<VerticalLayoutGroup>();
			((LayoutGroup)component3).padding = new RectOffset(12, 12, 10, 10);
			((HorizontalOrVerticalLayoutGroup)component3).spacing = 6f;
			((HorizontalOrVerticalLayoutGroup)component3).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component3).childForceExpandHeight = false;
			GameObject header = CrewRelationshipHandlerPatch.CreateHorizontalRow(_popup.transform, "GWM_Header");
			header.GetComponent<LayoutElement>().minHeight = 28f;
			Text title = CrewRelationshipHandlerPatch.CreateLabel(header.transform, "GWM_Title", "Mediate War", 15, (FontStyle)1);
			title.alignment = (TextAnchor)3;
			((Graphic)title).color = new Color(0.92f, 0.95f, 1f);
			((Component)title).GetComponent<LayoutElement>().flexibleWidth = 1f;
			Button close = CrewRelationshipHandlerPatch.CreateButton(header.transform, "GWM_Close", "X", delegate
			{
				if ((UnityEngine.Object)(object)_popup != (UnityEngine.Object)null)
				{
					_popup.SetActive(false);
				}
			});
			LayoutElement closeLayout = ((Component)close).GetComponent<LayoutElement>();
			closeLayout.minWidth = 32f;
			closeLayout.preferredWidth = 32f;
			closeLayout.flexibleWidth = 0f;
			_bodyText = CrewRelationshipHandlerPatch.CreateLabel(_popup.transform, "GWM_Body", string.Empty, 12, (FontStyle)0);
			_bodyText.alignment = (TextAnchor)0;
			((Graphic)_bodyText).color = new Color(0.86f, 0.88f, 0.82f);
			GameObject scroll = new GameObject("GWM_Scroll", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(ScrollRect),
				typeof(LayoutElement)
			});
			scroll.transform.SetParent(_popup.transform, false);
			LayoutElement scrollLayout = scroll.GetComponent<LayoutElement>();
			scrollLayout.flexibleHeight = 1f;
			scrollLayout.minHeight = 330f;
			((Graphic)scroll.GetComponent<Image>()).color = new Color(0.12f, 0.13f, 0.16f, 0.86f);
			GameObject viewport = new GameObject("Viewport", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(Mask)
			});
			viewport.transform.SetParent(scroll.transform, false);
			RectTransform viewportRect = viewport.GetComponent<RectTransform>();
			viewportRect.anchorMin = Vector2.zero;
			viewportRect.anchorMax = Vector2.one;
			viewportRect.offsetMin = new Vector2(2f, 2f);
			viewportRect.offsetMax = new Vector2(-2f, -2f);
			((Graphic)viewport.GetComponent<Image>()).color = new Color(1f, 1f, 1f, 0.01f);
			viewport.GetComponent<Mask>().showMaskGraphic = false;
			GameObject content = new GameObject("Content", new Type[3]
			{
				typeof(RectTransform),
				typeof(VerticalLayoutGroup),
				typeof(ContentSizeFitter)
			});
			content.transform.SetParent(viewport.transform, false);
			RectTransform contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = new Vector2(0f, 1f);
			contentRect.anchorMax = new Vector2(1f, 1f);
			contentRect.pivot = new Vector2(0.5f, 1f);
			contentRect.sizeDelta = Vector2.zero;
			VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
			((LayoutGroup)contentLayout).padding = new RectOffset(6, 6, 6, 6);
			((HorizontalOrVerticalLayoutGroup)contentLayout).spacing = 6f;
			((HorizontalOrVerticalLayoutGroup)contentLayout).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)contentLayout).childForceExpandHeight = false;
			content.GetComponent<ContentSizeFitter>().verticalFit = (ContentSizeFitter.FitMode)2;
			_content = content.transform;
			ScrollRect scrollRect = scroll.GetComponent<ScrollRect>();
			scrollRect.viewport = viewportRect;
			scrollRect.content = contentRect;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.scrollSensitivity = 30f;
			CrewRelationshipHandlerPatch.RethemeMenuHierarchy(_popup);
			_popup.SetActive(false);
		}

		private static void RefreshPopup()
		{
			if ((UnityEngine.Object)(object)_content == (UnityEngine.Object)null)
			{
				return;
			}
			for (int i = _content.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(((Component)_content.GetChild(i)).gameObject);
			}
			string gangName = _requestGang != null ? GetGangDisplayName(_requestGang.PID.id) : "this outfit";
			List<GangWarMediationCandidate> candidates = BuildGangWarMediationCandidates(_requestGang);
			string status = string.IsNullOrWhiteSpace(_lastResult) ? string.Empty : "\nLast: " + _lastResult;
			_bodyText.text = $"{gangName} current wars: {candidates.Count}. Cash offers use $100 steps, max ${GANG_WAR_MEDIATION_MAX_CASH}.{status}";
			if (candidates.Count == 0)
			{
				Text empty = CrewRelationshipHandlerPatch.CreateLabel(_content, "GWM_Empty", "No active wars are available to mediate.", 12, (FontStyle)0);
				empty.alignment = (TextAnchor)4;
				((Graphic)empty).color = new Color(0.82f, 0.82f, 0.76f);
				return;
			}
			foreach (GangWarMediationCandidate candidate in candidates)
			{
				AddCandidateRow(candidate);
			}
		}

		private static void AddCandidateRow(GangWarMediationCandidate candidate)
		{
			if (candidate?.OpponentGang == null)
			{
				return;
			}
			string pairKey = GetGangWarMediationPairKey(candidate.RequestGang.PID.id, candidate.OpponentGang.PID.id);
			if (!_cashOffersByPair.TryGetValue(pairKey, out int offerCash))
			{
				offerCash = 0;
			}
			offerCash = ClampGangWarMediationCashOffer(offerCash);
			_cashOffersByPair[pairKey] = offerCash;
			RefreshGangWarMediationCandidatePreview(candidate, offerCash);
			GameObject row = CrewRelationshipHandlerPatch.CreateHorizontalRow(_content, "GWM_Row_" + candidate.OpponentGang.PID.id.ToString(CultureInfo.InvariantCulture));
			row.GetComponent<LayoutElement>().minHeight = 88f;
			Text info = CrewRelationshipHandlerPatch.CreateLabel(row.transform, "Info", BuildCandidateInfo(candidate), 11, (FontStyle)0);
			info.alignment = (TextAnchor)3;
			((Graphic)info).color = new Color(0.86f, 0.88f, 0.82f);
			((Component)info).GetComponent<LayoutElement>().flexibleWidth = 1f;
			Button minus = CrewRelationshipHandlerPatch.CreateButton(row.transform, "MinusCash", "-$100", null);
			LayoutElement minusLayout = ((Component)minus).GetComponent<LayoutElement>();
			minusLayout.minWidth = 54f;
			minusLayout.preferredWidth = 54f;
			minusLayout.flexibleWidth = 0f;
			minus.interactable = candidate.CanAttempt && offerCash > 0;
			((UnityEvent)minus.onClick).AddListener((UnityAction)delegate
			{
				_cashOffersByPair[pairKey] = ClampGangWarMediationCashOffer(offerCash - GANG_WAR_MEDIATION_CASH_STEP);
				RefreshPopup();
			});
			Text offerText = CrewRelationshipHandlerPatch.CreateLabel(row.transform, "Offer", "$" + offerCash.ToString(CultureInfo.InvariantCulture), 11, (FontStyle)1);
			offerText.alignment = (TextAnchor)4;
			((Graphic)offerText).color = new Color(0.95f, 0.85f, 0.45f);
			((Component)offerText).GetComponent<LayoutElement>().preferredWidth = 54f;
			Button plus = CrewRelationshipHandlerPatch.CreateButton(row.transform, "PlusCash", "+$100", null);
			LayoutElement plusLayout = ((Component)plus).GetComponent<LayoutElement>();
			plusLayout.minWidth = 54f;
			plusLayout.preferredWidth = 54f;
			plusLayout.flexibleWidth = 0f;
			plus.interactable = candidate.CanAttempt && offerCash < GANG_WAR_MEDIATION_MAX_CASH;
			((UnityEvent)plus.onClick).AddListener((UnityAction)delegate
			{
				_cashOffersByPair[pairKey] = ClampGangWarMediationCashOffer(offerCash + GANG_WAR_MEDIATION_CASH_STEP);
				RefreshPopup();
			});
			string actionLabel = !candidate.CanAttempt ? $"Wait {candidate.CooldownRemainingDays}d" : (candidate.WouldAccept ? (offerCash > 0 ? "Pay End" : "End War") : "Try");
			Button button = CrewRelationshipHandlerPatch.CreateButton(row.transform, "Act", actionLabel, null);
			LayoutElement buttonLayout = ((Component)button).GetComponent<LayoutElement>();
			buttonLayout.minWidth = 92f;
			buttonLayout.preferredWidth = 92f;
			buttonLayout.flexibleWidth = 0f;
			button.interactable = candidate.CanAttempt;
			((Graphic)((Component)button).GetComponent<Image>()).color = !candidate.CanAttempt ? new Color(0.25f, 0.25f, 0.25f, 0.95f) : (candidate.WouldAccept ? new Color(0.2f, 0.4f, 0.24f, 0.95f) : new Color(0.38f, 0.28f, 0.18f, 0.95f));
			((UnityEvent)button.onClick).AddListener((UnityAction)delegate
			{
				bool resolved = TryResolveGangWarMediation(candidate, offerCash, out _lastResult, out bool shouldConsumeConvoAction);
				if (resolved)
				{
					CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(G.GetHumanPlayer(), _visit?.crew.peepId ?? EntityID.INVALID, 0.05f, 0.06f, "gang-war-mediation");
				}
				if (resolved || shouldConsumeConvoAction)
				{
					_visit?.ConsumeConvoActionsHelper();
				}
				RefreshPopup();
			});
		}

		private static string BuildCandidateInfo(GangWarMediationCandidate candidate)
		{
			string name = GetGangDisplayName(candidate.OpponentGang.PID.id);
			string cashText = candidate.OfferCash > 0 ? $" | Cash +{candidate.CashAcceptanceBonus}" : string.Empty;
			string cooldownText = candidate.CooldownRemainingDays > 0 ? $"\nCooldown: {candidate.CooldownRemainingDays} day(s)" : string.Empty;
			return $"{name}\n{candidate.ContextLabel} | Heat {candidate.WarHeat:0} | {candidate.DifficultyLabel} {candidate.AcceptanceScore}/100{cashText}\nPower: player {candidate.PlayerPower}, sides {candidate.RequestGangPower}/{candidate.OpponentPower}{cooldownText}";
		}
	}


	private static class PactInvitationEvent
	{
		private static GameObject _invitePopup;

		private static AlliancePact _invitingPact;

		internal static void ShowInvitation(AlliancePact pact)
		{

			_invitingPact = pact;
			if ((UnityEngine.Object)(object)_invitePopup != (UnityEngine.Object)null)
			{
				_invitePopup.SetActive(true);
				RefreshText();
				return;
			}
			Canvas orCreateOverlayCanvas = GetOrCreateOverlayCanvas();
			if (!((UnityEngine.Object)(object)orCreateOverlayCanvas == (UnityEngine.Object)null))
			{
				GameObject val = new GameObject("PactInvitePopup", new Type[5]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(VerticalLayoutGroup),
					typeof(Canvas),
					typeof(GraphicRaycaster)
				});
				val.transform.SetParent(((Component)orCreateOverlayCanvas).transform, false);
				Canvas component = val.GetComponent<Canvas>();
				component.overrideSorting = true;
				component.sortingOrder = 998;
				RectTransform component2 = val.GetComponent<RectTransform>();
				component2.anchorMin = new Vector2(0.5f, 0.65f);
				component2.anchorMax = new Vector2(0.5f, 0.65f);
				component2.pivot = new Vector2(0.5f, 0.5f);
				component2.sizeDelta = ScalePopupSize(380f, 200f);
				CrewRelationshipHandlerPatch.ApplyPopupPanelTheme(val, new Color(0.11f, 0.12f, 0.14f, 0.98f));
				VerticalLayoutGroup component3 = val.GetComponent<VerticalLayoutGroup>();
				((LayoutGroup)component3).padding = new RectOffset(14, 14, 12, 12);
				((HorizontalOrVerticalLayoutGroup)component3).spacing = 8f;
				((HorizontalOrVerticalLayoutGroup)component3).childForceExpandWidth = true;
				((HorizontalOrVerticalLayoutGroup)component3).childForceExpandHeight = false;
				Text obj2 = CrewRelationshipHandlerPatch.CreateLabel(val.transform, "PI_Title", "-- Pact Invitation --", 15, (FontStyle)1);
				obj2.alignment = (TextAnchor)4;
				((Graphic)obj2).color = pact.SharedColor;
				int num = pact.MemberIds.Count + ((pact.LeaderGangId >= 0) ? 1 : 0);
				Text obj3 = CrewRelationshipHandlerPatch.CreateLabel(val.transform, "PI_Body", $"The {pact.DisplayName} ({num}/{PACT_MAX_TOTAL_GANGS} gangs) is inviting\nyour outfit to join their alliance!", 12, (FontStyle)0);
				obj3.alignment = (TextAnchor)4;
				((Graphic)obj3).color = new Color(0.85f, 0.85f, 0.8f);
				GameObject obj4 = CrewRelationshipHandlerPatch.CreateHorizontalRow(val.transform, "PI_Buttons");
				obj4.GetComponent<LayoutElement>().minHeight = 35f;
				Button obj5 = CrewRelationshipHandlerPatch.CreateButton(obj4.transform, "PI_Accept", "Join Pact", OnAcceptInvite);
				((Graphic)((Component)obj5).GetComponent<Image>()).color = new Color(0.2f, 0.4f, 0.2f, 0.95f);
				((Component)obj5).GetComponent<LayoutElement>().flexibleWidth = 1f;
				Button obj6 = CrewRelationshipHandlerPatch.CreateButton(obj4.transform, "PI_Deny", "Decline", OnDeclineInvite);
				((Graphic)((Component)obj6).GetComponent<Image>()).color = new Color(0.4f, 0.2f, 0.2f, 0.95f);
				((Component)obj6).GetComponent<LayoutElement>().flexibleWidth = 1f;
				CrewRelationshipHandlerPatch.RethemeMenuHierarchy(val);
				val.transform.SetAsLastSibling();
				_invitePopup = val;
			}
		}

		private static void RefreshText()
		{
		}

		private static void OnAcceptInvite()
		{
			if (_invitingPact == null)
			{
				return;
			}
			try
			{
				AlliancePact alliancePact = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == PLAYER_PACT_SLOT_INDEX);
				if (alliancePact != null)
				{
					CrewRelationshipHandlerPatch.LeavePlayerPactForWar(alliancePact);
				}
				if (SaveData.PlayerJoinedPactIndex >= 0)
				{
					CrewRelationshipHandlerPatch.LeaveCurrentAIPact();
				}
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer != null && !CanGangJoinPact(_invitingPact, humanPlayer.PID.id))
				{
					Debug.Log($"[GameplayTweaks] Player could not join {_invitingPact.DisplayName} because it is full.");
					if ((UnityEngine.Object)(object)_invitePopup != (UnityEngine.Object)null)
					{
						_invitePopup.SetActive(false);
					}
					return;
				}
				SaveData.PlayerJoinedPactIndex = _invitingPact.ColorIndex;
				if (humanPlayer != null && !_invitingPact.MemberIds.Contains(humanPlayer.PID.id))
				{
					_invitingPact.MemberIds.Add(humanPlayer.PID.id);
				}
				if (humanPlayer != null)
				{
					ApplyPactJoinRelationshipBoost(_invitingPact, humanPlayer.PID.id, humanPlayer.social?.PlayerGroupName ?? "Your outfit");
				}
				RefreshPactCache();
				TerritoryColorPatch.RefreshAllTerritoryColors();
				Debug.Log(("[GameplayTweaks] Player accepted invitation to " + _invitingPact.DisplayName + "!"));
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] Accept invite failed: {arg}");
			}
			if ((UnityEngine.Object)(object)_invitePopup != (UnityEngine.Object)null)
			{
				_invitePopup.SetActive(false);
			}
		}

		private static void OnDeclineInvite()
		{
			Debug.Log("[GameplayTweaks] Player declined pact invitation.");
			if ((UnityEngine.Object)(object)_invitePopup != (UnityEngine.Object)null)
			{
				_invitePopup.SetActive(false);
			}
		}
	}
}
}
