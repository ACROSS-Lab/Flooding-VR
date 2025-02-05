using System;
using System.Collections.Generic;
using Gama_Provider.Simulation;
using QuickTest;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
//using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit; 
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;


public class SimulationManager : MonoBehaviour
{
    [SerializeField] protected XRRayInteractor leftXRRayInteractor;
    [SerializeField] protected XRRayInteractor rightXRRayInteractor;
    [SerializeField] protected InputActionReference primaryRightHandButton = null;
    [SerializeField] protected InputActionReference TryReconnectButton = null;
    [SerializeField] protected InputActionReference rightHandTriggerButton = null;

    [Header("Base GameObjects")]
    [SerializeField] protected GameObject player;
    [SerializeField] protected GameObject Ground;


    // optional: define a scale between GAMA and Unity for the location given
    [Header("Coordinate conversion parameters")]
    [SerializeField] protected float GamaCRSCoefX = 1.0f;
    [SerializeField] protected float GamaCRSCoefY = 1.0f;
    [SerializeField] protected float GamaCRSOffsetX = 0.0f;
    [SerializeField] protected float GamaCRSOffsetY = 0.0f;

    protected Boolean StartMenuDone = false;
    private string currentStage = "s_start";



    protected Transform XROrigin;

    // Z offset and scale
    [SerializeField] protected float GamaCRSOffsetZ = 0.0f;

    protected List<GameObject> toFollow;

    XRInteractionManager interactionManager;

    // ################################ EVENTS ################################
    // called when the current game state changes
    public static event Action<GameState> OnGameStateChanged;
    // called when the game is restarted
    public static event Action OnGameRestarted;
    
    protected List<GameObject> SelectedObjects;

    // called when the world data is received
    //    public static event Action<WorldJSONInfo> OnWorldDataReceived;
    // ########################################################################

    protected Dictionary<string, List<object>> geometryMap;
    protected Dictionary<string, PropertiesGAMA> propertyMap = null;



    protected bool handleGeometriesRequested;
    protected bool handleGroundParametersRequested;

    protected CoordinateConverter converter = null;
    protected PolygonGenerator polyGen = null;
    protected ConnectionParameter parameters = null;
    protected AllProperties propertiesGAMA = null;
    protected WorldJSONInfo infoWorld = null;

    protected GameState currentState;

    public static SimulationManager Instance = null;

    protected float timeWithoutInteraction = 1.0f; //in second
    protected float remainingTime = 0.0f;


    protected bool sendMessageToReactivatePositionSent = false;

    protected float maxTimePing = 1.0f;
    protected float currentTimePing = 0.0f;

    protected List<GameObject> toDelete;

    protected bool readyToSendPosition = false;

    protected float TimeSendPosition = 1.0f;
    protected float TimerSendPosition = 0.0f;

    protected int _dykePointCnt = 0;

    protected Vector3 _startPoint;
    protected Vector3 _endPoint;

    protected GameObject startPoint;
    protected GameObject endPoint;

    protected ScoreMessage scoreM;

    [SerializeField] protected Text modalText;
    //[SerializeField] protected Button startButton;
    [SerializeField] protected Text movementText;
    [SerializeField] protected Text timeText;
    [SerializeField] protected int maximumTimeToBuild;
    protected bool mustNotBuildDyke = false;
    //protected float StartTime;
    protected Vector3 originalStartPosition;
    protected bool firstPositionStored;


    private bool _inTriggerPress = false;

    public GameObject FutureDike = null;
    protected PropertiesGAMA propFutureDike;
    public bool DisplayFutureDike = false;
    protected bool StartFloodingDone = false;

    protected Vector3 FinalPositionPlayer = new Vector3(872, 1205.2f, -3427); 
    [SerializeField] protected GameObject FinalScene;
    [SerializeField] protected GameObject WinAnimtion;
    [SerializeField] protected GameObject LooseAnimtion;

    protected bool endOfGame = false;
    protected float TimeEndOfGame = 17.0f;
    protected float TimerEndOfGame = 0.0f;

    protected bool _initializedFloodingTime = false;

    [SerializeField] protected InputActionReference mainButton = null;
    [SerializeField] protected InputActionReference secondButton = null;

    //[SerializeField] protected GameObject tutorial;
    [SerializeField] protected GameObject globalVolume;
    
 
    //public bool startDykingPressed;

