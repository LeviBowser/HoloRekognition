using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.Text;
using UnityEngine.Networking;
using HoloToolkit.Unity;


/* ====== SUMMARY ======
 * This project uses the hololens to capture a photo using the Tap gesture and then send the
 * image to Microsoft's Face API endpoint to locate and identify faces in the image. Additionally,
 * the faceID that is returned after it is identified is used with the Face API to return more information
 * about the person that was uploaded via the companion app: Custom Person Maker.
 */

/* ====== SOME REQUIREMENTS ======
 * HoloToolkit (MixedRealityToolkit), Unity, and Microsoft Azure Cognitive Services Face API are required to run, build, or maintain this project.
 * 
 * HoloToolkit: https://github.com/Microsoft/MixedRealityToolkit-Unity
 * Unity: https://unity3d.com/get-unity/download
 * Face API:https://azure.microsoft.com/en-us/services/cognitive-services/face/
 */

public class FaceAnalysis : MonoBehaviour {

    /// <summary>
    /// Base endpoint of Face Recognition Service
    /// </summary>
    const string baseEndpoint = "https://westus.api.cognitive.microsoft.com/face/v1.0/";

    /// <summary>
    /// Auth key of Face Recognition Service
    /// </summary>
    private const string key = "<AUTH KEY GOES HERE>";

    /// <summary>
    /// Id (name) of the created person group 
    /// </summary>
    private const string personGroupId = "<PERSON GROUP ID GOES HERE>";

    /// <summary>
    /// When trying to identify faces, how many different matches are returned with their confidence levels
    /// Matches are returned with highest confidence listed first
    /// </summary>
    private const int MAX_NUM_POSSIBLE_CANDIDATES_RETURNED = 2;

    /// <summary>
    /// What confidence threshold must matches pass to be considered candidates to be returned
    /// </summary>
    private const double CONFIDENCE_THRESHOLD = 0.5;


    //Use this for initialization
    //void Start () {
    //}

    // Update is called once per frame
    //void Update(){
    //}


    /// <summary>
    /// Allows this class to behave like a singleton
    /// Meaning: only one instance of this class can be alive at any time.
    /// used a lot in ImageCapture.cs to set the internal properties listed below
    /// </summary>
    public static FaceAnalysis Instance;

    /// <summary>
    /// Bytes of the image captured with camera
    /// Given a value in ImageCapture.cs
    /// </summary>
    internal byte[] imageBytes;

    /// <summary>
    /// Path of the image captured with camera
    /// Given a value in ImageCapture.cs
    /// </summary>
    internal string imagePath;

    /// <summary>
    /// The resolution of the camera that was used to capture the image
    /// Given a value in ImageCapture.cs
    /// </summary>
    internal Resolution cameraResolution;

    /// <summary>
    /// The camera position and rotation that was used when the image was captured
    /// Given values in ImageCapture.cs
    /// </summary>
    internal Vector3 cameraPosition;
    internal Quaternion cameraRotation;

    /// <summary>
    /// The matrixes that were created when the image was captured.
    /// Matrixes are used to establish the coordinate system point relative to pixel coordinates in a taken photo
    /// https://docs.microsoft.com/en-us/windows/mixed-reality/locatable-camera#pixel-to-application-specified-coordinate-system
    /// Given values in ImageCapture.cs
    /// </summary>
    internal Matrix4x4 cameraToWorldMatrix;
    internal Matrix4x4 pixelToCameraMatrix;
    internal Matrix4x4 projectionMatrix;

    /// <summary>
    /// Dictionary that maintains positions of faceRectangles to prevent duplicates being created in same world space
    /// </summary>
    Dictionary<string, Vector3> faceRectanglePositionsDict = new Dictionary<string, Vector3>();
    

    /// <summary>
    /// Initialises this class
    /// </summary>
    private void Awake()
    {
        //// Allows this instance to behave like a singleton
        Instance = this;

        //// Add the ImageCapture Class to this Game Object to allow use and gesture recognition and image capture
        gameObject.AddComponent<ImageCapture>();
    }

