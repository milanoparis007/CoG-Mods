using System.Collections;
using System.Linq;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;
using Game.UI.Session.Tutorial;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonExtortion : BaseLesson
{
	private struct Candidate
	{
		public Entity building;

		public float distance;
	}

	public override int LessonNumber => 13;

	public override int StepsCount => 9;

	public override bool InhibitsQuestRequests => false;

	public override IEnumerator Run()
	{
		ShowStepBlurb(1, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Entity target = FindExtortionTarget();
		if (target != null)
		{
			TweenCameraTo(target, HUDUtil.ZoomInLevel.PullBack);
		}
		ShowStepBlurb(2, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(3, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(4, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		if (target != null)
		{
			TweenCameraTo(target);
		}
		yield return new WaitForSecondsRealtime(0.5f);
		ShowStepBlurb(5, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(6, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting);
		while (!HasExtortedAny())
		{
			yield return new WaitForSecondsRealtime(1f);
		}
		yield return new WaitForSecondsRealtime(1f);
		while (Game.serv.ui.ContainsPopup<PhotoPopup>())
		{
			yield return new WaitForSecondsRealtime(1f);
		}
		Game.ctx.selection.ClearActive();
		ShowStepBlurbGendered(7, GetPlayerPeep(), FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Agreed);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(8, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(9, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Understood);
		while (!GotNext())
		{
			yield return null;
		}
	}

	private bool HasExtortedAny()
	{
		return CountExtorted() > 0;
	}

	private int CountExtorted()
	{
		PlayerOutposts outposts = base.HumanPlayer.outposts;
		return (from building in base.HumanPlayer.territory.GetAllOwnedNodesUnsafe().SelectMany((NodeID nodeId) => nodeId.FindNode().FindAllInterestingBuildings())
			where WasExtorted(building)
			select building).ToList().Count;
		bool WasExtorted(Entity building)
		{
			Entity biz = BuildingUtil.FindBizForBuilding(building);
			return outposts.IsBizPayingTribute(biz);
		}
	}

	private Entity FindExtortionTarget()
	{
		PlayerOutposts outposts = base.HumanPlayer.outposts;
		WorldPos origin = outposts.GetOutpostEntriesUnsafe().FirstOrDefaultFast().OutpostNode.nodeId.FindNode().pos;
		return (from c in (from building in base.HumanPlayer.territory.GetAllOwnedNodesUnsafe().SelectMany((NodeID nodeId) => nodeId.FindNode().FindAllInterestingBuildings())
				where CanExtort(building)
				select new Candidate
				{
					building = building,
					distance = (building.data.board.worldpos - origin).Magnitude
				}).ToList()
			orderby c.distance
			select c).ToList().FirstOrDefaultFast().building;
		bool CanExtort(Entity building)
		{
			BuildingComponent building2 = building.components.building;
			if (!building2.IsInteractableByPlayer(PlayerID.HumanPlayer))
			{
				return false;
			}
			if (building2.IsControlledByAnyPlayer())
			{
				return false;
			}
			if (building2.IsOutpost || building2.IsPoliceStation || building2.IsSafehouse)
			{
				return false;
			}
			Entity biz = BuildingUtil.FindBizForBuilding(building);
			bool num = outposts.IsBizPayingTribute(biz);
			bool flag = outposts.IsBizRejectingTribute(biz);
			if (!num)
			{
				return !flag;
			}
			return false;
		}
	}

	public override void OnSkip()
	{
		if (Game.ctx.players.Human.crew.CrewFreeCapacity() < 1)
		{
			Game.ctx.players.Human.crew.CrewGrowth.DebugApplyCapDelta(1);
			Game.ctx.players.Human.crew.CrewGrowth.OnPlayerTurnStarted(Game.ctx.players.Human.crew);
			Game.ctx.hud.bar.TutForceRefresh();
		}
		Game.ctx.tutorial.TryStartManualLesson(LessonNumber + 1);
	}
}
