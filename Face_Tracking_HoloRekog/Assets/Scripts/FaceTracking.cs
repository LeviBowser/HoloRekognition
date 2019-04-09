using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading.Tasks;
using HoloLensCameraStream;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine.Networking;
using Newtonsoft.Json;

#if !UNITY_EDITOR
using Windows.Storage.Streams;
using Windows.Media.FaceAnalysis;
using Windows.Graphics.Imaging;
#endif


public class FaceTracking : MonoBehaviour
{

    //    /// <summary>
    //    /// allows this to act like a singleton
    //    /// </summary>
    public FaceTracking Instance;

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

#if !UNITY_EDITOR

    /// <summary>
    /// References a FaceDetector instance.
    /// </summary>
    private FaceDetector faceDetector;

#endif
    HoloLensCameraStream.Resolution _resolution;
    VideoCapture _videoCapture;
    IntPtr _spatialCoordinateSystemPtr;
    byte[] _latestImageBytes;

    GameObject[] previousFrameRectanglesGO;

    List<Face_RootObject> recognizedFaceRootObjectList;
    List<Face_RootObject> faceIdFaceRootObjectList;
    List<Face_RootObject> noFaceIdFaceRootObjectList;
    List<Face_RootObject> detectedFaceRootObjectList;

    //////https://github.com/EnoxSoftware/HoloLensWithOpenCVForUnityExample/blob/master/Assets/HoloLensWithOpenCVForUnityExample/HoloLensFaceDetectionOverlayExample/HoloLensFaceDetectionOverlayExample.cs
    System.Object sync = new System.Object();

    //private static int isDetecting = 0;
    //private static int isFindFaceBoxes = 0;
    //private static int isDetectingFaceId = 0;
    //private static int isUpdating = 0;
    //private static int isUpdatingRectangles = 0;
    //private static int isProcessingFrame = 0;

    bool _isDetecting = false;
    bool isDetecting
    {
        get
        {
            lock (sync)
                return _isDetecting;
        }
        set
        {
            lock (sync)
                _isDetecting = value;
        }
    }

    bool _isFindFaceBoxes = false;
    bool isFindFaceBoxes
    {
        get
        {
            lock (sync)
                return _isFindFaceBoxes;
        }
        set
        {
            lock (sync)
                _isFindFaceBoxes = value;
        }
    }

    bool _isDetectingFaceId = false;
    bool isDetectingFaceId
    {
        get
        {
            lock (sync)
                return _isDetectingFaceId;
        }
        set
        {
            lock (sync)
                _isDetectingFaceId = value;
        }
    }

    bool _isUpdating = false;
    bool isUpdating
    {
        get
        {
            lock (sync)
                return _isUpdating;
        }
        set
        {
            lock (sync)
                _isUpdating = value;
        }
    }

    bool _isUpdatingRectangles = false;
    bool isUpdatingRectangles
    {
        get
        {
            lock (sync)
                return _isUpdatingRectangles;
        }
        set
        {
            lock (sync)
                _isUpdatingRectangles = value;
        }
    }

    bool _isProcessingFrame = false;
    bool isProcessingFrame
    {
        get
        {
            lock (sync)
                return _isProcessingFrame;
        }
        set
        {
            lock (sync)
                _isProcessingFrame = value;
        }
    }



    private class VideoFrameStruct
    {
        public float[] cameraToWorldFloatMatrix, projectionFloatMatrix;
        public byte[] data;
        public int frameWidth;
        public int frameHeight;
        public CapturePixelFormat pixelFormat;
    }