    [SerializeField] protected StatusEffectManager timer;
    [SerializeField] protected StatusEffectManager safeRateCount;
    [SerializeField] private TextMeshProUGUI timerText;

    protected float LastTime;
   
    // ############################################ UNITY FUNCTIONS ############################################
    void Awake() {
        Instance = this;
        // toDelete = new List<GameObject>();
        
        SelectedObjects = new List<GameObject>();


        startPoint = GameObject.FindGameObjectWithTag("startPoint");
        endPoint = GameObject.FindGameObjectWithTag("endPoint");

        if (endPoint != null)
            endPoint.SetActive(false);
        if (startPoint != null)
            startPoint.SetActive(false);

        maximumTimeToBuild += (int)Time.time;

        propFutureDike = new PropertiesGAMA();
        propFutureDike.red = 0;
        propFutureDike.blue = 0;
        propFutureDike.green = 255;
        propFutureDike.hasCollider = false;
        propFutureDike.hasPrefab = false;
        propFutureDike.height = 40 * 10000;
        propFutureDike.is3D = true;
        propFutureDike.visible = true;
        //startButton.onClick.AddListener(StartGame);



        XROrigin = player.transform.Find("XR Origin (XR Rig)");
        timer.gameObject.SetActive(false);
    }
    
    void StartTheFlood()
    {
        //StartTime = Time.time;
        //startButton.gameObject.SetActive(false);
    }


    void OnEnable() {
        if (ConnectionManager.Instance != null) {
            ConnectionManager.Instance.OnServerMessageReceived += HandleServerMessageReceived;
            ConnectionManager.Instance.OnConnectionAttempted += HandleConnectionAttempted;
            ConnectionManager.Instance.OnConnectionStateChanged += HandleConnectionStateChanged;
            Debug.Log("SimulationManager: OnEnable");
        } else {
            Debug.Log("No connection manager");
        }
    }

    void OnDisable() {
        Debug.Log("SimulationManager: OnDisable");
        ConnectionManager.Instance.OnServerMessageReceived -= HandleServerMessageReceived;
        ConnectionManager.Instance.OnConnectionAttempted -= HandleConnectionAttempted;
        ConnectionManager.Instance.OnConnectionStateChanged -= HandleConnectionStateChanged;
    }

    void OnDestroy() {
        Debug.Log("SimulationManager: OnDestroy");
    }

    void Start() {
        geometryMap = new Dictionary<string, List<object>>();
        handleGeometriesRequested = false;
        // handlePlayerParametersRequested = false;
        handleGroundParametersRequested = false;
        infoWorld = null;
        interactionManager = player.GetComponentInChildren<XRInteractionManager>();
        OnEnable();
    }


    void FixedUpdate()
    {
        
        
        if (sendMessageToReactivatePositionSent)
        {
            Dictionary<string, string> args = new Dictionary<string, string> {
            {"id",ConnectionManager.Instance.getUseMiddleware() ? ConnectionManager.Instance.GetConnectionId()  : ("\"" + ConnectionManager.Instance.GetConnectionId() +  "\"") }};

            ConnectionManager.Instance.SendExecutableAsk("player_position_updated", args);
            sendMessageToReactivatePositionSent = false;

        }
        if (handleGroundParametersRequested)
        {
            InitGroundParameters();
            handleGroundParametersRequested = false;

        }
      
        if (handleGeometriesRequested && infoWorld != null && propertyMap != null)
        {


            sendMessageToReactivatePositionSent = true;
            GenerateGeometries(true, new List<string>());
            handleGeometriesRequested = false;
            UpdateGameState(GameState.GAME);

        }

        if (IsGameState(GameState.GAME))
        {
            if (readyToSendPosition && TimerSendPosition <= 0.0f)
                UpdatePlayerPosition();
            if (infoWorld != null && !infoWorld.isInit)
                UpdateAgentsList();
            
        }

    }
    
