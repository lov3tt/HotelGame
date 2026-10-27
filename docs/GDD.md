# The Wayside — Game Design Document

**Genre:** Tycoon / management strategy, hotel & room service
**Engine:** Unity 6 LTS (URP), 3D scene presented as a **2D side-view cutaway**
**Camera:** Zoomed out it looks like a flat 2D hotel game, with every floor visible like a dollhouse. Zooming in close swings into an angled 3D perspective.
**Setting:** Medieval grand inn, run by isekai'd streamer-persona staff (active theme: Isekai Stream, see §1b). A plain medieval theme is also included.
**Time model:** Real-time with pause (1x / 2x / 3x)
**Current scope:** Vertical slice, ~30 minutes / 7 in-game days

---

## 1. Pitch
You inherit *The Wayside*, a three-storey lodging house on a royal trade road.
- Travellers, merchants and the occasional noble take rooms for one or more nights.
- During their stay they ring for service: a hot meal, a drawn bath, firewood, fresh linens, boots polished by morning.
- You never carry a tray yourself. You hire, schedule, train and fire the staff who keep every room running.

Every coin comes from filling rooms, answering bells fast and turning rooms over quickly.

**Design pillars**
1. **Rooms are the business.** Occupancy, room quality and in-room service drive income.
2. **People are the strategy.** Staff are the main lever. Everything else supports them.
3. **Readable bustle.** Bells ringing over doors, staff hurrying up stairs with trays, dirty rooms waiting. You can read the state of the inn at a glance.
4. **Make it yours.** The theme, names and character models can be swapped without touching code.

---

## 1b. The Twist: Isekai Stream Inn (active theme)
**Premise:** a troupe of streamers wakes up inside a medieval fantasy world. They decide to run an inn the only way they know how: **as if it were on stream**.
- Guests are ordinary fantasy locals: pilgrims, adventurers, merchants, nobles.
- The staff are **original VTuber-style personas**. No real streamers or likenesses are used.
- The tone is cute, chaotic and cozy, not suggestive, so it suits all audiences and store requirements.

The gameplay is identical to the base design. The twist lives in the `Theme_IsekaiStream` theme plus a few stream-flavoured presentation features. Switching back to `Theme_Medieval` gives the plain inn.

### Starting staff personas
| Persona | Role | Gimmick | Natural traits |
|---|---|---|---|
| **Lumi**, Elf Receptionist | Clerk | Elegant, polite, secretly competitive | Charming, Discreet |
| **Ember**, Dragon Porter | Porter | Sleepy, huge, carries everything at once | Strong Back, Lazy |
| **Mochi**, Catgirl Server | Server | Hyperactive, everyone's favourite | Charming, Diligent (tires fast) |
| **Unit-07**, Android Maid | Chambermaid | Deadpan, flawless, zero charm | Steady |
| **Gigi**, Gremlin Chef | Cook | Chaotic, loud, occasionally on fire | Gossip (team morale up, sometimes burns the stew) |

Hiring-board candidates are new original personas, drawn from the theme's name pool.

### Stream-flavoured presentation
These are names in the theme; the underlying mechanics are unchanged.

| Base mechanic | In this theme |
|---|---|
| Reputation (stars) | **Followers** |
| Tips | **Donations**, with on-screen alerts for big ones |
| Tiers D → S | Trainee → Debuted → Partner → Top Streamer → **Legend** |
| Servants' room (rest area) | **Green Room** |
| Hot bath service | **Hot Spring Bath** |
| Luggage service | **Inventory Carry** |
| Currency | Gold (g) |

### Planned stream features (post-slice, presentation only)
- **Chat feed:** guest feedback appears as chat messages, e.g. "the bath was cold lol" or "Mochi carried 2 trays POG".
- **Raid events:** a Noble or a big pilgrim group arrives as a "raid", a burst of guests at once.
- **Live2D staff portraits (started):** in the staff bar, hiring board and events. Expressions react to fatigue, morale and rush-hour panic.
  - **Done:**
    - The staff bar shows all five personas as animated 2.5D puppets (`PortraitPuppet`), with Happy, Tired and Panic moods (`PortraitDriver`).
    - Real Cubism models plug into the same `CharacterAppearance → Portrait Prefab` slot through `CubismPortraitModel`. See `docs/Live2D.md`.
  - **To do:** gameplay sets each mood from fatigue and morale (M7), plus portraits on the hiring board and event pop-ups.
