using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Relish
{
    public class Program
    {
        static void Main(string[] args)
        {
            Core Core = new Core("C:\\Program Files\\Epic Games\\DeadByDaylight\\DeadByDaylight\\Content\\Paks");
        }
    }

    public class Core
    {
        private List<string> CharacterNames { get; set; }
        private List<string> Blacklist { get; set; }
        private Dictionary<string, string> AccessKeys { get; set; }
        private Dictionary<string, Action<JObject?>> DB { get; set; }
        private DefaultFileProvider? Provider { get; set; }
        private string PaksPath { get; set; }

        public Core(string paksPath)
        {
            if (Directory.Exists(paksPath) == true)
            {
                PaksPath = paksPath;
                CharacterNames = new List<string>();
                AccessKeys = new Dictionary<string, string>();
                Blacklist = new List<string>()
                {
                    "Item_LamentConfiguration"
                };
                DB = new Dictionary<string, Action<JObject?>>()
                {
                    { "CustomizationItemDB", CustomizationItemDB },
                    { "OutfitDB", OutfitDB },
                    { "CharacterDescriptionDB", CharacterDescriptionDB },
                    { "ItemDB", ItemDB },
                    { "ItemAddonDB", ItemAddonDB },
                    { "OfferingDB", OfferingDB },
                    { "PerkDB", PerkDB }
                };
                LoadProvider();
                GetAccessKey();
                Get_Files();
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
            byte[]? data = null;
            string keyPattern = @"KeyId=""(.*?)"".*?Key=""(.*?)""";
            string keyId = null;
            string key = null;
            (string? keyID, string? key) buffer = (null, null);
            Match match = null;

            Provider.TrySaveAsset("DeadByDaylight/Config/DefaultGame.ini", out data);

            using (var stream = new MemoryStream(data))
            using (var reader = new StreamReader(stream))
            {
                while (reader.ReadLine() is { } line)
                {
                    if (line.Contains("_live") == true)
                    {
                        match = Regex.Match(line, keyPattern);

                        if (match.Success)
                        {
                            keyId = match.Groups[1].Value;
                            key = match.Groups[2].Value;

                            AccessKeys.Add(keyId, key);
                            buffer = (keyId, key);
                        }
                    }
                }
            }

            return (buffer.key);
        }

        private void CustomizationItemDB(JObject? property)
        {
            string cosmeticId = property?["CustomizationId"]?.ToString() ?? string.Empty;

            if (IsInBlacklist(cosmeticId) == false)
            {
                //Ids.CosmeticIds.Add(cosmeticId);
                Console.WriteLine($"CustomizationItemDB: {cosmeticId}");
            }
        }

        private void OutfitDB(JObject? property)
        {
            string outfitId = property?["ID"]?.ToString() ?? string.Empty;

            if (IsInBlacklist(outfitId) == false)
            {
                //Ids.OutfitIds.Add(outfitId);
                Console.WriteLine($"OutfitDB: {outfitId}");
            }
        }

        private void CharacterDescriptionDB(JObject? property)
        {
            if (property?["CharacterId"]?.ToString() != "None")
            {
                CharacterNames.Add($"[{property?["CharacterId"]}] {property?["DisplayName"]?["SourceString"]}");

                Dictionary<string, string> charData = new Dictionary<string, string>
                {
                    { "characterName", property?["CharacterId"]?.ToString() ?? string.Empty },
                    { "characterType", property?["Role"]?.ToString() ?? string.Empty },
                    { "characterDefaultItem", property?["DefaultItem"]?.ToString() ?? string.Empty },
                    { "name", property?["DisplayName"]?["SourceString"]?.ToString() ?? string.Empty }
                };

                //Ids.DlcIds.Add(charData);
                Console.WriteLine($"CharacterDescriptionDB: {charData}");
            }
        }

        private void ItemDB(JObject? property)
        {
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

                if (!IsInBlacklist(itemId))
                {
                    //Ids.ItemIds.Add(itemData);
                    Console.WriteLine($"ItemDB: {itemId}");
                }
            }
            else
            {
                Console.WriteLine($"[ ITEMDB ]: {0}", property?["Type"]?.ToString());
            }
        }

        private void ItemAddonDB(JObject? property)
        {
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

            if (IsInBlacklist(itemAddonData) == false)
            {
                //Ids.AddonIds.Add(itemAddonData);
                Console.WriteLine($"ItemAddonDB: {itemAddonData}");
            }
        }

        private void OfferingDB(JObject? property)
        {
            string offeringId = property?["ItemId"]?.ToString() ?? string.Empty;

            Dictionary<string, string> offeringData = new Dictionary<string, string>
            {
                { "itemId", offeringId },
                { "characterType", property?["Role"]?.ToString() ?? string.Empty },
                { "name", property?["UIData"]?["DisplayName"]?["SourceString"]?.ToString() ?? string.Empty },
                { "rarity", property?["Rarity"]?.ToString() ?? string.Empty },
                { "availability", property?["Availability"]?["itemAvailability"]?.ToString() ?? string.Empty }
            };

            if (IsInBlacklist(offeringData) == false)
            {
                //Ids.OfferingIds.Add(offeringData);
                Console.WriteLine($"OfferingDB: {offeringData}");
            }
        }

        private void PerkDB(JObject? property)
        {
            string perkId = property?["ItemId"]?.ToString() ?? string.Empty;

            Dictionary<string, string> perkData = new Dictionary<string, string>
            {
                { "itemId", perkId },
                { "characterType", property?["Role"]?.ToString() ?? string.Empty },
                { "name", property?["UIData"]?["DisplayName"]?["SourceString"]?.ToString() ?? string.Empty },
                { "rarity", property?["Rarity"]?.ToString() ?? string.Empty },
                { "availability", property?["Availability"]?["itemAvailability"]?.ToString() ?? string.Empty }
            };

            if (IsInBlacklist(perkId) == false)
            {
                //Ids.PerkIds.Add(perkData);
                Console.WriteLine($"PerkDB: {perkData}");
            }
        }

        public void Get_Files()
        {
            List<string> CustomizationItemDB = new List<string>();
            List<string> OutfitDB = new List<string>();
            List<string> CharacterDescriptionDB = new List<string>();
            List<string> ItemDB = new List<string>();
            List<string> ItemAddonDB = new List<string>();
            List<string> OfferingDB = new List<string>();
            List<string> PerkDB = new List<string>();

            foreach (var keyValuePair in Provider.Files.Where(val => val.Value.Path.Contains("DeadByDaylight/Content/Data")))
            {
                switch (keyValuePair.Value.Name)
                {
                    case "CustomizationItemDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "CustomizationItemDB");
                        break;
                    case "OutfitDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "OutfitDB");
                        break;
                    case "CharacterDescriptionDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "CharacterDescriptionDB");
                        break;
                    case "ItemDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "ItemDB");
                        break;
                    case "ItemAddonDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "ItemAddonDB");
                        break;
                    case "OfferingDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "OfferingDB");
                        break;
                    case "PerkDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "PerkDB");
                        break;
                }
            }
        }

        private void Add_Values(string path, string type)
        {
            var export = JsonConvert.DeserializeObject<dynamic>(
                JsonConvert.SerializeObject(Provider?.LoadAllObjects(path))
            ) ?? new ExpandoObject();

            foreach (JProperty p in export[0]?.Rows ?? Enumerable.Empty<JProperty>())
            {
                foreach (JObject? property in p.Values<JObject>())
                {
                    if (DB.ContainsKey(type) == true)
                    {
                        DB[type](property);
                    }
                }
            }
        }

        private bool IsInBlacklist(string id)
        {
            return (Blacklist.Contains(id));
        }

        private bool IsInBlacklist(Dictionary<string, string> ids)
        {
            foreach (KeyValuePair<string, string> id in ids)
            {
                if (IsInBlacklist(id.Key) == true || IsInBlacklist(id.Value) == true)
                {
                    return (true);
                }
            }
            return (false);
        }
    }
}
