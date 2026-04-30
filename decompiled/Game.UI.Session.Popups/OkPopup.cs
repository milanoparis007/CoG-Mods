using System;
using Game.Core;

namespace Game.UI.Session.Popups;

public static class OkPopup
{
	public static void Show(string message)
	{
		Game.serv.ui.AddPopup(new OkCancelPopup(message));
	}

	public static void ShowOk(string message, Action onOk, bool hides = false)
	{
		Game.serv.ui.AddPopup(new OkCancelPopup(message, onOk, null, hides));
	}

	public static void ShowOkCancel(string message, Action onOk, Action onCancel)
	{
		Game.serv.ui.AddPopup(new OkCancelPopup(message, onOk, onCancel));
	}

	public static void ShowOkCancel(string message, string okText, string cancelText, Action onOk, Action onCancel)
	{
		Game.serv.ui.AddPopup(new OkCancelPopup(message, okText, cancelText, onOk, onCancel));
	}

	public static void Show(EntityID peepId, string message)
	{
		Game.serv.ui.AddPopup(new PortraitPopup(peepId, message));
	}

	public static void ShowOk(EntityID peepId, string message, Action onOk)
	{
		Game.serv.ui.AddPopup(new PortraitPopup(peepId, message, onOk));
	}

	public static void ShowOkCancel(EntityID peepId, string message, Action onOk, Action onCancel)
	{
		Game.serv.ui.AddPopup(new PortraitPopup(peepId, message, onOk, onCancel));
	}
}