    //Start is called before the first frame update
    void Start()
    {

#if !UNITY_EDITOR
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif
        Instance = this;

        _spatialCoordinateSystemPtr = UnityEngine.XR.WSA.WorldManager.GetNativeISpatialCoordinateSystemPtr();

        //Call this in Start() to ensure that the CameraStreamHelper is already "Awake".
        CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);
        init_faceTracker();
        recognizedFaceRootObjectList = new List<Face_RootObject>();
        faceIdFaceRootObjectList = new List<Face_RootObject>();
        noFaceIdFaceRootObjectList = new List<Face_RootObject>();
    }

    ////https://stackoverflow.com/questions/41330771/use-unity-api-from-another-thread-or-call-a-function-in-the-main-thread/41333540#41333540
    void Awake()
    {
        //// requird to be in the Awake() to make sure it runs. This coordinates the UI unity thread and the calculation thread.
        UnityThread.initUnityThread();
    }

    // Update is called once per frame
    //void Update()
    //{

    //}

    private async void init_faceTracker()
    {
#if !UNITY_EDITOR

        if (this.faceDetector == null)
        {
            this.faceDetector = await FaceDetector.CreateAsync();
            Debug.Log("faceDetector started.");
        }
#endif
    }

    void OnVideoCaptureCreated(VideoCapture videoCapture)
    {
        if (videoCapture == null)
        {
            Debug.LogError("Did not find a video capture object. You may not be using the HoloLens.");
            return;
        }

        this._videoCapture = videoCapture;

        //Request the spatial coordinate ptr if you want fetch the camera and set it if you need to 
        CameraStreamHelper.Instance.SetNativeISpatialCoordinateSystemPtr(_spatialCoordinateSystemPtr);

        _resolution = CameraStreamHelper.Instance.GetLowestResolution();
        UnityEngine.Resolution resForAnalysis = new UnityEngine.Resolution();
        resForAnalysis.height = _resolution.height;
        resForAnalysis.width = _resolution.width;
        FaceAnalysis.Instance.cameraResolution = resForAnalysis;

        float frameRate = CameraStreamHelper.Instance.GetHighestFrameRate(_resolution);
        videoCapture.FrameSampleAcquired += OnFrameSampleAcquired;

        //You don't need to set all of these params.
        //I'm just adding them to show you that they exist.
        CameraParameters cameraParams = new CameraParameters();
        cameraParams.cameraResolutionHeight = _resolution.height;
        cameraParams.cameraResolutionWidth = _resolution.width;
        cameraParams.frameRate = Mathf.RoundToInt(frameRate);
        cameraParams.pixelFormat = CapturePixelFormat.BGRA32;
        cameraParams.rotateImage180Degrees = true; //If your image is upside down, remove this line.
        cameraParams.enableHolograms = false;

        Debug.Log("Configuring camera: " + _resolution.width + "x" + _resolution.height + " | " + cameraParams.pixelFormat);
        videoCapture.StartVideoModeAsync(cameraParams, OnVideoModeStarted);
    }

    void OnVideoModeStarted(VideoCaptureResult result)
    {
        if (result.success == false)
        {
            Debug.LogWarning("Could not start video mode.");
            return;
        }
        Debug.Log("Video capture started.");
    }


    //long frameCounter = 0;
    void OnFrameSampleAcquired(VideoCaptureSample sample)
    {
        ////#### sample description
        //// MediaFrameReference frameReference: private member
        //// internal spatialCoordinateSystem worldOrigin: internal member
        //// bitmap = frameReference.VideoMediaFrame.SoftwareBitmap;
        //// FrameWidth = bitmap.PixelWidth;
        //// FrameHeight = bitmap.PixelHeight;
        //// _latestImageBytes is a byte[] buffer from the bitmap

        //frameCounter += 1;
        //Debug.Log("Frame counter: " + frameCounter);
        if (sample != null)
        {

            if (isProcessingFrame)
            {
                //Debug.Log("OnFrameSampleAcquired: Still detecting : Dropping frame");
                sample.Dispose();
                return;
            }

            isProcessingFrame = true;
            //When copying the bytes out of the buffer, you must supply a byte[] that is appropriately sized.
            //You can reuse this byte[] until you need to resize it (for whatever reason).
            if (_latestImageBytes == null || _latestImageBytes.Length < sample.dataLength)
            {
                _latestImageBytes = new byte[sample.dataLength];
            }
            sample.CopyRawImageDataIntoBuffer(_latestImageBytes);
            //Debug.Log("Got frame: " + sample.FrameWidth + "x" + sample.FrameHeight + " | " + sample.pixelFormat);

            //fill frame struct
            VideoFrameStruct videoFrameStruct = new VideoFrameStruct();
            videoFrameStruct.data = _latestImageBytes;
            videoFrameStruct.frameWidth = sample.FrameWidth;
            videoFrameStruct.frameHeight = sample.FrameHeight;
            videoFrameStruct.pixelFormat = sample.pixelFormat;

            // Get the cameraToWorldMatrix and projectionMatrix, if either fails, exit function
            if (!sample.TryGetCameraToWorldMatrix(out videoFrameStruct.cameraToWorldFloatMatrix) || !sample.TryGetProjectionMatrix(out videoFrameStruct.projectionFloatMatrix))
                return;

            sample.Dispose();

            ////works, but is slow and causes framerate to drop to noticably low levels. (10 fps-ish)
            Task.Run(() => ProcessVideoFrameStructAsync(videoFrameStruct));
            return;
        }
        
        sample.Dispose();
        return;
    }

    
    //long framesProcessed = 0;
    private async Task ProcessVideoFrameStructAsync(VideoFrameStruct videoFrameStruct)
    {
#if !UNITY_EDITOR
        SoftwareBitmap tmpSoftwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, videoFrameStruct.frameWidth, videoFrameStruct.frameHeight);
        SoftwareBitmap nvSoftwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Nv12, videoFrameStruct.frameWidth, videoFrameStruct.frameHeight);
        tmpSoftwareBitmap.CopyFromBuffer(videoFrameStruct.data.AsBuffer());

        //// converts the software bitmap to NV12 to allow faceDetector to work
        nvSoftwareBitmap = SoftwareBitmap.Convert(tmpSoftwareBitmap, BitmapPixelFormat.Nv12);

        IList<DetectedFace> detectedFaces = null;
        detectedFaces = await faceDetector.DetectFacesAsync(nvSoftwareBitmap);

        //framesProcessed += 1;
        //Debug.Log("Frames Processed: " + framesProcessed);
        if (detectedFaces.Count > 0)
        {
            if (!isUpdating)
            {
                isUpdating = true;
                //// creates Face_RootObject, and adds faceRectangles with their position for each facebox, to the list detectedFaceRootObjectList
                detectedFaceRootObjectList = new List<Face_RootObject>();
                this.CreateDetectedFaceBoxes(videoFrameStruct, detectedFaces, detectedFaceRootObjectList);


                //// using noFaceIdFaceRootObjectList need to get cropped copies of the face 
                //// and store those to be able to be used to identify the faces and get a faceID from azure to then get a person id from azure
                //// for each face root object without a faceId, they are sent up to Azure to get one and their faceRoot_object.faceID is updated.
                //// Every new face that enters the frame should make an api call.

                ////https://docs.microsoft.com/en-us/dotnet/api/system.threading.interlocked?view=netframework-4.7.2
                //if (0 == Interlocked.Exchange(ref isDetectingFaceId, 1) && noFaceIdFaceRootObjectList.Count == 1)
                //if (!isDetectingFaceId && noFaceIdFaceRootObjectList.Count == 1)
                //{
                //    //Debug.LogFormat("{0} acquired the lock", Thread.CurrentThread.ManagedThreadId);
                //    isDetectingFaceId = true;

                //    ////have to duplicate list to make sure i don't edit the list while iterating through it.
                //    List<Face_RootObject> tmpNoFaceIdFaceRootObjectList = new List<Face_RootObject>();
                //    foreach (Face_RootObject face_RootObject in noFaceIdFaceRootObjectList)
                //    {
                //        if (face_RootObject != null)
                //        {
                //            tmpNoFaceIdFaceRootObjectList.Add(face_RootObject);
                //        }
                //    }

                //    foreach (Face_RootObject face_RootObject in tmpNoFaceIdFaceRootObjectList)
                //    {
                //        noFaceIdFaceRootObjectList.Remove(face_RootObject);
                //        if ((face_RootObject.faceId == null || face_RootObject.faceId == ""))
                //        {
                //            Debug.LogFormat("Trying to get a faceID for {0}.", face_RootObject);
                //            //DetectFaceIdAsync(tmpSoftwareBitmap, face_RootObject);
                //            //// is detectingFaceID becomes false at the end of the API called in DetectFaceIdAsync
                //        }
                //        else if (face_RootObject.faceId != null || face_RootObject.faceId != "")
                //        {
                //            Debug.LogFormat("{0} already has a faceId: {1}. Skipping detecting faceID. Adding it to faceIdFaceRootObjectList.", face_RootObject, face_RootObject.faceId);
                //            faceIdFaceRootObjectList.Add(face_RootObject);
                //        }
                //    }
                //    //Interlocked.Exchange(ref isDetectingFaceId, 0);
                //    isDetectingFaceId = false;
                //}
                //else
                //{
                //    Debug.LogFormat("{0} denied the lock", Thread.CurrentThread.ManagedThreadId);
                //}
                //// create faceRectangles from Face_RootObjects that did not have faceIDs and did not have an update that needed to be made
                if (detectedFaceRootObjectList.Count > 0)
                {
                    UnityThread.executeInUpdate(() => FaceAnalysis.Instance.createFaceRectangles(detectedFaceRootObjectList));
                }
            }
        }
        else
        {
            UnityThread.executeInUpdate(() => FaceAnalysis.Instance.DestroyAllFaceRectangles());
        }
        isProcessingFrame = false;
