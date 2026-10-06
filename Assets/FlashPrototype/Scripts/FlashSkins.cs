using UnityEngine;

namespace FlashGame
{
    // A suit palette for the realistic Flash model. Colours are sRGB. A clear Legs, Cowl, Boots or Seams colour
    // means "inherit" (Suit, Suit, Legs and Trim respectively); Cover paints over the face and eyes for full masks.
    // A look with a Character is Barry out of costume instead: that prefab replaces the suited model (only the
    // lightning colours apply).
    public sealed class FlashSkin
    {
        public string Name, Source, Character;
        public Color Suit, Legs, Cowl, Boots, Trim, Seams, EmblemField, EmblemBolt, Cover, Glow, Core;
        public float BootsMetal;
        public Color LegsOrSuit => Legs.a > 0 ? Legs : Suit;
        public Color CowlOrSuit => Cowl.a > 0 ? Cowl : Suit;
        public Color BootsOrLegs => Boots.a > 0 ? Boots : LegsOrSuit;
        public Color SeamsOrTrim => Seams.a > 0 ? Seams : Trim;
    }

    public static class FlashSkins
    {
        const string PrefKey = "FlashGame.Suit.v2";
        const int DefaultSuit = 3;   // Season 4: the look the model's textures were painted for
        static readonly Color BarryGlow = new Color(1, .42f, .06f), BarryCore = new Color(1, .9f, .62f);
        static Color C(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        static Color Optional(string hex) => hex != null ? C(hex) : Color.clear;
        static FlashSkin Skin(string name, string source, string suit, string trim, string field, string bolt, Color glow, Color core,
            string legs = null, string cowl = null, string boots = null, string seams = null, float bootsMetal = 0,
            string cover = null, float coverAmount = 0)
        {
            var coverColor = Optional(cover);
            coverColor.a = coverAmount;
            return new FlashSkin
            {
                Name = name, Source = source, Suit = C(suit), Trim = C(trim), EmblemField = C(field), EmblemBolt = C(bolt),
                Legs = Optional(legs), Cowl = Optional(cowl), Boots = Optional(boots), Seams = Optional(seams), BootsMetal = bootsMetal,
                Cover = coverColor, Glow = glow, Core = core,
            };
        }

        public static readonly FlashSkin[] All =
        {
            // Barry Allen's CW suits, season by season.
            Skin("The Flash \u2022 Season 1", "TV \u2022 burgundy leather, red emblem", "#5C1114", "#A47A33", "#5A0E12", "#C9963A",
                BarryGlow, BarryCore, seams: "#3E0A0C"),
            Skin("The Flash \u2022 Season 2", "TV \u2022 brighter gold, red emblem", "#631316", "#B68A3A", "#6E1418", "#D09A3C",
                BarryGlow, BarryCore, seams: "#43090C"),
            Skin("The Flash \u2022 Season 3", "TV \u2022 white emblem", "#6A1316", "#C2913A", "#ECE6D8", "#C99434",
                BarryGlow, BarryCore, seams: "#4A0C0F"),
            Skin("The Flash \u2022 Season 4", "TV \u2022 brighter red, gold seams", "#7E1515", "#D6A043", "#F2EEE4", "#D6A043",
                BarryGlow, BarryCore),
            Skin("The Flash \u2022 Season 5", "TV \u2022 fabric suit, no gold seams", "#6A1019", "#D4A341", "#F0ECE2", "#D4A341",
                BarryGlow, BarryCore, seams: "#571019"),
            Skin("The Flash \u2022 Season 6", "TV \u2022 bright comic red", "#A3121A", "#E0AA3E", "#F5F2EA", "#E0AA3E",
                BarryGlow, BarryCore, seams: "#8A1017"),
            Skin("The Flash \u2022 Season 7", "TV \u2022 silver accents (reference image)", "#9C121A", "#C9CDD3", "#E6E9EE", "#B9BEC6",
                BarryGlow, BarryCore, seams: "#841018"),
            Skin("The Flash \u2022 Season 8", "TV \u2022 gold boots arrive", "#A3121A", "#E0AA3E", "#F5F2EA", "#E0AA3E",
                BarryGlow, BarryCore, boots: "#D9A63E", seams: "#8A1017", bootsMetal: .6f),
            Skin("The Flash \u2022 Season 9", "TV \u2022 final suit, gold boots", "#9A1018", "#E3AE42", "#F5F2EA", "#E3AE42",
                BarryGlow, BarryCore, boots: "#DCA940", seams: "#82101A", bootsMetal: .6f),
            // Other speedsters from the show.
            Skin("Reverse-Flash", "TV & comics \u2022 Eobard Thawne", "#D8A01C", "#8C1010", "#141414", "#A01212",
                new Color(1, .06f, .03f), new Color(1, .6f, .5f), boots: "#8C1010"),
            Skin("Zoom", "TV \u2022 Hunter Zolomon", "#101012", "#2C2C31", "#0C0C0E", "#36363C",
                new Color(.1f, .35f, 1), new Color(.75f, .88f, 1), cover: "#0B0B0D", coverAmount: .94f),
            Skin("Kid Flash", "TV \u2022 Wally West", "#E2A51E", "#A3161A", "#F2EEE4", "#A3161A",
                new Color(1, .62f, .1f), new Color(1, .96f, .75f), legs: "#9C1418"),
            Skin("Jesse Quick", "TV \u2022 Jesse Wells", "#5A1214", "#E0701E", "#F2EEE4", "#5A1214",
                new Color(1, .55f, .1f), new Color(1, .93f, .7f)),
            Skin("XS", "TV \u2022 Nora West-Allen", "#4A1430", "#8E4FB8", "#F0E8F4", "#7A3CA8",
                new Color(.6f, .15f, 1), new Color(.9f, .75f, 1), cowl: "#5E2D86"),
            Skin("Godspeed", "TV \u2022 August Heart", "#E8EAEE", "#C99A3A", "#F4F5F7", "#C99A3A",
                new Color(.75f, .8f, 1), new Color(1, 1, 1), cover: "#DDE1E8", coverAmount: .9f),
            // Comics.
            Skin("Classic", "Comics \u2022 Silver Age Barry Allen", "#C4131C", "#F2C11C", "#F7F5EE", "#F2C11C",
                new Color(1, .75f, .12f), new Color(1, .97f, .8f), boots: "#F2C11C", bootsMetal: .3f),
            Skin("Rebirth", "Comics \u2022 DC Rebirth", "#981019", "#D99B2E", "#F2EEE4", "#D99B2E",
                new Color(1, .45f, .05f), new Color(1, .9f, .6f), boots: "#D99B2E", bootsMetal: .5f),
            Skin("Golden Age", "Comics \u2022 Jay Garrick colours", "#B5161C", "#E3B12A", "#B5161C", "#E3B12A",
                new Color(1, .7f, .15f), new Color(1, .95f, .75f), legs: "#243A78", boots: "#B5161C"),
            Skin("Impulse", "Comics \u2022 Bart Allen", "#ECEAE4", "#B8141B", "#B8141B", "#F2C11C",
                new Color(1, .7f, .2f), new Color(1, .97f, .85f), legs: "#ECEAE4", boots: "#B8141B"),
            Skin("Negative Flash", "Comics \u2022 Negative Speed Force", "#121214", "#9E1016", "#121214", "#C4141C",
                new Color(1, .05f, .08f), new Color(1, .55f, .55f), cowl: "#1A1A1D"),
            // Barry Allen out of costume (Character/Barry, built from the user's base mesh and CSI design).
            Civilian("Barry Allen", "Out of costume \u2022 tee, jeans and sneakers", "BarryAllen"),
            Civilian("CSI Barry", "Out of costume \u2022 CCPD forensics, plaid overshirt", "CSIBarry"),
        };
        static FlashSkin Civilian(string name, string source, string character) => new FlashSkin
        {
            Name = name, Source = source, Character = character, Suit = C("#2B3A55"), Trim = C("#D9DCE0"),
            EmblemField = Color.white, EmblemBolt = Color.white, Glow = BarryGlow, Core = BarryCore,
        };

        public static int Saved
        {
            get { int i = PlayerPrefs.GetInt(PrefKey, DefaultSuit); return i >= 0 && i < All.Length ? i : DefaultSuit; }
            set { PlayerPrefs.SetInt(PrefKey, value); PlayerPrefs.Save(); }
        }
        public static int Wrap(int index) => ((index % All.Length) + All.Length) % All.Length;
    }
}