    void UpdateGame()
    {
        
        if (IsGameState(GameState.GAME) && infoWorld != null)
        {
          
            if (currentStage != infoWorld.state)
            {
                
                Debug.Log("BEGIN OF STAGE : " + infoWorld.state);
                currentStage = infoWorld.state;
                if (currentStage == "s_flooding")
                {
                    
                }
            }

            if (infoWorld.state == "wait_flooding")
            {
                if (!StartFloodingDone)
                {
                    StartMenuDone = false;
                    UIController.Instance.StartFloodingPhase();
                    if (FutureDike != null)
                    {
                        FutureDike.SetActive(false);
                        GameObject.DestroyImmediate(FutureDike);

                        FutureDike = null;

                    }
                    DisplayFutureDike = false;
                    StartFloodingDone = true;
                }

            }
            if (infoWorld.state == "s_init")
            {
                if (!StartMenuDone && infoWorld.playback_finished)
                {
                    UIController.Instance.StartMenuDikingPhase();
                    StartMenuDone = true;
                    StartFloodingDone = false;
                }
            }
            else if (infoWorld.state == "s_diking")
            {
                //timeText.text = "Remaining Time: " + Math.Max(0, infoWorld.remaining_time);
            }
            else if (infoWorld.state == "s_flooding")
            {

            }
            
            if (infoWorld.state != "s_init" && infoWorld.remaining_time > LastTime)
            {
                timer.gameObject.SetActive(true);
                Debug.Log("Remaining time: " + infoWorld.remaining_time);
                timer.StartEnergizedEffect(infoWorld.remaining_time);
            }
            
            if (infoWorld.state == "s_init" || UIController.Instance.UI_EndingPhase_eng.activeSelf || UIController.Instance.UI_EndingPhase_viet.activeSelf)
                timer.gameObject.SetActive(false);
            
            LastTime = infoWorld.remaining_time;
            
            TimeSpan timeSpan = TimeSpan.FromSeconds(Math.Max(0, (int)LastTime));
            Debug.Log("Remaining time span: " + Math.Max(0, (int)LastTime));
            timerText.text = timeSpan.ToString(@"mm\:ss");
        }
    }

    private void Update()
    {
        if (remainingTime > 0)
            remainingTime -= Time.deltaTime;
        if (TimerSendPosition > 0)
        {
            TimerSendPosition -= Time.deltaTime;
        }
        if (currentTimePing > 0)
        {
            currentTimePing -= Time.deltaTime;
            if (currentTimePing <= 0)
            {
                Debug.Log("Try to reconnect to the server");
                ConnectionManager.Instance.Reconnect();
            }
        }


        if (primaryRightHandButton != null && primaryRightHandButton.action.triggered)
        {
            TriggerMainButton();
        }
        if (TryReconnectButton != null && TryReconnectButton.action.triggered)
        {
            Debug.Log("TryReconnectButton activated");
            TryReconnect();
        }

       // Debug.Log("currentStage: " + currentStage + " IsGameState(GameState.GAME) :" +IsGameState(GameState.GAME));
        if ( IsGameState(GameState.GAME) && UIController.Instance.DikingStart)
            ProcessRightHandTrigger();

        //UpdateTimeLeftToBuildDykes();
        OtherUpdate();
        UpdateGame();
        if (scoreM != null){
            UIController.Instance.EndGame(scoreM.score);
            scoreM = null;
        }

         


    }


   

   


