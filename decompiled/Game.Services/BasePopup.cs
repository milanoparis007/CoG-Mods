using System.Collections.Generic;
using Game.Services.Input;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Services;

public abstract class BasePopup : IUIPopup, ISmartStackElement, IKeyboardHandler
{
	protected bool _pausesGame;

	protected bool _hidesWhenCovered;

	protected bool _blursWhenActive;

	private bool _pushed;

	private bool _popped;

	protected GameObject _go;

	protected GameObject _panel;

	protected KeyboardHandler _keyhandler;

	public GameObject GameObject => _go;

	public abstract UIReference UIReference { get; }

	public bool IsShowing
	{
		get
		{
			if (_pushed)
			{
				return !_popped;
			}
			return false;
		}
	}

	public virtual bool ForceDeselectOnClose => false;

	protected abstract void InitializeOnPush();

	protected abstract void ReleaseOnPop();

	public KeyboardHandler GetKeyHandler()
	{
		return _keyhandler;
	}

	protected BasePopup()
		: this(hides: false, blurs: true)
	{
	}

	protected BasePopup(bool hides, bool blurs)
	{
		_pausesGame = false;
		_hidesWhenCovered = hides;
		_blursWhenActive = blurs;
	}

	public virtual void OnPushed(object stack)
	{
		_pushed = true;
		_go = Game.serv.ui.GetUI(UIReference);
		_panel = _go.GetChild("Panel");
		UISoundUtil.AttachSFXToAllClickables(_go);
		InitializeOnPush();
		_go.SetActive(value: false);
		InitializeKeyHandler();
	}

	protected virtual void InitializeKeyHandler()
	{
		_keyhandler = new BasicKeyboardHandler(KeyboardHandler.Priority.HighestModalDialog, new List<KeyInput>
		{
			new KeyInput(KeyAction.FinishTurn, delegate
			{
			}),
			new KeyInput(KeyAction.AdvanceToNextCrew, delegate
			{
			})
		}, KeyboardHandler.Fallthrough.OnlyIfNotProcessed);
	}

	public virtual void OnActivated(bool pushed)
	{
		if (_pausesGame && Game.ctx != null && Game.ctx.IsInteractive)
		{
			Game.ctx.clock.PauseAnimations(this);
		}
		_go.SetActive(value: true);
		Game.serv.keyboard.PushHandler(this);
		EventSystem.current.SetSelectedGameObject(null);
		_go.transform.SetAsLastSibling();
	}

	public virtual void OnDeactivated(bool popped)
	{
		if (popped || _hidesWhenCovered)
		{
			_go.SetActive(value: false);
			if (ForceDeselectOnClose)
			{
				Game.ctx?.selection?.HandleDeselect();
			}
		}
		Game.serv.keyboard.RemoveHandler(this);
		if (_pausesGame && Game.ctx != null && Game.ctx.IsInteractive)
		{
			Game.ctx.clock.UnpauseAnimations(this);
		}
	}

	public virtual void OnPopped()
	{
		_go.SetActive(value: false);
		_popped = true;
		ReleaseOnPop();
		_keyhandler = null;
		_panel = null;
		_go = null;
	}

	public virtual void Close()
	{
		Game.serv.ui.RemovePopup(this);
	}
}
