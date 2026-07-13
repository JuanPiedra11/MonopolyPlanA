using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MonopolyPlanA.EditorTools
{
    /// <summary>
    /// Utilidad de editor: crea la escena Menu y registra Menu + Main
    /// en Build Settings. Ejecutar desde el menú "PlanA" de Unity.
    /// </summary>
    public static class PlanASetup
    {
        [MenuItem("PlanA/Crear escena Menu + configurar Build Settings")]
        public static void Setup()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("MenuBootstrap");
            go.AddComponent<MenuBootstrap>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Menu.unity");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Menu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true)
            };

            Debug.Log("PlanA: escena Menu creada y Build Settings configurados (Menu, Main).");
        }

        [MenuItem("PlanA/Limpiar scripts faltantes de la escena abierta")]
        public static void RemoveMissingScripts()
        {
            int removed = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (removed > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
                EditorSceneManager.SaveOpenScenes();
            }
            Debug.Log($"PlanA: {removed} script(s) faltante(s) eliminado(s) y escena guardada.");
        }

        [MenuItem("PlanA/Añadir CoinRainSettings a la escena abierta")]
        public static void AddCoinRainSettings()
        {
            var existing = Object.FindAnyObjectByType<CoinRainSettings>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("PlanA: CoinRainSettings ya existe — seleccionado en la jerarquía.");
                return;
            }
            var host = GameObject.Find("Bootstrap") ?? new GameObject("CoinRainSettings");
            host.AddComponent<CoinRainSettings>();
            Selection.activeGameObject = host;
            EditorSceneManager.MarkSceneDirty(host.scene);
            Debug.Log("PlanA: CoinRainSettings añadido. Elige el modo (AnimatedSprite/Particles) en el Inspector y guarda la escena.");
        }

        [MenuItem("PlanA/Añadir TokenStyleSettings a la escena abierta")]
        public static void AddTokenStyleSettings()
        {
            var existing = Object.FindAnyObjectByType<TokenStyleSettings>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("PlanA: TokenStyleSettings ya existe — seleccionado en la jerarquía.");
                return;
            }
            var host = GameObject.Find("Bootstrap") ?? new GameObject("TokenStyleSettings");
            host.AddComponent<TokenStyleSettings>();
            Selection.activeGameObject = host;
            EditorSceneManager.MarkSceneDirty(host.scene);
            Debug.Log("PlanA: TokenStyleSettings añadido. Activa/desactiva rey3D en el Inspector y guarda la escena.");
        }

        [MenuItem("PlanA/Crear AnimatorController del rey")]
        public static void CreateReyAnimator()
        {
            AnimationClip Clip(string file)
            {
                return AssetDatabase.LoadAllAssetsAtPath($"Assets/Resources/Models/{file}.fbx")
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.Contains("__preview"));
            }

            const string path = "Assets/Resources/Models/ReyAnimator.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("walking", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("victory", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("defeat", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var idle = sm.AddState("Idle");    idle.motion = Clip("Rey_Idle");
            var walk = sm.AddState("Walk");    walk.motion = Clip("Rey_Walkcycle");
            var vict = sm.AddState("Victory"); vict.motion = Clip("Rey_Victory");
            var defe = sm.AddState("Defeat");  defe.motion = Clip("Rey_Defeat");
            sm.defaultState = idle;

            // árbol de estados: idle ⇄ walk por bool; victoria/derrota por trigger
            var t1 = idle.AddTransition(walk);
            t1.AddCondition(AnimatorConditionMode.If, 0, "walking");
            t1.hasExitTime = false; t1.duration = 0.15f;

            var t2 = walk.AddTransition(idle);
            t2.AddCondition(AnimatorConditionMode.IfNot, 0, "walking");
            t2.hasExitTime = false; t2.duration = 0.15f;

            var tv = sm.AddAnyStateTransition(vict);
            tv.AddCondition(AnimatorConditionMode.If, 0, "victory");
            tv.hasExitTime = false; tv.duration = 0.1f; tv.canTransitionToSelf = false;

            var td = sm.AddAnyStateTransition(defe);
            td.AddCondition(AnimatorConditionMode.If, 0, "defeat");
            td.hasExitTime = false; td.duration = 0.1f; td.canTransitionToSelf = false;

            var bv = vict.AddTransition(idle);
            bv.hasExitTime = true; bv.exitTime = 0.95f; bv.duration = 0.15f;

            var bd = defe.AddTransition(idle);
            bd.hasExitTime = true; bd.exitTime = 0.95f; bd.duration = 0.15f;

            AssetDatabase.SaveAssets();
            Debug.Log("PlanA: ReyAnimator.controller creado en Resources/Models con idle/walk/victory/defeat.");
        }

        [MenuItem("PlanA/Build Windows (16:9)")]
        public static void BuildWindows()
        {
            // ventana fija 16:9 para que la UI (IMGUI anclada a fracciones) no se deforme
            PlayerSettings.companyName = "PlanA";
            PlayerSettings.productName = "Monopoly PlanA";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.runInBackground = true;

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Menu.unity", "Assets/Scenes/Main.unity" },
                locationPathName = "Builds/MonopolyPlanA/MonopolyPlanA.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"PlanA BUILD: {report.summary.result} → {report.summary.outputPath} " +
                      $"({report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errores)");
        }

        [MenuItem("PlanA/Reimportar cartas (corregir proporciones)")]
        public static void ReimportCards()
        {
            AssetDatabase.ImportAsset("Assets/Resources/Cards",
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("PlanA: cartas reimportadas sin reescalado a potencia de 2.");
        }
    }
}
