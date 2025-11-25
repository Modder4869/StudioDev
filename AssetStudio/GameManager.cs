using System;
using System.Collections.Generic;
using System.Linq;
using static AssetStudio.Crypto;

namespace AssetStudio
{
    public static class GameManager
    {
        private static Dictionary<int, Game> Games = new Dictionary<int, Game>();

        static GameManager()
        {
            int index = 0;
            Games.Add(index++, new(GameType.Normal));
            Games.Add(index++, new(GameType.UnityCN));
            Games.Add(index++, new Mhy(GameType.GI, GIMhyShiftRow, GIMhyKey, GIMhyMul, GIExpansionKey, GISBox, GIInitVector, GIInitSeed));
            Games.Add(index++, new Mr0k(GameType.GI_Pack, PackExpansionKey, blockKey: PackBlockKey));
            Games.Add(index++, new Mr0k(GameType.GI_CB1));
            Games.Add(index++, new Blk(GameType.GI_CB2, GI_CBXExpansionKey, initVector: GI_CBXInitVector, initSeed: GI_CBXInitSeed));
            Games.Add(index++, new Blk(GameType.GI_CB3, GI_CBXExpansionKey, initVector: GI_CBXInitVector, initSeed: GI_CBXInitSeed));
            Games.Add(index++, new Mhy(GameType.GI_CB3Pre, GI_CBXMhyShiftRow, GI_CBXMhyKey, GI_CBXMhyMul, GI_CBXExpansionKey, GI_CBXSBox, GI_CBXInitVector, GI_CBXInitSeed));
            Games.Add(index++, new Mr0k(GameType.BH3, BH3ExpansionKey, BH3SBox, BH3InitVector, BH3BlockKey));
            Games.Add(index++, new Mr0k(GameType.BH3Pre, PackExpansionKey, blockKey: PackBlockKey));
            Games.Add(index++, new Mr0k(GameType.BH3PrePre, PackExpansionKey, blockKey: PackBlockKey));
            Games.Add(index++, new Mr0k(GameType.SR_CB2, Mr0kExpansionKey, initVector: Mr0kInitVector, blockKey: Mr0kBlockKey));
            Games.Add(index++, new Mr0k(GameType.SR, Mr0kExpansionKey, initVector: Mr0kInitVector, blockKey: Mr0kBlockKey));
            Games.Add(index++, new Mr0k(GameType.ZZZ_CB1, Mr0kExpansionKey, initVector: Mr0kInitVector, blockKey: Mr0kBlockKey));
            Games.Add(index++, new Mr0k(GameType.TOT, Mr0kExpansionKey, initVector: Mr0kInitVector, blockKey: Mr0kBlockKey, postKey: ToTKey));
            Games.Add(index++, new Game(GameType.GGZ));
            Games.Add(index++, new Game(GameType.GGZV2));
            Games.Add(index++, new Game(GameType.Naraka));
            Games.Add(index++, new Game(GameType.EnsembleStars));
            Games.Add(index++, new Game(GameType.OPFP));
            Games.Add(index++, new Game(GameType.OPBR));
            Games.Add(index++, new Game(GameType.FakeHeader));
            Games.Add(index++, new Game(GameType.FantasyOfWind));
            Games.Add(index++, new Game(GameType.ShiningNikki));
            Games.Add(index++, new Game(GameType.HelixWaltz2));
            Games.Add(index++, new Game(GameType.NetEase));
            Games.Add(index++, new Game(GameType.AnchorPanic));
            Games.Add(index++, new Game(GameType.DreamscapeAlbireo));
            Games.Add(index++, new Game(GameType.ImaginaryFest));
            Games.Add(index++, new Game(GameType.AliceGearAegis));
            Games.Add(index++, new Game(GameType.ProjectSekai));
            Games.Add(index++, new Game(GameType.CodenameJump));
            Games.Add(index++, new Game(GameType.GirlsFrontline));
            Games.Add(index++, new Game(GameType.Reverse1999));
            Games.Add(index++, new Game(GameType.ArknightsEndfield));
            Games.Add(index++, new Game(GameType.JJKPhantomParade));
            Games.Add(index++, new Game(GameType.MuvLuvDimensions));
            Games.Add(index++, new Game(GameType.PartyAnimals));
            Games.Add(index++, new Game(GameType.LoveAndDeepspace));
            Games.Add(index++, new Game(GameType.SchoolGirlStrikers));
            Games.Add(index++, new Game(GameType.ExAstris));
            Games.Add(index++, new Game(GameType.PerpetualNovelty));
            Games.Add(index++, new Game(GameType.GakuenImas));
            Games.Add(index++, new Game(GameType.NarutoMobile));
            Games.Add(index++, new Game(GameType.CardCaptorSakura));
            Games.Add(index++, new Game(GameType.CardCaptorSakuraTEST));
            Games.Add(index++, new Game(GameType.ProjectNet));
            Games.Add(index++, new Game(GameType.ThreeKingdoms));
            Games.Add(index++, new Game(GameType.BLR3));
            Games.Add(index++, new Game(GameType.Metallopus));
            Games.Add(index++, new Game(GameType.EOS));
            Games.Add(index++, new Game(GameType.InfinityKingdom));
            Games.Add(index++, new Game(GameType.SSTX));
            Games.Add(index++, new Game(GameType.LATALE));
            Games.Add(index++, new Game(GameType.SRU));

        }



