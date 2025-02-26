﻿//MIT License
//Copyright 2016-Present 
//Ross Tredinnick
//Benny Wysong-Grass
//University of Wisconsin - Madison Virtual Environments Group
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
//to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, 
//sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
//INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Rendering;
using System;
using System.Runtime.InteropServices;
using System.Reflection;
using static UnityEngine.Camera;
using System.Collections.Specialized;







#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEditor;
#endif

namespace UniCAVE
{
    struct Settings
    {
        //public PhysicalDisplayManager manager;

        [UnityEngine.Serialization.FormerlySerializedAs("machineName")]
        public string oldMachineName;
        public MachineName machineNameAsset;
        public string machineName => MachineName.GetMachineName(oldMachineName, machineNameAsset);
        public bool convergenceAtScreen;
        public float convdistance;
        public float width;
        public float height;
        public float halfWidth() => width * 0.5f;
        public float halfHeight() => height * 0.5f;
        public HeadConfiguration head;

        public bool useXRCameras;
        public bool useRenderTextures;
        public Vector2Int renderTextureSize;

        public bool is3D;
        public bool exclusiveFullscreen;
        public int display;
        public bool dualPipe;
        public bool dualInstance;
        public RectInt windowBounds;
        public RectInt leftViewport;
        public RectInt rightViewport;
    }

    [Serializable]
    public class PhysicalDisplay : MonoBehaviour
    {
        //public PhysicalDisplayManager manager;

        [UnityEngine.Serialization.FormerlySerializedAs("machineName")]
        public string oldMachineName;
        public MachineName machineNameAsset;
        public string machineName => MachineName.GetMachineName(oldMachineName, machineNameAsset);
        public bool convergenceAtScreen;
        public float convdistance;
        public float width;
        public float height;
        public float halfWidth() => width * 0.5f;
        public float halfHeight() => height * 0.5f;
        public HeadConfiguration head;

        public bool useXRCameras;
        public bool useRenderTextures;
        public Vector2Int renderTextureSize;

        public bool is3D;
        public bool exclusiveFullscreen;
        public int display;
        [Tooltip("Does the display use a dual pipe setup?")]
        public bool dualPipe;
        [Tooltip("Use one instance of Unity for each eye?")]
        public bool dualInstance;
        [Tooltip("Where the window will be positioned on the screen")]
        public RectInt windowBounds;
        public RectInt leftViewport;
        public RectInt rightViewport;

        public bool loadSettingsAtRuntime;

        private bool updatedViewports = false;

        /// <summary>
        /// Camera for rendering stereo left eye
        /// </summary>
        [NonSerialized]
        public Camera leftCam;

        /// <summary>
        /// Camera for rendering non-stereo eye or passive-stereo left and right
        /// </summary>
        [NonSerialized]
        public Camera centerCam;

        /// <summary>
        /// Camera for rendering stereo right eye
        /// </summary>
        [NonSerialized]
        public Camera rightCam;

        /// <summary>
        /// If post processing is used, the texture rendered to stereo left
        /// </summary>
        [NonSerialized]
        public RenderTexture leftTex;

        /// <summary>
        /// If post processing is used, the texture rendered to non-stereo
        /// </summary>
        [NonSerialized]
        public RenderTexture centerTex;

        /// <summary>
        /// If post processing is used, the texture rendered to stereo right
        /// </summary>
        [NonSerialized]
        public RenderTexture rightTex;

        /// <summary>
        /// Whether all setup operations are complete
        /// </summary>
        [NonSerialized]
        bool initialized = false;

        /// <summary>
        /// Whether all setup operations are complete
        /// </summary>
        /// <returns>initialized</returns>
        public bool Initialized()
        {
            return initialized;
        }

        /// <summary>
        /// Whether or not this display is enabled for this machine.
        /// If has manager, cedes decision to the manager
        /// When in editor, always true.
        /// </summary>
        /// <returns>Whether to enable this display</returns>


        /// <summary>
        /// A list of every camera associated with this display
        /// </summary>
        /// <returns>A list of every camera associated with this display
        /// </summary></returns>
        public List<Camera> GetAllCameras()
        {
            List<Camera> res = new List<Camera>();
            if (centerCam != null) res.Add(centerCam);
            if (leftCam != null) res.Add(leftCam);
            if (rightCam != null) res.Add(rightCam);
            return res;
        }