    void GenerateGeometries(bool initGame, List<string> toRemove)
    {
         if (infoWorld.position != null && infoWorld.position.Count > 1 && (initGame || !sendMessageToReactivatePositionSent))
        {
            Vector3 pos = converter.fromGAMACRS(infoWorld.position[0], infoWorld.position[1], infoWorld.position[2]);
           XROrigin.localPosition = pos;
            //Camera.main.transform.position = pos;

            // Debug.Log("player.transform.position: " + pos[0] + "," + pos[1] + "," + pos[2]);
            sendMessageToReactivatePositionSent = true;
            readyToSendPosition = true;
            TimerSendPosition = TimeSendPosition;

        }
        foreach (string n in infoWorld.keepNames) 
            toRemove.Remove(n);
        int cptPrefab = 0;
        int cptGeom = 0;
        for (int i = 0; i < infoWorld.names.Count; i++)
        {
            string name = infoWorld.names[i];
            string propId = infoWorld.propertyID[i];
         
            PropertiesGAMA prop = propertyMap[propId];
            GameObject obj = null;

            if (prop.hasPrefab)
            {
                if (initGame || !geometryMap.ContainsKey(name))
                {
                    obj = instantiatePrefab(name, prop, initGame);
                }
                else
                {
                    List<object> o = geometryMap[name];
                   
                    PropertiesGAMA p = (PropertiesGAMA)o[1];
                    if (p == prop)
                    {
                        obj = (GameObject)o[0];


                    }
                    else
                    {
                       
                        obj.transform.position = new Vector3(0, -100, 0);
                        if (toFollow.Contains(obj))
                            toFollow.Remove(obj);

                        GameObject.Destroy(obj);
                        obj = instantiatePrefab(name, prop, initGame);
                    }

                }
                List<int> pt = infoWorld.pointsLoc[cptPrefab].c;
                Vector3 pos = converter.fromGAMACRS(pt[0], pt[1], pt[2]);
                pos.y += pos.y + prop.yOffsetF;
                float rot = prop.rotationCoeffF * ((0.0f + pt[3]) / parameters.precision) + prop.rotationOffsetF ;
                obj.transform.SetPositionAndRotation(pos, Quaternion.AngleAxis(rot, Vector3.up));
                //obj.SetActive(true);
                toRemove.Remove(name);
                cptPrefab++;

            }
            else
            {
                if (polyGen == null) 
                { 
                    polyGen = PolygonGenerator.GetInstance(); 
                    polyGen.Init(converter);
                }
                List<int> pt = infoWorld.pointsGeom[cptGeom].c;

                obj = polyGen.GeneratePolygons(false, name , pt, prop, parameters.precision);

                if (prop.hasCollider)
                {

                    MeshCollider mc = obj.AddComponent<MeshCollider>();
                    if (prop.isGrabable)
                    {
                        mc.convex = true;
                    }
                    mc.sharedMesh = polyGen.surroundMesh;
                   // mc.isTrigger = prop.isTrigger;
                }

                instantiateGO(obj, name, prop);
                // polyGen.surroundMesh = null;
                
               if (geometryMap.ContainsKey(name)) {

                    GameObject objOld = (GameObject)geometryMap[name][0];
                   // objOld.transform.position = new Vector3(0, -100, 0);
                    geometryMap.Remove(name);
                    GameObject.Destroy(objOld);
                }
                List<object> pL = new List<object>();
                pL.Add(obj); pL.Add(prop);
                toRemove.Remove(name);

                if (!initGame)
                {

                    geometryMap.Add(name, pL);
                }

                //obj.SetActive(true);
                cptGeom++;

            }



        }
     
        infoWorld = null;
    }

   


   

    // ############################################ GAMESTATE UPDATER ############################################
    public void UpdateGameState(GameState newState) {    
        
        switch(newState) {
            case GameState.MENU:
                Debug.Log("SimulationManager: UpdateGameState -> MENU");
                break;

            case GameState.WAITING:
                Debug.Log("SimulationManager: UpdateGameState -> WAITING");
                break;

            case GameState.LOADING_DATA:
                Debug.Log("SimulationManager: UpdateGameState -> LOADING_DATA");
                if (ConnectionManager.Instance.getUseMiddleware())
                {
                    Dictionary<string, string> args = new Dictionary<string, string> {
                         {"id", ConnectionManager.Instance.GetConnectionId() }
                    };
                    ConnectionManager.Instance.SendExecutableAsk("send_init_data", args);
                }
                break;

            case GameState.GAME:
                Debug.Log("SimulationManager: UpdateGameState -> GAME");
                break;

            case GameState.END:
                Debug.Log("SimulationManager: UpdateGameState -> END");
                break;

            case GameState.CRASH:
                Debug.Log("SimulationManager: UpdateGameState -> CRASH");
                break;

            default:
                Debug.Log("SimulationManager: UpdateGameState -> UNKNOWN");
                break;
        }
        
        currentState = newState;
        OnGameStateChanged?.Invoke(currentState); 
    }

    

    // ############################# INITIALIZERS ####################################
   

    private void InitGroundParameters() {
        Debug.Log("GroundParameters : Beginnig ground initialization");
        if (Ground == null) {
            Debug.LogError("SimulationManager: Ground not set");
            return;
        }
        Vector3 ls = converter.fromGAMACRS(parameters.world[0], parameters.world[1], 0);
        
        if (ls.z < 0)
            ls.z = -ls.z;
        if (ls.x < 0)
            ls.x = -ls.x; 
        ls.y = Ground.transform.localScale.y;
       
        Ground.transform.localScale = ls;
        Vector3 ps = converter.fromGAMACRS(parameters.world[0] / 2, parameters.world[1] / 2, 0);
        
        Ground.transform.position = ps;
        Debug.Log("SimulationManager: Ground parameters initialized");
    }


 




