#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using NeonDistrict.Player;
using NeonDistrict.Vehicles;
using NeonDistrict.World;

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Сборка игровых префабов из исходных FBX (Neon District → 2) Build Prefabs).
    ///
    /// Делает:
    ///  • импорт персонажей как Humanoid (Renderpeople), анимация ходьбы/идла;
    ///  • префаб игрока (CharacterController + PlayerController + CameraPivot);
    ///  • префаб прохожего (NavMeshAgent + Pedestrian);
    ///  • префабы машин (Rigidbody + VehicleController + фары/стопы/сиденье);
    ///  • копирование моделей города в Resources и точку спавна на тротуаре Манхэттена.
    /// </summary>
    public static class AssetPipeline
    {
        private const string PlayerFbx = "Assets/Art/Characters/Player_Male.fbx";
        private const string NpcFbx = "Assets/Art/Characters/NPC_Female.fbx";
        private const string MercedesFbx = "Assets/Art/Vehicles/Mercedes_GLS.fbx";
        private const string PolestarFbx = "Assets/Art/Vehicles/Polestar_4.fbx";
        private const string CorvetteFbx = "Assets/Art/Vehicles/Corvette_Stingray.fbx";
        private const string AmgFbx = "Assets/Art/Vehicles/Mercedes_AMG_C63.fbx";
        private const string ResFolder = "Assets/Resources/NeonDistrict";

        [MenuItem("Neon District/2) Build Prefabs", priority = 2)]
        public static void BuildAll()
        {
            Directory.CreateDirectory(ResFolder);
            ConfigureCharacterImporter(PlayerFbx, "Player_Male_Albedo.jpg");
            ConfigureCharacterImporter(NpcFbx, "NPC_Female_Albedo.jpg");
            ConfigureVehicleImporter(MercedesFbx);
            ConfigurePoliceTextures();
            ConfigureVehicleImporter(PolestarFbx);
            ConfigureVehicleImporter(CorvetteFbx);
            ConfigureVehicleImporter(AmgFbx);

            BuildPlayerPrefab();
            BuildPedestrianPrefab();
            BuildVehiclePrefab(MercedesFbx, "Vehicle_GLS.prefab", 2400f, 34f, 200f);
            BuildVehiclePrefab(PolestarFbx, "Vehicle_Polestar.prefab", 2350f, 32f, 190f);
            BuildVehiclePrefab(CorvetteFbx, "Vehicle_Corvette.prefab", 1600f, 42f, 240f);
            BuildVehiclePrefab(AmgFbx, "Vehicle_AMG.prefab", 1750f, 40f, 250f);
            BuildPoliceCarPrefab();
            BuildPoliceFromBase("Vehicle_AMG.prefab", "Police_AMG.prefab");
            BuildPoliceFromBase("Vehicle_Polestar.prefab", "Police_Polestar.prefab");
            CopyCityResources();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<b>[Neon District]</b> Префабы готовы: Resources/NeonDistrict/{Player, Pedestrian, Vehicle_GLS, Vehicle_Polestar}");
        }

        // ------------------------------------------------------------ importers
        private static void ConfigureCharacterImporter(string fbx, string albedoName)
        {
            var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (imp == null) { Debug.LogWarning("Нет модели " + fbx); return; }

            imp.globalScale = 1f;
            imp.useFileScale = true;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.animationCompression = ModelImporterAnimationCompression.Optimal;
            imp.optimizeGameObjects = true;
            imp.importBlendShapes = false;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.meshCompression = ModelImporterMeshCompression.Medium;
            imp.SaveAndReimport();

            // материал
            var tex = FindTexture(albedoName);
            var matPath = "Assets/Art/Materials/" + Path.GetFileNameWithoutExtension(fbx) + "_Mat.mat";
            Directory.CreateDirectory("Assets/Art/Materials");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetFloat("_Smoothness", 0.22f);
            mat.SetFloat("_Metallic", 0f);
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            var normal = FindTexture(Path.GetFileNameWithoutExtension(fbx) + "_Normal.jpg");
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", 1f);
            }
            EditorUtility.SetDirty(mat);

            // назначаем материал всем рендерерам модели
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model != null)
            {
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            }
        }

        /// <summary>Ливрея и эмиссия: альфа для декалей, sRGB, мипы.</summary>
        private static void ConfigurePoliceTextures()
        {
            foreach (var name in new[] { "Police_Livery.png", "Police_LightBar_Emission.png" })
            {
                var path = "Assets/Art/Vehicles/Textures/" + name;
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.sRGBTexture = true;
                ti.mipmapEnabled = true;
                ti.maxTextureSize = 2048;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
        }

        private static void ConfigureVehicleImporter(string fbx)
        {
            var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (imp == null) { Debug.LogWarning("Нет модели " + fbx); return; }
            imp.globalScale = 1f;
            imp.useFileScale = true;
            imp.importAnimation = true;             // у Polestar есть анимация дверей
            imp.animationCompression = ModelImporterAnimationCompression.Optimal;
            imp.importBlendShapes = false;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.materialLocation = ModelImporterMaterialLocation.External;
            imp.meshCompression = ModelImporterMeshCompression.Medium;
            imp.SaveAndReimport();

            // все «металлики» машин делаем менее пластиковыми
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Path.GetDirectoryName(fbx).Replace('\\', '/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/Art/Vehicles")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null) continue;
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", Mathf.Clamp(m.GetFloat("_Metallic"), 0.4f, 0.9f));
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.82f);
                if (m.HasProperty("_ClearCoatMask") && m.shader.name.Contains("Lit")) m.SetFloat("_ClearCoatMask", 0.35f);
                EditorUtility.SetDirty(m);
            }
        }

        private static Texture2D FindTexture(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(name) + " t:Texture2D"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(name)) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            return null;
        }

        // ------------------------------------------------------------ prefabs
        private static void BuildPlayerPrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFbx);
            if (model == null) { Debug.LogError("Нет " + PlayerFbx); return; }

            var root = new GameObject("Player");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;

            var norm = visual.AddComponent<CharacterNormalizer>();
            norm.targetHeight = 1.82f;

            // процедурный слой анимации (наклон корпуса, отдача, приземление)
            var locomotion = visual.AddComponent<LocomotionAnimator>();

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.34f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 55f;
            cc.stepOffset = 0.4f;

            var pc = root.AddComponent<PlayerController>();
            pc.animator = visual.GetComponentInChildren<Animator>();
            pc.locomotion = locomotion;
            EnsureAnimatorController(fbx, pc.animator);
            pc.walkSpeed = 2.0f;
            pc.runSpeed = 5.2f;
            pc.sprintSpeed = 6.8f;

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            pc.cameraPivot = pivot.transform;

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0.22f, 1.4f, 0.5f);
            pc.muzzle = muzzle.transform;

            Save(root, "Player.prefab");
        }

        private static void BuildPedestrianPrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(NpcFbx);
            if (model == null) { Debug.LogError("Нет " + NpcFbx); return; }

            var root = new GameObject("Pedestrian");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Visual";

            var norm = visual.AddComponent<CharacterNormalizer>();
            norm.targetHeight = 1.68f;

            var col = root.AddComponent<CapsuleCollider>();
            col.height = 1.7f;
            col.radius = 0.3f;
            col.center = new Vector3(0f, 0.85f, 0f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.height = 1.7f;
            agent.radius = 0.3f;
            agent.speed = 1.4f;
            agent.angularSpeed = 260f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 0.2f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var ped = root.AddComponent<Pedestrian>();
            ped.animator = visual.GetComponentInChildren<Animator>();
            ped.walkSpeed = 1.35f;
            ped.runSpeed = 4.6f;

            Save(root, "Pedestrian.prefab");
        }

        private static void BuildVehiclePrefab(string fbx, string prefabName, float mass, float power, float maxKmh)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) { Debug.LogError("Нет " + fbx); return; }

            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabName));
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Visual";

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0.02f;
            rb.angularDamping = 3f;
