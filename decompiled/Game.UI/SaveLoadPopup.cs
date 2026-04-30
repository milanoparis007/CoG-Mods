using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Platform;
using Game.Services;
using Game.UI.Session.Popups;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI;

public class SaveLoadPopup : BasePopup
{
	public enum OpType
	{
		Load,
		Save
	}

	private const string TMPL_CARD = "Templates/Save Load Card";

	private const string HEADER_TEXT = "Header";

	private const string CLOSE_BUTTON = "Close Button";

	private const string CONTENT = "Scroll View List/Viewport/Content";

	private const string BUTTON_LOAD = "Buttons/Load";

	private const string BUTTON_SAVE = "Buttons/Save";

	private const string BUTTON_DELETE = "Buttons/Delete";

	private const string BUTTON_COPY_SEED = "Buttons/Seed/Copy";

	private const string PREVIEW_DESC = "Preview/Description";

	private const string PREVIEW_SEED = "Buttons/Seed/Value";

	private const string PREVIEW_BORDER = "Preview/Border";

	private const string PREVIEW_IMAGE = "Preview/Image";

	private RectTransform _previewMask;

	private RawImage _previewImage;

	private TextMeshProUGUI _previewDesc;

	private TextMeshProUGUI _previewSeed;

	private GameObject _previewBorder;

	private GameObject _tmplCard;

	private GameObject _container;

	private ToggleGroup _toggleGroup;

	private Button _load;

	private Button _save;

	private Button _delete;

	private Button _copy;

	private OpType _optype;

	private Action<IPlatformSaveSlotDescriptor> _loadfn;

	private SaveFileMetadata _newsave;

	private Action<SaveFileMetadata> _savefn;

	private List<SaveLoadCardData> _items;

	private int _selected;

	private const string CARD_TEXT = "Toggle/Text";

	public override UIReference UIReference => UIElements.SaveLoad;

	public bool IsSavePopup => _optype == OpType.Save;

	public bool IsLoadPopup => _optype == OpType.Load;

	private SaveLoadCardData Current
	{
		get
		{
			if (_selected < 0)
			{
				return null;
			}
			return _items[_selected];
		}
	}

	private SaveLoadPopup()
	{
	}

	public static SaveLoadPopup MakeForLoading(Action<IPlatformSaveSlotDescriptor> callback)
	{
		return new SaveLoadPopup
		{
			_optype = OpType.Load,
			_loadfn = callback
		};
	}

	public static SaveLoadPopup MakeForSaving(SaveFileMetadata newsave, Action<SaveFileMetadata> callback)
	{
		return new SaveLoadPopup
		{
			_optype = OpType.Save,
			_newsave = newsave,
			_savefn = callback
		};
	}

	protected override void InitializeOnPush()
	{
		_tmplCard = _go.GetChild("Templates/Save Load Card");
		_container = _panel.GetChild("Scroll View List/Viewport/Content");
		_toggleGroup = _container.GetComponent<ToggleGroup>();
		_panel.SetButtonListener("Close Button", Close);
		_panel.SetText("Header", IsSavePopup ? Loc.Get("ui.saveload.header.save") : Loc.Get("ui.saveload.header.load"));
		_previewMask = _panel.GetChild<RectTransform>("Preview/Image");
		_previewImage = _panel.GetChild<RawImage>("Preview/Image");
		_previewBorder = _panel.GetChild("Preview/Border");
		_previewDesc = _panel.GetChild<TextMeshProUGUI>("Preview/Description");
		_previewDesc.SetText("");
		_previewSeed = _panel.GetChild<TextMeshProUGUI>("Buttons/Seed/Value");
		_previewSeed.SetText("");
		PreviewImageUtils.HidePreview(_previewImage, _previewBorder, deleteOldTexture: false);
		_load = _panel.GetButton("Buttons/Load");
		_load.onClick.SetListener(OnLoadClick);
		_save = _panel.GetButton("Buttons/Save");
		_save.onClick.SetListener(OnSaveClick);
		_delete = _panel.GetButton("Buttons/Delete");
		_delete.onClick.SetListener(OnDeleteClick);
		_copy = _panel.GetButton("Buttons/Seed/Copy");
		_copy.onClick.SetListener(OnCopySeedClick);
		_items = new List<SaveLoadCardData>();
		_selected = -1;
		RefreshSaveLoadButtons();
		Game.serv.sequencer.StartCoroutine(LoadExistingFiles());
	}

	private IEnumerator LoadExistingFiles()
	{
		yield return Game.platform.GetSaveFiles(OnListingSuccess, OnListingFailure);
	}

	protected override void ReleaseOnPop()
	{
		_items = null;
		_selected = -1;
		_load = (_save = (_delete = null));
		_container.DestroyAllChildren();
		_tmplCard = (_container = null);
		_toggleGroup = null;
		_loadfn = null;
		_savefn = null;
		_newsave = null;
	}

	private void Select(SaveLoadCardData data)
	{
		_selected = data.index;
		RefreshSaveLoadButtons();
		RefreshDescription();
		RefreshPreview();
	}

	private void RefreshSaveLoadButtons()
	{
		bool interactable;
		bool num = (interactable = _selected >= 0);
		bool interactable2 = num && Current.CanLoad;
		bool interactable3 = num && Current.CanDelete;
		_save.gameObject.SetActive(IsSavePopup);
		_save.interactable = interactable;
		_load.gameObject.SetActive(IsLoadPopup);
		_load.interactable = interactable2;
		_delete.gameObject.SetActive(value: true);
		_delete.interactable = interactable3;
	}

