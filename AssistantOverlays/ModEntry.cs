using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;

namespace kotae.AssistantOverlays
{
    public class ModEntry : Mod
    {
        enum EObjectType { Truffle, LadderStone, Ladder, Shaft, AnnoyingCrabMonster }
        enum ECompassDirection { North, South, East, West, NorthWest, NorthEast, SouthEast, SouthWest, Center}
        Vector2[] DirVectors = new Vector2[] {new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(-1f, 0f),
            new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f)};

        class ObjReference
        {
            public int X, Y;
            public EObjectType Type;
            public object _Ref;
            public ObjReference(int _TileX, int _TileY, EObjectType _type, object _ref = null)
            {
                X = _TileX;
                Y = _TileY;
                Type = _type;
                _Ref = _ref;
            }
        }

        Texture2D pixelTex;
        Rectangle outlineRect = new Rectangle(0, 0, Game1.tileSize, Game1.tileSize);

        List<ObjReference> m_OutlineObjects = new List<ObjReference>();
        List<ObjReference> m_OutlineNPCs = new List<ObjReference>();
        MineShaft m_ActiveMineshaft;
        bool mineshaftHasLadderSpawned = false;
        bool mineshaftMustKillEnemies = false;
        int mineshaftLadderStones = 0;
        Vector2 mineshaftLadderPos = new Vector2(0f, 0f);

        int mineshaft_NumCopperNodes = 0;
        int mineshaft_NumIronNodes = 0;
        int mineshaft_NumGoldNodes = 0;
        int mineshaft_NumIridiumNodes = 0;
        int mineshaft_NumShrooms = 0;

        IModHelper _Helper;
        ModConfig Config;

        public override void Entry(IModHelper helper)
        {
            _Helper = helper;
            Config = _Helper.ReadConfig<ModConfig>();

            pixelTex = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            pixelTex.SetData(new Color[] { Color.White });
            helper.Events.Display.RenderedWorld += Display_RenderedWorld;
            helper.Events.Display.RenderedHud += Display_RenderedHud;
            helper.Events.Player.Warped += Player_Warped;
            // this fires when a stone is broken (or stones if using an explosive) in the mines, and when a pig creates truffles on the farm
            // it also fires when picking up produce from inside the coop/barns
            helper.Events.World.ObjectListChanged += World_ObjectListChanged;
            helper.Events.World.NpcListChanged += World_NpcListChanged;
            helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
        }

        private void GameLoop_DayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            // clear all overlay lists
            m_OutlineObjects.Clear();
            mineshaftLadderStones = 0;
            m_OutlineNPCs.Clear();
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