#endif
    }

#if !UNITY_EDITOR
    private async void DetectFaceIdAsync(SoftwareBitmap tmpSoftwareBitmap, Face_RootObject face_RootObject)
    {
        byte[] jpgByteArray = null;
        SoftwareBitmap tmp_faceSoftwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, face_RootObject.faceRectangle.width, face_RootObject.faceRectangle.height);

        //// converts and crops the software bitmap to be jpeg encoded to be able to be sent to the faceAPI
        tmp_faceSoftwareBitmap = await CreateFaceCropBitMap(tmpSoftwareBitmap, face_RootObject);
        jpgByteArray = await EncodeToJpgByteArray(tmp_faceSoftwareBitmap);

        UnityThread.executeCoroutine(DetectFaceIDFromBitmapByteArray(jpgByteArray, face_RootObject));
    }
#endif


#if !UNITY_EDITOR
    /// <summary>
    /// https://stackoverflow.com/questions/49244618/how-to-create-a-softwarebitmap-from-region-of-another-softwarebitmap-uwp
    /// </summary>
    /// <param name = "softwareBitmap" ></ param >
    /// < param name="rect"></param>
    /// <returns></returns>
    private async Task<SoftwareBitmap> CreateFaceCropBitMap(SoftwareBitmap softwareBitmap, Face_RootObject face_RootObject)
    {
        uint tmp_height = 0;
        uint tmp_width = 0;

        //// azure api requires certain size of image
        if (face_RootObject.faceRectangle.height < 50)
        {
            tmp_height = 50;
        } else
        {
            tmp_height = (uint)face_RootObject.faceRectangle.height;
        }

        //// azure api requires certain size of image
        if (face_RootObject.faceRectangle.width < 50)
        {
            tmp_width = 50;
        }
        else
        {
            tmp_width = (uint)face_RootObject.faceRectangle.width;
        }

        using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
        {
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);

            encoder.SetSoftwareBitmap(softwareBitmap);
            encoder.BitmapTransform.Bounds = new BitmapBounds()
            {
                X = (uint)face_RootObject.faceRectangle.left,
                Y = (uint)face_RootObject.faceRectangle.top,
                Height = tmp_height,
                Width = tmp_width
            };

            await encoder.FlushAsync();

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            return await decoder.GetSoftwareBitmapAsync(softwareBitmap.BitmapPixelFormat, softwareBitmap.BitmapAlphaMode);
        }
    }