    /// <summary>
    /// Detect faces from a submitted image
    /// Sends image bytes to Face API to locate any faces and return the pixel-based coordinate location
    /// and face square width and height
    /// </summary>
    internal IEnumerator DetectFacesFromImage()
    {
        WWWForm webForm = new WWWForm();
        string detectFacesEndpoint = $"{baseEndpoint}detect";

        using (UnityWebRequest www =
            UnityWebRequest.Post(detectFacesEndpoint, webForm))
        {
            www.SetRequestHeader("Ocp-Apim-Subscription-Key", key);
            www.SetRequestHeader("Content-Type", "application/octet-stream");
            www.uploadHandler.contentType = "application/octet-stream";
            www.uploadHandler = new UploadHandlerRaw(this.imageBytes);
            www.downloadHandler = new DownloadHandlerBuffer();

            yield return www.SendWebRequest();
            string jsonResponse = www.downloadHandler.text;
            Face_RootObject[] face_RootObject =
                JsonConvert.DeserializeObject<Face_RootObject[]>(jsonResponse);
            //Debug.Log($"====== JSON Response: {jsonResponse}");

            List<string> facesIdList = new List<string>();

            //// Create a list with the face Ids of faces detected in image
            foreach (Face_RootObject faceRO in face_RootObject)
            {
                //Debug.Log($"Detected face object: {faceRO.ToString()}");
                addFaceRectangle(faceRO);
                facesIdList.Add(faceRO.faceId);
            }

            //// Identify faces if faces were found
            if (facesIdList.Count != 0)
            {
                StartCoroutine(IdentifyFaces(facesIdList));
            }
        }
    }

    /// <summary>
    /// Creating face rectangle and calculating depth (z) by using camera resolution ratios and unprojection of vectors for use in world space.
    /// Additionally adds a world anchor to the face rectangle to increase stability while a user moves around a distance greater than a few meters.
    /// </summary>
    public void addFaceRectangle(Face_RootObject faceRO)
    {

        /**********************************************************************
        * BEGIN: Pixel to application-specified coordinate system: https://docs.microsoft.com/en-us/windows/mixed-reality/locatable-camera#pixel-to-application-specified-coordinate-system
        */
        float width = faceRO.faceRectangle.width / (float)this.cameraResolution.width; //pixel ratio of face rectangle and camera resolution
        float height = faceRO.faceRectangle.height / (float)this.cameraResolution.height;

        //// *** This equation was calculated using the pixel width of faces returned by the FaceAPI and comparing them across measured distances in increments ***
        //// equation: y = 494.94x^-1.06
        ////    y is the pixel length of one of the sides of the FaceRectangle
        ////    x is the distance
        //// x = (494.94/y)^(1/1.06)
        float estimatedDistance = Mathf.Pow((float)(494.94 / faceRO.faceRectangle.height), (float)(1 / 1.06)) / 2;
        //Debug.Log(string.Format("estimatedDistance: {0}", estimatedDistance));

        float halfRectangleX = faceRO.faceRectangle.width / 2;
        float halfRectangleY = faceRO.faceRectangle.height / 2;

        Vector2 ImagePosZeroToOne; //getting the pixel coordinates of face converted to camera positioning from 0 to 1
        ImagePosZeroToOne.x = (float)((faceRO.faceRectangle.left + halfRectangleX) / (float)this.cameraResolution.width);
        ImagePosZeroToOne.y = (float)(1.0 - ((faceRO.faceRectangle.top + halfRectangleY) / (float)this.cameraResolution.height));

        Vector2 ImagePosProjected;
        ImagePosProjected = (ImagePosZeroToOne * 2f) - (new Vector2(1, 1)); // -1 to 1 coordinate space

        Vector3 tmpVectorDistance; //image position of elements projected being multiplied (scaled) according to the estimated distance of face
        tmpVectorDistance.x = ImagePosProjected.x * estimatedDistance;
        tmpVectorDistance.y = ImagePosProjected.y * estimatedDistance;
        tmpVectorDistance.z = 1 * estimatedDistance;

        Vector3 DistanceSpacePos = UnProjectVector(projectionMatrix, tmpVectorDistance); //unprojected to fit into 3d space

        Vector3 worldSpaceDistancePoint;
        worldSpaceDistancePoint = this.cameraToWorldMatrix.MultiplyPoint3x4(DistanceSpacePos); // ray point for distance in world space
        /*
        * END: Pixel to application-specified coordinate system
        ******************************************************************/

        //// check the previous FaceRectangle positions to see if the newly created FaceRectangle is in relatively the same spot.
        //// if it is, destroy the previous faceRectangles and their world anchors
        //// *Duplicating dictionary to iterate over and edit the original without causing an issue modifying the original while iterating over it.
        Dictionary<string, Vector3> tmpFaceRectanglePosDict = new Dictionary<string, Vector3>(faceRectanglePositionsDict);
        foreach (KeyValuePair<string, Vector3> pair in tmpFaceRectanglePosDict)
        {
            Vector3 position = pair.Value;
            float distance = Vector3.Distance(worldSpaceDistancePoint, position);

            //// if the new face rectangle is too close, don't add it
            //Debug.LogFormat("{0}, {1} distance", pair.Key, distance);
            if (distance <= .4)
            {
                //Debug.LogFormat("{0} distance is too close! Removing old rectangle!", distance);
                //// if personID is already present, remove the prefab game object in the world and world anchor and remove it from the dictionary
                Destroy(GameObject.Find(pair.Key));
                WorldAnchorManager.Instance.RemoveAnchor(pair.Key);
                faceRectanglePositionsDict.Remove(pair.Key);
            }
        }

        //// create a FaceRectangle using the FramePrefab that is in the resources folder. Pull up unity to access and modify prefab
        GameObject faceRectangle = (GameObject)Instantiate(Resources.Load("FramePrefab"));
        faceRectangle.tag = "FaceRectangleTag";
        faceRectangle.transform.position = worldSpaceDistancePoint;
        faceRectangle.transform.rotation = this.cameraRotation;

        //// Adding the billboard component to try to get the face rectangles to always face the user. doesn't work. Don't know why.
        //Billboard billboardSetting = FaceRectangle.AddComponent<Billboard>();
        //billboardSetting.PivotAxis = PivotAxis.XY;

        //// The scale of the transform is relative to the parent. essentially controls the size/scale of the FaceRectangle
        Vector3 scale = this.pixelToCameraMatrix.MultiplyPoint3x4(new Vector3(width * estimatedDistance, height * estimatedDistance, 0));
        scale.z = .1f; //length of depth
        faceRectangle.transform.localScale = scale;
        faceRectangle.name = faceRO.faceId;

        //// Add the position to a dictionary to be checked against to avoid duplicate rectangles being made in the same position.
        faceRectanglePositionsDict.Add(faceRectangle.name, faceRectangle.transform.position);

        //// create the anchor position. Currently not saved acrossed sessions. Can be changed in unity really easily.
        WorldAnchorManager.Instance.AttachAnchor(faceRectangle, faceRectangle.name);
        //Debug.Log("### Created face rectangle ###");
    }