- **Persona events:** for example, "Gigi set the kitchen on fire. Chat loved it (+followers, −stew)."

### Art notes
- **Placeholders for now:** the personas are primitive models (hair, ears, horns, tails, visor, chef hat) that stand in for real art.
- **Live2D art has to be licensed for game use.** Commission new models, or use models whose licence allows commercial game use. Never reuse existing VTuber models.

## 2. Core Loop

| Phase | Time | Player does | Game does |
|---|---|---|---|
| **Morning** | Paused | Set shifts, assign roles, restock, buy room upgrades, train, hire/fire, read report | Shows checkouts and bookings for today, plus new candidates |
| **Day** | ~2.5 min @1x | Drag staff to rooms/zones, drag tired staff to rest | Checkouts, so rooms need cleaning. Early arrivals, light room service. |
| **Evening** | ~3.5 min @1x | Same, under pressure | Peak check-ins, dinner room service, baths, firewood, turndown |
| **Night** | Summary | Read the report | Charges room nights, pays wages, updates morale/fatigue/XP/reputation, fires events |

A full day lasts about 6 minutes at 1x.

**The room cycle (the central loop):**
```
Ready → Check-in → Occupied (service requests) → Checkout → Dirty → Cleaned → Ready
```
A room that isn't Ready can't be sold, so a slow chambermaid costs you a booking.

---

## 3. Rooms

### 3.1 Room types
| Type | Count (slice) | Price / night | Guests | Services available |
|---|---|---|---|---|
| Bunk Room (4 bunks) | 1 | 3c per bunk | Pilgrims, labourers | Bread & ale only |
| Common Room | 4 | 10c | Travellers | Meals, ale, firewood |
| Private Chamber | 2 | 20c | Merchants | All except bath |
| Noble Suite | 1 | 45c | Nobles | All services, including bath and wake-up call |

### 3.2 Room state
- **Cleanliness:** Clean / Dirty / Poorly cleaned. Poorly cleaned rooms give −satisfaction.
- **Hearth:** firewood level. A cold room in the evening slowly drains satisfaction.
- **Linens:** fresh or used. Turndown service refreshes them.

### 3.3 Room upgrades (bought in the morning)
Each room has up to 3 upgrade slots:
| Upgrade | Cost | Effect |
|---|---|---|
| Feather bed | 30c | +3c / night, +satisfaction |
| Private hearth | 40c | Needs firewood, unlocks firewood service, +satisfaction in the evening |
| Wash tub | 50c | Unlocks *Hot Bath* service (high tip) |
| Fine furnishings | 60c | +5c / night, attracts merchants and nobles |

Upgrades are the main thing to spend profit on. Staff are the main recurring cost.

---

## 4. Guests

| Type | Room | Stay | Typical requests | Spend | Patience |
|---|---|---|---|---|---|
| **Pilgrim group** (3–4) | Bunks | 1 night | Bread, ale | Low, paid in bulk | Low |
| **Traveller** | Common | 1–2 nights | Supper, ale, firewood | Medium | High |
| **Merchant** | Private | 2–3 nights | Meals, firewood, turndown, boot polish | High | Medium |
| **Noble** | Suite | 1–2 nights | Everything: bath, wake-up call, fine meals | Very high, big tips | Low, very demanding |

- **Arrival:** guests arrive at the **front desk**. If no clerk is free, they queue, and patience drains.
- **Service requests:** an occupied room periodically **rings a bell**. An icon appears over the door, the request is posted and a patience timer starts.
- **Satisfaction (0–100):** tracked for the whole stay. It is affected by:
  - Check-in wait.
  - Response time to each request.
  - Room condition.
  - Staff mistakes, such as cold food, a lukewarm bath or a poorly made bed.
- **Reputation (1–5★):** a rolling average of satisfaction. It sets how many and what kind of guests book. Nobles only appear at 3★ and above.
- **Leaving early:** a guest whose satisfaction drops to 0 leaves early without paying for the remaining nights.

