using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>Converts carried documents into score after the owner holds this zone.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class OfficeDeliveryZone : NetworkBehaviour
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
        OfficeFusionPlayer player = other.GetComponentInParent<OfficeFusionPlayer>();
        if (IsPlayerStateAuthority(player))
            TryTrackPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        OfficeFusionPlayer networkPlayer = other.GetComponentInParent<OfficeFusionPlayer>();
        if (!IsPlayerStateAuthority(networkPlayer))
            return;

        networkPlayer = TryTrackPlayer(other);
        if (networkPlayer == null)
            return;
        OfficePlayerMotor player = networkPlayer.Motor;
        if (player == null)
            return;

        if (networkPlayer.CarriedDocuments <= 0)
        {
            SetProgress(networkPlayer, 0f);
            return;
        }

        float progress = deliveryProgress[player] + Time.deltaTime;
        float duration = Mathf.Max(0.1f, requiredDeliverySeconds);
        if (progress >= duration)
        {
            int delivered = networkPlayer.DropCarriedDocumentsAuthority();
            networkPlayer.DeliverDocumentsAuthority(delivered);
            SetProgress(networkPlayer, 0f);
            return;
        }

        SetProgress(networkPlayer, progress);
    }

    private void OnTriggerExit(Collider other)
    {
        OfficeFusionPlayer networkPlayer = other.GetComponentInParent<OfficeFusionPlayer>();
        OfficePlayerMotor player = networkPlayer != null ? networkPlayer.Motor : null;
        if (IsPlayerStateAuthority(networkPlayer) && player != null && deliveryProgress.Remove(player))
        {
            networkPlayer.SetDeliveryProgressAuthority(0f);
            ProgressChanged?.Invoke(player, 0f);
        }
    }

    private OfficeFusionPlayer TryTrackPlayer(Collider other)
    {
        OfficeFusionPlayer networkPlayer = other.GetComponentInParent<OfficeFusionPlayer>();
        OfficePlayerMotor player = networkPlayer != null ? networkPlayer.Motor : null;
        if (!IsPlayerStateAuthority(networkPlayer) || player == null ||
            networkPlayer.PlayerSlot != ownerPlayerId)
            return null;

        if (!deliveryProgress.ContainsKey(player))
        {
            deliveryProgress.Add(player, 0f);
            ProgressChanged?.Invoke(player, 0f);
        }

        return networkPlayer;
    }

    private static bool IsPlayerStateAuthority(OfficeFusionPlayer player)
    {
        return player != null && player.Object != null && player.Object.HasStateAuthority;
    }

    private void SetProgress(OfficeFusionPlayer networkPlayer, float seconds)
    {
        OfficePlayerMotor player = networkPlayer.Motor;
        deliveryProgress[player] = seconds;
        float normalizedProgress = Mathf.Clamp01(seconds / Mathf.Max(0.1f, requiredDeliverySeconds));
        networkPlayer.SetDeliveryProgressAuthority(normalizedProgress);
        ProgressChanged?.Invoke(player, normalizedProgress);
    }
}
