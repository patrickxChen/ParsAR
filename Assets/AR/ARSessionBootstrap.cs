using System;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace AR
{
    public static class ARSessionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureARRig()
        {
            var arSessionType = FindType("UnityEngine.XR.ARFoundation.ARSession");
            var arInputManagerType = FindType("UnityEngine.XR.ARFoundation.ARInputManager");
            var xrOriginType = FindType("Unity.XR.CoreUtils.XROrigin");
            var arCameraManagerType = FindType("UnityEngine.XR.ARFoundation.ARCameraManager");
            var arCameraBackgroundType = FindType("UnityEngine.XR.ARFoundation.ARCameraBackground");
            var arPlaneManagerType = FindType("UnityEngine.XR.ARFoundation.ARPlaneManager");
            var arRaycastManagerType = FindType("UnityEngine.XR.ARFoundation.ARRaycastManager");

            if (arSessionType != null && UnityObject.FindObjectOfType(arSessionType) == null)
            {
                var sessionGo = new GameObject("AR Session");
                sessionGo.AddComponent(arSessionType);
                if (arInputManagerType != null)
                {
                    sessionGo.AddComponent(arInputManagerType);
                }
            }

            Component origin = null;
            if (xrOriginType != null)
            {
                origin = UnityObject.FindObjectOfType(xrOriginType) as Component;
                if (origin == null)
                {
                    var originGo = new GameObject("XR Origin (AR Rig)");
                    origin = originGo.AddComponent(xrOriginType);
                }
            }

            Camera cam = null;
            if (origin != null)
            {
                var cameraProp = xrOriginType.GetProperty("Camera");
                cam = cameraProp != null ? cameraProp.GetValue(origin, null) as Camera : null;
            }

            if (origin != null)
            {
                if (arPlaneManagerType != null && origin.GetComponent(arPlaneManagerType) == null)
                {
                    origin.gameObject.AddComponent(arPlaneManagerType);
                }
                if (arRaycastManagerType != null && origin.GetComponent(arRaycastManagerType) == null)
                {
                    origin.gameObject.AddComponent(arRaycastManagerType);
                }

                if (origin.GetComponent<ARCharacterPlacer>() == null)
                {
                    origin.gameObject.AddComponent<ARCharacterPlacer>();
                }
            }

            if (cam == null)
            {
                cam = UnityObject.FindObjectOfType<Camera>();
                if (cam == null)
                {
                    var camGo = new GameObject("Main Camera");
                    cam = camGo.AddComponent<Camera>();
                    camGo.tag = "MainCamera";
                }

                if (arCameraManagerType != null && cam.GetComponent(arCameraManagerType) == null)
                {
                    cam.gameObject.AddComponent(arCameraManagerType);
                }
                if (arCameraBackgroundType != null && cam.GetComponent(arCameraBackgroundType) == null)
                {
                    cam.gameObject.AddComponent(arCameraBackgroundType);
                }
                if (cam.GetComponent<AudioListener>() == null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                }

                if (origin != null)
                {
                    cam.transform.SetParent(origin.transform, false);
                    var cameraProp = xrOriginType.GetProperty("Camera");
                    if (cameraProp != null)
                    {
                        cameraProp.SetValue(origin, cam, null);
                    }
                }
            }
        }

        private static Type FindType(string fullName)
        {
            var type = Type.GetType(fullName);
            if (type != null) return type;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                type = asm.GetType(fullName);
                if (type != null) return type;
            }

            return null;
        }
    }
}