    // ############################################ UPDATERS ############################################
    private void UpdatePlayerPosition() {
        Vector2 vF = new Vector2(Camera.main.transform.forward.x, Camera.main.transform.forward.z);
        Vector2 vR = new Vector2(transform.forward.x, transform.forward.z);
        vF.Normalize();
        vR.Normalize();
        float c = vF.x * vR.x + vF.y * vR.y;
        float s = vF.x * vR.y - vF.y * vR.x;
        int angle = (int) (((s > 0) ? -1.0 : 1.0) * (180 / Math.PI) * Math.Acos(c) * parameters.precision);

        //List<int> p = converter.toGAMACRS3D(player.transform.position);

        Vector3 v = new Vector3(Camera.main.transform.position.x, player.transform.position.y, Camera.main.transform.position.z);

        if (!firstPositionStored)
        {
            originalStartPosition = v;
            firstPositionStored = true;
        }
        
        List<int> p = converter.toGAMACRS3D(v);
        Dictionary<string, string> args = new Dictionary<string, string> {
            {"id",ConnectionManager.Instance.getUseMiddleware() ? ConnectionManager.Instance.GetConnectionId()  : ("\"" + ConnectionManager.Instance.GetConnectionId() +  "\"") },
            {"x", "" +p[0]},
            {"y", "" +p[1]},
            {"z", "" +p[2]},
            {"angle", "" +angle}
        };
       
        ConnectionManager.Instance.SendExecutableAsk("move_player_external", args);

        if (Math.Abs(originalStartPosition.x - v.x) >= 0.1 ||
            Math.Abs(originalStartPosition.z - v.z) >= 0.1)
        {
            movementText.gameObject.SetActive(false);
        }
    }
   

    private void instantiateGO(GameObject obj,  String name, PropertiesGAMA prop)
    {
        obj.name = name;
        if (prop.toFollow)
        {
            toFollow.Add(obj);
        }
        if (prop.tag != null && !string.IsNullOrEmpty(prop.tag))
            obj.tag = prop.tag;
         
        if (prop.isInteractable){
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interaction = null;
            if (prop.isGrabable)
            {
                interaction = obj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (prop.constraints != null && prop.constraints.Count == 6)
                {
                        if (prop.constraints[0])
                            rb.constraints = rb.constraints | RigidbodyConstraints.FreezePositionX;
                        if (prop.constraints[1])
                            rb.constraints = rb.constraints | RigidbodyConstraints.FreezePositionY;
                        if (prop.constraints[2])
                            rb.constraints = rb.constraints | RigidbodyConstraints.FreezePositionZ;
                        if (prop.constraints[3])
                            rb.constraints = rb.constraints | RigidbodyConstraints.FreezeRotationX;
                        if (prop.constraints[4])
                            rb.constraints = rb.constraints | RigidbodyConstraints.FreezeRotationY;
                        if (prop.constraints[5])
                            rb.constraints = rb.constraints | RigidbodyConstraints.FreezeRotationZ;
                    }

                
                 }
                else {

                     interaction = obj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
                }
               if(interaction.colliders.Count == 0)
                {
                   Collider[] cs = obj.GetComponentsInChildren<Collider>();
                   if (cs != null)
                   {
                       foreach (Collider c in cs)
                       {
                                interaction.colliders.Add(c);
                       }
                   }
                }
                interaction.interactionManager = interactionManager;
                interaction.selectEntered.AddListener(SelectInteraction);
                interaction.firstHoverEntered.AddListener(HoverEnterInteraction);
                interaction.hoverExited.AddListener(HoverExitInteraction);
          
         }
     }

   