#endif

#if !UNITY_EDITOR
    /// <summary>
    /// https://social.msdn.microsoft.com/Forums/Lync/en-US/328984f6-3891-46ab-b0da-d22f17826c18/uwp-encode-writeablebitmap-to-jpeg-byte-array?forum=wpdevelop
    /// </summary>
    /// <param name="softwareBitmap"></param>
    /// <param name="rect"></param>
    /// <returns></returns>
    private async Task<SoftwareBitmap> ConvertToJPGBitmap(SoftwareBitmap softwareBitmap)
    {
        using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
        {
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
            encoder.SetSoftwareBitmap(softwareBitmap);

            await encoder.FlushAsync();

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            return await decoder.GetSoftwareBitmapAsync(softwareBitmap.BitmapPixelFormat, softwareBitmap.BitmapAlphaMode);
        }
    }
#endif

#if !UNITY_EDITOR
    private async Task<byte[]> EncodeToJpgByteArray(SoftwareBitmap softwareBitmap)
    {
        byte[] array = null;

        using (var ms = new InMemoryRandomAccessStream())
        {
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, ms);
            encoder.SetSoftwareBitmap(softwareBitmap);

            try
            {
                await encoder.FlushAsync();
            }
            catch { }

            array = new byte[ms.Size];
            await ms.ReadAsync(array.AsBuffer(), (uint)ms.Size, InputStreamOptions.None);
        }
        return array;
    }