    /// <summary>
    /// Finds all rectangles using the tag: FaceRectangleTag and destroys them
    /// </summary>
    public void DestroyAllFaceRectangles()
    {
        var faceRectangleClones = GameObject.FindGameObjectsWithTag("FaceRectangleTag");
        if(faceRectangleClones.Length > 0)
        {
            foreach (var faceRectangleClone in faceRectangleClones)
            {
                Destroy(faceRectangleClone);
            }
        }
    }

    /// <summary>
    /// Iterates through a list of Face_RootObjects and adds a FaceRectangle to each object
    /// </summary>
    /// <param name="face_RootObjects"></param>
    public void createFaceRectangles(List<Face_RootObject> face_RootObjects)
    {
        foreach (Face_RootObject faceRO in face_RootObjects)
        {
            //Debug.Log($"Detected face object: {faceRO.ToString()}");
            //facesIdList.Add(faceRO.faceId);
            addFaceRectangle(faceRO);
        }
    }

    /// <summary>
    /// Identify the faces found in the image within the person group. The API returns personID of faces
    /// that were able to be identified.
    /// </summary>
    internal IEnumerator IdentifyFaces(List<string> listOfFacesIdToIdentify)
    {
        //// Create the object hosting the faces to identify
        FacesToIdentify_RootObject facesToIdentify = new FacesToIdentify_RootObject();
        facesToIdentify.faceIds = new List<string>();
        facesToIdentify.personGroupId = personGroupId;
        foreach (string facesId in listOfFacesIdToIdentify)
        {
            facesToIdentify.faceIds.Add(facesId);
        }

        facesToIdentify.maxNumOfCandidatesReturned = MAX_NUM_POSSIBLE_CANDIDATES_RETURNED;
        facesToIdentify.confidenceThreshold = CONFIDENCE_THRESHOLD;

        //// Serialise to Json format
        string facesToIdentifyJson = JsonConvert.SerializeObject(facesToIdentify);
        //// Change the object into a bytes array
        byte[] facesData = Encoding.UTF8.GetBytes(facesToIdentifyJson);

        WWWForm webForm = new WWWForm();
        string detectFacesEndpoint = $"{baseEndpoint}identify";

        using (UnityWebRequest www = UnityWebRequest.Post(detectFacesEndpoint, webForm))
        {
            www.SetRequestHeader("Ocp-Apim-Subscription-Key", key);
            www.SetRequestHeader("Content-Type", "application/json");
            www.uploadHandler.contentType = "application/json";
            www.uploadHandler = new UploadHandlerRaw(facesData);
            www.downloadHandler = new DownloadHandlerBuffer();

            yield return www.SendWebRequest();
            string jsonResponse = www.downloadHandler.text;
            //Debug.Log($"Get Person - jsonResponse: {jsonResponse}");
            Candidate_RootObject[] candidate_RootObject = JsonConvert.DeserializeObject<Candidate_RootObject[]>(jsonResponse);

            //// For each face to identify that has been submitted, display its candidate
            foreach (Candidate_RootObject candidateRO in candidate_RootObject)
            {
                if (candidateRO.candidates.Count > 0) {
                    StartCoroutine(GetPerson(candidateRO.candidates[0].personId, candidateRO.faceId));
                }
                // Delay the next "GetPerson" call, so all faces candidate are displayed properly
                yield return new WaitForSeconds(2);
            }
        }
    }

