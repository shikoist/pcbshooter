using BeardedManStudios.Forge.Networking;
using BeardedManStudios.Forge.Networking.Generated;
using BeardedManStudios.Forge.Networking.Unity;
using BeardedManStudios.SimpleJSON;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
//using UnityEditor;

public enum GameScreen
{
    Menu,
    Game
}

public class MainScript : MonoBehaviour
{
    // Строка ника
    public string nickname;

    // Максимальное количество игроков
    public int maxPlayers = 16;

    public GameObject mainMenuPanel;
    public GameObject inGamePanel;

    public InputField ifNickname; // Поле ввода ника

    public int gameScreen = 0;
    
    public int portNumber;
    public string ipAddress;

    // From Forge Multiplayer menu
    public InputField ifIpAddress = null;
    public InputField ifPortNumber = null;
    public Text lblPortNumber;
    public bool DontChangeSceneOnConnect = false;

    public GameObject networkManager = null;
    
    private NetworkManager mgr = null;
    private NetWorker server;
    private NetWorker netWorker;

    public bool useMainThreadManagerForRPCs = true;
    public bool useInlineChat = false;

    public bool getLocalNetworkConnections = false;

    public SoundManager soundManager;

    public Text playerListText;
    public List<Player> playerList;

    public Button connectButton;
    public Button hostButton;

    float timerConnect1;
    float rateTimerConnect1 = 5;

    void Awake()
    {
        soundManager = GameObject.FindObjectOfType<SoundManager>();
    }

    // Use this for initialization
    void Start()
    {
        //versionText.text = PlayerSettings.bundleVersion;

        playerList = new List<Player>();

        // Ограничиваем фпс в случае, если это будет запущено под линуксом без графики
        Application.targetFrameRate = 60;

        // Если ник пустой, то делаем рандомный
        if (nickname == "") nickname = "Player " + Random.Range(0,65536).ToString();
        ifNickname.text = nickname;

        // Обновление строчек, где указан порт
        ifPortNumber.text = portNumber.ToString();
        lblPortNumber.text = portNumber.ToString();

        // Обновление строчек, где указан айпи
        ifIpAddress.text = ipAddress;

        NetWorker.PingForFirewall((ushort)portNumber);

        if (useMainThreadManagerForRPCs)
            Rpc.MainThreadRunner = MainThreadManager.Instance;

        if (getLocalNetworkConnections)
        {
            NetWorker.localServerLocated += LocalServerLocated;
            NetWorker.RefreshLocalUdpListings((ushort)portNumber);
        }

        LoadPrefs();
    }

    // Редактирование ника
    public void EditNickname(string check) {
        nickname = ifNickname.text;
        SavePrefs();
    }

    // Редактирование порта
    public void EditPortNumber(string check) {
        int port = int.Parse(ifPortNumber.text);
        lblPortNumber.text = port.ToString();
        portNumber = port;
    }

    // Редактирование айпи адреса
    public void EditIPAddress(string check) {
        string ip = ifIpAddress.text;
        ipAddress = ip;
    }

    private void LocalServerLocated(NetWorker.BroadcastEndpoints endpoint, NetWorker sender)
    {
        Debug.Log("Found endpoint: " + endpoint.Address + ":" + endpoint.Port);
    }

    public void Connect()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        connectButton.interactable = false;
        timerConnect1 = Time.time + rateTimerConnect1;

        ushort port = (ushort)portNumber;

        NetWorker client;

        client = new UDPClient();
        ((UDPClient)client).Connect(ipAddress, (ushort)port);

        client.serverAccepted += (player) => 
        {
            MainThreadManager.Run(() =>
            {
                // Старт игры для клиента
                mainMenuPanel.gameObject.SetActive(false);
                inGamePanel.gameObject.SetActive(true);
                NetworkManager.Instance.InstantiatePlayer();

                Debug.Log("Connected(client)");
            }
            );
        };
        client.bindFailure += (player) => {
            MainThreadManager.Run(() => {
                Debug.Log("Client: Bind failure");
                
            }
            );
        };
        client.bindSuccessful += (player) => {
            MainThreadManager.Run(() => {
                Debug.Log("Client: Bind successful");

            }
            );
        };
        

