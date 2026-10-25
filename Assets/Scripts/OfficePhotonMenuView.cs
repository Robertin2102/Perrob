using UnityEngine;
using UnityEngine.UI;

/// <summary>Connects the uGUI room-code menu to the persistent Photon room controller.</summary>
public sealed class OfficePhotonMenuView : MonoBehaviour
{
    [SerializeField] private OfficeFusionRoomController roomController;
    [SerializeField] private InputField roomCodeInput;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Text statusText;
    [SerializeField] private Text playerCountText;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private Text lobbyRoomText;
    [SerializeField] private Text lobbyStatusText;
    [SerializeField] private Text readyPlayersText;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startMatchButton;
    [SerializeField] private GameObject[] connectionElements;

    public void Configure(
        OfficeFusionRoomController room,
        InputField roomCode,
        Button createButton,
        Button joinButton,
        Text status,
        Text playerCount,
        GameObject roomPanel,
        Text lobbyRoom,
        Text lobbyStatus,
        Text readyPlayers,
        Button ready,
        Button startMatch,
        GameObject[] roomElements)
    {
        roomController = room;
        roomCodeInput = roomCode;
        createRoomButton = createButton;
        joinRoomButton = joinButton;
        statusText = status;
        playerCountText = playerCount;
        lobbyPanel = roomPanel;
        lobbyRoomText = lobbyRoom;
        lobbyStatusText = lobbyStatus;
        readyPlayersText = readyPlayers;
        readyButton = ready;
        startMatchButton = startMatch;
        connectionElements = roomElements;
    }

    private void OnEnable()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(CreateRoom);
        if (joinRoomButton != null)
            joinRoomButton.onClick.AddListener(JoinRoom);
        if (readyButton != null)
            readyButton.onClick.AddListener(ToggleReady);
        if (startMatchButton != null)
            startMatchButton.onClick.AddListener(StartMatch);
    }

    private void OnDisable()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.RemoveListener(CreateRoom);
        if (joinRoomButton != null)
            joinRoomButton.onClick.RemoveListener(JoinRoom);
        if (readyButton != null)
            readyButton.onClick.RemoveListener(ToggleReady);
        if (startMatchButton != null)
            startMatchButton.onClick.RemoveListener(StartMatch);
    }

    private void Update()
    {
        if (roomController == null)
            return;

        bool busy = roomController.IsConnecting || roomController.HasActiveSession;
        bool inLobby = roomController.HasActiveSession;
        if (createRoomButton != null)
            createRoomButton.interactable = !busy;
        if (joinRoomButton != null)
            joinRoomButton.interactable = !busy;

        if (connectionElements != null)
        {
            foreach (GameObject element in connectionElements)
            {
                if (element != null && element.activeSelf == inLobby)
                    element.SetActive(!inLobby);
            }
        }

        if (lobbyPanel != null && lobbyPanel.activeSelf != inLobby)
            lobbyPanel.SetActive(inLobby);

        if (statusText != null)
            statusText.text = roomController.Status;
        if (playerCountText != null)
        {
            int count = roomController.ConnectedPlayers;
            playerCountText.text = roomController.HasActiveSession
                ? $"EMPLEADOS CONECTADOS   {count} / 2"
                : "EMPLEADOS CONECTADOS   0 / 2";
        }

        if (inLobby)
        {
            if (lobbyRoomText != null)
                lobbyRoomText.text = $"CÓDIGO DE SALA   {roomController.RoomName}";
            if (lobbyStatusText != null)
                lobbyStatusText.text = roomController.Status;
            if (readyPlayersText != null)
                readyPlayersText.text = roomController.ReadySummary;
            if (readyButton != null)
            {
                readyButton.interactable = roomController.ConnectedPlayers == 2 && roomController.HasLobbyState;
                Text label = readyButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = roomController.LocalPlayerReady ? "CANCELAR LISTO" : "ESTOY LISTO";
            }
            if (startMatchButton != null)
            {
                startMatchButton.gameObject.SetActive(roomController.IsHost && roomController.ConnectedPlayers == 2);
                startMatchButton.interactable = roomController.CanStartMatch;
            }
        }
    }

    private void CreateRoom()
    {
        if (roomController != null)
            roomController.CreateRoom(ReadRoomCode());
    }

    private void JoinRoom()
    {
        if (roomController != null)
            roomController.JoinRoom(ReadRoomCode());
    }

    private void ToggleReady()
    {
        if (roomController != null)
            roomController.ToggleReady();
    }

    private void StartMatch()
    {
        if (roomController != null)
            roomController.StartMatch();
    }

    private string ReadRoomCode()
    {
        return roomCodeInput != null ? roomCodeInput.text : "salario-minimo";
    }
}