        public static Game GetGame(GameType gameType) => GetGame((int)gameType);
        public static Game GetGame(int index)
        {
            if (!Games.TryGetValue(index, out var format))
            {
                throw new ArgumentException("Invalid format !!");
            }

            return format;
        }

        public static Game GetGame(string name) => Games.FirstOrDefault(x => x.Value.Name == name).Value;
        public static Game[] GetGames() => Games.Values.ToArray();
        public static string[] GetGameNames() => Games.Values.Select(x => x.Name).ToArray();
        public static string SupportedGames() => $"Supported Games:\n{string.Join("\n", Games.Values.Select(x => x.Name))}";
    }

    public record Game
    {
        public string Name { get; set; }
        public GameType Type { get; }
        public string Ext { get; set; }
        public Action<Game>? Callback { get; set; }
        public object? Data { get; set; }
        public Game(GameType type)
        {
            Name = type.ToString();
            Type = type;
        }
        public Game(GameType type, string ext)
        {
            Name = type.ToString();
            Type = type;
            Ext = ext;
        }

        public sealed override string ToString() => Name;
    }

    public record Mr0k : Game
    {
        public byte[] ExpansionKey { get; }
        public byte[] SBox { get; }
        public byte[] InitVector { get; }
        public byte[] BlockKey { get; }
        public byte[] PostKey { get; }

        public Mr0k(GameType type, byte[] expansionKey = null, byte[] sBox = null, byte[] initVector = null, byte[] blockKey = null, byte[] postKey = null) : base(type)
        {
            ExpansionKey = expansionKey ?? Array.Empty<byte>();
            SBox = sBox ?? Array.Empty<byte>();
            InitVector = initVector ?? Array.Empty<byte>();
            BlockKey = blockKey ?? Array.Empty<byte>();
            PostKey = postKey ?? Array.Empty<byte>();
        }
    }

    public record Blk : Game
    {
        public byte[] ExpansionKey { get; }
        public byte[] SBox { get; }
        public byte[] InitVector { get; }
        public ulong InitSeed { get; }

        public Blk(GameType type, byte[] expansionKey = null, byte[] sBox = null, byte[] initVector = null, ulong initSeed = 0) : base(type)
        {
            ExpansionKey = expansionKey ?? Array.Empty<byte>();
            SBox = sBox ?? Array.Empty<byte>();
            InitVector = initVector ?? Array.Empty<byte>();
            InitSeed = initSeed;
        }
    }

    public record Mhy : Blk
    {
        public byte[] MhyShiftRow { get; }
        public byte[] MhyKey { get; }
        public byte[] MhyMul { get; }

        public Mhy(GameType type, byte[] mhyShiftRow, byte[] mhyKey, byte[] mhyMul, byte[] expansionKey = null, byte[] sBox = null, byte[] initVector = null, ulong initSeed = 0) : base(type, expansionKey, sBox, initVector, initSeed)
        {
            MhyShiftRow = mhyShiftRow;
            MhyKey = mhyKey;
            MhyMul = mhyMul;
        }
    }

    public enum GameType
    {
        Normal,
        UnityCN,
        GI,
        GI_Pack,
        GI_CB1,
        GI_CB2,
        GI_CB3,
        GI_CB3Pre,
        BH3,
        BH3Pre,
        BH3PrePre,
        ZZZ_CB1,
        SR_CB2,
        SR,
        TOT,
        GGZ,
        GGZV2,
        Naraka,
        EnsembleStars,
        OPFP,
        OPBR,
        FakeHeader,
        FantasyOfWind,
        ShiningNikki,
        HelixWaltz2,
        NetEase,
        AnchorPanic,
        DreamscapeAlbireo,
        ImaginaryFest,
        AliceGearAegis,
        ProjectSekai,
        CodenameJump,
        GirlsFrontline,
        Reverse1999,
        ArknightsEndfield,
        JJKPhantomParade,
        MuvLuvDimensions,
        PartyAnimals,
        LoveAndDeepspace,
        SchoolGirlStrikers,
        ExAstris,
        PerpetualNovelty,
        GakuenImas,
        NarutoMobile,
        CardCaptorSakura,
        CardCaptorSakuraTEST,
        ProjectNet,
        ThreeKingdoms,
        BLR3,
        Metallopus,
        EOS,
        InfinityKingdom,
        SSTX,
        LATALE,
        SRU
    }

