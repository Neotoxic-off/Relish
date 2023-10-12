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
using static System.Net.WebRequestMethods;
using static Relish.Market;

namespace Relish
{
    public class Program
    {
        static void Main(string[] args)
        {
            Core Core = new Core("C:\\Program Files\\Epic Games\\DeadByDaylight\\DeadByDaylight\\Content\\Paks");
        }
    }

    public class Market
    {
        public int code { get; set; }
        public string message { get; set; }
        public Data data { get; set; }

        public class Data
        {
            public List<Item> inventory { get; set; }
            public string playerId { get; set; }
        }

        public class Item
        {
            public long lastUpdateAt { get; set; }
            public string objectId { get; set; }
            public int quantity { get; set; }
        }
    }

    public class Core
    {
        private List<string> Blacklist { get; set; }
        private Dictionary<string, string> AccessKeys { get; set; }
        private Dictionary<string, Action<JObject?>> DB { get; set; }
        private DefaultFileProvider? Provider { get; set; }
        private string PaksPath { get; set; }
        private Market Market { get; set; }
        private long unixTime { get; set; }

        public Core(string paksPath)
        {
            DateTime currentTime = DateTime.UtcNow;
            unixTime = ((DateTimeOffset)currentTime).ToUnixTimeSeconds();
            if (Directory.Exists(paksPath) == true)
            {
                PaksPath = paksPath;
                Market = new Market()
                {
                    code = 200,
                    message = "OK",
                    data = new Market.Data()
                    {
                        playerId = "neo",
                        inventory = new List<Market.Item>()
                    }
                };
                AccessKeys = new Dictionary<string, string>();
                Blacklist = new List<string>()
                {
                    "Item_LamentConfiguration",
                    "IO_001",
                    "IO_002",
                    "IO_003",
                    "IO_004"
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

        private void AddToMarket(string itemId, int quantity = 1)
        {
            Item item = new Item()
            {
                lastUpdateAt = unixTime,
                objectId = itemId,
                quantity = quantity
            };

            if (IsInBlacklist(itemId) == false && itemId != string.Empty)
            {
                if (Market.data.inventory.Any(x => x.objectId == item.objectId) == false)
                {
                    Market.data.inventory.Add(item);
                }
            }
        }

        private void CustomizationItemDB(JObject? property)
        {
            AddToMarket(property?["CustomizationId"]?.ToString() ?? string.Empty);
        }

        private void OutfitDB(JObject? property)
        {
            AddToMarket(property?["ID"]?.ToString() ?? string.Empty);
            AddToMarket(property?["_outfitId"]?.ToString() ?? string.Empty);
        }

        private void CharacterDescriptionDB(JObject? property)
        {
            if (property?["CharacterId"]?.ToString() != "None")
            {
                AddToMarket(property?["CharacterId"]?.ToString() ?? string.Empty);
                AddToMarket(property?["CustomizationDescription"]?[0]?["DefaultItemId"]?["RowValue"]?.ToString() ?? string.Empty);
                AddToMarket(property?["CustomizationDescription"]?[1]?["DefaultItemId"]?["RowValue"]?.ToString() ?? string.Empty);
                AddToMarket(property?["CustomizationDescription"]?[2]?["DefaultItemId"]?["RowValue"]?.ToString() ?? string.Empty);
            }
        }

        private void ItemDB(JObject? property)
        {
            if (property?["Type"]?.ToString() != "EInventoryItemType::Power")
            {
                AddToMarket(property?["ItemId"]?.ToString() ?? string.Empty);
            }
        }

        private void ItemAddonDB(JObject? property)
        {
            AddToMarket(property?["ItemId"]?.ToString() ?? string.Empty);
        }

        private void OfferingDB(JObject? property)
        {
            AddToMarket(property?["ItemId"]?.ToString() ?? string.Empty);
        }

        private void PerkDB(JObject? property)
        {
            AddToMarket(property?["ItemId"]?.ToString() ?? string.Empty, 3);
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
            List<string> files = new List<string>();
            List<string> cache = new List<string>();

            Console.WriteLine("==> extracting");
            foreach (var keyValuePair in Provider.Files.Where(val => val.Value.Path.Contains("DeadByDaylight/Content/Data")))
            {
                switch (keyValuePair.Value.Name)
                {
                    case "CustomizationItemDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "CustomizationItemDB");
                        Dump(files, keyValuePair, cache);
                        break;
                    case "OutfitDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "OutfitDB");
                        Dump(files, keyValuePair, cache);
                        break;
                    case "CharacterDescriptionDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "CharacterDescriptionDB");
                        Dump(files, keyValuePair, cache);
                        break;
                    case "ItemDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "ItemDB");
                        Dump(files, keyValuePair, cache);
                        break;
                    case "ItemAddonDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "ItemAddonDB");
                        Dump(files, keyValuePair, cache);
                        break;
                    case "OfferingDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "OfferingDB");
                        Dump(files, keyValuePair, cache);
                        break;
                    case "PerkDB.uasset":
                        Add_Values(keyValuePair.Value.Path, "PerkDB");
                        Dump(files, keyValuePair, cache);
                        break;
                }
            }
            Console.WriteLine("==> extracted");

            Console.WriteLine("==> dumping");
            System.IO.File.WriteAllText("market.json", JsonConvert.SerializeObject(Market, Formatting.Indented));
            Console.WriteLine("==> dumped");
        }

        private void Dump(List<string> files, dynamic keyValuePair, List<string> cache)
        {
            if (files.Contains(keyValuePair.Value.Name) == false && keyValuePair.Value.Name.EndsWith(".uasset"))
            {
                files.Add(keyValuePair.Value.Name);
                var export = JsonConvert.DeserializeObject<dynamic>(
                    JsonConvert.SerializeObject(Provider?.LoadAllObjects(keyValuePair.Value.Path))
                ) ?? new ExpandoObject();

                Console.WriteLine($"\t-> {keyValuePair.Value.Name}");
                foreach (JProperty p in export[0]?.Rows ?? Enumerable.Empty<JProperty>())
                {
                    foreach (JObject? property in p.Values<JObject>())
                    {
                        cache.Add(property.ToString());
                    }
                }
                if (cache.Count() > 0)
                {
                    System.IO.File.AppendAllLines($"raw\\{keyValuePair.Value.Name}.json", cache);
                    cache.Clear();
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