    /// <summary>
    /// Provided a identified personId, retrieve the person information associated from the Face API resource
    /// </summary>
    internal IEnumerator GetPerson(string personId, string faceId = "")
    {
        var deserializationSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };

        string getGroupEndpoint = $"{baseEndpoint}persongroups/{personGroupId}/persons/{personId}?";
        WWWForm webForm = new WWWForm();

        using (UnityWebRequest www = UnityWebRequest.Get(getGroupEndpoint))
        {
            www.SetRequestHeader("Ocp-Apim-Subscription-Key", key);
            www.downloadHandler = new DownloadHandlerBuffer();
            yield return www.SendWebRequest();
            string jsonResponse = www.downloadHandler.text;

            //Debug.Log($"Get Person - jsonResponse: {jsonResponse}");
            IdentifiedPerson_RootObject identifiedPerson_RootObject = JsonConvert.DeserializeObject<IdentifiedPerson_RootObject>(jsonResponse, deserializationSettings);

            //// check to make sure the userData isn't null or an empty json string to avoid json parsing error
            if(identifiedPerson_RootObject.userData != null && identifiedPerson_RootObject.userData != "{}")
            {
                identifiedPerson_RootObject.userDataObjectList = JsonConvert.DeserializeObject<List<UserData>>(identifiedPerson_RootObject.userData, deserializationSettings);
            }

            //// if personID is already present, remove the old prefab game object (FaceRectangle and text) and associated world anchor
            if(GameObject.Find(identifiedPerson_RootObject.personId))
            {
                Destroy(GameObject.Find(identifiedPerson_RootObject.personId));
                WorldAnchorManager.Instance.RemoveAnchor(identifiedPerson_RootObject.personId);
                faceRectanglePositionsDict.Remove(faceId);
            }

            //// Rename FaceRectangle to be personId for identification if person was found
            GameObject personFaceRectangle = GameObject.Find(faceId);
            if (identifiedPerson_RootObject.personId != null)
            {
                personFaceRectangle.name = identifiedPerson_RootObject.personId;
                attachTextToFaceRectangle(identifiedPerson_RootObject, personFaceRectangle);

                //// if the world anchor exists with the face Id or with the person Id (in case of an update photo)
                //// delete the anchor and create a new anchor with the correct name of the person
                WorldAnchorManager.Instance.RemoveAnchor(faceId);
                WorldAnchorManager.Instance.AttachAnchor(personFaceRectangle, personFaceRectangle.name);

                //// add in the new personID instead with that position
                faceRectanglePositionsDict.Remove(faceId);
                faceRectanglePositionsDict.Add(personFaceRectangle.name, personFaceRectangle.transform.position);
            }
        }
    }

    /// <summary>
    /// Attaches the text information to the Face Rectangle game object that was returned from identifying the faces
    /// </summary>
    private void attachTextToFaceRectangle(IdentifiedPerson_RootObject identifiedPerson_RootObject, GameObject faceRectangle)
    {
        if (identifiedPerson_RootObject.name != "" || identifiedPerson_RootObject.name != null)
        {
            /* Create the text label for the identified person's name */
            GameObject emptyGameObject = new GameObject(); // Creating the empty parent object of the textmesh to allow easy text size manipulation for text clarity
            emptyGameObject.transform.parent = faceRectangle.transform;

            TextMesh personNameText;
            personNameText = emptyGameObject.AddComponent<TextMesh>();
            personNameText.transform.parent = emptyGameObject.transform;

            personNameText.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f); // Resize and position the new label. very small initial scale for text clarity
            personNameText.transform.position = faceRectangle.transform.position;
            personNameText.transform.rotation = this.cameraRotation;
            personNameText.transform.Translate(new Vector3(0, faceRectangle.transform.localScale.y * 1.25f)); //moves the text up 1 unit in that scale

            personNameText.anchor = TextAnchor.LowerCenter;
            personNameText.alignment = TextAlignment.Center;
            personNameText.tabSize = 4;
            personNameText.fontSize = 500; //increase fontsize from initial very small scale for better text clarity

            personNameText.text = identifiedPerson_RootObject.name;

            /* Create the text label for the identified person's additional data */
            GameObject emptyGameObject2 = new GameObject(); // Creating the empty parent object of the textmesh
            emptyGameObject2.transform.parent = faceRectangle.transform;

            TextMesh personInfoText;
            personInfoText = emptyGameObject2.AddComponent<TextMesh>();
            personInfoText.transform.parent = emptyGameObject2.transform;

            personInfoText.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f); // Resize and position the new label. very small initial scale for text clarity
            personInfoText.transform.position = faceRectangle.transform.position;
            personInfoText.transform.rotation = this.cameraRotation;
            personInfoText.transform.Translate(new Vector3(faceRectangle.transform.localScale.x * 1.25f, faceRectangle.transform.localScale.y * 1.25f)); //moves the text up 1 unit in that scale

            personInfoText.anchor = TextAnchor.UpperLeft;
            personInfoText.alignment = TextAlignment.Left;
            personInfoText.tabSize = 4;
            personInfoText.fontSize = 500; //increase fontsize from initial very small scale for better text clarity

            // Display the information of the person in the UI
            if(identifiedPerson_RootObject.userDataObjectList != null)
            {
                foreach (UserData userData in identifiedPerson_RootObject.userDataObjectList)
                {
                    if (userData != null)
                    {
                        //// Added the \n at the end to provide a new line for each new data entry
                        personInfoText.text += userData.UserDataLabel + ": " + userData.UserDataValue + "\n";
                    }

                }
            }
        }
    }

    /*
    Additional information on unprojecting and projecting: https://docs.microsoft.com/en-us/windows/mixed-reality/locatable-camera#pixel-to-application-specified-coordinate-system
    */
    public static Vector3 UnProjectVector(Matrix4x4 proj, Vector3 to)
    {
        Vector3 from = new Vector3(0, 0, 0);
        var axsX = proj.GetRow(0);
        var axsY = proj.GetRow(1);
        var axsZ = proj.GetRow(2);
        from.z = to.z / axsZ.z;
        from.y = (to.y - (from.z * axsY.z)) / axsY.y;
        from.x = (to.x - (from.z * axsX.z)) / axsX.x;
        return from;
    }
}

