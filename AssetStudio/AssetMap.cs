﻿using MessagePack;
using SevenZip;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace AssetStudio
{
    public static class StringCache
    {
        private static readonly ConcurrentDictionary<uint, string> _cache = new ConcurrentDictionary<uint, string>();

        public static string Get(string value)
        {
            if (value == null) return null;

            uint key = CRC.CalculateDigestUTF8(value);

            if (_cache.TryGetValue(key, out var cached))
                return cached;

            _cache[key] = value;
            return value;
        }
    }
 
    [MessagePackObject]
    public record AssetMap
    {
        [Key(0)]
        public GameType GameType { get; set; }
        [Key(1)]
        public List<AssetEntry> AssetEntries { get; set; }
    }
    [MessagePackObject]
    public record AssetEntry
    {
        private string _name;
        private string _container;
        private string _source;

        [Key(0)]
        public string Name
        {
            get => _name;
            set => _name = StringCache.Get(value);
        }

        [Key(1)]
        public string Container
        {
            get => _container;
            set => _container = StringCache.Get(value);
        }

        [Key(2)]
        public string Source
        {
            get => _source;
            set => _source = StringCache.Get(value);
        }

        [Key(3)]
        public long PathID { get; set; }

        [Key(4)]
        public ClassIDType Type { get; set; }

        public bool Matches(Dictionary<string, Regex> filters)
        {
            var matches = new List<bool>();
            foreach(var filter in filters)
            {
                matches.Add(filter.Key switch
                {
                    string value when value.Equals(nameof(Name), StringComparison.OrdinalIgnoreCase) => filter.Value.IsMatch(Name),
                    string value when value.Equals(nameof(Container), StringComparison.OrdinalIgnoreCase) => filter.Value.IsMatch(Container),
                    string value when value.Equals(nameof(Source), StringComparison.OrdinalIgnoreCase) => filter.Value.IsMatch(Source),
                    string value when value.Equals(nameof(PathID), StringComparison.OrdinalIgnoreCase) => filter.Value.IsMatch(PathID.ToString()),
                    string value when value.Equals(nameof (Type), StringComparison.OrdinalIgnoreCase) => filter.Value.IsMatch(Type.ToString()),
                    _ => throw new NotImplementedException()
                });
            }
            return matches.Count(x => x == true) == filters.Count;
        }
    }
}