        /// <summary>
        /// Convert a coordinate in screenspace of this display to worldspace
        /// </summary>
        /// <param name="ss">Screenspace coordinate in range {[-1,1], [-1,1]} </param>
        /// <returns>The world space position of ss</returns>
        public Vector3 ScreenspaceToWorld(Vector2 ss)
        {
            return transform.localToWorldMatrix * new Vector4(ss.x * halfWidth(), ss.y * halfHeight(), 0.0f, 1.0f);
        }

        /// <summary>
        /// Upper right (quadrant 1) corner world space coordinate
        /// </summary>
        public Vector3 UpperRight
        {
            get
            {
                return transform.localToWorldMatrix * new Vector4(halfWidth(), halfHeight(), 0.0f, 1.0f);
            }
        }

        /// <summary>
        /// Upper left (quadrant 2) corner world space coordinate
        /// </summary>
        public Vector3 UpperLeft
        {
            get
            {
                return transform.localToWorldMatrix * new Vector4(-halfWidth(), halfHeight(), 0.0f, 1.0f);
            }
        }

        /// <summary>
        /// Lower left (quadrant 3) corner world space coordinate
        /// </summary>
        public Vector3 LowerLeft
        {
            get
            {
                return transform.localToWorldMatrix * new Vector4(-halfWidth(), -halfHeight(), 0.0f, 1.0f);
            }
        }

        /// <summary>
        /// Lower right (quadrant 4) corner world space coordinate
        /// </summary>
        public Vector3 LowerRight
        {
            get
            {
                return transform.localToWorldMatrix * new Vector4(halfWidth(), -halfHeight(), 0.0f, 1.0f);
            }
        }

        /// <summary>
        /// Generate a list of potential errors associated with this display, printed to console during runtime and shown in the script editor
        /// May include false positives and false negatives, this is just used to bring attention to common issues
        /// </summary>
        /// <returns>A list of potential script configuration errors</returns>
        public List<string> GetSettingsErrors()
        {
            List<string> errors = new List<string>();
            if (head == null)
            {
                errors.Add("Physical Display must have a reference to a GameObject with Head Configuration script!");
            }
            if (string.IsNullOrEmpty(machineName) == null)
            {
                errors.Add("Physical Display should probably have a non-empty machine name");
            }
            if (width <= 0 || height <= 0)
            {
                errors.Add("Physical Display has impossible dimensions");
            }
            if (exclusiveFullscreen && useRenderTextures)
            {
                errors.Add("Physical Display uses renderTextures but also uses a certain display");
            }
            if (exclusiveFullscreen && dualPipe)
            {
                errors.Add("Physical display is in dual pipe mode but also uses a certain display");
            }
            if (exclusiveFullscreen != null)
            {
                errors.Add("Physical display uses exlusive fullscreen, but is also managed");
            }
            if (dualInstance != null)
            {
                errors.Add("Physical Display has dual instance enabled, but the display is also managed");
            }

            if (useXRCameras && dualPipe)
            {
                errors.Add("Physical Display uses XR cameras but is also dual pipe");
            }
            if (useXRCameras && !is3D)
            {
                errors.Add("Physical Display uses XR cameras but is not 3D");
            }
            if (exclusiveFullscreen &&
                (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2 ||
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 ||
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore))
            {
                errors.Add("Physical Display uses a certain display but the Unity Player isn't using DirectX");
            }

            if (is3D && !dualPipe && (XRSettings.supportedDevices.Length < 1 || XRSettings.supportedDevices[0] != "stereo"))
            {
                errors.Add(gameObject.name + " expecting quad-buffered stereo but VR Supported - \"Stereo display non-head mounted\" not enabled.");
            }

            return errors;
        }

        /// <summary>
        /// Copies the values of a Settings object into this script instance
        /// </summary>
        /// <param name="settings">Settings to load in</param>
        void SetSettings(Settings settings)
        {
            //manager = settings.manager;
            head = settings.head;
            machineNameAsset = settings.machineNameAsset;
            width = settings.width;
            height = settings.height;
            convergenceAtScreen = settings.convergenceAtScreen;
            convdistance = settings.convdistance;
            useXRCameras = settings.useXRCameras;
            useRenderTextures = settings.useRenderTextures;
            renderTextureSize = settings.renderTextureSize;

            is3D = settings.is3D;
            exclusiveFullscreen = settings.exclusiveFullscreen;
            display = settings.display;
            dualPipe = settings.dualPipe;
            dualInstance = settings.dualInstance;
            windowBounds = settings.windowBounds;
            leftViewport = settings.leftViewport;
            rightViewport = settings.rightViewport;
        }