---

## 5. Room Service

Every request becomes one or more **tasks** on the Task Board (see the build plan).

| Request | Chain of tasks | Roles | Price / tip |
|---|---|---|---|
| Supper / meal | Cook at stove → carry tray to room → serve at table/bed | Cook → Server | 4–8c |
| Ale / wine | Pour at taproom → carry → serve | Server | 2–4c |
| Firewood | Fetch from woodpile → carry → stoke hearth | Porter | 1c + tip |
| Hot bath | Heat water at kitchen copper → carry buckets (2 trips) → fill tub | Porter | 6c + big tip |
| Turndown | Fresh linens from laundry → make bed | Chambermaid | tip only |
| Boot polish | Collect boots at night → polish → return by morning | Porter | 2c |
| Wake-up call | Knock at the set time (late = −satisfaction) | Any free staff | tip only |
| Luggage | Carry bags from desk to room at check-in | Porter | tip |

**In-room flow (animated):**
1. The staff member walks to the door and plays **Knock**.
2. The door opens and they enter.
3. They walk to the target: bedside, table, hearth or tub.
4. They play the work animation: **Serve**, **Stoke**, **Pour bath** or **Make bed**.
5. They leave and the door closes.

**Room access:** rooms are a separate NavMesh area. Only the room's guest and staff on a task for that room can path inside.

---

## 6. Staff (the heart of the strategy)

### 6.1 Roles
| Role | Base station | Tasks |
|---|---|---|
| **Clerk** | Front desk | Check-in, checkout, takes bookings. Can answer wake-up calls when free. |
| **Porter** | Woodpile / copper | Luggage, firewood, bath water, boot polish |
| **Server** | Kitchen pass / taproom | Pour drinks, carry trays to rooms, serve, clear trays |
| **Chambermaid** | Laundry | Clean rooms after checkout, turndown, linens |
| **Cook** | Stove | Cook meals (uses Provisions), heat bath water if no porter is free |

Anyone can be assigned to any role. They just have a different tier in each.

### 6.2 Stats
- **Tier per role:** D → S (see 6.4).
- **Stamina:** how quickly fatigue builds. Stairs cost more stamina, so carrying bath water up to the suite is exhausting.
- **Wage:** paid nightly. Based on their highest tier.
- **Fatigue (0–100):** rises while working and falls on breaks or days off.
  - Above 60: slower work and more mistakes.
- **Morale (0–100):**
  - Lowered by: low pay, double shifts, refused promotions, rude nobles, seeing coworkers fired.
  - Raised by: breaks, days off, raises, generous tips.
  - Below 30: slower work.
  - Below 10: they quit at the end of the day.

### 6.3 Traits (1–2 per person)
| Trait | Effect |
|---|---|
| Diligent | +15% speed, +25% fatigue |
| Charming | +20% tips on room service |
| Discreet | Nobles and merchants gain +satisfaction when served by them |
| Strong Back | Porter: carries 2 loads per trip, less stair fatigue |
| Lazy | 2–4s idle between tasks |
| Light-fingered | Sometimes steals a guest's tip, or sometimes guest belongings, which hits reputation |
| Night Owl | No fatigue penalty on evening shift |
| Steady | Mistake chance halved |
| Gossip | +morale to coworkers, −5% speed |

### 6.4 Tier list
| Tier | Name | Speed | Mistake | Wage ask | Perk |
|---|---|---|---|---|---|
| D | Apprentice | 0.7x | 15% | 4c | — |
| C | Hand | 0.9x | 8% | 6c | — |
| B | Journeyman | 1.0x | 4% | 9c | Carries 2 orders / loads per trip |
| A | Skilled | 1.2x | 2% | 13c | Role perk (see below) |
| S | Master | 1.5x | 0% | 18c | Mentor: same-shift juniors in the role gain +50% XP |

**A-tier role perks:**
| Role | Perk |
|---|---|
| Clerk | *Upsell*: guests sometimes book an extra night |
| Porter | *Quick Feet*: no stair slowdown |
| Server | *Silver Tray*: room service food never arrives cold |
| Chambermaid | *Swift Linens*: cleaning takes half as long |
| Cook | *House Special*: meals give +satisfaction |