/// <summary>
/// The Person Group object
/// </summary>
public class Group_RootObject
{
    public string personGroupId { get; set; }
    public string name { get; set; }
}

/// <summary>
/// The Person Face object
/// </summary>
public class Face_RootObject
{
    public string faceId { get; set; }
    public FaceRectangle faceRectangle { get; set; }
}

/// <summary>
/// Collection of faces that needs to be identified
/// </summary>
public class FacesToIdentify_RootObject
{
    public string personGroupId { get; set; }
    public List<string> faceIds { get; set; }
    public int maxNumOfCandidatesReturned { get; set; }
    public double confidenceThreshold { get; set; }
}

/// <summary>
/// Collection of Candidates for the face
/// </summary>
public class Candidate_RootObject
{
    public string faceId { get; set; }
    public List<Candidate> candidates { get; set; }
}

/// <summary>
/// Possible identified person
/// </summary>
public class Candidate
{
    public string personId { get; set; }
    public double confidence { get; set; }
}

/// <summary>
/// Name and Id of the identified Person
/// userData is originally returned as a string from the json. It then has to be parsed again to have it created into
/// a list of UserData objects.
/// </summary>
public class IdentifiedPerson_RootObject
{
    public string personId { get; set; }
    public string name { get; set; }
    public string userData { get; set; }
    public List<UserData> userDataObjectList { get; set; }
}

/// <summary>
/// Data struct used to keep track of user data that is returned
/// </summary>
public class UserData
{
    public string UserDataLabel { get; set; }
    public string UserDataValue { get; set; }
}

/// <summary>
/// FaceRectangle class to create objects
/// </summary>
public class FaceRectangle
{
    public int top { get; set; }
    public int left { get; set; }
    public int width { get; set; }
    public int height { get; set; }
}


