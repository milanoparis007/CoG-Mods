using UnityEngine;

namespace Game.UI.Session.Convo;

public class ConversationButtonContext : MonoBehaviour
{
	public ConversationDialog dialog;

	public ConvoState state;

	public ConvoButton button;

	public string forcedMO;

	public void SetData(ConversationDialog dialog, ConvoState state, ConvoButton button, string forcedMO = null)
	{
		this.dialog = dialog;
		this.state = state;
		this.button = button;
		this.forcedMO = forcedMO;
	}

	internal string ProduceButtonMouseover()
	{
		string text = forcedMO;
		if (text == null)
		{
			ConvoButton convoButton = button;
			if (convoButton == null)
			{
				return null;
			}
			text = convoButton.ProduceButtonMouseover(this);
		}
		return text;
	}
}
