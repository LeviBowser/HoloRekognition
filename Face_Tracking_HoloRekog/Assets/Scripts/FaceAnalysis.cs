using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using UnityEngine.Networking;
using UnityEngine.XR.WSA.WebCam;
using uVector3 = UnityEngine.Vector3;
using wVector3 = System.Numerics.Vector3;
using wMatrix4x4 = System.Numerics.Matrix4x4;
using System.Runtime.InteropServices;
using UnityEngine.XR.WSA;
using HoloToolkit.Unity;
using UnityEngine.XR.WSA.Persistence;


public class FaceAnalysis : MonoBehaviour {



    //Use this for initialization
    //void Start () {
    //}

    //// Update is called once per frame
    //void Update () {

    //}


    /// <summary>
    /// Allows this class to behave like a singleton
    /// </summary>
    public static FaceAnalysis Instance;

    /// <summary>
    /// Bytes of the image captured with camera
    /// </summary>
    internal byte[] imageBytes;

    /// <summary>
    /// Path of the image captured with camera
    /// </summary>
    internal string imagePath;

    /// <summary>
    /// The resolution of the camera that was used to capture the image
    /// </summary>
    internal Resolution cameraResolution;

    internal uVector3 cameraPosition;
    internal Quaternion cameraRotation;

    internal Matrix4x4 cameraToWorldMatrix;
    internal Matrix4x4 pixelToCameraMatrix;
    internal Matrix4x4 projectionMatrix;

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
    /// Initialises this class
    /// </summary>
    private void Awake()
    {
        // Allows this instance to behave like a singleton
        Instance = this;

        // Add the ImageCapture Class to this Game Object
        gameObject.AddComponent<ImageCapture>();

    }


    /// <summary>
    /// Creating face rectangle and calculating depth (z) by using camera resolution ratios and unprojection of vectors for use in world space.
    /// Additionally adds a world anchor to the face rectangle to increase stability while a user moves around a distance greater than a few meters.
    /// </summary>
    public void addFaceRectangle(Face_RootObject faceRO)
    {

        float width = faceRO.faceRectangle.width / (float)this.cameraResolution.width; //pixel ratio of face rectangle and camera resolution
        float height = faceRO.faceRectangle.height / (float)this.cameraResolution.height;

        GameObject faceRectangle = (GameObject)Instantiate(Resources.Load("FramePrefab"));
        faceRectangle.tag = "FaceRectangleTag";
        faceRectangle.name = faceRO.faceId;
        //faceRectangle.transform.position = worldSpaceDistancePoint;
        //faceRectangle.transform.position = testWorldPixelCoords;
        faceRectangle.transform.position = faceRO.faceRectangle.position;
        faceRectangle.transform.rotation = this.cameraRotation;

        //The scale of the transform relative to the parent. essentially controls the size/scale of the faceRectangle
        //Vector3 scale = this.pixelToCameraMatrix.MultiplyPoint3x4(new Vector3(width * estimatedDistance, height * estimatedDistance, 0));
        //Vector3 scale = this.pixelToCameraMatrix.MultiplyPoint3x4(new Vector3(width, height, 0)); //used for testing.
        Vector3 scale = new Vector3(width, width, 0); //used for frame processing
        scale.z = .1f; //length of depth
        faceRectangle.transform.localScale = scale;

        faceRO.FaceRectangleGameObject = faceRectangle;
        //Debug.Log("### Created face rectangle ###");

        //// world anchor commented out because issues with world anchor relation to rectangles during frame processing
        //WorldAnchorManager.Instance.AttachAnchor(faceRectangle, faceRectangle.name);

        //Debug.Log(string.Format("{0} camera width", this.cameraResolution.width));
        //Debug.Log(string.Format("{0} camera height", this.cameraResolution.height));
    }

    public void DestroyAllFaceRectangles()
    {
        try
        {
            var faceRectangleClones = GameObject.FindGameObjectsWithTag("FaceRectangleTag");
            if (faceRectangleClones.Length > 0)
            {
                foreach (var faceRectangleClone in faceRectangleClones)
                {
                    Destroy(faceRectangleClone);
                }
            }
        }
        catch (System.Exception)
        {
            Debug.Log("IN the destroy all face rectangles catch");
            throw;
        }
        
    }

    public void createFaceRectangles(List<Face_RootObject> face_RootObjects)
    {
        foreach (Face_RootObject faceRO in face_RootObjects)
        {
            if(faceRO.FaceRectangleGameObject == null)
            {
                //Debug.Log($"Detected face object: {faceRO.ToString()}");
                //facesIdList.Add(faceRO.faceId);
                addFaceRectangle(faceRO);
            }
        }
    }


    /// <summary>
    /// Detect faces from a submitted image
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
            Debug.Log($"====== JSON Response: {jsonResponse}");

            List<string> facesIdList = new List<string>();

            // Create a list with the face Ids of faces detected in image
            foreach (Face_RootObject faceRO in face_RootObject)
            {
                Debug.Log($"Detected face object: {faceRO.ToString()}");
                facesIdList.Add(faceRO.faceId);

                addFaceRectangle(faceRO);
            }