#else
            rb.drag = 0.02f;
            rb.angularDrag = 3f;
#endif
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                var box = root.AddComponent<BoxCollider>();
                box.center = root.transform.InverseTransformPoint(b.center);
                box.size = new Vector3(Mathf.Max(1.7f, b.size.x), Mathf.Max(1.25f, b.size.y), Mathf.Max(3.8f, b.size.z));
            }

            var vc = root.AddComponent<VehicleController>();
            vc.enginePower = power;
            vc.maxSpeedKmh = maxKmh;
            vc.bodyVisual = visual.transform;

            var seat = new GameObject("DriverSeat");
            seat.transform.SetParent(root.transform, false);
            seat.transform.localPosition = new Vector3(0.36f, 0.95f, 0.1f);
            vc.driverSeat = seat.transform;

            var exit = new GameObject("ExitPoint");
            exit.transform.SetParent(root.transform, false);
            exit.transform.localPosition = new Vector3(-1.9f, 0.15f, 0.2f);
            vc.exitPoint = exit.transform;

            var heads = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Headlight" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(i == 0 ? -0.72f : 0.72f, 0.8f, 2.15f);
                go.transform.localRotation = Quaternion.Euler(2f, i == 0 ? -7f : 7f, 0f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.range = 65f;
                l.spotAngle = 58f;
                l.innerSpotAngle = 30f;
                l.intensity = 0f;
                l.color = new Color(1f, 0.96f, 0.88f);
                l.shadows = LightShadows.None;
                heads[i] = l;
            }
            vc.headlights = heads;

            var brakeGo = new GameObject("BrakeLight");
            brakeGo.transform.SetParent(root.transform, false);
            brakeGo.transform.localPosition = new Vector3(0f, 0.85f, -2.3f);
            var bl = brakeGo.AddComponent<Light>();
            bl.type = LightType.Point;
            bl.color = new Color(1f, 0.1f, 0.05f);
            bl.range = 5f;
            bl.intensity = 0.2f;
            vc.brakeLights = new[] { bl };

            Save(root, prefabName);
        }

        /// <summary>
        /// Полицейский внедорожник: копия Mercedes GLS с ливреей, мигалками,
        /// сиреной и ИИ погони. Готов к спавну PoliceSpawner'ом.
        /// </summary>
        private static void BuildPoliceCarPrefab() => BuildPoliceFromBase("Vehicle_GLS.prefab", "PoliceCar.prefab");

        /// <summary>Копия любой машины + полицейский комплект (ливрея, мигалки, сирена, ИИ).</summary>
        private static void BuildPoliceFromBase(string basePrefabName, string outputName)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResFolder + "/" + basePrefabName);
            if (basePrefab == null) { Debug.LogWarning("[Neon District] нет " + basePrefabName); return; }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            root.name = Path.GetFileNameWithoutExtension(outputName);

            var livery = root.AddComponent<NeonDistrict.Police.PoliceLivery>();
            livery.liveryTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Vehicles/Textures/Police_Livery.png");
            livery.lightBarEmission = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Vehicles/Textures/Police_LightBar_Emission.png");
            livery.bodyWhiteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Police_WhitePaint.mat");
            if (livery.bodyWhiteMaterial == null)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = "Police_WhitePaint";
                m.color = new Color(0.94f, 0.95f, 0.96f);
                m.SetFloat("_Metallic", 0.25f);
                m.SetFloat("_Smoothness", 0.85f);
                Directory.CreateDirectory("Assets/Art/Materials");
                AssetDatabase.CreateAsset(m, "Assets/Art/Materials/Police_WhitePaint.mat");
                livery.bodyWhiteMaterial = m;
            }

            var siren = root.AddComponent<NeonDistrict.Police.PoliceSiren>();
            var ai = root.AddComponent<NeonDistrict.Police.PoliceAI>();
            ai.livery = livery;

            // физика погони: чуть мощнее и устойчивее гражданской
            var vc = root.GetComponent<NeonDistrict.Vehicles.VehicleController>();
            if (vc != null)
            {
                vc.enginePower = 40f;
                vc.maxSpeedKmh = 195f;
                vc.grip = 1.15f;
                vc.bodyVisual = root.transform.Find("Visual") != null ? root.transform.Find("Visual") : vc.bodyVisual;
            }

            PrefabUtility.SaveAsPrefabAsset(root, ResFolder + "/" + outputName);
            Object.DestroyImmediate(root);
            Debug.Log("[Neon District] префаб: " + outputName + " (ливрея + мигалки + сирена + ИИ)");
        }

        private static void Save(GameObject root, string name)
        {
            Directory.CreateDirectory(ResFolder);
            var path = ResFolder + "/" + name;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log("[Neon District] префаб: " + path);
        }

        // ------------------------------------------------------------ city resources
        private static void CopyCityResources()
        {
            CopyFolder("Assets/Art/City/buildings", "Assets/Resources/Models/City/Buildings");
            CopyFolder("Assets/Art/City/old", "Assets/Resources/Models/City/Old");
            CopyFolder("Assets/Art/City/trees", "Assets/Resources/Models/City/Trees");
            CopyFolder("Assets/Art/City/props", "Assets/Resources/Models/City/Props");
            CopyFolder("Assets/Art/City/details", "Assets/Resources/Models/City/Details");
            CopyFolder("Assets/Art/City/signs", "Assets/Resources/Models/City/Signs");
            CopyFolder("Assets/Art/City/vehicles", "Assets/Resources/Models/City/Vehicles");
            AssetDatabase.Refresh();
        }

        private static void CopyFolder(string from, string to)
        {
            if (!Directory.Exists(from)) return;
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from))
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".glb" && ext != ".gltf" && ext != ".fbx") continue;
                var dst = Path.Combine(to, Path.GetFileName(f));
                if (!File.Exists(dst)) File.Copy(f, dst);
            }
        }
    }
}
#endif
