# Castle & Village Builder

Castle & Village Builder paints medieval villages and castles straight into your scene. You brush down whole houses, each one put together from modular pieces and each one different, and you draw castle walls with towers and gateways. Then add upper areas, grand stairs, bridges, plazas with fountains, dressed streets and a keep. Paths join the houses up, and props can be scattered round them. It works on any ground with a collider, such as a Unity Terrain or a mesh. Every stroke can be undone.

## Requirements

- Unity 6 (6000.0) or newer.
- Universal Render Pipeline (URP).

## Quick start

1. Open **Assets/Castle & Village Builder/Sample/Castle & Village Demo.unity**.
2. Open the window with **Tools > Castle & Village Builder > Open Castle & Village Builder**.
3. Pick the **Houses**, **Castle Walls**, **Castle Features** or **Details** tab, click the big **Start** button under the tabs, then click or drag on the ground in the Scene view.

The first time the window opens, it sets up the Medieval Village kit: it makes the URP materials and sets up the models. You can run this again at any time with **Tools > Castle & Village Builder > Set Up Medieval Village Kit**.

### The window

Each tab is laid out the same way, top to bottom:

- **The tool picker:** what a click or drag in the Scene view does (for example Brush, Freehand or Stairs).
- **How to:** a short note on using the tool you picked, with its keys.
- **Panels:** the settings, grouped under shaded headers (for example **Walls**, **Gateways**, **Watch Towers**, **Stone Colour**, **Weathering**). Click a header to fold its panel away; the window remembers which are open.
- **Scene & Tools:** select, clear or rebuild what the tab has already made.

## Houses

| Mode | What it does |
| --- | --- |
| Brush | Click or drag to scatter a few houses under the brush. They keep a gap between each other and stay off steep ground, water and other buildings. [ and ] change the brush size. |
| Along a Line | Drag a line and the houses go down along it, like a street. |
| One at a Time | The next house follows the mouse. Click to put it down. [ and ] turn it, R gives it a quarter turn, and Space rolls a different house. |

- **Facing:** after a stroke, right-click in the Scene view and move the mouse: the houses you just put down turn their front doors to face it. Right-click again to keep it, or press Esc to put them back.
- **Erase:** hold Shift (or tick Erase) and drag to remove houses, together with their props. It also cuts away the mesh paths under the brush: just that part, so you can shorten a path or make a gap in it.
- **Styles:** limit the house sizes, storeys, roofs and details the houses are rolled from.
- **Clear scatter under:** removes the props (and a Unity Terrain's trees, or Paint Land's scatter) from under new houses.
- **Stone footing:** on a slope, a house stands level on a stone base.
- **Hide interior:** closes off the inside of each house, so you see a dark interior through the windows and doors, not empty space.
- **Themes:** colour themes for the walls, roofs and wood. Tick the themes new houses can use (they get one at random), edit their colours, or make your own. Changing a theme changes every house wearing it, and **Theme Selected** gives houses that are already down a theme.
- **Groups:** each stroke goes into its own group under the **Buildings** object, so you can select or delete a whole stroke at once.

### Plants and props round houses

Tick **Plant round new houses** and set how many clumps of each kind go round each house. **Spread** sets how far out from the roof's edge they go, and **Keep doorway clear** keeps the way to the front door free. **Replant Selected** and **Clear** work on houses that are already down.

The kinds come from **Builder Settings** (click **Edit Builder Settings**). It starts with the kit's crates, wagons and loose bricks. Add your own trees, bushes and rocks: give each kind a name, its prefabs, a size range, a clump size and the steepest ground it can go on. They're put under the **Village Plants** object.

### Paths

Tick **Paths to new houses** and each new house gets a path from its front door to the nearest house or path. The paths find their way round houses, castle walls (through the gateways) and steep ground, and they branch off one another like streets. **Join All Houses** and **Join Selected** add paths to houses that are already down.

Paths are strips of mesh laid on the ground, in the colour you pick or in your own material (Builder Settings > Path Material). They go under the **Village Paths** object, so you can delete them there.

## Castle Walls

| Draw | What it does |
| --- | --- |
| Tower to Tower | Click on the land to start, then click again and again: a wall goes to each point from the last. Click the first point to close a ring. Right-click or press Enter to finish. Walls drawn from a tower join onto it. |
| Freehand | Drag a line and the wall follows it, smoothly, with towers along it. End near where you started to close it into a ring. |
| Circle | Click the middle and drag out to the size. You get a round wall with towers spaced round it. |
| Tower | Click to place a single tower. |
| Gateway | Point at a wall to see where the arch would go, then click to cut a gateway through it, with a passage, a portcullis and doors (as the details say). Click a gateway to wall it up again. Or press G before drawing a wall to put a gate in it. |

