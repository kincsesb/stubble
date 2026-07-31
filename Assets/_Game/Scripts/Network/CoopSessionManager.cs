// Mirror + FizzySteamworks transport required.
// Install: Mirror (Asset Store, free) + FizzySteamworks (Package Manager git URL)
// Package Manager → Add from git URL: https://github.com/Chykary/FizzySteamworks.git
#if MIRROR
using Mirror;
using Steamworks;
#endif

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

#if MIRROR
        Callback<LobbyCreated_t>      _onLobbyCreated;
        Callback<GameLobbyJoinRequested_t> _onJoinRequested;
        Callback<LobbyEnter_t>        _onLobbyEntered;

        CSteamID _currentLobby;
        bool _isHost;
#endif

        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
#if MIRROR
            RegisterSteamCallbacks();
#endif
        }

        // ------------------------------------------------------------------ //
        // Public API
        // ------------------------------------------------------------------ //

        /// <summary>Create a Steam lobby and start as Mirror host.</summary>
        public void HostGame()
        {
#if MIRROR
            if (!SteamManager.Instance || !SteamAPI.IsSteamRunning())
            {
                Debug.LogWarning("[Coop] Steam not running — hosting without Steam lobby.");
                StartHost();
                return;
            }
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
#else
            Debug.LogWarning("[Coop] Mirror not installed — co-op unavailable.");
#endif
        }

        /// <summary>Join a friend's lobby by Steam lobby ID string.</summary>
        public void JoinGame(string lobbySteamIdStr)
        {
#if MIRROR
            if (ulong.TryParse(lobbySteamIdStr, out ulong id))
                SteamMatchmaking.JoinLobby(new CSteamID(id));
            else
                Debug.LogError("[Coop] Invalid lobby ID.");
#endif
        }

        /// <summary>Leave the current session cleanly.</summary>
        public void LeaveGame()
        {
#if MIRROR
            if (NetworkServer.active && NetworkClient.isConnected)
                NetworkManager.singleton.StopHost();
            else if (NetworkClient.isConnected)
                NetworkManager.singleton.StopClient();

            if (_currentLobby.IsValid())
            {
                SteamMatchmaking.LeaveLobby(_currentLobby);
                _currentLobby = CSteamID.Nil;
            }
#endif
        }

        // ------------------------------------------------------------------ //
        // Mirror helpers
        // ------------------------------------------------------------------ //

#if MIRROR
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

        // ------------------------------------------------------------------ //
        // Steam callbacks
        // ------------------------------------------------------------------ //

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
            // Store host Steam ID so joiners can connect
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
            if (_isHost) return; // host already started

            _currentLobby = new CSteamID(cb.m_ulSteamIDLobby);
            string hostId = SteamMatchmaking.GetLobbyData(_currentLobby, "HostSteamID");

            // FizzySteamworks uses Steam64 ID as address
            StartClient(hostId);
        }
#endif
    }
}
