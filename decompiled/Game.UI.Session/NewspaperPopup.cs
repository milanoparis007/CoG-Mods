using System;
using Game.Services;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session;

public class NewspaperPopup : BasePopup
{
	private const string DATE = "Panel/Date";

	private const string HEADLINE = "Panel/Headline";

	private const string SUBHEAD = "Panel/Subhead";

	private const string PHOTO_CARD = "Panel/Photo Card";

	private const string PHOTO_IMG = "Panel/Photo Card/Border/Image";

	private const string BTN_CLOSE = "Panel/Close Button";

	private string _header;

	private string _subhead;

	private PhotoConfig _photo;

	private Action _callback;

	public override UIReference UIReference => UIElements.NewspaperPopup;

	public NewspaperPopup(string header, PhotoConfig? photo = null, Action callback = null)
	{
		_header = header;
		_photo = photo.GetValueOrDefault();
		_callback = callback;
		string newspaperSubheadKey = Game.ctx.simman.residences.GetNewspaperSubheadKey();
		_subhead = Loc.Get(newspaperSubheadKey);
	}

	protected override void InitializeOnPush()
	{
		_go.SetButtonListener("Panel/Close Button", Close);
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		_go.SetButtonListener("Panel/Close Button", Close);
		RefreshContents();
	}

	protected override void ReleaseOnPop()
	{
		_header = (_subhead = null);
		_photo = null;
		_callback = null;
	}

	private void RefreshContents()
	{
		_go.SetText("Panel/Date", Loc.FormatDateLong(Game.ctx.clock.Now));
		_go.SetText("Panel/Headline", _header);
		_go.SetText("Panel/Subhead", _subhead);
		Sprite sprite = (_photo.HasSprite ? Game.ctx.hud.uisprites.photos.Find(_photo.sprite) : null);
		if (sprite != null)
		{
			_go.GetImage("Panel/Photo Card/Border/Image").sprite = sprite;
		}
		_go.SetActive("Panel/Photo Card", sprite != null);
	}

	public override void Close()
	{
		_callback?.Invoke();
		base.Close();
	}
}
