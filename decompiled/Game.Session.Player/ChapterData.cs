using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;

namespace Game.Session.Player;

public class ChapterData
{
	public Label chapterID;

	public bool succeeded;

	public SimTime finishTime;

	public EntityID scriptTarget;

	public ChapterData(ChapterDef def, EntityID crew, Label schemeId)
	{
		chapterID = def.id;
		succeeded = false;
		ModQuery query = new ModQuery(crew.FindEntity().data.agent.pid, EntityID.INVALID, crew);
		finishTime = Game.ctx.clock.Now.IncrementDays((int)def.timeCostDays.Evaluate(query));
		scriptTarget = Game.ctx.players.WithID(crew.FindEntity().data.agent.pid).schemes.GetRandomTargetForScheme(schemeId, crew);
	}

	public ChapterData()
	{
	}

	public ChapterDef GetChapterDef()
	{
		return Game.serv.globals.settings.schemes.FindChapterById(chapterID);
	}
}
