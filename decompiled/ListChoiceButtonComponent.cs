using System.Collections.Generic;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[AddComponentMenu("Game UI/List Choice Button")]
public class ListChoiceButtonComponent : MonoBehaviour
{
	public struct LabelAndData
	{
		public static readonly LabelAndData NONE = new LabelAndData
		{
			i = -1,
			label = null,
			data = null
		};

		public int i;

		public string label;

		public object data;
	}

	public Button Button;

	public Listeners<LabelAndData> OnSelection = new Listeners<LabelAndData>();

	private List<LabelAndData> _choices = new List<LabelAndData>();

	private int _index;

	public int CountChoices => _choices.Count;

	public int CurrentIndex => _index;

	public LabelAndData CurrentChoice
	{
		get
		{
			if (_index < 0 || _index >= _choices.Count)
			{
				return LabelAndData.NONE;
			}
			return _choices[_index];
		}
	}

	public void AddChoice(string label, object data)
	{
		_choices.Add(new LabelAndData
		{
			i = _choices.Count,
			label = label,
			data = data
		});
		if (_choices.Count == 1)
		{
			SetIndex(0, sendEvent: false);
		}
	}

	public void ClearChoices()
	{
		_choices.Clear();
		SetIndex(-1, sendEvent: false);
	}

	public object GetCurrentChoiceData()
	{
		return CurrentChoice.data;
	}

	public T GetCurrentChoiceData<T>() where T : class
	{
		return CurrentChoice.data as T;
	}

	public void OnClick()
	{
		if (_choices.Count > 1)
		{
			SetIndex((_index + 1) % _choices.Count, sendEvent: true);
		}
	}

	private void SetIndex(int index, bool sendEvent)
	{
		_index = index;
		LabelAndData currentChoice = CurrentChoice;
		Button.gameObject.GetChildText().SetText(currentChoice.label);
		if (sendEvent)
		{
			OnSelection?.Invoke(currentChoice);
		}
	}
}
