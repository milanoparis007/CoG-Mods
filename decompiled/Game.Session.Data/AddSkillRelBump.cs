using Game.Session.Player;

namespace Game.Session.Data;

public class AddSkillRelBump : AddRelBuff
{
	public AddSkillRelBump()
	{
		with = With.Owner;
		id = BuffConstants.TICKET_TAUGHT_SKILL;
	}
}
