using Game.Core;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.OwnedGambling;

public class DebtorMouseoverCtx : MonoBehaviour
{
	public EntityID debtor;

	public Fixnum debt;

	public Fixnum maxcredit;

	public Fixnum lastturn;

	public void Set(EntityID debtor, Fixnum debt, Fixnum lastturn, Fixnum maxcredit)
	{
		this.debtor = debtor;
		this.debt = debt;
		this.lastturn = lastturn;
		this.maxcredit = maxcredit;
	}
}
