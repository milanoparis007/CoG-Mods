# Ordered Implementation Roadmap

This roadmap turns the task pack into a practical implementation order with explicit tags on each step.

## Global Rule

Default to `StreamingAssets`.

Use patches or methods only when the stock data layer cannot cleanly express:

- mixed dirty / clean payment math
- recurring passive income or weekly upkeep
- exact acting-character arrest behavior
- item aggregation into higher-order states like unions
- UI or save-facing role renames that are hard-coded

## Bank Node Recommendation

Best-fit node style: train-station-style business-backed special building.

Working pattern:

1. `Buildings.sim`: special bank building template
2. `Businesses.sim`: bank business entry
3. `movesinto`: bank business moves into the special bank building
4. business modules carry purchases and quest interactions
5. building / district template lists handle placement

Why this is the best fit:

- it already matches an existing working pattern in the content
- it supports purchases and quests naturally
- it keeps district placement straightforward
- it stays mostly in `StreamingAssets`

## Phase 1: Foundation Content

### 1. Bank special building and business wrapper
Tag: `StreamingAssets only`

- create bank building template
- create bank business wrapper
- connect with `movesinto`
- place bank into district-friendly building template lists
- start with a downtown-focused version first

### 2. Train-station warehouse bulk-liquor sales
Tag: `StreamingAssets only`

- add warehouse / storage sale point attached to train station logic
- add bulk sale actions for each liquor tier
- use existing station and warehouse inventory modules where possible

### 3. Large connected drug and liquor deal expansion
Tag: `StreamingAssets only`

- add `45k` and `100k` deal content
- keep old large deals for non-connected traits
- add connected-only expanded deal content at roughly `4x` to `5x`
- frame them as purchases from other outfits

## Phase 2: Finance And Trait Quests

### 4. Funeral-home fake-account quest
Tag: `StreamingAssets only`

- add funeral-home quest chain
- require `connected`
- require personal info, ink, and paper
- add fake-account output or unlock state

### 5. Offshore account discount path
Tag: `Likely patch fallback`

- try content variants first
- if half-dirty-cash discount math cannot be represented in content, add a narrow runtime payment rule

### 6. Religious church support path
Tag: `StreamingAssets only`

- add church support action
- tie support into loan access
- model the support side in content first

### 7. School small-gang passive path
Tag: `Likely patch fallback`

- add school quest and local-goon requirement in content
- add recruit-muscle unlock in content
- use code only if true passive-income timing needs a runtime hook

## Phase 3: Maritime Ladder

### 8. Port Security Bribe entry point
Tag: `StreamingAssets only`

- add maritime dock interaction
- gate it to accountant
- reward `dock contract` item

### 9. Jewelry smuggling chain
Tag: `StreamingAssets only`

- add maritime trader chain
- support dirty or clean cash branches
- reward jewelry intake and follow-up progression

### 10. Dock contact item ladder
Tag: `StreamingAssets only`

- add `maritime dock contact` as an item
- use item-based follow-up schemes for ore
- tie success into jewelry-making skill and business unlock

### 11. Failure arrest targeting
Tag: `Likely patch fallback`

- try content-side failure branches first
- patch only if the exact acting character must be arrested and content cannot target them reliably

### 12. Contact items into unions
Tag: `Likely patch fallback`

- start with item turn-ins and law-gated quest progression
- patch only if multiple contact items need runtime aggregation into unions and contractor unlocks

## Phase 4: Ranked Schemes

### 13. Mini vacations
Tag: `StreamingAssets only`

- add boss and high-rank vacation actions
- require fancy suits
- make them expensive
- reward small street credit

### 14. Fundraiser schemes
Tag: `Likely patch fallback`

- build accountant and boss fundraiser actions in content
- patch only if smarter actors need dynamic street-credit scaling that content cannot express

### 15. Weapons, hijacks, and armed actions
Tag: `StreamingAssets only`

- add weapon search and truck hijack actions
- add weapon selling and robbery actions
- gate them by rank in content first

### 16. Con Artist rename and larger burglaries
Tag: `Likely patch fallback`

- do content-side rename and burglary gating first
- patch only if the role label is hard-coded in UI or save/state systems

### 17. Random business opportunity quests
Tag: `Likely patch fallback`

- start with weighted content pools
- patch only if true random business selection or duplicate protection needs runtime logic

## Recommended Build Order

1. Bank building and business wrapper
2. Bulk liquor train-station sales
3. Connected large deal expansion
4. Funeral-home fake accounts
5. Port Security Bribe and dock contract item
6. Jewelry smuggling and dock contact item chain
7. Trait support content for schools and churches
8. Ranked side schemes
9. Only then add narrow patch fallbacks where content actually runs out

## Best First Three Passes

### Pass A
Tag: `StreamingAssets only`

- bank building
- bank business wrapper
- district placement

### Pass B
Tag: `StreamingAssets only`

- connected large drug and liquor deals
- funeral-home fake-account quest

### Pass C
Tag: `StreamingAssets only`

- Port Security Bribe
- dock contract item
- maritime trader jewelry chain

After those three passes, we should have a strong signal on which remaining systems truly need code and which ones stay content-only.

