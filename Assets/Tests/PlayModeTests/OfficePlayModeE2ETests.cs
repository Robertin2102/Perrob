using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Play Mode smoke flows for the main Unity scenes and player gameplay.
/// The multiplayer room flow still requires a two-client Photon run and is not
/// represented as passing by these local Play Mode checks.
/// </summary>
public sealed class OfficePlayModeE2ETests
{
    private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    private GameObject playerObject;

    [UnitySetUp]
    public IEnumerator LoadMenuBeforeEachFlow()
    {
        yield return LoadScene("Sc_Menu");
    }

    [UnityTearDown]
    public IEnumerator RestoreStartingScene()
    {
        if (playerObject != null)
            Object.Destroy(playerObject);

        if (SceneManager.GetActiveScene().name != "Sc_Menu")
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Sc_Menu", LoadSceneMode.Single);
            while (load != null && !load.isDone)
                yield return null;
        }
    }

    [UnityTest]
    [Category("E2E")]
    public IEnumerator MenuFlow_LoadsRoomAndLobbyControls()
    {
        yield return null;

        System.Type roomType = RequireType("OfficeFusionRoomController");
        System.Type menuType = RequireType("OfficePhotonMenuView");
        Assert.That(Object.FindFirstObjectByType(roomType), Is.Not.Null,
            "Sc_Menu should contain its Photon room controller.");
        Assert.That(Object.FindFirstObjectByType(menuType), Is.Not.Null,
            "Sc_Menu should contain the room and lobby UI controller.");
    }

    [UnityTest]
    [Category("E2E")]
    public IEnumerator GameFlow_LoadsGameplaySystems()
    {
        yield return LoadScene("Sc_Game");

        Assert.That(Object.FindFirstObjectByType(RequireType("OfficeFusionGameSceneController")), Is.Not.Null,
            "Sc_Game should contain the multiplayer player-spawn controller.");
        Assert.That(Object.FindFirstObjectByType(RequireType("OfficeHudController")), Is.Not.Null,
            "Sc_Game should contain the gameplay HUD.");
        Assert.That(Object.FindFirstObjectByType(RequireType("OfficeDocumentSpawner")), Is.Not.Null,
            "Sc_Game should contain the document spawn system.");
        Assert.That(Object.FindFirstObjectByType(RequireType("OfficeDeliveryZone")), Is.Not.Null,
            "Sc_Game should contain a delivery zone.");
    }

    [UnityTest]
    [Category("E2E")]
    public IEnumerator PlayerFlow_MovesCollectsDropsAndRecordsDelivery()
    {
        playerObject = new GameObject("PlayMode_E2E_Player");
        playerObject.transform.position = new Vector3(1000f, 10f, 1000f);
        playerObject.AddComponent<CharacterController>();

        System.Type motorType = RequireType("OfficePlayerMotor");
        Component motor = playerObject.AddComponent(motorType);

        Invoke(motor, motorType, "SetLocalInputEnabled", false);
        Invoke(motor, motorType, "SetMoveInput", new Vector2(0f, 1f));
        Vector3 startingPosition = playerObject.transform.position;
        yield return new WaitForSeconds(0.2f);

        Assert.That(Vector3.Distance(playerObject.transform.position, startingPosition), Is.GreaterThan(0.1f),
            "Play Mode input should move the player.");

        Assert.That(Call<bool>(motor, motorType, "TryCollectDocument"), Is.True);
        Assert.That(Call<bool>(motor, motorType, "TryCollectDocument"), Is.True);
        Assert.That(Read<int>(motor, motorType, "CarriedDocuments"), Is.EqualTo(2));

        int delivered = Call<int>(motor, motorType, "DropCarriedDocuments");
        Invoke(motor, motorType, "RecordDelivery", delivered);

        Assert.That(delivered, Is.EqualTo(2));
        Assert.That(Read<int>(motor, motorType, "CarriedDocuments"), Is.Zero);
        Assert.That(Read<int>(motor, motorType, "DeliveredDocuments"), Is.EqualTo(2));
    }

    private static IEnumerator LoadScene(string sceneName)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        Assert.That(load, Is.Not.Null, $"Unity should be able to load the {sceneName} scene.");
        while (!load.isDone)
            yield return null;

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
    }

    private static System.Type FindType(string typeName)
    {
        return System.Type.GetType($"{typeName}, Assembly-CSharp");
    }

    private static System.Type RequireType(string typeName)
    {
        System.Type type = FindType(typeName);
        Assert.That(type, Is.Not.Null, $"The {typeName} script should be available.");
        return type;
    }

    private static void Invoke(Component target, System.Type type, string methodName, params object[] args)
    {
        MethodInfo method = type.GetMethod(methodName, PublicInstance);
        Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} should exist.");
        method.Invoke(target, args);
    }

    private static T Call<T>(Component target, System.Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName, PublicInstance);
        Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} should exist.");
        return (T)method.Invoke(target, null);
    }

    private static T Read<T>(Component target, System.Type type, string propertyName)
    {
        PropertyInfo property = type.GetProperty(propertyName, PublicInstance);
        Assert.That(property, Is.Not.Null, $"{type.Name}.{propertyName} should exist.");
        return (T)property.GetValue(target);
    }
}
