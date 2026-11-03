using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace SokudoTuners
{
    // Turns Marco's auto shop into Sokudo Tuners: my sign over the door, neon lamps, ceiling tubes, a floor frame
    // around the bay and dark walls. Everything is placed from the shop's own objects so it lines up exactly.
    public static class ShopMakeover
    {
        private const string MarkerName = "SokudoMakeover";

        private static readonly Color Pink = new Color(1f, 0.16f, 0.52f);
        private static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
        private static readonly Color Siding = new Color(0.13f, 0.12f, 0.17f);
        private static readonly Color Brick = new Color(0.3f, 0.24f, 0.28f);

        private static float _nextCheck;
        private static Material _neonTemplate;

        public static void Update()
        {
            if (Time.unscaledTime < _nextCheck)
                return;
            _nextCheck = Time.unscaledTime + 2f;

            // Marco's bay detector sits inside the real shop, so its parent is the shop. Looking the shop up by name
            // found the wrong object, there's more than one "Bodyshop".
            if (AutoShop.Bay == null || AutoShop.CarSpot == null)
                return;
            Transform root = AutoShop.Bay.transform.parent;
            if (root == null || root.Find(MarkerName) != null)
                return;

            Transform made = new GameObject(MarkerName).transform;
            made.SetParent(root, false);
            _found.Clear();

            // Each part on its own, so a game update breaking one doesn't take the rest down.
            Try("neon material", () => _neonTemplate = FindNeonTemplate(root));
            Try("sign", () => ReplaceSign(root, made));
            Try("lamps", () => RecolourLamps(root, made));
            Try("ceiling tubes", () => AddCeilingTubes(root, made));
            Try("floor frame", () => AddFloorFrame(made));
            Try("walls", () => DarkenWalls(root));
            ModSettings.LogDebug($"Shop makeover applied to {root.name}: {string.Join(", ", _found.Select(p => p.Key + " " + p.Value))}");
        }

        private static readonly Dictionary<string, int> _found = new Dictionary<string, int>();

        private static void Count(string part)
        {
            _found.TryGetValue(part, out int n);
            _found[part] = n + 1;
        }

        private static void Try(string part, Action apply)
        {
            try
            {
                apply();
            }
            catch (Exception ex)
            {
                Melon<SokudoTunersMod>.Logger.Warning($"Shop makeover: {part} failed: {ex.Message}");
            }
        }

        private static void ReplaceSign(Transform root, Transform made)
        {
            Renderer sign = root.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(r => r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Auto shop"));
            if (sign == null)
                return;
            Count("sign");
            Texture2D texture = ModAssets.LoadTexture("sign.png");

            // A copy of the sign's own material keeps its see-through settings, I just swap the picture and make it glow.
            Material material = new Material(sign.sharedMaterial);
            SetTexture(material, texture);
            SetColour(material, Color.white);
            EnableGlow(material, Color.white * 1.6f, texture);

            // The original panel sits recessed in the frame, which hides the top and bottom of my sign. It's part of a
            // combined mesh so it can't be moved, so I hide it and hang my own panel the same size out in front.
            Bounds bounds = sign.bounds;
            Vector3 outward = Outward(sign.transform, bounds.center - BuildingCentre(root));
            float width = new Vector2(bounds.size.x, bounds.size.z).magnitude;
            sign.enabled = false;

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(panel.GetComponent<Collider>());
            panel.name = "Sokudo Sign";
            // A quad shows its face to whoever looks along its forward axis, so it looks back into the building.
            panel.transform.SetPositionAndRotation(bounds.center + outward * 0.35f + Vector3.down * 0.15f, Quaternion.LookRotation(-outward));
            panel.transform.localScale = new Vector3(width * 1.05f, bounds.size.y * 1.05f, 1f);
            panel.transform.SetParent(made, true);
            Renderer renderer = panel.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            AddLight(made, "Sign Glow", bounds.center + outward * 1.2f, Pink, 4f, 6f);
        }

        // Flat direction the sign faces. The panel's own normal is the most accurate, flipped if it points into the building.
        private static Vector3 Outward(Transform sign, Vector3 roughlyOut)
        {
            roughlyOut.y = 0f;
            Vector3 normal = sign.forward;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.01f)
                return roughlyOut.normalized;
            normal.Normalize();
            return Vector3.Dot(normal, roughlyOut) < 0f ? -normal : normal;
        }

        // The shop's own lights may be baked into the lighting, where changing their colour does nothing, so each lamp
        // also gets a light of mine underneath it.
        private static void RecolourLamps(Transform root, Transform made)
        {
            List<Transform> lamps = LampsIn(root);
            for (int i = 0; i < lamps.Count; i++)
            {
                Color colour = i % 2 == 0 ? Pink : Cyan;
                Recolour(lamps[i], colour, 1.3f);
                AddLight(made, "Lamp Neon", BulbPosition(lamps[i]) + Vector3.down * 0.5f, colour, 3f, 7f);
                Count("lamps");
            }

            foreach (Transform wallLight in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Curved Wall Light")))
            {
                Recolour(wallLight, Cyan, 1.5f);
                AddLight(made, "Wall Neon", BulbPosition(wallLight), Cyan, 1.5f, 4f);
                Count("wall lights");
            }
        }

        private static void Recolour(Transform fitting, Color colour, float brighter)
        {
            foreach (Light light in fitting.GetComponentsInChildren<Light>(true))
            {
                light.color = colour;
                light.intensity *= brighter;
                Count("game lights");
            }

            Transform bulb = Bulb(fitting);
            if (bulb != null)
                bulb.GetComponent<Renderer>().sharedMaterial = NeonMaterial(colour);
        }

        private static Transform Bulb(Transform fitting) =>
            fitting.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Light" && t.GetComponent<Renderer>() != null);

        private static Vector3 BulbPosition(Transform fitting)
        {
            Transform bulb = Bulb(fitting);
            return bulb != null ? bulb.GetComponent<Renderer>().bounds.center : fitting.position;
        }

        // Two tubes along the ceiling, one through each row of lamps, running a metre past the end lamps.
        private static void AddCeilingTubes(Transform root, Transform made)
        {
            List<Transform> lamps = LampsIn(root);
            Transform spot = AutoShop.CarSpot;
            if (lamps.Count < 4 || spot == null)
                return;

            // Split the lamps into the row left of the car and the row right of it.
            List<Transform> sorted = lamps.OrderBy(l => Vector3.Dot(l.position - spot.position, spot.right)).ToList();
            int half = sorted.Count / 2;
            AddTube(made, sorted.Take(half).ToList(), Pink);
            AddTube(made, sorted.Skip(half).ToList(), Cyan);
        }

        private static void AddTube(Transform made, List<Transform> row, Color colour)
        {
            Vector3 a = row.First().position;
            Vector3 b = row.Last().position;
            float length = Vector3.Distance(a, b);
            if (length < 0.5f)
                return;

            Vector3 along = (b - a) / length;
            Vector3 middle = (a + b) / 2f + Vector3.up * 0.45f;
            AddBar(made, "Ceiling Tube", middle, Quaternion.LookRotation(along), new Vector3(0.07f, 0.07f, length + 2f), colour);
            Count("tubes");
        }

        // Neon outline on the floor around the bay, sized from the shop's own vehicle detector box.
        private static void AddFloorFrame(Transform made)
        {
            if (AutoShop.Bay == null || AutoShop.CarSpot == null)
                return;
            BoxCollider box = AutoShop.Bay.GetComponentInChildren<BoxCollider>();
            if (box == null)
                return;

            Transform t = box.transform;
            Vector3 size = Vector3.Scale(box.size, t.lossyScale) - new Vector3(0.6f, 0f, 0.6f);
            Vector3 centre = t.TransformPoint(box.center);
            centre.y = AutoShop.CarSpot.position.y + 0.015f;

            const float width = 0.08f;
            Quaternion rotation = Quaternion.LookRotation(t.forward, Vector3.up);
            Vector3 halfX = t.right * size.x / 2f;
            Vector3 halfZ = t.forward * size.z / 2f;
            AddBar(made, "Floor Line", centre + halfX, rotation, new Vector3(width, 0.01f, size.z), Pink);
            AddBar(made, "Floor Line", centre - halfX, rotation, new Vector3(width, 0.01f, size.z), Pink);
            AddBar(made, "Floor Line", centre + halfZ, rotation, new Vector3(size.x, 0.01f, width), Pink);
            AddBar(made, "Floor Line", centre - halfZ, rotation, new Vector3(size.x, 0.01f, width), Pink);
            Count("floor frame");
        }

        // Every material gets its own copy so other buildings sharing it don't change.
        private static void DarkenWalls(Transform root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                if (!materials.Any(m => m != null && (m.name.StartsWith("alum sheet bodyshop") || m.name.StartsWith("brick"))))
                    continue;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                        continue;
                    string name = materials[i].name;
                    Color? tint = name.StartsWith("alum sheet bodyshop") ? Siding : name.StartsWith("brick") ? Brick : (Color?)null;
                    if (tint == null)
                        continue;
                    materials[i] = new Material(materials[i]);
                    SetColour(materials[i], tint.Value);
                    Count("wall materials");
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static List<Transform> LampsIn(Transform root)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Industrial Hanging Light") && t.parent != null && !t.parent.name.StartsWith("Industrial Hanging Light"))
                .OrderBy(t => t.name)
                .ToList();
        }

        private static Vector3 BuildingCentre(Transform root)
        {
            Renderer building = root.GetComponentsInChildren<Renderer>(true).OrderByDescending(r => r.bounds.size.sqrMagnitude).FirstOrDefault();
            return building != null ? building.bounds.center : root.position;
        }

        private static void AddBar(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 size, Color colour)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(bar.GetComponent<Collider>());
            bar.name = name;
            bar.transform.SetPositionAndRotation(position, rotation);
            bar.transform.localScale = size;
            bar.transform.SetParent(parent, true);

            Renderer renderer = bar.GetComponent<Renderer>();
            renderer.sharedMaterial = NeonMaterial(colour);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void AddLight(Transform parent, string name, Vector3 position, Color colour, float intensity, float range)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        // The wall lights' "bulb on" material already glows, so copies of it are safe to tint for neon.
        private static Material FindNeonTemplate(Transform root)
        {
            Renderer bulb = root.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(r => r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("warmbulb_on"));
            return bulb != null ? bulb.sharedMaterial : null;
        }

        private static readonly Dictionary<Color, Material> _neon = new Dictionary<Color, Material>();

        private static Material NeonMaterial(Color colour)
        {
            if (_neon.TryGetValue(colour, out Material cached) && cached != null)
                return cached;

            Material material = _neonTemplate != null ? new Material(_neonTemplate) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            SetColour(material, colour);
            EnableGlow(material, colour * 3f, null);
            _neon[colour] = material;
            return material;
        }

        private static void SetTexture(Material material, Texture texture)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        }

        private static void SetColour(Material material, Color colour)
        {
            foreach (string property in new[] { "_BaseColor", "_Color", "_Tint" })
                if (material.HasProperty(property))
                    material.SetColor(property, colour);
        }

        private static void EnableGlow(Material material, Color emission, Texture map)
        {
            if (!material.HasProperty("_EmissionColor"))
                return;
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", emission);
            if (map != null && material.HasProperty("_EmissionMap"))
                material.SetTexture("_EmissionMap", map);
        }
    }
}
