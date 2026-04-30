using System.Collections.Generic;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Session.Player.Commands;

public static class CommandButtonUtil
{
	private const string COMMAND_BTN_TEXT = "Text";

	internal static void PopulateCommandButtons(CrewAssignment crew, GameObject allButtons, GameObject buttonTmpl)
	{
		List<CommandButtonState> availableCommands = HumanCommandValidator.GetAvailableCommands(crew);
		allButtons.EnsureChildCount(availableCommands, buttonTmpl);
		allButtons.InitializeChildren(availableCommands, PopulateCommandButton);
		allButtons.SetActive(availableCommands.Count > 0);
	}

	private static void PopulateCommandButton(int _, GameObject card, CommandButtonState state)
	{
		card.GetOrAddComponent<CommandButtonContext>().state = state;
		CommandStatus status = state.status;
		card.SetActive(!status.IsHidden);
		card.SetText("Text", TextUtil.ColorEnabledIf(status.IsEnabled, status.icon));
		Button button = card.GetButton();
		button.interactable = status.IsEnabled;
		button.onClick.SetListener(delegate
		{
			HumanCommandValidator.FindValidator(status.type).OnHumanButtonClick(state.crew);
		});
	}

	internal static CommandButtonState FindButtonStateOrNull(GameObject element)
	{
		CommandButtonContext componentInParent = element.GetComponentInParent<CommandButtonContext>();
		if (!(componentInParent != null))
		{
			return null;
		}
		return componentInParent.state;
	}
}
