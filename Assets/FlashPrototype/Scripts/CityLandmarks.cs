using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    // Walk-in landmarks built into the street grid:
    //   CCPD: the Central City Police Department, an Art Deco civic tower after Vancouver City Hall (the show's CCPD
    //         exterior): pale stone, vertical piers, stepped setbacks, a clock stage and flagpoles, low wings either
    //         side and a lawn. Inside: lobby, front desk and bullpen, and a ramp up to Barry's CSI lab on a mezzanine
    //         under a tall arched window.
    //   CC Jitters: the coffee shop one block away, across the avenue: a pale stone corner building with arched
    //         windows and a hanging cup sign; brick and timber inside, counter, espresso machine, pastry case, tables.
    //   Corner Mart: a convenience store (aisles, fridges, counter, ATM) where robberies happen.
    public static class CityLandmarks
    {
        // Block centres.
        public static readonly Vector2 CcpdBlock = new Vector2(-65, -195), JittersBlock = new Vector2(65, -195), MartBlock = new Vector2(-195, -195);
        const float Floor = .24f;
        static CityKit kit;
        static PrototypeWorld w;
        static Material stone, stoneTrim, interiorBrick, plaster, timber, tile, glass, deskWood, metal, chrome, screen, glowWarm, glowCool, lawn,
            dark, cream, orange, green, red, white, shelf, fridge, jittersStone, martBrick, evidence, flagBlue, flagRed, carpet;

        public static bool Claims(float cx, float cz) => Is(cx, cz, CcpdBlock) || Is(cx, cz, JittersBlock) || Is(cx, cz, MartBlock);
        static bool Is(float cx, float cz, Vector2 b) => Mathf.Abs(cx - b.x) < 1 && Mathf.Abs(cz - b.y) < 1;
        // Lots the landmark replaces; the block generator cuts its lots at Cut() so the landmark lot is exact.
        public static bool OnLot(float cx, float cz, int side, float x0, float x1)
        {
            if (Is(cx, cz, CcpdBlock)) return true;
            if (Is(cx, cz, JittersBlock)) return side == 1 && x0 < cx - 47 + 24 - .1f;
            if (Is(cx, cz, MartBlock)) return side == 1 && x1 > cx + 47 - 16 + .1f;
            return false;
        }
        public static bool HasAlley(float cx, float cz) => !Is(cx, cz, CcpdBlock);
        public static float? Cut(float cx, float cz, int side)
        {
            if (Is(cx, cz, JittersBlock) && side == 1) return cx - 47 + 24;
            if (Is(cx, cz, MartBlock) && side == 1) return cx + 47 - 16;
            return null;
        }

        static Material M(string name, Color c, float glow = 0, float surface = 0, float style = 0)
        {
            var m = w.Material(name, c, glow); m.SetFloat("_Surface", surface); m.SetFloat("_WindowStyle", style); return m;
        }
        static void Materials()
        {
            if (stone != null) return;
            stone = M("CCPD limestone", new Color(.78f, .76f, .69f), 0, 9, 1);
            stone.SetVector("_Facade", new Vector4(3, 3.8f, 1.5f / 3, .64f)); stone.SetVector("_Facade2", new Vector4(7.24f, .1f, .25f, 0));
            stoneTrim = M("CCPD stone trim", new Color(.84f, .82f, .75f));
            jittersStone = M("Jitters stone", new Color(.76f, .72f, .62f), 0, 9, 1);
            jittersStone.SetVector("_Facade", new Vector4(3, 3.4f, 1.4f / 3, .6f)); jittersStone.SetVector("_Facade2", new Vector4(4.64f, .15f, .4f, 0));
            martBrick = M("Mart brick", new Color(.44f, .22f, .16f), 0, 9, 0);
            martBrick.SetVector("_Facade", new Vector4(2.8f, 3.4f, 1.3f / 2.8f, .58f)); martBrick.SetVector("_Facade2", new Vector4(4.64f, .12f, .5f, 0));
            interiorBrick = M("Interior brick", new Color(.5f, .26f, .18f), 0, 9, 0);
            interiorBrick.SetVector("_Facade2", new Vector4(1000, 0, 0, 0));
            plaster = M("Interior plaster", new Color(.78f, .76f, .72f));
            timber = M("Timber floor", new Color(.42f, .28f, .17f), 0, 4, .22f);
            tile = M("Vinyl floor tile", new Color(.7f, .7f, .68f), 0, 4, .6f);
            carpet = M("Office carpet", new Color(.25f, .27f, .3f), 0, 4, 1.2f);
            glass = M("Landmark glass", new Color(.22f, .3f, .34f));
            deskWood = M("Desk wood", new Color(.36f, .25f, .16f));
            metal = M("Landmark metal", new Color(.2f, .21f, .23f));
            chrome = M("Landmark chrome", new Color(.72f, .73f, .75f));
            screen = M("Monitor screen", new Color(.35f, .6f, .9f), .9f);
            glowWarm = M("Warm lamp", new Color(1, .82f, .55f), 1);
            glowCool = M("Ceiling panel", new Color(.92f, .95f, 1), 1);
            lawn = M("CCPD lawn", new Color(.2f, .33f, .15f));
            dark = M("Dark panel", new Color(.07f, .07f, .08f));
            cream = M("Cream", new Color(.9f, .85f, .74f));
            orange = M("Jitters orange", new Color(.88f, .42f, .1f));
            green = M("Jitters green", new Color(.1f, .22f, .16f));
            red = M("Signal red glow", new Color(1, .15f, .1f), 1);
            white = M("White", new Color(.92f, .92f, .9f));
            shelf = M("Shelving", new Color(.75f, .76f, .78f));
            fridge = M("Fridge glow", new Color(.75f, .9f, 1), .75f);
            evidence = M("Evidence board", new Color(.62f, .5f, .35f));
            flagBlue = M("Flag blue", new Color(.12f, .2f, .5f));
            flagRed = M("Flag red", new Color(.6f, .1f, .1f));
        }

        public static void Build(CityKit k, PrototypeWorld world, float cx, float cz)
        {
            kit = k; w = world; Materials();
            if (Is(cx, cz, CcpdBlock)) Ccpd(new Vector3(cx, Floor, cz));
            if (Is(cx, cz, JittersBlock)) Jitters(new Vector3(cx, Floor, cz));
            if (Is(cx, cz, MartBlock)) Mart(new Vector3(cx, Floor, cz));
        }

        // ---- Shared pieces ----------------------------------------------------------------------

        // Wall along one side of a room with openings [from, to] (doors are walk-through; windows get glass).
        struct Opening { public float from, to, bottom, top; public bool door; }
        static Opening Door(float from, float to, float top = 2.7f) => new Opening { from = from, to = to, bottom = 0, top = top, door = true };
        static Opening Window(float from, float to, float bottom, float top) => new Opening { from = from, to = to, bottom = bottom, top = top };
        static void Wall(Material m, Vector3 a, Vector3 b, float height, float thick, params Opening[] openings)
        {
            Vector3 dir = (b - a).normalized; float length = (b - a).magnitude;
            Vector3 n = Vector3.Cross(dir, Vector3.up);
            var sorted = new List<Opening>(openings); sorted.Sort((p, q) => p.from.CompareTo(q.from));
            float at = 0;
            void Segment(float s0, float s1, float y0, float y1, Material mat, bool solid)
            {
                if (s1 - s0 < .01f || y1 - y0 < .01f) return;
                Vector3 c = a + dir * ((s0 + s1) / 2) + Vector3.up * ((y0 + y1) / 2);
                kit.Box(mat, c, Abs(dir * (s1 - s0)) + Vector3.up * (y1 - y0) + Abs(n) * thick, solid);
            }
            foreach (var o in sorted)
            {
                Segment(at, o.from, 0, height, m, true);
                Segment(o.from, o.to, o.top, height, m, true);
                if (!o.door)
                {
                    Segment(o.from, o.to, 0, o.bottom, m, true);
                    Segment(o.from, o.to, o.bottom, o.top, glass, true);
                }
                at = o.to;
            }
            Segment(at, length, 0, height, m, true);
        }
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        static void Desk(Vector3 p, float yaw, bool monitor = true)
        {
            var r = Quaternion.Euler(0, yaw, 0);
            kit.Box(deskWood, p + Vector3.up * .74f, new Vector3(1.6f, .05f, .8f), r, true);
            kit.Box(metal, p + Vector3.up * .37f + r * new Vector3(-.75f, 0, 0), new Vector3(.05f, .72f, .75f), r);
            kit.Box(metal, p + Vector3.up * .37f + r * new Vector3(.75f, 0, 0), new Vector3(.05f, .72f, .75f), r);
            if (monitor)
            {
                kit.Box(dark, p + Vector3.up * 1.03f + r * new Vector3(0, 0, .2f), new Vector3(.62f, .4f, .04f), r);
                kit.Box(screen, p + Vector3.up * 1.03f + r * new Vector3(0, 0, .175f), new Vector3(.56f, .34f, .01f), r, false, false);
                kit.Box(dark, p + Vector3.up * .78f + r * new Vector3(0, 0, -.1f), new Vector3(.45f, .02f, .16f), r, false, false);
            }
            Chair(p + r * new Vector3(0, 0, -.75f), yaw);
        }
        static void Chair(Vector3 p, float yaw)
        {
            var r = Quaternion.Euler(0, yaw, 0);
            kit.Box(dark, p + Vector3.up * .46f, new Vector3(.48f, .07f, .46f), r);
            kit.Box(dark, p + Vector3.up * .78f + r * new Vector3(0, 0, -.22f), new Vector3(.46f, .6f, .06f), r);
            kit.Cylinder(metal, p + Vector3.up * .22f, .04f, .44f, 6);
        }
        static void Pendant(Vector3 p, float drop)
        {
            kit.Beam(metal, p, p - Vector3.up * drop, .02f, .02f, false);
            kit.Cylinder(glowWarm, p - Vector3.up * (drop + .1f), .16f, .2f, 8, false, false);
        }

        // ---- CCPD -------------------------------------------------------------------------------

        static void Ccpd(Vector3 c)
        {
            // Lawn and forecourt on the avenue (east) side; paths to the doors.
            kit.Box(lawn, c + new Vector3(30, .03f, 0), new Vector3(42, .06f, 104));
            kit.Box(stoneTrim, c + new Vector3(37, .035f, 0), new Vector3(30, .07f, 10));
            for (int s = -1; s <= 1; s += 2) kit.Box(stoneTrim, c + new Vector3(30, .035f, s * 30), new Vector3(42, .07f, 4));
            // Wings (north and south): four storeys, piers on the front.
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 wing = c + new Vector3(-4, 0, s * 29);
                kit.Box(stone, wing + Vector3.up * 8.6f, new Vector3(18, 17.2f, 32), true);
                Piers(wing + new Vector3(9, 0, 0), 32, 17.2f);
                kit.Box(stoneTrim, wing + Vector3.up * 17.5f, new Vector3(19, .6f, 33));
            }
            // Tower: hollow two-level ground storey, then floors with stepped setbacks and the clock stage.
            Vector3 t = c + new Vector3(-4, 0, 0);
            const float hx = 11, hz = 13, lobbyH = 7.2f;
            float x0 = t.x - hx, x1 = t.x + hx, z0 = t.z - hz, z1 = t.z + hz;
            Wall(stone, new Vector3(x1, Floor, z0), new Vector3(x1, Floor, z1), lobbyH, .6f, Door(9, 17, 4.6f), Window(2, 7, 1.2f, 5.6f), Window(19, 24, 1.2f, 5.6f));
            Wall(stone, new Vector3(x0, Floor, z0), new Vector3(x1, Floor, z0), lobbyH, .6f);
            Wall(stone, new Vector3(x0, Floor, z1), new Vector3(x1, Floor, z1), lobbyH, .6f);
            // Back (west) wall: the lab's tall arched window above the mezzanine.
            Wall(stone, new Vector3(x0, Floor, z0), new Vector3(x0, Floor, z1), lobbyH, .6f, Window(9.5f, 16.5f, 4.6f, 6.9f));
            kit.Cylinder(glass, new Vector3(x0, Floor + 6.9f, t.z), 3.5f, .7f, 16, false, true, Vector3.right);
            float floors = Floor + lobbyH;
            kit.Box(stone, new Vector3(t.x, floors + 15.4f, t.z), new Vector3(2 * hx, 30.8f, 2 * hz), true);
            kit.Box(stone, new Vector3(t.x, floors + 30.8f + 3.8f, t.z), new Vector3(2 * hx - 5, 7.6f, 2 * hz - 5), true);
            kit.Box(stone, new Vector3(t.x, floors + 38.4f + 3.8f, t.z), new Vector3(2 * hx - 10, 7.6f, 2 * hz - 10), true);
            float stage = floors + 46;
            kit.Box(stoneTrim, new Vector3(t.x, stage + 3.2f, t.z), new Vector3(9, 6.4f, 9), true);
            kit.Box(stoneTrim, new Vector3(t.x, stage + 6.7f, t.z), new Vector3(7, .6f, 7));
            kit.Cylinder(metal, new Vector3(t.x, stage + 11.5f, t.z), .1f, 9, 6);
            kit.Box(flagBlue, new Vector3(t.x + .9f, stage + 15, t.z), new Vector3(1.8f, 1.1f, .03f), false, false);
            for (int f = 0; f < 4; f++)
            {
                var rot = Quaternion.Euler(0, f * 90, 0);
                Vector3 n = rot * Vector3.forward;
                Vector3 face = new Vector3(t.x, stage + 3.4f, t.z) + n * 4.55f;
                kit.Cylinder(cream, face, 1.9f, .12f, 24, false, true, n);
                kit.Cylinder(dark, face + n * .07f, .1f, .04f, 8, false, false, n);
                kit.Beam(dark, face + n * .08f, face + n * .08f + Vector3.up * 1.4f, .08f, .02f, false);
                kit.Beam(dark, face + n * .08f, face + n * .08f + (rot * Vector3.right) * 1.0f, .08f, .02f, false);
            }
            Piers(new Vector3(x1, Floor, t.z), 2 * hz, lobbyH + 30.8f, t.z - 5, t.z + 5, 12.4f);
            Piers(new Vector3(x1 - 2.5f, Floor + lobbyH + 30.8f, t.z), 2 * hz - 5, 7.6f);
            // Entrance portal with the department name.
            for (int s = -1; s <= 1; s += 2) kit.Box(stoneTrim, new Vector3(x1 + .6f, Floor + 5.5f, t.z + s * 4.6f), new Vector3(1.4f, 11, 1.6f), true);
            kit.Box(stoneTrim, new Vector3(x1 + .6f, Floor + 11.6f, t.z), new Vector3(1.4f, 1.4f, 10.8f));
            kit.Box(dark, new Vector3(x1 + .35f, Floor + 7.6f, t.z), new Vector3(.2f, 2.6f, 7.6f));
            CitySignFont.Label("CENTRAL CITY", new Vector3(x1 + 1.32f, Floor + 12.0f, t.z), Vector3.right, .75f, new Color(.25f, .22f, .18f));
            CitySignFont.Label("POLICE DEPARTMENT", new Vector3(x1 + 1.32f, Floor + 11.2f, t.z), Vector3.right, .6f, new Color(.25f, .22f, .18f));
            for (int s = -1; s <= 1; s += 2)
            {
                kit.Cylinder(metal, new Vector3(x1 + 12, Floor + 6, t.z + s * 7), .08f, 12, 6);
                kit.Box(s < 0 ? flagBlue : flagRed, new Vector3(x1 + 12, Floor + 11, t.z + s * 7 + 1), new Vector3(.03f, 1.2f, 2), false, false);
            }
            kit.Cylinder(metal, new Vector3(x1 + 14, Floor + 7, t.z), .09f, 14, 6);
            kit.Box(flagBlue, new Vector3(x1 + 14, Floor + 13, t.z + 1.2f), new Vector3(.03f, 1.4f, 2.4f), false, false);
            // Interior: floor, ceiling panels, front desk, bullpen.
            kit.Box(tile, new Vector3(t.x, Floor + .01f, t.z), new Vector3(2 * hx - 1.2f, .02f, 2 * hz - 1.2f));
            for (float x = x0 + 3; x < x1 - 2; x += 5) for (float z = z0 + 3; z < z1 - 2; z += 5)
                    kit.Box(glowCool, new Vector3(x, Floor + lobbyH - .05f, z), new Vector3(1.2f, .05f, 1.2f), false, false);
            kit.Box(deskWood, new Vector3(x1 - 7, Floor + .55f, t.z), new Vector3(1.2f, 1.1f, 7), true);
            kit.Box(stoneTrim, new Vector3(x1 - 7, Floor + 1.12f, t.z), new Vector3(1.4f, .06f, 7.2f));
            CitySignFont.Label("C C P D", new Vector3(x0 + .33f, Floor + 2.5f, t.z), Vector3.right, .8f, new Color(.85f, .75f, .4f));
            kit.Box(dark, new Vector3(x0 + .31f, Floor + 2.5f, t.z), new Vector3(.02f, 1.2f, 6));
            for (int row = 0; row < 3; row++)
                for (int col = -1; col <= 1; col += 2)
                    Desk(new Vector3(x1 - 12.5f - row * 2.6f, Floor, t.z + col * 5.5f), col > 0 ? 180 : 0);
            for (int i = 0; i < 4; i++) kit.Box(metal, new Vector3(x0 + 1, Floor + .7f, z0 + 1.5f + i * .9f), new Vector3(.6f, 1.4f, .8f), true);
            // Mezzanine with Barry's lab, reached by a ramp along the north wall.
            float mez = Floor + 3.8f;
            kit.Box(plaster, new Vector3(x0 + 4.3f, mez - .15f, t.z), new Vector3(7.4f, .3f, 2 * hz - 1.2f), true);
            kit.Box(chrome, new Vector3(x0 + 8.05f, mez + .55f, t.z - 1), new Vector3(.06f, 1.1f, 2 * hz - 5), true);
            float rampStart = x0 + 20, rampEnd = x0 + 8, rampZ = z1 - 1.6f;
            float rise = mez - Floor, run = rampStart - rampEnd;
            var slope = Quaternion.Euler(0, 0, -Mathf.Atan2(rise, run) * Mathf.Rad2Deg);
            kit.Solid(new Vector3((rampStart + rampEnd) / 2, Floor + rise / 2 - .1f, rampZ), new Vector3(Mathf.Sqrt(rise * rise + run * run), .2f, 2.2f), slope);
            int steps = 22;
            for (int i = 0; i < steps; i++)
            {
                float k = (i + .5f) / steps;
                kit.Box(stoneTrim, new Vector3(rampStart - run * k, Floor + rise * k - .08f, rampZ), new Vector3(run / steps + .02f, rise / steps + .16f, 2.2f));
            }
            kit.Box(chrome, new Vector3((rampStart + rampEnd) / 2, Floor + rise / 2 + .9f, rampZ - 1.15f), new Vector3(run, .05f, .05f), Quaternion.Euler(0, 0, -Mathf.Atan2(rise, run) * Mathf.Rad2Deg));
            // The lab: benches, microscope, centrifuge, glassware, monitors, evidence board.
            float lx = x0 + 2.6f;
            kit.Box(white, new Vector3(lx, mez + .48f, t.z), new Vector3(1.1f, .96f, 8), true);
            kit.Box(dark, new Vector3(lx, mez + .98f, t.z), new Vector3(1.2f, .05f, 8.2f));
            kit.Box(white, new Vector3(lx + 2.5f, mez + .48f, t.z - 6), new Vector3(3.5f, .96f, 1.1f), true);
            kit.Cylinder(dark, new Vector3(lx, mez + 1.15f, t.z - 2), .1f, .3f, 8);
            kit.Beam(dark, new Vector3(lx, mez + 1.3f, t.z - 2), new Vector3(lx + .1f, mez + 1.55f, t.z - 2), .06f, .06f);
            kit.Cylinder(chrome, new Vector3(lx, mez + 1.12f, t.z + 1.2f), .22f, .26f, 12);
            for (int i = 0; i < 7; i++)
                kit.Cylinder(i % 2 == 0 ? glowCool : glass, new Vector3(lx + R(-.3f, .3f, i), mez + 1.1f, t.z + 2.2f + i * .3f), .045f, .22f, 6, false, false);
            for (int i = -1; i <= 1; i++)
            {
                kit.Box(dark, new Vector3(lx - .2f, mez + 1.35f, t.z + 3.4f + i * .7f), new Vector3(.04f, .42f, .62f));
                kit.Box(screen, new Vector3(lx - .17f, mez + 1.35f, t.z + 3.4f + i * .7f), new Vector3(.02f, .36f, .56f), false, false);
            }
            kit.Box(evidence, new Vector3(x0 + .35f, mez + 1.8f, t.z - 4.6f), new Vector3(.05f, 1.4f, 2.6f));
            for (int i = 0; i < 9; i++)
                kit.Box(i % 3 == 0 ? red : white, new Vector3(x0 + .4f, mez + 1.35f + (i / 3) * .4f, t.z - 5.6f + (i % 3) * .8f + R(-.1f, .1f, i)), new Vector3(.02f, .22f, .3f), false, false);
            Pendant(new Vector3(lx + 1.5f, Floor + lobbyH - .1f, t.z - 2), 1.3f);
            Pendant(new Vector3(lx + 1.5f, Floor + lobbyH - .1f, t.z + 2), 1.3f);
            // Police parking behind the building: bays and cruisers.
            kit.Box(CityBlocks.AsphaltMaterial, c + new Vector3(-34, .02f, 0), new Vector3(36, .04f, 100));
            for (int i = -5; i <= 5; i++)
            {
                kit.Box(white, c + new Vector3(-40, .05f, i * 6 + 3), new Vector3(6, .02f, .14f), false, false);
                if (i < 5 && (i + 7) % 3 != 0) CityBlocks.PoliceCar(c + new Vector3(-40, 0, i * 6 + 6), Vector3.right);
            }
            CityBlocks.Ccpd = new CityPlace("CCPD", new Vector3(x1 + 4, Floor, t.z), Vector3.right);
            w.Labels.Add(new WorldLabel(new Vector3(x1 + 6, 24, t.z), "C.C.P.D.\nCentral City Police Department"));
        }
        static float R(float a, float b, int seed) => a + (b - a) * Mathf.Repeat(Mathf.Sin(seed * 12.9898f) * 43758.5453f, 1);
        // Art Deco piers: shallow vertical fins every 3 m on a front face (window bays sit between them).
        static void Piers(Vector3 face, float width, float height, float skipFrom = 1, float skipTo = 0, float skipUpTo = 0)
        {
            for (float z = Mathf.Ceil((face.z - width / 2 + .3f) / 3) * 3; z <= face.z + width / 2 - .3f; z += 3)
            {
                float bottom = z > skipFrom && z < skipTo ? skipUpTo : 0;      // clear of the entrance portal
                kit.Box(stoneTrim, new Vector3(face.x + .25f, face.y + (bottom + height) / 2, z), new Vector3(.5f, height - bottom, .45f));
            }
        }

        // ---- CC Jitters ---------------------------------------------------------------------------

        static void Jitters(Vector3 c)
        {
            // North-west corner lot: x from c.x-47 (the avenue) 24 m east; z from c.z+4 to c.z+47 (the street).
            float x0 = c.x - 47, x1 = x0 + 24, zf = c.z + 47, zb = zf - 16, zr = c.z + 4;
            const float room = 4.4f, upper = 3 * 3.4f;
            // Ground storey: café at the front, solid behind it; three storeys above.
            Wall(jittersStone, new Vector3(x0, Floor, zf), new Vector3(x1, Floor, zf), room, .5f,
                Window(2, 5, .7f, 3.4f), Window(7, 10, .7f, 3.4f), Window(14, 17, .7f, 3.4f), Window(19, 22, .7f, 3.4f));
            Wall(jittersStone, new Vector3(x0, Floor, zb), new Vector3(x0, Floor, zf), room, .5f,
                Window(2, 5, .7f, 3.4f), Window(7, 10, .7f, 3.4f), Door(12.6f, 14.4f, 2.8f));
            Wall(interiorBrick, new Vector3(x1, Floor, zb), new Vector3(x1, Floor, zf), room, .5f);
            Wall(interiorBrick, new Vector3(x0, Floor, zb), new Vector3(x1, Floor, zb), room, .5f);
            kit.Box(jittersStone, new Vector3((x0 + x1) / 2, Floor + room / 2, (zb + zr) / 2), new Vector3(24, room, zb - zr), true);
            kit.Box(jittersStone, new Vector3((x0 + x1) / 2, Floor + room + upper / 2, (zf + zr) / 2), new Vector3(24, upper, zf - zr), true);
            kit.Box(stoneTrim, new Vector3((x0 + x1) / 2, Floor + room + upper + .3f, (zf + zr) / 2), new Vector3(24.6f, .6f, zf - zr + .6f));
            // Arched heads over the windows.
            foreach (float s in new[] { 3.5f, 8.5f, 15.5f, 20.5f })
                kit.Cylinder(glass, new Vector3(x0 + s, Floor + 3.4f, zf), 1.5f, .26f, 14, false, true, Vector3.forward);
            foreach (float s in new[] { 3.5f, 8.5f })
                kit.Cylinder(glass, new Vector3(x0, Floor + 3.4f, zb + s), 1.5f, .26f, 14, false, true, Vector3.right);
            // Sign band, awning and the hanging cup sign on the corner.
            kit.Box(green, new Vector3((x0 + x1) / 2, Floor + 4.2f, zf + .35f), new Vector3(22, .55f, .2f));
            CitySignFont.Label("CC JITTERS", new Vector3((x0 + x1) / 2, Floor + 4.2f, zf + .47f), Vector3.forward, .42f, new Color(.95f, .78f, .42f));
            kit.Box(green, new Vector3(x0 - .35f, Floor + 4.2f, zf - 8), new Vector3(.2f, .55f, 14));
            CitySignFont.Label("COFFEE  •  PASTRIES", new Vector3(x0 - .47f, Floor + 4.2f, zf - 8), Vector3.left, .32f, new Color(.95f, .78f, .42f));
            kit.Beam(metal, new Vector3(x0 - .2f, Floor + 5.6f, zf + .2f), new Vector3(x0 - 1.6f, Floor + 5.6f, zf + 1.6f), .06f, .06f);
            Vector3 sign = new Vector3(x0 - 1.2f, Floor + 4.7f, zf + 1.2f);
            var diag = Quaternion.Euler(0, -45, 0);
            kit.Box(cream, sign, new Vector3(1.5f, 1.5f, .1f), diag);
            Vector3 n = diag * Vector3.forward;
            for (int f = -1; f <= 1; f += 2)
            {
                kit.Cylinder(orange, sign + n * f * .07f, .58f, .02f, 20, false, false, n);
                kit.Cylinder(white, sign + n * f * .09f - Vector3.up * .05f, .2f, .02f, 12, false, false, n);
                kit.Box(white, sign + n * f * .09f + diag * new Vector3(.22f, -.02f, 0), new Vector3(.1f, .16f, .02f), diag, false, false);
                CitySignFont.Label("CC JITTERS", sign + n * f * .1f - Vector3.up * .52f, n * f, .17f, new Color(.2f, .12f, .05f));
            }
            // Interior: timber floor, brick, counter with espresso machine and pastry case, menu boards, tables.
            kit.Box(timber, new Vector3((x0 + x1) / 2, Floor + .01f, (zf + zb) / 2), new Vector3(23, .02f, 15));
            kit.Box(plaster, new Vector3((x0 + x1) / 2, Floor + room - .05f, (zf + zb) / 2), new Vector3(23, .06f, 15));
            float cz = zb + 1.6f;
            kit.Box(deskWood, new Vector3(x0 + 13, Floor + .55f, cz), new Vector3(12, 1.1f, .9f), true);
            kit.Box(dark, new Vector3(x0 + 13, Floor + 1.12f, cz), new Vector3(12.2f, .06f, 1.0f));
            kit.Box(chrome, new Vector3(x0 + 10, Floor + 1.45f, cz + .1f), new Vector3(1.4f, .6f, .6f));
            kit.Box(dark, new Vector3(x0 + 10, Floor + 1.25f, cz - .25f), new Vector3(1.1f, .1f, .1f));
            for (int i = 0; i < 3; i++) kit.Cylinder(dark, new Vector3(x0 + 9.6f + i * .4f, Floor + 1.24f, cz - .32f), .03f, .14f, 6, false, false);
            kit.Box(glass, new Vector3(x0 + 15.5f, Floor + 1.45f, cz - .05f), new Vector3(2.4f, .7f, .7f));
            for (int i = 0; i < 8; i++)
                kit.Box(i % 2 == 0 ? orange : cream, new Vector3(x0 + 14.6f + (i % 4) * .6f, Floor + 1.22f + (i / 4) * .3f, cz - .05f), new Vector3(.3f, .1f, .3f), false, false);
            kit.Box(dark, new Vector3(x0 + 18.3f, Floor + 1.3f, cz), new Vector3(.5f, .35f, .4f));
            string[] menu = { "ESPRESSO  2.50", "LATTE  4.25", "FLASHPRESSO  5.00", "COLD BREW  4.00", "CRONUT  3.75" };
            for (int i = 0; i < 3; i++)
            {
                Vector3 board = new Vector3(x0 + 8 + i * 5, Floor + 3.1f, zb + .3f);
                kit.Box(dark, board, new Vector3(4.2f, 1.6f, .05f));
                CitySignFont.Label(i == 1 ? "CC JITTERS" : "MENU", board + Vector3.forward * .04f + Vector3.up * .5f, Vector3.forward, .26f, new Color(.95f, .9f, .8f));
                CitySignFont.Label(menu[i], board + Vector3.forward * .04f, Vector3.forward, .2f, new Color(.95f, .9f, .8f));
                CitySignFont.Label(menu[(i + 3) % menu.Length], board + Vector3.forward * .04f - Vector3.up * .35f, Vector3.forward, .2f, new Color(.95f, .9f, .8f));
            }
            for (int tx = 0; tx < 3; tx++)
                for (int tz = 0; tz < 2; tz++)
                {
                    Vector3 table = new Vector3(x0 + 4 + tx * 6, Floor, zf - 3.5f - tz * 4.5f);
                    kit.Cylinder(deskWood, table + Vector3.up * .74f, .45f, .05f, 14, true);
                    kit.Cylinder(metal, table + Vector3.up * .37f, .05f, .74f, 6);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * 120 + tx * 30;
                        Vector3 chair = table + Quaternion.Euler(0, a, 0) * Vector3.forward * .85f;
                        kit.Box(deskWood, chair + Vector3.up * .45f, new Vector3(.42f, .05f, .42f), a);
                        kit.Box(deskWood, chair + Vector3.up * .72f + Quaternion.Euler(0, a, 0) * Vector3.forward * .2f, new Vector3(.42f, .5f, .05f), a);
                        kit.Cylinder(metal, chair + Vector3.up * .22f, .03f, .44f, 6);
                    }
                    Pendant(new Vector3(table.x, Floor + room - .06f, table.z), 1.4f);
                }
            CityBlocks.Jitters = new CityPlace("CC Jitters", new Vector3(x0 - 2, Floor, zf - 13.5f), Vector3.left);
            w.Labels.Add(new WorldLabel(new Vector3(x0 - 2, 8, zf), "CC JITTERS"));
        }

        // ---- Corner Mart ---------------------------------------------------------------------------

        static void Mart(Vector3 c)
        {
            // North-east corner lot: x from c.x+31 to c.x+47 (the avenue), z from c.z+4 to c.z+47 (the street).
            float x1 = c.x + 47, x0 = x1 - 16, zf = c.z + 47, zb = zf - 20, zr = c.z + 4;
            const float room = 4.4f, upper = 4 * 3.4f;
            Wall(martBrick, new Vector3(x0, Floor, zf), new Vector3(x1, Floor, zf), room, .45f, Window(1.5f, 8, .9f, 3.4f), Door(9.5f, 11.5f, 2.8f), Window(12.5f, 15, .9f, 3.4f));
            Wall(martBrick, new Vector3(x1, Floor, zb), new Vector3(x1, Floor, zf), room, .45f, Window(3, 17, .9f, 3.4f));
            Wall(interiorBrick, new Vector3(x0, Floor, zb), new Vector3(x0, Floor, zf), room, .45f);
            Wall(interiorBrick, new Vector3(x0, Floor, zb), new Vector3(x1, Floor, zb), room, .45f);
            kit.Box(martBrick, new Vector3((x0 + x1) / 2, Floor + room / 2, (zb + zr) / 2), new Vector3(16, room, zb - zr), true);
            kit.Box(martBrick, new Vector3((x0 + x1) / 2, Floor + room + upper / 2, (zf + zr) / 2), new Vector3(16, upper, zf - zr), true);
            kit.Box(stoneTrim, new Vector3((x0 + x1) / 2, Floor + room + upper + .3f, (zf + zr) / 2), new Vector3(16.6f, .6f, zf - zr + .6f));
            kit.Box(red, new Vector3((x0 + x1) / 2, Floor + 4.1f, zf + .32f), new Vector3(15, .6f, .2f));
            CitySignFont.Label("CORNER MART  •  24 HRS", new Vector3((x0 + x1) / 2, Floor + 4.1f, zf + .44f), Vector3.forward, .38f, Color.white);
            kit.Box(red, new Vector3(x1 + .32f, Floor + 4.1f, (zb + zf) / 2), new Vector3(.2f, .6f, 15));
            CitySignFont.Label("CORNER MART", new Vector3(x1 + .44f, Floor + 4.1f, (zb + zf) / 2), Vector3.right, .38f, Color.white);
            CitySignFont.Label("OPEN", new Vector3(x0 + 4.5f, Floor + 2.6f, zf + .27f), Vector3.forward, .35f, new Color(1, .2f, .15f));
            // Interior: floor, ceiling, lights, aisles of stocked shelves, fridges, counter, ATM.
            kit.Box(tile, new Vector3((x0 + x1) / 2, Floor + .01f, (zf + zb) / 2), new Vector3(15, .02f, 19));
            kit.Box(plaster, new Vector3((x0 + x1) / 2, Floor + room - .03f, (zf + zb) / 2), new Vector3(15, .06f, 19));
            for (float x = x0 + 3; x < x1 - 1; x += 4) for (float z = zb + 3; z < zf - 1; z += 4)
                    kit.Box(glowCool, new Vector3(x, Floor + room - .05f, z), new Vector3(2.4f, .05f, .4f), false, false);
            var products = new[] { orange, red, flagBlue, green, cream, white };
            for (int a = 0; a < 3; a++)
            {
                float ax = x0 + 3.5f + a * 3.4f;
                kit.Box(shelf, new Vector3(ax, Floor + .8f, zb + 8), new Vector3(.9f, 1.6f, 9), true);
                for (int side = -1; side <= 1; side += 2)
                    for (int level = 0; level < 3; level++)
                        for (int i = 0; i < 12; i++)
                            kit.Box(products[(a * 7 + level * 3 + i) % products.Length], new Vector3(ax + side * .38f, Floor + .45f + level * .45f, zb + 4 + i * .72f),
                                new Vector3(.18f, .3f, .55f), false, false);
            }
            for (int i = 0; i < 6; i++)
            {
                kit.Box(fridge, new Vector3(x0 + 1.5f + i * 2.2f, Floor + 1.1f, zb + .55f), new Vector3(2.1f, 2.2f, .7f), true);
                kit.Box(metal, new Vector3(x0 + 1.5f + i * 2.2f, Floor + 1.1f, zb + .92f), new Vector3(.06f, 2.2f, .04f));
            }
            Vector3 counter = new Vector3(x1 - 3, Floor, zf - 3.5f);
            kit.Box(deskWood, counter + Vector3.up * .55f, new Vector3(1, 1.1f, 4.5f), true);
            kit.Box(dark, counter + Vector3.up * 1.22f + Vector3.forward * .5f, new Vector3(.45f, .25f, .4f));
            kit.Box(screen, counter + Vector3.up * 1.42f + Vector3.forward * .5f, new Vector3(.3f, .2f, .02f), false, false);
            kit.Box(shelf, counter + new Vector3(1.5f, 1.2f, 0), new Vector3(.4f, 2.4f, 3.5f), true);
            kit.Box(metal, new Vector3(x0 + .7f, Floor + .9f, zf - 2.5f), new Vector3(.8f, 1.8f, .9f), true);
            kit.Box(screen, new Vector3(x0 + 1.12f, Floor + 1.3f, zf - 2.5f), new Vector3(.02f, .3f, .4f), false, false);
            CityBlocks.CornerMart = new CityPlace("Corner Mart", new Vector3(x0 + 10.5f, Floor, zf + 2), Vector3.forward);
            MartInterior = new Bounds(new Vector3((x0 + x1) / 2, Floor + 1, (zf + zb) / 2), new Vector3(15, 2, 19));
        }
        public static Bounds MartInterior { get; private set; }
    }
}
