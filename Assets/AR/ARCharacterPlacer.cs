using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AR
{
    public class ARCharacterPlacer : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private GameObject aiPrefab;
        [SerializeField] private float yOffset = 0f;
        [SerializeField] private bool lockPlacementAfterSpawn = true;

        [Header("Interaction")]
        [SerializeField] private bool sendGreetingOnTap = false;
        [SerializeField] private string greetingText = "Hi!";
        [SerializeField] private float tapMaxDistance = 5f;

        private ARRaycastManager raycastManager;
        private Camera mainCamera;
        private GameObject spawnedAI;
        private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

        private void Awake()
        {
            raycastManager = GetComponent<ARRaycastManager>();
            if (raycastManager == null)
            {
                raycastManager = FindObjectOfType<ARRaycastManager>();
            }

            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
        }

        private void Update()
        {
            if (Input.touchCount == 0) return;

            var touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began) return;

            if (spawnedAI != null)
            {
                if (TryTapAI(touch.position))
                {
                    HandleTapOnAI();
                    return;
                }

                if (lockPlacementAfterSpawn)
                {
                    return;
                }
            }

            TryPlaceAI(touch.position);
        }

        private bool TryTapAI(Vector2 screenPos)
        {
            if (mainCamera == null || spawnedAI == null) return false;
            var ray = mainCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, tapMaxDistance))
            {
                return hit.collider != null && hit.collider.gameObject == spawnedAI;
            }
            return false;
        }

        private void HandleTapOnAI()
        {
            if (!sendGreetingOnTap) return;

            var gemini = FindObjectOfType<UnityAndGeminiV3>();
            if (gemini == null)
            {
                Debug.LogWarning("ARCharacterPlacer: UnityAndGeminiV3 not found.");
                return;
            }

            gemini.SendChat(greetingText);
        }

        private void TryPlaceAI(Vector2 screenPos)
        {
            if (raycastManager == null)
            {
                Debug.LogWarning("ARCharacterPlacer: ARRaycastManager missing; cannot place.");
                return;
            }

            if (!raycastManager.Raycast(screenPos, Hits, TrackableType.PlaneWithinPolygon)) return;

            var pose = Hits[0].pose;
            var position = pose.position + new Vector3(0f, yOffset, 0f);

            if (spawnedAI == null)
            {
                spawnedAI = aiPrefab != null ? Instantiate(aiPrefab, position, pose.rotation) : CreateFallbackAI(position, pose.rotation);
            }
            else
            {
                spawnedAI.transform.SetPositionAndRotation(position, pose.rotation);
            }

            FaceCamera(spawnedAI.transform);
        }

        private void FaceCamera(Transform target)
        {
            if (mainCamera == null || target == null) return;
            var lookPos = mainCamera.transform.position;
            lookPos.y = target.position.y;
            target.LookAt(lookPos);
        }

        private GameObject CreateFallbackAI(Vector3 position, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "AI Character";
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }
    }
}
