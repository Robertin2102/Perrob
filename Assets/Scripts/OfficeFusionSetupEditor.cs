#if UNITY_EDITOR
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Configures the existing player prefab and connects it to Sc_Game for Fusion.</summary>
public static class OfficeFusionSetupEditor
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("Pelea por el Salario Mínimo/Configurar jugador y sala Photon")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Sc_Game")
        {
            EditorUtility.DisplayDialog("Escena incorrecta", "Abre Sc_Game antes de configurar Photon.", "Entendido");
            return;
        }

        NetworkObject networkPrefab = ConfigurePlayerPrefab();
        if (networkPrefab == null)
            return;

        Transform spawnOne = FindSpawn(scene, "PlayerSpawn_01");
        Transform spawnTwo = FindSpawn(scene, "PlayerSpawn_02");
        if (spawnOne == null || spawnTwo == null)
        {
            EditorUtility.DisplayDialog("Faltan apariciones", "Sc_Game debe tener PlayerSpawn_01 y PlayerSpawn_02.", "Entendido");
            return;
        }

        GameObject playerTemplate = FindPlayerTemplate(scene);
        if (playerTemplate == null)
        {
            EditorUtility.DisplayDialog("Falta el jugador de referencia", "Deja una instancia de Player.prefab en Sc_Game para que pueda ocultarse al iniciar la partida en red.", "Entendido");
            return;
        }

        OfficeFusionGameSceneController gameController = FindInScene<OfficeFusionGameSceneController>(scene);
        if (gameController == null)
        {
            GameObject controllerObject = new GameObject("OfficeFusionGameScene");
            Undo.RegisterCreatedObjectUndo(controllerObject, "Crear controlador de partida Photon");
            SceneManager.MoveGameObjectToScene(controllerObject, scene);
            gameController = controllerObject.AddComponent<OfficeFusionGameSceneController>();
        }

        gameController.Configure(networkPrefab, spawnOne, spawnTwo, playerTemplate);
        EditorUtility.SetDirty(gameController);

        // The instance in the scene is only an authoring reference. Fusion will spawn the two
        // registered prefab instances when the match starts, so keep this one out of the session.
        Undo.RecordObject(playerTemplate, "Ocultar plantilla de jugador en Sc_Game");
        playerTemplate.SetActive(false);
        EditorUtility.SetDirty(playerTemplate);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.SetDirty(networkPrefab);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = gameController.gameObject;
        EditorUtility.DisplayDialog("Jugador de Photon configurado",
            "Player.prefab quedó registrado con NetworkObject, NetworkTransform y OfficeFusionPlayer. Sc_Game usará los dos puntos PlayerSpawn para crear un jugador por participante.",
            "Entendido");
    }

    private static NetworkObject ConfigurePlayerPrefab()
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefabAsset == null)
        {
            EditorUtility.DisplayDialog("No encontré Player.prefab", $"No existe el prefab esperado en {PlayerPrefabPath}.", "Entendido");
            return null;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            OfficePlayerMotor motor = contents.GetComponent<OfficePlayerMotor>();
            OfficePaperThrower thrower = contents.GetComponent<OfficePaperThrower>();
            CharacterController characterController = contents.GetComponent<CharacterController>();
            if (motor == null || thrower == null || characterController == null)
            {
                EditorUtility.DisplayDialog("Prefab incompleto", "Player.prefab debe tener en su raíz OfficePlayerMotor, OfficePaperThrower y CharacterController.", "Entendido");
                return null;
            }

            Animator animator = contents.GetComponentInChildren<Animator>(true);
            if (animator != null)
                animator.applyRootMotion = false;

            if (contents.GetComponent<NetworkObject>() == null)
                contents.AddComponent<NetworkObject>();
            if (contents.GetComponent<NetworkTransform>() == null)
                contents.AddComponent<NetworkTransform>();
            if (contents.GetComponent<OfficeFusionPlayer>() == null)
                contents.AddComponent<OfficeFusionPlayer>();

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
        NetworkProjectConfigUtilities.RebuildPrefabTable();
        AssetDatabase.Refresh();
        return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath).GetComponent<NetworkObject>();
    }

    private static Transform FindSpawn(Scene scene, string spawnName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == spawnName)
                return root.transform;
        }

        return null;
    }

    private static GameObject FindPlayerTemplate(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            OfficePlayerMotor motor = root.GetComponentInChildren<OfficePlayerMotor>(true);
            if (motor != null)
                return motor.transform.root.gameObject;
        }

        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }
}
#endif
