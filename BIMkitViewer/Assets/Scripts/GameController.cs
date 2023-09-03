using DbmsApi;
using DbmsApi.API;
using DbmsApi.Mongo;
using MathPackage;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.AI;
using static UnityEngine.UI.Dropdown;
using Component = DbmsApi.API.Component;
using Debug = UnityEngine.Debug;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;

public class GameController : MonoBehaviour
{
    #region UI Fields

    public Camera MainCamera;
    public GameObject LoadingCanvas;

    public GameObject ModelViewCanvas;
    public Text ObjectDataText;

    public GameObject ModelSelectCanvas;
    public Button ModelButtonPrefab;
    private List<ButtonData> ModelButtonData;
    public GameObject ModelListViewContent;

    public GameObject ModelObjectPrefab;
    public GameObject ModelComponentPrefab;

    public GameObject ModelDeleteCanvas;
    private GameObject UpperRange;
    private GameObject LowerRange;
    public GameObject PlanePrefabUpper;
    public GameObject PlanePrefabLower;

    public GameObject TextureListViewContent;
    public Button TextureButtonPrefab;

    public Dropdown startDropdown;
    public Dropdown endDropdown;

    public Dropdown startBathroomDropdown;

    public Text distanceText;
    
    public InputField propertyInputField;
    public InputField valueInputField;
    public GameObject assignPanel;
    public Button applyPropertyButton;

    public InputField MaxOccupancyField;
    public InputField SearchInputField;

    public InputField roomTypeInputField;

    #endregion

    #region Game Fields

    private List<Model> CurrentModels;
    public GameObject ModelsGameObj;
    public List<GameObject> CurrentModelGameObjs;
    private List<ModelObjectScript> ModelObjects = new List<ModelObjectScript>();
    private GameObject ViewingGameObject;
    private List<ModelObjectScript> SpaceObjectList = new List<ModelObjectScript>();

    public float cameraMoveSpeed = 200f;
    float cameraScrollSensitivity = 10f;
    private GameObject PreviouslyActiveCanvas;

    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private NavMeshAgent agent;
    public GameObject playerPrefab;
    private bool playerInstantiated;
    private bool objectDeleted;
    public Vector3 startPosition;
    public Vector3 endPosition;
    private float distance;

    private bool distanceCalculated;
    private bool pathSet;

    public List<Tuple<ModelObjectScript, float, bool>> ObjectDistanceTupleList = new List<Tuple<ModelObjectScript, float, bool>>();
    public Tuple<ModelObjectScript, float, bool> ObjectDistanceTuple;
    #endregion

    #region Materials

    public Material HighlightMatRed;
    public Material HighlightMatYellow;
    public Material HighlightMatGreen;
    public Material DefaultMat;
    public Material TempMat;
    public Material VoxelMaterial;
    public Material FloorMaterial;
    public Material InvisibleMaterial;

    private Dictionary<string, Material> MaterialDict;

    #endregion

    // Start is called before the first frame update
    void Start()
    {
        ResetCanvas();
        this.ModelSelectCanvas.SetActive(true);

        MaterialDict = new Dictionary<string, Material>()
        {
            { "Floor", FloorMaterial },
            { "Space", VoxelMaterial },
            { "Room", VoxelMaterial },
        };

        FindModelPaths();
        FindTexturePaths();
    }

