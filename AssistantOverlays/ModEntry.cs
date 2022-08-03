using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.Menus;
using StardewValley.BellsAndWhistles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewValley.TerrainFeatures;

namespace kotae.AssistantOverlays
{
    public class ModEntry : Mod
    {
        enum EObjectType
        {
            Debris, GenericForageable, GenericObject, FiberWeeds, HayGrass,
            Tree, FruitTree, Crop, ResourceClump,
            OverlayObject,
            Bush, Truffle, ArtifactSpot, 
            LadderStone, Ladder, NPC, Monster,
            Quartz
        }
        enum ECompassDirection { North, South, East, West, NorthWest, NorthEast, SouthEast, SouthWest, Center}
        Vector2[] DirVectors = new Vector2[] {new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(-1f, 0f),
            new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f)};

        class ObjReference
        {
            public string Name;
            public int X, Y;
            public EObjectType Type;
            public object _Ref;
            public ObjReference(string _Name, int _TileX, int _TileY, EObjectType _type, object _ref = null)
            {
                Name = _Name;
                X = _TileX;
                Y = _TileY;
                Type = _type;
                _Ref = _ref;
            }
        }

        IModHelper _Helper;
        ModConfig Config;
        Dictionary<EObjectType, ConfigCategoryOptions> m_OptionsConfig = new Dictionary<EObjectType, ConfigCategoryOptions>();

        Texture2D pixelTex;
        Rectangle outlineRect = new Rectangle(0, 0, Game1.tileSize, Game1.tileSize);
        Dictionary<EObjectType, List<ObjReference>> m_OutlineObjects = new Dictionary<EObjectType, List<ObjReference>>();

        bool mineshaftHasLadderSpawned = false;
        bool mineshaftMustKillEnemies = false;

        public override void Entry(IModHelper helper)
        {
            _Helper = helper;
            Config = _Helper.ReadConfig<ModConfig>();
            MapConfigOptions();

            foreach (EObjectType type in Enum.GetValues(typeof(EObjectType)))
            {
                m_OutlineObjects.Add(type, new List<ObjReference>());
            }

            pixelTex = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            pixelTex.SetData(new Color[] { Color.White });
            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            helper.Events.Display.RenderedWorld += Display_RenderedWorld;
            helper.Events.Display.RenderedHud += Display_RenderedHud;
            helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
        }

