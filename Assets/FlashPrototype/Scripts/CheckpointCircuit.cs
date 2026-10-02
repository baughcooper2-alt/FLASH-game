using UnityEngine;

namespace FlashGame
{
    public sealed class CheckpointCircuit
    {
        public readonly Vector3 StartPosition = new Vector3(0, 0.12f, -25);
        public readonly Vector3[] Gates = {
            new Vector3(0, 2, 80), new Vector3(0, 2, 240), new Vector3(25, 2, 260),
            new Vector3(240, 2, 260), new Vector3(260, 2, 235), new Vector3(260, 2, 25),
            new Vector3(235, 2, 0), new Vector3(25, 2, 0), new Vector3(0, 2, -20) };
        public bool Active { get; private set; }
        public int Next { get; private set; }
        public float Elapsed { get; private set; }
        public float LastTime { get; private set; }
        public float BestTime { get; private set; }
        public bool Finished { get; private set; }
        readonly GameObject marker;
        public CheckpointCircuit(PrototypeWorld world, Transform root)
        {
            BestTime = PlayerPrefs.GetFloat("FlashPrototype.Circuit.v1.Best", 0);
            marker = new GameObject("Next checkpoint");
            marker.transform.SetParent(root, false);
            var gold = world.Material("Checkpoint glow", new Color(1, 0.73f, 0.17f), 1);
            world.Shape("Left post", PrimitiveType.Cube, marker.transform, new Vector3(-6, 2, 0), new Vector3(0.2f, 8, 0.2f), gold, false);
            world.Shape("Right post", PrimitiveType.Cube, marker.transform, new Vector3(6, 2, 0), new Vector3(0.2f, 8, 0.2f), gold, false);
            world.Shape("Top", PrimitiveType.Cube, marker.transform, new Vector3(0, 6, 0), new Vector3(12, 0.2f, 0.2f), gold, false);
            marker.SetActive(false);
        }
        public void Start() { Active = true; Finished = false; Next = 0; Elapsed = 0; ShowGate(); }
        public void Cancel() { Active = false; Finished = false; marker.SetActive(false); }
        public void Tick(Vector3 previous, Vector3 current, float dt)
        {
            if (!Active) return;
            Elapsed += dt;
            if (!SpeedMath.SegmentHitsSphere(previous + Vector3.up, current + Vector3.up, Gates[Next], 7)) return;
            Next++;
            if (Next == Gates.Length)
            {
                Active = false; Finished = true; LastTime = Elapsed; marker.SetActive(false);
                if (BestTime <= 0 || LastTime < BestTime)
                {
                    BestTime = LastTime;
                    PlayerPrefs.SetFloat("FlashPrototype.Circuit.v1.Best", BestTime);
                    PlayerPrefs.Save();
                }
            }
            else ShowGate();
        }
        void ShowGate()
        {
            marker.SetActive(true);
            marker.transform.position = Gates[Next];
            Vector3 from = Next == 0 ? StartPosition : Gates[Next - 1];
            Vector3 heading = Gates[Next] - from; heading.y = 0;
            marker.transform.rotation = Quaternion.LookRotation(heading);
        }
    }
}
