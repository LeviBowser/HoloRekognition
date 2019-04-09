using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEngine.XR.WSA.Input;
using UnityEngine.XR.WSA.WebCam;
using uVector3 = UnityEngine.Vector3;
using wVector3 = System.Numerics.Vector3;
using wMatrix4x4 = System.Numerics.Matrix4x4;

public class ImageCapture : MonoBehaviour {

    /// <summary>
    /// Allows this class to behave like a singleton
    /// </summary>
    public static ImageCapture instance;

    /// <summary>
    /// Keeps track of tapCounts to name the captured images 
    /// </summary>
    private int tapsCount;

    /// <summary>
    /// PhotoCapture object used to capture images on HoloLens 
    /// </summary>
    private PhotoCapture photoCaptureObject = null;


    private Vector3 cameraPosition;
    private Quaternion cameraRotation;

    /// <summary>
    /// HoloLens class to capture user gestures
    /// </summary>
    private GestureRecognizer recognizer;
    // Use this for initialization
 //   void Start () {
		
	//}
	
	// Update is called once per frame
	void Update () {
		
	}

    /// <summary>
    /// Initialises this class
    /// </summary>
    private void Awake()
    {
        instance = this;
    }

    /// <summary>
    /// Called right after Awake
    /// </summary>
    void Start()
    {
        // Initialises user gestures capture 
        recognizer = new GestureRecognizer();
        recognizer.SetRecognizableGestures(GestureSettings.Tap);
        recognizer.Tapped += TapHandler;
        recognizer.StartCapturingGestures();
    }

    /// <summary>
    /// Respond to Tap Input.
    /// </summary>
    private void TapHandler(TappedEventArgs obj)
    {
        tapsCount++;
        ExecuteImageCaptureAndAnalysis();
    }

    /// <summary>
    /// Begin process of Image Capturing and send To Azure Computer Vision service.
    /// </summary>
    private void ExecuteImageCaptureAndAnalysis()
    {
        Resolution cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending
            ((res) => res.width * res.height).First();
        //Resolution cameraResolution = PhotoCapture.SupportedResolutions.FirstOrDefault();
        FaceAnalysis.Instance.cameraResolution = cameraResolution;
        Texture2D targetTexture = new Texture2D(cameraResolution.width, cameraResolution.height);

        PhotoCapture.CreateAsync(false, delegate (PhotoCapture captureObject)
        {
            photoCaptureObject = captureObject;

            CameraParameters c = new CameraParameters();
            c.hologramOpacity = 0.0f;
            c.cameraResolutionWidth = targetTexture.width;
            c.cameraResolutionHeight = targetTexture.height;
            c.pixelFormat = CapturePixelFormat.JPEG;

            captureObject.StartPhotoModeAsync(c, delegate (PhotoCapture.PhotoCaptureResult result)
            {
                string filename = string.Format(@"CapturedImage{0}.jpg", tapsCount);
                string filePath = Path.Combine(Application.persistentDataPath, filename);

                // Set the image path on the FaceAnalysis class
                FaceAnalysis.Instance.imagePath = filePath;

                //photoCaptureObject.TakePhotoAsync
                //(filePath, PhotoCaptureFileOutputFormat.JPG, OnCapturedPhotoToDisk);

                photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
            });
        });
    }

    /// <summary>
    /// Called right after the photo capture process has concluded
    /// </summary>
    void OnCapturedPhotoToDisk(PhotoCapture.PhotoCaptureResult result)
    {
        photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
    }

    /// <summary>
    /// Called right after the photo capture process has concluded
    /// </summary>
    void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
    {
        Debug.Log("photo captured");
        List<byte> imageBufferList = new List<byte>();
        // Copy the raw IMFMediaBuffer data into our empty byte list.
        photoCaptureFrame.CopyRawImageDataIntoBuffer(imageBufferList);

        FaceAnalysis.Instance.imageBytes = imageBufferList.ToArray();

        var cameraToWorldMatrix = new Matrix4x4();
        photoCaptureFrame.TryGetCameraToWorldMatrix(out cameraToWorldMatrix);

        cameraPosition = cameraToWorldMatrix.MultiplyPoint3x4(new Vector3(0, 0, -1));
        cameraRotation = Quaternion.LookRotation(-cameraToWorldMatrix.GetColumn(2), cameraToWorldMatrix.GetColumn(1));

        FaceAnalysis.Instance.cameraPosition = cameraPosition;
        FaceAnalysis.Instance.cameraRotation = cameraRotation;

        Matrix4x4 projectionMatrix;
        photoCaptureFrame.TryGetProjectionMatrix(Camera.main.nearClipPlane, Camera.main.farClipPlane, out projectionMatrix);
        Matrix4x4 pixelToCameraMatrix = projectionMatrix.inverse;

        FaceAnalysis.Instance.projectionMatrix = projectionMatrix;
        FaceAnalysis.Instance.cameraToWorldMatrix = cameraToWorldMatrix;
        FaceAnalysis.Instance.pixelToCameraMatrix = pixelToCameraMatrix;


        photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
    }

    /// <summary>
    /// Register the full execution of the Photo Capture. If successfull, it will begin the Image Analysis process.
    /// </summary>
    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        photoCaptureObject.Dispose();
        photoCaptureObject = null;

        // Request image capture analysis
        StartCoroutine(FaceAnalysis.Instance.DetectFacesFromImage());
    }
}
