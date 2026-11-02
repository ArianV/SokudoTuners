using System.Collections.Generic;
using MelonLoader;
using ScheduleOne.Vehicles;
using UnityEngine;

namespace SokudoTuners
{
    // Blue exhaust flames while nitro fires. Every car's prefab already has its exhaust smoke particles
    // placed on the tailpipes (VehicleFX.exhaustFX), so I put a flame on each of those.
    public static class NitroFlames
    {
        private const string FlameName = "SokudoNitroFlame";

        private static LandVehicle _vehicle;
        private static readonly List<ParticleSystem> _flames = new List<ParticleSystem>();
        private static Material _material;

        // Called every frame from the mod.
        public static void Update()
        {
            LandVehicle vehicle = LocalDriver.GetVehicle();

            if (vehicle != _vehicle)
            {
                // Got out or switched cars. Stop the old flames, they die out on their own.
                SetEmitting(false);
                _flames.Clear();
                _vehicle = vehicle;
                if (vehicle != null)
                    FindOrCreateFlames(vehicle);
            }

            SetEmitting(vehicle != null && LocalDriver.IsBoosting);
        }

        private static void SetEmitting(bool on)
        {
            foreach (ParticleSystem flame in _flames)
            {
                if (flame == null)
                    continue;
                ParticleSystem.EmissionModule emission = flame.emission;
                emission.enabled = on;
            }
        }

        // The flames stay on the car after I get out, so reuse them instead of stacking new ones every time.
        private static void FindOrCreateFlames(LandVehicle vehicle)
        {
            foreach (ParticleSystem existing in vehicle.GetComponentsInChildren<ParticleSystem>(true))
                if (existing.name == FlameName)
                    _flames.Add(existing);
            if (_flames.Count > 0)
                return;

            VehicleFX fx = vehicle.GetComponentInChildren<VehicleFX>(true);
            if (fx != null && fx.exhaustFX != null)
                foreach (ParticleSystem exhaust in fx.exhaustFX)
                    if (exhaust != null)
                        _flames.Add(CreateFlame(exhaust.transform, Vector3.zero, Quaternion.identity));

            // No exhaust on this car, so fire one from the middle of the back bumper, pointing backwards.
            if (_flames.Count == 0)
            {
                BoxCollider box = vehicle.boundingBox;
                Vector3 rear = box.transform.TransformPoint(box.center - new Vector3(0f, box.size.y * 0.3f, box.size.z * 0.5f));
                _flames.Add(CreateFlame(vehicle.transform, vehicle.transform.InverseTransformPoint(rear),
                    Quaternion.Euler(0f, 180f, 0f)));
            }

            Melon<SokudoTunersMod>.Logger.Msg($"Added {_flames.Count} nitro flame(s) to '{vehicle.VehicleName}' " +
                $"({(fx != null ? "on its exhausts" : "no exhausts found, using the rear bumper")}).");
        }

        private static ParticleSystem CreateFlame(Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject go = new GameObject(FlameName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;

            // A new ParticleSystem starts playing with Unity's defaults, so stop it while I set it up.
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Short-lived, fast particles in local space read as a jet of flame stuck to the pipe.
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.95f, 1f), new Color(0.25f, 0.55f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 150f;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.04f;

            // White-hot at the pipe, blue in the middle, fading out at the tip.
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.4f, 0.85f, 1f), 0.3f), new GradientColorKey(new Color(0.1f, 0.3f, 1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = GetMaterial();

            ps.Play();
            return ps;
        }

        // A plain unlit material with a soft round dot texture. Without the texture every particle is a hard square.
        // Sprites/Default ships in every Unity build, so I can count on finding it.
        private static Material GetMaterial()
        {
            if (_material != null)
                return _material;

            const int size = 64;
            Texture2D dot = new Texture2D(size, size, TextureFormat.RGBA32, false);
            dot.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(falloff * falloff * 255f));
                }
            }
            dot.SetPixels32(pixels);
            dot.Apply();

            _material = new Material(Shader.Find("Sprites/Default"));
            _material.mainTexture = dot;
            return _material;
        }
    }
}
