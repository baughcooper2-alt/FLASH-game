using UnityEngine;

namespace FlashGame
{
    // S.T.A.R. Labs after the CW-style references: a tiered disc with three leaning pylons around the
    // particle accelerator ring (the Pipeline, lined with containment cells). Inside the ring are the
    // lobby, Cortex, Speed Lab, red access corridor, accelerator chamber, med bay, workshop and time vault.
    // Everything is walkable. Interior surfaces use the lab's own lamps instead of the sun (Prototype.shader).
    public sealed partial class StarLabs
    {
        const float FL = 2;                        // interior floor; the building sits on a podium
        const float RingTop = 11.5f, LampRadius = 68, LampHeight = 8.6f, LampStep = 8, LampRange = 11;
        const float ZoneRadius = 84.5f, ZoneTop = 18, EntranceHalfAngle = 5.4f;
        const float Flush = .003f;                 // corridor floors sit a hair lower so overlaps at doorways never z-fight
        const int LampLimit = 48;
        static readonly Color LampColor = new Color(1, .84f, .66f);
        static readonly float[] PylonAngles = { 0, 120, 240 };
        // Openings through the ring's inner wall: centre angle, half width (degrees), height above the floor.
        static readonly Vector3[] InnerGaps = { new Vector3(0, 3.6f, 6.2f), new Vector3(97, 2.75f, 4.2f), new Vector3(180, 8.45f, 7), new Vector3(270, 2.75f, 5) };
        static readonly string[] GapSigns = { "ACCELERATOR CHAMBER", "MED BAY  •  TIME VAULT", "CORTEX  •  MAIN LOBBY", "SPEED LAB" };

        Material shell, shellTrim, plinth, shellGlass, skylight, pylon, pylonFrame, cable, paving, pavingDark, streetPad,
            bark, leaves, lampHead, monument, entranceGlow;
        Material black, metal, white, screen, glowWarm, glowCool, blueGlow, hazard, yellow, deskGray, ceilingDark, floorDark, corridorWall;
        Material grating, ringCeiling, ribWhite, pilaster, ringWall, ribbedRed, copper, cellShell, cellPad, cellGlass, accelWall;
        Material lobbyFloor, lobbyWall, foliage, planter;
        Material brownWall, brownFloor, deck, navy, skylightWarm, cream, drawerBlue, drawerWhite, toolRed, toolGreen;
        Material concrete, runway, runwayBlue, runwayGray, labWall, steel, grille, galleryGlass, bigScreen, tunnelBack, fanBlade,
            beltBlue, amberStripe, orangeGlow, beigeGlow, yellowGlow, ledGreen, ledAmber, redGlow;
        Material medWall, medFloor, medCeiling, scannerHousing, scannerCore, leather, cartBlue, darkWall, workshopFloor, wire,
            studs, gloss, foam, sculpture, tube, clearGlass, displayGlass;
        Material redBright, redDark, floorBlack, trenchDark, pipeGray, plate, doorMetal, doorRing;
        Material chamberRed, chamberRedBright, chamberCeiling, hexTiles, grid, portalBlue;
        Material[] bottleColors, cableColors;

        SpeedsterMotor runner;
        Transform vaultDoor;
        Vector3 vaultDoorClosed;
        Color doorRingColor;
        float vaultDoorOpen, clock;

        public static void Build(PrototypeWorld world, Transform parent, Vector3 center)
        {
            var labs = new StarLabs(world, parent, center);
            labs.Plaza(); labs.Shell();
            foreach (float angle in PylonAngles) labs.Pylon(angle);
            labs.Ring(); labs.Lobby(); labs.Cortex(); labs.SpeedLab(); labs.EastWing(); labs.AccessCorridor(); labs.Chamber();
            labs.UploadLighting();
            world.Ticked += labs.Tick;
            world.Labels.Add(new WorldLabel(center + new Vector3(0, 16, -97), "S.T.A.R. LABS\nMAIN ENTRANCE"));
        }

        StarLabs(PrototypeWorld world, Transform parent, Vector3 center)
        {
            w = world;
            site = new GameObject("S.T.A.R. Labs").transform;
            site.SetParent(parent, false);
            site.localPosition = center;
            origin = site.position;
            animated = new GameObject("S.T.A.R. Labs moving parts").transform;
            animated.SetParent(site, false);
            runtime = animated.gameObject.AddComponent<StarLabsDynamic>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            glassShader = Resources.Load<Shader>("StarLabsGlass");
            var textShader = Resources.Load<Shader>("StarLabsText");
            textMaterial = font.material;
            if (textShader != null)
            {
                textMaterial = new Material(textShader) { name = "Lab sign text", mainTexture = font.material.mainTexture };
                runtime.Materials.Add(textMaterial);
                runtime.TrackFont(font, textMaterial);
            }
            CreateMaterials();
        }

        void CreateMaterials()
        {
            shell = M("Lab shell", .84f, .85f, .86f, 0, 0, 0, false);
            shellTrim = M("Lab shell trim", .5f, .52f, .55f, 0, 0, 0, false);
            plinth = M("Lab plinth", .3f, .31f, .33f, 0, 4, 1.5f, false);
            shellGlass = M("Lab glazing", .13f, .23f, .34f, 0, 4, 2.4f, false);
            skylight = M("Roof skylight", .07f, .11f, .16f, 0, 0, 0, false);
            pylon = M("Pylon cladding", .8f, .81f, .83f, 0, 4, 4, false);
            pylonFrame = M("Pylon frame", .42f, .44f, .47f, 0, 0, 0, false);
            cable = M("Pylon cable", .66f, .68f, .7f, 0, 0, 0, false);
            paving = M("Plaza paving", .56f, .55f, .52f, 0, 4, 4, false);
            pavingDark = M("Plaza inlay", .34f, .35f, .36f, 0, 0, 0, false);
            streetPad = M("Plaza surround", .4f, .41f, .42f, 0, 4, 6, false);
            bark = M("Plaza tree trunk", .2f, .16f, .12f, 0, 0, 0, false);
            leaves = M("Plaza tree canopy", .15f, .27f, .13f, 0, 0, 0, false);
            lampHead = M("Plaza lamp", 1, .92f, .75f, 1, 0, 0, false);
            monument = M("Monument", .1f, .11f, .12f, 0, 0, 0, false);
            entranceGlow = M("Entrance glow", .3f, .6f, 1, 1, 0, 0, false);

            black = M("Matte black", .045f, .045f, .05f);
            metal = M("Brushed metal", .55f, .58f, .62f);
            white = M("Lab white", .86f, .87f, .88f);
            screen = M("S.T.A.R. screen", .07f, .2f, .55f, 1);
            glowWarm = M("Lamp warm", 1, .93f, .8f, 1);
            glowCool = M("Lamp cool", .92f, .96f, 1, 1);
            blueGlow = M("Accelerator blue", .25f, .55f, 1, 1);
            hazard = M("Hazard stripes", .92f, .74f, .08f, 0, 3);
            yellow = M("Safety yellow", .86f, .63f, .1f);
            deskGray = M("Equipment grey", .22f, .22f, .23f);
            ceilingDark = M("Ceiling", .09f, .09f, .1f, 0, 4, 2);
            floorDark = M("Corridor floor", .1f, .1f, .11f, 0, 4, 2);
            corridorWall = M("Corridor wall", .7f, .71f, .72f, 0, 4, 2);

            grating = M("Pipeline grating", .3f, .31f, .33f, 0, 5);
            ringCeiling = M("Pipeline ceiling", .34f, .12f, .08f, 0, 4, 1.2f);
            ribWhite = M("Pipeline rib", .7f, .7f, .68f);
            pilaster = M("Pipeline pilaster", .44f, .4f, .35f, 0, 4, 2);
            ringWall = M("Pipeline wall", .2f, .17f, .15f, 0, 4, 2.4f);
            ribbedRed = M("Pipeline base", .34f, .1f, .07f, 0, 4, .45f);
            copper = M("Copper main", .56f, .33f, .15f);
            cellShell = M("Cell shell", .06f, .065f, .07f, 0, 4, 1);
            cellPad = M("Cell padding", .26f, .34f, 1, .7f, 4, .22f);
            cellGlass = Glass("Cell window", new Color(.5f, .7f, 1, .16f));
            accelWall = M("Accelerator wall", .24f, .25f, .27f, 0, 4, 2);

            lobbyFloor = M("Lobby floor", .1f, .1f, .11f, 0, 4, 2);
            lobbyWall = M("Lobby wall", .58f, .59f, .6f, 0, 4, 2.5f);
            foliage = M("Lobby plant", .15f, .32f, .14f);
            planter = M("Lobby planter", .3f, .3f, .31f);

            brownWall = M("Cortex wall", .23f, .19f, .16f, 0, 4, 2.2f);
            brownFloor = M("Cortex floor", .17f, .14f, .11f, 0, 4, 3);
            deck = M("Cortex platform", .13f, .12f, .11f, 0, 4, .5f);
            navy = M("Navy steel", .08f, .1f, .3f);
            skylightWarm = M("Cortex skylight", .95f, .82f, .5f, 1);
            cream = M("Workbench cream", .78f, .75f, .66f);
            drawerBlue = M("Drawer blue", .2f, .42f, .62f, 0, 4, .3f);
            drawerWhite = M("Tool chest", .85f, .86f, .87f, 0, 4, .3f);
            toolRed = M("Toolbox red", .6f, .09f, .07f);
            toolGreen = M("Toolbox green", .2f, .35f, .2f);

            concrete = M("Speed lab floor", .4f, .34f, .29f, 0, 4, 4);
            runway = M("Runway", .13f, .13f, .14f);
            runwayBlue = M("Runway blue", .2f, .38f, .66f);
            runwayGray = M("Runway centre", .46f, .47f, .48f);
            labWall = M("Speed lab wall", .3f, .3f, .31f, 0, 4, 3);
            steel = M("Truss steel", .035f, .035f, .04f);
            grille = M("Wall grille", .08f, .08f, .09f, 0, 4, .25f);
            galleryGlass = M("Gallery glass", .1f, .14f, .18f, 0, 4, 2.4f);
            bigScreen = M("Big screen", .05f, .1f, .28f, .55f, 4, 1.5f);
            tunnelBack = M("Wind tunnel", .12f, .1f, .3f, .35f);
            fanBlade = M("Fan blade", .7f, .72f, .75f);
            beltBlue = M("Belt stripes", .3f, .5f, .95f, 0, 3);
            amberStripe = M("Console stripe", .7f, .5f, .15f);
            orangeGlow = M("Capsule lamp", 1, .55f, .25f, 1);
            beigeGlow = M("Lit doorway", .78f, .64f, .5f, .6f);
            yellowGlow = M("Truss lamp", 1, .85f, .4f, 1);
            ledGreen = M("LED green", .2f, 1, .35f, 1);
            ledAmber = M("LED amber", 1, .7f, .15f, 1);
            redGlow = M("Red indicator", 1, .15f, .1f, 1);

            medWall = M("Med bay wall", .52f, .53f, .54f, 0, 4, 1.8f);
            medFloor = M("Med bay floor", .14f, .14f, .15f, 0, 4, 2);
            medCeiling = M("Med bay ceiling", .5f, .5f, .52f, 0, 4, 2);
            scannerHousing = M("Scanner housing", .72f, .73f, .75f);
            scannerCore = M("Scanner core", .8f, .93f, 1, 1);
            leather = M("Bed leather", .3f, .22f, .14f);
            cartBlue = M("Cart blue", .15f, .3f, .65f);
            darkWall = M("Workshop wall", .16f, .15f, .15f, 0, 4, 2);
            workshopFloor = M("Workshop floor", .07f, .07f, .075f);
            wire = M("Wire shelving", .55f, .53f, .5f, 0, 4, .1f);
            studs = M("Vault studs", .38f, .38f, .4f, 0, 7);
            gloss = M("Vault floor", .075f, .075f, .08f, 0, 4, 3);
            foam = M("Vault foam", .12f, .16f, .55f, .25f, 4, .35f);
            sculpture = M("Vault sculpture", .62f, .62f, .63f);
            tube = M("Test tube", .8f, .82f, .8f);
            clearGlass = Glass("Lab glass", new Color(.55f, .72f, .85f, .12f));
            displayGlass = Glass("Display glass", new Color(.7f, .85f, 1, .07f));

            redBright = M("Access red", .66f, .13f, .07f, 0, 4, 1.5f);
            redDark = M("Access ceiling", .25f, .05f, .04f);
            floorBlack = M("Access floor", .07f, .07f, .075f, 0, 4, 1.6f);
            trenchDark = M("Access trench", .04f, .04f, .045f);
            pipeGray = M("Pipe grey", .33f, .34f, .36f);
            plate = M("Label plate", .85f, .85f, .82f);
            doorMetal = M("Vault door", .2f, .21f, .23f, 0, 4, .8f);
            doorRingColor = new Color(.25f, .7f, 1);
            doorRing = M("Vault door glow", doorRingColor.r, doorRingColor.g, doorRingColor.b, 1);

            chamberRed = M("Chamber red", .42f, .06f, .05f, 0, 4, 2);
            chamberRedBright = M("Chamber trim", .55f, .08f, .06f);
            chamberCeiling = M("Chamber ceiling", .1f, .05f, .05f);
            hexTiles = M("Chamber tiles", .15f, .15f, .16f, 0, 6, 2.2f);
            grid = M("Containment grid", .04f, .05f, .12f, 0, 8, 1.1f);
            portalBlue = M("Portal tunnel", .12f, .22f, .6f, .45f);

            bottleColors = new[] { M("Bottle red", .62f, .12f, .1f), M("Bottle green", .14f, .45f, .24f), M("Bottle blue", .14f, .3f, .72f),
                M("Bottle amber", .85f, .62f, .12f), M("Bottle brown", .4f, .22f, .1f), metal };
            cableColors = new[] { M("Cable red", .55f, .08f, .06f), M("Cable green", .12f, .4f, .22f), M("Cable white", .8f, .8f, .78f), black };
        }

