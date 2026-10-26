using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode checks for the player's actual input and gameplay rules.
/// Reflection is used because the game's scripts currently compile into Unity's
/// predefined Assembly-CSharp assembly, which this test assembly cannot reference directly.
/// </summary>
public sealed class OfficePlayerMotorEditModeTests
{
    private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    private const BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private System.Type motorType;
    private GameObject playerObject;
    private Component motor;

    [SetUp]
    public void SetUp()
    {
        motorType = System.Type.GetType("OfficePlayerMotor, Assembly-CSharp");
        Assert.That(motorType, Is.Not.Null,
            "OfficePlayerMotor should be compiled in Assembly-CSharp.");

        playerObject = new GameObject("OfficePlayerMotor_TestInstance");
        motor = playerObject.AddComponent(motorType);

        // Keep input deterministic and initialize the same runtime defaults as a spawned player.
        Invoke("SetLocalInputEnabled", false);
        MethodInfo awake = motorType.GetMethod("Awake", NonPublicInstance);
        Assert.That(awake, Is.Not.Null, "OfficePlayerMotor.Awake should exist.");
        awake.Invoke(motor, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (playerObject != null)
            Object.DestroyImmediate(playerObject);
    }

    [Test]
    public void SetMoveInput_ClampsInputToUnitMagnitude()
    {
        Invoke("SetMoveInput", new Vector2(3f, 4f));

        Vector2 captured = Call<Vector2>("CaptureMoveInput");
        Assert.That(captured.magnitude, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(captured.x, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(captured.y, Is.EqualTo(0.8f).Within(0.0001f));
    }

    [Test]
    public void SetCarriedDocuments_ClampsBelowZeroAndAboveCapacity()
    {
        Invoke("SetCarriedDocuments", -4);
        Assert.That(Read<int>("CarriedDocuments"), Is.Zero);

        Invoke("SetCarriedDocuments", 99);
        Assert.That(Read<int>("CarriedDocuments"), Is.EqualTo(Read<int>("MaximumDocuments")));
    }

    [Test]
    public void TryCollectDocument_IncrementsCarriedCount()
    {
        bool collected = Call<bool>("TryCollectDocument");

        Assert.That(collected, Is.True);
        Assert.That(Read<int>("CarriedDocuments"), Is.EqualTo(1));
    }

    [Test]
    public void TryCollectDocument_RejectsDocumentsAfterCapacity()
    {
        int capacity = Read<int>("MaximumDocuments");
        for (int i = 0; i < capacity; i++)
            Assert.That(Call<bool>("TryCollectDocument"), Is.True);

        Assert.That(Call<bool>("TryCollectDocument"), Is.False);
        Assert.That(Read<int>("CarriedDocuments"), Is.EqualTo(capacity));
    }

    [Test]
    public void CurrentMoveSpeed_DecreasesForOneCarriedDocument()
    {
        float baseSpeed = Read<float>("CurrentMoveSpeed");
        Invoke("TryCollectDocument");

        Assert.That(Read<float>("CurrentMoveSpeed"), Is.LessThan(baseSpeed));
        Assert.That(Read<float>("CurrentLoadPenalty"), Is.EqualTo(0.08f).Within(0.0001f));
    }

    [Test]
    public void CurrentLoadPenalty_DoesNotExceedConfiguredMaximum()
    {
        Invoke("SetCarriedDocuments", 5);

        Assert.That(Read<float>("CurrentLoadPenalty"), Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(Read<float>("CurrentMoveSpeed"), Is.EqualTo(3f).Within(0.0001f));
    }

    [Test]
    public void DropCarriedDocuments_ReturnsCountAndClearsLoad()
    {
        Invoke("SetCarriedDocuments", 3);

        int dropped = Call<int>("DropCarriedDocuments");

        Assert.That(dropped, Is.EqualTo(3));
        Assert.That(Read<int>("CarriedDocuments"), Is.Zero);
    }

    [Test]
    public void RecordDelivery_AddsPositiveDocumentCountToScore()
    {
        Invoke("RecordDelivery", 3);
        Invoke("RecordDelivery", 2);

        Assert.That(Read<int>("DeliveredDocuments"), Is.EqualTo(5));
    }

    [Test]
    public void RecordDelivery_IgnoresZeroAndNegativeCounts()
    {
        Invoke("RecordDelivery", 0);
        Invoke("RecordDelivery", -2);

        Assert.That(Read<int>("DeliveredDocuments"), Is.Zero);
    }

    [Test]
    public void TakeDamage_ReducesEnergyAndStunsPlayer()
    {
        Invoke("TakeDamage", 25f);

        Assert.That(Read<float>("Energy"), Is.EqualTo(75f).Within(0.0001f));
        Assert.That(Read<bool>("IsStunned"), Is.True);
    }

    private void Invoke(string methodName, params object[] arguments)
    {
        MethodInfo method = motorType.GetMethod(methodName, PublicInstance);
        Assert.That(method, Is.Not.Null, $"OfficePlayerMotor.{methodName} should exist.");
        method.Invoke(motor, arguments);
    }

    private T Call<T>(string methodName, params object[] arguments)
    {
        MethodInfo method = motorType.GetMethod(methodName, PublicInstance);
        Assert.That(method, Is.Not.Null, $"OfficePlayerMotor.{methodName} should exist.");
        return (T)method.Invoke(motor, arguments);
    }

    private T Read<T>(string propertyName)
    {
        PropertyInfo property = motorType.GetProperty(propertyName, PublicInstance);
        Assert.That(property, Is.Not.Null, $"OfficePlayerMotor.{propertyName} should exist.");
        return (T)property.GetValue(motor);
    }
}
