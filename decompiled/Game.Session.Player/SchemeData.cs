using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.UI.Session;

namespace Game.Session.Player;

public class SchemeData
{
	public enum SchemeState
	{
		Available,
		InProgress,
		Epilogue,
		OnCooldown
	}

	public Label schemeID;

	public EntityID crewAssigned;

	public ChapterData currentChapter;

	public SchemeState currState;

	public string epilogueText;

	public EntityID overallTarget;

	public List<HistoryItem> schemeChapterHistory = new List<HistoryItem>();

	public SchemeData(SchemeDef def, EntityID crew)
	{
		schemeID = def.id;
		crewAssigned = crew;
		currentChapter = new ChapterData(Game.serv.globals.settings.schemes.FindChapterById(def.startup.firstChapter), crew, def.id);
		currState = SchemeState.InProgress;
	}

	public SchemeData()
	{
	}

	public bool HasCompletedChapterInThisScheme(Label chapterID)
	{
		return schemeChapterHistory.Select((HistoryItem x) => x.chapterId).Contains(chapterID);
	}

	public void SetTarget(EntityID targetSelected)
	{
		overallTarget = targetSelected;
		Node node = targetSelected.FindEntity().components.board.GetNode();
		if (!node.known.Contains(PlayerID.HumanPlayer))
		{
			Game.ctx.players.Human.meetings.MarkNodeAsKnown(node, expectedSeen: true, instant: true);
		}
		PersonInfoUtil.TweenCameraToEntity(overallTarget);
	}

	public bool IsWaitingForDecision()
	{
		if (currentChapter.finishTime <= Game.ctx.clock.Now)
		{
			return epilogueText == null;
		}
		return false;
	}

	public bool DecisionPopThisTurn()
	{
		SimTime now = Game.ctx.clock.Now;
		SimTime simTime = Game.ctx.clock.Now.IncrementDays(-Game.ctx.clock.DaysPerTurn);
		bool num = now.days >= currentChapter.finishTime.days;
		bool flag = simTime.days >= currentChapter.finishTime.days;
		if (num)
		{
			return !flag;
		}
		return false;
	}
}