        /// <summary>
        /// Get a Settings object associated with the settings of this script instance
        /// </summary>
        /// <returns>Script settings</returns>
        Settings GetSettings()
        {
            Settings settings = new Settings();
            //settings.manager = manager;
            settings.head = head;
            settings.machineNameAsset = machineNameAsset;
            settings.width = width;
            settings.height = height;
            settings.convergenceAtScreen = convergenceAtScreen;
            settings.convdistance = convdistance;
            settings.useXRCameras = useXRCameras;
            settings.useRenderTextures = useRenderTextures;
            settings.renderTextureSize = renderTextureSize;

            settings.is3D = is3D;
            settings.exclusiveFullscreen = exclusiveFullscreen;
            settings.display = display;
            settings.dualPipe = dualPipe;
            settings.dualInstance = dualInstance;
            settings.windowBounds = windowBounds;
            settings.leftViewport = leftViewport;
            settings.rightViewport = rightViewport;
            return settings;
        }

        /// <summary>
        /// Disable this object if it shouldn't be active
        /// Otherwise, create and assign cameras, reposition window, etc
        /// </summary>
        void Start()
        {
            Cursor.visible = false;

            centerCam = null;
            leftCam = null;
            rightCam = null;

            if (loadSettingsAtRuntime)
            {
                Debug.Log("Attempting to Load Settings for Display: " + gameObject.name);
                TryToDeSerialize(serializedLocation);
            }

            Debug.Log("EXCLUSIVE: " + (exclusiveFullscreen ? "YES" : "NO"));



            Debug.Log("Display Active: " + gameObject.name);

            foreach (string er in GetSettingsErrors())
            {
                Debug.Log("Display Warning for Display: " + gameObject.name + ": " + er);
            }

            if (head == null)
            {
                Debug.Log("Display Error for Display: " + gameObject.name + ": Physical Display has no head object");
            }

            initialized = true;

            if (exclusiveFullscreen)
            {
                if (display < Display.displays.Length)
                {
                    Display.displays[display].Activate();
                }
                else
                {
                    Debug.Log("Display Error for Display: " + gameObject.name + ": Physical Display uses display index " + display + " but Unity does not detect that many displays");
                }
            }


            if (!exclusiveFullscreen)
            {
                Debug.Log("Setting Display: " + gameObject.name + " to Windowed Mode...");
                if (!is3D || useXRCameras)
                {
                    centerCam = head.CreateEye(HeadConfiguration.Eyes.Center, gameObject.name);
                    if (useXRCameras)
                    {
                        leftCam = head.CreateEye(HeadConfiguration.Eyes.Left, gameObject.name);
                        rightCam = head.CreateEye(HeadConfiguration.Eyes.Right, gameObject.name);
                        StereoBlit blit = centerCam.gameObject.AddComponent<StereoBlit>();
                        blit.lcam = leftCam;
                        blit.rcam = rightCam;
                        Debug.Log("Created dummy cams");
                    }
                    Debug.Log("Setting Display: " + gameObject.name + " to Non-3D Windowed");

                }
                else
                {
                    if (!dualPipe && !dualInstance)
                    {
                        leftCam = head.CreateEye(HeadConfiguration.Eyes.Left, gameObject.name);
                        rightCam = head.CreateEye(HeadConfiguration.Eyes.Right, gameObject.name);
                        Debug.Log("Setting Display: " + gameObject.name + " to Quad Buffer 3D Windowed");

                    }
                    else if (dualPipe && !dualInstance)
                    {
                        leftCam = head.CreateEye(HeadConfiguration.Eyes.Left, gameObject.name);
                        rightCam = head.CreateEye(HeadConfiguration.Eyes.Right, gameObject.name);
                        Debug.Log("Setting Display: " + gameObject.name + " to Dual-Eye Dual-Pipe-3D Windowed");

                    }
                    else if (dualPipe && dualInstance)
                    {
                        if (Util.GetArg("eye") == "left")
                        {
                            leftCam = head.CreateEye(HeadConfiguration.Eyes.Left, gameObject.name);
                            Debug.Log("Setting Display: " + gameObject.name + " to Left-Eye Dual-Pipe-3D Windowed");

                        }
                        else if (Util.GetArg("eye") == "right")
                        {
                            rightCam = head.CreateEye(HeadConfiguration.Eyes.Right, gameObject.name);
                            Debug.Log("Setting Display: " + gameObject.name + " to Right-Eye Dual-Pipe-3D Windowed");

                        }
                    }
                }
            }
            else
            {
                if (!is3D || useXRCameras)
                {
                    centerCam = head.CreateEye(HeadConfiguration.Eyes.Center, gameObject.name);
                    if (useXRCameras)
                    {
                        leftCam = head.CreateEye(HeadConfiguration.Eyes.Left, gameObject.name);
                        rightCam = head.CreateEye(HeadConfiguration.Eyes.Right, gameObject.name);
                        StereoBlit blit = centerCam.gameObject.AddComponent<StereoBlit>();
                        blit.lcam = leftCam;
                        blit.rcam = rightCam;
                        Debug.Log("Created dummy cams");
                    }
                }
                else
                {
                    if (!dualPipe && !dualInstance)
                    {
                        leftCam = head.CreateEye(HeadConfiguration.Eyes.Left, gameObject.name);
                        rightCam = head.CreateEye(HeadConfiguration.Eyes.Right, gameObject.name);
                    }
                    else if (dualPipe && !dualInstance)
                    {
                        leftCam = head.CreateEye(HeadConfiguration.Eyes.Left, gameObject.name);
                        rightCam = head.CreateEye(HeadConfiguration.Eyes.Right, gameObject.name);
                    }
                    else if (dualPipe && dualInstance)
                    {
                        if (Util.GetArg("eye") == "left")
                        {
                            leftCam = head.CreateEye(HeadConfiguration.Eyes.Left, gameObject.name);
                        }
                        else if (Util.GetArg("eye") == "right")
                        {
                            rightCam = head.CreateEye(HeadConfiguration.Eyes.Right, gameObject.name);
                        }
                    }
                }
            }

            if (useRenderTextures)
            {
                if (leftCam != null)
                {
                    leftTex = new RenderTexture(renderTextureSize.x, renderTextureSize.y, 0);
                    leftCam.targetTexture = leftTex;
                    leftTex.name = Util.ObjectFullName(leftCam.gameObject) + " Target Tex";
                }
                if (centerCam != null)
                {
                    centerTex = new RenderTexture(renderTextureSize.x, renderTextureSize.y, 0);
                    centerCam.targetTexture = centerTex;
                    centerTex.name = Util.ObjectFullName(centerCam.gameObject) + " Target Tex";
                }
                if (rightCam != null)
                {
                    rightTex = new RenderTexture(renderTextureSize.x, renderTextureSize.y, 0);
                    rightCam.targetTexture = rightTex;
                    rightTex.name = Util.ObjectFullName(rightCam.gameObject) + " Target Tex";
                }
            }

            if (exclusiveFullscreen)
            {

                if (leftCam != null)
                {

                    //Unwrapper left eye cams
                    if (gameObject.name == "-X")
                    {
                        leftCam.rect = new Rect(new Vector2(0, 0), new Vector2((float)1 / 4, 1));
                        leftCam.targetDisplay = display;
                    }
                    if (gameObject.name == "+Z")
                    {
                        leftCam.rect = new Rect(new Vector2((float)1 / 4, 0), new Vector2((float)1 / 4, 1));
                        leftCam.targetDisplay = display;
                    }
                    if (gameObject.name == "+X")
                    {
                        leftCam.rect = new Rect(new Vector2((float)1 / 2, 0), new Vector2((float)1 / 4, 1));
                        leftCam.targetDisplay = display;
                    }
                    if (gameObject.name == "-Y")
                    {
                        leftCam.rect = new Rect(new Vector2((float)3 / 4, 0), new Vector2((float)1 / 4, 1));
                        leftCam.targetDisplay = display;
                    }

                }
                if (centerCam != null)

                {

                    //Unwrapper center cams (when used)
                    if (gameObject.name == "-X")
                    {
                        centerCam.rect = new Rect(new Vector2(0, 0), new Vector2((float)1 / 4, 1));
                        centerCam.targetDisplay = display;
                    }
                    if (gameObject.name == "+Z")
                    {
                        centerCam.rect = new Rect(new Vector2((float)1 / 4, 0), new Vector2((float)1 / 4, 1));
                        centerCam.targetDisplay = display;
                    }
                    if (gameObject.name == "+X")
                    {
                        centerCam.rect = new Rect(new Vector2((float)1 / 2, 0), new Vector2((float)1 / 4, 1));
                        centerCam.targetDisplay = display;
                    }
                    if (gameObject.name == "-Y")
                    {
                        centerCam.rect = new Rect(new Vector2((float)3 / 4, 0), new Vector2((float)1 / 4, 1));
                        centerCam.targetDisplay = display;
                    }
                }
                if (rightCam != null)
                {
                    //Unwrapper right eye cams
                    if (gameObject.name == "-X")
                    {
                        rightCam.rect = new Rect(new Vector2(0, 0), new Vector2((float)1 / 4, 1));
                        rightCam.targetDisplay = display;
                    }
                    if (gameObject.name == "+Z")
                    {
                        rightCam.rect = new Rect(new Vector2((float)1 / 4, 0), new Vector2((float)1 / 4, 1));
                        rightCam.targetDisplay = display;
                    }
                    if (gameObject.name == "+X")
                    {
                        rightCam.rect = new Rect(new Vector2((float)1 / 2, 0), new Vector2((float)1 / 4, 1));
                        rightCam.targetDisplay = display;
                    }
                    if (gameObject.name == "-Y")
                    {
                        rightCam.rect = new Rect(new Vector2((float)3 / 4, 0), new Vector2((float)1 / 4, 1));
                        rightCam.targetDisplay = display;
                    }
                }
            }
#if UNITY_EDITOR
            if(rightCam == null && leftCam == null && centerCam == null)
            {
                if(!is3D || useXRCameras)
                {
                    centerCam = head.CreateEye(HeadConfiguration.Eyes.Center, gameObject.name);
                }
                else
                {
                    leftCam = head.CreateEye(HeadConfiguration.Eyes.Left, gameObject.name);
                    rightCam = head.CreateEye(HeadConfiguration.Eyes.Right, gameObject.name);
                }
            }
#endif
        }

