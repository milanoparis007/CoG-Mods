using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session;

public class PhotoPopup : BasePopup
{
	private const string BTN_CONTINUE = "Panel/Footer/Continue Button";

	private const string CONTAINER = "Panel/Photo Container";

	private const string TEMPLATE = "Templates/Photo Card";

	private const string CARD_BORDER = "Border";

	private const string CARD_IMAGE = "Border/Image";

	private const string CARD_FRAME = "Border/Frame";

	private const string CARD_TEXT = "Border/Text";

	private Action _callback;

	private List<PhotoConfig> _photos;

	private SFXType _sfx;

	private GameObject _container;

	private GameObject _tmplCard;

	public override UIReference UIReference => UIElements.PhotoPopup;

	public PhotoPopup(List<PhotoConfig> names, Action onOk)
		: this(names, SFXType.None, onOk)
	{
	}

	public PhotoPopup(List<PhotoConfig> names, SFXType sfx, Action onOk)
	{
		_photos = new List<PhotoConfig>(names);
		_sfx = sfx;
		_callback = onOk;
	}

	protected override void InitializeOnPush()
	{
		_container = _go.GetChild("Panel/Photo Container");
		_tmplCard = _go.GetChild("Templates/Photo Card");
	}

	protected override void ReleaseOnPop()
	{
		_tmplCard = (_container = null);
		_callback = null;
	}

	private void OnContinue()
	{
		_callback?.Invoke();
		Close();
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		_go.SetButtonListener("Panel/Footer/Continue Button", OnContinue);
		_go.SetActive("Panel/Footer/Continue Button", value: false);
		_container.EnsureChildCount(_photos, _tmplCard);
		_container.InitializeChildren(_photos, InitCard);
		TimerUtil.RunAfterTime(delegate
		{
			if (_sfx != SFXType.None)
			{
				Game.serv.audio.PlayUISFX(_sfx);
			}
		}, 0.25f);
		TimerUtil.RunAfterTime(delegate
		{
			if (!(_go == null))
			{
				_go.SetActive("Panel/Footer/Continue Button", value: true);
			}
		}, (float)_photos.Count * 0.5f);
	}

	public override void OnDeactivated(bool popped)
	{
		foreach (Transform item in _container.transform)
		{
			LeanTween.cancel(item.gameObject);
		}
		base.OnDeactivated(popped);
	}

	private void InitCard(int index, GameObject card, PhotoConfig config)
	{
		SplitMix64 rng = new SplitMix64((uint)(index << 8 + Time.frameCount));
		Sprite sprite = Game.ctx.hud.uisprites.photos.Find(config.sprite);
		if (sprite == null)
		{
			card.SetActive(value: false);
			return;
		}
		Image image = card.GetImage("Border/Image");
		image.sprite = sprite;
		image.color = new Color(1f, 1f, 1f, 0f);
		Image frame = card.GetImage("Border/Frame");
		frame.color = new Color(1f, 1f, 1f, 0f);
		TextMeshProUGUI text = card.GetText("Border/Text");
		string text2 = config.ProduceText();
		text.gameObject.SetActive(text2 != null);
		if (text2 != null)
		{
			text.color = ColorUtil.HexToColor(ColorConstants.TEXT_HEX_POSTCARD).SetAlpha(0f);
			text.SetText(text2);
		}
		Image border = card.GetImage("Border");
		float num = rng.Generate(-5f, 5f);
		num += Mathf.Sign(num) * 5f;
		border.transform.localEulerAngles = new Vector3(0f, 0f, num);
		border.transform.localPosition = new Vector3(rng.Generate(-10f, 10f), rng.Generate(-10f, 10f), 0f);
		border.color = new Color(1f, 1f, 1f, 0f);
		TimerUtil.RunAfterTime(delegate
		{
			if (!(card == null) && !(_go == null))
			{
				LeanTween.cancel(card);
				LeanTween.value(card, delegate(float c)
				{
					Image image2 = border;
					Image image3 = image;
					Color color = (frame.color = new Color(1f, 1f, 1f, c));
					Color color3 = (image3.color = color);
					image2.color = color3;
					text.color = ColorUtil.HexToColor(ColorConstants.TEXT_HEX_POSTCARD).SetAlpha(c);
				}, 0f, 1f, 1f).setEase(LeanTweenType.easeOutQuad);
			}
		}, (float)index * 0.75f);
		card.SetActive(value: true);
	}
}
