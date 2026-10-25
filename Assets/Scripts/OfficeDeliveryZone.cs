using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Converts carried documents into score after the owner holds this zone.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class OfficeDeliveryZone : MonoBehaviour
{
    [SerializeField, Min(0)] private int ownerPlayerId;
    [SerializeField, Min(0.1f)] private float requiredDeliverySeconds = 2f;

    private readonly Dictionary<OfficePlayerMotor, float> deliveryProgress =
        new Dictionary<OfficePlayerMotor, float>();

    /// <summary>Progress is normalized from 0 to 1 and can drive a HUD bar.</summary>
    public event Action<OfficePlayerMotor, float> ProgressChanged;

    private void Awake()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;

        // A kinematic body ensures trigger callbacks work even if the player has no Rigidbody.
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTrackPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        OfficePlayerMotor player = TryTrackPlayer(other);
        if (player == null)
            return;

        if (player.CarriedDocuments <= 0)
        {
            SetProgress(player, 0f);
            return;
        }

        float progress = deliveryProgress[player] + Time.deltaTime;
        float duration = Mathf.Max(0.1f, requiredDeliverySeconds);
        if (progress >= duration)
        {
            int delivered = player.DropCarriedDocuments();
            player.RecordDelivery(delivered);
            SetProgress(player, 0f);
            return;
        }

        SetProgress(player, progress);
    }

    private void OnTriggerExit(Collider other)
    {
        OfficePlayerMotor player = other.GetComponentInParent<OfficePlayerMotor>();
        if (player != null && deliveryProgress.Remove(player))
            ProgressChanged?.Invoke(player, 0f);
    }

    private OfficePlayerMotor TryTrackPlayer(Collider other)
    {
        OfficePlayerMotor player = other.GetComponentInParent<OfficePlayerMotor>();
        if (player == null || player.PlayerId != ownerPlayerId)
            return null;

        if (!deliveryProgress.ContainsKey(player))
        {
            deliveryProgress.Add(player, 0f);
            ProgressChanged?.Invoke(player, 0f);
        }

        return player;
    }

    private void SetProgress(OfficePlayerMotor player, float seconds)
    {
        deliveryProgress[player] = seconds;
        float normalizedProgress = Mathf.Clamp01(seconds / Mathf.Max(0.1f, requiredDeliverySeconds));
        ProgressChanged?.Invoke(player, normalizedProgress);
    }
}
