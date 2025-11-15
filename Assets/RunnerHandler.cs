using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class RunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Session")]
    [SerializeField] private string sessionName = "chaos-duel";
    [SerializeField] private int gameSceneBuildIndex = 1;

    [Header("Spawn")]
    [SerializeField] private NetworkPrefabRef playerPrefab;

    private NetworkRunner _runner;

    // =========================
    // === BUTTON: HOST GAME ===
    // =========================
    public async void StartHost()
    {
        Debug.Log("[RunnerHandler] StartHost CLICK!");

        if (_runner != null)
        {
            Debug.LogWarning("[RunnerHandler] Runner already running, ignoring StartHost.");
            return;
        }

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.name = "NetworkRunner (Host)";


        _runner.ProvideInput = true;

       
        _runner.AddCallbacks(this);

        
        var sceneMgr = gameObject.AddComponent<NetworkSceneManagerDefault>();

        Debug.Log("[RunnerHandler] Starting HOST game...");
        var result = await _runner.StartGame(new StartGameArgs
        {
            GameMode     = GameMode.Host,
            SessionName  = sessionName,
            Scene        = SceneRef.FromIndex(gameSceneBuildIndex),
            SceneManager = sceneMgr
        });

        if (result.Ok)
        {
            Debug.Log("[RunnerHandler] HOST started OK ");
        }
        else
        {
            Debug.LogError($"[RunnerHandler] HOST FAILED: {result.ShutdownReason}");
        }
    }

    // ==========================
    // === BUTTON: JOIN GAME ====
    // ==========================
    public async void StartClient()
    {
        Debug.Log("[RunnerHandler] StartClient CLICK!");

        if (_runner != null)
        {
            Debug.LogWarning("[RunnerHandler] Runner already running, ignoring StartClient.");
            return;
        }

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.name = "NetworkRunner (Client)";
        _runner.ProvideInput = true; 

        _runner.AddCallbacks(this);

        var sceneMgr = gameObject.AddComponent<NetworkSceneManagerDefault>();

        Debug.Log("[RunnerHandler] Starting CLIENT...");
        var result = await _runner.StartGame(new StartGameArgs
        {
            GameMode     = GameMode.Client,
            SessionName  = sessionName,
            SceneManager = sceneMgr
        });

        if (result.Ok)
        {
            Debug.Log("[RunnerHandler] CLIENT started OK ");
        }
        else
        {
            Debug.LogError($"[RunnerHandler] CLIENT FAILED: {result.ShutdownReason}");
        }
    }

public void OnInput(NetworkRunner runner, NetworkInput input)
{
    PlayerInputData data = new PlayerInputData();

    float x = Input.GetAxisRaw("Horizontal");
    float y = Input.GetAxisRaw("Vertical");

    if (RouletteModifierManager.ReverseControls)
        x = -x;

    data.Move = new Vector2(x, y).normalized;
    data.JumpPressed   = Input.GetKeyDown(KeyCode.Space);
    data.JumpHeld      = Input.GetKey(KeyCode.Space);
    data.AttackPressed = Input.GetMouseButtonDown(0);

    input.Set(data);
}



    // ==========================
    // === SPAWN GRACZA ========
    // ==========================
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[RunnerHandler] Player joined: {player}");

        if (!runner.IsServer)
            return;

        Vector3 pos = FindSpawnPoint();

        NetworkObject playerObj = runner.Spawn(playerPrefab, pos, Quaternion.identity, player);


        runner.SetPlayerObject(player, playerObj);

        Debug.Log($"[RunnerHandler] Spawned player object {playerObj.name} for {player}");
    }

    Vector3 FindSpawnPoint()
    {
        var sm = UnityEngine.Object.FindAnyObjectByType<SpawnManager2D>();
        return sm ? sm.GetSpawnPoint() : Vector3.zero;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {

        if (!runner.IsServer)
            return;

        var obj = runner.GetPlayerObject(player);
        if (obj != null)
        {
            Debug.Log($"[RunnerHandler] Despawning player object for {player}");
            runner.Despawn(obj);
        }
    }


    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