#endif

    void UpdateExistingRectanglePositions(List<Face_RootObject> detectedFaceRootObjectList)
    {
        if (!isUpdatingRectangles)
        {
            isUpdatingRectangles = true;
            previousFrameRectanglesGO = GameObject.FindGameObjectsWithTag("FaceRectangleTag");
            //Debug.Log("Updating existing rectangle positions...");
            foreach (GameObject previousRectangle in previousFrameRectanglesGO)
            {

                Face_RootObject closestFace = null;
                foreach (Face_RootObject detectedFace in detectedFaceRootObjectList)
                {
                    float distance = Mathf.Infinity;
                    Vector3 diff = detectedFace.faceRectangle.position - previousRectangle.transform.position;
                    float curDistance = diff.sqrMagnitude;
                    if (curDistance < distance)
                    {
                        closestFace = detectedFace;
                        distance = curDistance;
                    }
                }

                if (closestFace != null)
                {
                    //// update rectangle from previous frame to replace the closest face rectangle from current frame to avoid having to delete faceRectangles and recreate new objects
                    //// then remove the faceRectangle that was replaced in the current frame to avoid recreating a new faceRectangle in the same position
                    //Debug.LogFormat("Updating to closest face");

                    //// make sure the previous rectangle position moved/transformed
                    previousRectangle.transform.position = closestFace.faceRectangle.position;

                    //// make sure the closestFace face rectangle value is updated to the new value
                    closestFace.faceRectangle.position = previousRectangle.transform.position;
                    
                    //// make the newly updated previousRectangle game object related to the rootObject that it updated.
                    closestFace.FaceRectangleGameObject = previousRectangle;
                    closestFace.faceId = closestFace.FaceRectangleGameObject.name;

                    //// if the faceRectangleObject has had a name given to it (a faceID) add it to faceIdFaceRootObjectList
                    //if (closestFace.FaceRectangleGameObject != null && closestFace.FaceRectangleGameObject.name != null && closestFace.FaceRectangleGameObject.name != "")
                    //{
                    //    Debug.LogFormat("Adding {0} to faceIdFaceRootObjectList with FaceRectangleGameObject.name: {1}.", closestFace, closestFace.FaceRectangleGameObject.name);
                    //    faceIdFaceRootObjectList.Add(closestFace);
                    //}
                    //else
                    //{
                    //    if (noFaceIdFaceRootObjectList.Count < 1)
                    //    {
                    //        Debug.LogFormat("ELSE: Adding {0} to noFaceIdFaceRootObjectList", closestFace);
                    //        noFaceIdFaceRootObjectList.Add(closestFace);
                    //    }
                    //    else
                    //    {
                    //        Debug.LogFormat("NOT Adding {0} to noFaceIdFaceRootObjectList. Count too high: {1}", closestFace, noFaceIdFaceRootObjectList.Count);
                    //    }
                    //}

                    //Debug.LogFormat("Removing index {0} with position {1}", closestFaceIndex, detectedFaceRootObjectList[closestFaceIndex].faceRectangle.position);
                    //// remove the closest rootobject to prevent duplicate rectangles
                    detectedFaceRootObjectList.Remove(closestFace);
                }
            }
            isUpdatingRectangles = false;
        }
        isUpdating = false;
    }


