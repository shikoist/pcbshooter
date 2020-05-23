using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BeardedManStudios.Forge.Networking;
using BeardedManStudios.Forge.Networking.Generated;
using BeardedManStudios.Forge.Networking.Unity;

// -------------------- Заметки ------------------
// 1. Вызовы RPC исполняются только на том скрипте, с которого его вызвали
//    Поэтому не надо дополнительно вписывать ID
// 2. 

public class Player : PlayerBehavior
{
    public Transform cameras;

    public float height = 1.8f;
    public float camOffset = 0.3f;

    public Transform explosionPrefab;
    public Transform explosion3DSound;

    public GameObject[] objectsForHide;

    public Transform prefabCorpse;

    // Префаб лазера
    public Transform laserPrefab;

    // Место, где заканчивается дуло
    public Transform endOfGun;

    // Префаб звука выстрела
    public Transform soundShot;

    // Префаб вспышки
    public Transform lightShot;

    // Список рендереров для окраски
    public Renderer[] rrs;

    //The layermask the raycast shooting should use
    [SerializeField]
    private LayerMask raycastIncludeMask;

    public uint ID = 0;
    public int HP = 100;
    public int Kills = 0;
    public int PlayerPing = 0;
    public string Name;

    // Вилка, на которой две камеры - пустой объект
    Transform cams;

    // Прямые ссылки на две камеры
    Camera playerCamL;
    Camera playerCamR;

    // Таймер для пингования сервера
    float timerPing1;
    float rateTimerPing1 = 1.0f;

    // Таймер для окраски персонажа обратно
    float timerColor1;
    float rateTimerColor1 = 1.0f;

    // Таймер для респауна после смерти
    float timerRespawn;
    float rateTimerRespawn = 5;

    // Таймер для прыжков
    float timeJump;

    // Трансформа головы
    Transform head;

    //A timer that defines when a weapon can shoot again
    private float nextShotTime;

    public bool isSurface;
    public bool isJumping;
    public bool isGrounded;

    private float inputx;
    private float inputy;
    private float inputJump;

    private bool isGroundedOld = true;

    private float mousex;
    private float mousey;
    private float maxSpeed = 10.0f;
    private float currentSpeed = 10.0f;
    private float startJumpSpeed;
    private Vector3 moveDir;
    private Vector3 jumpVector;
    private float delta;

    public LayerMask layerMaskForMoving;

    private bool isDead = false;

    private bool stereoMode = false;

    public float Speed = 0.3f;
    public float JumpForce = 1f;

    public LayerMask GroundLayer = 1;

    private Rigidbody _rb;
    private CapsuleCollider _collider;

    protected override void NetworkStart()
    {
        base.NetworkStart();

        NetworkManager.Instance.Networker.onPingPong += OnPingPong;

        if (networkObject.IsOwner)
        {
            MainScript main = GameObject.FindObjectOfType<MainScript>();
            networkObject.SendRpc(PlayerBehavior.RPC_UPDATE_NAME, Receivers.AllBuffered, main.ifNickname.text);
            networkObject.SendRpc(PlayerBehavior.RPC_UPDATE_ID, Receivers.AllBuffered, networkObject.NetworkId);

            LocalSpawn();
        }

        if (NetworkManager.Instance.Networker is IServer) {
            //here you can also do some server specific code
        }
        else {
            //setup the disconnected event
            NetworkManager.Instance.Networker.disconnected += DisconnectedFromServer; }
    }
    
    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _collider = GetComponent<CapsuleCollider>();

        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (GroundLayer == gameObject.layer)
            Debug.LogError("Player SortingLayer must be different from Ground SourtingLayer!");

        //timeJump = Time.time;

        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;

        // Выключаем таймер
        timerColor1 = Mathf.Infinity;
        timerRespawn = Mathf.Infinity;

        //
        //SetColor(Color.blue);

        head = transform.Find("Head");