    public static class GameTypes
    {
        public static bool isMultiBundle { get; set; }

        public static bool IsNormal(this GameType type) => type == GameType.Normal;
        public static bool IsUnityCN(this GameType type) => type == GameType.UnityCN;
        public static bool IsGI(this GameType type) => type == GameType.GI;
        public static bool IsGIPack(this GameType type) => type == GameType.GI_Pack;
        public static bool IsGICB1(this GameType type) => type == GameType.GI_CB1;
        public static bool IsGICB2(this GameType type) => type == GameType.GI_CB2;
        public static bool IsGICB3(this GameType type) => type == GameType.GI_CB3;
        public static bool IsGICB3Pre(this GameType type) => type == GameType.GI_CB3Pre;
        public static bool IsBH3(this GameType type) => type == GameType.BH3;
        public static bool IsBH3Pre(this GameType type) => type == GameType.BH3Pre;
        public static bool IsBH3PrePre(this GameType type) => type == GameType.BH3PrePre;
        public static bool IsZZZCB1(this GameType type) => type == GameType.ZZZ_CB1;
        public static bool IsSRCB2(this GameType type) => type == GameType.SR_CB2;
        public static bool IsSR(this GameType type) => type == GameType.SR;
        public static bool IsTOT(this GameType type) => type == GameType.TOT;
        public static bool IsGGZ(this GameType type) => type == GameType.GGZ;
        public static bool IsGGZV2(this GameType type) => type == GameType.GGZV2;
        public static bool IsNaraka(this GameType type) => type == GameType.Naraka;
        public static bool IsOPFP(this GameType type) => type == GameType.OPFP;
        public static bool IsNetEase(this GameType type) => type == GameType.NetEase;
        public static bool IsArknightsEndfield(this GameType type) => type == GameType.ArknightsEndfield;
        public static bool IsLoveAndDeepspace(this GameType type) => type == GameType.LoveAndDeepspace;
        public static bool IsExAstris(this GameType type) => type == GameType.ExAstris;
        public static bool IsPerpetualNovelty(this GameType type) => type == GameType.PerpetualNovelty;
        public static bool IsShiningNikki(this GameType type) => type == GameType.ShiningNikki;
        public static bool IsGakuenImas(this GameType type) => type == GameType.GakuenImas;
        public static bool IsNarutoMobile(this GameType type) => type == GameType.NarutoMobile;
        public static bool IsCardCaptorSakura(this GameType type) => type == GameType.CardCaptorSakura;
        public static bool IsCardCaptorSakuraTest(this GameType type) => type == GameType.CardCaptorSakuraTEST;
        public static bool IsProjectNet(this GameType type) => type == GameType.ProjectNet;
        public static bool isThreeKingdoms(this GameType type) => type == GameType.ThreeKingdoms;
        public static bool isBLR3(this GameType type) => type == GameType.BLR3;
        public static bool isMetallopus(this GameType type) => type == GameType.Metallopus;
        public static bool isEOS(this GameType type) => type == GameType.EOS;
        public static bool isSSTX(this GameType type) => type == GameType.SSTX;
        public static bool isLATALE(this GameType type) => type == GameType.LATALE;

        public static bool IsGIGroup(this GameType type) => type switch
        {
            GameType.GI or GameType.GI_Pack or GameType.GI_CB1 or GameType.GI_CB2 or GameType.GI_CB3 or GameType.GI_CB3Pre => true,
            _ => false,
        };

        public static bool IsGISubGroup(this GameType type) => type switch
        {
            GameType.GI or GameType.GI_CB2 or GameType.GI_CB3 or GameType.GI_CB3Pre => true,
            _ => false,
        };

        public static bool IsBH3Group(this GameType type) => type switch
        {
            GameType.BH3 or GameType.BH3Pre => true,
            _ => false,
        };

        public static bool IsSRGroup(this GameType type) => type switch
        {
            GameType.SR_CB2 or GameType.SR => true,
            _ => false,
        };

        public static bool IsBlockFile(this GameType type)
        {
            if (isMultiBundle)
            {
                return true;
            }
            return type switch
            {
                GameType.BH3 or
                GameType.BH3Pre or
                GameType.SR or
                GameType.GI_Pack or
                GameType.TOT or
                GameType.ArknightsEndfield => true,

                _ => false,
            };
        }

        public static bool IsMhyGroup(this GameType type) => type switch
        {
            GameType.GI or GameType.GI_Pack or GameType.GI_CB1 or GameType.GI_CB2 or GameType.GI_CB3 or GameType.GI_CB3Pre or GameType.BH3 or GameType.BH3Pre or GameType.BH3PrePre or GameType.SR_CB2 or GameType.SR or GameType.ZZZ_CB1 or GameType.TOT => true,
            _ => false,
        };
    }
}