#if !UNITY_EDITOR
    /// <summary>
    /// Takes the webcam image and FaceTracker results and assembles the visualization onto the Canvas.
    /// </summary>
    /// <param name="framePizelSize">Width and height (in pixels) of the video capture frame</param>
    /// <param name="foundFaces">List of detected faces; output from FaceTracker</param>
    private void CreateDetectedFaceBoxes(VideoFrameStruct videoFrameStruct, IList<DetectedFace> foundFaces, List<Face_RootObject> detectedFaceRootObjectList)
    {
        if (foundFaces != null)
        {
            //Debug.Log("creating face boxes");

            foreach (DetectedFace face in foundFaces)
            {
                Face_RootObject face_RootObject = new Face_RootObject();

                faceRectangle tmpFaceRectangle = new faceRectangle();
                tmpFaceRectangle.top = (int)face.FaceBox.Y;
                tmpFaceRectangle.left = (int)face.FaceBox.X;
                tmpFaceRectangle.width = (int)face.FaceBox.Width;
                tmpFaceRectangle.height = (int)face.FaceBox.Height;

                face_RootObject.faceRectangle = tmpFaceRectangle;
                face_RootObject.faceRectangle.position = createFaceRectanglePosition(face_RootObject, videoFrameStruct);

                //// add the newly detected faceRootObject to a list to be processed and later get faceID if the exact same position has not been added
                detectedFaceRootObjectList.Add(face_RootObject);
            }
        }

        //// issue when video mode starts while looking at a person. creates duplicate rectangles.
        //// updates all previous rectangles to position of closest new rectangles.
        //Action updateExistingRectanglePositions = UpdateExistingRectanglePositions(detectedFaceRootObjectList);
        //UnityThread.executeInUpdate(updateExistingRectanglePositions);
        UnityThread.executeInUpdate(() => UpdateExistingRectanglePositions(detectedFaceRootObjectList));
    }