        static bool NearPylon(float angle, float margin)
        {
            foreach (float p in PylonAngles) if (Mathf.Abs(Mathf.DeltaAngle(angle, p)) < margin) return true;
            return false;
        }
        static bool InInnerGap(float angle, float margin)
        {
            foreach (var gap in InnerGaps) if (Mathf.Abs(Mathf.DeltaAngle(angle, gap.x)) < gap.y + margin) return true;
            return false;
        }

        // Exterior ----------------------------------------------------------------------

        void Plaza()
        {
            Block("Plaza surround", -118, 0, -118, 118, .06f, 118, streetPad);
            Shape(site, "S.T.A.R. Labs plaza", LabMesh.Arc(0, 118, 0, .1f, 0, 360, 120), paving, Vector3.zero, true);
            foreach (float r in new[] { 92f, 104f, 114f })
                Shape(site, "Plaza inlay", LabMesh.Arc(r, r + 1.2f, .1f, .112f, 0, 360, 120), pavingDark, Vector3.zero);
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f + 11.25f, t = a + 11.25f;
                if (Mathf.Abs(Mathf.DeltaAngle(a, 180)) > 20)
                {
                    Vector3 p = Polar(a, 104.6f, 0);
                    Cyl(site, "Plaza lamp post", p + Vector3.up * 3.6f, .1f, 7.2f, plinth);
                    Box(site, "Plaza lamp", p + Vector3.up * 7.25f, new Vector3(.4f, .18f, 1.2f), lampHead, new Vector3(0, a, 0));
                }
                if (Mathf.Abs(Mathf.DeltaAngle(t, 180)) > 20)
                {
                    Vector3 q = Polar(t, 110, 0);
                    Cyl(site, "Planter", q + Vector3.up * .4f, 1.7f, .8f, plinth);
                    Cyl(site, "Plaza tree trunk", q + Vector3.up * 2.6f, .3f, 3.6f, bark);
                    Ball(site, "Plaza tree canopy", q + Vector3.up * 6, new Vector3(5.5f, 6.5f, 5.5f), leaves);
                }
            }
            Block("S.T.A.R. Labs monument", 12, 0, -114, 24, 3.4f, -112.6f, monument);
            Text(site, "S.T.A.R. LABS", new Vector3(18, 2.3f, -114.06f), 1.25f, Color.white, 180);
            Text(site, "SCIENTIFIC & TECHNOLOGICAL ADVANCED RESEARCH", new Vector3(18, 1.1f, -114.06f), .3f, new Color(.75f, .85f, 1), 180);
        }

        void Shell()
        {
            float e0 = 180 - EntranceHalfAngle, e1 = 180 + EntranceHalfAngle;
            // Kept below step height so runners can reach the facade and wall-run it.
            Shape(site, "Plinth", LabMesh.Arc(85, 88, 0, .28f), plinth, Vector3.zero, true);
            Shape(site, "Lab shell", LabMesh.Arc(84.4f, 85, 0, 12, e1, e0 + 360), shell, Vector3.zero, true);
            Shape(site, "Entrance lintel", LabMesh.Arc(84.4f, 85, FL + 8, 12, e0, e1), shell, Vector3.zero, true);
            foreach (float y in new[] { 4.2f, 8.4f })
                Shape(site, "Shell groove", LabMesh.Arc(84.99f, 85.06f, y, y + .14f, e1, e0 + 360), shellTrim, Vector3.zero);
            // The glazing is vertical so wall-runners reach the roof lip and crest onto the roof.
            Shape(site, "Glazing band", LabMesh.Arc(84.6f, 85, 12, 17), shellGlass, Vector3.zero, true);
            for (int i = 0; i < 90; i++)
                Box(site, "Glazing mullion", Polar(i * 4, 85.05f, 14.5f), new Vector3(.18f, 5, .14f), shellTrim, new Vector3(0, i * 4, 0));
            Shape(site, "Roof lip", LabMesh.Arc(84.6f, 86.2f, 17, 18), shell, Vector3.zero, true);
            Shape(site, "Main roof", LabMesh.Band(86.2f, 18, 62, 27), shell, Vector3.zero, true);
            float slope = Mathf.Atan2(9, 24.2f) * Mathf.Rad2Deg;
            foreach (var (radius, count) in new[] { (81f, 72), (75f, 64), (69f, 56) })
                for (int i = 0; i < count; i++)
                {
                    float a = (i + .5f) * 360f / count;
                    if (NearPylon(a, 6)) continue;
                    float y = 18 + (86.2f - radius) / 24.2f * 9 + .06f;
                    Box(site, "Roof skylight", Polar(a, radius, y), new Vector3(1.8f, .12f, 1.8f), skylight, new Vector3(slope, a, 0));
                }
            Shape(site, "Upper ring", LabMesh.Arc(61, 62, 26, 31.5f), shell, Vector3.zero, true);
            for (int i = 0; i < 48; i++)
                Box(site, "Upper ring window", Polar(i * 7.5f + 3.75f, 62.03f, 29.3f), new Vector3(1.6f, 1.3f, .1f), skylight, new Vector3(0, i * 7.5f + 3.75f, 0));
            Shape(site, "Upper roof", LabMesh.Arc(40, 61.5f, 31, 31.5f), shell, Vector3.zero, true);
            Shape(site, "Glass crown", LabMesh.Band(40, 31.5f, 24, 36), shellGlass, Vector3.zero, true);
            Shape(site, "Crown roof", LabMesh.Arc(0, 24.2f, 35.6f, 36), shell, Vector3.zero, true);
            Block("Roof core", -14, 36, -7, 14, 40.5f, 7, shell);
            for (int i = -1; i <= 1; i += 2) Cyl(site, "Roof core end", new Vector3(i * 14, 38.25f, 0), 7, 4.5f, shell, default, true);
            Block("Roof core opening", -11, 40.5f, -4.5f, 11, 40.56f, 4.5f, skylight, false);

            // Entrance: ramp up to the podium floor, canopy and lettering.
            float run = 19, rise = FL - .1f, length = Mathf.Sqrt(run * run + rise * rise), angle = Mathf.Atan2(rise, run);
            Box(site, "Entrance ramp", new Vector3(0, .1f + rise / 2 - .5f * run / length, -85 - run / 2 + .5f * rise / length),
                new Vector3(16, 1, length), paving, new Vector3(-angle * Mathf.Rad2Deg, 0, 0), true);
            for (int i = -1; i <= 1; i += 2)
            {
                Strut(site, "Ramp glow", new Vector3(i * 7.9f, .16f, -104), new Vector3(i * 7.9f, FL + .06f, -85), .12f, entranceGlow);
                Cyl(site, "Canopy column", new Vector3(i * 9.2f, 5.1f, -94.2f), .28f, 10, shell, default, true);
                Block("Entrance glow", i * 7.95f - .08f, FL, -84.85f, i * 7.95f + .08f, FL + 8, -84.65f, entranceGlow, false);
            }
            Block("Entrance canopy", -10, 10.1f, -95, 10, 10.6f, -84.8f, shell);
            Block("Canopy glow", -9.8f, 10.04f, -94.85f, 9.8f, 10.1f, -94.6f, entranceGlow, false);
            Text(site, "S.T.A.R. LABS", new Vector3(0, 11.45f, -94.6f), 1.7f, Color.white, 180);
        }