    private GameObject instantiatePrefab(String name, PropertiesGAMA prop, bool initGame)
    {
        if (prop.prefabObj == null)
        {
            prop.loadPrefab(parameters.precision);
        }
        GameObject obj = Instantiate(prop.prefabObj);
        float scale = ((float)prop.size) / parameters.precision;
        obj.transform.localScale = new Vector3(scale, scale, scale);
        obj.SetActive(true);

        if (prop.hasCollider)
        {
            if (obj.TryGetComponent<LODGroup>(out var lod))
            {
                 foreach (LOD l in lod.GetLODs())
                {
                    GameObject b = l.renderers[0].gameObject;
                    BoxCollider bc = b.AddComponent<BoxCollider>();
                   // b.tag = obj.tag;
                   // b.name = obj.name;
                    //bc.isTrigger = prop.isTrigger;
                }
                    
            } else
            {
                BoxCollider bc = obj.AddComponent<BoxCollider>();
               // bc.isTrigger = prop.isTrigger;
            }
        }
        List<object> pL = new List<object>();
        pL.Add(obj); pL.Add(prop);
        if (!initGame) geometryMap.Add(name, pL);
        instantiateGO(obj, name, prop);
        return obj;
    }

   

    private void UpdateAgentsList() {


        ManageOtherInformation();
        List<string> toRemove = new List<string>(geometryMap.Keys);
      
        // foreach (List<object> obj in geometryMap.Values) {
        //((GameObject) obj[0]).SetActive(false);
        //}
        // toRemove.addAll(toRemoveAfter.k);
        GenerateGeometries(false, toRemove);


       // List<string> ids = new List<string>(geometryMap.Keys);
        foreach (string id in toRemove)
        {
            List<object> o = geometryMap[id];
            GameObject obj = (GameObject)o[0];
            obj.transform.position = new Vector3(0, -100, 0);
            geometryMap.Remove(id);
            GameObject.Destroy(obj);
        }
        
        infoWorld = null;
    }
    
    protected virtual void ManageAttributes(List<Attributes> attributes)
    {

    }


    protected virtual void ManageOtherInformation()
    {

    }

    // ############################################# HANDLERS ########################################
    private void HandleConnectionStateChanged(ConnectionState state) {
        Debug.Log("HandleConnectionStateChanged: " + state);
        // player has been added to the simulation by the middleware
        if (state == ConnectionState.AUTHENTICATED) {
            Debug.Log("SimulationManager: Player added to simulation, waiting for initial parameters");
            UpdateGameState(GameState.LOADING_DATA);
        }
    }

    protected virtual void GenerateFutureDike()
    {

    }
    protected virtual void OtherUpdate()
    {

    }

    protected virtual void TriggerMainButton()
    {

    }

    protected virtual void HoverEnterInteraction(HoverEnterEventArgs ev)
    {
    }

    protected virtual void HoverExitInteraction(HoverExitEventArgs ev)
    {
         
    }

    protected virtual void SelectInteraction(SelectEnterEventArgs ev)
    {

    }
     