#endif

    private Vector3 createFaceRectanglePosition(Face_RootObject faceRO, VideoFrameStruct videoFrameStruct)
    {
        Matrix4x4 projectionMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(videoFrameStruct.projectionFloatMatrix);
        Matrix4x4 cameraToWorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(videoFrameStruct.cameraToWorldFloatMatrix);

        FaceAnalysis.Instance.cameraPosition = cameraToWorldMatrix.MultiplyPoint3x4(new Vector3(0, 0, -1)); ;
        FaceAnalysis.Instance.cameraRotation = Quaternion.LookRotation(-cameraToWorldMatrix.GetColumn(2), cameraToWorldMatrix.GetColumn(1));
        FaceAnalysis.Instance.cameraToWorldMatrix = cameraToWorldMatrix;
        FaceAnalysis.Instance.projectionMatrix = projectionMatrix;
        FaceAnalysis.Instance.pixelToCameraMatrix = projectionMatrix.inverse;

        /**********************************************************************
            * BEGIN: Pixel to application-specified coordinate system: https://docs.microsoft.com/en-us/windows/mixed-reality/locatable-camera#pixel-to-application-specified-coordinate-system
            */

        //float width = faceRO.faceRectangle.width / (float)_resolution.width; //pixel ratio of face rectangle and camera resolution
        //float height = faceRO.faceRectangle.height / (float)_resolution.height;

        float width = faceRO.faceRectangle.width / (float)videoFrameStruct.frameWidth; //pixel ratio of face rectangle and camera resolution
        float height = faceRO.faceRectangle.height / (float)videoFrameStruct.frameHeight;

        //equation: y = 494.94x^-1.06
        //    y is the pixel length of one of the sides of the faceRectangle
        //    x is the distance
        // x = (494.94/y)^(1/1.06)
        float estimatedDistance = (Mathf.Pow((float)(494.94 / faceRO.faceRectangle.height), (float)(1 / 1.06)) / 2) - (float)1.5;
        //Debug.Log(string.Format("estimatedDistance: {0}", estimatedDistance));

        float halfRectangleX = faceRO.faceRectangle.width / 2;
        float halfRectangleY = faceRO.faceRectangle.height / 2;

        Vector2 ImagePosZeroToOne; //getting the pixel coordinates of face converted to camera positioning from 0 to 1
        ImagePosZeroToOne.x = (float)((faceRO.faceRectangle.left + halfRectangleX) / (float)videoFrameStruct.frameWidth);
        ImagePosZeroToOne.y = (float)(1.0 - ((faceRO.faceRectangle.top + halfRectangleY) / (float)videoFrameStruct.frameHeight));

        Vector2 ImagePosProjected;
        ImagePosProjected = (ImagePosZeroToOne * 2f) - (new Vector2(1, 1)); // -1 to 1 space

        Vector3 worldSpaceRayPoint1;
        Vector4 tmpVector4 = new Vector4(0f, 0f, 0f);
        worldSpaceRayPoint1 = cameraToWorldMatrix.MultiplyPoint3x4(tmpVector4); //camera location in world space

        Vector3 tmpVectorDistance; //image position of elements projected being multiplied (scaled) according to the estimated distance of face
        tmpVectorDistance.x = ImagePosProjected.x * estimatedDistance;
        tmpVectorDistance.y = ImagePosProjected.y * estimatedDistance;
        tmpVectorDistance.z = 1 * estimatedDistance;

        Vector3 DistanceSpacePos = UnProjectVector(projectionMatrix, tmpVectorDistance); //unprojected to fit into 3d space

        Vector3 worldSpaceDistancePoint;
        worldSpaceDistancePoint = cameraToWorldMatrix.MultiplyPoint3x4(DistanceSpacePos); // ray point for distance in world space
        /*
            * END: Pixel to application-specified coordinate system
            ******************************************************************/

        //Debug.Log("returning createFaceRectanglePosition.");
        return worldSpaceDistancePoint;
    }

    


    /// <summary>
    /// Detect faces from a submitted image
    /// </summary>
    int apiCallCount = 0;
    internal IEnumerator DetectFaceIDFromBitmapByteArray(byte[] bitmapImageBytes, Face_RootObject face_RootObject)
    {
        
        WWWForm webForm = new WWWForm();
        string detectFacesEndpoint = $"{baseEndpoint}detect";
        apiCallCount += 1;
        Debug.LogFormat("DetectFaceID Api call count: {0}", apiCallCount);

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
            Debug.Log($"====== JSON Response: {jsonResponse}");

            Face_RootObject[] returned_face_RootObject =
                JsonConvert.DeserializeObject<Face_RootObject[]>(jsonResponse);
            

            //// use the returned json response to assign the faceID to its face_rootObject
            foreach (Face_RootObject returnedFace in returned_face_RootObject)
            {
                face_RootObject.faceId = returnedFace.faceId;
                faceIdFaceRootObjectList.Add(face_RootObject);
                //try
                //{
                //    noFaceIdFaceRootObjectList.Remove(face_RootObject);
                //}
                //catch (Exception)
                //{
                //    throw;
                //}
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

    private void OnDestroy()
    {
        if (_videoCapture != null)
        {
            _videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
            _videoCapture.Dispose();
        }
    }

}

