using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    // A named spot in the city that events use (crime scenes, rescue sites, landmarks).
    public readonly struct CityPlace
    {
        public readonly string Name;
        public readonly Vector3 Position;   // where the event happens (ground level)
        public readonly Vector3 Facing;     // the way the place opens to the street
        public CityPlace(string name, Vector3 position, Vector3 facing) { Name = name; Position = position; Facing = facing; }
    }

    // Street-level Central City: every city block is split into lots along its streets with a service alley through
    // the middle, so a block reads as a row of separate buildings (brick walk-ups, stone and concrete mid-rises,
    // glass towers downtown) instead of one box. Ground floors carry storefronts with signs and awnings; walk-ups get
    // fire escapes and rooftop water tanks; sidewalks get lamps, hydrants, bins, benches, bus stops, newspaper boxes,
    // parked cars, trash bags and litter; alleys get dumpsters, graffiti and puddles. Landmarks (CCPD, CC Jitters,
    // the Corner Mart) are built by CityLandmarks with walkable interiors.
    public static partial class CityBlocks
    {
        public const float Pitch = 130, BlockHalf = 53, LotHalf = 47, Alley = 4;
        public static readonly List<CityPlace> Shops = new List<CityPlace>();      // storefront doors (robbery targets)
        public static readonly List<CityPlace> Alleys = new List<CityPlace>();     // alley mid-points (muggings)
        public static readonly List<CityPlace> Plazas = new List<CityPlace>();     // busy corners (bomb threats)
        public static CityPlace CornerMart, Ccpd, Jitters;

        sealed class Mats
        {
            public Material asphalt, sidewalk, curb, roof, cornice, trimDark, metal, metalLight, glass, shopGlass, door, lamp, lampPole,
                hydrant, bin, binLid, newsBlue, newsRed, newsYellow, mailbox, bench, wood, dumpster, dumpsterBlue, bagBlack, bagGreen, bagWhite,
                cardboard, paper, can, leaf, puddle, stain, crosswalk, manhole, fireEscape, tank, tankRoof, ac, glassDark, tire, chrome,
                signalBody, red, amber, green, adPanel, pallet, awningGreen, awningRed, awningBlue, awningStriped, grass, signBoard, bark, foliage;
            public Material[] brick, stone, concrete, stucco, curtain, carPaint, graffiti, signColors;
        }
        static Mats m;
        static System.Random rng;
        static CityKit kit;
        static PrototypeWorld world;
        static Transform signs;
        static Font font;
        static Material textMaterial;

        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static int Ri(int a, int b) => rng.Next(a, b);
        static bool Chance(float p) => rng.NextDouble() < p;
        static T Pick<T>(T[] items) => items[rng.Next(items.Length)];

        static Material Facade(string name, Color c, float wall, float bay, float window, float windowH, float lit, float trim = .5f)
        {
            var mat = world.Material(name, c);
            mat.SetFloat("_Surface", 9); mat.SetFloat("_WindowStyle", wall);
            mat.SetVector("_Facade", new Vector4(bay, 3.4f, window / bay, windowH));
            mat.SetVector("_Facade2", new Vector4(4.6f, lit, trim, 0));
            return mat;
        }
        static Material Surface(string name, Color c, float surface, float glow = 0, float style = 0)
        {
            var mat = world.Material(name, c, glow); mat.SetFloat("_Surface", surface); mat.SetFloat("_WindowStyle", style); return mat;
        }

        static void CreateMaterials(Material[] towerFacades)
        {
            m = new Mats();
            m.asphalt = Surface("Street asphalt", new Color(.12f, .125f, .13f), 11);
            m.sidewalk = Surface("Sidewalk paving", new Color(.56f, .55f, .52f), 10);
            m.curb = world.Material("Curb stone", new Color(.62f, .61f, .58f));
            m.roof = Surface("Roof membrane", new Color(.3f, .3f, .31f), 13);
            m.cornice = world.Material("Cornice stone", new Color(.7f, .68f, .62f));
            m.trimDark = world.Material("Dark trim", new Color(.13f, .13f, .14f));
            m.metal = world.Material("Painted steel", new Color(.18f, .2f, .21f));
            m.metalLight = world.Material("Galvanised steel", new Color(.55f, .57f, .58f));
            m.glass = world.Material("Window glass", new Color(.2f, .28f, .32f));
            m.glassDark = world.Material("Tinted glass", new Color(.08f, .1f, .12f));
            m.shopGlass = Surface("Shop window", new Color(.42f, .36f, .28f), 12, .3f);
            m.door = world.Material("Shop door", new Color(.1f, .09f, .08f));
            m.lamp = world.Material("Street lamp glow", new Color(1, .93f, .78f), 1);
            m.lampPole = world.Material("Lamp post green", new Color(.12f, .17f, .15f));
            m.hydrant = world.Material("Hydrant red", new Color(.62f, .1f, .08f));
            m.bin = world.Material("Litter bin green", new Color(.1f, .2f, .14f));
            m.binLid = world.Material("Bin liner", new Color(.05f, .05f, .05f));
            m.newsBlue = world.Material("Newspaper box blue", new Color(.12f, .25f, .55f));
            m.newsRed = world.Material("Newspaper box red", new Color(.6f, .1f, .1f));
            m.newsYellow = world.Material("Newspaper box yellow", new Color(.85f, .7f, .15f));
            m.mailbox = world.Material("Mailbox blue", new Color(.1f, .2f, .45f));
            m.bench = world.Material("Bench slats", new Color(.35f, .24f, .15f));
            m.wood = world.Material("Wood", new Color(.42f, .3f, .2f));
            m.dumpster = world.Material("Dumpster green", new Color(.12f, .26f, .16f));
            m.dumpsterBlue = world.Material("Dumpster blue", new Color(.12f, .2f, .38f));
            m.bagBlack = world.Material("Trash bag black", new Color(.04f, .04f, .045f));
            m.bagGreen = world.Material("Trash bag green", new Color(.1f, .17f, .1f));
            m.bagWhite = world.Material("Trash bag white", new Color(.75f, .75f, .72f));
            m.cardboard = world.Material("Cardboard", new Color(.55f, .42f, .27f));
            m.paper = world.Material("Litter paper", new Color(.85f, .84f, .8f));
            m.can = world.Material("Litter can", new Color(.7f, .15f, .12f));
            m.leaf = world.Material("Dead leaves", new Color(.35f, .26f, .12f));
            m.puddle = world.Material("Puddle", new Color(.08f, .1f, .12f));
            m.stain = world.Material("Grime stain", new Color(.09f, .09f, .09f));
            m.crosswalk = world.Material("Crosswalk paint", new Color(.85f, .84f, .78f));
            m.manhole = world.Material("Manhole iron", new Color(.07f, .07f, .07f));
            m.fireEscape = world.Material("Fire escape iron", new Color(.08f, .08f, .085f));
            m.tank = world.Material("Water tank cedar", new Color(.36f, .26f, .18f));
            m.tankRoof = world.Material("Water tank roof", new Color(.15f, .14f, .13f));
            m.ac = world.Material("Rooftop unit", new Color(.62f, .63f, .62f));
            m.tire = world.Material("Tyres", new Color(.03f, .03f, .03f));
            m.chrome = world.Material("Chrome", new Color(.75f, .76f, .78f));
            m.signalBody = world.Material("Signal housing", new Color(.85f, .7f, .1f));
            m.red = world.Material("Signal red", new Color(1, .1f, .05f), 1);
            m.amber = world.Material("Signal amber", new Color(.35f, .22f, .02f), .3f);
            m.green = world.Material("Signal green", new Color(.15f, 1, .45f), 1);
            m.adPanel = world.Material("Lit advert", new Color(.9f, .92f, .95f), .8f);
            m.pallet = world.Material("Pallet wood", new Color(.6f, .5f, .35f));
            m.awningGreen = world.Material("Awning green", new Color(.1f, .3f, .2f));
            m.awningRed = world.Material("Awning red", new Color(.5f, .1f, .1f));
            m.awningBlue = world.Material("Awning blue", new Color(.12f, .2f, .4f));
            m.awningStriped = Surface("Awning stripes", new Color(.75f, .72f, .65f), 3);
            m.grass = world.Material("Lawn", new Color(.2f, .32f, .15f));
            m.bark = world.Material("Street tree bark", new Color(.2f, .16f, .12f));
            m.foliage = world.Material("Street tree leaves", new Color(.14f, .25f, .12f));
            m.signBoard = world.Material("Sign board", new Color(.08f, .08f, .09f));
            m.brick = new[]
            {
                Facade("Brick red", new Color(.45f, .2f, .15f), 0, 2.8f, 1.3f, .58f, .12f),
                Facade("Brick dark", new Color(.3f, .16f, .13f), 0, 3.2f, 1.5f, .6f, .1f),
                Facade("Brick brown", new Color(.4f, .28f, .2f), 0, 2.6f, 1.2f, .56f, .14f),
                Facade("Brick tan", new Color(.6f, .48f, .34f), 0, 3f, 1.4f, .6f, .12f),
                Facade("Brick grey", new Color(.42f, .38f, .36f), 0, 3.4f, 1.6f, .58f, .1f),
            };
            m.stone = new[]
            {
                Facade("Limestone", new Color(.72f, .69f, .6f), 1, 3f, 1.4f, .62f, .1f, .35f),
                Facade("Sandstone", new Color(.66f, .55f, .42f), 1, 2.8f, 1.3f, .6f, .12f, .35f),
                Facade("Granite", new Color(.5f, .5f, .5f), 1, 3.2f, 1.6f, .64f, .1f, .3f),
                Facade("Brownstone", new Color(.36f, .25f, .2f), 1, 3f, 1.3f, .62f, .14f, .6f),
            };
            m.concrete = new[]
            {
                Facade("Concrete grey", new Color(.6f, .6f, .58f), 2, 3.6f, 2.6f, .55f, .1f, .1f),
                Facade("Concrete warm", new Color(.64f, .58f, .5f), 2, 3.2f, 2.2f, .5f, .12f, .1f),
                Facade("Concrete dark", new Color(.36f, .37f, .38f), 2, 4f, 3.1f, .62f, .08f, .1f),
            };
            m.stucco = new[]
            {
                Facade("Stucco cream", new Color(.78f, .72f, .6f), 3, 3f, 1.3f, .58f, .12f, .4f),
                Facade("Stucco sage", new Color(.55f, .6f, .52f), 3, 3.2f, 1.4f, .58f, .1f, .4f),
            };
            m.curtain = towerFacades;
            m.carPaint = new[]
            {
                world.Material("Car black", new Color(.04f, .04f, .045f)), world.Material("Car white", new Color(.8f, .8f, .78f)),
                world.Material("Car silver", new Color(.5f, .52f, .54f)), world.Material("Car red", new Color(.5f, .06f, .05f)),
                world.Material("Car blue", new Color(.1f, .18f, .38f)), world.Material("Car taxi", new Color(.9f, .68f, .08f)),
                world.Material("Car green", new Color(.12f, .25f, .18f)),
            };
            m.graffiti = new[]
            {
                world.Material("Graffiti magenta", new Color(.7f, .15f, .5f)), world.Material("Graffiti teal", new Color(.1f, .55f, .55f)),
                world.Material("Graffiti orange", new Color(.85f, .45f, .1f)), world.Material("Graffiti lime", new Color(.5f, .75f, .15f)),
            };
            m.signColors = new[]
            {
                world.Material("Sign red", new Color(.55f, .08f, .08f)), world.Material("Sign green", new Color(.08f, .3f, .15f)),
                world.Material("Sign navy", new Color(.08f, .12f, .3f)), world.Material("Sign black", new Color(.06f, .06f, .07f)),
                world.Material("Sign burgundy", new Color(.35f, .08f, .12f)), world.Material("Sign teal", new Color(.05f, .3f, .32f)),
            };
        }

        static readonly string[] ShopNames =
        {
            "PIZZA", "DELI", "PHARMACY", "LAUNDROMAT", "BOOKS", "BARBER", "HARDWARE", "FLOWERS", "BAKERY", "NOODLES",
            "PAWN SHOP", "BIG BELLY BURGER", "TACOS", "DINER", "NAILS", "PHONE REPAIR", "LIQUOR", "SHOES", "TAILOR", "CAFE",
            "BODEGA", "DRY CLEANING", "COMICS", "RECORDS", "BANK", "GYM", "DENTIST", "PET SUPPLY", "WINE BAR", "SUSHI",
        };

        // Called by CityDistrict for every regular city block.
        public static void Begin(PrototypeWorld w, Transform root, Material[] towerFacades)
        {
            world = w; rng = new System.Random(5150);
            var painted = w.Material("City painted detail", Color.white);
            painted.SetFloat("_VertexTint", 1);
            kit = new CityKit(root, "City streets", painted);
            Shops.Clear(); Alleys.Clear(); Plazas.Clear();
            CreateMaterials(towerFacades);
            signs = new GameObject("Shop signs").transform; signs.SetParent(root, false);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textShader = Resources.Load<Shader>("StarLabsText");
            textMaterial = textShader != null ? new Material(textShader) { name = "Shop sign text", mainTexture = font.material.mainTexture } : font.material;
            CitySignFont.Begin(signs, font, textMaterial);
        }
        public static void End() { kit.Build(); CitySignFont.End(); }
        public static Material SidewalkMaterial => m.sidewalk;
        public static Material AsphaltMaterial => m.asphalt;

        // ---- Blocks ---------------------------------------------------------------------------------

        // Downtown is around (150, 0): towers there, mid-rises around it, brick walk-ups toward the edges.
        static float Density(float x, float z) => Mathf.Clamp01(1.25f - new Vector2((x - 150) / 520, z / 480).magnitude);

        public static void Block(float cx, float cz)
        {
            Vector3 c = new Vector3(cx, .24f, cz);
            Sidewalks(c);
            bool landmark = CityLandmarks.Claims(cx, cz);
            for (int side = -1; side <= 1; side += 2)
            {
                // One row of lots per half block, fronting the street on that side; the alley runs between.
                float front = cz + side * LotHalf, back = cz + side * Alley;
                float x = cx - LotHalf;
                float? cut = landmark ? CityLandmarks.Cut(cx, cz, side) : null;
                while (x < cx + LotHalf - .1f)
                {
                    float width = Mathf.Min(R(13, 30), cx + LotHalf - x);
                    if (cx + LotHalf - (x + width) < 11) width = cx + LotHalf - x;
                    if (cut.HasValue && x < cut.Value - .1f && x + width > cut.Value + .1f) width = cut.Value - x;
                    bool west = x <= cx - LotHalf + .1f, east = x + width >= cx + LotHalf - .1f;
                    if (!landmark || !CityLandmarks.OnLot(cx, cz, side, x, x + width))
                        Lot(new Vector3(x + width / 2, .24f, (front + back) / 2), width, Mathf.Abs(front - back), side, west, east);
                    x += width;
                }
            }
            if (landmark) CityLandmarks.Build(kit, world, cx, cz);
            if (!landmark || CityLandmarks.HasAlley(cx, cz)) AlleyDressing(c);
        }
        // Park blocks keep their lawn and trees; they get the same sidewalks and street furniture.
        public static void Park(float cx, float cz) => Sidewalks(new Vector3(cx, .24f, cz));

        // A Central City police cruiser: black and white, light bar, push bar.
        public static void PoliceCar(Vector3 p, Vector3 along)
        {
            Car(p, along, m.carPaint[0]);
            var rot = Quaternion.LookRotation(along);
            Vector3 side = Vector3.Cross(Vector3.up, along);
            for (int s = -1; s <= 1; s += 2) kit.Box(m.carPaint[1], p + side * s * .92f + Vector3.up * .66f, new Vector3(.03f, .5f, 2.2f), rot, false, false);
            kit.Box(m.carPaint[1], p + Vector3.up * 1.35f - along * .25f, new Vector3(1.58f, .03f, 2.2f), rot, false, false);
            kit.Box(m.red, p + Vector3.up * 1.42f - along * .1f + side * .35f, new Vector3(.6f, .12f, .25f), rot, false, false);
            kit.Box(m.signalBody, p + Vector3.up * 1.42f - along * .1f, new Vector3(.12f, .12f, .25f), rot, false, false);
            kit.Box(m.newsBlue, p + Vector3.up * 1.42f - along * .1f - side * .35f, new Vector3(.6f, .12f, .25f), rot, false, false);
            kit.Box(m.trimDark, p + Vector3.up * .55f + along * 2.35f, new Vector3(1.4f, .5f, .1f), rot, false, false);
        }

        static void Lot(Vector3 centre, float width, float depth, int side, bool westCorner, bool eastCorner)
        {
            float d = Density(centre.x, centre.z);
            float roll = (float)rng.NextDouble();
            Vector3 frontNormal = new Vector3(0, 0, side);
            Vector3 frontFace = centre + frontNormal * depth / 2;
            // Style by density: towers downtown, then mid-rise stone/concrete, then walk-ups.
            int style = d > .72f && roll < .55f ? 3 : d > .45f && roll < .6f ? (Chance(.5f) ? 1 : 2) : roll < .78f ? 0 : 4;
            float height;
            Material facade;
            switch (style)
            {
                case 3: height = R(60, 70 + 140 * d); facade = Pick(m.curtain); break;
                case 1: height = 4.6f + 3.4f * Ri(6, 13 + (int)(10 * d)); facade = Pick(m.stone); break;
                case 2: height = 4.6f + 3.4f * Ri(5, 12 + (int)(14 * d)); facade = Pick(m.concrete); break;
                case 4: height = 4.6f + 3.4f * Ri(2, 5); facade = Pick(m.stucco); break;
                default: height = 4.6f + 3.4f * Ri(3, 7); facade = Pick(m.brick); break;
            }
            Vector3 size = new Vector3(width - .3f, height, depth - .3f);
            kit.Box(facade, centre + Vector3.up * height / 2, size, true);
            // Ground storey: storefronts along the street (and the side street on corners), or a tower lobby.
            if (style == 3) Lobby(frontFace, frontNormal, width);
            else Storefronts(frontFace, frontNormal, width);
            if (westCorner) Storefronts(centre + Vector3.left * width / 2 + frontNormal * depth * .1f, Vector3.left, depth * .75f, true);
            if (eastCorner) Storefronts(centre + Vector3.right * width / 2 + frontNormal * depth * .1f, Vector3.right, depth * .75f, true);
            // Belt course over the shops, cornice and roof.
            kit.Box(m.cornice, centre + Vector3.up * 4.75f + frontNormal * (depth / 2 + .05f), new Vector3(width - .1f, .35f, .5f));
            float top = centre.y + height - .24f;
            if (style != 3)
            {
                kit.Box(style == 0 ? m.cornice : m.trimDark, new Vector3(centre.x, top + .3f, centre.z), new Vector3(width + .3f, .6f, depth + .3f));
                kit.Box(m.cornice, new Vector3(centre.x, top - .4f, centre.z) + frontNormal * (depth / 2 + .2f), new Vector3(width, .25f, .5f));
            }
            kit.Box(m.roof, new Vector3(centre.x, top + .62f, centre.z), new Vector3(width - 1, .05f, depth - 1));
            Rooftop(new Vector3(centre.x, top + .6f, centre.z), width, depth, style);
            if (style == 0 && Chance(.6f)) FireEscape(frontFace, frontNormal, width, height, Chance(.5f) ? -1 : 1);
            if ((style == 0 || style == 4) && Chance(.7f)) FireEscape(centre - frontNormal * depth / 2, -frontNormal, width, height, Chance(.5f) ? -1 : 1);
        }

        static void Lobby(Vector3 face, Vector3 n, float width)
        {
            Vector3 right = Vector3.Cross(Vector3.up, n);
            kit.Box(m.glassDark, face + n * .08f + Vector3.up * 2.4f, Abs(right * (width - 4)) + Vector3.up * 4.4f + Abs(n) * .12f);
            for (float t = -width / 2 + 2; t <= width / 2 - 2; t += 3)
                kit.Box(m.metal, face + n * .16f + right * t + Vector3.up * 2.4f, Abs(right * .15f) + Vector3.up * 4.4f + Abs(n) * .14f);
            kit.Box(m.metal, face + n * 2.5f + Vector3.up * 4.6f, Abs(right * 10) + Vector3.up * .35f + Abs(n) * 5);
            kit.Box(m.glass, face + n * .2f + Vector3.up * 1.4f, Abs(right * 3) + Vector3.up * 2.6f + Abs(n) * .1f);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // Shop windows with a door, a sign band (named sign text) and sometimes an awning.
        static void Storefronts(Vector3 face, Vector3 n, float width, bool side = false)
        {
            Vector3 right = Vector3.Cross(Vector3.up, n);
            int shops = Mathf.Max(1, Mathf.RoundToInt(width / R(9, 14)));
            float w = width / shops;
            for (int i = 0; i < shops; i++)
            {
                Vector3 c = face + right * (-width / 2 + w * (i + .5f)) + n * .1f;
                // Pilasters between shops, glazing, a door and a kick plate.
                kit.Box(m.cornice, c + right * (w / 2 - .25f) + Vector3.up * 2.3f, Abs(right * .5f) + Vector3.up * 4.6f + Abs(n) * .3f);
                kit.Box(m.shopGlass, c + Vector3.up * 2.25f, Abs(right * (w - 1)) + Vector3.up * 3.1f + Abs(n) * .1f);
                kit.Box(m.trimDark, c + Vector3.up * .45f, Abs(right * (w - .9f)) + Vector3.up * .55f + Abs(n) * .16f);
                float doorAt = R(-w / 2 + 1.6f, w / 2 - 1.6f);
                kit.Box(m.door, c + right * doorAt + n * .04f + Vector3.up * 1.3f, Abs(right * 1.15f) + Vector3.up * 2.5f + Abs(n) * .14f);
                kit.Box(m.chrome, c + right * (doorAt + .4f) + n * .14f + Vector3.up * 1.2f, new Vector3(.04f, .4f, .04f));
                // Sign band.
                var board = Pick(m.signColors);
                kit.Box(board, c + Vector3.up * 4.05f + n * .12f, Abs(right * (w - .8f)) + Vector3.up * .85f + Abs(n) * .2f);
                string name = Pick(ShopNames);
                CitySignFont.Label(name, c + Vector3.up * 4.05f + n * .23f, n, Mathf.Min(.62f, (w - 1.4f) / Mathf.Max(4, name.Length) * 1.5f), new Color(.98f, .95f, .85f));
                if (Chance(.55f))
                {
                    var awning = Pick(new[] { m.awningGreen, m.awningRed, m.awningBlue, m.awningStriped });
                    var tilt = Quaternion.LookRotation(n) * Quaternion.Euler(22, 0, 0);       // slopes down and out
                    kit.Box(awning, c + Vector3.up * 3.45f + n * .85f, new Vector3(w - 1.2f, .08f, 1.9f), tilt);
                    kit.Box(awning, c + Vector3.up * 3.05f + n * 1.72f, Abs(right * (w - 1.2f)) + Vector3.up * .35f + Abs(n) * .05f);
                }
                if (!side) Shops.Add(new CityPlace(name, c + right * doorAt, n));
            }
        }

        public static TextMesh Sign(string text, Vector3 position, Vector3 facing, float height, Color color)
        {
            var go = new GameObject("Sign: " + text);
            go.transform.SetParent(signs, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(-facing);
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font; mesh.fontSize = 64; mesh.characterSize = height / 6.4f;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
            mesh.fontStyle = FontStyle.Bold; mesh.color = color; mesh.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
            return mesh;
        }

        static void Rooftop(Vector3 roof, float width, float depth, int style)
        {
            int units = Ri(1, 4);
            for (int i = 0; i < units; i++)
                kit.Box(m.ac, roof + new Vector3(R(-width / 3, width / 3), .7f, R(-depth / 3, depth / 3)), new Vector3(R(1.6f, 3), 1.4f, R(1.6f, 2.6f)), R(0, 90), false);
            kit.Box(m.trimDark, roof + new Vector3(R(-width / 4, width / 4), 1.5f, R(-depth / 4, depth / 4)), new Vector3(3, 3, 3.5f));
            if ((style == 0 || style == 1) && Chance(.45f))
            {
                // Cedar water tank on a steel stand with a conical roof.
                Vector3 t = roof + new Vector3(R(-width / 4, width / 4), 0, R(-depth / 4, depth / 4));
                for (int k = 0; k < 4; k++)
                    kit.Beam(m.metal, t + new Vector3(k % 2 == 0 ? -1.3f : 1.3f, 0, k < 2 ? -1.3f : 1.3f), t + new Vector3(k % 2 == 0 ? -1.1f : 1.1f, 3, k < 2 ? -1.1f : 1.1f), .15f, .15f);
                kit.Box(m.metal, t + Vector3.up * 3.05f, new Vector3(3, .15f, 3));
                kit.Cylinder(m.tank, t + Vector3.up * 5, 1.7f, 3.8f, 12);
                kit.Cylinder(m.tankRoof, t + Vector3.up * 7.2f, 1.2f, .6f, 12);
                kit.Cylinder(m.tankRoof, t + Vector3.up * 7.6f, .5f, .3f, 8);
            }
        }

        // Zig-zag iron fire escape: a landing per storey, railings and a stair between landings, a drop ladder.
        static void FireEscape(Vector3 face, Vector3 n, float width, float height, int hand)
        {
            Vector3 right = Vector3.Cross(Vector3.up, n);
            float span = Mathf.Min(7, width - 3);
            Vector3 c = face + n * .75f + right * hand * (width / 2 - span / 2 - 1.2f);
            int floors = Mathf.FloorToInt((height - 4.6f) / 3.4f);
            for (int f = 0; f < floors; f++)
            {
                float y = .24f + 4.6f + f * 3.4f - .2f;
                Vector3 p = new Vector3(c.x, y, c.z);
                kit.Box(m.fireEscape, p, Abs(right * span) + Vector3.up * .06f + Abs(n) * 1.3f);
                kit.Box(m.fireEscape, p + n * .62f + Vector3.up * .5f, Abs(right * span) + Vector3.up * .05f + Abs(n) * .05f);
                kit.Box(m.fireEscape, p + n * .62f + Vector3.up * .25f, Abs(right * span) + Vector3.up * .03f + Abs(n) * .03f);
                for (int s = -1; s <= 1; s += 2)
                    kit.Box(m.fireEscape, p + right * s * span / 2 + n * .3f + Vector3.up * .5f, Abs(right * .05f) + Vector3.up * 1 + Abs(n) * .65f);
                for (float t = -span / 2; t <= span / 2; t += 1.2f)
                    kit.Box(m.fireEscape, p + right * t + n * .62f + Vector3.up * .5f, new Vector3(.03f, 1, .03f));
                if (f + 1 < floors)
                {
                    float dir = f % 2 == 0 ? 1 : -1;
                    Vector3 a = p + right * dir * (-span / 2 + .5f) + n * .2f, b = p + right * dir * (span / 2 - .9f) + n * .2f + Vector3.up * 3.4f;
                    kit.Beam(m.fireEscape, a, b, .55f, .06f);
                }
            }
            if (floors > 0) kit.Beam(m.fireEscape, new Vector3(c.x, .24f + 4.4f, c.z) + right * hand * (span / 2 - .5f), new Vector3(c.x, .24f + 2.2f, c.z) + right * hand * (span / 2 - .5f), .4f, .05f);
        }

        // ---- Sidewalks and street furniture ------------------------------------------------------

        static void Sidewalks(Vector3 c)
        {
            for (int s = 0; s < 4; s++)
            {
                float yaw = s * 90;
                var rot = Quaternion.Euler(0, yaw, 0);
                Vector3 n = rot * Vector3.forward, right = rot * Vector3.right;
                Vector3 edge = c + n * BlockHalf;
                // Curb.
                kit.Box(m.curb, edge - n * .15f + Vector3.up * -.08f, Abs(right * (2 * BlockHalf)) + Vector3.up * .2f + Abs(n) * .3f);
                // Lamps every ~26 m, trees between, a hydrant, bins and boxes by the corners.
                for (float t = -BlockHalf + 6; t <= BlockHalf - 6; t += 26)
                    StreetLamp(edge - n * .8f + right * t, n);
                for (float t = -BlockHalf + 19; t <= BlockHalf - 15; t += 26)
                    if (!(Mathf.Abs(t) < 6)) StreetTree(edge - n * 1.6f + right * t);
                Hydrant(edge - n * .7f + right * R(-35, 35));
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector3 corner = edge - n * 1.1f + right * k * (BlockHalf - 3.5f);
                    Bin(corner + right * -k * 1.5f);
                    if (Chance(.6f)) NewsBoxes(corner + right * -k * 3.5f, n);
                }
                if (Chance(.35f)) Mailbox(edge - n * 1 + right * R(-30, 30));
                if (Chance(.3f)) BusStop(edge - n * 2.2f + right * R(-25, 25), n);
                else if (Chance(.45f)) Bench(edge - n * 1.2f + right * R(-30, 30), n);
                if (Chance(.5f)) for (float t = -40; t <= 40; t += 7) if (Chance(.6f)) Meter(edge - n * .45f + right * t);
                // Trash: bag piles against the curb or the buildings, litter along the gutter and sidewalk.
                int piles = Ri(0, 3);
                for (int k = 0; k < piles; k++) BagPile(edge - n * R(.9f, 1.3f) + right * R(-40, 40), Ri(3, 8));
                for (int k = 0; k < 18; k++) Litter(edge + n * R(.08f, .9f) + right * R(-BlockHalf, BlockHalf), true);
                for (int k = 0; k < 8; k++) Litter(edge - n * R(1, 5.5f) + right * R(-BlockHalf, BlockHalf), false);
                for (int k = 0; k < 4; k++) kit.Flat(m.stain, edge - n * R(1, 5) + right * R(-48, 48), new Vector2(R(.4f, 1.4f), R(.4f, 1.2f)), R(0, 180));
                ParkedCars(edge + n * 2.6f, n, right);
            }
        }

        static void StreetLamp(Vector3 p, Vector3 n)
        {
            kit.Cylinder(m.lampPole, p + Vector3.up * .3f, .16f, .6f, 8);
            kit.Cylinder(m.lampPole, p + Vector3.up * 4, .08f, 7.4f, 8);
            kit.Beam(m.lampPole, p + Vector3.up * 7.4f, p + Vector3.up * 7.7f + n * 2, .08f, .08f);
            kit.Box(m.lampPole, p + Vector3.up * 7.6f + n * 2.1f, new Vector3(.45f, .18f, .45f));
            kit.Box(m.lamp, p + Vector3.up * 7.48f + n * 2.1f, new Vector3(.36f, .06f, .36f), false, false);
        }
        static void StreetTree(Vector3 p)
        {
            kit.Flat(m.stain, p, new Vector2(1.4f, 1.4f), 0, .014f);
            float s = R(.65f, .85f);
            kit.Cylinder(m.bark, p + Vector3.up * 2 * s, .2f, 4 * s, 7);
            kit.Ellipsoid(m.foliage, p + Vector3.up * (4.6f * s + R(-.2f, .3f)), new Vector3(2.7f, 3.2f, 2.7f) * s * R(.9f, 1.1f));
        }
        static void Hydrant(Vector3 p)
        {
            kit.Cylinder(m.hydrant, p + Vector3.up * .32f, .14f, .64f, 10);
            kit.Cylinder(m.hydrant, p + Vector3.up * .68f, .11f, .12f, 10);
            kit.Cylinder(m.hydrant, p + Vector3.up * .74f, .05f, .08f, 6);
            kit.Cylinder(m.hydrant, p + Vector3.up * .45f, .055f, .4f, 6, false, true, Vector3.right);
            kit.Cylinder(m.hydrant, p + Vector3.up * .08f, .19f, .08f, 10);
        }
        static void Bin(Vector3 p)
        {
            kit.Cylinder(m.bin, p + Vector3.up * .45f, .32f, .9f, 10);
            kit.Cylinder(m.binLid, p + Vector3.up * .92f, .3f, .06f, 10);
            if (Chance(.5f)) kit.Sack(m.bagBlack, p + Vector3.up * .9f, .45f, R(0, 6));
        }
        static void NewsBoxes(Vector3 p, Vector3 n)
        {
            Vector3 right = Vector3.Cross(Vector3.up, n);
            var mats = new[] { m.newsBlue, m.newsRed, m.newsYellow, m.metalLight };
            int count = Ri(1, 4);
            for (int i = 0; i < count; i++)
            {
                Vector3 c = p + right * (i * .6f) + Vector3.up * .55f;
                kit.Box(Pick(mats), c, Abs(right * .5f) + Vector3.up * 1.1f + Abs(n) * .45f, false, false);
                kit.Box(m.glassDark, c + n * .23f + Vector3.up * .2f, Abs(right * .38f) + Vector3.up * .3f + Abs(n) * .02f, false, false);
            }
        }
        static void Mailbox(Vector3 p)
        {
            kit.Box(m.mailbox, p + Vector3.up * .65f, new Vector3(.5f, .9f, .55f));
            kit.Cylinder(m.mailbox, p + Vector3.up * 1.1f, .25f, .55f, 10, false, true, Vector3.forward);
            for (int k = -1; k <= 1; k += 2) kit.Box(m.mailbox, p + new Vector3(k * .2f, .1f, 0), new Vector3(.06f, .2f, .5f));
        }
        static void Bench(Vector3 p, Vector3 n)
        {
            Vector3 right = Vector3.Cross(Vector3.up, n);
            kit.Box(m.bench, p + Vector3.up * .45f, Abs(right * 1.8f) + Vector3.up * .06f + Abs(n) * .45f);
            kit.Box(m.bench, p + Vector3.up * .75f - n * .22f, Abs(right * 1.8f) + Vector3.up * .4f + Abs(n) * .05f);
            for (int k = -1; k <= 1; k += 2) kit.Box(m.metal, p + right * k * .8f + Vector3.up * .22f, Abs(right * .06f) + Vector3.up * .44f + Abs(n) * .45f);
        }
        static void BusStop(Vector3 p, Vector3 n)
        {
            Vector3 right = Vector3.Cross(Vector3.up, n);
            for (int k = -1; k <= 1; k += 2)
            {
                kit.Box(m.metal, p + right * k * 2 + Vector3.up * 1.3f - n * .7f, new Vector3(.08f, 2.6f, .08f));
                kit.Box(m.metal, p + right * k * 2 + Vector3.up * 1.3f + n * .7f, new Vector3(.08f, 2.6f, .08f));
            }
            kit.Box(m.metal, p + Vector3.up * 2.65f, Abs(right * 4.4f) + Vector3.up * .1f + Abs(n) * 1.8f);
            kit.Box(m.glass, p + Vector3.up * 1.4f - n * .75f, Abs(right * 3.9f) + Vector3.up * 2.1f + Abs(n) * .04f);
            kit.Box(m.adPanel, p + right * 2.05f + Vector3.up * 1.3f, Abs(right * .1f) + Vector3.up * 1.8f + Abs(n) * 1.2f, false, false);
            Bench(p - n * .3f, n);
        }
        static void Meter(Vector3 p)
        {
            kit.Cylinder(m.metal, p + Vector3.up * .6f, .04f, 1.2f, 6, false, false);
            kit.Box(m.metalLight, p + Vector3.up * 1.3f, new Vector3(.16f, .3f, .14f), false, false);
        }
        static void BagPile(Vector3 p, int count)
        {
            var mats = new[] { m.bagBlack, m.bagBlack, m.bagGreen, m.bagWhite };
            for (int i = 0; i < count; i++)
            {
                Vector2 o = Random2() * .9f;
                kit.Sack(Pick(mats), p + new Vector3(o.x, i > 3 ? .35f : 0, o.y), R(.55f, .85f), R(0, 9));
            }
            if (Chance(.5f)) kit.Box(m.cardboard, p + new Vector3(R(-1, 1), .25f, R(-1, 1)), new Vector3(R(.4f, .8f), R(.3f, .6f), R(.4f, .7f)), R(0, 90), false, false);
        }
        static Vector2 Random2() { float a = R(0, Mathf.PI * 2), r = Mathf.Sqrt(R(0, 1)); return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; }
        static void Litter(Vector3 p, bool gutter)
        {
            float y = gutter ? .0f : .24f;
            Vector3 at = new Vector3(p.x, y, p.z);
            float roll = R(0, 1);
            if (roll < .45f) kit.Flat(m.paper, at, new Vector2(R(.12f, .35f), R(.1f, .28f)), R(0, 180));
            else if (roll < .65f) kit.Cylinder(Chance(.5f) ? m.can : m.metalLight, at + Vector3.up * .035f, .033f, .12f, 6, false, false, new Vector3(R(-1, 1), 0, R(-1, 1)));
            else if (roll < .85f) kit.Flat(m.leaf, at, new Vector2(R(.3f, .7f), R(.2f, .5f)), R(0, 180));
            else kit.Box(m.cardboard, at + Vector3.up * .05f, new Vector3(R(.2f, .4f), .1f, R(.2f, .35f)), R(0, 180), false, false);
        }

        static void ParkedCars(Vector3 lane, Vector3 n, Vector3 right)
        {
            for (float t = -BlockHalf + 14; t <= BlockHalf - 14; t += R(6.2f, 9))
            {
                if (!Chance(.45f)) continue;
                Car(new Vector3(lane.x, 0, lane.z) + right * t, right, Pick(m.carPaint));
            }
        }
        // A parked car: body, cabin with glass, wheels, lights. Solid so runners and bots collide with it.
        public static void Car(Vector3 p, Vector3 along, Material paint, bool solid = true)
        {
            var rot = Quaternion.LookRotation(along);
            kit.Box(paint, p + Vector3.up * .62f, new Vector3(1.82f, .62f, 4.4f), rot);
            kit.Box(paint, p + Vector3.up * 1.12f - along * .25f, new Vector3(1.6f, .42f, 2.3f), rot);
            kit.Box(m.glassDark, p + Vector3.up * 1.12f - along * .25f, new Vector3(1.64f, .34f, 2.2f), rot);
            kit.Box(m.trimDark, p + Vector3.up * .45f + along * 2.18f, new Vector3(1.86f, .2f, .12f), rot);
            kit.Box(m.trimDark, p + Vector3.up * .45f - along * 2.18f, new Vector3(1.86f, .2f, .12f), rot);
            Vector3 side = Vector3.Cross(Vector3.up, along);
            for (int a = -1; a <= 1; a += 2)
                for (int s = -1; s <= 1; s += 2)
                    kit.Cylinder(m.tire, p + along * a * 1.35f + side * s * .82f + Vector3.up * .33f, .33f, .24f, 10, false, true, side);
            for (int s = -1; s <= 1; s += 2)
            {
                kit.Box(m.lamp, p + along * 2.21f + side * s * .62f + Vector3.up * .7f, new Vector3(.32f, .12f, .04f), rot, false, false);
                kit.Box(m.red, p - along * 2.21f + side * s * .66f + Vector3.up * .72f, new Vector3(.26f, .12f, .04f), rot, false, false);
            }
            if (solid) kit.Solid(p + Vector3.up * .75f, new Vector3(1.85f, 1.5f, 4.45f), rot);
        }

        static void AlleyDressing(Vector3 c)
        {
            kit.Box(m.asphalt, c + Vector3.up * .01f, new Vector3(2 * BlockHalf - 2, .02f, 2 * Alley - .4f));
            Alleys.Add(new CityPlace("alley", new Vector3(c.x + R(-25, 25), .24f, c.z), Vector3.right));
            for (int k = 0; k < 3; k++)
            {
                float x = c.x + R(-40, 40);
                int side = Chance(.5f) ? 1 : -1;
                Dumpster(new Vector3(x, .24f, c.z + side * (Alley - 1.1f)), Chance(.6f) ? m.dumpster : m.dumpsterBlue);
                BagPile(new Vector3(x + R(1.8f, 3.5f), .24f, c.z + side * (Alley - .9f)), Ri(3, 9));
            }
            for (int k = 0; k < 6; k++) kit.Flat(m.puddle, new Vector3(c.x + R(-45, 45), .25f, c.z + R(-2.5f, 2.5f)), new Vector2(R(.8f, 2.5f), R(.6f, 1.6f)), R(0, 180), .004f);
            for (int k = 0; k < 14; k++) Litter(new Vector3(c.x + R(-46, 46), 0, c.z + R(-3.5f, 3.5f)), false);
            for (int k = 0; k < 4; k++)
            {
                int side = Chance(.5f) ? 1 : -1;
                Vector3 wall = new Vector3(c.x + R(-40, 40), .24f + R(1.2f, 2.2f), c.z + side * (Alley - .02f));
                kit.Panel(Pick(m.graffiti), wall, new Vector3(0, 0, -side), R(1.5f, 4), R(.8f, 1.8f));
            }
            for (int k = 0; k < 2; k++) kit.Box(m.pallet, new Vector3(c.x + R(-44, 44), .3f, c.z + (Chance(.5f) ? 1 : -1) * (Alley - .6f)), new Vector3(1.2f, .14f, 1), R(-10, 10), false, false);
        }
        static void Dumpster(Vector3 p, Material paint)
        {
            kit.Box(paint, p + Vector3.up * .7f, new Vector3(2, 1.3f, 1.2f), true);
            kit.Box(m.trimDark, p + Vector3.up * 1.4f + Vector3.forward * .05f, new Vector3(2.04f, .08f, 1.3f), Quaternion.Euler(-6, 0, 0));
            for (int k = -1; k <= 1; k += 2) kit.Cylinder(m.tire, p + new Vector3(k * .8f, .1f, 0), .1f, .1f, 6, false, false, Vector3.forward);
        }

        // ---- Streets ------------------------------------------------------------------------------

        // Crosswalks, stop lines, traffic signals and manholes at an intersection.
        public static void Intersection(float x, float z)
        {
            Vector3 c = new Vector3(x, 0, z);
            for (int s = 0; s < 4; s++)
            {
                var rot = Quaternion.Euler(0, s * 90, 0);
                Vector3 n = rot * Vector3.forward, right = rot * Vector3.right;
                for (float t = -10.5f; t <= 10.5f; t += 1.5f)
                    kit.Flat(m.crosswalk, c + n * 15 + right * t, new Vector2(.75f, 4.2f), s * 90, .018f);
                kit.Flat(m.crosswalk, c + n * 17.8f + right * 6, new Vector2(11, .4f), s * 90, .018f);
                // Signal on the corner: pole, mast arm over the lanes, three-lamp head.
                Vector3 corner = c + n * (12 + 1.4f) + right * (12 + 1.4f);
                kit.Cylinder(m.metal, corner + Vector3.up * 3.5f, .12f, 7, 8);
                kit.Beam(m.metal, corner + Vector3.up * 6.6f, corner + Vector3.up * 6.6f - right * 8, .12f, .12f);
                Vector3 head = corner + Vector3.up * 5.9f - right * 6.5f;
                kit.Box(m.signalBody, head, new Vector3(.4f, 1.1f, .4f));
                kit.Box(s % 2 == 0 ? m.red : m.amber, head + n * .21f + Vector3.up * .32f, new Vector3(.24f, .24f, .04f), rot, false, false);
                kit.Box(m.amber, head + n * .21f, new Vector3(.24f, .24f, .04f), rot, false, false);
                kit.Box(s % 2 == 0 ? m.amber : m.green, head + n * .21f - Vector3.up * .32f, new Vector3(.24f, .24f, .04f), rot, false, false);
            }
            kit.Cylinder(m.manhole, c + new Vector3(R(-6, 6), .01f, R(-6, 6)), .38f, .02f, 12, false, false);
            Plazas.Add(new CityPlace("corner", c + new Vector3(15.5f, .24f, 15.5f), Vector3.forward));
        }
    }

    // City sign lettering baked into a few combined meshes (hundreds of TextMeshes would cost a draw call each).
    // The dynamic font can rebuild its atlas when other text asks for new glyphs; the meshes are rebuilt then.
    public sealed class CitySignFont : MonoBehaviour
    {
        const int Size = 64;
        static CitySignFont current;
        readonly List<(string text, Vector3 position, Vector3 facing, float height, Color color)> labels = new List<(string, Vector3, Vector3, float, Color)>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<GameObject> parts = new List<GameObject>();
        Font font; Material material;

        public static void Begin(Transform parent, Font f, Material mat)
        {
            current = parent.gameObject.AddComponent<CitySignFont>();
            current.font = f; current.material = mat;
            Font.textureRebuilt += current.Rebuilt;
        }
        public static void Label(string text, Vector3 position, Vector3 facing, float height, Color color)
            => current.labels.Add((text, position, facing, height, color));
        public static void End() => current.Generate();
        void Rebuilt(Font f) { if (f != font) return; material.mainTexture = font.material.mainTexture; if (labels.Count > 0) Generate(); }

        void Generate()
        {
            foreach (var go in parts) Destroy(go);
            foreach (var mesh in meshes) Destroy(mesh);
            parts.Clear(); meshes.Clear();
            var chars = new System.Text.StringBuilder();
            foreach (var l in labels) chars.Append(l.text);
            font.RequestCharactersInTexture(chars.ToString(), Size, FontStyle.Bold);
            material.mainTexture = font.material.mainTexture;
            var cells = new Dictionary<(int, int), (List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> t)>();
            foreach (var (text, position, facing, height, color) in labels)
            {
                var key = (Mathf.FloorToInt(position.x / 260), Mathf.FloorToInt(position.z / 260));
                if (!cells.TryGetValue(key, out var cell)) cells[key] = cell = (new List<Vector3>(), new List<Vector2>(), new List<Color>(), new List<int>());
                float scale = height / Size, width = 0;
                foreach (char ch in text) if (font.GetCharacterInfo(ch, out var info, Size, FontStyle.Bold)) width += info.advance;
                Vector3 right = Vector3.Cross(facing, Vector3.up).normalized;   // reads left to right facing the sign
                float pen = -width / 2;
                foreach (char ch in text)
                {
                    if (!font.GetCharacterInfo(ch, out var info, Size, FontStyle.Bold)) continue;
                    Vector3 Corner(float x, float y) => position + right * ((pen + x) * scale) + Vector3.up * ((y - Size * .36f) * scale);
                    int i = cell.v.Count;
                    cell.v.Add(Corner(info.minX, info.minY)); cell.v.Add(Corner(info.minX, info.maxY)); cell.v.Add(Corner(info.maxX, info.maxY)); cell.v.Add(Corner(info.maxX, info.minY));
                    cell.uv.Add(info.uvBottomLeft); cell.uv.Add(info.uvTopLeft); cell.uv.Add(info.uvTopRight); cell.uv.Add(info.uvBottomRight);
                    for (int k = 0; k < 4; k++) cell.c.Add(color);
                    cell.t.Add(i); cell.t.Add(i + 1); cell.t.Add(i + 2); cell.t.Add(i); cell.t.Add(i + 2); cell.t.Add(i + 3);
                    pen += info.advance;
                }
            }
            foreach (var cell in cells.Values)
            {
                var mesh = new Mesh { name = "Sign lettering", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(cell.v); mesh.SetUVs(0, cell.uv); mesh.SetColors(cell.c); mesh.SetTriangles(cell.t, 0);
                mesh.RecalculateBounds(); mesh.UploadMeshData(true);       // not readable: the district merge skips it
                var go = new GameObject("Sign lettering");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshes.Add(mesh); parts.Add(go);
            }
        }
        void OnDestroy()
        {
            Font.textureRebuilt -= Rebuilt;
            foreach (var mesh in meshes) Destroy(mesh);
            if (material != null && font != null && material != font.material) Destroy(material);
        }
    }
}
