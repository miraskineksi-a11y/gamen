using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Caribbean.EditorTools
{
    /// <summary>
    /// При первом открытии проекта создаёт сцену Assets/Scenes/Main.unity с объектом игры,
    /// добавляет её в Build Settings и открывает. Меню «Caribbean» — служебные команды.
    /// </summary>
    [InitializeOnLoad]
    public static class CaribbeanSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string DoneKey = "Caribbean.SetupDone";

        static CaribbeanSetup()
        {
            EditorApplication.delayCall += FirstRun;
        }

        static void FirstRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(DoneKey, false)) return;
            SessionState.SetBool(DoneKey, true);
            if (!File.Exists(ScenePath))
            {
                // первый запуск проекта: линейный цвет (красивее освещение) и имя игры
                if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
                if (PlayerSettings.productName == "CaribbeanTreasure" || string.IsNullOrEmpty(PlayerSettings.productName))
                    PlayerSettings.productName = "Клад Кровавой Бороды";
                var active = SceneManager.GetActiveScene();
                bool canReplace = string.IsNullOrEmpty(active.path) && !active.isDirty;
                CreateScene(canReplace);
            }
            EnsureBuildSettings();
        }

        [MenuItem("Caribbean/Открыть игровую сцену", priority = 1)]
        static void OpenScene()
        {
            if (!File.Exists(ScenePath)) CreateScene(false);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Caribbean/Пересоздать игровую сцену", priority = 2)]
        static void RecreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScene(true);
            EnsureBuildSettings();
        }

        [MenuItem("Caribbean/Переимпортировать модели из Blender", priority = 20)]
        static void ReimportModels()
        {
            AssetDatabase.ImportAsset("Assets/Resources/Models", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("Модели переимпортированы.");
        }

        [MenuItem("Caribbean/Удалить сохранение игры", priority = 40)]
        static void DeleteSave()
        {
            Caribbean.SaveData.Delete();
            Debug.Log("Сохранение удалено.");
        }

        static void CreateScene(bool openAfter)
        {
            var current = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, openAfter ? NewSceneMode.Single : NewSceneMode.Additive);
            var go = new GameObject("Caribbean Game");
            go.AddComponent<Caribbean.Game>();
            SceneManager.MoveGameObjectToScene(go, scene);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!openAfter) EditorSceneManager.CloseScene(scene, true);
            else EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("Создана сцена " + ScenePath + ". Нажмите Play!");
        }

        static void EnsureBuildSettings()
        {
            if (!File.Exists(ScenePath)) return;
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list) if (s.path == ScenePath) return;
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