    static public void ChangeColor(GameObject obj, Color color) 
    {
        Renderer[] renderers = obj.gameObject.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material.color = color;// renderers[i].material.color == Color.red ? Color.gray : Color.red;
        }
    }

    protected virtual void ManageOtherMessages(string content)
    {

    }

    private async void HandleServerMessageReceived(String firstKey, String content) {
        
        if (content == null || content.Equals("{}")) return;
        if (firstKey == null)
        {
            if (content.Contains("pong"))
            {
                currentTimePing = 0;
                return;
            }
            else if (content.Contains("pointsLoc"))
                firstKey = "pointsLoc";
            else if (content.Contains("precision"))
                firstKey = "precision";
            else if (content.Contains("properties"))
                firstKey = "properties";
            else if (content.Contains("endOfGame"))
                firstKey = "endOfGame";
            else
            {
                ManageOtherMessages(content);
                return;
            }
                
        }

       
        switch (firstKey) {
            // handle general informations about the simulation
            case "precision":

                parameters = ConnectionParameter.CreateFromJSON(content);
                converter = new CoordinateConverter(parameters.precision, GamaCRSCoefX, GamaCRSCoefY, GamaCRSCoefY, GamaCRSOffsetX, GamaCRSOffsetY, GamaCRSOffsetZ);

                Debug.Log("SimulationManager: Received simulation parameters");
                // Init ground and player
                // await Task.Run(() => InitGroundParameters());
                // await Task.Run(() => InitPlayerParameters()); 
               // handlePlayerParametersRequested = true;   
                handleGroundParametersRequested = true;
                handleGeometriesRequested = true;


            break;
            case "score":

               scoreM = ScoreMessage.CreateFromJSON(content);
              
                break;

            case "properties":
                propertiesGAMA = AllProperties.CreateFromJSON(content);
                propertyMap = new Dictionary<string, PropertiesGAMA>();
               foreach (PropertiesGAMA p in propertiesGAMA.properties)
                {
                    propertyMap.Add(p.id, p);
                }
                break;

            // handle agents while simulation is running
            case "pointsLoc":
                if (infoWorld == null) {                    
                    infoWorld = WorldJSONInfo.CreateFromJSON(content);
                    //Debug.Log("Current info world score: "  + infoWorld.score);
                    //Debug.Log("Current info world budget: " + infoWorld.budget);
                    //Debug.Log("Current info world ok_to_build_dyke: " + infoWorld.ok_build_dyke_with_unity);
                }
                break;
           
            default:
                ManageOtherMessages(content);
                break;
        }
         
    }

    protected void ProcessRightHandTrigger()
    {
        if (rightHandTriggerButton != null && rightHandTriggerButton.action.triggered)
        {
            if (!_inTriggerPress)
            {
                _inTriggerPress = true;
                if (rightXRRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit raycastHit))
                {
                    _startPoint = raycastHit.point;
                    startPoint.transform.position = _startPoint;
                   // startPoint.SetActive(true);
                    //endPoint.SetActive(false);
                    DisplayFutureDike = true;
                }
            }
            //_apiTest.TestDrawDykeWithParams(_startPoint, _endPoint);
            //_dykePointCnt = 0;
        }

        if (rightHandTriggerButton != null && !rightHandTriggerButton.action.inProgress)
        {
            if (_inTriggerPress)
            {
                _inTriggerPress = false;
                if (rightXRRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit raycastHit))
                {
                    _endPoint = raycastHit.point;
                    endPoint.transform.position = _endPoint;
                   // endPoint.active = true;
                    DisplayFutureDike = false;
                    if (FutureDike != null)
                    {
                        FutureDike.SetActive(false);
                        GameObject.DestroyImmediate(FutureDike);

                        FutureDike = null;
                    }
                   

                    APITest.Instance.TestDrawDykeWithParams(_startPoint, _endPoint);
                        
                  //  Debug.Log("Number of dykes: " + dykeObjects.Length);
                }
            }
        }
        

    }

    private void HandleConnectionAttempted(bool success) {
        Debug.Log("SimulationManager: Connection attempt " + (success ? "successful" : "failed"));
        if (success) {
            if(IsGameState(GameState.MENU)) {
                Debug.Log("SimulationManager: Successfully connected to middleware");
                UpdateGameState(GameState.WAITING);
            }
        } else {
            // stay in MENU state
            Debug.Log("Unable to connect to middleware");
        }
    }

    private void TryReconnect()
    {
        Dictionary<string, string> args = new Dictionary<string, string> {
            {"id",ConnectionManager.Instance.getUseMiddleware() ? ConnectionManager.Instance.GetConnectionId()  : ("\"" + ConnectionManager.Instance.GetConnectionId() +  "\"") }};

        ConnectionManager.Instance.SendExecutableAsk("ping_GAMA", args);

        currentTimePing = maxTimePing;
        Debug.Log("Sent Ping test");

    }

    // ############################################# UTILITY FUNCTIONS ########################################


    public bool IsGameState(GameState state) {
        return currentState == state;
    }


    public GameState GetCurrentState() {
        return currentState;
    }
}


// ############################################################
public enum GameState {
    // not connected to middleware
    MENU,
    // connected to middleware, waiting for authentication
    WAITING,
    // connected to middleware, authenticated, waiting for initial data from middleware
    LOADING_DATA,
    // connected to middleware, authenticated, initial data received, simulation running
    GAME,
    END, 
    CRASH
}



[System.Serializable]
public class ScoreMessage
{


    public int score;

    public static ScoreMessage CreateFromJSON(string jsonString)
    {
        return JsonUtility.FromJson<ScoreMessage>(jsonString);
    }

}




public static class Extensions
{
    public static bool TryGetComponent<T>(this GameObject obj, T result) where T : Component
    {
        return (result = obj.GetComponent<T>()) != null;
    }
}