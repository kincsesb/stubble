using Mirror;
using Steamworks;
using UnityEngine;
using Fields.Core;

namespace Fields.Network
{
    /// <summary>
    /// P2-01: Co-op session manager — Steam lobby create/join, Mirror host/client start.
    /// Host-authoritative. No dedicated server, no host migration (spec).
    /// Max 4 players via Steam P2P (FizzySteamworks transport).
    /// </summary>
    public class CoopSessionManager : MonoBehaviour
    {
        public static CoopSessionManager Instance { get; private set; }

        [Header("Config")]
        public int maxPlayers = 4;
        public GameObject playerPrefab;

        Callback<LobbyCreated_t>           _onLobbyCreated;
        Callback<GameLobbyJoinRequested_t>  _onJoinRequested;
        Callback<LobbyEnter_t>             _onLobbyEntered;

        CSteamID _currentLobby;
        bool _isHost;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            RegisterSteamCallbacks();
        }

        public void HostGame()
        {
            if (!SteamManager.Instance || !SteamAPI.IsSteamRunning())
            {
                Debug.LogWarning("[Coop] Steam not running — hosting without Steam lobby.");
                StartHost();
                return;
            }
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
        }

        public void JoinGame(string lobbySteamIdStr)
        {
            if (ulong.TryParse(lobbySteamIdStr, out ulong id))
                SteamMatchmaking.JoinLobby(new CSteamID(id));
            else
                Debug.LogError("[Coop] Invalid lobby ID.");
        }

        public void LeaveGame()
        {
            if (NetworkServer.active && NetworkClient.isConnected)
                NetworkManager.singleton.StopHost();
            else if (NetworkClient.isConnected)
                NetworkManager.singleton.StopClient();

            if (_currentLobby.IsValid())
            {
                SteamMatchmaking.LeaveLobby(_currentLobby);
                _currentLobby = CSteamID.Nil;
            }
        }

        void StartHost()
        {
            _isHost = true;
            NetworkManager.singleton.StartHost();
            Debug.Log("[Coop] Started as host.");
        }

        void StartClient(string ip)
        {
            _isHost = false;
            NetworkManager.singleton.networkAddress = ip;
            NetworkManager.singleton.StartClient();
            Debug.Log($"[Coop] Connecting to {ip}.");
        }

        void RegisterSteamCallbacks()
        {
            _onLobbyCreated  = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _onJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            _onLobbyEntered  = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        }

        void OnLobbyCreated(LobbyCreated_t cb)
        {
            if (cb.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError($"[Coop] Lobby creation failed: {cb.m_eResult}");
                return;
            }
            _currentLobby = new CSteamID(cb.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(_currentLobby, "HostSteamID",
                SteamUser.GetSteamID().ToString());
            StartHost();
        }

        void OnJoinRequested(GameLobbyJoinRequested_t cb)
        {
            SteamMatchmaking.JoinLobby(cb.m_steamIDLobby);
        }

        void OnLobbyEntered(LobbyEnter_t cb)
        {
            if (_isHost) return;

            _currentLobby = new CSteamID(cb.m_ulSteamIDLobby);
            string hostId = SteamMatchmaking.GetLobbyData(_currentLobby, "HostSteamID");
            StartClient(hostId);
        }
    }
}