        Connected(client);
    }

    public void Host()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        hostButton.interactable = false;

        server = new UDPServer(maxPlayers);

        ((UDPServer)server).Connect(ipAddress, (ushort)portNumber);

        server.playerTimeout += (player, sender) =>
        {
            Debug.Log("Player " + player.NetworkId + " timed out.");
        };
        server.playerConnected += (player, sender) =>
        {
            MainThreadManager.Run(() => 
            {
                Debug.Log("Server: Player connected");
                
                soundManager.Play(VGCSoundEffects.PlayerConnected);
            });
        };
        server.playerDisconnected += (player, sender) =>
        {
            MainThreadManager.Run(() =>
            {
                Debug.Log("Server: Player disconnected");
                
                soundManager.Play(VGCSoundEffects.PlayerDisconnected);

                //Loop through all players and find the player who disconnected, store all it's networkobjects to a list
                List<NetworkObject> toDelete = new List<NetworkObject>();
                foreach (var no in sender.NetworkObjectList) {
                    if (no.Owner == player) {
                        //Found him
                        toDelete.Add(no);
                    }
                }

                //Remove the actual network object outside of the foreach loop, as we would modify the collection at runtime elsewise. (could also use a return, too late)
                if (toDelete.Count > 0) {
                    for (int i = toDelete.Count - 1; i >= 0; i--) {
                        sender.NetworkObjectList.Remove(toDelete[i]);
                        toDelete[i].Destroy();
                    }
                }
                /*
                // Удаляем игроков
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                if (players.Length > 0) {
                    for (int i = 0; i < players.Length; i++) {
                        if (players[i].GetComponent<Player>().ID == sender.) {
                            GameObject.Destroy(players[i]);
                            break;
                        }
                    }
                }*/
            });
        };
        server.playerAccepted += (player, sender) => 
        {
            MainThreadManager.Run(() =>
            {
                Debug.Log("Server: Player accepted");

                
            });
        };

        Connected(server);
        Debug.Log("Connected(server)");

        // Старт игры для хоста
        mainMenuPanel.gameObject.SetActive(false);
        inGamePanel.gameObject.SetActive(true);
        NetworkManager.Instance.InstantiatePlayer();

        // Старт сетевой игры как объекта
        NetworkManager.Instance.InstantiateGame();
    }

    // Update is called once per frame
    void Update()
    {
        // Таймер для коннекта
        if (timerConnect1 < Time.time) {
            timerConnect1 = Mathf.Infinity;

            connectButton.interactable = true;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) {
            CloseSession();
            
        }
    }

    private void TestLocalServerFind(NetWorker.BroadcastEndpoints endpoint, NetWorker sender)
    {
        Debug.Log("Address: " + endpoint.Address + ", Port: " + endpoint.Port + ", Server? " + endpoint.IsServer);
    }

    // Событие, когда присоединён
    public void Connected(NetWorker localNetWorker)
    {
        if (!localNetWorker.IsBound)
        {
            Debug.LogError("NetWorker failed to bind");
            connectButton.interactable = true;
            hostButton.interactable = true;
            return;
        }

        if (mgr == null && networkManager == null)
        {
            Debug.LogWarning("A network manager was not provided, generating a new one instead");
            networkManager = new GameObject("Network Manager");
            mgr = networkManager.AddComponent<NetworkManager>();
        }
        else if (mgr == null)
        {
            mgr = Instantiate(networkManager).GetComponent<NetworkManager>();
        }

        mgr.Initialize(localNetWorker);

        if (useInlineChat && localNetWorker.IsServer)
        {
            SceneManager.sceneLoaded += CreateInlineChat;
        }

        netWorker = localNetWorker;

        // Три дня! Три дня угробил на это!
        // Без нижней инструкции сетевые объекты не начинают обмениваться сообщениями.
        NetworkObject.Flush(localNetWorker);

        // Создаём сетевого игрока
        if (true) {
            //mainMenuPanel.gameObject.SetActive(false);
            //inGamePanel.gameObject.SetActive(true);
            //NetworkManager.Instance.InstantiateVGCPlayer();
        }
        //playerList.Add(player.)
    }

    private void CreateInlineChat(Scene arg0, LoadSceneMode arg1)
    {
        SceneManager.sceneLoaded -= CreateInlineChat;
    }

    private void OnApplicationQuit()
    {
        if (getLocalNetworkConnections)
            NetWorker.EndSession();
    }

    // Загружаем настройки
    void LoadPrefs()
    {
        string keyNickname = "Nickname";
        if (PlayerPrefs.HasKey(keyNickname))
        {
            ifNickname.text = PlayerPrefs.GetString(keyNickname);
        }
    }

    // Сохраняем настройки
    void SavePrefs()
    {
        PlayerPrefs.SetString("Nickname", ifNickname.text);
        PlayerPrefs.Save();
    }

    // Выходим из сетевой сессии
    public void CloseSession()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (netWorker != null && netWorker is IServer)
        {
            server.Disconnect(true);
        }

        netWorker.Disconnect(false);

        /*if (getLocalNetworkConnections)
        {
            NetWorker.EndSession();
        }*/

        NetWorker.EndSession();

        // Обнуляем родителя камеры.    
        GameObject cam = GameObject.FindGameObjectWithTag("MainCamera");
        if (cam)
        {
            cam.transform.SetParent(null);
            cam.GetComponent<Camera>().enabled = true;
            //cam.GetComponent<FlareLayer>().enabled = true;
            cam.GetComponent<AudioListener>().enabled = true;
        }

        // Удаляем игроков
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length > 0)
        {
            for (int i = 0; i < players.Length; i++)
            {
                GameObject.Destroy(players[i]);
            }
        }

        // Переходим в главное меню
        mainMenuPanel.gameObject.SetActive(true);
        inGamePanel.gameObject.SetActive(false);

        hostButton.interactable = true;
    }

    public void Quit()
    {
        SavePrefs();
        Application.Quit();
    }

    public void SetNick(string newNick)
    {
        ifNickname.text = newNick;
        SavePrefs();
    }
}