        if (networkObject.IsOwner) {

            // Отключаем камеру из меню
            GameObject menuCam = GameObject.Find("MenuCamera");
            menuCam.GetComponent<Camera>().enabled = false;
            menuCam.GetComponent<AudioListener>().enabled = false;

            // Забираем главную камеру
            //cams = GameObject.FindGameObjectWithTag("MainCamera").transform;
            cams = Instantiate(cameras);

            // Временно присваиваем родителя, чтобы правильно поставить камеру относительно персонажа
            cams.parent = this.transform;
            cams.localPosition = new Vector3(0, height, camOffset); // Отодвинем камеру, чтобы было видно руки.
            cams.localRotation = Quaternion.identity;

            playerCamL = cams.transform.Find("CameraLeft").GetComponent<Camera>();
            playerCamR = cams.transform.Find("CameraRight").GetComponent<Camera>();
            playerCamR.enabled = false;

            // Теперь указываем в качестве родителя головы камеру. Камера будет рулить головой.
            head.parent = cams.transform;
        }
        else {
            //GetComponent<RigidbodyFirstPersonController>().enabled = false;
        }
    }

    private void Update() {
        // Недоумевал, почему не у всех обратно перекрашивается. Надо было до return ставить.
        if (timerColor1 < Time.time) {
            timerColor1 = Mathf.Infinity;
            ResetColor();
        }

        // If we are not the owner of this network object then we should
        // move this cube to the position/rotation dictated by the owner
        if (!networkObject.IsOwner) {

            transform.position = networkObject.position;
            transform.rotation = networkObject.rotation;
            head.rotation = networkObject.headRotation;
            return;
        }

        if (timerRespawn < Time.time) {
            timerRespawn = Mathf.Infinity;
            LocalSpawn();
        }

        // Стреляем только, если не мертвы
        if (!isDead && Input.GetButton("Fire1")) {
            //Debug.Log("Mouse 0 hit");
            LocalShoot();
        }
               
        // Движение персонажа
        if (!isDead) {
            inputx = Input.GetAxis("Horizontal");
            inputy = Input.GetAxis("Vertical");
            inputJump = Input.GetAxis("Jump");
        }

        if (inputx != 0 || inputy != 0) {
            currentSpeed = maxSpeed;
        }
        else {
            currentSpeed = 0;
        }

        if (Input.GetKey(KeyCode.LeftShift)) {
            currentSpeed = maxSpeed * 2;
        }

        // Прыгаем
        if (inputJump > 0 && isGrounded && !isDead && !isJumping) {
            //timeJump = Time.time;
            //isJumping = true;
            //transform.position += Vector3.up * 0.01f;
            //startJumpSpeed = maxSpeed / 3.0f;
        }
        
        // Нужно перезапускать таймер прыжка каждый раз, когда меняется isGrounded
        //if (isGrounded != isGroundedOld) {
        //    if (isGrounded == false && isJumping == false) {
        //        timeJumping = Time.time;
        //    }
        //    isGroundedOld = isGrounded;
        //}

        delta = Time.deltaTime;

        if (!isDead) {
            mousex = Input.GetAxis("Mouse X");
            mousey = Input.GetAxis("Mouse Y");
        }

        // Поворот головы слева направо
        Vector3 rotX = Vector3.up * 100 * delta * mousex;

        // Поворот головы вверх-вниз
        Vector3 rotY = -Vector3.right * 100 * delta * mousey;

        // Поворачиваем персонажа по вертикальной оси слева-направо
        transform.Rotate(rotX);
        _rb.angularVelocity = Vector3.zero;

        // Камеры только вверх-вниз
        cams.Rotate(rotY);

        if (timerPing1 < Time.time) {
            timerPing1 = Time.time + rateTimerPing1;

            networkObject.Networker.Me.Ping();
        }

        if (Input.GetKeyDown(KeyCode.F6)) {
            stereoMode = !stereoMode;
            ToggleStereoMode(stereoMode);
        }

        // Если упали сильно низко, то респавнимся
        if (transform.position.y < -100) {
            isDead = true;
            LocalSpawn();
        }

        // Начинаем проверять виртуальный шар игрока на упирание в препятствия
        // always move along the camera forward as it is the direction that it being aimed at
        //Vector3 desiredMove = transform.forward * inputy + transform.right * inputx;

        //// get a normal for the surface that is being touched to move along it
        //RaycastHit hitSurface;

        //// Здесь мы тыкаем условным шаром на пересечение с поверхностями по горизонталыной плоскости
        //isSurface = Physics.SphereCast(transform.position, 1.2f, desiredMove, out hitSurface,
        //    1.2f, layerMaskForMoving, QueryTriggerInteraction.Ignore);

        //// Вектор проецируемый даёт возможность "скользить" вдоль стен
        //desiredMove = Vector3.ProjectOnPlane(desiredMove, hitSurface.normal).normalized;

        //moveDir.x = desiredMove.x * currentSpeed * delta;
        //moveDir.z = desiredMove.z * currentSpeed * delta;

        //RaycastHit hitGround;

        //// А здесь мы тыкаем шаром вниз...
        //bool tmpGround = Physics.SphereCast(transform.position + Vector3.up * 100, 0.9f, Vector3.down, out hitGround,
        //    200.0f, layerMaskForMoving, QueryTriggerInteraction.Ignore);

        //if (tmpGround) {
        //    if (transform.position.y - hitGround.point.y <= 0.0f) {
        //        transform.position = new Vector3(transform.position.x, hitGround.point.y, transform.position.z);
        //        isGrounded = true;
        //    }
        //    else { isGrounded = false; }
        //}
        //else {
        //    isGrounded = false;
        //}

        //Text debug = GameObject.Find("DebugInfo").GetComponent<Text>();
        //debug.text = (transform.position.y - hitGround.point.y).ToString("F2");

        //if (!isJumping) {
        //    if (!isGrounded) {
        //        moveDir.y -= 0.5f * delta;
        //    }
        //    else {
        //        moveDir.y = 0;
        //    }
        //}

        //// y = V0 * sin(a) * t - (g * t * t) / 2.0f
        //// z = V0 * cos(a) * t
        //// Поскольку движение в прыжке определяет игрок, то формула значительно сократилась.
        //// Помним, что косинус 90 это ноль, а синус 90 это единица

        //float t = (Time.time - timeJump) * 1.0f;

        //if (isJumping && !isGrounded) {
        //    jumpVector = new Vector3(
        //        0,
        //        startJumpSpeed * t - (9.81f * t * t) / 2.0f,
        //        0
        //    );
        //}
        //if (isJumping && isGrounded) {
        //    jumpVector = Vector3.zero;
        //    isJumping = false;
        //}
        //moveDir += transform.rotation * jumpVector * delta;
    }

    private void LateUpdate() {
        //transform.position += moveDir;

        // If we are the owner of the object we should send the new position
        // and rotation across the network for receivers to move to in the above code
        networkObject.position = transform.position;
        networkObject.rotation = transform.rotation;
        networkObject.headRotation = head.rotation;
    }

    private void FixedUpdate() {

        JumpLogic();
        MoveLogic();
    }

    private bool _isGrounded {
        get {
            var bottomCenterPoint = new Vector3(_collider.bounds.center.x, _collider.bounds.min.y, _collider.bounds.center.z);
            return Physics.CheckCapsule(_collider.bounds.center, bottomCenterPoint, _collider.bounds.size.x / 2 * 0.9f, GroundLayer);
        }
    }

    private Vector3 _movementVector {
        get {
            var horizontal = Input.GetAxis("Horizontal");
            var vertical = Input.GetAxis("Vertical");

            return transform.rotation * new Vector3(horizontal, 0.0f, vertical);
        }
    }

    private void MoveLogic() {
        _rb.AddForce(_movementVector * Speed, ForceMode.VelocityChange);
    }

    private void JumpLogic() {
        if (_isGrounded && (Input.GetAxis("Jump") > 0)) {
            _rb.AddForce(Vector3.up * JumpForce, ForceMode.Impulse);
        }
    }

    //----------------------------Functions -----------------------
    void ToggleStereoMode(bool mode) {
        // Если режим стерео включен
        if (mode == true) {
            // Камера теперь показывает только в левую часть экрана
            playerCamL.rect = new Rect(0, 0, 0.5f, 1);
            
            // Отодвинем камеру влево для стерео-эффекта
            playerCamL.transform.localPosition = new Vector3(-0.035f, 0, 0); 
            
            // Включаем ранее отключенную правую камеру
            playerCamR.enabled = true;

            // Камера теперь показывает только в правую часть экрана
            playerCamR.rect = new Rect(0.5f, 0, 0.5f, 1);

            // Отодвинем камеру вправо для стерео-эффекта
            playerCamR.transform.localPosition = new Vector3(0.035f, 0, 0);
        }
        // Если стерео режим отключен
        if (mode == false) {
            // Правая камера отключается
            playerCamR.enabled = false;

            // Левая камера теперь рендерит на весь экран
            playerCamL.rect = new Rect(0, 0, 1, 1);

            // Левая камера на исходнйо позиции
            playerCamL.transform.localPosition = new Vector3(0, 0, 0); // 
        }
    }

    //----------------------Client Events--------------------
    
    // Called when a player disconnects
    private void DisconnectedFromServer(NetWorker sender) {
        NetworkManager.Instance.Networker.disconnected -= DisconnectedFromServer;

        MainThreadManager.Run(() => {
            //Loop through the network objects to see if the disconnected player is the host
            foreach (var no in sender.NetworkObjectList) {
                if (no.Owner.IsHost) {
                    BMSLogger.Instance.Log("Server disconnected");
                    //Should probably make some kind of "You disconnected" screen. ah well
                }
            }
            MainScript main = GameObject.FindObjectOfType<MainScript>();
            main.CloseSession();
            //NetworkManager.Instance.Disconnect();
        });

        
    }
    
    private void OnPingPong(double ping, NetWorker sender) {
        PlayerPing = (int)ping;
        networkObject.SendRpc(RPC_UPDATE_PING, Receivers.AllBuffered, PlayerPing);
    }

    //-----------------RPC Calls----------------------------

    // Override the abstract RPC method that we made in the NCW
    public override void UpdateName(RpcArgs args)
    {
        // Since there is only 1 argument and it is a string we can safely
        // cast the first argument to a string knowing that it is going to
        // be the name for this player
        Name = args.GetNext<string>();
        transform.name = Name;
        //networkObject.Networker.Me.Name = Name;
    }
    
    // Override the abstract RPC method that we made in the NCW
    public override void UpdatePing(RpcArgs args) {
        // Since there is only 1 argument and it is a string we can safely
        // cast the first argument to a string knowing that it is going to
        // be the name for this player
        PlayerPing = args.GetNext<int>();
    }

    public void LocalUpdateKills(int kills, uint playerID) {
        networkObject.SendRpc(
            PlayerBehavior.RPC_UPDATE_KILLS,
            BeardedManStudios.Forge.Networking.Receivers.All,
            kills);
    }

    // Override the abstract RPC method that we made in the NCW
    public override void UpdateKills(RpcArgs args) {
        // Since there is only 1 argument and it is a string we can safely
        // cast the first argument to a string knowing that it is going to
        // be the name for this player
        int newKills = args.GetNext<int>();
        Kills = newKills;
    }

    void LocalShoot() {
        //Wait between shots
        if (Time.time > nextShotTime) {
            //set the next shot time
            nextShotTime = Time.time + 1.0f;
            //Find the points from the clients camera and send them to the server to calculate the shot from.
            //On a fully server auth solution, the server would have a version of the camera and calculate the shot from that
            Vector3 rayOrigin = playerCamL.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f));
            Vector3 forward = playerCamL.transform.forward;
            //send the rpc
            networkObject.SendRpc(PlayerBehavior.RPC_SHOOT, BeardedManStudios.Forge.Networking.Receivers.All, rayOrigin, forward);

            //Debug.Log("Local Shoot " + rayOrigin + " " + forward);
        }
    }

    // Стреляем по сети
    public override void Shoot(RpcArgs args) {
        Vector3 origin = args.GetNext<Vector3>();
        Vector3 camForward = args.GetNext<Vector3>();

        Instantiate(soundShot, endOfGun.position, Quaternion.identity);
        Instantiate(lightShot, endOfGun.position, Quaternion.identity);

        Transform tmp = (Transform)Instantiate(laserPrefab, endOfGun.position, Quaternion.LookRotation(camForward));
        tmp.localScale = new Vector3(1, 1, 10000);
        //tmp.parent = endOfGun;
        //tmp.localPosition = Vector3.zero;

        //Debug.Log("RPC Shoot " + origin + " " + camForward);

        //Only make the server do the actual shooting
        //if (networkObject.IsServer) {
        RaycastHit hit;
        Debug.DrawRay(origin, camForward * 10000, Color.black, 10);
        //Debug.Break();

        //Debug.Log("RPC Server Shoot " + origin + " " + camForward);

        //Do the actual raycast on the server
        if (Physics.Raycast(origin, camForward, out hit, 10000, raycastIncludeMask)) {
            //If the current weapon doesn't use projectiles
            Player enemyPlayer = hit.collider.GetComponent<Player>();
            if (enemyPlayer) {
                Debug.Log("Detected hit " + hit.point);
                // call take damage and supply some raycast hit information
                if (networkObject.IsServer) {
                    // Это должно вызываться на том, в кого попали
                    enemyPlayer.LocalTakeDamage(50, ID, hit.point, hit.normal);
                }
                //networkObject.SendRpc(PlayerBehavior.RPC_TAKE_DAMAGE, BeardedManStudios.Forge.Networking.Receivers.All, 50, ID);
            }

            tmp.localScale = new Vector3(1, 1, Vector3.Distance(hit.point, endOfGun.position));
        }
        //}
    }

    // damage повреждение, id киллера, точка попадания, нормаль попадания
    public void LocalTakeDamage(int damage, uint playerID, Vector3 point, Vector3 normal) {
        networkObject.SendRpc(
            PlayerBehavior.RPC_TAKE_DAMAGE,
            BeardedManStudios.Forge.Networking.Receivers.All,
            damage,
            playerID,
            point,
            normal);
    }

    public override void TakeDamage(RpcArgs args) {
        int damage = args.GetNext<int>();
        uint playerID = args.GetNext<uint>();
        Vector3 point = args.GetNext<Vector3>();
        Vector3 normal = args.GetNext<Vector3>();

        HP -= damage;

        SetColor(Color.red);
        timerColor1 = Time.time + rateTimerColor1;

        if (HP <= 0) {
            HP = 100;
            // Здесь нужно послать сообщение, что игрок убит игроком 2
            // Ещё включить анимацию убийста, может, просто ригидбади включить и пусть робот валится
            // И отреспаунить игрока
            // Ещё киллсов добавить
            Player winner = FindPlayerByID(playerID);
            winner.LocalUpdateKills(winner.Kills + 1, playerID);

            //GetComponent<RigidbodyFirstPersonController>().enabled = false;
            //camTransform.localPosition = transform.position + Vector3.up * 10;
            //camTransform.SetParent(null);
            //camTransform.LookAt(transform.position);

            // Чтобы труп игрока не оказался в одном месте с игроком
            Hide();
            isDead = true;
            this.GetComponent<Collider>().isTrigger = true;

            Transform tmp = (Transform)Instantiate(prefabCorpse, transform.position, transform.rotation);

            Instantiate(explosion3DSound, transform.position, transform.rotation);
            Instantiate(explosionPrefab, transform.position, transform.rotation);

            Rigidbody rb = tmp.GetComponent<Rigidbody>();
            rb.AddForce(-normal * 10, ForceMode.Impulse);

            timerRespawn = Time.time + rateTimerRespawn;
            if (networkObject.IsOwner) {
                cams.position += Vector3.up * 25; // + Vector3.right * 10 + Vector3.forward * 10;
                cams.LookAt(tmp.position);
            }
        }
    }

    // Override the abstract RPC method that we made in the NCW
    public override void UpdateId(RpcArgs args) {
        // Since there is only 1 argument and it is a string we can safely
        // cast the first argument to a string knowing that it is going to
        // be the name for this player
        uint id = args.GetNext<uint>();
        ID = id;
    }

    //---------------------Additional Functions--------------------------

    public void ResetColor() {
        SetColor(Color.white);
    }

    // Прячем дочерние объекты
    public void Hide() {
        for (int i = 0; i < objectsForHide.Length; i++) {
            objectsForHide[i].SetActive(false);
        }
    }

    // Показываем дочерние объекты
    public void Show() {
        for (int i = 0; i < objectsForHide.Length; i++) {
            objectsForHide[i].SetActive(true);
        }
    }

    public void SetColor(Color newColor) {

        //Renderer[] rrs = gameObject.GetComponentsInChildren<Renderer>();

        //Debug.Log("rrs.length " + rrs.Length);

        for (int i = 0; i < rrs.Length; i++) {
            rrs[i].materials[0].SetColor("_Color", newColor);
        }
    }

    // Для работы поиска необходимо, чтобы Player.ID = networkObject.NetworkID
    Player FindPlayerByID(uint playerID) {
        Player player = null;
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++) {
            player = players[i].GetComponent<Player>();
            if (player.ID == playerID) {
                return player;
            }
        }
        return player;
    }

    public void LocalSpawn() {
        // Появление в случайной точке из набора координат объектов с тегом Respawn
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");
        int rnd = UnityEngine.Random.Range(0, spawnPoints.Length - 1);

        isJumping = false;

        networkObject.SendRpc(
            PlayerBehavior.RPC_SPAWN,
            BeardedManStudios.Forge.Networking.Receivers.All,
            spawnPoints[rnd].transform.position,
            spawnPoints[rnd].transform.rotation);
    }

    public override void Spawn(RpcArgs args) {
        Vector3 pos = args.GetNext<Vector3>();
        Quaternion rot = args.GetNext<Quaternion>();

        isDead = false;

        _rb.velocity = Vector3.zero;
        this.GetComponent<Collider>().isTrigger = false;
        Show();

        if (networkObject.IsOwner) {
            cams.localPosition = new Vector3(0, height, camOffset); // Отодвинем камеру, чтобы было видно аватар.
            cams.localRotation = Quaternion.identity;
        }

        this.transform.position = pos;
        this.transform.rotation = rot;
    }
}
