using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace GreenhouseUpgrades
{
    public class Config
    {
        public bool SmallerInterriorEnabled { get; set; }
    }
    public class ModEntry : Mod
    {
        private static IMonitor StaticMonitor;
        string configpath = string.Empty;
        bool IsSmallerInterior = false;

        public override void Entry(IModHelper helper)
        {
            string path = this.Helper.DirectoryPath;
            string ParentFolder = path.Substring(0 , path.Length - 19);
            string FolderName = "[CP] Greenhouse Upgrades";
            string CPFolder = Path.Combine(ParentFolder, FolderName);
            configpath = Path.Combine(CPFolder, "config.json");

            StaticMonitor = Monitor;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        }
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            OnLoadGreenhouseRemoval();
        }
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            InitalGreenhouseExchange();

            try
            {
                if (File.Exists(configpath))
                {
                    string configJson = File.ReadAllText(configpath);
                    JsonNode? config = JsonNode.Parse(configJson);
                    string? configvalue = config?["SmallerInterior"]?.GetValue<string>();
                    IsSmallerInterior = bool.TryParse(configvalue, out bool result) && result;
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error reading Greenhouse Upgrades config: {ex.Message}", LogLevel.Error);
            }

            var farm = Game1.getFarm();

            foreach (var b in farm.buildings)
            {
                CheckGreenhouseUpgradedT1(b, Monitor);

                if (IsSmallerInterior)
                    CheckGreenhouseUpgradedT2Small(b, Monitor);
            }
            string[] targetGreenhouseNames =
            {
                "GreenhouseUpgrades.GreenhouseUpgrade4",
                "GreenhouseUpgrades.GreenhouseUpgrade5"
            };
            var matchingGreenhouses = farm.buildings.Where(b => targetGreenhouseNames.Contains(b.buildingType.Value)).ToList();

            foreach (var b in matchingGreenhouses)
            {
                var interior = b.indoors.Value;
                if (interior == null) continue;
                if (IsSmallerInterior == true)
                {
                    WaterCropsInRectangle(interior, 5, 11, 25, 31);
                }
                else
                {
                    WaterCropsInRectangle(interior, 5, 11, 39, 45);
                }
            }
        }
        private List<string> BuildingsNames = new List<string>();
        public void OnLoadGreenhouseRemoval()
        {
            if ((Game1.player.mailReceived.Contains("GreenhouseAdded")))
            {
                var Greenhouse = Game1.getFarm().buildings.OfType<GreenhouseBuilding>().FirstOrDefault();

                if (Greenhouse.GetIndoorsName() == "Greenhouse")
                {
                    Game1.getFarm().buildings.Remove(Greenhouse);
                    Monitor.Log($"Vanilla greenhouse removed on save load.", LogLevel.Trace);
                }
            }
        }
        public void InitalGreenhouseExchange()
        {
            if (Game1.player.mailReceived.Contains("ccPantry") && !(Game1.player.mailReceived.Contains("GreenhouseAdded")))
            {
                foreach (var location in Game1.locations)
                {
                    if (location.IsBuildableLocation != null)
                    {
                        foreach (var building in location.buildings)
                        {
                            string buildingName = building.GetIndoorsName();
                            BuildingsNames.Add(buildingName);

                            if (buildingName == "Greenhouse")
                            {
                                var GreenhouseLocationX = (float)building.tileX.Value;
                                var GreenhouseLocationY = (float)building.tileY.Value;
                                var DefaultGreenhouse = Game1.getFarm().buildings.OfType<GreenhouseBuilding>().FirstOrDefault();
                                string id;

                                if (Game1.player.hasOrWillReceiveMail("CosmicKillsSouls.GreenhouseV6"))
                                {
                                    id = "GreenhouseUpgrades.GreenhouseUpgrade5";
                                }
                                else if (Game1.player.hasOrWillReceiveMail("CosmicKillsSouls.GreenhouseV5"))
                                {
                                    id = "GreenhouseUpgrades.GreenhouseUpgrade4";
                                }
                                else if (Game1.player.hasOrWillReceiveMail("CosmicKillsSouls.GreenhouseV4"))
                                {
                                    id = "GreenhouseUpgrades.GreenhouseUpgrade3";
                                }
                                else if (Game1.player.hasOrWillReceiveMail("CosmicKillsSouls.GreenhouseV3"))
                                {
                                    id = "GreenhouseUpgrades.GreenhouseUpgrade2";
                                }
                                else if (Game1.player.hasOrWillReceiveMail("CosmicKillsSouls.GreenhouseV2"))
                                {
                                    id = "GreenhouseUpgrades.GreenhouseUpgrade1";
                                }
                                else
                                {
                                    id = "GreenhouseUpgrades.Greenhouse";
                                }

                                Monitor.Log($"{id}", LogLevel.Trace);

                                var CustomGreenhouse = new Building(id, new Vector2(GreenhouseLocationX, GreenhouseLocationY));

                                Game1.getFarm().buildings.Add(CustomGreenhouse);
                                Game1.player.mailReceived.Add("GreenhouseAdded");
                                Monitor.Log($"Custom greenhouse added", LogLevel.Trace);

                                GameLocation greenhouse = Game1.getLocationFromName("Greenhouse");
                                GameLocation targetLocation = CustomGreenhouse.indoors.Value;
                                if (greenhouse == null)
                                { Monitor.Log("Vanilla greenhouse location is null.", LogLevel.Warn); return; }
                                if (targetLocation == null)
                                {
                                    Monitor.Log("Custom greenhouse indoors is null. Attempting manual load...", LogLevel.Trace); CustomGreenhouse.load(); targetLocation = CustomGreenhouse.indoors.Value;
                                    if (targetLocation == null)
                                    { Monitor.Log("Custom greenhouse indoors still null after manual load.", LogLevel.Error); return; }
                                }

                                DelayedAction.functionAfterDelay(() =>
                                {
                                    TransferGreenhouseContents(greenhouse, targetLocation);
                                    Monitor.Log($"Moved contentes of vanilla greenhouse to Custom one", LogLevel.Trace);
                                }, 50);


                                DelayedAction.functionAfterDelay(() =>
                                {
                                    Game1.getFarm().buildings.Remove(DefaultGreenhouse);
                                    Monitor.Log($"Removed vanilla greenhouse.", LogLevel.Trace);
                                }, 50);
                                break;
                            }
                        }
                    }
                }
            }
        }
        private static readonly FieldInfo TF_CurrentLocation_Field = typeof(TerrainFeature).GetField("currentLocation", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo HoeDirt_NeedsUpdate_Field = typeof(HoeDirt).GetField("needsUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        private static void SetCurrentLocation(TerrainFeature tf, GameLocation loc)
        {
            TF_CurrentLocation_Field?.SetValue(tf, loc);
        }
        private static void SanitizeFertilizerCrop(HoeDirt dirt)
        {
            if (dirt.crop == null && dirt.fertilizer.Value != "0")
                dirt.fertilizer.Value ="0";
        }
        private static void MarkDirtAndNeighborsDirty(GameLocation loc, Vector2 tile)
        {
            if (loc == null || HoeDirt_NeedsUpdate_Field == null) return;

            void mark(Vector2 t)
            {
                if (loc.terrainFeatures.TryGetValue(t, out var tf) && tf is HoeDirt hd)
                    HoeDirt_NeedsUpdate_Field.SetValue(hd, true);
            }

            mark(tile);
            mark(tile + new Vector2(1, 0));
            mark(tile + new Vector2(-1, 0));
            mark(tile + new Vector2(0, 1));
            mark(tile + new Vector2(0, -1));
        }
        public static void TransferGreenhouseContents(GameLocation greenhouse, GameLocation targetLocation)
        {
            foreach (var pair in greenhouse.terrainFeatures.Pairs.ToList())
            {
                var tile = pair.Key;
                var tf = pair.Value;

                greenhouse.terrainFeatures.Remove(pair.Key);
                targetLocation.terrainFeatures[tile] = tf;

                SetCurrentLocation(tf, targetLocation);
                if (tf is HoeDirt dirt)
                {
                    SanitizeFertilizerCrop(dirt);
                    MarkDirtAndNeighborsDirty(targetLocation, tile);
                }

            }
            foreach (var pair in greenhouse.objects.Pairs.ToList())
            {
                var tile = pair.Key;
                var obj = pair.Value;

                greenhouse.objects.Remove(pair.Key);
                targetLocation.objects[tile] = obj;

                if (obj is IndoorPot pot && pot.hoeDirt.Value is HoeDirt potdirt)
                {
                    SetCurrentLocation(potdirt, targetLocation);
                    SanitizeFertilizerCrop(potdirt);
                }

            }
            foreach (Furniture furniture in greenhouse.furniture.ToList())
            {
                targetLocation.furniture.Add(furniture);
                greenhouse.furniture.Remove(furniture);
            }
            foreach (var pair in greenhouse.modData.Pairs)
            {
                targetLocation.modData[pair.Key] = pair.Value;
            }
            StaticMonitor.Log($"Greenhouse contents transfered.", LogLevel.Trace);
        }
        private static void WaterCropsInRectangle(GameLocation location, int xStart, int yStart, int xEnd, int yEnd)
        {
            for (int x = xStart; x <= xEnd; x++)
            {
                for (int y = yStart; y <= yEnd; y++)
                {
                    var tile = new Vector2(x, y);
                    if (location.terrainFeatures.ContainsKey(tile) && location.terrainFeatures[tile] is HoeDirt dirt)
                    {
                        dirt.state.Value = HoeDirt.watered;
                    }
                }

            }
        }
        public static void MoveGreenhouseSection(GameLocation location, Rectangle sourceArea, Vector2 offset)
        {
            Vector2 OffsetTile(Vector2 tile) => tile + offset;

            // Phase 1: Collect everything to move
            var terrainToMove = location.terrainFeatures.Pairs
                .Where(p => sourceArea.Contains((int)p.Key.X, (int)p.Key.Y))
                .Select(p => (Original: p.Key, New: OffsetTile(p.Key), Feature: p.Value))
                .ToList();

            var objectsToMove = location.objects.Pairs
                .Where(p => sourceArea.Contains((int)p.Key.X, (int)p.Key.Y))
                .Select(p => (Original: p.Key, New: OffsetTile(p.Key), Object: p.Value))
                .ToList();

            var furnitureToMove = location.furniture
                .Where(f => sourceArea.Contains((int)f.TileLocation.X, (int)f.TileLocation.Y))
                .ToList();

            // Phase 2: Remove all from original positions
            foreach (var (Original, _, _) in terrainToMove)
                location.terrainFeatures.Remove(Original);

            foreach (var (Original, _, _) in objectsToMove)
                location.objects.Remove(Original);

            foreach (var furniture in furnitureToMove)
                location.furniture.Remove(furniture);

            // Phase 3: Reinsert at new positions
            foreach (var (Original, New, Feature) in terrainToMove)
            {
                location.terrainFeatures[New] = Feature;
                SetCurrentLocation(Feature, location);

                if (Feature is HoeDirt dirt)
                {
                    SanitizeFertilizerCrop(dirt);
                    MarkDirtAndNeighborsDirty(location, New);
                }
            }

            foreach (var (Original, New, Object) in objectsToMove)
            {
                location.objects[New] = Object;

                if (Object is IndoorPot pot && pot.hoeDirt.Value is HoeDirt potdirt)
                {
                    SetCurrentLocation(potdirt, location);
                    SanitizeFertilizerCrop(potdirt);
                }
            }

            foreach (var furniture in furnitureToMove)
            {
                furniture.TileLocation += offset;
                location.furniture.Add(furniture);
            }

            StaticMonitor.Log($"Moved greenhouse section {sourceArea} by {offset}.", LogLevel.Info);
        }
        private bool CheckGreenhouseUpgradedT1(Building b, IMonitor monitor)
        {
            const string UpgradeKey = "CsmicKillsSouls.GreenhouseUpgrades/TrackingGreenhouseT1";
            var interior = b.indoors.Value;
            if (b.buildingType.Value == "GreenhouseUpgrades.Greenhouse" && b.daysUntilUpgrade.Value > 0 && !b.modData.ContainsKey(UpgradeKey))
            {
                b.modData[UpgradeKey] = "InProgress";
                return false;
            }
            if (b.buildingType.Value == "GreenhouseUpgrades.GreenhouseUpgrade1" && b.daysOfConstructionLeft.Value == 0 && b.modData.ContainsKey(UpgradeKey))
            {
                if (interior != null)
                {
                    var section = new Rectangle(4, 10, 15, 13);
                    var offset = new Vector2(1, 1);
                    MoveGreenhouseSection(interior, section, offset);
                }
                b.modData.Remove(UpgradeKey);
                return true;
            }

            return false;
        }
        private bool CheckGreenhouseUpgradedT2Small(Building b, IMonitor monitor)
        {
            const string UpgradeKey = "CsmicKillsSouls.GreenhouseUpgrades/TrackingGreenhouseT2small";
            var interior = b.indoors.Value;
            if (b.buildingType.Value == "GreenhouseUpgrades.GreenhouseUpgrade1" && b.daysUntilUpgrade.Value > 0 && !b.modData.ContainsKey(UpgradeKey))
            {
                b.modData[UpgradeKey] = "InProgress";
                return false;
            }
            if (b.buildingType.Value == "GreenhouseUpgrades.GreenhouseUpgrade2" && b.daysOfConstructionLeft.Value == 0 && b.modData.ContainsKey(UpgradeKey))
            {
                if (interior != null)
                {
                    var moves = new List<(Rectangle source, Vector2 DateTimeOffset)>
                    {
                        (new Rectangle(14, 7, 3, 1), new Vector2(-3, 0)),
                        (new Rectangle(4, 26, 20, 4), new Vector2(0, 6)),
                        (new Rectangle(20, 10, 4, 17), new Vector2(6, 0)),
                        (new Rectangle(5, 21, 5, 5), new Vector2(0, 1)),
                        (new Rectangle(10, 21, 5, 5), new Vector2(11, -5)),
                        (new Rectangle(15, 21, 5, 5), new Vector2(6, -10)),
                        (new Rectangle(15, 11, 5, 10), new Vector2(1, 0))
                    };
                    var allMoves = new List<(Vector2 origional, Vector2 destination)>();
                    foreach (var (source, offset) in moves)
                    {
                        for (int x = source.X; x< offset.X; x++)
                        {
                            for (int y = source.Y; y< offset.Y; y++)
                            {
                                var origional = new Vector2(x, y);
                                var destination = origional + offset;
                                allMoves.Add((origional, destination));
                            }
                        }
                    }
                    var destinationSet = new HashSet<Vector2>();
                    foreach (var (_, destination) in allMoves)
                    {
                        if (!destinationSet.Add(destination))
                        {
                            Monitor.Log($"Tile conflict at {destination}. Aborting move.", LogLevel.Warn);
                            return false;
                        }
                    }
                    foreach (var (source, offset) in moves)
                    {
                        MoveGreenhouseSection(interior, source, offset);
                    }
                }
                b.modData.Remove(UpgradeKey);
                return true;
            }
            return false;
        }
    }
}
