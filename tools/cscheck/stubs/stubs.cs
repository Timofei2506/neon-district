// Минимальные заглушки: только те API, которых нет в reference-сборках из NuGet.
// Нужны исключительно для офлайн-проверки компиляции (CI-скрипт шага 1),
// в Unity-проект НЕ входят.
namespace UnityEngine.Rendering.Universal
{
    public class UniversalRenderPipelineAsset : UnityEngine.Rendering.RenderPipelineAsset
    {
        public override UnityEngine.Rendering.RenderPipeline CreatePipeline() => null;
        public int msaaSampleCount;
        public float renderScale;
        public bool supportsHDR;
        public int shadowDistance;
        public int shadowCascadeCount;
        public int mainLightShadowmapResolution;
        public bool supportsSoftShadows;
        public bool supportsCameraDepthTexture;
        public bool supportsCameraOpaqueTexture;
        public int maxAdditionalLightsCount;
    }

    public class UniversalRendererData : UnityEngine.ScriptableObject { }
    public class Bloom : UnityEngine.Rendering.PostProcessing.VolumeComponent { }
    public class Vignette : UnityEngine.Rendering.PostProcessing.VolumeComponent { }
    public class ColorAdjustments : UnityEngine.Rendering.PostProcessing.VolumeComponent { }
}

namespace UnityEngine.Rendering.PostProcessing
{
    public class VolumeComponent : UnityEngine.ScriptableObject { }
}