            if (facesIdList.Count != 0)
            {
                StartCoroutine(IdentifyFaces(facesIdList));
            }
        }
    }

    /// <summary>
    /// Detect faces from a submitted image
    /// </summary>
    internal IEnumerator DetectFacesFromBitmapByteArray(byte[] bitmapImageBytes)
    {
        WWWForm webForm = new WWWForm();
        string detectFacesEndpoint = $"{baseEndpoint}detect";

        using (UnityWebRequest www =
            UnityWebRequest.Post(detectFacesEndpoint, webForm))
        {
            www.SetRequestHeader("Ocp-Apim-Subscription-Key", key);
            www.SetRequestHeader("Content-Type", "application/octet-stream");
            www.uploadHandler.contentType = "application/octet-stream";
            www.uploadHandler = new UploadHandlerRaw(bitmapImageBytes);
            www.downloadHandler = new DownloadHandlerBuffer();

            yield return www.SendWebRequest();
            string jsonResponse = www.downloadHandler.text;
            Face_RootObject[] face_RootObject =
                JsonConvert.DeserializeObject<Face_RootObject[]>(jsonResponse);
            Debug.Log($"====== JSON Response: {jsonResponse}");

            List<string> facesIdList = new List<string>();

            // Create a list with the face Ids of faces detected in image
            foreach (Face_RootObject faceRO in face_RootObject)
            {
                Debug.Log($"Detected face object: {faceRO.ToString()}");
                facesIdList.Add(faceRO.faceId);

                addFaceRectangle(faceRO);
            }

            if (facesIdList.Count != 0)
            {
                StartCoroutine(IdentifyFaces(facesIdList));
            }
        }
    }

    /// <summary>
    /// Identify the faces found in the image within the person group
    /// </summary>
    internal IEnumerator IdentifyFaces(List<string> listOfFacesIdToIdentify)
    {
        // Create the object hosting the faces to identify
        FacesToIdentify_RootObject facesToIdentify = new FacesToIdentify_RootObject();
        facesToIdentify.faceIds = new List<string>();
        facesToIdentify.personGroupId = personGroupId;
        foreach (string facesId in listOfFacesIdToIdentify)
        {
            facesToIdentify.faceIds.Add(facesId);
        }
        facesToIdentify.maxNumOfCandidatesReturned = 2;
        facesToIdentify.confidenceThreshold = 0.5;

        // Serialise to Json format
        string facesToIdentifyJson = JsonConvert.SerializeObject(facesToIdentify);
        // Change the object into a bytes array
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
            Debug.Log($"Get Person - jsonResponse: {jsonResponse}");
            Candidate_RootObject[] candidate_RootObject = JsonConvert.DeserializeObject<Candidate_RootObject[]>(jsonResponse);

            //For each face to identify that has been submitted, display its candidate
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
    /// Provided a personId, retrieve the person name associated with it
    /// </summary>
    internal IEnumerator GetPerson(string personId, string faceId = "")
    {
        string getGroupEndpoint = $"{baseEndpoint}persongroups/{personGroupId}/persons/{personId}?";
        WWWForm webForm = new WWWForm();

        using (UnityWebRequest www = UnityWebRequest.Get(getGroupEndpoint))
        {
            www.SetRequestHeader("Ocp-Apim-Subscription-Key", key);
            www.downloadHandler = new DownloadHandlerBuffer();
            yield return www.SendWebRequest();
            string jsonResponse = www.downloadHandler.text;

            Debug.Log($"Get Person - jsonResponse: {jsonResponse}");
            IdentifiedPerson_RootObject identifiedPerson_RootObject = JsonConvert.DeserializeObject<IdentifiedPerson_RootObject>(jsonResponse);

            identifiedPerson_RootObject.userDataObjectList = JsonConvert.DeserializeObject<List<UserData>>(identifiedPerson_RootObject.userData);

            // if personID is already present, remove the prefab and rename the new one
            if (GameObject.Find(identifiedPerson_RootObject.personId))
            {
                Destroy(GameObject.Find(identifiedPerson_RootObject.personId));
                //WorldAnchorManager.Instance.RemoveAnchor(identifiedPerson_RootObject.personId);
            }

            // Rename faceRectangle to be personId for identification
            GameObject personFaceRectangle = GameObject.Find(faceId);
            personFaceRectangle.name = identifiedPerson_RootObject.personId;

            attachTextToFaceRectangle(identifiedPerson_RootObject, personFaceRectangle);

            // if the world anchor exists with the face Id or with the person Id (in case of an update photo)
            //     delete the anchor and create a new anchor with the correct name of the person
            //WorldAnchorManager.Instance.RemoveAnchor(faceId);
            //WorldAnchorManager.Instance.AttachAnchor(personFaceRectangle, personFaceRectangle.name);
        }
    }

    /// <summary>
    /// Attaches the text information to the Face Rectangle game object that was returned from identifying the faces
    /// </summary>
    private void attachTextToFaceRectangle(IdentifiedPerson_RootObject identifiedPerson_RootObject, GameObject faceRectangle)
    {
        if (identifiedPerson_RootObject.name != "" || identifiedPerson_RootObject.name != null)
        {
            GameObject emptyGameObject = new GameObject(); // Creating the empty parent object of the textmesh
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

            // Display the name of the person in the UI
            foreach (UserData userData in identifiedPerson_RootObject.userDataObjectList)
            {
                personInfoText.text += userData.UserDataLabel + ": " + userData.UserDataValue + "\n";
            }
        }
    }

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
    public faceRectangle faceRectangle { get; set; }
    public GameObject FaceRectangleGameObject { get; set; }
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
    public string personId { get; set; }
    public List<Candidate> candidates { get; set; }
}

public class Candidate
{
    public string personId { get; set; }
    public double confidence { get; set; }
}

/// <summary>
/// Name and Id of the identified Person
/// </summary>
public class IdentifiedPerson_RootObject
{
    public string personId { get; set; }
    public string name { get; set; }
    public string userData { get; set; }
    public List<UserData> userDataObjectList { get; set; }
}

public class UserData
{
    public string UserDataLabel { get; set; }
    public string UserDataValue { get; set; }
}


public class faceRectangle
{
    public int top { get; set; }
    public int left { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    public Vector3 position { get; set; }
}


