using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Map;
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.NPCs.Schedules;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SokudoTuners
{
    // Dev tool: F9 (with DebugLogging on) dumps everything around me to a text file, so I can see what a location is built from.
    public static class ShopInspector
    {
        private const float Radius = 30f;

        public static void Update()
        {
            if (!ModSettings.DebugLoggingEnabled || GameInput.IsTyping)
                return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f9Key.wasPressedThisFrame || Player.Local == null)
                return;

            try
            {
                string path = Dump(Player.Local.transform.position);
                Melon<SokudoTunersMod>.Logger.Msg($"Scene dump written to {path}");
            }
            catch (Exception ex)
            {
                Melon<SokudoTunersMod>.Logger.Error($"Scene dump failed: {ex}");
            }
        }

        private static string Dump(Vector3 centre)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine($"Scene dump around {centre} (radius {Radius} m), {DateTime.Now}");
            text.AppendLine();

            text.AppendLine("== Reference points");
            Marco marco = UnityEngine.Object.FindObjectOfType<Marco>();
            if (marco != null)
            {
                text.AppendLine($"Marco: {marco.transform.position} ({Vector3.Distance(centre, marco.transform.position):F1} m away)");
                if (marco.VehicleDetector != null)
                    text.AppendLine($"Marco vehicle detector: {Path(marco.VehicleDetector.transform)} at {marco.VehicleDetector.transform.position}");
                if (marco.VehicleRecoveryPoint != null)
                    text.AppendLine($"Marco recovery point: {marco.VehicleRecoveryPoint.position}");
                DumpSchedule(marco, text);
            }
            else
                text.AppendLine("Marco: not found");

            if (Singleton<VehicleModMenu>.InstanceExists)
            {
                VehicleModMenu paint = Singleton<VehicleModMenu>.Instance;
                if (paint.VehiclePosition != null)
                    text.AppendLine($"Paint shop car spot: {paint.VehiclePosition.position} rot {paint.VehiclePosition.eulerAngles}");
                if (paint.CameraPosition != null)
                    text.AppendLine($"Paint shop camera: {paint.CameraPosition.position} rot {paint.CameraPosition.eulerAngles}");
            }
            text.AppendLine();

            text.AppendLine("== Map pins within 150 m");
            foreach (POI poi in UnityEngine.Object.FindObjectsOfType<POI>())
            {
                float distance = Vector3.Distance(centre, poi.transform.position);
                if (distance <= 150f)
                    text.AppendLine($"{distance,6:F1} m  '{poi.MainText}' (default '{poi.DefaultMainText}')  {Path(poi.transform)}");
            }
            text.AppendLine();

            text.AppendLine("== Text");
            foreach (TMP_Text tmp in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
                if (Near(tmp.transform, centre, out float d))
                    text.AppendLine($"{d,6:F1} m  TMP '{tmp.text}'  {Path(tmp.transform)}");
            foreach (TextMesh mesh in UnityEngine.Object.FindObjectsOfType<TextMesh>())
                if (Near(mesh.transform, centre, out float d))
                    text.AppendLine($"{d,6:F1} m  TextMesh '{mesh.text}'  {Path(mesh.transform)}");
            text.AppendLine();

            text.AppendLine("== Lights");
            foreach (Light light in UnityEngine.Object.FindObjectsOfType<Light>())
                if (Near(light.transform, centre, out float d))
                    text.AppendLine($"{d,6:F1} m  {light.type} colour {light.color} intensity {light.intensity} range {light.range}  {Path(light.transform)}");
            text.AppendLine();

            text.AppendLine("== Renderers (closest first)");
            List<Renderer> renderers = UnityEngine.Object.FindObjectsOfType<Renderer>()
                .Where(r => r.enabled && Vector3.Distance(centre, r.bounds.center) <= Radius)
                .OrderBy(r => Vector3.Distance(centre, r.bounds.center))
                .ToList();
            foreach (Renderer renderer in renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                string mesh = filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : "-";
                text.AppendLine($"{Vector3.Distance(centre, renderer.bounds.center),6:F1} m  {Path(renderer.transform)}");
                text.AppendLine($"          mesh '{mesh}'  size {renderer.bounds.size}  static {renderer.gameObject.isStatic}");
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                        continue;
                    Texture texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
                    Color colour = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.HasProperty("_Color") ? material.color : Color.white;
                    text.AppendLine($"          material '{material.name}'  shader '{material.shader.name}'  texture '{(texture != null ? texture.name : "-")}'  colour {colour}");
                }
            }

            string folder = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "SokudoTuners");
            Directory.CreateDirectory(folder);
            string file = System.IO.Path.Combine(folder, $"scene-dump-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(file, text.ToString());
            return file;
        }

        private static void DumpSchedule(Marco marco, StringBuilder text)
        {
            NPCScheduleManager schedule = marco.GetComponentInChildren<NPCScheduleManager>(true);
            if (schedule == null)
            {
                text.AppendLine("Marco schedule: not found");
                return;
            }

            text.AppendLine($"Marco schedule: {Path(schedule.transform)}  enabled {schedule.ScheduleEnabled}  curfew mode {schedule.CurfewModeEnabled}  " +
                            $"active '{(schedule.ActiveAction != null ? schedule.ActiveAction.GetName() : "-")}'");
            foreach (GameObject go in schedule.EnabledDuringCurfew ?? new GameObject[0])
                text.AppendLine($"  enabled during curfew: {(go != null ? Path(go.transform) : "-")}");
            foreach (GameObject go in schedule.EnabledDuringNoCurfew ?? new GameObject[0])
                text.AppendLine($"  enabled outside curfew: {(go != null ? Path(go.transform) : "-")}");

            foreach (NPCAction action in schedule.GetComponentsInChildren<NPCAction>(true))
            {
                string inList = schedule.ActionList.Contains(action) ? "in list" : "not in list";
                text.AppendLine($"  {action.GetType().Name}  '{action.GetName()}'  {action.GetTimeDescription()}  start {action.StartTime} end {action.GetEndTime()}  " +
                                $"active {action.gameObject.activeInHierarchy}  {inList}  {Path(action.transform)}");
            }
        }

        private static bool Near(Transform transform, Vector3 centre, out float distance)
        {
            distance = Vector3.Distance(centre, transform.position);
            return distance <= Radius && transform.gameObject.activeInHierarchy;
        }

        private static string Path(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }
    }
}
