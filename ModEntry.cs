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
        bool IsSmallerInterrior = false;

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
                    string? configvalue = config?["SmallerInterrior"]?.GetValue<string>();
                    IsSmallerInterrior = bool.TryParse(configvalue, out bool result) && result;
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error reading Greenhouse Upgrades config: {ex.Message}", LogLevel.Error);
            }

            var farm = Game1.getFarm();

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
                if (IsSmallerInterrior == true)
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
                Monitor.Log($"{Greenhouse}", LogLevel.Trace);

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

                                var CustomGreenhouse = new Building(id, new Vector2(GreenhouseLocationX, GreenhouseLocationY));

                                Game1.getFarm().buildings.Add(CustomGreenhouse);
                                Game1.player.mailReceived.Add("GreenhouseAdded");
                                Monitor.Log($"Custom greenhouse added", LogLevel.Trace);

                                GameLocation greenhouse = Game1.getLocationFromName("Greenhouse");
                                GameLocation targetLocation = CustomGreenhouse.indoors.Value;
                                if (greenhouse == null)
                                { Monitor.Log("Vanilla greenhouse location is null.", LogLevel.Error); return; }
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
    }
}
