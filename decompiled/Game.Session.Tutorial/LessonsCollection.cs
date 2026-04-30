using System.Collections.Generic;

namespace Game.Session.Tutorial;

public static class LessonsCollection
{
	public static List<BaseLesson> GetLinearLessonSequence()
	{
		return new List<BaseLesson>
		{
			new LessonWASD(),
			new LessonThisIsYou(),
			new LessonTopHUD(),
			new LessonSafehouse(),
			new LessonSameNode(),
			new LessonFindingZiggy(),
			new LessonSellingToZiggy(),
			new LessonIntroToZiggysFriend(),
			new LessonExploration(),
			new LessonFirstBrewery(),
			new LessonOverlays(),
			new LessonTerritory(),
			new LessonExtortion()
		};
	}

	public static List<BaseLesson> GetManuallyStartedLessons()
	{
		return new List<BaseLesson>
		{
			new LessonCrew(),
			new LessonDeliveries(),
			new LessonMovingOn()
		};
	}
}
