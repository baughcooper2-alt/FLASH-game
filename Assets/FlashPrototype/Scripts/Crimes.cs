using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    // A bomb left in the street: a holdall with wires and a blinking light under a countdown.
    public sealed class Bomb
    {
        public Transform Root;
        public TextMesh Clock;
        public Renderer Light;
        public float Timer;
        public bool Carried, Defused, Exploded;
    }

    // Crimes in the emergency rotation. They use the same people (Townsperson) and the bots' combat code:
    //   Armed robbery: masked robbers inside the Corner Mart hold the clerk and customers at gunpoint, with a lookout
    //       at the door. They open fire when the Flash shows up; knock them all out.
    //   Mugging: a gunman corners someone in an alley. Too slow and he grabs the bag and runs: catch him.
    //   Bomb threat: a timed bomb on a busy corner. Grab it and drop it in deep harbour water before it goes off.
    public sealed partial class EmergencyDirector
    {
        public BotDirector Bots;
        public System.Func<Vector3> PlayerHand;
        public Transform Player;
        readonly System.Random crimeRng = new System.Random(911);
        GameObject townsperson;

        Townsperson Person(Emergency e, Vector3 at, float yaw, Townsperson.Pose pose)
        {
            if (townsperson == null) townsperson = Resources.Load<GameObject>("Townsperson");
            if (townsperson == null) return null;
            var go = Instantiate(townsperson, e.Root);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            var p = go.GetComponent<Townsperson>();
            p.Dress(crimeRng);
            p.Posing = pose;
            e.People.Add(p);
            return p;
        }
        BotEnemy Criminal(Emergency e, BotKind kind, Vector3 at, Townsperson victim)
        {
            if (Bots == null) return null;
            var bot = Bots.SpawnHuman(kind, at, at, crimeRng);
            if (bot == null) return null;
            bot.Mode = BotEnemy.Role.Hold;
            Vector3 look = victim != null ? victim.transform.position - at : Vector3.forward; look.y = 0;
            bot.transform.rotation = Quaternion.LookRotation(look.sqrMagnitude > .01f ? look : Vector3.forward);
            if (bot.Person != null && victim != null) bot.Person.AimTarget = victim.transform.position + Vector3.up * 1.4f;
            e.Criminals.Add(bot);
            return bot;
        }
        static float Yaw(Vector3 from, Vector3 to) { var d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

        // Picks the place nearest the player, so crimes happen where the action is.
        CityPlace Nearest(List<CityPlace> places, float min = 60)
        {
            Vector3 p = Player != null ? Player.position : Vector3.zero;
            CityPlace best = places[0]; float bestD = float.MaxValue;
            foreach (var place in places)
            {
                float d = Vector3.Distance(place.Position, p);
                if (d > min && d < bestD) { bestD = d; best = place; }
            }
            return best;
        }

        void BeginRobbery(Emergency e)
        {
            var mart = CityBlocks.CornerMart;
            var room = CityLandmarks.MartInterior;
            e.Location = mart.Position;
            e.Instructions = "Robbers have the clerk and customers at gunpoint. Hit them at speed before they hurt anyone: they open fire once they see you.";
            Vector3 c = room.center; c.y = mart.Position.y;
            var clerk = Person(e, c + new Vector3(5.6f, 0, 6), -90, Townsperson.Pose.HandsUp);
            var shopperA = Person(e, c + new Vector3(-3.5f, 0, 2), 0, Townsperson.Pose.Cower);
            var shopperB = Person(e, c + new Vector3(1.2f, 0, -4.5f), 90, Townsperson.Pose.HandsUp);
            Criminal(e, BotKind.Robber, c + new Vector3(3.2f, 0, 6.5f), clerk);
            Criminal(e, BotKind.Robber, c + new Vector3(-1.5f, 0, 0), shopperA);
            var lookout = Criminal(e, BotKind.Robber, mart.Position + mart.Facing * 2 + Vector3.right * 2, null);
            if (lookout != null) lookout.transform.rotation = Quaternion.LookRotation(mart.Facing);
            if (lookout?.Person != null) lookout.Person.AimTarget = mart.Position + mart.Facing * 12 + Vector3.up;
            if (shopperB != null) shopperB.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }

        void BeginMugging(Emergency e)
        {
            var alley = Nearest(CityBlocks.Alleys);
            e.Location = alley.Position;
            e.Timer = 30;
            e.Instructions = "A gunman has someone cornered in the alley. Get there before he loses his nerve, or chase him down when he runs with the bag.";
            var victim = Person(e, alley.Position + Vector3.right * 1.2f, -90, Townsperson.Pose.HandsUp);
            victim?.HoldBag(true, new Color(.45f, .2f, .12f));
            var mugger = Criminal(e, BotKind.Mugger, alley.Position - Vector3.right * 1.6f, victim);
            if (mugger != null) mugger.FleeTo = alley.Position - Vector3.right * 220;
        }

        void BeginBomb(Emergency e)
        {
            var corner = Nearest(CityBlocks.Plazas, 150);
            Vector3 at = corner.Position;
            e.Location = at;
            e.Instructions = "A bomb on the corner, timer running. Grab it (G / Y), sprint to the harbour and drop it in deep water past the sea wall.";
            var bomb = new Bomb { Timer = 60 };
            bomb.Root = new GameObject("Bomb").transform;
            bomb.Root.SetParent(e.Root, false);
            bomb.Root.position = at;
            w.Shape("Holdall", PrimitiveType.Capsule, bomb.Root, new Vector3(0, .22f, 0), new Vector3(.45f, .35f, .45f), charred, false).transform.localRotation = Quaternion.Euler(0, 0, 90);
            w.Shape("Pipe charge", PrimitiveType.Cylinder, bomb.Root, new Vector3(.05f, .32f, .1f), new Vector3(.12f, .2f, .12f), gasPipe, false).transform.localRotation = Quaternion.Euler(0, 0, 90);
            w.Shape("Timer", PrimitiveType.Cube, bomb.Root, new Vector3(0, .46f, 0), new Vector3(.24f, .1f, .14f), metal, false);
            var light = w.Shape("Blink", PrimitiveType.Sphere, bomb.Root, new Vector3(.08f, .53f, 0), Vector3.one * .06f, blink, false);
            bomb.Light = light.GetComponent<Renderer>();
            var label = new GameObject("Countdown");
            label.transform.SetParent(bomb.Root, false);
            label.transform.localPosition = new Vector3(0, 1.1f, 0);
            bomb.Clock = label.AddComponent<TextMesh>();
            bomb.Clock.fontSize = 64; bomb.Clock.characterSize = .05f; bomb.Clock.anchor = TextAnchor.MiddleCenter;
            bomb.Clock.color = new Color(1, .2f, .15f); bomb.Clock.fontStyle = FontStyle.Bold;
            e.Bomb = bomb;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = at + Quaternion.Euler(0, 40 + i * 70, 0) * Vector3.forward * (7 + i % 2 * 2);
                Person(e, p, Yaw(p, at) + 180, Townsperson.Pose.Cower);
            }
        }

        // Crime state each frame: robbers and muggers, the bomb countdown, hostages' reactions.
        void TickCrime(Emergency e, float dt, float world)
        {
            bool armed = false;
            foreach (var c in e.Criminals) if (c != null && !c.Dead) armed = true;
            foreach (var p in e.People)
                if (p != null && p.Posing != Townsperson.Pose.Down)
                    p.Posing = e.Complete ? Townsperson.Pose.Cheer : p.Posing;
            if (e.Timer > 0 && e.Bomb == null)
            {
                // Mugging: when the timer runs out the mugger takes the bag and runs.
                e.Timer -= world;
                if (e.Timer <= 0)
                    foreach (var c in e.Criminals)
                        if (c != null && !c.Dead && c.Mode == BotEnemy.Role.Hold)
                        {
                            c.Mode = BotEnemy.Role.Flee;
                            foreach (var p in e.People) if (p != null) { p.HoldBag(false, Color.clear); p.Posing = Townsperson.Pose.Cower; }
                            c.Person?.HoldBag(true, new Color(.45f, .2f, .12f));
                            Banner = "THE MUGGER IS RUNNING WITH THE BAG"; bannerTimer = 4;
                        }
                foreach (var c in e.Criminals)
                    if (c != null && !c.Dead && c.Mode == BotEnemy.Role.Flee && Vector3.Distance(c.transform.position, e.Location) > 190)
                        Fail(e, "SUSPECT ESCAPED • the mugger got away");
            }
            if (e.Bomb != null) TickBomb(e, world);
            if (!armed && e.Criminals.Count > 0 && e.Bomb == null && e.Timer <= 0 && e.People.Count > 0 && e.People[0] != null)
            {
                // The mugger is down: the bag goes back to its owner.
                foreach (var c in e.Criminals) c?.Person?.HoldBag(false, Color.clear);
                e.People[0].HoldBag(true, new Color(.45f, .2f, .12f));
            }
        }

        void TickBomb(Emergency e, float world)
        {
            var b = e.Bomb;
            if (b.Defused || b.Exploded) return;
            if (b.Carried && PlayerHand != null) b.Root.position = PlayerHand() + Vector3.up * .1f - b.Root.up * .3f;
            b.Timer -= world;
            int seconds = Mathf.CeilToInt(Mathf.Max(0, b.Timer));
            b.Clock.text = "0:" + seconds.ToString("00");
            if (Camera.main != null) b.Clock.transform.rotation = Quaternion.LookRotation(b.Clock.transform.position - Camera.main.transform.position);
            b.Light.enabled = Mathf.Repeat(b.Timer * (b.Timer < 10 ? 4 : 1.5f), 1) > .5f;
            if (b.Timer > 0) return;
            Vector3 at = b.Root.position;
            if (DeepWater(at))
            {
                Defuse(e, at);
                return;
            }
            // It went off in the street.
            b.Exploded = true;
            fx.Shockwave(at, 22, new Color(1, .55f, .2f), .8f);
            fx.Burst(at + Vector3.up, 6, 20, new Color(1, .5f, .15f), .5f);
            fx.Debris(at, 30, 18, Color.gray, .35f);
            fx.Smoke(at + Vector3.up * 2, Vector3.up * 3, 9, new Color(.15f, .14f, .14f, .9f), 6);
            fx.ScreenFlash(new Color(1, .7f, .4f), .8f);
            if (Player != null && Vector3.Distance(Player.position, at) < 14) Bots?.DamagePlayer(60, at);
            b.Root.gameObject.SetActive(false);
            Fail(e, "THE BOMB WENT OFF");
        }
        static bool DeepWater(Vector3 p) => WaterZones.Surface(p, out _) && p.x > 805;
        void Defuse(Emergency e, Vector3 at)
        {
            var b = e.Bomb;
            b.Defused = true; b.Carried = false;
            b.Root.gameObject.SetActive(false);
            Vector3 water = new Vector3(at.x, -1.85f, at.z);
            fx.Spray(water, Vector3.up, 60, 22);
            fx.Shockwave(water, 14, new Color(.6f, .8f, 1), .7f);
            Banner = "BOMB DISPOSED OF IN THE HARBOUR"; bannerTimer = 5;
        }
        public void GrabBomb() { if (Active?.Bomb != null && !Active.Bomb.Defused && !Active.Bomb.Exploded) Active.Bomb.Carried = true; }
        public void DropBomb()
        {
            var b = Active?.Bomb;
            if (b == null || !b.Carried) return;
            b.Carried = false;
            Vector3 at = b.Root.position;
            if (DeepWater(at)) Defuse(Active, at);
            else if (Physics.Raycast(at + Vector3.up, Vector3.down, out RaycastHit hit, 30, ~(1 << 2), QueryTriggerInteraction.Ignore))
                b.Root.position = hit.point;
        }
        void Fail(Emergency e, string text)
        {
            if (e.Failed) return;
            e.Failed = true;
            Banner = text; bannerTimer = 6;
        }
    }
}