        private void GameLoop_GameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = this.Helper.ModRegistry.GetApi<GenericModConfigMenu.IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            foreach (KeyValuePair<EObjectType, ConfigCategoryOptions> pair in m_OptionsConfig)
            {
                configMenu.AddSectionTitle(
                    mod: this.ModManifest,
                    text: () => pair.Key.ToString()
                    );
                configMenu.AddBoolOption(
                    mod: this.ModManifest,
                    name: () => "Should Show In List",
                    getValue: () => pair.Value.ShouldShowInList,
                    setValue: value => pair.Value.ShouldShowInList = value
                    );
                configMenu.AddBoolOption(
                    mod: this.ModManifest,
                    name: () => "Should Draw Overlay",
                    getValue: () => pair.Value.ShouldDrawOverlay,
                    setValue: value => pair.Value.ShouldDrawOverlay = value
                    );
                configMenu.AddBoolOption(
                    mod: this.ModManifest,
                    name: () => "Should Draw Snaplines",
                    getValue: () => pair.Value.ShouldDrawSnaplines,
                    setValue: value => pair.Value.ShouldDrawSnaplines = value
                    );
                configMenu.AddNumberOption(
                    mod: this.ModManifest,
                    name: () => "Draw Color R",
                    getValue: () => pair.Value.DrawColor.R,
                    setValue: value => pair.Value.DrawColor = new Color(value, pair.Value.DrawColor.G, pair.Value.DrawColor.B),
                    min: 0,
                    max: 255
                    );
                configMenu.AddNumberOption(
                    mod: this.ModManifest,
                    name: () => "Draw Color G",
                    getValue: () => pair.Value.DrawColor.G,
                    setValue: value => pair.Value.DrawColor = new Color(pair.Value.DrawColor.R, value, pair.Value.DrawColor.B),
                    min: 0,
                    max: 255
                    );
                configMenu.AddNumberOption(
                    mod: this.ModManifest,
                    name: () => "Draw Color B",
                    getValue: () => pair.Value.DrawColor.B,
                    setValue: value => pair.Value.DrawColor = new Color(pair.Value.DrawColor.R, pair.Value.DrawColor.G, value),
                    min: 0,
                    max: 255
                    );
            }

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => "Default Text Color"
                );
            configMenu.AddNumberOption(
                    mod: this.ModManifest,
                    name: () => "Default Text Color R",
                    getValue: () => Config.DefaultTextColor.R,
                    setValue: value => Config.DefaultTextColor = new Color(value, Config.DefaultTextColor.G, Config.DefaultTextColor.B),
                    min: 0,
                    max: 255
                    );
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Default Text Color G",
                getValue: () => Config.DefaultTextColor.G,
                setValue: value => Config.DefaultTextColor = new Color(Config.DefaultTextColor.R, value, Config.DefaultTextColor.B),
                min: 0,
                max: 255
                );
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Default Text Color B",
                getValue: () => Config.DefaultTextColor.B,
                setValue: value => Config.DefaultTextColor = new Color(Config.DefaultTextColor.R, Config.DefaultTextColor.G, value),
                min: 0,
                max: 255
                );
        }

        void MapConfigOptions()
        {
            // in case i add more object types later but forget to create configs for them:
            foreach (EObjectType type in Enum.GetValues(typeof(EObjectType)))
            {
                m_OptionsConfig.Add(type, new ConfigCategoryOptions() { DrawColor = Color.White, ShouldDrawOverlay = false, ShouldDrawSnaplines = false, ShouldShowInList = false });
            }

            m_OptionsConfig[EObjectType.Debris] = Config.DebrisOptions;
            m_OptionsConfig[EObjectType.GenericForageable] = Config.GenericForageableOptions;
            m_OptionsConfig[EObjectType.GenericObject] = Config.GenericObjectOptions;
            m_OptionsConfig[EObjectType.FiberWeeds] = Config.FiberWeedsOptions;
            m_OptionsConfig[EObjectType.HayGrass] = Config.HayGrassOptions;
            m_OptionsConfig[EObjectType.Tree] = Config.TreeOptions;
            m_OptionsConfig[EObjectType.FruitTree] = Config.FruitTreeOptions;
            m_OptionsConfig[EObjectType.Crop] = Config.CropOptions;
            m_OptionsConfig[EObjectType.ResourceClump] = Config.ResourceClumpOptions;
            m_OptionsConfig[EObjectType.OverlayObject] = Config.OverlayObjectOptions;
            m_OptionsConfig[EObjectType.Bush] = Config.BushOptions;
            m_OptionsConfig[EObjectType.Truffle] = Config.TruffleOptions;
            m_OptionsConfig[EObjectType.ArtifactSpot] = Config.ArtifactSpotOptions;
            m_OptionsConfig[EObjectType.LadderStone] = Config.LadderStoneOptions;
            m_OptionsConfig[EObjectType.Ladder] = Config.LadderOptions;
            m_OptionsConfig[EObjectType.NPC] = Config.NPCOptions;
            m_OptionsConfig[EObjectType.Monster] = Config.EnemyOptions;
            m_OptionsConfig[EObjectType.Quartz] = Config.QuartzOptions;
        }

        private void ClearOutlineList()
        {
            foreach (KeyValuePair<EObjectType, List<ObjReference>> pair in m_OutlineObjects)
            {
                pair.Value.Clear();
            }
        }

        private void GameLoop_DayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            // clear all overlay lists
            ClearOutlineList();
        }

        ECompassDirection GetCompassDirectionFromTo(Vector2 from, Vector2 to)
        {
            Vector2 dir = to - from;
            float maxDot = float.NegativeInfinity;
            int retVal = (int)ECompassDirection.Center;
            for (int index = 0; index < DirVectors.Length; index++)
            {
                float dot = Vector2.Dot(dir, DirVectors[index]);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    retVal = index;
                }
            }
            return (ECompassDirection)retVal;
        }

        bool IsLadderStone(MineShaft shaft, int tileX, int tileY, int stonesRemaining, bool hasLadderSpawned)
        {
            Random random = new Random((((tileX * 1000) + tileY) + shaft.mineLevel) + (((int)Game1.uniqueIDForThisGame) / 2));
            random.NextDouble();
            double chanceToSpawnLadder = ((0.02 + (1.0 / ((double)Math.Max(1, stonesRemaining - 1)))) + (((double)Game1.player.LuckLevel) / 100.0)) + (Game1.player.DailyLuck / 5.0);
            // BUGFIX:
            // LadderLocator uses character.Count, but the game uses EnemyCount. character.Count will never == 0 because the player counts as a character.
            if (shaft.EnemyCount == 0)
            {
                chanceToSpawnLadder += 0.04;
            }
            // NOTE:
            // the game also checks && !this.mustKillAllMonstersToAdvance() in group with !ladderHasSpawned, and shouldCreateLadderOnThisLevel() as separate && group
            if (((!mineshaftHasLadderSpawned) && (!mineshaftMustKillEnemies)) && ((stonesRemaining == 0) || (random.NextDouble() < chanceToSpawnLadder)))
            {
                return true;
            }
            return false;
        }

        private void ProcessObjects(GameLocation location)
        {
            foreach (KeyValuePair<Vector2, StardewValley.Object> pair in location.netObjects.Pairs)
            {
                StardewValley.Object obj = pair.Value;
                if (obj == null) continue;
                //Monitor.Log($"Saw object {obj.DisplayName}");
                string name = "Forage";
                string itemName = (string.IsNullOrEmpty(obj.DisplayName) ? (string.IsNullOrEmpty(obj.Name) ? "unknown" : obj.Name) : obj.DisplayName);
                Vector2 objPos = pair.Key;

                if (obj.Name.ToLower().Contains("truffle"))
                {
                    m_OutlineObjects[EObjectType.Truffle].Add(new ObjReference($"{name} (Truffle)", (int)objPos.X, (int)objPos.Y, EObjectType.Truffle, obj));
                }
                else if ((obj.IsSpawnedObject) || (obj.isForage(location)))
                {
                    m_OutlineObjects[EObjectType.GenericForageable].Add(new ObjReference($"{name} ({itemName})", (int)objPos.X, (int)objPos.Y, EObjectType.GenericForageable, obj));
                }
                else if (obj.parentSheetIndex.Value == 590)
                {
                    // artifact spot
                    m_OutlineObjects[EObjectType.ArtifactSpot].Add(new ObjReference("Artifact Spot", (int)objPos.X, (int)objPos.Y, EObjectType.ArtifactSpot, obj));
                }
                else
                {
                    // generic object
                    m_OutlineObjects[EObjectType.GenericObject].Add(new ObjReference($"Generic Item ({itemName})", (int)objPos.X, (int)objPos.Y, EObjectType.GenericObject, obj));
                }
            }
        }

        void ProcessIndividualTerrainFeature(TerrainFeature feature, GameLocation location)
        {
            if (feature is StardewValley.TerrainFeatures.Tree)
            {
                //Monitor.Log("Saw Tree: " + feature.ToString() + "(" + feature.GetType().Name + ")");
                Tree obj = feature as Tree;
                // TO-DO:
                // RNG calculation for if shaking the tree will yield anything
                // for now, i'm just going to check that it **could** yield something
                if (obj.hasSeed.Value == true)
                {
                    string treeName = "unknown";
                    switch (obj.treeType.Value)
                    {
                        case 1:
                            treeName = "oak";
                            break;
                        case 2:
                            treeName = "maple";
                            break;
                        case 3:
                            treeName = "pine";
                            break;
                        case 7:
                            treeName = "mushroom";
                            break;
                        case 8:
                            treeName = "mahogany";
                            break;
                        case 6:
                        case 9:
                            treeName = "palm";
                            break;
                    }
                    m_OutlineObjects[EObjectType.Tree].Add(new ObjReference($"Shake-able Tree ({treeName})", (int)obj.currentTileLocation.X, (int)obj.currentTileLocation.Y, EObjectType.Tree, obj));
                }
                // TO-DO:
                // maybe check for tapper as well? or maybe the tapper is part of 'objects' list?
            }
            else if (feature is StardewValley.TerrainFeatures.FruitTree)
            {
                //Monitor.Log("Saw FruitTree: " + feature.ToString() + "(" + feature.GetType().Name + ")");
                FruitTree obj = feature as FruitTree;
                if (obj.fruitsOnTree.Value > 0)
                {
                    string name = $"Fruit Tree";
                    string fruitName = "unknown_fruit";
                    try
                    {
                        if (Game1.objectInformation.ContainsKey(obj.indexOfFruit.Value) == true)
                        {
                            fruitName = Game1.objectInformation[obj.indexOfFruit.Value].Split('/')[0];
                        }
                    }
                    catch (Exception ex)
                    {
                        Monitor.Log($"Handled exception parsing fruit tree item (item index={obj.indexOfFruit.Value}): {ex.Message}");
                    }
                    finally
                    {
                        name = $"{name} ({obj.fruitsOnTree.Value} {fruitName})";
                        m_OutlineObjects[EObjectType.FruitTree].Add(new ObjReference(name, (int)obj.currentTileLocation.X, (int)obj.currentTileLocation.Y, EObjectType.FruitTree, obj));
                    }
                }
            }
            else if (feature is StardewValley.TerrainFeatures.Grass)
            {
                Grass obj = feature as Grass;
                //Monitor.Log($"Saw Grass: {feature} ({feature.GetType().Name}) (numWeeds: {obj.numberOfWeeds.Value})");
                // TO-DO:
                // RNG calculation for whether scything this grass will yield hay
                // it seems there's also a super tiny chance that it could yield object IDs 114, 4, or 92. don't know what those are.
                m_OutlineObjects[EObjectType.HayGrass].Add(new ObjReference("Hay Grass", (int)obj.currentTileLocation.X, (int)obj.currentTileLocation.Y, EObjectType.HayGrass, obj));
            }
            else if (feature is StardewValley.TerrainFeatures.HoeDirt)
            {
                //Monitor.Log("Saw HoeDirt: " + feature.ToString() + "(" + feature.GetType().Name + ")");
                HoeDirt obj = feature as HoeDirt;
                string name = "Crop";
                if ((obj.crop != null) && (obj.readyForHarvest()))
                {
                    string cropName = "unknown";
                    if (Game1.objectInformation.ContainsKey(obj.crop.indexOfHarvest.Value))
                    {
                        cropName = Game1.objectInformation[obj.crop.indexOfHarvest.Value].Split('/')[0];
                    }
                    m_OutlineObjects[EObjectType.Crop].Add(new ObjReference($"{name} ({cropName})", (int)obj.currentTileLocation.X, (int)obj.currentTileLocation.Y, EObjectType.Crop, obj));
                }
            }
            else if (feature is StardewValley.TerrainFeatures.Quartz)
            {
                //Monitor.Log("Saw Quartz Terrain Feature?");
                Quartz obj = feature as Quartz;
                m_OutlineObjects[EObjectType.Quartz].Add(new ObjReference("Quartz?? (no idea)", (int)obj.currentTileLocation.X, (int)obj.currentTileLocation.Y, EObjectType.Quartz, obj));
            }
            else if (feature is StardewValley.TerrainFeatures.Bush)
            {
                Bush obj = feature as Bush;

                if ((obj.townBush.Value == false) && (obj.tileSheetOffset.Value == 1) && (obj.inBloom(Game1.GetSeasonForLocation(location), Game1.dayOfMonth)))
                {
                    m_OutlineObjects[EObjectType.Bush].Add(new ObjReference($"Bush", (int)obj.tilePosition.Value.X, (int)obj.tilePosition.Value.Y, EObjectType.Bush, obj));
                }
            }
            else if (feature is LargeTerrainFeature)
            {
                Monitor.Log($"Saw LargeTerrainFeature as part of normal TerrainFeature's ({feature.GetType().Name})");
            }
            // NOTE:
            // GameLocation has its own resourceClumps field, so maybe this isn't part of the terrainFeatures list.
            else if (feature is StardewValley.TerrainFeatures.ResourceClump)
            {
                //Monitor.Log("Saw ResourceClump: " + feature.ToString() + "(" + feature.GetType().Name + ")");
                ResourceClump obj = feature as ResourceClump;
                m_OutlineObjects[EObjectType.ResourceClump].Add(new ObjReference("ResourceClump (ERROR)", (int)obj.tile.Value.X, (int)obj.tile.Value.Y, EObjectType.ResourceClump, obj));
            }
        }

        void ProcessTerrainFeatures(GameLocation location)
        {
            foreach (KeyValuePair<Vector2, StardewValley.TerrainFeatures.TerrainFeature> pair in location.terrainFeatures.Pairs)
            {
                StardewValley.TerrainFeatures.TerrainFeature feature = pair.Value;
                if (feature == null) continue;

                ProcessIndividualTerrainFeature(feature, location);
            }
        }

        void ProcessLargeTerrainFeatures(GameLocation location)
        {
            foreach (StardewValley.TerrainFeatures.LargeTerrainFeature feature in location.largeTerrainFeatures)
            {
                if (feature == null) continue;

                ProcessIndividualTerrainFeature(feature, location);
            }
        }

        void ProcessResourceClumps(GameLocation location)
        {
            foreach (ResourceClump clump in location.resourceClumps)
            {
                //Monitor.Log($"Saw ResourceClump {clump.parentSheetIndex}");

                string name = "ResourceClump (Unknown)";
                if (clump is GiantCrop)
                {
                    GiantCrop giantCrop = clump as GiantCrop;
                    int which = giantCrop.which.Value;
                    switch (which)
                    {
                        case GiantCrop.cauliflower:
                            name = "Giant Crop (cauliflower)";
                            break;
                        case GiantCrop.melon:
                            name = "Giant Crop (melon)";
                            break;
                        case GiantCrop.pumpkin:
                            name = "Giant Crop (pumpkin)";
                            break;
                        default:
                            name = "Giant Crop (unknown)";
                            break;
                    }
                }
                else
                {
                    switch (clump.parentSheetIndex.Value)
                    {
                        case ResourceClump.stumpIndex:
                            {
                                name = "Stump";
                                break;
                            }
                        case ResourceClump.hollowLogIndex:
                            {
                                name = "Hollow Log";
                                break;
                            }
                        case ResourceClump.meteoriteIndex:
                            {
                                name = "Meteorite";
                                break;
                            }
                        case ResourceClump.boulderIndex:
                            {
                                name = "Farm Boulder";
                                break;
                            }
                        case ResourceClump.mineRock1Index:
                        case ResourceClump.mineRock2Index:
                        case ResourceClump.mineRock3Index:
                        case ResourceClump.mineRock4Index:
                            {
                                name = "Mine Boulder";
                                break;
                            }
                    }
                }
                m_OutlineObjects[EObjectType.ResourceClump].Add(new ObjReference(name, (int)clump.tile.Value.X, (int)clump.tile.Value.Y, EObjectType.ResourceClump, clump));
            }
        }

        

        // NOTE:
        // seems like it would contain robin's lost axe and lewis' purple shorts
        // might also contain other stuff
        void ProcessOverlayObjects(GameLocation location)
        {
            foreach (KeyValuePair<Vector2, StardewValley.Object> pair in location.overlayObjects)
            {
                if (pair.Value == null) continue;

                //Monitor.Log($"Saw OverlayObject {pair.Value.DisplayName}");
                StardewValley.Object obj = pair.Value;
                m_OutlineObjects[EObjectType.OverlayObject].Add(new ObjReference(obj.DisplayName, (int)obj.TileLocation.X, (int)obj.TileLocation.Y, EObjectType.OverlayObject, obj));
            }
        }

        void ProcessDebris(GameLocation location)
        {
            foreach (Debris debris in location.debris)
            {
                //Monitor.Log($"Saw debris {debris.spriteChunkSheetName} (item: {(debris.item != null ? debris.item.DisplayName : "null")})");
                string name = "Debris";
                string itemName = "unknown";
                foreach (Chunk chunk in debris.Chunks)
                {
                    if (debris.item != null)
                        itemName = debris.item.DisplayName;

                    m_OutlineObjects[EObjectType.Debris].Add(new ObjReference($"{name} ({itemName})", (int)chunk.position.Value.X, (int)chunk.position.Value.Y, EObjectType.ResourceClump, debris));
                }
            }
        }

        void ProcessFurniture(GameLocation location)
        {
            foreach (StardewValley.Objects.Furniture furniture in location.furniture)
            {
                Monitor.Log($"Saw Furniture: {furniture.DisplayName} ({furniture.GetType().Name})");
            }
        }

        void ProcessNPCs(GameLocation location)
        {
            foreach (StardewValley.NPC npc in location.characters)
            {
                //Monitor.Log($"Saw NPC {npc.displayName}");

                if (npc.IsMonster)
                {
                    m_OutlineObjects[EObjectType.Monster].Add(new ObjReference(npc.Name, (int)npc.getTileLocation().X, (int)npc.getTileLocation().Y, EObjectType.Monster, npc));
                }
                else
                {
                    m_OutlineObjects[EObjectType.NPC].Add(new ObjReference(npc.Name, (int)npc.getTileLocation().X, (int)npc.getTileLocation().Y, EObjectType.NPC, npc));
                }
            }
        }

        void ProcessSpecial_Mineshaft(MineShaft shaft)
        {
            // the mineshaft has 3 layers:
            // [0] is Back
            // [1] is Buildings
            // [2] is Front
            // stones, weeds, quartz, torches, barrels are in the Back layer, ladders/shafts are in Buildings layer.
            // they should all have the same dimensions.
            xTile.Layers.Layer backLayer = shaft.map.GetLayer("Back");
            xTile.Layers.Layer buildingsLayer = shaft.map.GetLayer("Buildings");
            if ((backLayer == null) || (buildingsLayer == null))
                return;
            int layerWidth = backLayer.LayerWidth;
            int layerHeight = backLayer.LayerHeight;
            bool areLayersSameSize = ((backLayer.LayerWidth == buildingsLayer.LayerWidth) && (backLayer.LayerHeight == buildingsLayer.LayerHeight));
            if (!areLayersSameSize)
                this.Monitor.Log("MineShaft does not having matching layer sizes!", LogLevel.Debug);
            int stonesRemaining = _Helper.Reflection.GetField<NetIntDelta>(shaft, "netStonesLeftOnThisLevel", true).GetValue().Value;
            mineshaftHasLadderSpawned = _Helper.Reflection.GetField<bool>(shaft, "ladderHasSpawned", true).GetValue();
            for (int i = 0; i < layerWidth; i++)
            {
                for (int j = 0; j < layerHeight; j++)
                {
                    // check if tile contains a stone object, if so predict whether that stone will spawn ladder
                    Vector2 key = new Vector2(i, j);
                    StardewValley.Object item;
                    if (shaft.objects.TryGetValue(key, out item))
                    {
                        //this.Monitor.Log("Saw object '" + item.DisplayName + "' internal: '" + item.Name + "' type: '" + item.Type + "' sheetIndex: " + item.ParentSheetIndex + " in mineshaft");
                        // NOTES:
                        // diamond ore has item.Name of "Stone" and item.Type of "Basic" and a sheetIndex of 2. normal stone has sheet index around 48-54 with null type.
                        // quartz has DisplayName and Name of "Quartz" and type "Minerals".
                        // copper has sheetIndex of 751
                        // iron has sheetIndex of 290.
                        // gold has sheetIndex of 764
                        // iridum is probably 765? 668 and 670 are something too
                        // red mushroom has DisplayName and Name of "Red Mushroom" and type "Basic" with sheetIndex of 420 (nice)
                        // amethyst ore has item.Name of "Stone" and does NOT have an item.Type, has a sheetIndex of 8.
                        // the minerals (diamond, amethyst, ruby, etc) seem to be 2-14, multiples of 2.
                        // topaz is 10
                        if (item.Name.Equals("Stone"))
                        {
                            if (IsLadderStone(shaft, i, j, stonesRemaining, mineshaftHasLadderSpawned))
                            {
                                m_OutlineObjects[EObjectType.LadderStone].Add(new ObjReference("Ladder Stone", (int)item.TileLocation.X, (int)item.TileLocation.Y, EObjectType.LadderStone));
                            }
                        }
                    }
                    // check if tileindex on Buildings layer is 173 (ladder) or 174 (shaft)
                    // (this detects existing ladders / shafts, not hidden under a rock)
                    else if (areLayersSameSize)
                    {
                        xTile.Tiles.Tile curTile = buildingsLayer.Tiles[i, j];
                        if (curTile == null) continue;
                        if ((curTile.TileIndex == 173) || (curTile.TileIndex == 174))
                        {
                            m_OutlineObjects[EObjectType.Ladder].Add(new ObjReference("Ladder", i, j, EObjectType.Ladder));
                            mineshaftHasLadderSpawned = true;
                        }
                    }
                }
            }
        }

        void ProcessSpecial_Buildings(BuildableGameLocation buildableLocation)
        {
            foreach (StardewValley.Buildings.Building building in buildableLocation.buildings)
            {
                if (building == null) continue;

                Monitor.Log($"Saw building {building.ToString()} ({building.GetType().Name})");
            }
        }
        void ProcessCurrentLocation()
        {
            ClearOutlineList();

            Farmer localPlayer = Game1.player;
            GameLocation location = localPlayer.currentLocation;
            // NOTE:
            // GameLocation also has a 'furniture' field, but it seems like it's only cosmetic stuff

            //Monitor.Log("=== Processing Objects ===");
            ProcessObjects(location);
            //Monitor.Log($"=== Processing [{location.terrainFeatures.Count()}] Terrain Features ===");
            ProcessTerrainFeatures(location);
            //Monitor.Log("=== Processing Large Terrain Features ===");
            ProcessLargeTerrainFeatures(location);
            //Monitor.Log("=== Processing Resource Clumps ===");
            ProcessResourceClumps(location);
            //Monitor.Log("=== Processing Overlay Objects ===");
            ProcessOverlayObjects(location);
            //Monitor.Log("=== Processing Debris ===");
            ProcessDebris(location);
            //Monitor.Log("=== Processing NPCs ===");
            ProcessNPCs(location);


            /* don't see a point to having these, 
             * but maybe there's some weird object that's technically a building or furniture that we would be interested in in the future
            //Monitor.Log("=== Processing Furniture ===");
            ProcessFurniture(location);
            if (location is BuildableGameLocation)
            {
                //Monitor.Log("=== Processing Buildings ===");
                ProcessSpecial_Buildings((location as BuildableGameLocation));
            }
            */

            if (location is MineShaft)
            {
                //Monitor.Log("=== Processing Mineshaft ===");
                ProcessSpecial_Mineshaft((location as MineShaft));
            }
        }

        void DrawOutline(SpriteBatch sb, Rectangle rect, Color color, int lineThickness = 3)
        {
            sb.Draw(pixelTex, new Rectangle(rect.Left, rect.Top, rect.Width, lineThickness), color);
            sb.Draw(pixelTex, new Rectangle(rect.Left, rect.Bottom, rect.Width, lineThickness), color);
            sb.Draw(pixelTex, new Rectangle(rect.Left, rect.Top, lineThickness, rect.Height), color);
            sb.Draw(pixelTex, new Rectangle(rect.Right, rect.Top, lineThickness, rect.Height), color);
        }

        // credit: Viviano Cantu, et al
        // source: https://stackoverflow.com/a/19957844
        //this returns the angle between two points in radians
        private float getRotation(float x, float y, float x2, float y2)
        {
            float adj = x - x2;
            float opp = y - y2;
            float tan = opp / adj;
            float res = MathHelper.ToDegrees((float)Math.Atan2(opp, adj));
            res = (res - 180) % 360;
            if (res < 0) { res += 360; }
            res = MathHelper.ToRadians(res);
            return res;
        }

        void DrawLineTo(SpriteBatch sb, Vector2 start, Vector2 end, Color color, int lineThickness = 3)
        {
            // credit: Viviano Cantu, et al
            // source: https://stackoverflow.com/a/19957844
            // changes: removed comments at end of lines, changed variable names
            int length = (int)Vector2.Distance(start, end);
            float rotation = getRotation(start.X, start.Y, end.X, end.Y);
            Rectangle rect = new Rectangle((int)start.X, (int)start.Y, length, lineThickness);

            sb.Draw(pixelTex, rect, null, color, rotation, Vector2.Zero, SpriteEffects.None, 0.0f);
        }

        Color GetColorByObjectType(EObjectType type)
        {
            Color retColor = Color.White;

            if (m_OptionsConfig.ContainsKey(type))
                retColor = m_OptionsConfig[type].DrawColor;

            return retColor;
        }

        private void Display_RenderedWorld(object sender, StardewModdingAPI.Events.RenderedWorldEventArgs e)
        {
            if ((!Context.IsWorldReady) || (Game1.eventUp)) return;

            // gather all the data
            ProcessCurrentLocation();

            // now draw all the outlines
            xTile.Dimensions.Rectangle viewport = Game1.viewport;
            // ladders get special handling for snapline. we only draw to the nearest.
            bool shouldDrawLadderSnapline = m_OptionsConfig[EObjectType.Ladder].ShouldDrawSnaplines || m_OptionsConfig[EObjectType.LadderStone].ShouldDrawSnaplines;
            bool localPlayerIsInMines = ((Game1.player.currentLocation != null) && (Game1.player.currentLocation.Name.ToLower().Contains("mine")));
            float closestLadderDist = float.MaxValue;
            ObjReference closestLadderObj = null;

            Vector2 localPlayerScreenPos = new Vector2(Game1.player.Position.X - viewport.X, Game1.player.Position.Y - viewport.Y);
            foreach (KeyValuePair<EObjectType, List<ObjReference>> pair in m_OutlineObjects)
            {
                // early exit condition
                if ((m_OptionsConfig[pair.Key].ShouldDrawSnaplines == false) && (m_OptionsConfig[pair.Key].ShouldDrawOverlay == false))
                    continue;

                Color activeColor = GetColorByObjectType(pair.Key);
                foreach (ObjReference obj in pair.Value)
                {
                    if (obj == null) continue;
                    Vector2 loc = new Vector2(obj.X * Game1.tileSize, obj.Y * Game1.tileSize);
                    if (((pair.Key == EObjectType.NPC) || (pair.Key == EObjectType.Monster))
                        && (obj._Ref != null))
                    {
                        loc = (obj._Ref as NPC).Position;
                    }
                    outlineRect.X = (int)loc.X - viewport.X;
                    outlineRect.Y = (int)loc.Y - viewport.Y;

                    // draw outline for object types that have it enabled
                    if (m_OptionsConfig[pair.Key].ShouldDrawOverlay)
                        DrawOutline(e.SpriteBatch, outlineRect, activeColor, 3);

                    // draw snaplines for object types that have it enabled
                    if ((m_OptionsConfig[pair.Key].ShouldDrawSnaplines) && (pair.Key != EObjectType.LadderStone) && (pair.Key != EObjectType.Ladder))
                    {
                        outlineRect.X = (int)(obj.X * Game1.tileSize) - viewport.X + (Game1.tileSize / 2);
                        outlineRect.Y = (int)(obj.Y * Game1.tileSize) - viewport.Y + (Game1.tileSize / 2);
                        DrawLineTo(e.SpriteBatch, localPlayerScreenPos, new Vector2(outlineRect.X, outlineRect.Y), GetColorByObjectType(pair.Key));
                    }

                    // special handling for monsters, draw their health value over their head
                    if (obj._Ref is StardewValley.Monsters.Monster)
                    {
                        Vector2 textDimensions = Game1.smallFont.MeasureString((obj._Ref as StardewValley.Monsters.Monster).Health.ToString());
                        outlineRect.X = (outlineRect.X + (Game1.tileSize / 2)) - (int)(textDimensions.X / 2);
                        outlineRect.Y = (outlineRect.Y - 15) - (int)textDimensions.Y;
                        e.SpriteBatch.DrawString(Game1.smallFont, (obj._Ref as StardewValley.Monsters.Monster).Health.ToString(), new Vector2(outlineRect.X, outlineRect.Y), activeColor);
                    }

                    // special handling for ladder snaplines: get nearest ladder / ladderstone
                    if ((localPlayerIsInMines) && (shouldDrawLadderSnapline) && ((obj.Type == EObjectType.LadderStone) || (obj.Type == EObjectType.Ladder)))
                    {
                        float dist = Vector2.DistanceSquared(localPlayerScreenPos, new Vector2(outlineRect.X, outlineRect.Y));
                        if (dist < closestLadderDist)
                        {
                            closestLadderDist = dist;
                            closestLadderObj = obj;
                        }
                    }
                }
            }

            // special handling for ladder snaplines: draw line to **only** the nearest ladder / ladderstone
            if ((localPlayerIsInMines) && (shouldDrawLadderSnapline) && (closestLadderObj != null))
            {
                // draw line to closest ladder
                outlineRect.X = (int)(closestLadderObj.X * Game1.tileSize) - viewport.X + (Game1.tileSize / 2);
                outlineRect.Y = (int)(closestLadderObj.Y * Game1.tileSize) - viewport.Y + (Game1.tileSize / 2);
                DrawLineTo(e.SpriteBatch, localPlayerScreenPos, new Vector2(outlineRect.X, outlineRect.Y), GetColorByObjectType(closestLadderObj.Type));
            }
        }

        private void Display_RenderedHud(object sender, StardewModdingAPI.Events.RenderedHudEventArgs e)
        {
            if ((!Context.IsWorldReady) || (Game1.eventUp)) return;
            // show helpful information based on where the player is located:
            // mineshaft: how many enemies remain, whether all enemies need to be killed
            // everywhere: everything else
            string drawStr = "";
            if (Game1.player.currentLocation.NameOrUniqueName.StartsWith("UndergroundMine"))
            {
                MineShaft shaft = Game1.player.currentLocation as MineShaft;
                drawStr = "Enemies: " + shaft.EnemyCount.ToString();
                if (mineshaftMustKillEnemies)
                {
                    drawStr += "\nMustKill: " + mineshaftMustKillEnemies.ToString();
                }
            }

            // list out all the tracked items on the map
            Dictionary<string, int> specificObjCounts = new Dictionary<string, int>();
            foreach(KeyValuePair<EObjectType, List<ObjReference>> pair in m_OutlineObjects)
            {
                if ((pair.Value == null) || (pair.Value.Count == 0)) continue;

                if (m_OptionsConfig[pair.Key].ShouldShowInList == false) continue;

                drawStr += "\n" + pair.Key.ToString() + ": " + pair.Value.Count.ToString();
                foreach (ObjReference obj in pair.Value)
                {
                    if (obj == null) continue;

                    if (specificObjCounts.ContainsKey(obj.Name))
                        specificObjCounts[obj.Name]++;
                    else
                        specificObjCounts[obj.Name] = 1;
                }

                foreach (KeyValuePair<string, int> objPair in specificObjCounts)
                {
                    drawStr += "\n\t" + objPair.Key + ": " + objPair.Value.ToString();
                }

                specificObjCounts.Clear();
            }

            if (string.IsNullOrEmpty(drawStr))
                return;

            Vector2 textDimensions = Game1.smallFont.MeasureString(drawStr);
            e.SpriteBatch.Draw(pixelTex, new Rectangle(5, 50, (int)textDimensions.X + 40, (int)textDimensions.Y + 20), new Color(0, 0, 0, 80));
            //IClickableMenu.drawTextureBox(e.SpriteBatch, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), 5, 50, (int)textDimensions.X + 40, (int)textDimensions.Y + 20, new Color(255, 255, 255, 80), 4f, true);

            e.SpriteBatch.DrawString(Game1.smallFont, drawStr, new Vector2(25f, 52f), Config.DefaultTextColor);
        }
    }
}