- **Weathering:** age the stone with soft colour patches (so the tiling doesn't show), grime in the joints, rain streaks, and moss. Moss is set separately for **walls and towers** and for **floors and paving**: how much grows on top, how much in the joints, and how big its patches are (scattered tufts up to big drifts). Use the sliders or the **Fresh**, **Weathered** and **Ancient** presets. It's on the kit's stone, brick and rock trim (every colour theme too), so the houses' stonework weathers with the castle.
- **Walls, Gateways and Watch Towers:** set the height in storeys, the walkway width, battlements, the gateway size, and how far apart the watch towers are.
- **Stone colour:** click a swatch: Kit's Own, Sandstone, Honey Limestone, Pale Limestone, Chalk White, Light Granite, Grey Granite, Dark Basalt, Charcoal, Slate Blue, Frost Grey, Mossy Green, Olive Fieldstone, Red Sandstone, Rose Granite, Baked Clay, Golden Ochre, Brown Fieldstone or Desert Tan. Or pick **Custom** and choose any colour. These recolour only the stone (roofs and wood keep their colours). **Or like the houses** uses one of the house colour themes instead.
- **Tower styles:** Square, Round, Octagonal, Tapered, Turreted, Stepped, Hoarded, or Mixed.
- **Wall Details:** add arrow slits and see-through windows, shutters, buttresses, corbels, a plinth, banners, flags, torches (optionally with real lights), stairs, ivy and rubble. The presets **All On**, **Defensive**, **Ruined** and **Bare Stone** set them all at once.
- Each wall you draw goes into its own **section** under the **Castle Walls** object. Sections are listed in the window, where you can select or delete them.
- Shift+click (or Erase) removes a tower or wall. Esc stops painting.

## Castle Features

The **Castle Features** tab builds the grand pieces that make a castle town. Each one is a single solid piece of the kit's stone, and it takes the castle's colour theme (Castle Walls > Stone Colour).

| Feature | How to build it |
| --- | --- |
| Upper Area | Draw its outline freehand (let go to close it), or drag out a rectangle ([ and ] turn it). With **Lock to castle walls** on, wherever the outline comes near a castle wall or tower it locks onto its face (shown in gold), so the area fits snugly against the wall: those sides get no edge, stairs or lamps. You get a raised, paved terrace on stone retaining walls, with grand stairs up on the sides you choose (the front is the side facing you). Set its height, its edge (balustrade, battlements, hedge or plain), lamps at the corners, and planters. Upper areas can go on top of other upper areas. |
| Stairs | **Straight:** drag from the bottom to where the stairs should arrive (a terrace, a wall walk, a hill). **Curved:** the same, but one flight sweeps round as it climbs (set the turn, and which way it sweeps). **Double:** two mirrored flights, either curving outward (a horseshoe: they sweep out either way and meet at the top) or curving inward (their feet meet side by side and their tops arrive apart). **Freehand:** draw their path, and they curve as they climb from its lower end to its higher one (the label shows how long the path needs to be for the climb). Grand stairs with side walls or balustrades, landings on long flights, and lamps. **Start** sets how they begin: straight, a round fan of steps, or flared (each bottom step wider, its front bowed and its ends swept round). |
| Bridge | Drag from one end to the other: the deck meets the ground, walls or terraces at both ends. Choose stone arches on piers (with parapets, balustrades or open sides) or a wooden trestle bridge, and lanterns. |
| Street | Draw along a street. Lamp posts, banners in the castle's colours, clipped hedges, cypresses and benches go down both sides. |
| Plaza | **Round:** click the middle and drag out. **Freehand:** draw its outline, smoothed into flowing curves. A paved plaza on ring steps that follow its shape, with a fountain in the middle and lamps round it. |
| Keep | Click to place a great keep: one of the tower styles, much broader and taller. Walls can join onto it. |

- **Upper areas, bridges and plazas are ground.** Houses, props, paths and other features can go on top of them. Stairs, streets and keeps keep houses off.
- **Real lights in lanterns** adds a point light to each lantern. It looks lovely at night, but each light costs, so use it on a few features.
- Everything goes under the **Castle Features** object, and the tab lists what you've built, to select or delete. Shift+click removes a feature.
- **Connect to upper areas** (on by default): end stairs near an upper area's edge and they snap onto it, arriving square to the edge, clear of its corners and at its height (the preview turns gold). Freehand stairs bend smoothly from the way you drew them into a square arrival.
- Stairs that arrive at an upper area (straight or freehand) open a gap in its railing, battlements or hedge where they come in, and clear any planters there. Undo puts the railing back.

## Details

The **Details** tab dresses the scene with props. Tick the props you want (or use the **Market**, **Courtyard** and **Barracks** picks), then:

| Tool | How it works |
| --- | --- |
| Brush | Click or drag to scatter the ticked props under the brush, kept a gap apart and off houses and walls. [ and ] change the brush size. |
| One at a Time | Click to place one. [ and ] turn it, Space moves to the next ticked prop. |
| Fence Line | Draw a line: a wooden fence, picket fence, low stone wall, hedge row or iron railing follows it, post to post, over the ground. |

- **Props:** barrels, barrel stacks, sacks, hay bales, crates, a wagon, market stalls (striped canopies and produce), tables with stools, woodpiles, a well, signposts, lamp posts, braziers, banner poles (in the castle's banner colours), benches, planters, cypresses, weapon racks, archery targets and loose bricks. Most come in a few variants.
- **Placing:** set how many per dab, the gap between them, a random size range, a random turn, and whether they lean with slopes. **Real lights** adds a light to each lamp and brazier.
- Props sit on any ground, including upper areas, plazas and bridges, and take the castle's colour theme.
- Hold **Shift** to rub out props and fence spans under the brush. Everything goes under the **Village Details** object.

## Builder Settings

**Assets/Castle & Village Builder/Builder Settings.asset** (made the first time it's needed):

| Setting | What it does |
| --- | --- |
| Use Paint Land | When Paint Land is in the project and its land is in the scene, build on it (see below). Turn it off to always build on any ground with a collider. |
| Nature | Your kinds of plants, rocks and props to put round houses. |
| Path Material | The material for mesh paths. If it's empty, a plain material in the path's colour is used. |
| Path Lift | How far mesh paths sit above the ground, so they don't flicker into it. |
| Merge Details | On by default: each wall, tower and house's many small pieces are fused into one mesh as it's built (thousands of objects become one, and it looks the same). Turn it off to keep every piece as its own object. |
| Remove Terrain Trees | Remove a Unity Terrain's trees from under new houses, walls and paths. |

## Performance

The builder keeps what it makes light to render:

- Walls, towers and houses are fused into one mesh each as they're built (Builder Settings > Merge Details), with one collider made of just their solid parts.
- Props and fence spans have simple box colliders, are static, and fade out when they're only a speck on screen.
- **Tools > Castle & Village Builder > Optimize Scene** does all of this for what's already in the open scene (for example a scene built with an older version). It can be undone.
- Real lights (torches, lanterns, lamps and braziers) cost the most: use them on a few, or leave them off.

## Rain and day/night

The houses and castles use the **Custom/Building Lit** shader, which is URP Lit with rain wetness added. It lights like Lit, so the sun, sky, fog and any day/night cycle work as normal. When things get wet, they get darker and richer in colour, with a sharp shine.

To make the buildings wet, add **Castle & Village Builder > Building Weather** to any object. Set **Wetness**, or tick **Raining** and they'll soak and then dry out over time (in Play mode). If you have your own weather system, set **Wetness** from your script. Or set the shader globals `_Wetness` (0 to 1), `_RainNow` (0 to 1) and `_WetOvercast` (0 to 1) yourself, and leave the component out.

**Surface depth:** the stone, brick, plaster and wood have height maps (the surface shifts as you look across it, so mortar and cracks sink in), occlusion that shades the gaps, and stronger normals. **Tools > Castle & Village Builder > Add Surface Depth** applies it again (for example after adding a colour theme).

**Tools > Castle & Village Builder > Make Buildings Wet In The Rain** moves any of the kit's materials that still use plain URP Lit onto Building Lit.

## Working with Paint Land (optional)

Castle & Village Builder doesn't need Paint Land. If Paint Land is in the project and its land is in the scene, the builder finds it and uses it:

- Houses and walls go on Paint Land's land, and keep out of its water.
- Paths can also be **painted** on the land, in a colour or with one of its textures, as well as made as meshes.
- Paint Land's scatter kinds (trees, bushes, flowers...) can be planted round houses, alongside your own.
- **Clear scatter under** removes Paint Land's scatter from under new houses and walls.
- Grass keeps out of castle gateways, and houses move with the ground when you reshape the land.

## Scripting

Houses carry a **ScatteredBuilding** component (its footprint, size, theme and props). Castle towers and walls carry a **CastlePiece** component (its footprint, the line it runs along, and its gateways). Both are in the `YaraJ.CastleVillageBuilder` namespace, in the runtime assembly `YaraJ.CastleVillageBuilder`. The tools themselves are editor-only.

## Folders

| Folder | What's in it |
| --- | --- |
| Editor | The window and the builders (editor-only). |
| Runtime | ScatteredBuilding, CastlePiece and BuildingWeather. |
| Shaders | Building Lit and its rain include. |
| Medieval Village Kit | The house modules, textures and materials. Generated holds materials and meshes the tools make. |
| Sample | The demo scene. |

## Credits

The Medieval Village MegaKit models and textures are by Quaternius (quaternius.com), released under CC0 1.0 (public domain). See Third-Party Notices.txt.

## Support

Contact YaraJ through the Asset Store publisher page.
