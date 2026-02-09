using UnityEngine;

namespace GeminiManager
{
    public class ArmsCrossedPose : MonoBehaviour
    {
        [Header("Apply Pose")]
        public bool applyOnStart = true;
        public bool reapplyEveryFrame = false;

        [Header("Pose Tweaks (degrees)")]
        public Vector3 leftUpperArmOffset = new Vector3(10f, 0f, 65f);
        public Vector3 leftLowerArmOffset = new Vector3(0f, -20f, 80f);
        public Vector3 leftHandOffset = new Vector3(0f, 0f, 20f);

        public Vector3 rightUpperArmOffset = new Vector3(10f, 0f, -65f);
        public Vector3 rightLowerArmOffset = new Vector3(0f, 20f, -80f);
        public Vector3 rightHandOffset = new Vector3(0f, 0f, -20f);

        public Vector3 chestOffset = new Vector3(0f, 0f, 0f);

        private Animator animator;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform leftHand;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform rightHand;
        private Transform chest;

        private Quaternion leftUpperArmBase;
        private Quaternion leftLowerArmBase;
        private Quaternion leftHandBase;
        private Quaternion rightUpperArmBase;
        private Quaternion rightLowerArmBase;
        private Quaternion rightHandBase;
        private Quaternion chestBase;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("ArmsCrossedPose: Animator not found.");
                return;
            }

            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);

            CacheBaseRotations();
        }

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyPose();
            }
        }

        private void LateUpdate()
        {
            if (reapplyEveryFrame)
            {
                ApplyPose();
            }
        }

        private void CacheBaseRotations()
        {
            if (leftUpperArm) leftUpperArmBase = leftUpperArm.localRotation;
            if (leftLowerArm) leftLowerArmBase = leftLowerArm.localRotation;
            if (leftHand) leftHandBase = leftHand.localRotation;
            if (rightUpperArm) rightUpperArmBase = rightUpperArm.localRotation;
            if (rightLowerArm) rightLowerArmBase = rightLowerArm.localRotation;
            if (rightHand) rightHandBase = rightHand.localRotation;
            if (chest) chestBase = chest.localRotation;
        }

        public void ApplyPose()
        {
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("ArmsCrossedPose: Animator missing or not humanoid.");
                return;
            }

            if (leftUpperArm) leftUpperArm.localRotation = leftUpperArmBase * Quaternion.Euler(leftUpperArmOffset);
            if (leftLowerArm) leftLowerArm.localRotation = leftLowerArmBase * Quaternion.Euler(leftLowerArmOffset);
            if (leftHand) leftHand.localRotation = leftHandBase * Quaternion.Euler(leftHandOffset);

            if (rightUpperArm) rightUpperArm.localRotation = rightUpperArmBase * Quaternion.Euler(rightUpperArmOffset);
            if (rightLowerArm) rightLowerArm.localRotation = rightLowerArmBase * Quaternion.Euler(rightLowerArmOffset);
            if (rightHand) rightHand.localRotation = rightHandBase * Quaternion.Euler(rightHandOffset);

            if (chest) chest.localRotation = chestBase * Quaternion.Euler(chestOffset);
        }
    }
}
