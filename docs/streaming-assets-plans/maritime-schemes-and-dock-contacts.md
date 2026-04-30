# Maritime Schemes And Dock Contacts

## Goal

Group the dock, smuggling, and contact-to-union progression into one chain so it can be designed as a coherent content arc.

## Scope

1. New `Port Security Bribe` from a maritime dock for an accountant to start the first scheme.
2. Smuggle multiple jewelry types for dirty or clean cash through a maritime trader.
3. First schemes reward a `dock contract` item that starts the maritime-trade quest.
4. Later mini-schemes unlock a `maritime dock contact`.
5. Dock contact starts another scheme for ore types, requires dirty cash plus another security bribe, and unlocks jewelry making on success.
6. If the contact gets caught, the acting character is arrested.
7. Contacts can expand into more schemes, then unions, then general contractor and wholesale warehouse unlocks.
8. Worker reform law plus employment agency plus influence are needed to turn contacts into unions.

## Clarified Direction

Dock contacts should be items.

That means the default content design should treat:

- `dock contract` as an item
- `maritime dock contact` as an item
- union progression as item-count or item-turn-in progression first

## Streaming Assets First

This arc should begin in content:

- maritime dock interactions
- accountant-gated scheme quests
- maritime trader trade definitions
- item rewards:
  - dock contract
  - maritime dock contact
- ore and jewelry shipment rewards
- arrest-risk scheme variants
- union / contractor / wholesale unlock quest chains
- worker-reform gating content

## Patch Only If Needed

Use code only if these cannot be expressed cleanly through normal content:

- failure-state arrests tied to specific acting characters
- persistent contact-item aggregation into unions
- multi-contact counting for higher unlock tiers
- law-enactment hooks if the existing law systems do not expose the needed triggers

## Task Breakdown

1. Add `Port Security Bribe` as the accountant entry point.
2. Create first maritime smuggling scheme for jewelry intake.
3. Reward `dock contract`.
4. Use `dock contract` to unlock a maritime-trade quest.
5. Add repeat or follow-up scheme that upgrades into `maritime dock contact`.
6. Add second contact-driven scheme for ore.
7. Make `maritime dock contact` an item reward.
8. Make failure arrest the acting schemer.
9. Tie success to jewelry-making skill and business unlock.
10. Extend contact items into other scheme families.
11. Define the union conversion path:
    - multiple contacts
    - employment agency
    - influence cost
    - worker reform law
12. Make two unions the threshold for general contractor progression.

## Desired Outcome

This should feel like a real criminal logistics ladder:

- bribe
- shipment
- contact
- repeat work
- specialized industry
- organized labor / contractor expansion
