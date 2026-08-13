using ClayEditor.Rigging;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Scene.DesktopPet
{
    /// <summary>
    /// デスクトップペット焼き出し用にロードモデルへモーションを付与する
    /// </summary>
    public static class DesktopPetBakeModelSetup
    {
        /// <summary>
        /// 焼き出しに必要なモーションを初期化する
        /// </summary>
        /// <param name="importedRoot">ImportFromGlbAsyncで生成したモデルのルート</param>
        /// <param name="partAnalyzer">部位分類器</param>
        /// <param name="clayMaterial">ClayMonsterマテリアル未指定なら適用しない</param>
        public static LoadedModelConfigurator.Result Configure(
            GameObject importedRoot,
            SkeletonPartAnalyzer partAnalyzer,
            Material clayMaterial)
        {
            LoadedModelConfigurator.Result result = default;
            if (importedRoot == null)
            {
                Debug.LogWarning("[DesktopPetBakeModelSetup] importedRootがnullです");
                return result;
            }

            if (partAnalyzer == null)
            {
                Debug.LogError("[DesktopPetBakeModelSetup] SkeletonPartAnalyzerが未設定です");
                return result;
            }

            SkinnedMeshRenderer skinned = importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned == null)
            {
                Debug.LogWarning("[DesktopPetBakeModelSetup] SkinnedMeshRendererが見つかりません");
                return result;
            }

            result.Renderer = skinned;
            ApplyMaterial(importedRoot, clayMaterial);

            Animator animator = importedRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = importedRoot.AddComponent<Animator>();
            }

            animator.applyRootMotion = false;
            result.Animator = animator;

            bool hasBones = skinned.bones != null && skinned.bones.Length > 0;
            if (!hasBones)
            {
                Debug.LogWarning("[DesktopPetBakeModelSetup] ボーンが無いためモーションを初期化できません");
                return result;
            }

            ProceduralMotionCharacter motion = importedRoot.GetComponent<ProceduralMotionCharacter>();
            if (motion == null)
            {
                motion = importedRoot.AddComponent<ProceduralMotionCharacter>();
            }

            motion.Initialize(skinned.bones, partAnalyzer);
            motion.Play(MotionType.Idle);
            result.Motion = motion;
            return result;
        }

        private static void ApplyMaterial(GameObject root, Material clayMaterial)
        {
            if (root == null || clayMaterial == null)
            {
                Debug.LogError("[DesktopPetBakeModelSetup] ClayMonsterマテリアルが未設定です焼き出しが透明になります");
                return;
            }

            SkinnedMeshRenderer[] skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                skinnedRenderers[i].sharedMaterial = clayMaterial;
            }

            MeshRenderer[] meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                meshRenderers[i].sharedMaterial = clayMaterial;
            }
        }

        /// <summary>
        /// 焼き出し用DirectionalLightへURP追加データを付ける
        /// </summary>
        public static void EnsureUrpLight(Light light)
        {
            if (light == null)
            {
                return;
            }

            if (!light.TryGetComponent(out UniversalAdditionalLightData _))
            {
                light.gameObject.AddComponent<UniversalAdditionalLightData>();
            }
        }

        /// <summary>
        /// 焼き出し用CameraへURP追加データを付ける
        /// </summary>
        public static void EnsureUrpCamera(UnityEngine.Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (!camera.TryGetComponent(out UniversalAdditionalCameraData urpCameraData))
            {
                urpCameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            urpCameraData.renderPostProcessing = false;
            urpCameraData.renderShadows = false;
            urpCameraData.requiresColorOption = CameraOverrideOption.Off;
            urpCameraData.requiresDepthOption = CameraOverrideOption.Off;
        }
    }
}