        /// <summary>
        /// Assign camera matrices for all associated cameras
        /// </summary>
        private void LateUpdate()
        {
            if (leftCam != null)
            {
                //Orignal code has convergence at the screen. 
                if (!convergenceAtScreen)
                {
                    Vector3 difference = (leftCam.transform.position - rightCam.transform.position) * 0.5f;//inter eye distance /2
                    Vector3 vr = (LowerRight - LowerLeft).normalized;
                    Vector3 vu = (UpperLeft - LowerLeft).normalized;
                    Vector3 vn = Vector3.Cross(vr, vu).normalized;
                    Vector3 va = LowerLeft - leftCam.transform.position;
                    float d = Vector3.Dot(va, vn); // distance from eye to screen
                    Vector3 convdifference = (difference * (convdistance - d)) / convdistance;//Calculate the screen edge offset distance based on the concergence distance and the distance from the eye to the screen
                    Vector3 LowerLeftStereo = LowerLeft + convdifference;
                    Vector3 LowerRightStereo = LowerRight + convdifference;
                    Vector3 UpperLeftStereo = UpperLeft + convdifference;
                    Matrix4x4 leftMat = Util.getAsymProjMatrix(LowerLeftStereo, LowerRightStereo, UpperLeftStereo, leftCam.transform.position, head.nearClippingPlane, head.farClippingPlane);
                    // For Stereo mode 
                    leftCam.SetStereoProjectionMatrix(StereoscopicEye.Left, leftMat);
                    leftCam.stereoTargetEye = StereoTargetEyeMask.Left;
                    // For Non Stereo and stereo
                    leftCam.projectionMatrix = leftMat;
                    leftCam.transform.rotation = transform.rotation;

                }
                if (convergenceAtScreen) 
                {
                    Matrix4x4 leftMat = Util.getAsymProjMatrix(LowerLeft, LowerRight, UpperLeft, leftCam.transform.position, head.nearClippingPlane, head.farClippingPlane);
                    // For Stereo mode 
                    leftCam.SetStereoProjectionMatrix(StereoscopicEye.Left, leftMat);
                    leftCam.stereoTargetEye = StereoTargetEyeMask.Left;
                    // For Non Stereo and stereo
                    leftCam.projectionMatrix = leftMat;
                    leftCam.transform.rotation = transform.rotation;
                }

                //float convdistance = HeadConfiguration.convergenceDistance;


                //Vector3 LowerLeftStereo = LowerLeft + difference;//transform inter eye distance to screen to create parallel stereo; no convergence
                //Vector3 LowerRightStereo = LowerRight + difference;//transform inter eye distance to screen to create parallel stereo; no convergence
                //Vector3 UpperLeftStereo = UpperLeft + difference;//transform inter eye distance to screen to create parallel stereo; no convergence
                //compute orthonormal basis for the screen - could pre-compute this...

                //Debug.Log("Even een testje " + lowerLeft + fromm);
                //compute screen corner vectors

                //Vector3 vb = LowerRight - leftCam.transform.position;
                //Vector3 vc = UpperLeft - leftCam.transform.position;

                //find the distance from the eye to screen plane
                //float n = head.nearClippingPlane;
                //float f = head.farClippingPlane;

                //Debug.Log("testje " + d);



                
                
                //Debug.Log("Even een testje leftCam " + LowerLeft + difference + rightCam.transform.position + leftCam.transform.position);

            }

            if (rightCam != null)
            {
                if (!convergenceAtScreen)
                {
                    Vector3 difference2 = (leftCam.transform.position - rightCam.transform.position) * 0.5f;//inter eye distance /2
                    Vector3 vr2 = (LowerRight - LowerLeft).normalized;
                    Vector3 vu2 = (UpperLeft - LowerLeft).normalized;
                    Vector3 vn2 = Vector3.Cross(vr2, vu2).normalized;
                    Vector3 va2 = LowerLeft - leftCam.transform.position;
                    float d2 = Vector3.Dot(va2, vn2); // distance from eye to screen
                    Vector3 convdifference2 = (difference2 * (convdistance - d2)) / convdistance;//Calculate the screen edge offset distance based on the concergence distance and the distance from the eye to the screen
                    Vector3 LowerLeftStereo2 = LowerLeft - convdifference2;//offset screen edge
                    Vector3 LowerRightStereo2 = LowerRight - convdifference2;//offset screen edge
                    Vector3 UpperLeftStereo2 = UpperLeft - convdifference2;//offset screen edge
                    Matrix4x4 rightMat = Util.getAsymProjMatrix(LowerLeftStereo2, LowerRightStereo2, UpperLeftStereo2, rightCam.transform.position, head.nearClippingPlane, head.farClippingPlane);
                    // For Stereo mode 
                    rightCam.SetStereoProjectionMatrix(StereoscopicEye.Right, rightMat);
                    rightCam.stereoTargetEye = StereoTargetEyeMask.Right;
                    // For Non Stereo and stereo
                    rightCam.projectionMatrix = rightMat;
                    rightCam.transform.rotation = transform.rotation;
                    //Debug.Log("Even een testje rightCam " + LowerLeft + leftCam.transform.position );
                }
                if (convergenceAtScreen)
                {
                    Matrix4x4 rightMat = Util.getAsymProjMatrix(LowerLeft, LowerRight, UpperLeft, rightCam.transform.position, head.nearClippingPlane, head.farClippingPlane);
                    // For Stereo mode 
                    rightCam.SetStereoProjectionMatrix(StereoscopicEye.Right, rightMat);
                    rightCam.stereoTargetEye = StereoTargetEyeMask.Right;
                    // For Non Stereo and stereo
                    rightCam.projectionMatrix = rightMat;
                    rightCam.transform.rotation = transform.rotation;
                    //Debug.Log("Even een testje rightCam " + LowerLeft + leftCam.transform.position );
                }



            }

            if (centerCam != null)
            {
                //if(!useXRCameras)
                //{
                //    centerCam.projectionMatrix = Util.getAsymProjMatrix(LowerLeft, LowerRight, UpperLeft, centerCam.transform.position, head.nearClippingPlane, head.farClippingPlane);
                //}
                centerCam.projectionMatrix = Util.getAsymProjMatrix(LowerLeft, LowerRight, UpperLeft, centerCam.transform.position, head.nearClippingPlane, head.farClippingPlane);
                centerCam.transform.rotation = transform.rotation;
                //Debug.Log("Even een testje centerCam " + LowerLeft);

            }
        }

