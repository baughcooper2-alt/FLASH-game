using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    // Applies FlashSkin palettes to the realistic Flash. Each suit texture is repainted on the GPU once per
    // skin (SuitRecolor shader) and swapped into per-character material copies, so lighting stays URP Lit.
    public sealed class SuitPainter : MonoBehaviour
    {
        enum Slot { Suit, Legs, Boots, Cowl, Trim, Ear, EmblemField, EmblemBolt, Face, Eyes }
        static readonly (string name, Slot slot)[] Names = {
            ("Flash suit body", Slot.Suit), ("Flash suit legs", Slot.Legs), ("Flash boots", Slot.Boots), ("Flash cowl", Slot.Cowl), ("Flash gold trim", Slot.Trim),
            ("Flash ear lightning", Slot.Ear), ("Flash emblem field", Slot.EmblemField), ("Flash emblem bolt", Slot.EmblemBolt),
            ("Flash face", Slot.Face), ("Flash eyes", Slot.Eyes) };
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"), PrimaryId = Shader.PropertyToID("_Primary"),
            AccentId = Shader.PropertyToID("_Accent"), LightId = Shader.PropertyToID("_Light"),
            CoverId = Shader.PropertyToID("_Cover"), ClassesId = Shader.PropertyToID("_Classes"),
            MetallicId = Shader.PropertyToID("_Metallic"), SmoothnessId = Shader.PropertyToID("_Smoothness");
        readonly List<(Material material, Texture original, Slot slot)> targets = new List<(Material, Texture, Slot)>();
        readonly Dictionary<Material, (float metal, float smooth)> finish = new Dictionary<Material, (float, float)>();
        readonly Dictionary<(int skin, Slot slot), RenderTexture> painted = new Dictionary<(int, Slot), RenderTexture>();
        Material recolor;
        int current = -1;

        public bool Ready => targets.Count > 0 && recolor != null;

        public void Collect()
        {
            var shader = Resources.Load<Shader>("SuitRecolor");
            if (shader == null) { Debug.LogWarning("SuitRecolor shader missing; suits stay in their original colours."); return; }
            recolor = new Material(shader) { name = "Suit recolour" };
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    foreach (var (name, slot) in Names)
                    {
                        if (!materials[i].name.StartsWith(name)) continue;
                        var copy = new Material(materials[i]) { name = materials[i].name + " (suit)" };
                        targets.Add((copy, copy.GetTexture(BaseMapId), slot));
                        finish[copy] = (copy.GetFloat(MetallicId), copy.GetFloat(SmoothnessId));
                        materials[i] = copy;
                        break;
                    }
                }
                renderer.sharedMaterials = materials;
            }
        }

        public void Apply(int index)
        {
            if (!Ready) return;
            current = index;
            var skin = FlashSkins.All[index];
            // Only the worn suit stays in video memory; repainting another is a few blits.
            foreach (var key in new List<(int skin, Slot slot)>(painted.Keys))
                if (key.skin != index) { painted[key].Release(); Destroy(painted[key]); painted.Remove(key); }
            foreach (var (material, original, slot) in targets)
            {
                if (original == null) continue;
                // Gold boots are metallic leather; everything else keeps its authored finish.
                var (metal, smooth) = finish[material];
                bool shiny = slot == Slot.Boots && skin.BootsMetal > 0;
                material.SetFloat(MetallicId, shiny ? skin.BootsMetal : metal);
                material.SetFloat(SmoothnessId, shiny ? Mathf.Max(smooth, .6f) : smooth);
                if ((slot == Slot.Face || slot == Slot.Eyes) && skin.Cover.a <= 0)
                {
                    material.SetTexture(BaseMapId, original);
                    continue;
                }
                if (!painted.TryGetValue((index, slot), out var target) || !target.IsCreated())
                {
                    if (target != null) target.Release();
                    target = Paint(original, skin, slot);
                    painted[(index, slot)] = target;
                }
                material.SetTexture(BaseMapId, target);
            }
        }

        // Render textures can lose their contents (graphics device resets, window or fullscreen changes, and on
        // entering Play Mode), which left the suit untextured grey. Repaint whenever that happens.
        void LateUpdate()
        {
            if (current < 0) return;
            foreach (var target in painted.Values)
                if (target == null || !target.IsCreated()) { Apply(current); return; }
        }

        RenderTexture Paint(Texture source, FlashSkin skin, Slot slot)
        {
            Color primary = slot switch { Slot.Legs => skin.LegsOrSuit, Slot.Boots => skin.BootsOrLegs, Slot.Cowl => skin.CowlOrSuit, _ => skin.Suit };
            // Seam lines painted into the suit textures follow Seams; solid gold parts follow Trim.
            Color accent = slot switch { Slot.EmblemBolt => skin.EmblemBolt, Slot.Suit or Slot.Legs or Slot.Boots or Slot.Cowl => skin.SeamsOrTrim, _ => skin.Trim };
            var classes = slot switch
            {
                Slot.Suit or Slot.Legs or Slot.Boots or Slot.Cowl => new Vector4(1, 1, 0, 0),
                Slot.EmblemField => new Vector4(1, 1, 1, 0),
                Slot.Face or Slot.Eyes => Vector4.zero,
                _ => new Vector4(0, 1, 0, 0),
            };
            recolor.SetColor(PrimaryId, slot == Slot.EmblemField ? skin.EmblemField : primary);
            recolor.SetColor(AccentId, slot == Slot.EmblemField ? skin.EmblemBolt : accent);
            recolor.SetColor(LightId, skin.EmblemField);
            recolor.SetColor(CoverId, slot == Slot.Face || slot == Slot.Eyes ? skin.Cover : Color.clear);
            recolor.SetVector(ClassesId, classes);
            var target = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = skin.Name + " " + slot, useMipMap = true, autoGenerateMips = true,
                wrapMode = source.wrapMode, filterMode = FilterMode.Trilinear, anisoLevel = 4,
            };
            target.Create();
            Graphics.Blit(source, target, recolor);
            return target;
        }

        void OnDestroy()
        {
            foreach (var target in painted.Values) if (target != null) { target.Release(); Destroy(target); }
            foreach (var (material, _, _) in targets) Destroy(material);
            if (recolor != null) Destroy(recolor);
        }
    }
}