**XP and promotion:**
- Staff earn XP in the role they work. Filling the bar makes them **eligible for promotion**.
- Promoting raises their wage ask. Refusing gives −15 morale, and the request comes back in 2 days.
- **Tier board:** a sortable roster view showing every staff member's tier in each role, with XP bars.

### 6.5 Training
- Done in the morning phase. The person is **off shift that day**.
- Training gives a large chunk of XP in one role.
- Costs:
  | Target tier | Cost |
  |---|---|
  | C | 10c |
  | B | 20c |
  | A | 40c |
- **S tier cannot be bought.** It needs work XP plus at least one shift with an S-tier mentor in the same role.

### 6.6 Hiring and firing
- **Job board:**
  - Shows 3 candidates every 2 days.
  - Candidates come with random tiers, name, appearance and hidden traits.
  - A **reference** (5c) reveals one trait.
- **Hire:** the candidate joins at their wage ask, starting tomorrow.
- **Fire:**
  - Available any time from the staff panel.
  - Severance is 1 day's wage.
  - The rest of the staff take −5 morale.
  - There's no morale hit if the fired person had a visible bad trait.
- **Quitting:** staff whose morale ends the day below 10 leave. They warn you one day ahead.

### 6.7 Shifts
- Shifts are **Day** and **Evening**. Each staff member can work Day, Evening, both or neither.
- Double shifts give ×2 fatigue and −10 morale per day.
- **Breaks** work through drag and drop: drop a staff member on the servants' room (see 6.8).

### 6.8 Drag-and-drop assignment (the main moment-to-moment control)
You place staff by **dragging** them. You can grab either the 3D character in the world or their portrait in the staff bar at the bottom of the screen.

| Drop target | Result |
|---|---|
| **A guest room** (e.g. Noble Suite) | **Assigned to that room.** They take only that room's tasks: service requests, cleaning, turndown. If the room has no tasks, they wait in the corridor outside it. Their portrait shows a room badge. |
| **A work zone** (Front desk, Kitchen, Laundry, Woodpile, a whole floor) | **Assigned to the zone.** They take only tasks that start or end there. Dropping on a floor means any room on that floor. |
| **Servants' room (resting area)** | **Resting.** They walk there, sit and recover fatigue fast, plus a little morale. They take no tasks until you drag them out. |
| **Lobby / "Anywhere" slot on the staff bar** | **Free.** This is the default: they pick the best task anywhere, using normal role-based task choice. |

**Rules:**
- **Role still matters.** Assignment limits *where* someone works, not *what* they do. A Cook dragged to the Noble Suite will carry trays there, at their Server tier, but won't clean.
- **Drop feedback:** while dragging, valid targets glow green. Targets with no tasks this person could do glow yellow ("they'll just wait"). Highlights use theme colours.
- **Hover tooltip on a target:** pending tasks there, who is already assigned, and the resting area's capacity (3 seats in the slice; a full room glows red).
- **Pause-friendly:** dragging works while paused, so you can rearrange everyone at once and then resume.
- **Assignments persist** across days until changed. The morning screen shows them on a floor plan.
- **Shift change:** an assigned staff member going off shift keeps their assignment for next time.
- **Leaving a task:** if you drag someone mid-task, they finish carrying what's in their hands. Otherwise the task goes back to the board.
- **Strategy:** assigning a Discreet A-tier server to the Noble Suite is how you keep nobles happy. Resting staff at the right moment, before the evening rush, is how you avoid exhaustion mistakes.

**Implementation sketch:**
- Each room and zone has a `WorkZone` trigger collider, and the servants' room is a `RestZone`.
- A `DragDropController` raycasts these with the Input System.
- `StaffAgent.Assignment` holds one of: `Free`, `Zone(x)` or `Resting`.
- `TaskBoard` filters candidate tasks by the agent's assignment before scoring them.
- This is built in Milestone 7 (Staff depth), alongside the staff bar UI.

---

