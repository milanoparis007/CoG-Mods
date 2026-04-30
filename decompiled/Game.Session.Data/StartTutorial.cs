using Game.Services;
using Game.Session.Tutorial;

namespace Game.Session.Data;

public class StartTutorial : VisitGrant
{
	public int lesson;

	public override GrantReq RequiredContext => GrantReq.Nothing;

	public override void Apply(GrantContext ctx)
	{
		Game.ctx.tutorial.TryStartManualLesson(lesson);
	}

	public override string Describe(GrantContext ctx)
	{
		string lessonTitle = BaseLesson.GetLessonTitle(lesson);
		return Loc.Get("ui.grants.starttutorial.describe", "name", lessonTitle);
	}
}