	private void RefreshDescription()
	{
		uint num = (Current.IsNewGame ? Game.ctx.scenario.rngseed : (Current?.metadata.rngseed ?? 0));
		string text = Loc.Get("ui.customgame.forceseed");
		string sourceText = ((num == 0) ? "" : (text + ": " + num.ToString("D", CultureInfo.InvariantCulture)));
		_previewSeed.SetText(sourceText);
		string sourceText2 = ((_selected >= 0 && !Current.IsNewGame) ? Current.description : "");
		_previewDesc.SetText(sourceText2);
	}

	private void RefreshPreview()
	{
		if (Current?.slot?.PreviewImage == null)
		{
			PreviewImageUtils.HidePreview(_previewImage, _previewBorder, deleteOldTexture: false);
		}
		else
		{
			PreviewImageUtils.ShowPreview(Current.slot.PreviewImage, _previewImage, _previewMask, _previewBorder);
		}
	}

	private void OnListingFailure(kGetFileResult result)
	{
		if (base.IsShowing)
		{
			Logger.Error("ERROR: ", result);
			_items.Clear();
			_selected = -1;
			_container.DestroyAllChildren();
		}
	}

	private void OnListingSuccess(List<IPlatformSaveSlotDescriptor> slots)
	{
		_items = (from data in slots.SelectIntoNewList((IPlatformSaveSlotDescriptor slot) => new SaveLoadCardData(slot))
			orderby data.metadata.saveTime.Ticks descending
			select data).ToList();
		if (_newsave != null)
		{
			_items.Insert(0, new SaveLoadCardData(_newsave));
		}
		for (int num = 0; num < _items.Count; num++)
		{
			_items[num].index = num;
		}
		_container.EnsureChildCount(_items, _tmplCard);
		_container.InitializeChildren(_items, InitializeCard);
		if (_items.Count > 0)
		{
			_items.FirstOrDefaultFast().toggle.isOn = true;
		}
	}

	private void InitializeCard(int index, GameObject card, SaveLoadCardData data)
	{
		if (base.IsShowing)
		{
			card.GetOrAddComponent<SaveLoadCardContext>().data = data;
			data.SetCard(card);
			data.card.SetText("Toggle/Text", data.message);
			data.toggle.onValueChanged.SetListener(delegate(bool val)
			{
				OnCardSelected(data, val);
			});
			data.toggle.group = _toggleGroup;
		}
	}

	private void OnCardSelected(SaveLoadCardData data, bool value)
	{
		if (value && base.IsShowing)
		{
			Select(data);
		}
	}

	private void OnLoadClick()
	{
		if (!Current.IsVersionCompatible)
		{
			uint version = Current.metadata?.gameVersion ?? 0;
			OkPopup.ShowOkCancel(Loc.Get("ui.saveload.warning.confirm", "version", GameSettings.GetVersionString(version), "current", GameSettings.GetVersionString(GameSettings.version)), DoLoad, delegate
			{
			});
		}
		else
		{
			DoLoad();
		}
	}

	private void DoLoad()
	{
		if (Current?.slot != null && _loadfn != null)
		{
			_loadfn(Current.slot);
		}
		else
		{
			Logger.Error("Missing save slot info for " + Current?.message);
		}
		Close();
	}

	private void OnSaveClick()
	{
		if (!Current.IsNewGame)
		{
			OkPopup.ShowOkCancel(Loc.Get("ui.saveload.overwrite.okcancel"), delegate
			{
				_savefn(_items[0].metadata);
				Game.serv.sequencer.StartCoroutine(DeleteFile());
				Close();
			}, delegate
			{
			});
		}
		else
		{
			if (Current?.metadata != null && _savefn != null)
			{
				_savefn(Current.metadata);
			}
			else
			{
				Logger.Error("Missing save slot meta for " + Current?.message);
			}
			Close();
		}
	}

	private void OnDeleteClick()
	{
		if (Current?.metadata != null)
		{
			OkPopup.ShowOkCancel(Loc.Get("ui.saveload.delete.okcancel"), delegate
			{
				Game.serv.sequencer.StartCoroutine(DeleteFile());
			}, delegate
			{
			});
		}
		else
		{
			Logger.Error("Missing save slot meta for " + Current?.message);
		}
	}

	private void OnCopySeedClick()
	{
		string systemCopyBuffer = "";
		if (Current?.metadata != null)
		{
			systemCopyBuffer = ((Current.metadata.rngseed == 0) ? "" : Current.metadata.rngseed.ToString("D", CultureInfo.InvariantCulture));
		}
		else if (Current.IsNewGame)
		{
			systemCopyBuffer = ((Game.ctx.scenario.rngseed == 0) ? "" : Game.ctx.scenario.rngseed.ToString("D", CultureInfo.InvariantCulture));
		}
		GUIUtility.systemCopyBuffer = systemCopyBuffer;
	}

	private IEnumerator DeleteFile()
	{
		yield return Game.serv.saveload.DeleteGame(Current.slot, delegate
		{
			UnityEngine.Object.Destroy(Current.card);
			RefreshSaveLoadButtons();
		}, delegate
		{
		});
	}
}
