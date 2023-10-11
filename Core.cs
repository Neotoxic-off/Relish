using System.Dynamic;
using System.Text.RegularExpressions;
using CUE4Parse.FileProvider;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Relish
{
    public class Core
    {
        private List<string> CharacterNames { get; set; }
        private Dictionary<string, string> AccessKeys { get; set; }
        private DefaultFileProvider? Provider { get; set; }
        private string PaksPath { get; set; }

        public Core(string paksPath)
        {
            if (Directory.Exists(paksPath) == true)
            {
                PaksPath = paksPath;
                CharacterNames = new List<string>();
                AccessKeys = new Dictionary<string, string>();
                LoadProvider();
            }
        }

        private void LoadProvider()
        {
            Provider = new DefaultFileProvider(PaksPath, SearchOption.AllDirectories, true);
            Provider.Initialize();
            Provider.Mount();
            Provider.LoadLocalization();
        }

        public string GetAccessKey()
        {
            var data = null;
            string keyPattern = @"KeyId=""(.*?)"".*?Key=""(.*?)""";
            string keyId = null;
            string key = null;
            (string keyID, string key) buffer = null;
            Match match = null;

            Provider.TrySaveAsset("DeadByDaylight/Config/DefaultGame.ini", out ref data);
            
            using (var stream = new MemoryStream(data))
            using (var reader = new StreamReader(stream))
            {
                while (reader.ReadLine() is { } line)
                {
                    if (line.Contains("_live") == true)
                    {
                        match = Regex.Match(input, keyPattern);

                        if (match.Success)
                        {
                            keyId = match.Groups[1].Value;
                            key = match.Groups[2].Value;

                            AccessKeys.Add({ keyId, key });
                            buffer = (keyId, key);
                        }
                    }
                }
            }

            return (buffer.key);
        }

        internal static void Get_Files()
        {
            foreach (var keyValuePair in Provider.Files.Where(val => val.Value.Path.Contains("DeadByDaylight/Content/Data")))
            {
                switch (keyValuePair.Value.Name)
                {
                    case "CustomizationItemDB.uasset":
                        FilePaths.CustomizationItemDb.Add(keyValuePair.Value.Path);
                        break;
                    case "OutfitDB.uasset":
                        FilePaths.OutfitDb.Add(keyValuePair.Value.Path);
                        break;
                    case "CharacterDescriptionDB.uasset":
                        FilePaths.CharacterDescriptionDb.Add(keyValuePair.Value.Path);
                        break;
                    case "ItemDB.uasset":
                        FilePaths.ItemDb.Add(keyValuePair.Value.Path);
                        break;
                    case "ItemAddonDB.uasset":
                        FilePaths.ItemAddonDb.Add(keyValuePair.Value.Path);
                        break;
                    case "OfferingDB.uasset":
                        FilePaths.OfferingDb.Add(keyValuePair.Value.Path);
                        break;
                    case "PerkDB.uasset":
                        FilePaths.PerkDb.Add(keyValuePair.Value.Path);
                        break;
                }
            }

            Add_Values(FilePaths.CustomizationItemDb, "CustomizationItemDB");
            Add_Values(FilePaths.OutfitDb, "OutfitDB");
            Add_Values(FilePaths.CharacterDescriptionDb, "CharacterDescriptionDB");
            Add_Values(FilePaths.ItemDb, "ItemDB");
            Add_Values(FilePaths.ItemAddonDb, "ItemAddonDB");
            Add_Values(FilePaths.OfferingDb, "OfferingDB");
            Add_Values(FilePaths.PerkDb, "PerkDB");

            const string filePath = "Other/CharacterNames.txt";
            using var writer = new StreamWriter(filePath);
            foreach (var name in CharacterNames)
            {
                writer.WriteLine(name);
            }

            writer.Flush();
            writer.Close();
        }

        private static void Add_Values(List<string> list, string type)
        {
            foreach (var item in list)
            {
                var export = JsonConvert.DeserializeObject<dynamic>(
                    JsonConvert.SerializeObject(Provider?.LoadObjectExports(item))) ?? new ExpandoObject();

                foreach (JProperty p in export[0]?.Rows ?? Enumerable.Empty<JProperty>())
                {
                    var properties = p.Values<JObject>();
                    foreach (var property in properties)
                    {
                        switch (type)
                        {
                            case "CustomizationItemDB":
                                string cosmeticId = property?["CustomizationId"]?.ToString() ?? string.Empty;
                                if (!IsInBlacklist(cosmeticId)) Ids.CosmeticIds.Add(cosmeticId);
                                break;
                            case "OutfitDB":
                                string outfitId = property?["ID"]?.ToString() ?? string.Empty;
                                if (!IsInBlacklist(outfitId)) Ids.OutfitIds.Add(outfitId);
                                break;
                            case "CharacterDescriptionDB":
                                if (property?["CharacterId"]?.ToString() == "None") continue;
                                CharacterNames.Add($"[{property?["CharacterId"]}] {property?["DisplayName"]?["SourceString"]}");

                                Dictionary<string, string> charData = new Dictionary<string, string>
                                {
                                    { "characterName", property?["CharacterId"]?.ToString() ?? string.Empty },
                                    { "characterType", property?["Role"]?.ToString() ?? string.Empty },
                                    { "characterDefaultItem", property?["DefaultItem"]?.ToString() ?? string.Empty },
                                    { "name", property?["DisplayName"]?["SourceString"]?.ToString() ?? string.Empty }
                                };
                                
                                Ids.DlcIds.Add(charData);
                                break;
                            case "ItemDB":
                                if (property?["Type"]?.ToString() != "EInventoryItemType::Power")
                                {
                                    string itemId = property?["ItemId"]?.ToString() ?? string.Empty;
                                    
                                    Dictionary<string, string> itemData = new Dictionary<string, string>
                                    {
                                        { "itemId", itemId },
                                        { "characterType", property?["Role"]?.ToString() ?? string.Empty },
                                        { "name", property?["UIData"]?["DisplayName"]?["SourceString"]?.ToString() ?? string.Empty },
                                        { "rarity", property?["Rarity"]?.ToString() ?? string.Empty },
                                        { "availability", property?["Availability"]?["itemAvailability"]?.ToString() ?? string.Empty }
                                    };
                                    
                                    if (!IsInBlacklist(itemId)) Ids.ItemIds.Add(itemData);
                                }
                                break;
                            case "ItemAddonDB":
                                string itemAddonId = property?["ItemId"]?.ToString() ?? string.Empty;

                                Dictionary<string, string> itemAddonData = new Dictionary<string, string>
                                {
                                    { "itemId", itemAddonId },
                                    { "characterType", property?["Role"]?.ToString() ?? string.Empty },
                                    { "characterDefaultItem", property?["ParentItem"]?["ItemIDs"]?.Count() > 0 ? (property?["ParentItem"]?["ItemIDs"]?[0]?.ToString() ?? string.Empty) : string.Empty },
                                    { "name", property?["UIData"]?["DisplayName"]?["SourceString"]?.ToString() ?? string.Empty },
                                    { "rarity", property?["Rarity"]?.ToString() ?? string.Empty },
                                    { "availability", property?["Availability"]?["itemAvailability"]?.ToString() ?? string.Empty }
                                };
                                
                                if (!IsInBlacklist(itemAddonId)) Ids.AddonIds.Add(itemAddonData);
                                break;
                            case "OfferingDB":
                                string offeringId = property?["ItemId"]?.ToString() ?? string.Empty;

                                Dictionary<string, string> offeringData = new Dictionary<string, string>
                                {
                                    { "itemId", offeringId },
                                    { "characterType", property?["Role"]?.ToString() ?? string.Empty },
                                    { "name", property?["UIData"]?["DisplayName"]?["SourceString"]?.ToString() ?? string.Empty },
                                    { "rarity", property?["Rarity"]?.ToString() ?? string.Empty },
                                    { "availability", property?["Availability"]?["itemAvailability"]?.ToString() ?? string.Empty }
                                };

                                if (!IsInBlacklist(offeringId)) Ids.OfferingIds.Add(offeringData);
                                break;
                            case "PerkDB":
                                string perkId = property?["ItemId"]?.ToString() ?? string.Empty;
                                
                                Dictionary<string, string> perkData = new Dictionary<string, string>
                                {
                                    { "itemId", perkId },
                                    { "characterType", property?["Role"]?.ToString() ?? string.Empty },
                                    { "name", property?["UIData"]?["DisplayName"]?["SourceString"]?.ToString() ?? string.Empty },
                                    { "rarity", property?["Rarity"]?.ToString() ?? string.Empty },
                                    { "availability", property?["Availability"]?["itemAvailability"]?.ToString() ?? string.Empty }
                                };
                                
                                if (!IsInBlacklist(perkId)) Ids.PerkIds.Add(perkData);
                                break;
                        }
                    }
                }
            }
        }

        private static bool IsInBlacklist(string id)
        {
            if (!File.Exists("blacklist.json"))
            {
                var blacklist = new
                {
                    IDs = new List<string>()
                    {
                        "Item_LamentConfiguration"
                    }
                };
                
                File.WriteAllText("blacklist.json", JsonConvert.SerializeObject(blacklist, Formatting.Indented));
            }

            string blacklistContent = File.ReadAllText("blacklist.json");
            JObject json = JObject.Parse(blacklistContent);

            foreach (string? blacklistId in ((JArray)json["IDs"]!)!)
            {
                if (blacklistId == id) return true;
            }

            return false;
        }

        private static class FilePaths
        {
            internal static readonly List<string> CustomizationItemDb = new();
            internal static readonly List<string> OutfitDb = new();
            internal static readonly List<string> CharacterDescriptionDb = new();
            internal static readonly List<string> ItemDb = new();
            internal static readonly List<string> ItemAddonDb = new();
            internal static readonly List<string> OfferingDb = new();
            internal static readonly List<string> PerkDb = new();
        }

        internal static class Ids
        {
            internal static readonly List<object> CosmeticIds = new();
            internal static readonly List<object> OutfitIds = new();
            internal static readonly List<object> DlcIds = new();
            internal static readonly List<object> ItemIds = new();
            internal static readonly List<object> AddonIds = new();
            internal static readonly List<object> OfferingIds = new();
            internal static readonly List<object> PerkIds = new();
        }
    }
}
