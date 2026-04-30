namespace Game.UI;

public class UIMouseoverName
{
	public static readonly UIMouseoverName ConvoMouseover = new UIMouseoverName
	{
		path = "Convo Mouseover"
	};

	public static readonly UIMouseoverName TextMouseoverTL = new UIMouseoverName
	{
		path = "Text Mouseover Bounded TL Wide"
	};

	public static readonly UIMouseoverName TextMouseoverTR = new UIMouseoverName
	{
		path = "Text Mouseover Bounded TR Wide"
	};

	public static readonly UIMouseoverName TextMouseoverBoundedTRSmall = new UIMouseoverName
	{
		path = "Text Mouseover Bounded TR Small"
	};

	public static readonly UIMouseoverName TextMouseoverBoundedTR = new UIMouseoverName
	{
		path = "Text Mouseover Bounded TR"
	};

	public static readonly UIMouseoverName TextMouseoverBoundedTL = new UIMouseoverName
	{
		path = "Text Mouseover Bounded TL"
	};

	public static readonly UIMouseoverName LedgerMouseover = new UIMouseoverName
	{
		path = "Ledger Mouseover"
	};

	public static readonly UIMouseoverName TickerMouseover = new UIMouseoverName
	{
		path = "Ticker Mouseover"
	};

	public static readonly UIMouseoverName CrewButtonMouseover = new UIMouseoverName
	{
		path = "Crew Button Mouseover"
	};

	public static readonly UIMouseoverName PickMouseover = new UIMouseoverName
	{
		path = "Pick Mouseover"
	};

	public string path { get; private set; }
}
