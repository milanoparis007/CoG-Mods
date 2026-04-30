using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Assets;

public sealed class SFXManager : AbstractSessionManager
{
	private void Play(SFXType type)
	{
		Game.serv.audio.PlayUISFX(type);
	}

	public void PlayWarning()
	{
		Play(SFXType.TickerWarning);
	}

	public void PlayCrewSelect()
	{
		Game.serv.audio.PlayerCrewSelectSFX();
	}

	public void PlayDriving(Entity vehicle)
	{
		Game.serv.audio.PlayDrivingSFX(vehicle.config.mobile.IsCar);
	}

	public void PlayAutomationChange(bool start)
	{
		Play(start ? SFXType.EventDeliveryStart : SFXType.EventDeliveryStop);
	}

	public void PlayScopeOutBiz()
	{
		Play(SFXType.ScopeOutBusiness);
	}

	public void PlayIntroduction()
	{
		Play(SFXType.EventSocialBoost);
	}

	public void PlayPartyImminent()
	{
		Play(SFXType.EventPartyAlert);
	}

	public void PlayOutOfPoints(bool moves = false, bool actions = false)
	{
		if (moves)
		{
			Play(SFXType.OutOfMoves);
		}
		if (actions)
		{
			Play(SFXType.OutOfActions);
		}
	}

	public void PlayCornerReveal(bool interesting)
	{
		Play(interesting ? SFXType.CornerRevealPositive : SFXType.CornerRevealAny);
	}

	public void PlayTerritoryChanged(bool gained)
	{
		Play(gained ? SFXType.EventTerritoryExpanded : SFXType.EventTerritoryContracted);
	}

	public void PlayStalled()
	{
		Play(SFXType.EventStall);
	}

	public void PlayHealing()
	{
		Play(SFXType.EventHealing);
	}

	public void PlayCombatHappened()
	{
		Play(SFXType.EventCombatHappened);
	}

	public void PlayGoonQuestReady()
	{
		Play(SFXType.EventQuestComplete);
	}

	public void PlayMoneyConfirm()
	{
		Play(SFXType.ClickBuySellConfirm);
	}

	public void PlayConvoStart(VisitState visit, PlayerInfo otherPlayer)
	{
		Play(FindType());
		SFXType FindType()
		{
			if (otherPlayer != null)
			{
				if (otherPlayer.IsCopOrFed)
				{
					return SFXType.ConvoPolice;
				}
				if (otherPlayer.IsAnyAIPlayer)
				{
					return SFXType.ConvoAny;
				}
			}
			return (visit?.building?.config.building?.type).GetValueOrDefault() switch
			{
				ZoneType.Com => SFXType.ConvoCommercial, 
				ZoneType.Ind => SFXType.ConvoIndustrial, 
				ZoneType.Res => SFXType.ConvoResidential, 
				_ => SFXType.ConvoAny, 
			};
		}
	}

	public void PlayOwnedBizShow(VisitState visit)
	{
		Play(FindType());
		SFXType FindType()
		{
			return (visit?.building?.config.building?.type).GetValueOrDefault() switch
			{
				ZoneType.Com => SFXType.OwnedBizCommercial, 
				ZoneType.Ind => SFXType.OwnedBizIndustrial, 
				ZoneType.Res => SFXType.OwnedBizResidential, 
				_ => SFXType.OwnedBizResidential, 
			};
		}
	}

	public void PlayOwnedBizTab()
	{
		Play(SFXType.ClickBuildingTab);
	}

	public void PlayPersonInfoShow()
	{
		Play(SFXType.ClickSocialNetwork);
	}
}
