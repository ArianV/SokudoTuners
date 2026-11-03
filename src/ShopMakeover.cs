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
        private const string ShopPath = "Map/Hyland Point/Region_Downtown/Gas Station/gas station/Bodyshop";
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

            // The floor frame and tubes are placed from Marco's bay, so wait until AutoShop has found him.
            if (AutoShop.Bay == null || AutoShop.CarSpot == null)
                return;
            GameObject shop = GameObject.Find(ShopPath);
            if (shop == null || shop.transform.Find(MarkerName) != null)
                return;

            Transform root = shop.transform;
            Transform made = new GameObject(MarkerName).transform;
            made.SetParent(root, false);

            // Each part on its own, so a game update breaking one doesn't take the rest down.
            Try("neon material", () => _neonTemplate = FindNeonTemplate(root));
            Try("sign", () => ReplaceSign(root, made));
            Try("lamps", () => RecolourLamps(root));
            Try("ceiling tubes", () => AddCeilingTubes(root, made));
            Try("floor frame", () => AddFloorFrame(made));
            Try("walls", () => DarkenWalls(root));
            ModSettings.LogDebug("Shop makeover applied.");
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
            Renderer sign = root.Find("bodyshop/Quad").GetComponent<Renderer>();
            Texture2D texture = ModAssets.LoadTexture("sign.png");

            // A copy of the sign's own material keeps its shape and settings, I just swap the picture and make it glow.
            Material material = new Material(sign.sharedMaterial);
            SetTexture(material, texture);
            SetColour(material, Color.white);
            EnableGlow(material, Color.white * 1.6f, texture);
            sign.sharedMaterial = material;

            // Pink wash on the wall around it. Outward is away from the middle of the building.
            Vector3 centre = sign.bounds.center;
            Vector3 outward = centre - BuildingCentre(root);
            outward.y = 0f;
            AddLight(made, "Sign Glow", centre + outward.normalized * 1.2f, Pink, 4f, 6f);
        }

        private static void RecolourLamps(Transform root)
        {
            List<Transform> lamps = LampsIn(root);
            for (int i = 0; i < lamps.Count; i++)
                Recolour(lamps[i], i % 2 == 0 ? Pink : Cyan, 1.3f);

            foreach (Transform wallLight in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Curved Wall Light")))
                Recolour(wallLight, Cyan, 1.5f);
        }

        private static void Recolour(Transform fitting, Color colour, float brighter)
        {
            foreach (Light light in fitting.GetComponentsInChildren<Light>(true))
            {
                light.color = colour;
                light.intensity *= brighter;
            }

            Transform bulb = fitting.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Light" && t.GetComponent<Renderer>() != null);
            if (bulb != null)
                bulb.GetComponent<Renderer>().sharedMaterial = NeonMaterial(colour);
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
        }

        // Every material gets its own copy so other buildings sharing it don't change.
        private static void DarkenWalls(Transform root)
        {
            foreach (Renderer renderer in new[] { root.Find("bodyshop/Block"), root.Find("bodyshop") }.Where(t => t != null).Select(t => t.GetComponent<Renderer>()))
            {
                if (renderer == null)
                    continue;
                Material[] materials = renderer.sharedMaterials;
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
            Renderer building = root.Find("bodyshop")?.GetComponent<Renderer>();
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
