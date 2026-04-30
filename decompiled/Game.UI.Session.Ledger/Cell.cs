using System.Text;
using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.UI.Session.Ledger;

internal class Cell : IReportCell
{
	public enum Alignment
	{
		Left,
		Right
	}

	public readonly Alignment align = Alignment.Right;

	public readonly string suffix;

	public readonly object sortvalue;

	public readonly string origtext;

	public string text;

	public int width = 10;

	private static StringBuilder _sbexp = new StringBuilder(120);

	public Cell(string text, Alignment align = Alignment.Left)
	{
		origtext = text;
		sortvalue = text;
		this.align = align;
		suffix = "…";
		UpdateWidth(width);
	}

	public Cell(int value)
	{
		origtext = value.ToString();
		sortvalue = value;
		UpdateWidth(width);
	}

	public Cell(float value, int decimals)
	{
		origtext = Loc.FormatNumber((Fixnum)value, decimals);
		sortvalue = value;
		UpdateWidth(width);
	}

	public Cell(Money cash)
	{
		origtext = Loc.Money(cash, 2);
		sortvalue = (int)cash;
		UpdateWidth(width);
	}

	public Cell(Price price)
	{
		origtext = Loc.Price(price, abs: false, 2);
		sortvalue = (int)price;
		UpdateWidth(width);
	}

	public void UpdateWidth(int width)
	{
		this.width = width;
		text = TrimOrPad(origtext, width, align, suffix);
	}

	public string GetText()
	{
		return text;
	}

	internal static string MaybeExpandText(string text)
	{
		foreach (char value in text)
		{
			_sbexp.Append(value);
		}
		return _sbexp.ToStringAndReset();
	}

	private static bool IsCJKGlyph(char ch)
	{
		if (ch >= '⺀')
		{
			return ch <= '鿿';
		}
		return false;
	}

	private static int GetTextLength(string text)
	{
		int num = 0;
		foreach (char ch in text)
		{
			num += ((!IsCJKGlyph(ch)) ? 1 : 2);
		}
		return num;
	}

	private static string MakeSubstring(string text, int width)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		int num = 0;
		foreach (char c in text)
		{
			num += ((!IsCJKGlyph(c)) ? 1 : 2);
			if (num > width)
			{
				break;
			}
			stringBuilder.Append(c);
		}
		return stringBuilder.ToStringAndReturnToPool();
	}

	private static string MakePadding(string text, int width)
	{
		int value = width - GetTextLength(text);
		return "".PadRight(MathUtil.ClampMin(value, 0), ' ');
	}

	internal static string TrimOrPad(string orig, int width, Alignment align, string suffix = null)
	{
		string text = MaybeExpandText(orig);
		int textLength = GetTextLength(text);
		if (textLength == width)
		{
			return text;
		}
		if (textLength > width)
		{
			if (suffix != null)
			{
				return MakeSubstring(text, width - suffix.Length) + suffix;
			}
			return MakeSubstring(text, width);
		}
		switch (align)
		{
		case Alignment.Left:
			return text + MakePadding(text, width);
		case Alignment.Right:
			return MakePadding(text, width) + text;
		default:
			Logger.Warning("Unknown alignment: " + align);
			return text;
		}
	}
}