    // Update is called once per frame
    void Update()
    {
        if (this.ModelViewCanvas.activeInHierarchy)
        {
            if (Input.GetKey(KeyCode.Space))
            {
                foreach (ModelObjectScript mObject in SpaceObjectList)
                {
                    GameObject spaceModelObject = mObject.gameObject;
                    SetLayerRecursive(spaceModelObject, 0);
                    spaceModelObject.GetComponent<ModelObjectScript>().SetMainMaterial(TempMat);
                }
            }
            else
            {
                foreach (ModelObjectScript mObject in SpaceObjectList)
                {
                    GameObject spaceModelObject = mObject.gameObject;
                    SetLayerRecursive(spaceModelObject, 2);
                    spaceModelObject.GetComponent<ModelObjectScript>().SetMainMaterial(InvisibleMaterial);
                }
            }

            ViewingMode();
        }

        if (this.ModelDeleteCanvas.activeInHierarchy)
        {
            UpdateRanges();
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                DeleteObjectsOutsideHeightRangeClicked();
            }
        }
    }

    void FixedUpdate()
    {
        MoveCamera();
    }

    #region Camera Controls

    private void MoveCamera()
    {
        if (Input.GetMouseButton(1))
        {
            MainCamera.transform.Rotate(new Vector3(-Input.GetAxis("Mouse Y") * cameraMoveSpeed * Time.deltaTime, Input.GetAxis("Mouse X") * cameraMoveSpeed * Time.deltaTime, 0));
            float X = MainCamera.transform.rotation.eulerAngles.x;
            float Y = MainCamera.transform.rotation.eulerAngles.y;
            MainCamera.transform.rotation = Quaternion.Euler(X, Y, 0);
        }

        if (Input.GetMouseButton(2))
        {
            var newPosition = new Vector3();
            newPosition.x = Input.GetAxis("Mouse X") * cameraMoveSpeed * Time.deltaTime;
            newPosition.y = Input.GetAxis("Mouse Y") * cameraMoveSpeed * Time.deltaTime;
            MainCamera.transform.Translate(-newPosition);
        }

        if (!EventSystem.current.IsPointerOverGameObject())
        {
            MainCamera.transform.position += MainCamera.transform.forward * Input.GetAxis("Mouse ScrollWheel") * cameraScrollSensitivity;
        }
       
    }

    private void SetupMainCamera()
    {
        List<ModelObject> mos = ModelObjects.Select(m => m.ModelObject).ToList();
        List<Vector3D> vList = mos.SelectMany(m => m.Components.SelectMany(c => c.Vertices.Select(v => Vector3D.Add(v, m.Location)))).ToList();
        Utils.GetXYZDimentions(vList, out Vector3D mid, out Vector3D dims);

        Vector3 center = VectorConvert(mid);
        Vector3 diment = VectorConvert(dims);

        MainCamera.orthographic = false;
        MainCamera.nearClipPlane = 0.1f;
        MainCamera.farClipPlane = 100.0f;
        MainCamera.transform.position = new Vector3(center.x, center.y + 2.0f * diment.y, center.z);
        MainCamera.transform.LookAt(center, Vector3.up);
    }

    #endregion

    #region Model Select Mode

    public void ModelViewClicked()
    {
        ResetCanvas();
        this.ModelViewCanvas.SetActive(true);
    }

    public void FindModelPaths()
    {
        // Get all file names
        string modelsPath = Application.dataPath + "/../../Models";
        Debug.Log(Application.dataPath);
        string[] fileNames = Directory.GetFiles(modelsPath, "*.bpmm");

        RemoveAllChidren(this.ModelListViewContent);
        ModelButtonData = new List<ButtonData>();
        foreach (string modelPath in fileNames)
        {
            Button newButton = GameObject.Instantiate(this.ModelButtonPrefab, this.ModelListViewContent.transform);
            newButton.GetComponentInChildren<Text>().text = Path.GetFileNameWithoutExtension(modelPath);
            UnityAction action = new UnityAction(() =>
            {
                ModelButtonClicked(newButton, modelPath);
            });
            newButton.onClick.AddListener(action);
            ModelButtonData.Add(newButton.GetComponent<ButtonData>());
        }

        LoadingCanvas.SetActive(false);
    }

    public void FindTexturePaths()
    {
        string texturesPath = Application.dataPath + "/_Textures";
        string[] textureFileNames = Directory.GetFiles(texturesPath, "*.jpeg");
        List<Sprite> sprites = new List<Sprite>();
        Button newTextureButton;

        foreach (string texturePath in textureFileNames)
        {
            var fileContent = File.ReadAllBytes(texturePath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            tex.LoadImage(fileContent);

            Color[] pix = tex.GetPixels();
            tex.SetPixels(pix);
            tex.Apply();

            float height = tex.height;
            float width = tex.width;

            //Sprite textureSprite = Sprite.Create(tex, new Rect(0, 0, height, width), new Vector2(0, 0));
            newTextureButton = GameObject.Instantiate(this.TextureButtonPrefab, TextureListViewContent.transform);
            newTextureButton.GetComponentInChildren<Text>().text = Path.GetFileNameWithoutExtension(texturePath);
            //newTextureButton.GetComponent<Image>().sprite = textureSprite;

            Material material = new Material(Shader.Find("Standard"));
            material.mainTexture = tex;
            material.SetTexture(Path.GetFileNameWithoutExtension(texturePath), tex);

            string savePath = AssetDatabase.GetAssetPath(tex);
            savePath = savePath.Substring(0, savePath.LastIndexOf('/') + 1);

            string newAssetName = "Assets/_Textures/" + Path.GetFileNameWithoutExtension(texturePath) + ".mat";

            AssetDatabase.CreateAsset(material, newAssetName);

            AssetDatabase.SaveAssets();

            UnityAction action = new UnityAction(() =>
            {
                _SetTextureSelected(newTextureButton, material);
            });
            newTextureButton.onClick.AddListener(action);
        }
    }

    public void FindRoomDropdown()
    {
        startDropdown.options.Clear();
        endDropdown.options.Clear();
        startDropdown.options.Clear();

        foreach (ModelObjectScript mObject in SpaceObjectList)
        {
            GameObject modelObject = mObject.gameObject;

            //startPosition dropdown
            startDropdown.options.Add(new Dropdown.OptionData() { text = modelObject.name });
            startDropdown.onValueChanged.AddListener(delegate { startPosition = modelObject.transform.position; });

            //endPosition dropdown
            endDropdown.options.Add(new Dropdown.OptionData() { text = modelObject.name });
            endDropdown.onValueChanged.AddListener(delegate { endPosition = modelObject.transform.position; });

            ////For finding the closest bathroom
            //startBathroomDropdown.options.Add(new Dropdown.OptionData() { text = modelObject.name });
            //startBathroomDropdown.onValueChanged.AddListener(delegate { startPosition = modelObject.transform.position; });
        }
    }

    private void ModelButtonClicked(Button ruleButton, string modelPath)
    {
        ButtonData data = ruleButton.GetComponent<ButtonData>();
        data.Clicked = !data.Clicked;
        ruleButton.image.color = data.Clicked ? new Color(0, 250, 0) : new Color(255, 255, 255);
        data.Item = modelPath;
    }

    public void LoadMultipleModelClicked()
    {
        List<string> modelPaths = ModelButtonData.Where(rbd => rbd.Clicked).Select(r => (string)r.Item).ToList();
        if (modelPaths.Count == 0)
        {
            Debug.LogWarning("No Models Selected");
            return;
        }

        CurrentModels = new List<Model>();
        CurrentModelGameObjs = new List<GameObject>();
        ModelObjects = new List<ModelObjectScript>();
        RemoveAllChidren(ModelsGameObj);
        foreach (string modelPath in modelPaths)
        {
            LoadModel(modelPath);
        }
    }

    private void LoadModel(string modelFileName)
    {
        LoadingCanvas.SetActive(true);
        ModelSelectCanvas.SetActive(false);

        string modelName = Path.GetFileNameWithoutExtension(modelFileName);
        string modelDir = Path.GetDirectoryName(modelFileName);
        MongoModel loadedModel = DBMSReadWrite.JSONReadFromFile<MongoModel>(modelFileName);
        loadedModel.Name = modelName; // Name is weird sometimes

        List<ModelObjectSplit> modSplits = new List<ModelObjectSplit>();
        foreach (ModelObjectReference mor in loadedModel.ModelObjectReferences)
        {
            KeyValuePair<string, string> containedInTag = mor.Tags.Find(t => t.Key == "ContainedIn");

            //if (containedInTag.Value != null)
            //{
            //    if (!containedInTag.Value.Contains("7"))
            //    {
            //        continue;
            //    }
            //}
            //else
            //{
            //    continue;
            //}

            string splitFileName = modelDir + "/" + modelName + "/" + mor.ModelRefId + ".bpmo";
            try
            {
                modSplits.Add(DBMSReadWrite.JSONReadFromFile<ModelObjectSplit>(splitFileName));
            }
            catch
            {
                Debug.LogWarning("Could not find file: " + splitFileName);
            }
        }

        Model currentModel = new Model();
        currentModel.ReadFromMogoModel(loadedModel, modSplits, new List<CatalogObject>());
        CurrentModels.Add(currentModel);
        GameObject currentModelGameObj = new GameObject(currentModel.Name);
        currentModelGameObj.transform.parent = ModelsGameObj.transform;
        CurrentModelGameObjs.Add(currentModelGameObj);

        ObjectSkipCount = 0;
        foreach (ModelObject obj in currentModel.ModelObjects)
        {
            CreateObjects(obj, currentModelGameObj);
        }
        Debug.LogWarning("Objects Boxed = " + ObjectSkipCount.ToString());

        foreach (ModelObjectScript mObject in ModelObjects)
        {
            GameObject modelObject = mObject.gameObject;
            string TypeIdString = mObject.ModelObject.TypeId;
            int IgnoreNavMeshLayer = LayerMask.NameToLayer("Doors");

            if (modelObject.name.Contains("Space"))
            {
                SpaceObjectList.Add(mObject);
            }

            if (TypeIdString == "Door")
            {
                SetLayerRecursive(modelObject, IgnoreNavMeshLayer);
            }
            else
            {
                continue;
            }
        }

        distanceText.text = "Rough distance: null";

        SetupMainCamera();

        ResetCanvas();
        ModelViewCanvas.SetActive(true);
        LoadingCanvas.SetActive(false);
    }

    private int ObjectSkipCount;
    public int OBJ_TRI_LIMIT = 100;
    private void CreateObjects(ModelObject obj, GameObject CurrentModelGameObj)
    {
        GameObject modelObject = CreateModelObject(obj, CurrentModelGameObj);
        int triangleCount = obj.Components.Sum(c => c.Triangles.Count);
        if (triangleCount <= OBJ_TRI_LIMIT)
        {
            CreateComponents(obj.Components, modelObject);
        }
        else
        {
            // Replace with a box?
            Utils.GetXYZDimentions(obj.Components.SelectMany(c => c.Vertices).ToList(), out Vector3D center, out Vector3D dims);
            CreateBoxComponent(Utils.CreateBoundingBox(center, dims, FaceSide.BACK), modelObject);
            ObjectSkipCount++;
        }

        modelObject.name = obj.Name;
        modelObject.transform.SetPositionAndRotation(VectorConvert(obj.Location), VectorConvert(obj.Orientation));
        ModelObjectScript script = modelObject.GetComponent<ModelObjectScript>();
        script.ModelObject = obj;

        if (MaterialDict.TryGetValue(obj.TypeId, out Material material))
        {
            script.SetMainMaterial(material);
        }
        else
        {
            script.SetMainMaterial(DefaultMat);
        }

        ModelObjects.Add(script);
    }

    private GameObject CreateModelObject(ModelObject o, GameObject parentObj)
    {
        o.Id = o.Id ?? Guid.NewGuid().ToString();
        o.Orientation = o.Orientation ?? Utils.GetQuaterion(new Vector3D(0, 0, 1), 0.0 * Math.PI / 180.0);
        o.Location = o.Location ?? new Vector3D(0, 0, 0);
        return Instantiate(ModelObjectPrefab, parentObj.transform);
    }

    private void CreateComponents(List<Component> components, GameObject parentObj)
    {
        foreach (Component c in components)
        {
            GameObject meshObject = Instantiate(ModelComponentPrefab, parentObj.transform);
            Mesh mesh = new Mesh();
            MeshFilter meshFilter = meshObject.GetComponent<MeshFilter>();
            meshFilter.mesh = mesh;
            MeshCollider meshCollider = meshObject.GetComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            mesh.vertices = c.Vertices.Select(v => VectorConvert(v)).ToArray();
            mesh.uv = mesh.vertices.Select(v => new Vector2(v.x, v.y)).ToArray();
            mesh.uv = CalculateUVs(mesh, mesh.vertices.ToList());
            mesh.triangles = c.Triangles.SelectMany(t => new List<int>() { t[0], t[1], t[2] }).Reverse().ToArray();
            mesh.RecalculateNormals();
        }
    }

    private void CreateBoxComponent(MathPackage.Mesh boxMesh, GameObject parentObj)
    {
        GameObject meshObject = Instantiate(ModelComponentPrefab, parentObj.transform);
        Mesh mesh = new Mesh();
        MeshFilter meshFilter = meshObject.GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        MeshCollider meshCollider = meshObject.GetComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        mesh.vertices = boxMesh.VertexList.Select(v => VectorConvert(v)).ToArray();
        mesh.triangles = boxMesh.TriangleList.SelectMany(t => new List<int>() { t[0], t[1], t[2] }).ToArray();
        mesh.RecalculateNormals();
        //mesh.uv = mesh.vertices.Select(v => new Vector2(v.x, v.y)).ToArray();
        mesh.uv = CalculateUVs(mesh, mesh.vertices.ToList());
    }
    private static Vector2[] CalculateUVs(Mesh mesh, List<Vector3> newVerticesFinal)
    {
        // calculate UVs ============================================
        float scaleFactor = 1.0f;
        Vector2[] uvs = new Vector2[newVerticesFinal.Count];
        int len = mesh.triangles.Length;
        int[] tris = mesh.triangles;
        int normCounter = 0;
        for (int i = 0; i < len; i += 3)
        {
            Vector3 v1 = newVerticesFinal[tris[i + 0]];
            Vector3 v2 = newVerticesFinal[tris[i + 1]];
            Vector3 v3 = newVerticesFinal[tris[i + 2]];
            Vector3 normal = Vector3.Cross(v3 - v1, v2 - v1);
            normal.Normalize();
            Quaternion rotation = Quaternion.LookRotation(normal);
            rotation = Quaternion.Inverse(rotation);
            uvs[tris[i + 0]] = (Vector2)(rotation * v1) * scaleFactor;
            uvs[tris[i + 1]] = (Vector2)(rotation * v2) * scaleFactor;
            uvs[tris[i + 2]] = (Vector2)(rotation * v3) * scaleFactor;
            //Vector3 u1 = uvs[tris[i + 0]];
            //Vector3 u2 = uvs[tris[i + 1]];
            //Vector3 u3 = uvs[tris[i + 2]];
            //Vector3 normal2 = Vector3.Cross(u3 - u1, u2 - u1);
            normCounter++;
        }
        //==========================================================
        return uvs;
    }

    public static Vector3 VectorConvert(Vector3D v)
    {
        return new Vector3((float)v.x, (float)v.z, (float)v.y);
    }
    public static Quaternion VectorConvert(Vector4D v)
    {
        return new Quaternion((float)v.x, (float)v.z, (float)v.y, (float)v.w);
    }
    public static Vector3D VectorConvert(Vector3 v)
    {
        return new Vector3D((float)v.x, (float)v.z, (float)v.y);
    }
    public static Vector4D VectorConvert(Quaternion v)
    {
        return new Vector4D((float)v.x, (float)v.z, (float)v.y, (float)v.w);
    }

    #endregion

    #region Model View Mode

    private void ViewingMode()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Ray ray = MainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hitData;
            if (Physics.Raycast(ray, out hitData, 1000))
            {
                ModelObjectScript mos;
                if (ViewingGameObject != null)
                {
                    mos = ViewingGameObject.GetComponent<ModelObjectScript>();
                    if (mos != null)
                    {
                        mos.UnHighlight();
                    }
                }

                GameObject hitObject = hitData.collider.gameObject;

                // Hit a model object component
                ViewingGameObject = hitData.collider.gameObject.transform.parent.gameObject;

                mos = ViewingGameObject.GetComponent<ModelObjectScript>();
                if (mos != null)
                {
                    mos.Highlight(HighlightMatYellow);
                    DisplayObjectInfo(mos);
                }

                startPosition = hitData.point;
            }

        }
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            DeleteSelectedObjectClicked();
        }

        if (playerInstantiated && agent.hasPath)
        {
            Vector3 recentPosition = new Vector3(0, 0, 0);

            DrawAgentPath();

            if (!distanceCalculated)
            {
                CalculateDistance();
                distanceCalculated = true;
                distance = 0;
            }

            if (recentPosition != endPosition)
            {
                distanceCalculated = false;

                recentPosition = endPosition;
            }
        }

        //if (FillTupleList())
        //{
        //    YieldCalculatePath();
        //    GetDistanceOfTupleItem();
        //}
    }

    private void DisplayObjectInfo(ModelObjectScript mos)
    {
        if (mos == null)
        {
            return;
        }

        ObjectDataText.text = "Name: " + mos.ModelObject.Name + "\n";
        ObjectDataText.text += "Id: " + mos.ModelObject.Id + "\n";

        if (mos.ModelObject.GetType() == typeof(ModelCatalogObject))
        {
            ObjectDataText.text += "Catalog Id: " + ((ModelCatalogObject)mos.ModelObject).CatalogId + "\n";
        }

        ObjectDataText.text += "TypeId: " + mos.ModelObject.TypeId + "\n\n";
        foreach (Property p in mos.ModelObject.Properties)
        {
            ObjectDataText.text += p.Name + ": " + p.GetValueString() + "\n";
        }
        foreach (var tag in mos.ModelObject.Tags)
        {
            ObjectDataText.text += tag.Key + ": " + tag.Value + "\n";
        }
    }

    public void DeleteSelectedObjectClicked()
    {
        if (ViewingGameObject != null)
        {
            ModelObjectScript deletingMOS = ViewingGameObject.GetComponent<ModelObjectScript>();
            DeleteModelObjectScript(deletingMOS);
            ViewingGameObject = null;
        }
    }

    private void DeleteModelObjectScript(ModelObjectScript deletingMOS)
    {
        ModelObjects.Remove(deletingMOS);
        foreach (Model model in CurrentModels)
        {
            model.ModelObjects.Remove(deletingMOS.ModelObject);
        }
        RemoveAllChidren(deletingMOS.gameObject);
    }

    public void DeleteMultipleClicked()
    {
        // create two planes at different heights
        LowerRange = Instantiate(PlanePrefabLower);
        UpperRange = Instantiate(PlanePrefabUpper);

        // load a new canvas for raising and lowering the planes
        ResetCanvas();
        this.ModelDeleteCanvas.SetActive(true);
    }

    public void SaveClicked()
    {
        LoadingCanvas.SetActive(true);
        SaveModel();
        LoadingCanvas.SetActive(false);
    }

    private void SaveModel()
    {
        if (CurrentModels.Count > 0)
        {
            Debug.LogWarning("All models saved individually");
        }

        foreach (Model model in CurrentModels)
        {
            // Save the full thing into a new Model with a new name
            MongoModel mongoModel = new MongoModel(model, "");
            List<ModelObjectSplit> moSplits = mongoModel.SplitModelObjectFromModel();
            string modelsPath = Application.dataPath + "/../../Models";
            DBMSReadWrite.WriteModel(mongoModel, modelsPath + "/" + model.Name + ".bpmm");
            string folderName = modelsPath + "/" + model.Name;
            if (Directory.Exists(folderName))
            {
                Directory.Delete(folderName, true);
            }
            Directory.CreateDirectory(folderName);
            foreach (ModelObjectSplit mos in moSplits)
            {
                DBMSReadWrite.JSONWriteToFile(folderName + "/" + mos.RefId + ".bpmo", mos);
            }
        }
    }

    public void SaveAsClicked()
    {
        LoadingCanvas.SetActive(true);
        SaveAsNewModel();
        LoadingCanvas.SetActive(false);
    }

    private void SaveAsNewModel()
    {
        // Combine all Models into one (for now just worries about the model obejcts but could combine properties and tags and such too later)
        Model combinedModel = new Model();
        combinedModel.Name = "CombinedModel";
        foreach (Model model in CurrentModels)
        {
            combinedModel.ModelObjects.AddRange(model.ModelObjects);
        }

        // Save the full thing into a new Model with a new name
        MongoModel mongoModel = new MongoModel(combinedModel, "");
        List<ModelObjectSplit> moSplits = mongoModel.SplitModelObjectFromModel();
        string modelsPath = Application.dataPath + "/../../Models";
        DBMSReadWrite.WriteModel(mongoModel, modelsPath + "/" + combinedModel.Name + ".bpmm");
        string folderName = modelsPath + "/" + combinedModel.Name;
        if (Directory.Exists(folderName))
        {
            Directory.Delete(folderName, true);
        }
        Directory.CreateDirectory(folderName);
        foreach (ModelObjectSplit mos in moSplits)
        {
            DBMSReadWrite.JSONWriteToFile(folderName + "/" + mos.RefId + ".bpmo", mos);
        }

        ExitClicked();
    }

    public void ExitClicked()
    {
        CurrentModels = new List<Model>();
        CurrentModelGameObjs = new List<GameObject>();
        RemoveAllChidren(ModelsGameObj);
        ResetCanvas();
        FindModelPaths();
        this.ModelSelectCanvas.SetActive(true);

        playerInstantiated = false;
        playerPrefab = (GameObject)AssetDatabase.LoadAssetAtPath("Assets/Prefabs/PlayerPrefab.prefab", typeof(GameObject));
        startPosition = new Vector3(0, 0, 0);
        endPosition = new Vector3(0, 0, 0);
    }

    #endregion

    #region Delete Objects Mode

    private void UpdateRanges()
    {
        if (Input.GetKey(KeyCode.A))
        {
            LowerRange.transform.position += new Vector3(0, 0.1f, 0);
        }
        if (Input.GetKey(KeyCode.S))
        {
            LowerRange.transform.position += new Vector3(0, -0.1f, 0);
        }
        if (Input.GetKey(KeyCode.D))
        {
            UpperRange.transform.position += new Vector3(0, 0.1f, 0);
        }
        if (Input.GetKey(KeyCode.F))
        {
            UpperRange.transform.position += new Vector3(0, -0.1f, 0);
        }
    }

    public void DeleteObjectsOutsideHeightRangeClicked()
    {
        List<ModelObjectScript> deletingModelObjectScripts = new List<ModelObjectScript>();
        for (int i = 0; i < ModelObjects.Count; i++)
        {
            ModelObjectScript mos = ModelObjects[i];
            if (mos.gameObject.transform.position.y < LowerRange.transform.position.y || mos.gameObject.transform.position.y > UpperRange.transform.position.y)
            {
                deletingModelObjectScripts.Add(mos);
            }
        }
        foreach (ModelObjectScript mos in deletingModelObjectScripts)
        {
            DeleteModelObjectScript(mos);
        }
    }

    public void DoneDeletingClicked()
    {
        Destroy(LowerRange);
        Destroy(UpperRange);
        ResetCanvas();
        this.ModelViewCanvas.SetActive(true);
    }

    #endregion

    #region Random Methods

    private static void RemoveAllChidren(GameObject obj)
    {
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            var child = obj.transform.GetChild(i);
            Destroy(child.gameObject);
        }
    }

    private static void ResetAllModelObjectTagAndLayers(List<ModelObjectScript> modelObjects)
    {
        foreach (ModelObjectScript moScript in modelObjects)
        {
            ChangeAllChidrenTagsAndLayer(moScript.gameObject, "Untagged", 0);
        }
    }

    private static void ChangeAllChidrenTagsAndLayer(GameObject obj, string newTag, int newLayer)
    {
        if (obj == null)
        {
            Debug.LogError("GameObject is Null");
        }

        obj.transform.tag = newTag;
        obj.layer = newLayer;
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            var child = obj.transform.GetChild(i);
            ChangeAllChidrenTagsAndLayer(child.gameObject, newTag, newLayer);
        }
        obj.transform.tag = newTag;
        obj.layer = newLayer;
    }

    private void ResetCanvas()
    {
        this.ModelViewCanvas.SetActive(false);
        this.ModelSelectCanvas.SetActive(false);
        this.ModelDeleteCanvas.SetActive(false);

        UnHighlightAllObjects();

        ViewingGameObject = null;

        RemoveAllChidren(this.ModelListViewContent);
    }

    private void UnHighlightAllObjects()
    {
        foreach (ModelObjectScript mo in ModelObjects)
        {
            if (mo.IsHighlighted)
            {
                mo.UnHighlight();
            }
        }
    }

    void _SetTextureSelected(Button textureButton, Material material)
    {
        ViewingGameObject.GetComponent<ModelObjectScript>().SetMainMaterial(material);
    }

    private void DrawAgentPath()
    {
        List<Vector3> tempList = new List<Vector3>();

        for (int i = 0; i < agent.path.corners.Length; i++)
        {
            tempList.Add(agent.path.corners[i]);
        }

        LineRenderer lineRenderer = playerPrefab.GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.positionCount = tempList.Count;

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startWidth = 0.3f;
        lineRenderer.endWidth = 0.3f;

        for (int i = 0; i < tempList.Count; i++)
        {
            Vector3 pointPosition = new Vector3(tempList[i].x, tempList[i].y, tempList[i].z);
            lineRenderer.SetPosition(i, pointPosition);
        }
    }

    void CalculateDistance()
    {
        for (int i = 0; i < agent.path.corners.Length - 1; i++)
        {
            distance += Vector3.Distance(agent.path.corners[i], agent.path.corners[i + 1]);
        }

        distanceText.text = "Rough distance: " + Mathf.Round(distance * 10f) * 0.1 + "m";
    }

    private void SetLayerRecursive(GameObject go, int Layer)
    {
        go.layer = Layer;

        foreach (Transform child in go.transform)
        {
            SetLayerRecursive(child.gameObject, Layer);
        }
    }

    public void GoButtonPressed()
    {
        if (!playerInstantiated)
        {
            InstantiatePlayer();
        }

        playerPrefab.transform.position = startPosition;

        if (playerPrefab.transform.position == startPosition)
        {
            agent.SetDestination(endPosition);
        }
    }

    public void AsignedRoomValuesClicked(string property, string value)
    {
        ModelObject selectedMo = ViewingGameObject.GetComponent<ModelObjectScript>().ModelObject;

        if (selectedMo.Properties.ContainsKey(property))
        {
            selectedMo.Properties.Items[property] = new PropertyString(property, value);
        }
        else
        {
            selectedMo.Properties.Add(new PropertyString(property, value));
        }
    }

    public void AsignButtonClicked()
    {
        UnityAction action = new UnityAction(() =>
        {
            AsignedRoomValuesClicked(propertyInputField.text, valueInputField.text);
        });
        applyPropertyButton.onClick.AddListener(action);
    }


    public void SetMaximumOccupancy()
    {
        ModelObject selectedMo = ViewingGameObject.GetComponent<ModelObjectScript>().ModelObject;

        if (!selectedMo.Properties.ContainsKey("Maximum Occupancy"))
        {
            selectedMo.Properties.Add(new PropertyString("Maximum Occupancy", MaxOccupancyField.text));
        }
        else
        {
            selectedMo.Properties.Items["Maximum Occupancy"] = new PropertyString("Maximum Occupancy", MaxOccupancyField.text);
        }
    }

    public void SeachByMaxOccupancy()
    {   
        foreach (ModelObjectScript mObject in ModelObjects)
        {
            ModelObject modelObject = mObject.GetComponent<ModelObjectScript>().ModelObject;
            GameObject modelsGameObj = mObject.gameObject;

            if (modelObject.Properties.ContainsKey("Maximum Occupancy"))
            {
                if ((modelObject.Properties.Items["Maximum Occupancy"] as PropertyString).Value == SearchInputField.text)
                {
                    mObject.UnHighlight();

                    modelsGameObj.GetComponent<ModelObjectScript>().SetMainMaterial(HighlightMatRed);
                }
            }
        }
    }

    public void SetRoomType()
    {
        ModelObject selectedMo = ViewingGameObject.GetComponent<ModelObjectScript>().ModelObject;

        if (!selectedMo.Properties.ContainsKey("Room Type"))
        {
            selectedMo.Properties.Add(new PropertyString("Room Type", roomTypeInputField.text));
        }
        else
        {
            selectedMo.Properties.Items["Room Type"] = new PropertyString("Room Type", roomTypeInputField.text);
        }
    }

    public void FillTupleList()
    {
        ObjectDistanceTupleList = new List<Tuple<ModelObjectScript, float, bool>>();

        foreach (ModelObjectScript mObject in ModelObjects)
        {
            ModelObject modelObject = mObject.GetComponent<ModelObjectScript>().ModelObject;

            if (modelObject.Properties.ContainsKey("Room Type") && (modelObject.Properties.Items["Room Type"] as PropertyString).Value == "Bathroom")
            {
                ObjectDistanceTuple = new Tuple<ModelObjectScript, float, bool>(mObject, 0, false);

                ObjectDistanceTupleList.Add(ObjectDistanceTuple);
            }
        }
        currentIndex = 0;
    }

    int currentIndex = 0;
    Tuple<ModelObjectScript, float, bool> currentCalculatingObj;
    bool waitingForPathToCalculate;
    public void YieldCalculatePath()
    {
        if (ObjectDistanceTupleList != null && !waitingForPathToCalculate)
        {
            if (ObjectDistanceTupleList.Count == currentIndex)
            {
                var maxListItem = ObjectDistanceTupleList[0];

                foreach (var qqqqq in ObjectDistanceTupleList)
                {
                    if (qqqqq.Item2 < maxListItem.Item2)
                    {
                        maxListItem = qqqqq;
                    }
                }

                agent.SetDestination(maxListItem.Item1.gameObject.transform.position);

                ObjectDistanceTupleList = null;
            }
            else
            {
                currentCalculatingObj = ObjectDistanceTupleList[currentIndex];

                waitingForPathToCalculate = true;
            }
        }
    }

    public void GetDistanceOfTupleItem()
    {
        float distanceToRoom = 0;

        if (waitingForPathToCalculate && !agent.hasPath)
        {
            agent.SetDestination(currentCalculatingObj.Item1.gameObject.transform.position);
        }
        else if (waitingForPathToCalculate && agent.hasPath)
        {
            for (int i = 0; i < agent.path.corners.Length - 1; i++)
            {
                distanceToRoom += Vector3.Distance(agent.path.corners[i], agent.path.corners[i + 1]);
            }

            ObjectDistanceTupleList[currentIndex] = new Tuple<ModelObjectScript, float, bool> (currentCalculatingObj.Item1, distanceToRoom, true);

            if (ObjectDistanceTupleList[currentIndex].Item3 == true)
            {
                currentIndex++;
                waitingForPathToCalculate = false;
            }
        }
    }

    public void FindBathromClicked()
    {
        if (!playerInstantiated)
        {
            InstantiatePlayer();
        }

        FillTupleList();
        YieldCalculatePath();
        GetDistanceOfTupleItem();
    }

    public void InstantiatePlayer()
    {
        navMeshSurface.BuildNavMesh();

        playerPrefab = GameObject.Instantiate(playerPrefab);
        playerPrefab.transform.position = ViewingGameObject.transform.position;

        playerPrefab.AddComponent<NavMeshAgent>();
        playerPrefab.AddComponent<LineRenderer>();

        Renderer r = playerPrefab.GetComponent<Renderer>();

        agent = playerPrefab.GetComponent<NavMeshAgent>();
        agent.speed = 0;

        playerInstantiated = true;
    }
    #endregion

    //private void CheckOverlapRuntime(ModelObjectScript mosEditingObj)
    //{
    //    //if (mo.Components.Sum(c => c.Triangles.Count) > 20)
    //    //{
    //    //    return;
    //    //}
    //    // Just an Overlap Check for testing
    //    bool overlapingSomething = false;
    //    RuleCheckObject rco1 = new RuleCheckObject(mosEditingObj.ModelObject);
    //    MathPackage.Mesh mesh1 = GetGlobalBBMesh(rco1);
    //    foreach (ModelObjectScript mos in ModelObjects)
    //    {
    //        if (mos == mosEditingObj)// || mos.ModelObject.Components.Sum(c => c.Triangles.Count) > 20)
    //        {
    //            continue;
    //        }
    //        mos.UnHighlight();

    //        RuleCheckObject rco2 = new RuleCheckObject(mos.ModelObject);
    //        MathPackage.Mesh mesh2 = GetGlobalBBMesh(rco2);
    //        if (Utils.MeshOverlap(mesh1, mesh2, 1.0))
    //        {
    //            mos.Highlight(HighlightMatRed);
    //            overlapingSomething = true;
    //            break;
    //        }
    //    }
    //    if (overlapingSomething)
    //    {
    //        mosEditingObj.Highlight(HighlightMatRed);
    //    }
    //    else
    //    {
    //        mosEditingObj.UnHighlight();
    //    }
    //}

    //private MathPackage.Mesh GetGlobalBBMesh(RuleCheckObject rco)
    //{
    //    Utils.GetXYZDimentions(rco.LocalVerticies, out Vector3D center, out Vector3D dims);
    //    MathPackage.Mesh bbMesh = Utils.CreateBoundingBox(center, dims, FaceSide.FRONT);
    //    Matrix4 transMat = Utils.GetTranslationMatrixFromLocationOrientation(rco.Location, rco.Orientation);
    //    List<Vector3D> transVects = Utils.TranslateVerticies(transMat, bbMesh.VertexList);
    //    return new MathPackage.Mesh(transVects, bbMesh.TriangleList);
    //}
    //
    //public void SpawnAgent()
    //{
    //    if (!playerInstantiated && ViewingGameObject != null)
    //    {
    //        navMeshSurface.BuildNavMesh();

    //        playerPrefab = GameObject.Instantiate(playerPrefab, ViewingGameObject.transform);
    //        playerInstantiated = true;

    //        playerPrefab.AddComponent<NavMeshAgent>();
    //        playerPrefab.AddComponent<LineRenderer>();

    //        agent = playerPrefab.GetComponent<NavMeshAgent>();
    //    }
    //    else
    //    {
    //        Debug.LogWarning("Position not selected or player is already in the scene");
    //    }
    //}

    //public void FindClosestBathroom()
    //{
    //    if (!playerInstantiated)
    //    {
    //        InstantiatePlayer();
    //    }

    //    GameObject closestBathroom = null;
    //    float distanceToClosest = 0;

    //    playerPrefab.transform.position = startPosition;

    //    foreach (ModelObjectScript mObject in ModelObjects)
    //    {
    //        float distanceToRoom = 0;

    //        ModelObject modelObject = mObject.GetComponent<ModelObjectScript>().ModelObject;
    //        GameObject modelGameObject = mObject.gameObject;

    //        if (modelObject.Properties.ContainsKey("Room Type"))
    //        {
    //            string roomTypeValue = (modelObject.Properties.Items["Room Type"] as PropertyString).Value;

    //            if (roomTypeValue.ToLower() == "bathroom")
    //            {
    //                agent.SetDestination(modelGameObject.transform.position);

    //                pathSet = true;
    //            }

    //        }
    //    }
    //}
}