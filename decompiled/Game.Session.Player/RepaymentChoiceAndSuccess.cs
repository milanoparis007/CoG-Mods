using Game.Core;
using SomaSim.Util;

namespace Game.Session.Player;

public struct RepaymentChoiceAndSuccess
{
	public Label choiceId;

	public Fixnum success;

	public RepaymentChoiceAndSuccess(Label choiceId, Fixnum success)
	{
		this.choiceId = choiceId;
		this.success = success;
	}
}