        void Pylon(float angle)
        {
            const float H = 82;
            // Local +x points away from the centre; the fin leans outward and flares toward its top.
            // Its broad faces stay vertical so they can be wall-run.
            var t = Frame("S.T.A.R. Labs pylon", Polar(angle, 76, 19), 0);
            t.localRotation = Quaternion.Euler(0, angle - 90, 0) * Quaternion.Euler(0, 0, -8);
            Shape(t, "Pylon", LabMesh.Hull(new[] {
                new Vector3(-6, 0, -3), new Vector3(6, 0, -3), new Vector3(6, 0, 3), new Vector3(-6, 0, 3),
                new Vector3(-7, H, -3), new Vector3(11, H, -3), new Vector3(11, H, 3), new Vector3(-7, H, 3) }), pylon, Vector3.zero, true, true);
            Box(t, "Pylon cap", new Vector3(2, H + .45f, 0), new Vector3(18.8f, .9f, 6.6f), pylonFrame, default, true);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * 3.08f;
                Vector3 b0 = new Vector3(-5.2f, 7, z), b1 = new Vector3(5.2f, 7, z), t0 = new Vector3(-6.1f, H - 6, z), t1 = new Vector3(9.9f, H - 6, z);
                Strut(t, "Pylon frame", b0, t0, .5f, pylonFrame); Strut(t, "Pylon frame", b1, t1, .5f, pylonFrame);
                Strut(t, "Pylon frame", b0, b1, .5f, pylonFrame); Strut(t, "Pylon frame", t0, t1, .5f, pylonFrame);
                for (int i = 1; i < 12; i++)
                    Strut(t, "Pylon cable", Vector3.Lerp(b0, b1, i / 12f), Vector3.Lerp(t0, t1, i / 12f), .12f, cable);
                Strut(t, "Pylon rib", new Vector3(-6.1f, 1, side * 1.4f), new Vector3(-7.1f, H - 1, side * 1.4f), .35f, pylonFrame);
                // Brackets reach down into the roof.
                Strut(t, "Pylon bracket", new Vector3(-5.6f, 15, side * 2.2f), new Vector3(-15, -2, side * 2.2f), .8f, pylon);
                Strut(t, "Pylon bracket", new Vector3(6.4f, 13, side * 2.2f), new Vector3(13, -3, side * 2.2f), .8f, pylon);
            }
        }

        // The accelerator ring (the Pipeline) -------------------------------------------

        void Ring()
        {
            float e0 = 180 - EntranceHalfAngle, e1 = 180 + EntranceHalfAngle;
            const float cellsFrom = 200, cellsTo = 340;
            // The outer 1.4 m stays one solid band up to the roof: wall-runners on the facade probe 3 m inward
            // for a roof to crest onto, and must not find the tunnel floor or ceiling through the wall.
            Shape(site, "Pipeline floor", LabMesh.Arc(64.5f, 80.5f, 0, FL), grating, Vector3.zero, true);
            Shape(site, "Entrance floor", LabMesh.Arc(80.5f, 85, 0, FL, e0, e1), grating, Vector3.zero, true);
            Shape(site, "Pipeline ceiling", LabMesh.Arc(64.5f, 83, RingTop, RingTop + .6f), ringCeiling, Vector3.zero, true);
            Shape(site, "Pipeline back wall", LabMesh.Arc(83, 84.4f, 0, ZoneTop, e1, e0 + 360), ringWall, Vector3.zero, true);
            Shape(site, "Entrance lintel", LabMesh.Arc(80, 84.4f, FL + 8, RingTop, e0, e1), ringWall, Vector3.zero, true);
            Text(site, "EXIT  •  CENTRAL CITY", Polar(180, 79.9f, FL + 8.8f), .5f, Color.white, 0);

            // Inner wall, raised ledge and guide light, broken by the four openings.
            for (int i = 0; i < InnerGaps.Length; i++)
            {
                Vector3 gap = InnerGaps[i], next = InnerGaps[(i + 1) % InnerGaps.Length];
                float a0 = gap.x + gap.y, a1 = next.x - next.y;
                if (a1 <= a0) a1 += 360;
                Shape(site, "Pipeline inner wall", LabMesh.Arc(65, 66, FL, RingTop, a0, a1), ringWall, Vector3.zero, true);
                Shape(site, "Pipeline ledge", LabMesh.Arc(66, 67.5f, FL, FL + 1.2f, a0 + .4f, a1 - .4f), ribbedRed, Vector3.zero, true);
                Shape(site, "Pipeline guide light", LabMesh.Arc(67.5f, 67.62f, FL, FL + .05f, a0 + .4f, a1 - .4f), blueGlow, Vector3.zero);
                Shape(site, "Pipeline handrail", LabMesh.Arc(67.3f, 67.42f, FL + 2.15f, FL + 2.25f, a0 + .4f, a1 - .4f), metal, Vector3.zero);
                for (float a = a0 + 2; a < a1 - 1; a += 4)
                    Box(site, "Handrail post", Polar(a, 67.36f, FL + 1.7f), new Vector3(.07f, 1.05f, .07f), metal);
                Shape(site, "Pipeline door head", LabMesh.Arc(65, 66, FL + gap.z, RingTop, gap.x - gap.y, gap.x + gap.y), ringWall, Vector3.zero, true);
                Text(site, GapSigns[i], Polar(gap.x, 66.08f, FL + gap.z + .9f), .55f, Color.white, gap.x);
                for (int s = -1; s <= 1; s += 2)
                    Box(site, "Opening light", Polar(gap.x + s * gap.y, 66.05f, FL + gap.z / 2), new Vector3(.14f, gap.z, .1f), blueGlow, new Vector3(0, gap.x + s * gap.y, 0));
            }

            // Outer side: red base with the copper main, then containment cells (the Pipeline) or accelerator wall.
            Shape(site, "Pipeline base", LabMesh.Arc(78.5f, 80, FL, FL + 1.2f, e1, e0 + 360), ribbedRed, Vector3.zero, true);
            Shape(site, "Pipeline guide light", LabMesh.Arc(78.38f, 78.5f, FL, FL + .05f, e1, e0 + 360), blueGlow, Vector3.zero);
            Shape(site, "Copper main", LabMesh.Tube(79.25f, .3f, e1, e0 + 360), copper, Vector3.up * (FL + 1.5f));
            Shape(site, "Accelerator beam glow", LabMesh.Tube(73, .1f), blueGlow, Vector3.up * (RingTop - .12f));

            int bays = 41;
            float bay = (cellsTo - cellsFrom) / bays;
            Shape(site, "Cell bay sill", LabMesh.Arc(80, 83, FL, FL + 1.25f, cellsFrom, cellsTo), ringWall, Vector3.zero, true);
            Shape(site, "Cell shelf", LabMesh.Arc(80.2f, 83, FL + 4.27f, FL + 4.4f, cellsFrom, cellsTo), pilaster, Vector3.zero);
            Shape(site, "Cell bay header", LabMesh.Arc(80, 83, FL + 8.3f, RingTop, cellsFrom, cellsTo), ringWall, Vector3.zero, true);
            for (int i = 0; i <= bays; i++)
            {
                float a = cellsFrom + i * bay;
                Box(site, "Cell bay pilaster", Polar(a, 81.5f, (FL + 1.25f + RingTop) / 2), new Vector3(.8f, RingTop - FL - 1.25f, 3), pilaster, new Vector3(0, a, 0), true);
            }
            for (int i = 0; i < bays; i++)
                for (int row = 0; row < 2; row++)
                    Cell(cellsFrom + (i + .5f) * bay, FL + 1.25f + row * 3.15f);

            foreach (var (a0, a1) in new[] { (e1, cellsFrom), (cellsTo, e0 + 360) })
            {
                Shape(site, "Accelerator wall", LabMesh.Arc(80, 83, FL, RingTop, a0, a1), accelWall, Vector3.zero, true);
                for (float a = a0 + 1.5f; a < a1 - 1; a += 3)
                    Box(site, "Accelerator rib", Polar(a, 79.78f, (FL + 1.2f + RingTop) / 2), new Vector3(.45f, RingTop - FL - 1.2f, .45f), deskGray, new Vector3(0, a, 0));
                for (int k = 0; k < 4; k++)
                    Shape(site, "Cable bundle", LabMesh.Tube(79.68f, .075f, a0, a1, 6), cableColors[k], Vector3.up * (FL + 2.3f + k * .16f));
                for (float a = a0 + 6; a < a1 - 4; a += 12)
                {
                    Box(site, "Wall light", Polar(a, 79.94f, FL + 5.6f), new Vector3(1.6f, 3.2f, .06f), glowWarm, new Vector3(0, a, 0));
                    for (int k = 0; k < 4; k++)
                        Box(site, "Wall light slat", Polar(a, 79.88f, FL + 4.4f + k * .8f), new Vector3(1.7f, .1f, .07f), black, new Vector3(0, a, 0));
                }
                for (float a = a0 + 12; a < a1 - 4; a += 24)
                {
                    var label = Frame("Magnet label", Polar(a, 79.9f, FL + 8.6f), a + 180);
                    Box(label, "Label plate", Vector3.zero, new Vector3(1.6f, .5f, .02f), yellow);
                    Box(label, "Label stripe", new Vector3(-.66f, 0, .012f), new Vector3(.24f, .5f, .01f), hazard);
                    Text(label, "MAG RES\n0305-80", new Vector3(.1f, 0, .02f), .17f, Color.black, 0);
                }
            }

            // Ceiling ribs: white segmented frames with chamfered corners.
            for (int i = 0; i < 72; i++)
            {
                float a = i * 5 + 2.5f;
                var rib = Frame("Pipeline ceiling rib", Polar(a, 73, 0), a - 90);
                for (int k = -1; k <= 1; k++) Box(rib, "Rib block", new Vector3(k * 4.4f, RingTop - .38f, 0), new Vector3(4.1f, .7f, 1.3f), ribWhite);
                for (int k = -1; k <= 1; k += 2)
                {
                    Box(rib, "Rib joint", new Vector3(k * 2.2f, RingTop - .38f, 0), new Vector3(.3f, .74f, 1.34f), black);
                    Box(rib, "Rib corner", new Vector3(k * 6.1f, RingTop - 1.75f, 0), new Vector3(2.7f, .7f, 1.3f), ribWhite, new Vector3(0, 0, -k * 45));
                }
            }
            // Wall lamps match the analytic ring lamps in the shader; every other one is also a real light.
            for (int i = 0; i < 360 / LampStep; i++)
            {
                float a = i * LampStep;
                if (InInnerGap(a, 1.5f)) continue;
                Box(site, "Pipeline lamp", Polar(a, 66.12f, LampHeight), new Vector3(1.4f, .35f, .24f), glowWarm, new Vector3(0, a, 0));
                Box(site, "Pipeline lamp hood", Polar(a, 66.2f, LampHeight + .28f), new Vector3(1.6f, .14f, .45f), black, new Vector3(0, a, 0));
                if (i % 2 == 0) RealLight(Polar(a, LampRadius, LampHeight), LampColor, LampRange, 1.2f);
            }
        }

        // Containment cell: black shell, padded blue interior, elongated hexagonal window.
        void Cell(float angle, float y)
        {
            var c = Frame("Containment cell", Polar(angle, 81.5f, y), angle + 180);
            Box(c, "Cell back", new Vector3(0, 1.5f, -1.33f), new Vector3(3.2f, 3, .15f), cellShell);
            Box(c, "Cell roof", new Vector3(0, 2.925f, 0), new Vector3(3.2f, .15f, 2.8f), cellShell);
            Box(c, "Cell base", new Vector3(0, .075f, 0), new Vector3(3.2f, .15f, 2.8f), cellShell);
            Box(c, "Cell padding", new Vector3(0, 1.5f, -1.22f), new Vector3(2.9f, 2.7f, .06f), cellPad);
            Box(c, "Cell padding", new Vector3(0, .18f, 0), new Vector3(2.8f, .06f, 2.5f), cellPad);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(c, "Cell side", new Vector3(i * 1.525f, 1.5f, 0), new Vector3(.15f, 3, 2.8f), cellShell);
                Box(c, "Cell padding", new Vector3(i * 1.42f, 1.5f, 0), new Vector3(.06f, 2.7f, 2.5f), cellPad);
                Box(c, "Cell stile", new Vector3(i * 1.32f, 1.5f, 1.33f), new Vector3(.56f, 3, .22f), cellShell);
                Box(c, "Cell hazard band", new Vector3(i * 1.32f, 1.5f, 1.45f), new Vector3(.56f, .22f, .03f), hazard);
                Cyl(c, "Cell pipe", new Vector3(i * 1.66f, 2.45f, .1f), .09f, 2.5f, metal, new Vector3(90, 0, 0));
                for (int j = -1; j <= 1; j += 2)
                    Box(c, "Cell window corner", new Vector3(i * .98f, 1.5f + j * 1, 1.36f), new Vector3(.5f, .5f, .2f), cellShell, new Vector3(0, 0, 45));
            }
            Box(c, "Cell head", new Vector3(0, 2.77f, 1.33f), new Vector3(3.2f, .46f, .22f), cellShell);
            Box(c, "Cell sill", new Vector3(0, .23f, 1.33f), new Vector3(3.2f, .46f, .22f), cellShell);
            Box(c, "Cell hazard head", new Vector3(0, 2.66f, 1.45f), new Vector3(1.9f, .1f, .03f), hazard);
            Box(c, "Cell glass", new Vector3(0, 1.5f, 1.3f), new Vector3(2.12f, 2.12f, .03f), cellGlass);
            Box(c, "Cell crossbar", new Vector3(0, 1.5f, 1.4f), new Vector3(2.2f, .2f, .1f), metal);
        }

        // Rooms inside the ring ----------------------------------------------------------

        void Lobby()
        {
            Block("Lobby floor", -9.6f, 0, -66, 9.6f, FL - Flush, -22.1f, lobbyFloor);
            WallZ("Lobby wall", -10.6f, -9.6f, -64.4f, -21.3f, FL, 11, lobbyWall);
            WallZ("Lobby wall", 9.6f, 10.6f, -64.4f, -21.3f, FL, 11, lobbyWall);
            // Thin facing wall in front of the Cortex shell; its doorway is a touch wider to avoid coplanar faces.
            WallX("Lobby north wall", -22.3f, -21.95f, -10.6f, 10.6f, FL, 11, lobbyWall, Door(-2.55f, 2.55f, FL + 5));
            Block("Lobby ceiling", -10.6f, 11, -65, 10.6f, 11.5f, -21.3f, ceilingDark);
            for (int i = -1; i <= 1; i += 2)
            {
                Block("Lobby light strip", i * 4 - .25f, 10.92f, -62, i * 4 + .25f, 11, -24, glowCool, false);
                Block("Lobby floor light", i * 8.9f - .06f, FL, -63.5f, i * 8.9f + .06f, FL + .012f, -23, blueGlow, false);
                foreach (float z in new[] { -34f, -50f })
                    Monitor(site, new Vector3(i * 9.55f, FL + 2.6f, z), i < 0 ? 90 : 270, 2.4f, false);
                foreach (float z in new[] { -30f, -56f })
                    Block("Lobby bench", i * 7.6f - .5f, FL, z - 2, i * 7.6f + .5f, FL + .5f, z + 2, deskGray);
                foreach (float z in new[] { -24f, -62f })
                {
                    Cyl(site, "Lobby planter", new Vector3(i * 8.4f, FL + .45f, z), .55f, .9f, planter, default, true);
                    Ball(site, "Lobby plant", new Vector3(i * 8.4f, FL + 1.6f, z), new Vector3(1.3f, 1.6f, 1.3f), foliage);
                }
            }
            for (int z = -60; z <= -26; z += 8) Block("Lobby light bar", -3.6f, 10.92f, z - .2f, 3.6f, 11, z + .2f, glowCool, false);
            Block("Lobby logo panel", -7.5f, 6.4f, -22.45f, 7.5f, 10.2f, -22.3f, ceilingDark, false);
            Text(site, "S.T.A.R. LABS", new Vector3(0, 8.9f, -22.5f), 1.5f, Color.white, 180);
            Text(site, "SCIENTIFIC & TECHNOLOGICAL ADVANCED RESEARCH LABORATORIES", new Vector3(0, 7.5f, -22.5f), .3f, new Color(.72f, .84f, 1), 180);
            Block("Logo underline", -6.5f, 7.05f, -22.5f, 6.5f, 7.12f, -22.45f, blueGlow, false);

            Vector3 desk = new Vector3(-6.2f, 0, -48);
            Shape(site, "Reception desk", LabMesh.Arc(2.6f, 3.4f, FL, FL + 1.1f, 20, 160), white, desk, true);
            Shape(site, "Reception glow", LabMesh.Arc(3.4f, 3.45f, FL + .08f, FL + .16f, 20, 160), blueGlow, desk);
            foreach (float a in new[] { 70f, 110f }) Monitor(site, desk + Polar(a, 3, FL + 1.1f), a + 180, .7f);

            Vector3 exhibit = new Vector3(5, 0, -36);
            Cyl(site, "Exhibit plinth", exhibit + Vector3.up * (FL + .5f), 1.8f, 1, white, default, true);
            Shape(site, "Accelerator model", LabMesh.Tube(1.35f, .1f), metal, exhibit + Vector3.up * (FL + 1.2f));
            Shape(site, "Accelerator model glow", LabMesh.Tube(1.35f, .04f), blueGlow, exhibit + Vector3.up * (FL + 1.33f));
            Shape(site, "Exhibit floor ring", LabMesh.Arc(2.6f, 2.8f, FL, FL + .01f), blueGlow, exhibit);
            Text(site, "PARTICLE ACCELERATOR\n<size=48>SCALE MODEL</size>", exhibit + new Vector3(0, FL + .55f, -1.82f), .2f, new Color(.18f, .22f, .28f), 180);

            foreach (float z in new[] { -30f, -44f, -57f }) Lamp(new Vector3(0, 9.8f, z), new Color(1, .96f, .9f) * .95f, 14);
        }

        void Cortex()
        {
            var c = Frame("Cortex", new Vector3(0, 0, -8), 0);
            Vector3 centre = c.localPosition;
            Shape(c, "Cortex floor", LabMesh.Arc(0, 14.6f, 0, FL, 15, 375, 12), brownFloor, Vector3.zero, true);
            Shape(c, "Cortex ceiling", LabMesh.Arc(0, 14.6f, 10, 10.5f, 15, 375, 12), ceilingDark, Vector3.zero, true);
            for (int k = 0; k < 12; k++)
            {
                float a = k * 30;
                var s = Frame("Cortex wall", Dir(a) * 13.5f, a, c);
                if (k % 3 == 0)
                {
                    for (int i = -1; i <= 1; i += 2) Box(s, "Cortex door jamb", new Vector3(i * 3.125f, 6, 0), new Vector3(1.25f, 8, 1), brownWall, default, true);
                    Box(s, "Cortex door head", new Vector3(0, 8.5f, 0), new Vector3(5, 3, 1), brownWall, default, true);
                }
                else Box(s, "Cortex wall", new Vector3(0, 6, 0), new Vector3(7.5f, 8, 1), brownWall, default, true);
                Box(c, "Cortex pilaster", Dir(a + 15) * 13.25f + Vector3.up * 6, new Vector3(.7f, 8, .7f), deck, new Vector3(0, a + 15, 0));
                Cyl(c, "Cortex downlight", Polar(a + 15, 10.5f, 9.97f), .22f, .05f, glowWarm);
            }
            Box(c, "Cortex skylight", new Vector3(0, 9.97f, 0), new Vector3(7, .06f, 4), skylightWarm);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(c, "Skylight frame", new Vector3(i * 3.6f, 9.9f, 0), new Vector3(.25f, .2f, 4.4f), black);
                Box(c, "Skylight frame", new Vector3(0, 9.9f, i * 2.1f), new Vector3(7.4f, .2f, .25f), black);
                Box(c, "Ceiling beam", new Vector3(i * 6, 9.7f, 0), new Vector3(.45f, .6f, 27), black);
            }

            // Raised side platforms with steps (one carries the suit display).
            foreach (var (a0, a1) in new[] { (22f, 68f), (292f, 338f) })
            {
                Shape(c, "Cortex platform", LabMesh.Arc(8.5f, 14.6f, 0, FL + 1, a0, a1), deck, Vector3.zero, true);
                for (int s = 1; s <= 3; s++)
                    Shape(c, "Cortex platform step", LabMesh.Arc(8.5f - s * .5f, 9 - s * .5f, 0, FL + 1 - s * .25f, a0, a1), deck, Vector3.zero, true);
                Shape(c, "Platform edge", LabMesh.Arc(8.42f, 8.58f, FL + .99f, FL + 1.03f, a0, a1), hazard, Vector3.zero);
            }

            // Horseshoe command desk.
            var deskCentre = new Vector3(0, 0, -1);
            Shape(c, "Cortex desk", LabMesh.Arc(3.75f, 4.65f, 0, FL + .95f, 75, 285), deskGray, deskCentre, true);
            Shape(c, "Cortex desk top", LabMesh.Arc(3.55f, 4.85f, FL + .95f, FL + 1.03f, 72, 288), white, deskCentre);
            Shape(c, "Cortex desk glow", LabMesh.Arc(4.65f, 4.7f, FL + .06f, FL + .14f, 75, 285), blueGlow, deskCentre);
            foreach (float a in new[] { 115f, 160f, 205f, 248f })
            {
                Monitor(c, deskCentre + Polar(a, 4.45f, FL + 1.03f), a + 180, .8f);
                Box(c, "Keyboard", deskCentre + Polar(a, 3.95f, FL + 1.045f), new Vector3(.5f, .03f, .18f), metal, new Vector3(0, a, 0));
            }
            Stool(centre + deskCentre + Vector3.up * FL);

            // Suit display on the north-west platform.
            Vector3 casePos = Polar(315, 11, FL + 1);
            var dc = Frame("Suit display", casePos, 135, c);
            Box(dc, "Display base", new Vector3(0, .25f, 0), new Vector3(1.6f, .5f, 1.6f), black, default, true);
            Box(dc, "Display glass", new Vector3(0, 1.75f, 0), new Vector3(1.5f, 2.5f, 1.5f), displayGlass, default, true);
            Box(dc, "Display canopy", new Vector3(0, 3.12f, 0), new Vector3(1.6f, .24f, 1.6f), black);
            Box(dc, "Display light", new Vector3(0, 2.99f, 0), new Vector3(1.3f, .02f, 1.3f), glowCool);
            Text(dc, "THE FLASH", new Vector3(0, .3f, .81f), .18f, new Color(.95f, .8f, .4f), 0);
            var suit = Resources.Load<GameObject>("FlashReference");
            if (suit != null) PlaceExhibit(Object.Instantiate(suit, dc, false), dc);
            RealLight(centre + casePos + Dir(135) * .6f + Vector3.up * 2.6f, new Color(1, .97f, .92f) * 1.6f, 4, 1.5f);

            // Workbench, shelving, tool chest and the blue light pillars.
            var wb = Frame("Workbench", Polar(225, 11.4f, FL), 45, c);
            Box(wb, "Bench top", new Vector3(0, .95f, 0), new Vector3(3.6f, .08f, 1), cream, default, true);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(wb, "Bench drawers", new Vector3(i * 1.28f, .46f, 0), new Vector3(1, .9f, .95f), drawerBlue, default, true);
                Box(wb, "Shelf post", new Vector3(i * 1.75f, 1.6f, -.42f), new Vector3(.05f, 1.3f, .05f), metal);
            }
            foreach (float y in new[] { 1.55f, 2.2f }) Box(wb, "Bench shelf", new Vector3(0, y, -.3f), new Vector3(3.6f, .04f, .45f), metal);
            for (int i = 0; i < 4; i++) Box(wb, "Parts bin", new Vector3(-1.3f + i * .85f, 1.66f, -.3f), new Vector3(.6f, .18f, .38f), i == 1 ? drawerBlue : black);
            Box(wb, "Parts bin", new Vector3(.9f, 2.3f, -.3f), new Vector3(.7f, .16f, .38f), black);
            Bottles(wb, new Vector3(-1.4f, .99f, .1f), new Vector3(.22f, 0, 0), 4, 11);
            Box(wb, "Component tray", new Vector3(.6f, 1.02f, .15f), new Vector3(.5f, .06f, .35f), black);
            Box(wb, "Circuit board", new Vector3(1.25f, 1, .05f), new Vector3(.35f, .02f, .3f), toolGreen, new Vector3(0, 20, 0));
            Cyl(wb, "Bowl", new Vector3(.05f, 1.03f, .2f), .14f, .06f, black);
            Stool(centre + Polar(225, 9.9f, FL));
            foreach (float a in new[] { 140f, 205f })
            {
                Vector3 p = centre + Polar(a, 12.1f, FL);
                Shelf(p, a + 180, 2, 2.5f, 4, steel);
                var s = Frame("Shelf contents", p, a + 180);
                Box(s, "Toolbox", new Vector3(-.4f, 2.48f, 0), new Vector3(.8f, .32f, .42f), toolRed);
                Box(s, "Toolbox", new Vector3(.45f, 2.45f, 0), new Vector3(.6f, .26f, .4f), toolGreen);
                for (int i = 0; i < 3; i++) Box(s, "Storage bin", new Vector3(-.6f + i * .6f, .87f, 0), new Vector3(.5f, .26f, .5f), i == 1 ? drawerBlue : black);
                Box(s, "Equipment case", new Vector3(0, 1.62f, 0), new Vector3(1.4f, .3f, .5f), metal);
                Box(s, "Shelf glow", new Vector3(0, 1.43f, .2f), new Vector3(1.8f, .03f, .05f), glowCool);
            }
            var chest = Frame("Tool chest", Polar(45, 12.2f, FL + 1), 225, c);
            Box(chest, "Tool chest", new Vector3(0, .85f, 0), new Vector3(1.5f, 1.7f, .75f), drawerWhite, default, true);
            Box(chest, "Tool chest top", new Vector3(0, 1.95f, 0), new Vector3(1.4f, .5f, .7f), deskGray);
            Box(chest, "Parts box", new Vector3(.2f, 2.3f, 0), new Vector3(.5f, .2f, .3f), leather);
            foreach (float a in new[] { 18f, 342f })
            {
                var p = Frame("Light pillar", Polar(a, 9.6f, FL), a + 180, c);
                Box(p, "Pillar", new Vector3(0, 3, 0), new Vector3(.55f, 6, .55f), navy, default, true);
                Box(p, "Pillar light", new Vector3(0, 3, .29f), new Vector3(.13f, 5.4f, .04f), blueGlow);
                Box(p, "Pillar foot", new Vector3(0, .04f, 0), new Vector3(.9f, .08f, .9f), navy);
            }
            foreach (var (p, yaw) in new[] { (Polar(225, 10.6f, 6.4f), 45f), (new Vector3(-1.6f, 6.4f, -1), 0f), (new Vector3(1.6f, 6.4f, -1), 0f) })
            {
                var h = Frame("Hanging lamp", p, yaw, c);
                Box(h, "Lamp board", Vector3.zero, new Vector3(2.6f, .06f, .5f), cream);
                Box(h, "Lamp glow", new Vector3(0, -.04f, 0), new Vector3(2.4f, .02f, .36f), glowWarm);
                for (int i = -1; i <= 1; i += 2) Cyl(h, "Lamp cable", new Vector3(i * 1.1f, 1.8f, 0), .012f, 3.6f, black);
            }

            // Navy chamfered frame on the Pipeline door, and door signs.
            const float zf = 12.92f;
            for (int i = -1; i <= 1; i += 2)
            {
                Strut(c, "Door frame", new Vector3(i * 2.75f, FL, zf), new Vector3(i * 2.75f, FL + 4.1f, zf), .3f, navy);
                Strut(c, "Door frame", new Vector3(i * 2.75f, FL + 4.1f, zf), new Vector3(i * 1.4f, FL + 5.3f, zf), .3f, navy);
            }
            Strut(c, "Door frame", new Vector3(-1.4f, FL + 5.3f, zf), new Vector3(1.4f, FL + 5.3f, zf), .3f, navy);
            Color sign = new Color(.75f, .85f, 1);
            Text(c, "PIPELINE  •  ACCELERATOR", new Vector3(0, FL + 6.2f, 12.9f), .32f, sign, 180);
            Text(c, "SPEED LAB", new Vector3(-12.9f, FL + 5.8f, 0), .32f, sign, 90);
            Text(c, "MED BAY  •  TIME VAULT", new Vector3(12.9f, FL + 5.8f, 0), .32f, sign, 270);
            Text(c, "LOBBY", new Vector3(0, FL + 5.8f, -12.9f), .32f, sign, 0);

            Lamp(new Vector3(0, 8.8f, -8), new Color(1.05f, .9f, .72f), 13);
            Lamp(new Vector3(-6.5f, 7.5f, -14), new Color(.85f, .78f, .7f), 9);
            Lamp(new Vector3(6.5f, 7.5f, -2), new Color(.85f, .78f, .7f), 9);
        }

        // Scales the supplied static Flash model to human height and stands it on the display base.
        static void PlaceExhibit(GameObject model, Transform display)
        {
            model.name = "Supplied Flash model - suit display";
            model.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds Measure()
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                return b;
            }
            model.transform.localScale *= 1.85f / Mathf.Max(.01f, Measure().size.y);
            var bounds = Measure();
            Vector3 stand = display.TransformPoint(new Vector3(0, .5f, 0));
            model.transform.position += new Vector3(stand.x - bounds.center.x, stand.y - bounds.min.y, stand.z - bounds.center.z);
        }

        void SpeedLab()
        {
            const float x0 = -56, x1 = -32, z0 = -28, z1 = 28, top = 15, cx = -44;
            // Corridor from the Cortex.
            Block("West corridor floor", -31.5f, 0, -10.5f, -14.1f, FL - Flush, -5.5f, floorDark);
            for (int i = -1; i <= 1; i += 2)
                Block("West corridor wall", -32, FL, -8 + i * 2.5f, -13.8f, FL + 5.4f, -8 + i * 3.5f, corridorWall);
            Block("West corridor ceiling", -32, FL + 5, -11.5f, -13.8f, FL + 5.4f, -4.5f, medCeiling);
            Block("West corridor light", -31, FL + 4.95f, -8.3f, -15, FL + 5, -7.7f, glowCool, false);
            Lamp(new Vector3(-23, FL + 4.3f, -8), new Color(.9f, .92f, .95f), 8);

            Block("Speed lab floor", x0 - .5f, 0, z0 - .5f, x1 + .5f, FL, z1 + .5f, concrete);
            WallZ("Speed lab wall", x0 - 1, x0, z0 - 1, z1 + 1, FL, top + .5f, labWall, Door(-3, 3, FL + 5));
            WallZ("Speed lab wall", x1, x1 + 1, z0 - 1, z1 + 1, FL, top + .5f, labWall, Door(-10.5f, -5.5f, FL + 5));
            WallX("Speed lab wall", z0 - 1, z0, x0 - 1, x1 + 1, FL, top + .5f, labWall);
            WallX("Speed lab wall", z1, z1 + 1, x0 - 1, x1 + 1, FL, top + .5f, labWall, Door(-50, -38, FL + 10));
            Block("Speed lab ceiling", x0 - 1, top, z0 - 1, x1 + 1, top + .5f, z1 + 1, ceilingDark);
            // Side passage straight into the accelerator ring.
            Block("Ring passage floor", -66.5f, 0, -3.5f, x0 - .5f, FL - Flush, 3.5f, floorDark);
            for (int i = -1; i <= 1; i += 2) Block("Ring passage wall", -66, FL, i * 3, x0 - .5f, FL + 5.4f, i * 4, corridorWall);
            Block("Ring passage ceiling", -66, FL + 5, -4, x0 - .5f, FL + 5.4f, 4, medCeiling);
            Block("Ring passage light", -64, FL + 4.95f, -.3f, -58, FL + 5, .3f, glowCool, false);

            // Runway markings.
            Block("Runway", cx - 3.4f, FL, z0, cx + 3.4f, FL + .012f, 33, runway, false);
            for (int i = -1; i <= 1; i += 2)
            {
                Block("Runway edge", cx + i * 3.4f - .2f, FL, z0, cx + i * 3.4f + .2f, FL + .016f, 33, runwayBlue, false);
                for (float z = -22; z < 26; z += 9) Block("Runway edge jog", cx + i * 3.6f - .45f, FL, z, cx + i * 3.6f + .45f, FL + .016f, z + 3.2f, runwayBlue, false);
            }
            Block("Runway centre", cx - .22f, FL, z0, cx + .22f, FL + .016f, 33, runwayGray, false);
            for (float z = -20; z < 26; z += 12) Block("Runway centre plate", cx - .5f, FL, z, cx + .5f, FL + .018f, z + 3, runwayGray, false);

            // Steel trusses, X-braced wall bays, lower grille and upper gallery.
            float[] trussZ = { -24, -16.5f, -9, -1.5f, 6, 13.5f, 21 };
            foreach (float z in trussZ)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    bool doorway = side < 0 ? Mathf.Abs(z) < 4 : z > -12 && z < -4;
                    float inward = -side, wallX = side < 0 ? x0 : x1;
                    if (!doorway) Truss(new Vector3(wallX + inward * .35f, FL, z), inward, top - FL);
                    Cyl(site, "Truss lamp", new Vector3(wallX + inward * 1.4f, FL + 11.6f, z), .2f, .28f, yellowGlow, new Vector3(0, 0, 90));
                }
                Block("Roof truss", x0, top - .7f, z - .2f, x1, top - .1f, z + .2f, steel, false);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float face = side < 0 ? x0 : x1, inward = -side;
                for (int i = 0; i < trussZ.Length - 1; i++)
                {
                    float za = trussZ[i] + .7f, zb = trussZ[i + 1] - .7f;
                    bool doorway = side < 0 ? za < 3 && zb > -3 : za < -5.5f && zb > -10.5f;
                    if (!doorway)
                    {
                        Strut(site, "Wall brace", new Vector3(face + inward * .1f, FL + 1.6f, za), new Vector3(face + inward * .1f, FL + 7.2f, zb), .16f, deskGray);
                        Strut(site, "Wall brace", new Vector3(face + inward * .1f, FL + 1.6f, zb), new Vector3(face + inward * .1f, FL + 7.2f, za), .16f, deskGray);
                    }
                    Block("Light bar", face, FL + 12.2f, za + .4f, face + inward * .25f, FL + 12.4f, zb - .4f, glowCool, false);
                }
                var doors = side < 0 ? Door(-3, 3, FL + 5) : Door(-10.5f, -5.5f, FL + 5);
                WallZ("Wall grille", face, face + inward * .12f, z0, z1, FL, FL + 1.4f, grille, doors);
                Block("Gallery glass", face, FL + 8, z0, face + inward * .15f, FL + 11.6f, z1, galleryGlass, false);
                Block("Gallery ledge", face, FL + 7.6f, z0, face + inward * 1.3f, FL + 8, z1, white, false);
                Block("Gallery ledge light", face + inward * 1, FL + 7.55f, z0, face + inward * 1.25f, FL + 7.6f, z1, glowCool, false);
                Block("Gallery light", face, FL + 11.45f, z0, face + inward * .22f, FL + 11.6f, z1, glowCool, false);
                for (float z = z0 + 2; z < z1; z += 4) Box(site, "Gallery mullion", new Vector3(face + inward * .18f, FL + 9.8f, z), new Vector3(.12f, 3.6f, .14f), steel);
            }
            foreach (float z in new[] { -20f, -7f, 6f, 19f }) Lamp(new Vector3(cx, 12.5f, z), new Color(1, .95f, .88f) * 1.05f, 15.5f);

            // Wind tunnel recess with a glowing arch and spinning fans.
            Block("Wind tunnel floor", -50, 0, z1 + .5f, -38, FL, 33.5f, runway);
            Block("Wind tunnel back", -51, FL, 33, -37, FL + 10.5f, 34, tunnelBack);
            for (int i = -1; i <= 1; i += 2) Block("Wind tunnel side", cx + i * 6, FL, z1, cx + i * 7, FL + 10.5f, 34, tunnelBack);
            Block("Wind tunnel roof", -51, FL + 10, z1, -37, FL + 10.5f, 34, tunnelBack);
            for (int i = -1; i <= 1; i += 2) Block("Arch rim", cx + i * 5.2f, FL, z1 - .1f, cx + i * 5.95f, FL + 4, z1 + .55f, blueGlow);
            for (int i = 0; i < 14; i++)
            {
                float a = (i + .5f) * 180 / 14;
                Vector3 p = new Vector3(cx + Mathf.Cos(a * Mathf.Deg2Rad) * 5.55f, FL + 4 + Mathf.Sin(a * Mathf.Deg2Rad) * 5.55f, z1 + .22f);
                Box(site, "Arch rim", p, new Vector3(.75f, 1.32f, .65f), blueGlow, new Vector3(0, 0, a));
            }
            for (float dx = -5.75f; dx < 5.8f; dx += .5f)
            {
                float arc = FL + 4 + Mathf.Sqrt(Mathf.Max(0, 36 - dx * dx));
                if (arc < FL + 9.98f) Block("Arch spandrel", cx + dx - .26f, arc, z1, cx + dx + .26f, FL + 10, z1 + 1, labWall);
            }
            foreach (var f in new[] { new Vector2(-3, 3.6f), new Vector2(3, 3.6f) })
                Fan(new Vector3(cx + f.x, FL + f.y, 32.8f), 180, 1.75f, 4, 220, black, fanBlade);
            foreach (var f in new[] { new Vector2(0, 7.1f), new Vector2(-2.1f, 6.3f), new Vector2(2.1f, 6.3f), new Vector2(0, 4.6f), new Vector2(0, 1.9f) })
                Fan(new Vector3(cx + f.x, FL + f.y, 32.8f), 180, .9f, 3, -300, black, fanBlade);
            Lamp(new Vector3(cx, FL + 4.5f, 31), new Color(.35f, .5f, 1.2f), 9);

            // Treadmill with sensor console, cables and coolant tanks.
            var t = Frame("Treadmill", new Vector3(-51, FL, -14), 0);
            Box(t, "Treadmill base", new Vector3(0, .32f, 0), new Vector3(2, .45f, 5), metal, default, true);
            Box(t, "Treadmill belt", new Vector3(0, .79f, 0), new Vector3(1.7f, .06f, 5.4f), cream);
            Box(t, "Console frame", new Vector3(0, 2.35f, 2.3f), new Vector3(2.5f, .6f, .16f), black);
            Box(t, "Tank plate", new Vector3(0, .04f, 3.75f), new Vector3(1.8f, .08f, 1.2f), metal);
            for (int i = -1; i <= 1; i += 2)
            {
                Cyl(t, "Treadmill roller", new Vector3(0, .42f, i * 2.5f), .38f, 2, metal, new Vector3(0, 0, 90));
                Box(t, "Belt stripes", new Vector3(i * .42f, .825f, 0), new Vector3(.36f, .02f, 5.4f), beltBlue);
                Box(t, "Treadmill rail", new Vector3(i * .95f, .9f, 0), new Vector3(.1f, .14f, 5.2f), metal);
                Box(t, "Console post", new Vector3(i * .75f, 1.5f, 2.3f), new Vector3(.12f, 1.6f, .12f), black);
                Cyl(t, "Console end", new Vector3(i * 1.25f, 2.35f, 2.3f), .3f, .16f, black, new Vector3(90, 0, 0));
                Cyl(t, "Coolant tank", new Vector3(i * .45f, .53f, 3.75f), .3f, .9f, black);
            }
            for (int i = -1; i <= 1; i++) Box(t, "Console readout", new Vector3(i * .62f, 2.35f, 2.21f), new Vector3(.36f, .2f, .02f), screen);
            for (int i = 0; i < 4; i++)
            {
                float x = -.45f + i * .3f;
                Material m = i % 2 == 0 ? black : runwayBlue;
                Strut(t, "Sensor cable", new Vector3(x, 2.1f, 2.38f), new Vector3(x * 1.2f, 1.1f, 3.15f), .035f, m);
                Strut(t, "Sensor cable", new Vector3(x * 1.2f, 1.1f, 3.15f), new Vector3(x < 0 ? -.45f : .45f, 1, 3.75f), .035f, m);
            }

            foreach (float z in new[] { -24f, 9.8f, 23f }) StarSign(new Vector3(-54.6f, FL, z), 90);
            foreach (float z in new[] { -22f, 4f, 16f })
            {
                var k = Frame("Speed console", new Vector3(-34.6f, FL, z), 270);
                Box(k, "Console platform", new Vector3(0, .17f, 0), new Vector3(3.4f, .34f, 2.6f), white, default, true);
                Box(k, "Console step", new Vector3(0, .085f, 1.55f), new Vector3(3.4f, .17f, .5f), white, default, true);
                Box(k, "Console body", new Vector3(0, .95f, -.45f), new Vector3(1.9f, 1.2f, .85f), white, default, true);
                Box(k, "Console stripe", new Vector3(0, 1.15f, -.45f), new Vector3(1.92f, .1f, .87f), amberStripe);
                Box(k, "Console deck", new Vector3(0, 1.58f, -.38f), new Vector3(1.9f, .06f, .8f), black, new Vector3(-12, 0, 0));
                Monitor(k, new Vector3(0, 1.6f, -.7f), 0, .9f);
                for (int i = -1; i <= 1; i += 2) Box(k, "Console rail", new Vector3(i * 1.55f, 1, -1.05f), new Vector3(.06f, 1.3f, .06f), metal);
                Box(k, "Console rail", new Vector3(0, 1.65f, -1.05f), new Vector3(3.1f, .06f, .06f), metal);
            }
            Block("Tool cabinet", -33.2f, FL, -27.7f, -32.05f, FL + 1.5f, -25.3f, drawerWhite);
            Block("Indicator panel", -36.2f, FL + 3, z0, -34.8f, FL + 4.3f, z0 + .08f, black, false);
            var leds = new[] { redGlow, ledGreen, blueGlow, ledAmber };
            for (int r = 0; r < 4; r++)
                for (int i = 0; i < 6; i++)
                    Box(site, "Indicator LED", new Vector3(-36 + i * .2f, FL + 3.2f + r * .28f, z0 + .1f), new Vector3(.1f, .1f, .03f), leds[(r + i) % 4]);
            Block("Wall screen frame", cx - 4.7f, FL + 5.8f, z0, cx + 4.7f, FL + 11.2f, z0 + .06f, steel, false);
            Block("Wall screen", cx - 4.5f, FL + 6, z0, cx + 4.5f, FL + 11, z0 + .1f, bigScreen, false);
            foreach (float x in new[] { -7.6f, -6.2f, 6.2f, 7.6f }) Ball(site, "Capsule lamp", new Vector3(cx + x, FL + 8.6f, z0 + .1f), new Vector3(.8f, 1.5f, .2f), orangeGlow);
            Block("Doorway surround", -54.4f, FL, z0, -50.6f, FL + 4.9f, z0 + .08f, black, false);
            Block("Lit doorway", -54, FL, z0 + .08f, -51, FL + 4.5f, z0 + .1f, beigeGlow, false);
        }

        void Truss(Vector3 p, float inward, float height)
        {
            for (int i = 0; i < 2; i++) Box(site, "Truss chord", p + new Vector3(inward * i * .8f, height / 2, 0), new Vector3(.22f, height, .22f), steel);
            const int n = 7;
            for (int i = 0; i < n; i++)
                Strut(site, "Truss lattice", p + new Vector3(inward * (i % 2) * .8f, i * height / n, 0),
                    p + new Vector3(inward * ((i + 1) % 2) * .8f, (i + 1) * height / n, 0), .1f, steel);
        }

        void EastWing()
        {
            // Corridor from the Cortex to the ring; the med bay, workshop and vault open off it.
            Block("East corridor floor", 14.1f, 0, -11.5f, 66, FL - Flush, -4.5f, floorDark);
            WallX("East corridor wall", -11.5f, -11, 13.8f, 64.6f, FL, FL + 5.4f, corridorWall, Door(30, 34, FL + 4.2f), Door(48, 51.5f, FL + 4.2f));
            WallX("East corridor wall", -5, -4.5f, 13.8f, 64.8f, FL, FL + 5.4f, corridorWall, Door(33, 37, FL + 4.2f));
            Block("East corridor ceiling", 13.8f, FL + 5, -12, 65.5f, FL + 5.4f, -4, medCeiling);
            for (float x = 18; x < 62; x += 6) Block("Corridor light", x - .6f, FL + 4.95f, -8.9f, x + .6f, FL + 5, -7.1f, glowCool, false);
            foreach (float x in new[] { 24f, 40f, 56f }) Lamp(new Vector3(x, FL + 4.3f, -8), new Color(.95f, .95f, .95f), 9);
            Color sign = new Color(.15f, .2f, .3f);
            Text(site, "MED BAY", new Vector3(32, FL + 4.65f, -10.95f), .34f, sign, 0);
            Text(site, "LAB 2", new Vector3(49.75f, FL + 4.65f, -10.95f), .34f, sign, 0);
            Text(site, "TIME VAULT", new Vector3(35, FL + 4.65f, -5.05f), .34f, sign, 180);

            // Med bay: scanner ring, bed on tracks, surgical lamps and monitors.
            Block("Med bay floor", 19, 0, -31, 44.5f, FL, -11.5f, medFloor);
            WallX("Med bay wall", -12, -11.5f, 19, 44.5f, FL, 10.4f, medWall, Door(30, 34, FL + 4.2f));
            WallX("Med bay wall", -31, -30, 19, 44.5f, FL, 10.4f, medWall);
            WallZ("Med bay wall", 19, 20, -31, -11, FL, 10.4f, medWall);
            WallZ("Med bay partition", 44, 44.5f, -30, -12, FL, 10.4f, medWall, Door(-27, -15, FL + 3.6f, FL + 1));
            Box(site, "Partition window", new Vector3(44.25f, FL + 2.3f, -21), new Vector3(.06f, 2.6f, 12), clearGlass, default, true);
            Block("Med bay ceiling", 19, 10, -31, 44.5f, 10.4f, -11.5f, medCeiling);

            var scanner = new Vector3(30, FL + 3.6f, -29.3f);
            Vector3 vertical = new Vector3(90, 0, 0);
            Cyl(site, "Scanner housing", scanner, 3.6f, 2.2f, scannerHousing, vertical, true);
            Shape(site, "Scanner ring", LabMesh.Tube(2.55f, .32f), deskGray, scanner + Vector3.forward * 1.1f, euler: vertical);
            Cyl(site, "Scanner core", scanner + Vector3.forward * 1.06f, 2.25f, .1f, scannerCore, vertical);
            Shape(site, "Scanner inner ring", LabMesh.Tube(1, .07f), blueGlow, scanner + Vector3.forward * 1.14f, euler: vertical);
            Shape(site, "Scanner outer ring", LabMesh.Tube(2.95f, .05f), blueGlow, scanner + Vector3.forward * 1.12f, euler: vertical);
            foreach (float a in new[] { 35f, 62f, 215f, 242f })
                Box(site, "Scanner marker", scanner + Quaternion.Euler(0, 0, a) * Vector3.up * 3.25f + Vector3.forward * 1.11f, new Vector3(.12f, .8f, .04f), glowCool, new Vector3(0, 0, a));
            Lamp(scanner + new Vector3(0, 0, 2.5f), new Color(.55f, .8f, 1.1f), 8);

            Block("Bed bay", 27.6f, FL, -28.2f, 32.4f, FL + .008f, -16, deskGray, false);
            foreach (float x in new[] { 29.32f, 30.68f }) Block("Scanner track", x - .12f, FL, -28.2f, x + .12f, FL + .012f, -15, black, false);
            var bed = Frame("Med bed", new Vector3(30, FL, -22.5f), 0);
            Box(bed, "Bed frame", new Vector3(0, .42f, 0), new Vector3(.86f, .4f, 2.2f), black, default, true);
            Box(bed, "Bed cushion", new Vector3(0, .72f, .35f), new Vector3(.82f, .2f, 1.5f), leather);
            Box(bed, "Bed head", new Vector3(0, .9f, -.82f), new Vector3(.82f, .2f, .72f), white, new Vector3(22, 0, 0));
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(bed, "Bed leg", new Vector3(x * .35f, .14f, z * .9f), new Vector3(.08f, .28f, .08f), metal);
                    Cyl(bed, "Bed wheel", new Vector3(x * .35f, .07f, z * .9f), .07f, .06f, black, new Vector3(0, 0, 90));
                }
            var rig = new Vector3(30, 9.3f, -22.5f);
            Shape(site, "Surgical ring", LabMesh.Tube(3.1f, .24f), black, rig);
            Shape(site, "Surgical ring light", LabMesh.Arc(0, 2.85f, -.05f, -.01f, 0, 360, 48), glowCool, rig);
            Cyl(site, "Surgical column", rig + Vector3.down * .7f, .14f, 1.4f, white);
            foreach (var arm in new[] { new Vector3(1.7f, 0, .7f), new Vector3(-1.5f, 0, -1.1f) })
            {
                Vector3 joint = rig + Vector3.down * 1.3f, head = joint + arm + Vector3.down * .7f;
                Strut(site, "Lamp arm", joint, joint + arm, .1f, white);
                Strut(site, "Lamp arm", joint + arm, head, .08f, white);
                Cyl(site, "Lamp head", head + Vector3.up * .08f, .55f, .06f, deskGray);
                for (int i = 0; i < 3; i++) Ball(site, "Lamp bulb", head + Quaternion.Euler(0, i * 120, 0) * Vector3.forward * .28f, new Vector3(.36f, .12f, .36f), glowWarm);
            }
            foreach (float z in new[] { -26f, -21f, -16f })
            {
                Block("Screen backlight", 20, FL + 2.66f, z - .95f, 20.04f, FL + 3.84f, z + .95f, blueGlow, false);
                Monitor(site, new Vector3(20.08f, FL + 2.75f, z), 90, 1.7f, false);
            }
            Block("Med desk", 20.1f, FL + .74f, -27, 21.1f, FL + .8f, -15, white);
            foreach (float z in new[] { -26.6f, -15.4f }) Block("Desk leg", 20.2f, FL, z - .05f, 21, FL + .74f, z + .05f, black);
            foreach (float z in new[] { -24f, -18.5f })
            {
                Monitor(site, new Vector3(20.5f, FL + .8f, z), 90, .75f);
                Chair(new Vector3(21.9f, FL, z), 270, white);
            }
            Block("Med desk", 36, FL + .74f, -12.9f, 43, FL + .8f, -12.1f, white);
            Monitor(site, new Vector3(39.5f, FL + .8f, -12.4f), 180, .75f);
            Chair(new Vector3(39.5f, FL, -13.6f), 0, cartBlue);
            var cart = Frame("Medical cart", new Vector3(23.5f, FL, -29.3f), 0);
            Box(cart, "Cart body", new Vector3(0, .55f, 0), new Vector3(1.2f, .8f, .6f), cartBlue, default, true);
            for (int i = 0; i < 3; i++) Box(cart, "Cart drawer", new Vector3(0, .32f + i * .24f, .31f), new Vector3(1.05f, .17f, .02f), white);
            Box(cart, "Cart top", new Vector3(0, .98f, 0), new Vector3(1.3f, .05f, .66f), white);
            var shelves = Frame("Med shelves", new Vector3(39.5f, FL, -29.75f), 0);
            for (int i = 0; i < 3; i++)
            {
                float y = 1.4f + i * .7f;
                Box(shelves, "Wall shelf", new Vector3(0, y, 0), new Vector3(5.6f, .05f, .4f), white);
                Box(shelves, "Shelf glow", new Vector3(0, y - .03f, .2f), new Vector3(5.6f, .02f, .02f), blueGlow);
                Bottles(shelves, new Vector3(-2.5f, y + .03f, 0), new Vector3(.36f, 0, 0), 14, 20 + i);
            }
            SampleCart(new Vector3(41.6f, FL, -17), 270);
            Lamp(new Vector3(25, 8.8f, -16), new Color(.88f, .93f, 1), 12);
            Lamp(new Vector3(38, 8.8f, -25), new Color(.88f, .93f, 1), 12);

            // Workshop (lab 2): light table, sample cart and wire shelving behind the med bay glass.
            Block("Workshop floor", 44.5f, 0, -31, 57, FL, -11.5f, workshopFloor);
            WallX("Workshop wall", -12, -11.5f, 44.5f, 57, FL, 10.4f, darkWall, Door(48, 51.5f, FL + 4.2f));
            WallX("Workshop wall", -31, -30, 44.5f, 57, FL, 10.4f, darkWall);
            WallZ("Workshop wall", 56, 57, -31, -11, FL, 10.4f, darkWall);
            Block("Workshop ceiling", 44.5f, 10, -31, 57, 10.4f, -11.5f, ceilingDark);
            foreach (float x in new[] { 47.5f, 50.5f, 53.5f }) Cyl(site, "Downlight", new Vector3(x, 9.97f, -29.2f), .14f, .04f, glowWarm);
            var table = Frame("Light table", new Vector3(50, FL, -21), 0);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Box(table, "Table leg", new Vector3(x * .7f, .45f, z * .45f), new Vector3(.05f, .9f, .05f), metal);
            Box(table, "Light box", new Vector3(0, 1, 0), new Vector3(1.6f, .3f, 1.05f), deskGray, default, true);
            Box(table, "Light surface", new Vector3(0, 1.16f, 0), new Vector3(1.5f, .02f, .95f), glowCool);
            Lamp(new Vector3(50, FL + 2.2f, -21), new Color(.9f, .92f, .95f) * .8f, 5);
            SampleCart(new Vector3(53.8f, FL, -14.2f), 180);
            Shelf(new Vector3(55.4f, FL, -24), 270, 3, 2.4f, 5, wire);
            Box(site, "Storage bin", new Vector3(55.4f, FL + 2.46f, -23), new Vector3(.6f, .3f, .45f), cartBlue);
            Block("Exam bed", 46, FL, -29.6f, 49.5f, FL + .7f, -28.6f, leather);
            Lamp(new Vector3(50, 8.6f, -21), new Color(1, .9f, .78f) * .85f, 11);

            // Time vault: studded walls, cove lighting, faceted sculpture and the vault alcove.
            Block("Vault floor", 19, 0, -4.5f, 51, FL, 21, gloss);
            WallX("Vault wall", -4.5f, -4, 19, 51, FL, 10.4f, studs, Door(33, 37, FL + 4.2f));
            WallZ("Vault wall", 19, 20, -4.5f, 21, FL, 10.4f, studs);
            WallZ("Vault wall", 50, 51, -4.5f, 21, FL, 10.4f, studs);
            WallX("Vault wall", 20, 21, 19, 51, FL, 10.4f, studs, Door(26, 34, FL + 6.5f));
            Block("Vault ceiling", 19, 10, -4.5f, 51, 10.4f, 21, ceilingDark);
            Block("Cove light", 20, FL + 7.6f, -3.95f, 50, FL + 7.7f, -3.75f, glowCool, false);
            Block("Cove light", 20.05f, FL + 7.6f, -4, 20.25f, FL + 7.7f, 20, glowCool, false);
            Block("Cove light", 49.75f, FL + 7.6f, -4, 49.95f, FL + 7.7f, 20, glowCool, false);
            foreach (var (xa, xb) in new[] { (20f, 26f), (34f, 50f) }) Block("Cove light", xa, FL + 7.6f, 19.75f, xb, FL + 7.7f, 19.95f, glowCool, false);
            Block("Cove soffit", 20, FL + 7.7f, -4, 50, FL + 8, -3.4f, ceilingDark, false);
            Block("Cove soffit", 20, FL + 7.7f, 19.4f, 50, FL + 8, 20, ceilingDark, false);
            Block("Cove soffit", 20, FL + 7.7f, -4, 20.6f, FL + 8, 20, ceilingDark, false);
            Block("Cove soffit", 49.4f, FL + 7.7f, -4, 50, FL + 8, 20, ceilingDark, false);
            Shape(site, "Vault skylight", LabMesh.Arc(23.5f, 25.5f, 9.93f, 9.98f, 205, 258), glowCool, new Vector3(56, 0, 24));
            var sc = Frame("Vault sculpture", new Vector3(25.5f, FL, 12), 20);
            Box(sc, "Sculpture facet", new Vector3(0, 1.75f, 0), new Vector3(1.6f, 3.5f, 1.2f), sculpture, new Vector3(4, 25, 7), true);
            Box(sc, "Sculpture facet", new Vector3(.15f, 4.7f, .07f), new Vector3(1.35f, 2.9f, 1), sculpture, new Vector3(-6, -18, -9));
            Box(sc, "Sculpture facet", new Vector3(-.07f, 6.6f, 0), new Vector3(1, 1.7f, .8f), sculpture, new Vector3(9, 40, 14));
            var pc = Frame("Vault console", new Vector3(44.5f, FL, 11), 230);
            Box(pc, "Console stem", new Vector3(0, .55f, 0), new Vector3(.36f, 1.1f, .3f), sculpture, new Vector3(0, 15, 0), true);
            Box(pc, "Console stem", new Vector3(0, .95f, 0), new Vector3(.3f, .5f, .38f), sculpture, new Vector3(0, -20, 0));
            Box(pc, "Console top", new Vector3(0, 1.2f, 0), new Vector3(.6f, .12f, .5f), sculpture, new Vector3(-10, 0, 0));
            Cyl(pc, "Console reader", new Vector3(0, 1.27f, .02f), .1f, .02f, glowCool, new Vector3(-10, 0, 0));
            foreach (float x in new[] { 32.85f, 37.15f })
                for (float y = FL + .3f; y < FL + 4.1f; y += .3f)
                    Box(site, "Door light", new Vector3(x, y, -3.96f), new Vector3(.05f, .05f, .03f), blueGlow);

            Block("Alcove floor", 25, 0, 20, 35, FL + .15f, 25.5f, white);
            Block("Alcove wall", 25, FL, 21, 26, FL + 7, 25.5f, foam);
            Block("Alcove wall", 34, FL, 21, 35, FL + 7, 25.5f, foam);
            Block("Alcove wall", 25, FL, 24.5f, 35, FL + 7, 25.5f, foam);
            Block("Alcove ceiling", 25, FL + 6.5f, 21, 35, FL + 7, 25.5f, foam);
            Block("Alcove panel", 29.2f, FL + .15f, 24.38f, 33.8f, FL + 6.4f, 24.5f, white, false);
            foreach (float y in new[] { 4.7f, 1.9f })
            {
                var p = new Vector3(31.5f, FL + y, 24.32f);
                Cyl(site, "Vault window", p, .95f, .06f, black, vertical);
                Shape(site, "Vault window rim", LabMesh.Tube(.95f, .07f), blueGlow, p + Vector3.back * .04f, euler: vertical);
                Box(site, "Vault window shelf", p + new Vector3(0, -.05f, -.05f), new Vector3(1.7f, .05f, .08f), white);
            }
            Box(site, "Vault interface", new Vector3(31.5f, FL + 3.3f, 24.2f), new Vector3(2.5f, .42f, .22f), black);
            for (int i = -1; i <= 1; i += 2)
                Box(site, "Vault interface", new Vector3(31.5f + i * 1.45f, FL + 3.3f, 24.2f), new Vector3(.6f, .42f, .22f), black, new Vector3(0, 0, i * 30));
            Ball(site, "Vault interface light", new Vector3(31.5f, FL + 3.3f, 24.06f), Vector3.one * .2f, redGlow);
            Cyl(site, "Chamber base", new Vector3(27.9f, FL + .5f, 22.6f), 1.3f, .7f, white, default, true);
            Cyl(site, "Chamber glass", new Vector3(27.9f, FL + 3.1f, 22.6f), 1.2f, 4.5f, clearGlass, default, true);
            Cyl(site, "Chamber cap", new Vector3(27.9f, FL + 5.6f, 22.6f), 1.35f, .5f, deskGray);
            Cyl(site, "Chamber light", new Vector3(27.9f, FL + 5.33f, 22.6f), 1.1f, .04f, glowCool);
            foreach (float x in new[] { 26.4f, 29.4f }) Block("Chamber post", x - .07f, FL, 21.1f, x + .07f, FL + 6.5f, 21.25f, black);
            foreach (float x in new[] { 27f, 33f }) Block("Alcove step", x - .6f, FL + .15f, 21.1f, x + .6f, FL + .4f, 21.7f, white);
            Lamp(new Vector3(30, FL + 5.2f, 22.8f), new Color(.35f, .5f, 1.15f), 7);
            foreach (var p in new[] { new Vector3(22, 8.6f, 8), new Vector3(48, 8.6f, 8), new Vector3(35, 8.6f, -2.2f), new Vector3(35, 8.6f, 18.2f) })
                Lamp(p, new Color(1, 1, 1.05f), 14);
        }

        void SampleCart(Vector3 p, float yaw)
        {
            var t = Frame("Sample cart", p, yaw);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Box(t, "Cart post", new Vector3(x * .6f, .5f, z * .3f), new Vector3(.04f, 1, .04f), white);
            for (int i = 0; i < 3; i++) Box(t, "Cart shelf", new Vector3(0, .15f + i * .38f, 0), new Vector3(1.3f, .04f, .66f), white);
            Box(t, "Tube rack", new Vector3(-.25f, 1.02f, 0), new Vector3(.6f, .14f, .2f), white);
            for (int i = 0; i < 6; i++) Cyl(t, "Test tube", new Vector3(-.5f + i * .1f, 1.12f, 0), .025f, .26f, tube);
            Cyl(t, "Reagent jug", new Vector3(.35f, 1.15f, 0), .12f, .32f, yellow);
            Bottles(t, new Vector3(-.5f, .57f, 0), new Vector3(.2f, 0, 0), 6, (int)(p.x * 7));
            Bottles(t, new Vector3(-.5f, .19f, 0), new Vector3(.25f, 0, 0), 5, (int)(p.z * 3));
        }

        // Red magnet corridor from the Cortex to the accelerator chamber, ending in the rolling vault door.
        void AccessCorridor()
        {
            const float z0 = 4.6f, z1 = 32;
            Block("Access walkway", -3, 0, 6.1f, 3, FL - Flush, z1, floorBlack);
            for (int i = -1; i <= 1; i += 2)
            {
                Block("Access trench", i * 3, 0, 6.1f, i * 4.5f, FL - .25f, z1, trenchDark);
                Block("Trench light", i * 3.75f - .05f, FL - .25f, 6.1f, i * 3.75f + .05f, FL - .23f, z1, blueGlow, false);
                Block("Walkway edge", i * 2.98f - .1f, FL, 6.1f, i * 2.98f + .1f, FL + .012f, z1, hazard, false);
                Block("Access wall", i * 4.5f, FL - .25f, z0, i * 5.5f, FL + 7.4f, z1, redBright);
                float mid = (z0 + z1) / 2, len = z1 - z0;
                Cyl(site, "Access pipe", new Vector3(i * 3.95f, FL + 1.05f, mid), .2f, len, pipeGray, new Vector3(90, 0, 0));
                Cyl(site, "Access pipe", new Vector3(i * 3, FL + 5.4f, mid), .15f, len, pipeGray, new Vector3(90, 0, 0));
                Cyl(site, "Overhead pipe", new Vector3(i * .95f, FL + 6.55f, mid), .2f, len, black, new Vector3(90, 0, 0));
                for (int k = 0; k < 8; k++)
                    Block("Cable", i * 4.42f - .06f, FL + 2.3f + k * .15f, z0, i * 4.42f + .06f, FL + 2.42f + k * .15f, z1, cableColors[k % 4], false);
                for (float z = 9.1f; z < z1; z += 3.25f)
                {
                    Box(site, "Cable clamp", new Vector3(i * 4.3f, FL + 2.85f, z), new Vector3(.05f, 1.45f, .06f), plate);
                    for (int e = -1; e <= 1; e += 2) Box(site, "Cable clamp", new Vector3(i * 4.38f, FL + 2.85f + e * .7f, z), new Vector3(.16f, .05f, .06f), plate);
                }
                for (int k = 0; k < 3; k++)
                    Box(site, "Hanging cable", new Vector3(i * 4.36f, FL + 3.5f, 12 + k * 7.3f), new Vector3(.05f, 7, .05f), k % 2 == 0 ? black : cableColors[0]);
            }
            Block("Access ceiling", -5.5f, FL + 7, z0, 5.5f, FL + 7.4f, z1, redDark);
            Block("Access skylight", -1.2f, FL + 6.96f, z0 + .9f, 1.2f, FL + 7, z1 - .5f, glowCool, false);
            int frame = 0;
            for (float z = 7.5f; z < z1; z += 3.25f, frame++)
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    Box(site, "Access frame", new Vector3(i * 4.15f, FL + 3.4f, z), new Vector3(.7f, 7.2f, .9f), redBright, default, true);
                    Box(site, "Access brace", new Vector3(i * 3.3f, FL + 6.1f, z), new Vector3(2.5f, .6f, .9f), redBright, new Vector3(0, 0, -i * 45));
                    Box(site, "Label plate", new Vector3(i * 3.78f, FL + 1.75f, z), new Vector3(.04f, .32f, .82f), plate);
                    Text(site, "0110-11", new Vector3(i * 3.75f, FL + 1.75f, z), .2f, Color.black, i < 0 ? 90 : 270);
                    if (frame % 2 != 0) continue;
                    var label = Frame("Magnet label", new Vector3(i * 3.3f, FL + 6.1f, z - .47f), 0);
                    label.localRotation = Quaternion.Euler(0, 0, -i * 45);
                    Box(label, "Label plate", Vector3.zero, new Vector3(1.5f, .44f, .02f), yellow);
                    Box(label, "Label stripe", new Vector3(-.6f, 0, -.012f), new Vector3(.25f, .44f, .01f), hazard);
                    Text(label, "MAG RES\n0305-80", new Vector3(.12f, 0, -.015f), .15f, Color.black, 180);
                }
                Box(site, "Access beam", new Vector3(0, FL + 6.85f, z), new Vector3(6.2f, .5f, .9f), redBright);
            }
            for (float z = 9.1f; z < z1 - 2; z += 6.5f)
            {
                Block("Light panel", 4.42f, FL + 3.7f, z - 1.1f, 4.48f, FL + 5.3f, z + 1.1f, glowWarm, false);
                for (int k = 0; k < 4; k++) Block("Panel slat", 4.36f, FL + 3.9f + k * .4f, z - 1.15f, 4.42f, FL + 3.98f + k * .4f, z + 1.15f, black, false);
            }
            foreach (float z in new[] { 10f, 19.5f, 28f }) Lamp(new Vector3(0, FL + 5.4f, z), new Color(1, .9f, .82f), 9);

            // End wall (shared with the chamber) and the vault door that rolls into the wall.
            WallX("Chamber south wall", 32, 32.5f, -19, 19, 0, 16.5f, accelWall, Door(-2.1f, 2.1f, FL + 5.2f, FL));
            WallX("Chamber south wall", 32.5f, 33, -19, 19, 0, 16.5f, chamberRed, Door(-2.1f, 2.1f, FL + 5.2f, FL));
            Vector3 vertical = new Vector3(90, 0, 0), centre = new Vector3(0, FL + 2.8f, 31.72f);
            var door = Frame("Accelerator vault door", centre, 0, animated);
            Cyl(door, "Vault door", Vector3.zero, 3.6f, .44f, doorMetal, vertical);
            Shape(door, "Door rim", LabMesh.Tube(3.45f, .14f), pipeGray, new Vector3(0, 0, -.22f), euler: vertical);
            Shape(door, "Door hub", LabMesh.Tube(1.15f, .13f), pipeGray, new Vector3(0, 0, -.24f), euler: vertical);
            Cyl(door, "Door core", new Vector3(0, 0, -.26f), .6f, .08f, black, vertical);
            for (int i = 0; i < 6; i++)
                Box(door, "Door spoke", Quaternion.Euler(0, 0, i * 60) * Vector3.up * 2.3f + Vector3.back * .25f, new Vector3(.24f, 2.2f, .06f), pipeGray, new Vector3(0, 0, i * 60));
            door.gameObject.AddComponent<BoxCollider>().size = new Vector3(5.2f, 5.6f, .44f);
            door.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            vaultDoor = door;
            vaultDoorClosed = door.localPosition;
            for (int k = 0; k < 8; k++) Shape(site, "Door glow", LabMesh.Tube(3.95f, .09f, k * 45 + 6, k * 45 + 39), doorRing, centre + Vector3.forward * .18f, euler: vertical);
            for (int k = 0; k < 12; k++) Shape(site, "Door glow", LabMesh.Tube(4.5f, .07f, k * 30 + 4, k * 30 + 24), doorRing, centre + Vector3.forward * .2f, euler: vertical);
            Shape(site, "Door glow", LabMesh.Tube(3.4f, .08f), doorRing, new Vector3(0, FL + 2.8f, 33.05f), euler: vertical);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(site, "Bay plate", new Vector3(i * 3.3f, FL + 3.8f, 31.97f), new Vector3(.5f, .5f, .03f), plate);
                Text(site, "52", new Vector3(i * 3.3f, FL + 3.8f, 31.94f), .3f, new Color(.7f, .1f, .08f), 180);
            }
            Lamp(new Vector3(0, FL + 3, 29.5f), new Color(.3f, .62f, 1.2f), 7);
        }

        void Chamber()
        {
            const float x0 = -18, x1 = 18, z1 = 62, pit = .3f, top = 16;
            Block("Chamber pit", x0 - 1, 0, 39, x1 + 1, pit, z1 + .5f, hexTiles);
            Block("Chamber balcony", x0 - 1, 0, 33, x1 + 1, FL, 39, grating);
            WallZ("Chamber wall", x0 - 1, x0, 32, z1 + 1, 0, top + .5f, chamberRed);
            WallZ("Chamber wall", x1, x1 + 1, 32, z1 + 1, 0, top + .5f, chamberRed);
            WallX("Chamber north wall", z1, z1 + 1, x0 - 1, x1 + 1, 0, top + .5f, chamberRed, Door(-3, 3, FL + 6.2f, FL));
            Block("Chamber ceiling", x0 - 1, top, 32, x1 + 1, top + .5f, z1 + 1, chamberCeiling);
            Block("Floor drain", x0, pit, 44.6f, x1, pit + .01f, 45.4f, grating, false);

            // Balcony, stairs down into the pit, walkway and the lit steps up to the containment platform.
            const int steps = 7;
            float rise = (FL - pit) / steps, run = .45f;
            for (int k = 1; k < steps; k++)
                Block("Balcony stair", -1.8f, 0, 39 + (k - 1) * run, 1.8f, FL - k * rise, 39 + k * run, grating);
            for (int i = -1; i <= 1; i += 2)
            {
                Strut(site, "Stair rail", new Vector3(i * 1.95f, FL + 1.05f, 38.9f), new Vector3(i * 1.95f, pit + 1.05f, 41.9f), .07f, yellow);
                Box(site, "Stair post", new Vector3(i * 1.95f, pit + .55f, 41.9f), new Vector3(.07f, 1.1f, .07f), yellow);
                Railing(new Vector3(i * 1.95f, FL, 38.9f), new Vector3(i * 17.8f, FL, 38.9f), yellow, 1.1f, true);
                Box(site, "Monitor post", new Vector3(i * 4.8f, FL + .55f, 38.4f), new Vector3(.08f, 1.1f, .08f), black);
                Monitor(site, new Vector3(i * 4.8f, FL + 1.1f, 38.4f), 180, 1, false);
            }
            Block("Chamber walkway", -1.8f, pit, 41.7f, 1.8f, pit + .012f, 46.85f, deskGray, false);
            Block("Walkway stripe", -.2f, pit, 41.7f, .2f, pit + .016f, 46.85f, plate, false);
            var platform = new Vector3(0, 0, 55);
            Shape(site, "Containment platform", LabMesh.Arc(0, 5.41f, 0, FL, 22.5f, 382.5f, 8), black, platform, true);
            Shape(site, "Platform grating", LabMesh.Arc(0, 4.75f, FL, FL + .015f, 22.5f, 382.5f, 8), grating, platform);
            Shape(site, "Platform rim", LabMesh.Arc(4.75f, 5.41f, FL, FL + .02f, 22.5f, 382.5f, 8), deskGray, platform);
            for (int k = 1; k <= steps; k++)
            {
                float stepTop = pit + k * rise, z = 46.85f + (k - 1) * run;
                Block("Platform step", -2.3f, 0, z, 2.3f, stepTop, 50.2f, black);
                for (int i = -1; i <= 1; i += 2) Box(site, "Step light", new Vector3(i * .95f, stepTop - .12f, z - .012f), new Vector3(.75f, .07f, .02f), glowCool);
            }
            Block("Portal bridge", -2.6f, 0, 59, 2.6f, FL, z1, black);
            Block("Bridge grating", -2.4f, FL, 59.5f, 2.4f, FL + .015f, z1 - .05f, grating, false);

            // North wall: octagonal portal into the ring, glowing containment grids and radial fans.
            float face = z1 - .06f;
            OctagonFrame(new Vector3(0, FL + 2.75f, face), 3.9f, .7f, white);
            OctagonFrame(new Vector3(0, FL + 2.75f, face + .02f), 4.9f, .9f, chamberRedBright);
            Block("Portal light panel", -1.6f, FL + 6.9f, z1 - .1f, 1.6f, FL + 8.9f, z1, black, false);
            for (int r = 0; r < 4; r++)
                for (int i = -1; i <= 1; i += 2)
                    Box(site, "Portal light", new Vector3(i * .6f, FL + 7.25f + r * .45f, z1 - .12f), new Vector3(.75f, .14f, .03f), glowCool);
            Block("Portal passage floor", -4.3f, 0, z1, 4.3f, FL - Flush, 66.6f, black);
            for (int i = -1; i <= 1; i += 2) Block("Portal passage wall", i * 3, FL, z1 + 1, i * 4.3f, FL + 6.6f, 65.8f, portalBlue);
            Block("Portal passage ceiling", -4.3f, FL + 6.2f, z1 + 1, 4.3f, FL + 6.6f, 65.8f, portalBlue);
            Lamp(new Vector3(0, FL + 3.4f, 64.3f), new Color(.3f, .5f, 1.25f), 8);
            for (int i = -1; i <= 1; i += 2)
            {
                for (int k = 0; k < 3; k++)
                {
                    var g = Frame("Containment grid", new Vector3(i * (5.4f + k * 3.05f), pit, z1 - .55f - k * .45f), i * (6 + k * 9));
                    Box(g, "Grid glass", new Vector3(0, 3.9f, 0), new Vector3(3.1f, 7.2f, .12f), grid);
                    Box(g, "Grid sill", new Vector3(0, .25f, .1f), new Vector3(3.1f, .5f, .5f), white);
                }
                Vector3 hub = new Vector3(i * 15.2f, pit + 4.2f, z1 - .3f);
                OctagonFrame(hub, 2.75f, .55f, chamberRedBright);
                Shape(site, "Fan ring", LabMesh.Tube(2.35f, .12f), white, hub, euler: new Vector3(90, 0, 0));
                Shape(site, "Fan hub ring", LabMesh.Tube(.62f, .07f), yellowGlow, hub + Vector3.back * .1f, euler: new Vector3(90, 0, 0));
                Fan(hub + Vector3.back * .05f, 180, 2.3f, 24, i * 40, black, white);
            }

            // West booth, east observation deck and booth, ladders.
            Booth(x0, x0 + 5, 42, 52, pit, FL + 5);
            Booth(x1, x1 - 5, 50, 58, pit, FL + 5);
            const float deckTop = 1.8f;
            Block("Observation deck", 9, 0, 40, x1, deckTop, 47, black);
            Block("Observation grating", 9, deckTop, 40, x1, deckTop + .015f, 47, grating, false);
            for (int k = 1; k <= 5; k++) Block("Observation stair", 9 - k * .45f, 0, 42, 9 - (k - 1) * .45f, deckTop - k * .25f, 45, black);
            Railing(new Vector3(9, deckTop, 40.05f), new Vector3(17.8f, deckTop, 40.05f), yellow, 1.1f, true);
            Railing(new Vector3(9, deckTop, 46.95f), new Vector3(17.8f, deckTop, 46.95f), yellow, 1.1f, true);
            Railing(new Vector3(9.05f, deckTop, 40), new Vector3(9.05f, deckTop, 42), yellow, 1.1f, true);
            Railing(new Vector3(9.05f, deckTop, 45), new Vector3(9.05f, deckTop, 47), yellow, 1.1f, true);
            foreach (float z in new[] { 41.3f, 45.7f })
            {
                Box(site, "Monitor post", new Vector3(13.8f, deckTop + .55f, z), new Vector3(.08f, 1.1f, .08f), black);
                Monitor(site, new Vector3(13.8f, deckTop + 1.1f, z), 270, .9f, false);
            }
            for (int i = -1; i <= 1; i += 2)
            {
                Ladder(new Vector3(i * 17.2f, FL, 36.5f), top - FL, 90, yellow);
                Ladder(new Vector3(i * 17.2f, pit, 60.6f), top - pit, 90, yellow);
            }

            // Ceiling light rig over the platform.
            var rigCentre = new Vector3(0, top - .9f, 55);
            Shape(site, "Light rig", LabMesh.Tube(4.6f, .22f), black, rigCentre);
            for (int k = 0; k < 8; k++) Box(site, "Rig lamp", rigCentre + Dir(k * 45) * 4.6f + Vector3.down * .25f, new Vector3(.5f, .2f, .5f), glowCool);
            for (int k = 0; k < 4; k++)
                Strut(site, "Rig cable", rigCentre + Dir(k * 90 + 45) * 4.6f, rigCentre + Dir(k * 90 + 45) * 4.6f + Vector3.up * .9f, .04f, black);
            foreach (float x in new[] { -12f, -4f, 4f, 12f })
                foreach (float z in new[] { 37f, 45f })
                    Cyl(site, "Ceiling spot", new Vector3(x, top - .03f, z), .25f, .05f, glowCool);

            Lamp(new Vector3(0, 13.5f, 54), new Color(1, .94f, .9f) * 1.1f, 17);
            Lamp(new Vector3(0, 9.5f, 40), new Color(1, .9f, .85f) * .9f, 12);
            Lamp(new Vector3(-12, 9, 51), new Color(1, .82f, .78f) * .8f, 11);
            Lamp(new Vector3(12, 9, 51), new Color(1, .82f, .78f) * .8f, 11);
        }

        // Octagonal band of eight boxes facing -z, centred on c.
        void OctagonFrame(Vector3 c, float radius, float width, Material m)
        {
            float side = 2 * radius * Mathf.Tan(22.5f * Mathf.Deg2Rad) + width * .42f;
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45;
                Box(site, "Octagon frame", c + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad), 0) * radius,
                    new Vector3(side, width, .14f), m, new Vector3(0, 0, -a));
            }
        }

        // Glass-fronted booth against the wall at xWall, facing the chamber.
        void Booth(float xWall, float xFront, float za, float zb, float floorY, float topY)
        {
            float dir = Mathf.Sign(xFront - xWall);
            Block("Booth side", xWall, floorY, za - .3f, xFront, topY, za, chamberRedBright);
            Block("Booth side", xWall, floorY, zb, xFront, topY, zb + .3f, chamberRedBright);
            Block("Booth roof", xWall, topY - .4f, za - .3f, xFront + dir * .2f, topY, zb + .3f, chamberRedBright);
            Block("Booth front", xFront - dir * .3f, floorY, za, xFront, floorY + 1.1f, zb, chamberRedBright);
            float glassBottom = floorY + 1.1f, glassTop = topY - .4f;
            Box(site, "Booth glass", new Vector3(xFront - dir * .15f, (glassBottom + glassTop) / 2, (za + zb) / 2),
                new Vector3(.05f, glassTop - glassBottom, zb - za), clearGlass, default, true);
            for (float z = za + 3.3f; z < zb - 1; z += 3.3f)
                Block("Booth mullion", xFront - dir * .25f, glassBottom, z - .06f, xFront, glassTop, z + .06f, black, false);
            Block("Booth console", xWall + dir * .2f, floorY, za + 1.5f, xWall + dir * 1.1f, floorY + 1, zb - 1.5f, deskGray);
            Monitor(site, new Vector3(xWall + dir * .6f, floorY + 1, (za + zb) / 2), dir > 0 ? 90 : 270, .9f);
            Ladder(new Vector3(xFront - dir * .8f, floorY, za + .6f), topY - floorY - .4f, 0, yellow);
        }

        // Lighting and animation ------------------------------------------------------------

        void UploadLighting()
        {
            int count = lamps.Count;
            while (lamps.Count < LampLimit) { lamps.Add(Vector4.zero); lampColors.Add(Vector4.zero); }
            Shader.SetGlobalVector("_LabZone", new Vector4(origin.x, origin.y + ZoneTop, origin.z, ZoneRadius));
            Shader.SetGlobalVector("_LabAmbient", new Vector4(.12f, .12f, .135f, 0));
            Shader.SetGlobalVector("_LabRing", new Vector4(LampRadius, origin.y + LampHeight, LampStep * Mathf.Deg2Rad, LampRange));
            Shader.SetGlobalVector("_LabRingColor", new Vector4(LampColor.r, LampColor.g, LampColor.b, 65.5f));
            Shader.SetGlobalVectorArray("_LabLights", lamps);
            Shader.SetGlobalVectorArray("_LabLightColors", lampColors);
            Shader.SetGlobalFloat("_LabLightCount", count);
            if (textMaterial != font.material) textMaterial.mainTexture = font.material.mainTexture;
        }

        void Tick(float dt)
        {
            clock += dt;
            foreach (var (pivot, speed) in runtime.Spinners) pivot.Rotate(0, 0, speed * dt, Space.Self);
            doorRing.SetColor("_BaseColor", doorRingColor * (.8f + .2f * Mathf.Sin(clock * 2.4f)));
            if (runner == null) runner = Object.FindAnyObjectByType<SpeedsterMotor>();
            if (runner == null) return;
            // The vault door rolls aside early enough for a speedster; it runs on real time so
            // speed perception (which slows the world clock) never leaves it blocking the player.
            float reach = 9 + runner.Speed * .45f;
            bool near = (runner.transform.position - vaultDoor.parent.TransformPoint(vaultDoorClosed)).sqrMagnitude < reach * reach;
            vaultDoorOpen = Mathf.MoveTowards(vaultDoorOpen, near ? 1 : 0, Mathf.Max(dt, Mathf.Min(Time.unscaledDeltaTime, .05f)) / .35f);
            float slide = Mathf.SmoothStep(0, 1, vaultDoorOpen) * 7.4f;
            vaultDoor.localPosition = vaultDoorClosed + Vector3.left * slide;
            vaultDoor.localRotation = Quaternion.Euler(0, 0, slide / 3.6f * Mathf.Rad2Deg);
        }
    }
}
