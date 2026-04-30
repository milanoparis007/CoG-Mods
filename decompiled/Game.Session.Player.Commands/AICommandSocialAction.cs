using Game.Core;

namespace Game.Session.Player.Commands;

public class AICommandSocialAction : InstantAICommand
{
	public Label socialAction;

	public EntityID targetPeepId;

	public AICommandSocialAction()
	{
	}

	public AICommandSocialAction(PlayerID pid, EntityID eid, EntityID targetPeepId, Label socialAction)
		: base(pid, CommandType.SocialAction, eid)
	{
		Game.serv.globals.settings.people.social.relationships.GetSocialActionDefinition(socialAction);
		this.socialAction = socialAction;
		this.targetPeepId = targetPeepId;
	}

	protected override void PerformTurnActions()
	{
		base.PerformTurnActions();
		GetPlayer().social.PerformSocialActionOn(socialAction, targetPeepId, peepId);
	}
}
