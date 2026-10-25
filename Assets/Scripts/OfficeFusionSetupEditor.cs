#if UNITY_EDITOR
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Configures the existing game prefabs and scene objects for host-authoritative Fusion play.</summary>
public static class OfficeFusionSetupEditor
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string DocumentPrefabPath = "Assets/Prefabs/Document.prefab";
    private const string PaperBallPrefabPath = "Assets/Prefabs/PaperBall.prefab";

    [MenuItem("Pelea por el Salario Mínimo/Actualizar prefab Document para Photon")]
    public static void UpdateDocumentPrefabForNetwork()
    {
        NetworkObject documentPrefab = ConfigureNetworkPrefab(DocumentPrefabPath, true, false);
        if (documentPrefab == null)
            return;

        AssetDatabase.SaveAssets();
        NetworkProjectConfigUtilities.RebuildPrefabTable();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Documento preparado",
            "Document.prefab ahora sincroniza su posición con NetworkTransform y quedó en la tabla de prefabs de Fusion.",
            "Entendido");
    }

    [MenuItem("Pelea por el Salario Mínimo/Configurar jugador y sala Photon")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Sc_Game")
        {
            EditorUtility.DisplayDialog("Escena incorrecta", "Abre Sc_Game antes de configurar Photon.", "Entendido");
            return;
        }

        NetworkObject paperBallPrefab = ConfigureNetworkPrefab(PaperBallPrefabPath, true, true);
        NetworkObject documentPrefab = ConfigureNetworkPrefab(DocumentPrefabPath, true, false);
        NetworkObject playerPrefab = ConfigurePlayerPrefab(paperBallPrefab);
        if (paperBallPrefab == null || documentPrefab == null || playerPrefab == null)
            return;

        Transform spawnOne = FindSpawn(scene, "PlayerSpawn_01");
        Transform spawnTwo = FindSpawn(scene, "PlayerSpawn_02");
        if (spawnOne == null || spawnTwo == null)
        {
            EditorUtility.DisplayDialog("Faltan apariciones", "Sc_Game debe tener PlayerSpawn_01 y PlayerSpawn_02.", "Entendido");
            return;
        }

        GameObject playerTemplate = FindPlayerTemplate(scene);
        OfficeDocumentSpawner spawner = FindInScene<OfficeDocumentSpawner>(scene);
        OfficeDeliveryZone[] deliveryZones = FindAllInScene<OfficeDeliveryZone>(scene);
        if (playerTemplate == null || spawner == null || deliveryZones.Length < 2)
        {
            EditorUtility.DisplayDialog("Faltan referencias en Sc_Game", "Se necesita la instancia de Player, OfficeDocumentSpawner y los dos triggers OfficeDeliveryZone.", "Entendido");
            return;
        }

        EnsureNetworkObject(spawner.gameObject);
        foreach (OfficeDeliveryZone zone in deliveryZones)
            EnsureNetworkObject(zone.gameObject);

        OfficeFusionGameSceneController gameController = FindInScene<OfficeFusionGameSceneController>(scene);
        if (gameController == null)
        {
            GameObject controllerObject = new GameObject("OfficeFusionGameScene");
            Undo.RegisterCreatedObjectUndo(controllerObject, "Crear controlador de partida Photon");
            SceneManager.MoveGameObjectToScene(controllerObject, scene);
            gameController = controllerObject.AddComponent<OfficeFusionGameSceneController>();
        }

        gameController.Configure(playerPrefab, spawnOne, spawnTwo, playerTemplate);
        EditorUtility.SetDirty(gameController);

        // This scene instance is only an authoring placeholder. Fusion spawns the two registered
        // prefab instances when the Host loads the match scene.
        Undo.RecordObject(playerTemplate, "Ocultar plantilla de jugador en Sc_Game");
        playerTemplate.SetActive(false);
        EditorUtility.SetDirty(playerTemplate);

        NetworkObjectPostprocessor.BakeScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        ConfigureMenuScene(playerPrefab);
        EnableClientServerForWebGL();

        AssetDatabase.SaveAssets();
        NetworkProjectConfigUtilities.RebuildPrefabTable();
        AssetDatabase.Refresh();
        SceneManager.SetActiveScene(scene);

        Selection.activeGameObject = gameController.gameObject;
        EditorUtility.DisplayDialog("Photon preparado para WebGL",
            "Player, bolas de papel, documentos y escritorios quedaron registrados para Host autoritativo. Se habilitó Host/Client para WebGL. Photon advierte que Host en WebGL puede tener más latencia y depende de que la pestaña permanezca activa.",
            "Entendido");
    }

    private static NetworkObject ConfigurePlayerPrefab(NetworkObject paperBallPrefab)
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

            EnsureNetworkObject(contents);
            if (contents.GetComponent<NetworkTransform>() == null)
                contents.AddComponent<NetworkTransform>();
            OfficeFusionPlayer fusionPlayer = contents.GetComponent<OfficeFusionPlayer>();
            if (fusionPlayer == null)
                fusionPlayer = contents.AddComponent<OfficeFusionPlayer>();
            fusionPlayer.ConfigurePaperBallPrefab(paperBallPrefab);

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath).GetComponent<NetworkObject>();
    }

    private static NetworkObject ConfigureNetworkPrefab(string path, bool addNetworkTransform, bool requireProjectile)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            EditorUtility.DisplayDialog("Falta un prefab", $"No encontré {path}.", "Entendido");
            return null;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnsureNetworkObject(contents);
            if (addNetworkTransform && contents.GetComponent<NetworkTransform>() == null)
                contents.AddComponent<NetworkTransform>();

            if (requireProjectile && contents.GetComponent<PaperBallProjectile>() == null)
                contents.AddComponent<PaperBallProjectile>();
            if (!requireProjectile && contents.GetComponent<OfficeDocumentPickup>() == null)
                contents.AddComponent<OfficeDocumentPickup>();

            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<NetworkObject>();
    }

    private static void EnsureNetworkObject(GameObject target)
    {
        if (target.GetComponent<NetworkObject>() == null)
            target.AddComponent<NetworkObject>();
    }

    private static void ConfigureMenuScene(NetworkObject playerPrefab)
    {
        Scene menuScene = EditorSceneManager.OpenScene("Assets/Scenes/Sc_Menu.unity", OpenSceneMode.Additive);
        OfficeFusionRoomController roomController = FindInScene<OfficeFusionRoomController>(menuScene);
        if (roomController != null)
        {
            roomController.Configure(playerPrefab, null, null, null);
            EditorUtility.SetDirty(roomController);
            EditorSceneManager.MarkSceneDirty(menuScene);
            EditorSceneManager.SaveScene(menuScene);
        }
        EditorSceneManager.CloseScene(menuScene, true);
    }

    private static void EnableClientServerForWebGL()
    {
        NetworkProjectConfig config = NetworkProjectConfig.Global;
        config.AllowClientServerModesInWebGL = true;
        NetworkProjectConfigUtilities.SaveGlobalConfig(config);
        NetworkProjectConfigUtilities.ImportGlobalConfig();
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

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        System.Collections.Generic.List<T> found = new System.Collections.Generic.List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            found.AddRange(root.GetComponentsInChildren<T>(true));
        return found.ToArray();
    }
}
#endif