## 7. Economy
- **Income:** room nights (the main source), room service, tips, luggage tips.
- **Costs:**
  - Wages.
  - Stock, bought each morning: Provisions 10c = 8 meals, Ale cask 8c = 12 drinks, Firewood 6c = 10 loads, Soap & linens 5c = 6 cleans.
  - Room upgrades.
  - Training, references, severance.
  - **Crown tax:** 80c every 7th night.

**Starting state:**
- 100c in the purse.
- Staff: 1 Clerk (C), 1 Chambermaid (D), 1 Cook (D).
- No porter and no server. The cook carries their own trays slowly at first.
- The first decision is who to hire.

**Win:** at least 600c, at least 3★ and at least one Noble served satisfied, by the end of day 7.
**Lose:** the purse is below 0 after paying the Crown tax.

**Balance targets:**
- Day 1 roughly breaks even.
- A good roster plus 2–3 upgrades wins around day 6.
- Ignoring room turnover means rooms sit Dirty and the inn loses money by day 3.

---

## 8. The Inn (fixed layout for the slice)
The inn is a **side-view cutaway**:
- The building runs left to right and the floors stack upward.
- The front is open toward the camera.
- Each floor has a **walkway along the front**, with rooms and work areas behind it.
- Staff and guests walk the walkways. The **stairwell** on the left connects the floors.

```
            ┌──────┬────────┬────────┬───────────────────┐
  2nd floor │      │ Priv 1 │ Priv 2 │    Noble Suite    │
            │ ╲    ├────────┼────────┼─────────┬─────────┤
  1st floor │  ╲╱  │ Comm 1 │ Comm 2 │ Comm 3  │ Comm 4  │
            │  ╱   ├────────┴───┬────┴───┬─────┴──┬──────┤
  Ground    │ ╱    │ Lobby/desk │Kitchen │Laundry │ Rest │  Yard (woodpile)
  ══════════╧══════╧════════════╧════════╧════════╧══════╧══════ street ══
            stairwell
```

| Floor | Contents |
|---|---|
| **Ground** | Street entrance, **front desk / lobby** with a taproom table, kitchen (stove, copper for bath water), laundry, servants' room (resting area, 3 seats); woodpile in the yard outside |
| **First floor** | 4 Common Rooms |
| **Second floor** | 2 Private Chambers, Noble Suite (double width, tub slot) |
| **Attic** (later) | Bunk Room (4 bunks) |

Stairs are the key spatial pressure. Upper rooms pay more but take longer to service. Rooms on the far right of a floor are the longest walk from the stairwell.

---

## 9. Customisation

### 9.1 Themes
- A `ThemeProfile` ScriptableObject holds everything that defines the flavour:
  - Inn name, currency name and symbol.
  - Display names for room types, guest types, roles, services, tiers.
  - Pools of candidate first names.
  - UI palette, font and panel sprites.
  - Prefabs for props: desk, bed, tub, hearth, door, tray.
  - Bell sound, music and lighting preset.
- **Rule:** gameplay code never hard-codes a display name, colour or prop. It reads from `ThemeManager.Current`.
- **To retheme:** duplicate `Theme_Medieval`, edit it in the Inspector and assign it on `GameManager`. For example, a modern hotel theme could rename "Porter" to "Bellhop" and "Noble Suite" to "Penthouse".

### 9.2 Character models
- All guests and staff use a `CharacterRig` prefab. It has an empty **Model slot** and the shared Humanoid Animator Controller.
- A `CharacterAppearance` asset contains: the model prefab (any **Humanoid-rigged** FBX), optional material variants and a height scale.
- The staff panel lets you change any staff member's appearance. Each guest type has a pool of appearances.
- See `docs/Customising.md` for step-by-step theme, model and animation setup.

### 9.3 Animations
- **States:** Idle, Walk, Carry (upper-body layer: tray, buckets, wood, luggage), Knock, Serve, Pour, Cook, Stoke, Pour Bath, Make Bed, Clean, Sit, Sleep.
- **Source:** Mixamo (free). Because they're Humanoid, they retarget onto any Humanoid model.

---

## 10. Out of Scope (post-slice ideas)
- Building new wings and placing furniture freely.
- Advance bookings and reservations calendar.
- Rival inns.
- Seasons, festivals and weather.
- Regular guests with relationships.
- Stables (horse care as a service).
- Multi-inn campaign.
