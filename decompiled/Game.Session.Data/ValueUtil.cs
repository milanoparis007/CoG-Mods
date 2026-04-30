using SomaSim.Util;

namespace Game.Session.Data;

public static class ValueUtil
{
	public static bool TestCurrentValue(Fixnum current, Test @is, Fixnum testValue)
	{
		switch (@is)
		{
		case Test.EqualTo:
			return current == testValue;
		case Test.NotEqualTo:
			return current != testValue;
		case Test.MoreOrEqualTo:
		case Test.AtLeast:
			return current >= testValue;
		case Test.LessOrEqualTo:
		case Test.AtMost:
			return current <= testValue;
		case Test.LessThan:
			return current < testValue;
		case Test.MoreThan:
			return current > testValue;
		default:
			return false;
		}
	}
}