        /// <summary>
        /// Reposition window (takes multiple frames to complete)
        /// </summary>
        void Update()
        {
            if (!updatedViewports)// && WindowsUtils.CompletedOperation())
            {
                //we must also defer setting the camera viewports until the screen has the correct resolution
                if (dualPipe && !dualInstance)
                {
                    leftCam.pixelRect = new Rect(leftViewport.x, leftViewport.y, leftViewport.width, leftViewport.height);
                    rightCam.pixelRect = new Rect(rightViewport.x, rightViewport.y, rightViewport.width, rightViewport.height);
                }
                updatedViewports = true;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Draw debug view in editor
        /// </summary>
        void EditorDraw()
        {
            var mat = transform.localToWorldMatrix;
            Gizmos.color = Color.white;
            Gizmos.DrawLine(UpperRight, UpperLeft);
            Gizmos.DrawLine(UpperLeft, LowerLeft);
            Gizmos.DrawLine(LowerLeft, LowerRight);
            Gizmos.DrawLine(LowerRight, UpperRight);
        }

        /// <summary>
        /// Draw debug view in editor
        /// </summary>
        void OnDrawGizmos()
        {
            EditorDraw();
        }

        /// <summary>
        /// Draw advanced debug view in editor when selected
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            var mat = transform.localToWorldMatrix;
            Vector3 right = mat * new Vector4(halfWidth() * 0.75f, 0.0f, 0.0f, 1.0f);
            Vector3 up = mat * new Vector4(0.0f, halfHeight() * 0.75f, 0.0f, 1.0f);
            Gizmos.color = new Color(0.75f, 0.25f, 0.25f);
            Gizmos.DrawLine((transform.position * 2.0f + right) / 3.0f, right);
            Gizmos.color = new Color(0.25f, 0.75f, 0.25f);
            Gizmos.DrawLine((transform.position * 2.0f + up) / 3.0f, up);
        }
#endif

        /// <summary>
        /// Where to load and save serialized Settings from
        /// </summary>
        public string serializedLocation = "settings.json";

        /// <summary>
        /// Try to save Settings object0 to path
        /// </summary>
        /// <param name="path">path to save to</param>
        public void TryToSerialize(string path)
        {
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(GetSettings()));
            Debug.Log("Serialized settings to " + new System.IO.FileInfo(path).FullName);
        }

