# HoloRekognition
HoloRekognition is a Microsoft HoloLens Facial identification and recognition application. It is an academic project planned and developed in the Winter of 2019 by the following individuals as a part of the capstone requirement for the BYU Marriott School of Business MISM Program: 

* [Levi Bowser](https://github.com/LeviBowser)
* [Nathan Barton](https://github.com/nbarton915)
* [Cameron Spilker](https://github.com/CameronSpilker)

Additional backend utilities were created through this capstone to interact with Azure Face API:

* [CustomPersonMaker](https://github.com/nbarton915/CustomPersonMaker): Created As Convenience Utility For Integrating Project HoloRekognition With Azure Face Resource - Create, train, fetch, update, and delete (people and groups)
* [Person Group Python Utility](https://github.com/CameronSpilker/DownloadPersonGroupInformation): Created As Convenience Utility For Integrating Project HoloRekognition And Azure Face Resource - Download Person Group Information from your Azure Face Resource

There are two applications for the project that can be used **Single_Photo_HoloRekog** and **Face_Tracking_HoloRekog**.

## Single_Photo_HoloRekog
An application that uses the tap gesture to take a photo and then locate/identify and recognize faces in the photo using Microsoft's Azure Face API. After a face is located, a 3D face rectangle is placed around the individuals face. If the face is recognized, any information that was connected to the face is displayed around the face rectangle. Please keep in mind that each photo taken places **TWO** api calls to the Face API. One for location/identification and another for face recognition.

Distance was estimated using approximations of face rectangle size; i.e. the smaller the face, the larger the estimated distance will be.

Face management and resource creation was done using the [CustomPersonMaker](https://github.com/nbarton915/CustomPersonMaker) and [Person Group Python](https://github.com/CameronSpilker/DownloadPersonGroupInformation) utilities.

## Face_Tracking_HoloRekog
An application that tracks faces by processing individual frames. It creates face rectangles using the [FaceDetector](https://docs.microsoft.com/en-us/uwp/api/Windows.Media.FaceAnalysis.FaceDetector#Windows_Media_FaceAnalysis_FaceDetector_CreateAsync) class, as well as a plugins whose links were documented in the code, to move or create face rectangles that are updated every few frames.

This application works, but not very well.

### **Please note: This was *not* the main focus of this project and was essentially abandoned halfway through. The code is neither clean nor readable. It has comments throughout, testing variables still in place, and chunks of code commented out. A large portion of the codes commented out were efforts to integrate *Single_Photo_HoloRekog* in real-time. Issues with threading and API call management, limited time, compounded with limitied knowledge, required focus to shift back to the *Single_Photo_HoloRekog* and the utilities that were being developed.**


# Development Environment Walk-through
*The following is a quick walk-through for convenience. It goes through setting up the requirements to edit, deploy, and run the applications. This project is currently **NOT** being maintained. Please take care, as there are no guarantees for the projects.*

# Objective(s)
* To run the application for face recognition, the development environment must have an up-to-date version of visual studio (community addition is fine), Unity (community/personal use edition is fine) and then HoloToolkit plugin attached to Unity.
* Please note that the instructions for starting a new project from scratch and having it work on the Hololens require additional steps. Namely, setting the scenes, the SDK, and build structure. Although a little dated, this tutorial will get you on the right track.
    * [https://docs.microsoft.com/en-us/windows/mixed-reality/holograms-100](https://docs.microsoft.com/en-us/windows/mixed-reality/holograms-100)
    * [https://circuitstream.com/hololens-unity-setup/](https://circuitstream.com/hololens-unity-setup/ )

# Prerequisite(s) 
* Windows 10
* Time

# Visual Studio
1. **Windows 10** is required.
1. Download Visual Studio Community **(2017 version is required)**
    1. [Visual Studio](https://developer.microsoft.com/en-us/windows/downloads)
1. Step through the install wizard. It may take some time for the install to complete.
1. A pop up window will appear, if you don’t want to create an account just click **Not now, maybe later**
1. Click **Start Visual Studio**
1. Also, you will want to make sure you have the **Windows 10 SDK**. It should come with the latest version of Visual Studio if your windows machine is updated fully. But go ahead and download and install from the link below:
    1. [Windows 10 SDK](https://developer.microsoft.com/en-us/windows/downloads/windows-10-sdk)
    1. Click **Download Installer**
![Download Installer](/images/sdk install.png)
    1. Start the installer that you just downloaded
    1. Make sure you check the box **.NET Framework 4.7.2 Software Development Kit**
![.NET Framework 4.7.2 Software Development Kit](/images/check boxes.png)
    1. Click **Download**
    1. You may have to start the installer by finding the executable file if a window pops up telling you to do so. There’s a bug that sometimes tells you to re-download and start the installer again by finding the executable file. Go ahead and run the executable, and if you already have the SDK installed, it will tell you.
![Download complete](/images/sdk exe.png)
    1. To verify you have the SDK installed, check your Programs and Features (where you go to uninstall programs not used) and scroll to the bottom. It should be there labeled as Windows Software Development Kit - Windows 10.*.
![Verify complete](/images/verify sdk.png)
1. **Done**


# Unity
1. Install **Unity** v. 2018.3.2
    1. [Download Unity](https://unity3d.com/get-unity/download/archive)
        1. This is an earlier version that still allows the use of the .NET 4 Framework and isn’t deprecated
    1. **Download (Win)** -> **Unity Installer**
    1. Run the executable you just downloaded
    1. As you’re installing Unity, make sure to have the following components selected as you’re installing (Depending on the version the names may be different):
        1. UWP Build Support (.NET) *or* Windows Store .NET Scripting Backend 
        1. UWP Build Support (IL2CPP) *or* Windows Store IL2CPP Scripting Backend
        1. Windows Build Support (IL2CPP)
![Unity Build](/images/unity build checkbox.png)
    1. Click *Install*
    1. Leave defaults. Click *Next*
    1. Accept Terms. Click *Next*
    1. Wait for Install to finish!
1. After having everything set up, you should just be able to open Unity, create an account, and open the project folder.
    1. Navigate to the parent folder: **HoloRekognition**
    1. Select the project folder of the application to open: **Single_Photo_HoloRekog**
1. To rebuild the application in Unity:
    1. Go up to the top menu: **File** -> **Build Settings…**
    1. We now have to set up the build settings to work with Visual Studio and Universal Windows Platform. In the new window Build Settings Window that popped up:
        1. Under Platform, select **Universal Windows Platform**
        1. Click the button: **Switch Platform**
        1. Verify the Target Device is set to **HoloLens**
        1. Verify that under **Debugging**, that **Unity C# Projects** is checked
        1. Click the button: **Player Settings…**
![Player Settings](/images/build-settings.png)
        1. When the Inspector pops up for PlayerSettings, look under **Other Settings** -> **Configuration**
            1. Verify **Scripting Runtime Version** is set to **.Net 4.x Equivalent**
            1. Verify **Scripting Backend** is set to **.Net**
            1. Verify **Api Compatibility level** is set to **.Net 4.x**
![Configuration Settings](/images/configuration-settings.png)
        1. In the Inspector, look under **XR Settings**
            1. Verify **Virtual Reality Supported** is checked
![XR Settings](/images/xr-settings.png)
    1. In the Build Settings window that popped up previously, click: **Build**
    1. This will open a folder selection window at the parent folder that you used to open the project in Unity
        1. If there isn’t an **App** folder, create one.
        1. Select the **App** folder (highlight it, don’t go into it)
        1. Click **Select Folder**
        1. Wait for the build to finish
1. **Done**. You can then navigate to the App folder and open the .sln file using Visual Studio.


# HoloToolkit (a.k.a. Mixed Reality Toolkit (MRTK))

*NOTE: With this project, MRTK shouldn’t be needed as the project already includes the HoloToolKit library. If for some reason it is needed, these instructions are provided.*
*Additional reference: [Getting Started with the MRTK](https://github.com/Microsoft/MixedRealityToolkit-Unity/blob/mrtk_release/Documentation/GettingStartedWithTheMRTK.md )*
1. Download the **package** that you will need to import into Unity
    1. [Download version HoloToolkit 2017.4.2.0](https://github.com/Microsoft/MixedRealityToolkit-Unity/releases)
1. Import HoloToolkit into Unity
    1. Open the project in **Unity** you want the plugin to be a part of
    1. On the menu bar: **Assets** -> **Import Package** -> **Custom Package**
    1. Select the HoloToolKit package from where you downloaded/unpacked it
    1. In the window that pops up, **select all** the folders/content
    1. Click **Import**
    1. Click **Apply**
1. You will have to rebuild the project in Unity for the plugin to be added


# Setting Up the Project
*NOTE: This part assumes you have set up the Cognitive Service for the **Face API**, have an **API key** to the service, and have a **person group ID** that was created in the service.*

To open the project in Visual Studio, go to the projects parent folder that that contains the App, Assets, Library,... folders. Go into the App folder and select the .sln (solution) file inside. If there is no **App** folder, then the project needs to be opened in Unity, **App** folder created, and project built in the **App** folder.

When the solution loads, open **FaceAnalysis.cs** and scroll down to the constants that are being initialized. The API authentication key will go in the **key** string constant. The person group ID will go in the **personGroupId** string constant.

Now go up to the top menu: **Build** -> **Build Solution**


# Setting Up the Project
When deploying to the Hololens via Visual studio and USB cord, make sure to click the dropdown arrow for the green 'play' triangle button and select **Device**. Additionally, the field to the left should read "**x86**", not "x64" or "ARM." With the Hololens on, click the green 'play' button and the project will be built, deployed, and started on the Hololens.
![Visual Studio Build](/images/visual-studio-build-button.png)

After the project has been deployed once onto the hololens from Visual Studio, the app will be selectable under **All Apps** on the Hololens Home screen and the application will be labeled as **SinglePhoto_HoloRekognition**.

# Summary
You should now have your development environment set up. You will be able to run the HoloLens facial recognition applications.