        void FindTruffles(GameLocation location)
        {
            foreach (StardewValley.Object objOnFarm in location.objects.Values)
            {
                if (objOnFarm.Name.ToLower().Contains("truffle"))
                {
                    m_OutlineObjects.Add(new ObjReference((int)objOnFarm.TileLocation.X, (int)objOnFarm.TileLocation.Y, EObjectType.Truffle));
                }
            }
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

        void ProcessMineshaft(MineShaft shaft)
        {
            mineshaftLadderStones = 0;
            m_OutlineObjects.Clear();
            mineshaft_NumShrooms = 0;
            mineshaft_NumCopperNodes = 0;
            mineshaft_NumIronNodes = 0;
            mineshaft_NumGoldNodes = 0;
            mineshaft_NumIridiumNodes = 0;
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
                        // iron has item.Type of "Basic" and sheetindex of 290.
                        // red mushroom has DisplayName and Name of "Red Mushroom" and type "Basic" with sheetIndex of 420 (nice)
                        // amethyst ore has item.Name of "Stone" and does NOT have an item.Type, has a sheetIndex of 8.
                        // the minerals (diamond, amethyst, ruby, etc) seem to be 2-14, multiples of 2.
                        // topaz is 10
                        // gold has sheetIndex of 764
                        // copper has sheetIndex of 751
                        // iridum is probably 765? 668 and 670 are something too
                        if (item.name.Contains("Mushroom"))
                        {
                            mineshaft_NumShrooms += 1;
                        }
                        else if (item.Name.Equals("Stone"))
                        {
                            if (IsLadderStone(shaft, i, j, stonesRemaining, mineshaftHasLadderSpawned))
                            {
                                m_OutlineObjects.Add(new ObjReference((int)item.TileLocation.X, (int)item.TileLocation.Y, EObjectType.LadderStone));
                                mineshaftLadderStones++;
                            }
                            // check if it's an ore too, for text overlays
                            if (item.ParentSheetIndex == 751)
                                mineshaft_NumCopperNodes += 1;
                            else if (item.ParentSheetIndex == 290)
                                mineshaft_NumIronNodes += 1;
                            else if (item.ParentSheetIndex == 764)
                                mineshaft_NumGoldNodes += 1;
                            else if (item.ParentSheetIndex == 765)
                                mineshaft_NumIridiumNodes += 1;
                        }
                    }
                    // check if tileindex on Buildings layer is 173 (ladder) or 174 (shaft)
                    else if (areLayersSameSize)
                    {
                        xTile.Tiles.Tile curTile = buildingsLayer.Tiles[i, j];
                        if (curTile == null) continue;
                        if (curTile.TileIndex == 173)
                        {
                            m_OutlineObjects.Add(new ObjReference(i, j, EObjectType.Ladder));
                            mineshaftLadderPos.X = i;
                            mineshaftLadderPos.Y = j;
                            mineshaftHasLadderSpawned = true;
                        }
                        else if (curTile.TileIndex == 174)
                        {
                            m_OutlineObjects.Add(new ObjReference(i, j, EObjectType.Shaft));
                            mineshaftLadderPos.X = i;
                            mineshaftLadderPos.Y = j;
                            mineshaftHasLadderSpawned = true;
                        }
                    }
                }
            }
        }

        private void World_ObjectListChanged(object sender, StardewModdingAPI.Events.ObjectListChangedEventArgs e)
        {
#if DEBUG
            this.Monitor.Log("Objects on '" + e.Location.NameOrUniqueName + "' changed (" + e.Added.Count().ToString() + " added, " + e.Removed.Count().ToString() + " removed)", LogLevel.Info);
#endif
            // NOTE:
            // the Vector2 in KeyValuePair<Vector2, Object> used for the Added and Removed collections represent the TileCoordinate
            // if location is farm, check if truffle and add to overlay list
            if ((e.Location.IsFarm) && (e.IsCurrentLocation))
            {
                foreach (KeyValuePair<Vector2, StardewValley.Object> addedObj in e.Added)
                {
                    if (addedObj.Value.name == "Truffle")
                        m_OutlineObjects.Add(new ObjReference((int)addedObj.Key.X, (int)addedObj.Key.Y, EObjectType.Truffle));
                }
                foreach (KeyValuePair<Vector2, StardewValley.Object> removedObj in e.Removed)
                {
                    for (int outlineIndex = m_OutlineObjects.Count - 1; outlineIndex >= 0; outlineIndex--)
                    {
                        ObjReference outlineObj = m_OutlineObjects[outlineIndex];
                        if ((outlineObj.X == removedObj.Key.X) && (outlineObj.Y == removedObj.Key.Y))
                        {
                            m_OutlineObjects.RemoveAt(outlineIndex);
                            outlineIndex--;
                            break;
                        }
                    }
                }
            }
            // if location is mineshaft, recalculate where ladder is hiding
            else if ((e.Location.NameOrUniqueName.StartsWith("UndergroundMine")) && (e.IsCurrentLocation))
            {
                // so this is called whenever a stone is broken, but NOT for when a ladder is spawned.
                // we have to redo all the ladder stones because stonesRemaining is one of the variables used in the prediction
                ProcessMineshaft(e.Location as MineShaft);
            }
        }

        private void World_NpcListChanged(object sender, StardewModdingAPI.Events.NpcListChangedEventArgs e)
        {
            this.Monitor.Log("NPCs on '" + e.Location.NameOrUniqueName + "' changed (" + e.Added.Count().ToString() + " added, " + e.Removed.Count().ToString() + " removed)", LogLevel.Trace);
            if (e.IsCurrentLocation)
            {
                if (e.Location.NameOrUniqueName.StartsWith("UndergroundMine"))
                {
                    if ((m_ActiveMineshaft != null) && (m_ActiveMineshaft.EnemyCount == 0))
                    {
                        ProcessMineshaft(m_ActiveMineshaft);
                    }
                    foreach (NPC removedNPC in e.Removed)
                    {
                        for (int outlineIndex = m_OutlineNPCs.Count - 1; outlineIndex >= 0; outlineIndex--)
                        {
                            ObjReference outlineNPC = m_OutlineNPCs[outlineIndex];
                            if (outlineNPC._Ref == removedNPC)
                            {
                                m_OutlineNPCs.RemoveAt(outlineIndex);
                                outlineIndex--;
                            }
                        }
                    }
                }
            }
        }

        private void Player_Warped(object sender, StardewModdingAPI.Events.WarpedEventArgs e)
        {
#if DEBUG
            this.Monitor.Log("Player Warped to '" + e.NewLocation.NameOrUniqueName + "'", LogLevel.Info);
#endif
            // clear overlay lists of all old locations
            m_OutlineObjects.Clear();
            m_OutlineNPCs.Clear();

            if ((e.NewLocation.IsFarm) && (e.IsLocalPlayer))
            {
                // if location is farm, check e.NewLocation.objects[] for all existing truffles
                FindTruffles(e.NewLocation);
            }
            else if ((e.NewLocation.NameOrUniqueName.StartsWith("UndergroundMine")) && (e.IsLocalPlayer))
            {
                // if location is UndergroundMineXXX, calculate where the ladder is hiding
                m_ActiveMineshaft = e.NewLocation as MineShaft;
                mineshaftMustKillEnemies = m_ActiveMineshaft.mustKillAllMonstersToAdvance();
                ProcessMineshaft(m_ActiveMineshaft);
                // TO-DO:
                // find and mark all rock crabs, too. fuck those guys.
                foreach (NPC npc in e.NewLocation.characters)
                {
                    // "Rock Crab" is npc.Name on floor 15.
                    // "Lava Crab" on floor 112
                    // "Iridium Crab" on SC 32
                    if ((npc.name == "Rock Crab") || (npc.name == "Lava Crab") || (npc.name == "Iridium Crab"))
                    {
                        m_OutlineNPCs.Add(new ObjReference((int)npc.getTileLocation().X, (int)npc.getTileLocation().Y, EObjectType.AnnoyingCrabMonster, npc));
                    }
                }
            }
        }

        void DrawOutline(SpriteBatch sb, Rectangle rect, Color color, int lineThickness = 3)
        {
            sb.Draw(pixelTex, new Rectangle(rect.Left, rect.Top, rect.Width, lineThickness), color);
            sb.Draw(pixelTex, new Rectangle(rect.Left, rect.Bottom, rect.Width, lineThickness), color);
            sb.Draw(pixelTex, new Rectangle(rect.Left, rect.Top, lineThickness, rect.Height), color);
            sb.Draw(pixelTex, new Rectangle(rect.Right, rect.Top, lineThickness, rect.Height), color);
        }

        private void Display_RenderedWorld(object sender, StardewModdingAPI.Events.RenderedWorldEventArgs e)
        {
            if ((!Context.IsWorldReady) || (Game1.eventUp)) return;

            xTile.Dimensions.Rectangle viewport = Game1.viewport;

            // draw all overlays
            foreach (ObjReference obj in m_OutlineObjects)
            {
                Color activeColor = Color.White;
                switch (obj.Type)
                {
                    case EObjectType.Truffle:
                        activeColor = Config.TruffleColor;
                        break;
                    case EObjectType.LadderStone:
                        activeColor = Config.LadderStoneColor;
                        break;
                    case EObjectType.Ladder:
                        activeColor = Config.LadderColor;
                        break;
                    case EObjectType.Shaft:
                        activeColor = Config.ShaftColor;
                        break;
                }
                outlineRect.X = (obj.X * Game1.tileSize) - viewport.X;
                outlineRect.Y = (obj.Y * Game1.tileSize) - viewport.Y;
                DrawOutline(e.SpriteBatch, outlineRect, activeColor, 3);
            }

            foreach (ObjReference npc in m_OutlineNPCs)
            {
                if ((npc != null) && (npc._Ref != null))
                {
                    NPC monsterNPC = npc._Ref as NPC;
                    Vector2 loc = monsterNPC.Position;
                    outlineRect.X = (int)loc.X  - viewport.X;
                    outlineRect.Y = (int)loc.Y - viewport.Y;
                    DrawOutline(e.SpriteBatch, outlineRect, Config.EnemyColor, 3);
                }
            }
        }

        private void Display_RenderedHud(object sender, StardewModdingAPI.Events.RenderedHudEventArgs e)
        {
            if ((!Context.IsWorldReady) || (Game1.eventUp)) return;
            // show helpful information based on where the player is located:
            // farm: how many truffles are waiting to be picked up
            // mineshaft: whether the ladder has already spawned, how many enemies remain, whether all enemies need to be killed
            if (Game1.player.currentLocation.IsFarm)
            {
                e.SpriteBatch.DrawString(Game1.smallFont, "Truffles: " + m_OutlineObjects.Count.ToString(), new Vector2(10f, 65f), Config.TextColor);
            }
            else if ((Game1.player.currentLocation.NameOrUniqueName.StartsWith("UndergroundMine")) && (m_ActiveMineshaft != null))
            {
                string drawStr = "Enemies: " + m_ActiveMineshaft.EnemyCount.ToString();
                if (mineshaft_NumShrooms > 0)
                {
                    drawStr += "\nMushrooms: " + mineshaft_NumShrooms.ToString();
                }
                if (mineshaft_NumCopperNodes > 0)
                {
                    drawStr += "\nCopper: " + mineshaft_NumCopperNodes.ToString();
                }
                if (mineshaft_NumIronNodes > 0)
                {
                    drawStr += "\nIron: " + mineshaft_NumIronNodes.ToString();
                }
                if (mineshaft_NumGoldNodes > 0)
                {
                    drawStr += "\nGold: " + mineshaft_NumGoldNodes.ToString();
                }
                if (mineshaft_NumIridiumNodes > 0)
                {
                    drawStr += "\nIridium: " + mineshaft_NumIridiumNodes.ToString();
                }
                if (mineshaftHasLadderSpawned)
                {
                    drawStr += "\nLadder: " + GetCompassDirectionFromTo(Game1.player.getTileLocation(), mineshaftLadderPos).ToString();
                }
                else
                {
                    if (mineshaftMustKillEnemies)
                    {
                        drawStr += "\nMustKill: " + mineshaftMustKillEnemies.ToString();
                    }
                    else
                    {
                        drawStr += "\nLadderStones: " + mineshaftLadderStones.ToString();
                    }
                }
                e.SpriteBatch.DrawString(Game1.smallFont, drawStr, new Vector2(10f, 65f), Config.TextColor);
            }
        }
    }
}