        /// <summary>
        /// Try to load Settings object from path
        /// </summary>
        /// <param name="path">path to load from</param>
        public void TryToDeSerialize(string path)
        {
            SetSettings(JsonUtility.FromJson<Settings>(System.IO.File.ReadAllText(path)));
            Debug.Log("Deserialized settings from " + new System.IO.FileInfo(path).FullName);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor for this display; some options are not always valid so they are disabled in some settings configurations
        /// </summary>
        [CustomEditor(typeof(PhysicalDisplay))]
        public class Editor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                serializedObject.Update();

                

                

                

                //draw machine name (only if there is no manager)
                SerializedProperty oldMachineName = serializedObject.FindProperty(nameof(PhysicalDisplay.oldMachineName));
                SerializedProperty machineNameAsset = serializedObject.FindProperty(nameof(PhysicalDisplay.machineNameAsset));
                

                //draw display width, height
                SerializedProperty width = serializedObject.FindProperty(nameof(PhysicalDisplay.width));
                SerializedProperty height = serializedObject.FindProperty(nameof(PhysicalDisplay.height));
                EditorGUILayout.PropertyField(width);
                EditorGUILayout.PropertyField(height);

                //draw display convergenceAtScreen
                SerializedProperty convergenceAtScreen = serializedObject.FindProperty(nameof(PhysicalDisplay.convergenceAtScreen));
                EditorGUILayout.PropertyField(convergenceAtScreen);
                

                //draw display convdistance
                if(!convergenceAtScreen.boolValue)
                {
                SerializedProperty convdistance = serializedObject.FindProperty(nameof(PhysicalDisplay.convdistance));
                EditorGUILayout.PropertyField(convdistance);
                }


                //draw head configuration
                SerializedProperty head = serializedObject.FindProperty(nameof(PhysicalDisplay.head));
                EditorGUILayout.PropertyField(head);


                //draw XR camera settings toggle
                SerializedProperty useXRCameras = serializedObject.FindProperty(nameof(PhysicalDisplay.useXRCameras));
                GUIContent useXRCameras_content = new GUIContent(text: "Use XR Cameras (Quad Buffer)",
                                                                 tooltip: "Whether the cameras associated with this display should output to an XR device (such as headset or quad-buffered stereo 3D display).\n" +
                                                                          "If you do post processing on the cameras (such as a PhysicalDisplayCalibration) set this to false.\n" +
                                                                          "This is probably also unnecessary if using a Dual-Pipe 3D display.");
                EditorGUILayout.PropertyField(useXRCameras, useXRCameras_content);


                //draw render texture settings toggle
                SerializedProperty useRenderTextures = serializedObject.FindProperty(nameof(PhysicalDisplay.useRenderTextures));
                GUIContent useRenderTextures_content = new GUIContent(text: "Use Render Textures",
                                                                      tooltip: "Render to render textures instead of screen, for post processing");
                EditorGUILayout.PropertyField(useRenderTextures, useRenderTextures_content);


                //draw render texture settings if render textures are enabled
                SerializedProperty renderTextureSize = serializedObject.FindProperty(nameof(PhysicalDisplay.renderTextureSize));
                if(useRenderTextures.boolValue)
                {
                    EditorGUILayout.PropertyField(renderTextureSize);
                }


                //draw exclusive fullscreen toggle if render textures are disabled and there is no manager
                SerializedProperty exclusiveFullscreen = serializedObject.FindProperty(nameof(PhysicalDisplay.exclusiveFullscreen));
                SerializedProperty display = serializedObject.FindProperty(nameof(PhysicalDisplay.display));
                if(!useRenderTextures.boolValue)
                {
                    GUIContent exclusiveFullscreen_content = new GUIContent(text: "Use Specific Display");
                    EditorGUILayout.PropertyField(exclusiveFullscreen, exclusiveFullscreen_content);

                    //draw display if exclusive fullscreen is enabled
                    if(exclusiveFullscreen.boolValue)
                    {
                        EditorGUILayout.PropertyField(display);
                    }
                }

                


                //draw 3D settings
                SerializedProperty is3D = serializedObject.FindProperty(nameof(PhysicalDisplay.is3D));
                SerializedProperty dualPipe = serializedObject.FindProperty(nameof(PhysicalDisplay.dualPipe));
                SerializedProperty dualInstance = serializedObject.FindProperty(nameof(PhysicalDisplay.dualInstance));
                SerializedProperty windowBounds = serializedObject.FindProperty(nameof(PhysicalDisplay.windowBounds));

                EditorGUILayout.PropertyField(is3D);

                if(is3D.boolValue)
                {
                    //draw dual pipe toggle
                    EditorGUILayout.PropertyField(dualPipe);

                    if(dualPipe.boolValue)
                    {
                        //draw dual pipe settings
                        SerializedProperty leftViewport = serializedObject.FindProperty(nameof(PhysicalDisplay.leftViewport));
                        SerializedProperty rightViewport = serializedObject.FindProperty(nameof(PhysicalDisplay.rightViewport));

                        EditorGUILayout.PropertyField(dualInstance);

                        if(!exclusiveFullscreen.boolValue && !dualInstance.boolValue)
                        {
                            //3D, dual pipe, and single instance
                            EditorGUILayout.PropertyField(windowBounds);
                        }

                        EditorGUILayout.PropertyField(leftViewport);
                        EditorGUILayout.PropertyField(rightViewport);
                    }
                    else
                    {
                        //draw window settings
                        EditorGUILayout.PropertyField(windowBounds);

                        if(dualInstance.boolValue) dualInstance.boolValue = false;
                    }
                }
                else
                {
                    //draw window settings
                    EditorGUILayout.PropertyField(windowBounds);

                    if(dualPipe.boolValue) dualPipe.boolValue = false;
                    if(dualInstance.boolValue) dualInstance.boolValue = false;
                }


                //draw settings toggle
                SerializedProperty loadSettingsAtRuntime = serializedObject.FindProperty(nameof(PhysicalDisplay.loadSettingsAtRuntime));
                SerializedProperty serializedLocation = serializedObject.FindProperty(nameof(PhysicalDisplay.serializedLocation));

                EditorGUILayout.PropertyField(loadSettingsAtRuntime);
                if(loadSettingsAtRuntime.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedLocation);
                }

                //done with serialized properties
                serializedObject.ApplyModifiedProperties();

                //update manager if it changed
                PhysicalDisplay physicalDisplay = target as PhysicalDisplay;

                

                //JSON buttons
                if(GUILayout.Button("Export Display Settings to JSON"))
                {
                    string path = EditorUtility.SaveFilePanel("Export Settings", "./", "settings.json", "json");
                    if(!string.IsNullOrEmpty(path))
                    {
                        physicalDisplay.TryToSerialize(path);
                    }
                }

                if(GUILayout.Button("Import Display Settings from JSON"))
                {
                    string path = EditorUtility.SaveFilePanel("Import Settings", "./", "settings.json", "json");
                    if(!string.IsNullOrEmpty(path))
                    {
                        physicalDisplay.TryToDeSerialize(path);
                    }
                }
            }
        }
#endif
    }
}