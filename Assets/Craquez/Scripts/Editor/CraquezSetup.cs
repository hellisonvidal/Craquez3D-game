using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Video;

namespace Craquez.EditorTools
{
    /// <summary>
    /// Configura o projeto inteiro com um clique (executa sozinho na primeira abertura):
    /// URP para mobile, materiais PBR, controladores de animação, prefabs (jogador, goleiro, bola),
    /// a cena "ArenaTreino", pós-processamento por clima, Build Settings e Player Settings (Android/Windows).
    /// Menu: Craquez 3D > Configurar Projeto.
    /// </summary>
    public static class CraquezSetup
    {
        const string Root = "Assets/Craquez";
        const string Gen = Root + "/Gerado";
        const string Tex = Root + "/Textures/";
        const string ScenePath = Root + "/Scenes/ArenaTreino.unity";
        const float PlayerHeight = 1.78f;
        const float KeeperHeight = 1.90f;

        public const string Scene = ScenePath;
        public const string IntroVideo = Root + "/Video/Abertura.webm";

        [MenuItem("Craquez 3D/Configurar Projeto (gerar cena e assets)", priority = 0)]
        public static void Run()
        {
            if (!Configure(true)) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorUtility.DisplayDialog("Craquez 3D", "Arena de treino pronta!\n\nAperte Play para jogar.\nVersão: " + PlayerSettings.bundleVersion, "Bora!");
        }

        /// <summary>
        /// Gera URP, materiais, animators, prefabs, cena e Player Settings.
        /// interactive=false: sem janelas (build automático no GitHub Actions).
        /// </summary>
        public static bool Configure(bool interactive)
        {
            try
            {
                if (interactive)
                {
                    EditorUtility.DisplayProgressBar("Craquez 3D", "Reimportando modelos e animações...", 0.05f);
                    ReimportAll();
                }

                Progress(interactive, "Configurando URP e Layers de Colisão...", 0.15f);
                Folder(Gen); Folder(Gen + "/Materiais"); Folder(Gen + "/Animacao"); Folder(Gen + "/Prefabs"); Folder(Gen + "/Render"); Folder(Root + "/Scenes");
                SetupTagsAndLayers();
                Arena.ConfigurePhysicsLayers();
                var pipeline = SetupUrp();

                Progress(interactive, "Criando materiais...", 0.3f);
                var mats = CreateMaterials();

                Progress(interactive, "Ligando animações aos avatares...", 0.4f);
                LinkClipAvatars();

                Progress(interactive, "Criando animações...", 0.45f);
                var playerCtrl = CreatePlayerController();
                var keeperCtrl = CreateKeeperController();

                Progress(interactive, "Criando prefabs...", 0.6f);
                var refCtrl = CreateRefereeController();
                var playerPrefab = CreateCharacterPrefab("Jogador", "Jogador", PlayerHeight, playerCtrl, mats["Jogador"], typeof(PlayerController));
                var keeperPrefab = CreateCharacterPrefab("Goleiro", "Goleiro", KeeperHeight, keeperCtrl, mats["Goleiro"], typeof(GoalkeeperAI));
                // partida 11 x 11: corpo do jogador para a linha e a arbitragem, corpo do goleiro para os dois goleiros
                // (a variedade vem de pele, cabelo, barba, formato do rosto, altura e peso de cada um)
                var playerPrefabB = playerPrefab;
                var keeperPrefabA = keeperPrefab;
                var refereePrefab = CreateCharacterPrefab("Arbitro", "Jogador", 1.80f, refCtrl, mats["Jogador"], typeof(RefereeAgent));
                // limiares do blend de locomoção = velocidade real da passada de cada clipe (pés sem deslizar)
                Progress(interactive, "Calibrando a locomoção...", 0.7f);
                CalibrateLocomotion(playerCtrl, playerPrefab);
                CalibrateLocomotion(refCtrl, refereePrefab);
                var refereePrefabB = refereePrefab;
                var ballPrefab = CreateBallPrefab(mats["Bola"]);

                Progress(interactive, "Montando a cena...", 0.75f);
                var profiles = CreateVolumeProfiles();
                CreateScene(mats, playerPrefab, keeperPrefab, ballPrefab, profiles);
                LinkMatchAssets(playerPrefabB, keeperPrefabA, refereePrefab, refereePrefabB);

                Progress(interactive, "Player Settings...", 0.9f);
                SetupPlayerSettings();

                AssetDatabase.SaveAssets();
                Debug.Log("[Craquez 3D] Projeto configurado. Cena: " + ScenePath + (pipeline == null ? " (ATENÇÃO: URP não encontrado)" : ""));
            }
            catch (Exception e)
            {
                Debug.LogError("[Craquez 3D] Falha no setup: " + e);
                if (interactive)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Craquez 3D", "Falha no setup:\n" + e.Message + "\n\nVeja o Console para detalhes.", "OK");
                }
                return false;
            }
            if (interactive) EditorUtility.ClearProgressBar();
            return true;
        }

        static void Progress(bool interactive, string msg, float t)
        {
            if (interactive) EditorUtility.DisplayProgressBar("Craquez 3D", msg, t);
            else Debug.Log("[Craquez 3D] " + msg);
        }

        // ------------------------------------------------------------------ utilidades

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void SetupTagsAndLayers()
        {
            var tagAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagAssets == null || tagAssets.Length == 0) return;
            var so = new SerializedObject(tagAssets[0]);
            var layersProp = so.FindProperty("layers");
            if (layersProp != null && layersProp.isArray)
            {
                SetLayerName(layersProp, Arena.LayerPlayerBody, "PlayerBody");
                SetLayerName(layersProp, Arena.LayerBall, "Ball");
                SetLayerName(layersProp, Arena.LayerGKGloves, "GKGloves");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void SetLayerName(SerializedProperty layersProp, int index, string name)
        {
            if (index >= 0 && index < layersProp.arraySize)
            {
                var el = layersProp.GetArrayElementAtIndex(index);
                if (el != null) el.stringValue = name;
            }
        }

        /// <summary>
        /// Animações feitas sobre o MESMO esqueleto do personagem (nativas do Meshy transferidas e as do goleiro)
        /// usam o avatar do próprio personagem como origem: o retarget vira identidade, sem torções nos pés.
        /// </summary>
        static void LinkClipAvatars()
        {
            var sets = new[]
            {
                new { model = "Jogador", folder = "Jogador", clips = new[] { "Idle", "Andar", "Trote", "Correr", "Disparada", "ChuteForte", "ChuteColocado", "Cavadinha", "ChuteRasteiro", "Drible", "Dominio", "Cabeceio", "Voleio", "Carrinho" } },
                new { model = "Goleiro", folder = "Goleiro", clips = new[] { "GK_Pronto", "GK_DeslocarDir", "GK_DeslocarEsq", "GK_Andar", "GK_Correr", "GK_Recuar", "GK_Espera", "GK_Soco", "GK_Mergulho", "GK_Caido", "GK_Levantar", "GK_Salto", "GK_Defesa", "GK_Lancar", "GK_Chutao", "GK_Orientar", "GK_PosicionarBola", "GK_Frustracao", "GK_Carrinho" } },
            };
            foreach (var set in sets)
            {
                Avatar avatar = null;
                // o avatar vem do esqueleto original com que os clipes foram gravados (Animations/<pasta>/Esqueleto.fbx);
                // o retarget Humanoid leva o movimento para os corpos novos
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Root + "/Animations/" + set.folder + "/Esqueleto.fbx"))
                    if (o is Avatar a && a.isHuman) { avatar = a; break; }
                if (avatar == null) { Debug.LogWarning("[Craquez 3D] Avatar de " + set.model + " não encontrado"); continue; }
                foreach (var c in set.clips)
                {
                    string path = Root + "/Animations/" + set.folder + "/" + c + ".fbx";
                    var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (mi == null) continue;
                    if (mi.avatarSetup == ModelImporterAvatarSetup.CopyFromOther && mi.sourceAvatar == avatar) continue;
                    mi.animationType = ModelImporterAnimationType.Human;
                    mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    mi.sourceAvatar = avatar;
                    mi.SaveAndReimport();
                }
            }
        }

        static void ReimportAll()
        {
            foreach (var dir in new[] { Root + "/Models", Root + "/Animations", Root + "/Textures" })
            {
                foreach (var guid in AssetDatabase.FindAssets("", new[] { dir }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(p)) continue;
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                }
            }
            AssetDatabase.Refresh();
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogWarning("[Craquez 3D] Asset não encontrado: " + path);
            return a;
        }

        static Texture2D T(string name)
        {
            foreach (var ext in new[] { ".png", ".jpg" })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + name + ext);
                if (t != null) return t;
            }
            Debug.LogWarning("[Craquez 3D] Textura não encontrada: " + name);
            return null;
        }

        /// <summary>Textura do Kenney Particle Pack (Assets/ExternalAssets/Particles), importada com alfa = transparência.</summary>
        static Texture2D KenneyParticle(string name)
        {
            string path = "Assets/ExternalAssets/Particles/" + name + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && (!ti.alphaIsTransparency || ti.maxTextureSize != 256 || ti.wrapMode != TextureWrapMode.Clamp))
            {
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.maxTextureSize = 256;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) Debug.LogWarning("[Craquez 3D] Partícula não encontrada: " + path);
            return t;
        }

        static T SaveAsset<T>(T obj, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(obj, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        // ------------------------------------------------------------------ URP

        static UniversalRenderPipelineAsset SetupUrp()
        {
            string rdPath = Gen + "/Render/Craquez_Renderer.asset";
            string pPath = Gen + "/Render/Craquez_URP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pPath);
            if (asset == null)
            {
                var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, rdPath);
                asset = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(asset, pPath);
            }
            asset.supportsHDR = true;
            asset.msaaSampleCount = 2;
            asset.renderScale = 1f;
            asset.shadowDistance = 70f;
            asset.shadowCascadeCount = 2;
            // supportsSoftShadows é somente leitura no URP 17: grava o campo serializado
            var so = new SerializedObject(asset);
            var soft = so.FindProperty("m_SoftShadowsSupported");
            if (soft != null) { soft.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
            asset.mainLightShadowmapResolution = 2048;
            asset.maxAdditionalLightsCount = 4;
            asset.supportsCameraDepthTexture = false;
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            return asset;
        }

        // ------------------------------------------------------------------ materiais

        static Shader Find(string name, string fallback)
        {
            var s = Shader.Find(name);
            if (s == null) { Debug.LogError("[Craquez 3D] Shader não encontrado: " + name + " (o pacote URP está instalado?)"); s = Shader.Find(fallback); }
            return s;
        }

        static Material Mat(string name, Shader shader)
        {
            string path = Gen + "/Materiais/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            return m;
        }

        static void Keywords(Material m, bool lit)
        {
            try
            {
                if (lit) BaseShaderGUI.SetMaterialKeywords(m, UnityEditor.Rendering.Universal.ShaderGUI.LitGUI.SetMaterialKeywords);
                else BaseShaderGUI.SetMaterialKeywords(m);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Craquez 3D] Keywords manuais para " + m.name + ": " + e.Message);
                if (m.GetTexture("_BumpMap") != null) m.EnableKeyword("_NORMALMAP");
                if (m.HasProperty("_MetallicGlossMap") && m.GetTexture("_MetallicGlossMap") != null) m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            EditorUtility.SetDirty(m);
        }

        static Material Lit(string name, Texture2D albedo, Texture2D normal, Texture2D metallicSmooth, Color color, float smoothness, float metallic = 0f, float normalScale = 1f)
        {
            var m = Mat(name, Find("Universal Render Pipeline/Lit", "Standard"));
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", normalScale);
            m.SetTexture("_MetallicGlossMap", metallicSmooth);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_Cull", 2f);
            Keywords(m, true);
            return m;
        }

        static void MakeTransparent(Material m, bool lit)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            Keywords(m, lit);
        }

        static Material Particle(string name, Texture2D tex, Color color)
        {
            var m = Mat(name, Find("Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit"));
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            MakeTransparent(m, false);
            return m;
        }

        static Dictionary<string, Material> CreateMaterials()
        {
            var d = new Dictionary<string, Material>();
            // Smoothness = 1.0f porque o canal Alpha do mapa MetallicSmoothness já contém o inverso exato do Roughness do Meshy (1 - Roughness)
            d["Jogador"] = Lit("Jogador", T("Jogador_Albedo"), T("Jogador_Normal"), T("Jogador_MetallicSmoothness"), Color.white, 1.0f, 0f, 1.0f);
            d["Goleiro"] = Lit("Goleiro", T("Goleiro_Albedo"), T("Goleiro_Normal"), T("Goleiro_MetallicSmoothness"), Color.white, 1.0f, 0f, 1.0f);
            d["Bola"] = Lit("Bola", T("Bola_Albedo"), T("Bola_Normal"), T("Bola_MetallicSmoothness"), Color.white, 0.75f);
            d["Estadio"] = Lit("Estadio", T("Estadio_Albedo"), T("Estadio_Normal"), T("Estadio_MetallicSmoothness"), Color.white, 0.4f, 0f, 0.8f);
            d["GramaClara"] = Lit("GramaClara", T("Grama_Albedo"), T("Grama_Normal"), null, new Color(0.92f, 1f, 0.9f), 0.12f, 0f, 0.6f);
            d["GramaEscura"] = Lit("GramaEscura", T("Grama_Albedo"), T("Grama_Normal"), null, new Color(0.76f, 0.87f, 0.72f), 0.12f, 0f, 0.6f);
            d["Linhas"] = Lit("Linhas", null, null, null, new Color(0.93f, 0.93f, 0.9f), 0.25f);
            d["Trave"] = Lit("Trave", null, null, null, new Color(0.96f, 0.96f, 0.96f), 0.7f, 0.05f);

            var rede = Lit("Rede", T("Rede"), null, null, Color.white, 0.2f);
            rede.SetTextureScale("_BaseMap", new Vector2(1.04f, 1.04f));
            rede.SetFloat("_AlphaClip", 1f);
            rede.SetFloat("_Cutoff", 0.5f);
            rede.SetFloat("_Cull", 0f);
            Keywords(rede, true);
            d["Rede"] = rede;

            var poca = Lit("Poca", T("Poca_Mascara"), T("Poca_Normal"), null, new Color(0.18f, 0.28f, 0.38f, 0.84f), 0.99f, 0.22f, 0.45f);
            MakeTransparent(poca, true);
            d["Poca"] = poca;

            var encharcado = Lit("GramadoEncharcado", T("Poca_Mascara"), T("Poca_Normal"), null, new Color(0.10f, 0.18f, 0.12f, 0.42f), 0.92f, 0.06f, 0.22f);
            MakeTransparent(encharcado, true);
            d["Encharcado"] = encharcado;

            d["Chuva"] = Particle("Chuva", T("Chuva_Gota"), new Color(0.8f, 0.85f, 0.95f, 0.55f));
            d["Respingo"] = Particle("Respingo", T("Respingo"), new Color(0.85f, 0.9f, 1f, 0.6f));
            d["Borrifo"] = Particle("Borrifo", T("UI_Circulo"), new Color(0.8f, 0.86f, 0.95f, 0.75f));
            d["Anel"] = Particle("AnelChuva", T("Anel"), new Color(0.85f, 0.9f, 1f, 0.7f));
            // efeitos (Kenney Particle Pack, CC0): uma textura por material (o modo Sprites exige uma só textura)
            d["FxPoeira"] = Particle("FxPoeira", KenneyParticle("dirt_02"), Color.white);
            d["FxFumaca"] = Particle("FxFumaca", KenneyParticle("smoke_04"), Color.white);
            d["FxEstrela"] = Particle("FxEstrela", KenneyParticle("star_06"), Color.white);

            var nuvens = Mat("Nuvens", Find("Universal Render Pipeline/Unlit", "Unlit/Transparent"));
            nuvens.SetTexture("_BaseMap", T("Nuvens"));
            nuvens.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.3f));
            nuvens.SetFloat("_Cull", 0f);
            MakeTransparent(nuvens, false);
            d["Nuvens"] = nuvens;

            var ceu = Mat("Ceu", Find("Skybox/Procedural", "Skybox/Procedural"));
            ceu.SetFloat("_SunDisk", 2f);
            ceu.SetFloat("_SunSize", 0.04f);
            ceu.SetFloat("_AtmosphereThickness", 1f);
            ceu.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(ceu);
            d["Ceu"] = ceu;
            return d;
        }

        // ------------------------------------------------------------------ animação

        static AnimationClip Clip(string sub, string name)
        {
            string path = Root + "/Animations/" + sub + "/" + name + ".fbx";
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                var c = o as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview__")) return c;
            }
            Debug.LogError("[Craquez 3D] Clipe não encontrado: " + path);
            return null;
        }

        /// <summary>Clipe da Universal Animation Library (Quaternius, CC0) pelo nome do take.</summary>
        static AnimationClip QClip(string file, string take)
        {
            string path = Root + "/Animations/Quaternius/" + file + ".fbx";
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                var c = o as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview__") && CraquezImportSettings.TakeName(c.name) == take) return c;
            }
            Debug.LogWarning("[Craquez 3D] Clipe da Quaternius não encontrado: " + file + "/" + take);
            return null;
        }

        static AnimatorState AddClipState(AnimatorStateMachine sm, string state, AnimationClip clip, float speed, AnimatorState exitTo, float exitTime, float blend)
        {
            if (clip == null) return null;
            var st = sm.AddState(state);
            st.motion = clip;
            st.speed = speed;
            if (exitTo != null)
            {
                var tr = st.AddTransition(exitTo);
                tr.hasExitTime = true;
                tr.exitTime = exitTime;
                tr.duration = blend;
            }
            return st;
        }

        static float NativeSpeed(AnimationClip c, float fallback)
        {
            if (c == null) return fallback;
            Vector3 v = c.averageSpeed;
            float s = new Vector2(v.x, v.z).magnitude;
            return s > 0.2f ? s : fallback;
        }

        /// <summary>
        /// Velocidade no chão que um clipe "no lugar" representa: enquanto o pé está apoiado ele corre para trás
        /// (em relação ao corpo) na mesma velocidade em que o corpo avançaria. Mede no prefab real (retarget Humanoid)
        /// e devolve m/s, ou -1 se não der para medir. As animações são importadas sem deslocamento da raiz,
        /// então clip.averageSpeed é sempre 0 e não serve para isso.
        /// </summary>
        static float MeasureGroundSpeed(GameObject prefab, AnimationClip clip)
        {
            if (prefab == null || clip == null || clip.length < 0.05f) return -1f;
            var go = (GameObject)UnityEngine.Object.Instantiate(prefab);
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var anim = go.GetComponentInChildren<Animator>();
                if (anim == null || !anim.isHuman) return -1f;
                var feet = new[] { anim.GetBoneTransform(HumanBodyBones.LeftFoot), anim.GetBoneTransform(HumanBodyBones.RightFoot) };
                if (feet[0] == null || feet[1] == null) return -1f;
                var graph = PlayableGraph.Create("calibrar");
                const int N = 120;
                var z = new float[2, N + 1];
                var y = new float[2, N + 1];
                try
                {
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var output = AnimationPlayableOutput.Create(graph, "o", anim);
                    var cp = AnimationClipPlayable.Create(graph, clip);
                    cp.SetApplyFootIK(false);
                    output.SetSourcePlayable(cp);
                    graph.Play();
                    for (int k = 0; k <= N; k++)
                    {
                        cp.SetTime(clip.length * k / N);
                        graph.Evaluate(0f);
                        for (int f = 0; f < 2; f++)
                        {
                            Vector3 p = go.transform.InverseTransformPoint(feet[f].position);
                            z[f, k] = p.z; y[f, k] = p.y;
                        }
                    }
                }
                finally { graph.Destroy(); }
                float dt = clip.length / N;
                var v = new List<float>();
                for (int f = 0; f < 2; f++)
                {
                    float minY = float.MaxValue;
                    for (int k = 0; k <= N; k++) minY = Mathf.Min(minY, y[f, k]);
                    for (int k = 0; k < N; k++)
                        if (y[f, k] < minY + 0.03f && y[f, k + 1] < minY + 0.03f)
                        {
                            float s = (z[f, k] - z[f, k + 1]) / dt; // pé apoiado correndo para trás
                            if (s > 0.05f) v.Add(s);
                        }
                }
                if (v.Count < 4) return -1f;
                v.Sort();
                return v[v.Count / 2];
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// Recalibra o blend 1D "Locomocao": cada clipe passa a valer na velocidade da própria passada
        /// (ajustando o ritmo quando é preciso separar dois clipes parecidos) e o jogador recebe a
        /// velocidade do último clipe para acelerar a animação acima dela.
        /// </summary>
        static void CalibrateLocomotion(AnimatorController ac, GameObject prefab)
        {
            if (ac == null || prefab == null) return;
            BlendTree bt = null;
            foreach (var st in ac.layers[0].stateMachine.states)
                if (st.state.name == "Locomocao") bt = st.state.motion as BlendTree;
            if (bt == null || bt.blendType != BlendTreeType.Simple1D) return;
            var ch = bt.children;
            float prev = 0f;
            var log = new System.Text.StringBuilder("[Craquez 3D] Locomoção " + ac.name + ":");
            for (int i = 1; i < ch.Length; i++)
            {
                var clip = ch[i].motion as AnimationClip;
                float native = MeasureGroundSpeed(prefab, clip);
                float old = ch[i].threshold;
                if (native <= 0.2f) { native = old / Mathf.Max(0.1f, ch[i].timeScale); log.Append(" ").Append(clip != null ? clip.name : "?").Append("=sem medida"); }
                // ritmo original do clipe; acelera só o necessário para ficar acima do anterior
                float ts = Mathf.Max(1f, ch[i].timeScale);
                float thr = native * ts;
                float minThr = prev + (i == 1 ? 0.6f : 0.8f);
                if (thr < minThr) { ts = Mathf.Clamp(minThr / native, 0.7f, 2.2f); thr = native * ts; }
                thr = Mathf.Max(thr, minThr); // limiares sempre crescentes (o blend 1D exige)
                ch[i].timeScale = ts;
                ch[i].threshold = thr;
                prev = thr;
                log.Append(" ").Append(clip != null ? clip.name : "?").Append(" passada ").Append(native.ToString("0.00")).Append(" m/s x").Append(ts.ToString("0.00")).Append(" -> ").Append(thr.ToString("0.00"));
            }
            bt.children = ch;
            EditorUtility.SetDirty(bt);
            EditorUtility.SetDirty(ac);
            Debug.Log(log.ToString());

            var pc = prefab.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.locoTopSpeed = prev;
                EditorUtility.SetDirty(prefab);
                PrefabUtility.SavePrefabAsset(prefab);
            }
            var ra = prefab.GetComponent<RefereeAgent>();
            if (ra != null)
            {
                ra.locoTopSpeed = prev;
                EditorUtility.SetDirty(prefab);
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }

        static AnimatorController NewController(string name)
        {
            string path = Gen + "/Animacao/" + name + ".controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            return AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        static void EnableIK(AnimatorController ac)
        {
            var layers = ac.layers;
            layers[0].iKPass = true;
            ac.layers = layers;
        }

        static AnimatorController CreatePlayerController()
        {
            var ac = NewController("Jogador");
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Direction", AnimatorControllerParameterType.Float);
            ac.AddParameter("Angle", AnimatorControllerParameterType.Float);
            var sm = ac.layers[0].stateMachine;

            var idle = Clip("Jogador", "Idle");
            var walk = Clip("Jogador", "Andar");
            var jog = Clip("Jogador", "Trote");
            var run = Clip("Jogador", "Correr");
            float sWalk = NativeSpeed(walk, 1.4f), sJog = Mathf.Max(sWalk + 0.5f, NativeSpeed(jog, 3.2f)), sRun = Mathf.Max(sJog + 0.8f, NativeSpeed(run, 5.6f));

            BlendTree bt;
            var loco = ac.CreateBlendTreeInController("Locomocao", out bt, 0);
            bt.blendType = BlendTreeType.Simple1D;
            bt.blendParameter = "Speed";
            bt.useAutomaticThresholds = false;
            bt.AddChild(idle, 0f);
            bt.AddChild(walk, sWalk);
            bt.AddChild(jog, sJog);
            bt.AddChild(run, sRun);
            // sprint: arrancada nativa do Meshy (ou a corrida acelerada, se faltar)
            var sprint = Clip("Jogador", "Disparada");
            bt.AddChild(sprint != null ? sprint : run, 10.2f);
            var ch = bt.children;
            ch[3].timeScale = 1.18f;
            ch[4].timeScale = sprint != null ? 1.30f : 10.2f / sRun;
            bt.children = ch;
            sm.defaultState = loco;

            foreach (var n in new[] { "ChuteForte", "ChuteColocado", "Cavadinha", "ChuteRasteiro", "Drible", "Dominio", "Cabeceio", "Voleio", "Carrinho" })
            {
                var st = sm.AddState(n);
                st.motion = Clip("Jogador", n);
                st.speed = n == "Drible" ? 1.15f : 1.25f;
                var tr = st.AddTransition(loco);
                tr.hasExitTime = true;
                tr.exitTime = 0.85f;
                tr.duration = 0.16f;
            }

            // contato físico, contusões e bola parada (Universal Animation Library — Quaternius, CC0)
            var levantar = AddClipState(sm, "Levantar", QClip("UAL2", "LayToIdle"), 1.25f, loco, 0.9f, 0.22f);
            AddClipState(sm, "Queda", QClip("UAL1", "Death01"), 1.35f, null, 0f, 0f);                 // fica no chão até o código levantar
            AddClipState(sm, "Atingido", QClip("UAL1", "Hit_Chest"), 1.2f, loco, 0.8f, 0.18f);
            AddClipState(sm, "AtingidoCabeca", QClip("UAL1", "Hit_Head"), 1.2f, loco, 0.8f, 0.18f);
            AddClipState(sm, "Empurrado", QClip("UAL2", "Hit_Knockback"), 1.25f, loco, 0.85f, 0.2f);
            AddClipState(sm, "Lateral", QClip("UAL2", "OverhandThrow"), 1.1f, loco, 0.92f, 0.2f);
            AddClipState(sm, "Comemorar", QClip("UAL1", "Dance_Loop"), 1f, null, 0f, 0f);
            AddClipState(sm, "ComemorarSim", QClip("UAL2", "Yes"), 1f, loco, 0.95f, 0.25f);
            AddClipState(sm, "Reclamar", QClip("UAL2", "Idle_No_Loop"), 1f, loco, 1.6f, 0.25f);
            AddClipState(sm, "CaidoDor", QClip("UAL1", "Crouch_Idle_Loop"), 0.7f, null, 0f, 0f);       // agachado, segurando a contusão
            var deslize = AddClipState(sm, "CarrinhoInicio", QClip("UAL2", "Slide_Start"), 1.2f, null, 0f, 0f);
            var deslizeLoop = AddClipState(sm, "CarrinhoDeslize", QClip("UAL2", "Slide_Loop"), 1f, null, 0f, 0f);
            var deslizeFim = AddClipState(sm, "CarrinhoFim", QClip("UAL2", "Slide_Exit"), 1.2f, loco, 0.9f, 0.2f);
            if (deslize != null && deslizeLoop != null)
            {
                var a = deslize.AddTransition(deslizeLoop); a.hasExitTime = true; a.exitTime = 0.95f; a.duration = 0.05f;
            }
            if (levantar == null) Debug.LogWarning("[Craquez 3D] Sem LayToIdle: o jogador levanta direto na locomoção.");
            EnableIK(ac);
            EditorUtility.SetDirty(ac);
            return ac;
        }

        /// <summary>Árbitro e assistentes: locomoção completa e gestos (apito, cartão, bandeira).</summary>
        static AnimatorController CreateRefereeController()
        {
            var ac = NewController("Arbitro");
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var sm = ac.layers[0].stateMachine;
            BlendTree bt;
            var loco = ac.CreateBlendTreeInController("Locomocao", out bt, 0);
            bt.blendType = BlendTreeType.Simple1D;
            bt.blendParameter = "Speed";
            bt.useAutomaticThresholds = false;
            var idle = QClip("UAL1", "Idle_Loop");
            var walk = QClip("UAL1", "Walk_Loop");
            var jog = QClip("UAL1", "Jog_Fwd_Loop");
            var sprint = QClip("UAL1", "Sprint_Loop");
            // fallback: animações do jogador se a biblioteca não estiver presente
            bt.AddChild(idle != null ? idle : Clip("Jogador", "Idle"), 0f);
            bt.AddChild(walk != null ? walk : Clip("Jogador", "Andar"), 1.4f);
            bt.AddChild(jog != null ? jog : Clip("Jogador", "Trote"), 3.6f);
            bt.AddChild(sprint != null ? sprint : Clip("Jogador", "Correr"), 6.8f);
            sm.defaultState = loco;
            AddClipState(sm, "Apitar", QClip("UAL1", "Interact"), 1.1f, loco, 0.9f, 0.2f);
            AddClipState(sm, "Cartao", QClip("UAL2", "Idle_Rail_Call"), 1f, loco, 0.95f, 0.25f);
            AddClipState(sm, "Bandeira", QClip("UAL2", "Idle_Rail_Call"), 0.8f, null, 0f, 0f);
            AddClipState(sm, "Conversar", QClip("UAL1", "Idle_Talking_Loop"), 1f, loco, 1.8f, 0.3f);
            AddClipState(sm, "Negar", QClip("UAL2", "Idle_No_Loop"), 1f, loco, 1.2f, 0.25f);
            EnableIK(ac);
            EditorUtility.SetDirty(ac);
            return ac;
        }

        static AnimatorController CreateKeeperController()
        {
            var ac = NewController("Goleiro");
            ac.AddParameter("Lateral", AnimatorControllerParameterType.Float);
            ac.AddParameter("Forward", AnimatorControllerParameterType.Float);
            ac.AddParameter("Mirror", AnimatorControllerParameterType.Bool);
            ac.AddParameter("DiveSpeed", AnimatorControllerParameterType.Float);
            var pars = ac.parameters;
            foreach (var par in pars) if (par.name == "DiveSpeed") par.defaultFloat = 1f;
            ac.parameters = pars;
            var sm = ac.layers[0].stateMachine;

            // animações próprias do goleiro (locomoção 2D: lateral + avanço/recuo)
            var idle = Clip("Goleiro", "GK_Pronto");
            var left = Clip("Goleiro", "GK_DeslocarEsq");
            var right = Clip("Goleiro", "GK_DeslocarDir");
            var walkFwd = Clip("Goleiro", "GK_Andar");
            var runFwd = Clip("Goleiro", "GK_Correr");
            var walkBack = Clip("Goleiro", "GK_Recuar");
            float sl = NativeSpeed(left, 1.85f), sr = NativeSpeed(right, 1.85f);

            BlendTree bt;
            var loco = ac.CreateBlendTreeInController("Locomocao", out bt, 0);
            bt.blendType = BlendTreeType.FreeformDirectional2D;
            bt.blendParameter = "Lateral";
            bt.blendParameterY = "Forward";
            bt.useAutomaticThresholds = false;
            bt.AddChild(idle, new Vector2(0f, 0f));
            bt.AddChild(left, new Vector2(-sl, 0f));
            bt.AddChild(right, new Vector2(sr, 0f));
            if (walkFwd != null) bt.AddChild(walkFwd, new Vector2(0f, 1.6f));
            if (runFwd != null) bt.AddChild(runFwd, new Vector2(0f, 4.5f));
            if (walkBack != null) bt.AddChild(walkBack, new Vector2(0f, -2.2f));
            var gkCh = bt.children;
            if (gkCh.Length >= 3)
            {
                gkCh[1].timeScale = 1.25f;
                gkCh[2].timeScale = 1.25f;
            }
            bt.children = gkCh;
            sm.defaultState = loco;

            // mergulho: a animação base vai para a DIREITA do goleiro; o parâmetro Mirror leva para a esquerda
            var dive = sm.AddState("GK_Mergulho");
            dive.motion = Clip("Goleiro", "GK_Mergulho");
            dive.speed = 1f;
            dive.speedParameterActive = true;
            dive.speedParameter = "DiveSpeed";
            dive.mirrorParameterActive = true;
            dive.mirrorParameter = "Mirror";

            var jump = sm.AddState("GK_Salto");
            jump.motion = Clip("Goleiro", "GK_Salto");
            jump.speed = 1.1f;
            jump.mirrorParameterActive = true;
            jump.mirrorParameter = "Mirror";

            var block = sm.AddState("GK_Defesa");
            block.motion = Clip("Goleiro", "GK_Defesa");
            block.speed = 1.2f;

            var fallen = sm.AddState("GK_Caido");
            fallen.motion = Clip("Goleiro", "GK_Caido");
            fallen.mirrorParameterActive = true;
            fallen.mirrorParameter = "Mirror";
            var getUp = sm.AddState("GK_Levantar");
            getUp.motion = Clip("Goleiro", "GK_Levantar");
            getUp.speed = 1.2f;
            getUp.mirrorParameterActive = true;
            getUp.mirrorParameter = "Mirror";

            var throwBall = sm.AddState("GK_Lancar");
            throwBall.motion = Clip("Goleiro", "GK_Lancar");
            throwBall.speed = 1.15f;

            var puntBall = sm.AddState("GK_Chutao");
            puntBall.motion = Clip("Goleiro", "GK_Chutao");
            puntBall.speed = 1.1f;

            var directWall = sm.AddState("GK_Orientar");
            directWall.motion = Clip("Goleiro", "GK_Orientar");

            var placeBall = sm.AddState("GK_PosicionarBola");
            placeBall.motion = Clip("Goleiro", "GK_PosicionarBola");

            var missReact = sm.AddState("GK_Frustracao");
            missReact.motion = Clip("Goleiro", "GK_Frustracao");

            var slideSave = sm.AddState("GK_Carrinho");
            slideSave.motion = Clip("Goleiro", "GK_Carrinho");
            slideSave.speed = 1.2f;
            slideSave.mirrorParameterActive = true;
            slideSave.mirrorParameter = "Mirror";

            var waitIdle = sm.AddState("GK_Espera");
            waitIdle.motion = Clip("Goleiro", "GK_Espera");

            var punchJump = sm.AddState("GK_Soco");
            punchJump.motion = Clip("Goleiro", "GK_Soco");
            punchJump.speed = 1.15f;

            var t = getUp.AddTransition(loco); t.hasExitTime = true; t.exitTime = 0.92f; t.duration = 0.2f;
            var t2 = jump.AddTransition(loco); t2.hasExitTime = true; t2.exitTime = 0.95f; t2.duration = 0.25f;
            var t3 = block.AddTransition(loco); t3.hasExitTime = true; t3.exitTime = 0.95f; t3.duration = 0.3f;
            var t4 = dive.AddTransition(getUp); t4.hasExitTime = true; t4.exitTime = 0.97f; t4.duration = 0.2f;
            var t5 = throwBall.AddTransition(loco); t5.hasExitTime = true; t5.exitTime = 0.92f; t5.duration = 0.25f;
            var t6 = puntBall.AddTransition(loco); t6.hasExitTime = true; t6.exitTime = 0.90f; t6.duration = 0.25f;
            var t7 = directWall.AddTransition(loco); t7.hasExitTime = true; t7.exitTime = 0.95f; t7.duration = 0.25f;
            var t8 = placeBall.AddTransition(loco); t8.hasExitTime = true; t8.exitTime = 0.95f; t8.duration = 0.2f;
            var t9 = missReact.AddTransition(loco); t9.hasExitTime = true; t9.exitTime = 0.95f; t9.duration = 0.25f;
            var t10 = slideSave.AddTransition(getUp); t10.hasExitTime = true; t10.exitTime = 0.92f; t10.duration = 0.2f;
            var t11 = waitIdle.AddTransition(loco); t11.hasExitTime = true; t11.exitTime = 0.95f; t11.duration = 0.25f;
            var t12 = punchJump.AddTransition(loco); t12.hasExitTime = true; t12.exitTime = 0.95f; t12.duration = 0.22f;

            EnableIK(ac);
            EditorUtility.SetDirty(ac);
            return ac;
        }

        // ------------------------------------------------------------------ prefabs

        static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static void AssignMaterial(GameObject go, Material m)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                int n = 1;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null && smr.sharedMesh != null) n = smr.sharedMesh.subMeshCount;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) n = mf.sharedMesh.subMeshCount;
                var arr = new Material[Mathf.Max(1, n)];
                for (int i = 0; i < arr.Length; i++) arr[i] = m;
                r.sharedMaterials = arr;
                if (smr != null) smr.updateWhenOffscreen = false;
            }
        }

        static GameObject CreateCharacterPrefab(string name, string modelName, float height, AnimatorController ctrl, Material mat, Type controller)
        {
            var modelAsset = Load<GameObject>(Root + "/Models/" + modelName + ".fbx");
            var root = new GameObject(name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Modelo";
            model.transform.SetParent(root.transform, false);
            var anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            if (anim.avatar == null)
            {
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Root + "/Models/" + modelName + ".fbx"))
                    if (o is Avatar) { anim.avatar = (Avatar)o; break; }
            }
            if (anim.avatar == null || !anim.avatar.isHuman)
                Debug.LogError("[Craquez 3D] O avatar de " + name + " não é Humanoid válido. Verifique o rig em Models/" + modelName + ".fbx > Rig.");
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            AssignMaterial(model, mat);

            // normaliza a altura e apoia os pés no chão
            Bounds b = RendererBounds(model);
            if (b.size.y > 0.01f)
            {
                float k = height / b.size.y;
                model.transform.localScale *= k;
                b = RendererBounds(model);
                model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            }
            // a caixa da malha pode sobrar abaixo dos pés (personagem "voando"): apoia pela altura real dos dedos
            if (anim.avatar != null && anim.avatar.isHuman)
            {
                var lt = anim.GetBoneTransform(HumanBodyBones.LeftToes);
                var rt = anim.GetBoneTransform(HumanBodyBones.RightToes);
                if (lt != null && rt != null)
                {
                    float sole = Mathf.Min(lt.position.y, rt.position.y) - 0.0265f * height;
                    model.transform.localPosition -= new Vector3(0f, sole - root.transform.position.y, 0f);
                    Debug.Log("[Craquez 3D] " + name + ": pés apoiados (ajuste " + sole.ToString("0.000") + " m)");
                }
            }
            model.AddComponent<IKRelay>();
            root.layer = Arena.LayerPlayerBody;
            var cap = root.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.radius = name.StartsWith("Goleiro") ? 0.25f : 0.24f;
            cap.height = height;
            cap.center = new Vector3(0f, height * 0.5f, 0f);
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            var sockets = root.AddComponent<MixamoBoneSockets>();
            sockets.InitializeSockets();
            root.AddComponent(controller);
            root.AddComponent<FootGrounder>().height = height;

            string path = Gen + "/Prefabs/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject CreateBallPrefab(Material mat)
        {
            var root = new GameObject("Bola");
            root.layer = Arena.LayerBall;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Root + "/Models/Bola.fbx"));
            model.name = "Modelo";
            model.transform.SetParent(root.transform, false);
            foreach (var c in model.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            AssignMaterial(model, mat);
            Bounds b = RendererBounds(model);
            float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (size > 1e-4f)
            {
                model.transform.localScale *= (Arena.BallRadius * 2f) / size;
                b = RendererBounds(model);
                model.transform.localPosition -= b.center;
            }
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = Arena.BallMass;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var sc = root.AddComponent<SphereCollider>();
            sc.radius = Arena.BallRadius;
            root.AddComponent<BallPhysics>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Gen + "/Prefabs/Bola.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ pós-processamento

        static VolumeProfile Profile(string name, float bloom, float exposure, float contrast, float saturation, Color filter, float vignette)
        {
            string path = Gen + "/Render/PP_" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);

            var tm = p.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            AssetDatabase.AddObjectToAsset(tm, p);

            var bl = p.Add<Bloom>(true);
            bl.threshold.Override(1.0f);
            bl.intensity.Override(bloom);
            bl.scatter.Override(0.65f);
            AssetDatabase.AddObjectToAsset(bl, p);

            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(exposure);
            ca.contrast.Override(contrast);
            ca.saturation.Override(saturation);
            ca.colorFilter.Override(filter);
            AssetDatabase.AddObjectToAsset(ca, p);

            var vg = p.Add<Vignette>(true);
            vg.intensity.Override(vignette);
            vg.smoothness.Override(0.4f);
            AssetDatabase.AddObjectToAsset(vg, p);

            EditorUtility.SetDirty(p);
            return p;
        }

        static Dictionary<string, VolumeProfile> CreateVolumeProfiles()
        {
            return new Dictionary<string, VolumeProfile>
            {
                { "PP_Sol", Profile("Sol", 0.35f, 0.1f, 12f, 8f, Color.white, 0.18f) },
                { "PP_Chuva", Profile("Chuva", 0.25f, 0.2f, 8f, -12f, new Color(0.95f, 0.98f, 1f), 0.2f) },
                { "PP_Tempestade", Profile("Tempestade", 0.4f, 0.35f, 10f, -20f, new Color(0.9f, 0.94f, 1f), 0.24f) },
            };
        }

        // ------------------------------------------------------------------ cena

        static void CreateScene(Dictionary<string, Material> mats, GameObject playerPrefab, GameObject keeperPrefab, GameObject ballPrefab,
                                Dictionary<string, VolumeProfile> profiles)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // sol
            var sunGo = new GameObject("Sol");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sunGo.transform.rotation = Quaternion.Euler(47f, -32f, 0f);

            // câmera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;
            cam.fieldOfView = 52f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(30f, 4f, -8f);
            camGo.transform.LookAt(Arena.AttackGoalCenter);
            try
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = true;
            }
            catch (Exception e) { Debug.LogWarning("[Craquez 3D] Dados URP da câmera: " + e.Message); }

            // estádio (entorno do modelo do rascunho — o gramado/gols reais são gerados em tempo de execução)
            var stadiumAsset = Load<GameObject>(Root + "/Models/Estadio.fbx");
            if (stadiumAsset != null)
            {
                var st = (GameObject)PrefabUtility.InstantiatePrefab(stadiumAsset);
                st.name = "Estadio";
                AssignMaterial(st, mats["Estadio"]);
                Bounds b = RendererBounds(st);
                // garante "Y para cima" e o comprimento do campo no eixo X
                if (b.size.y > b.size.z * 1.2f) { st.transform.rotation = Quaternion.Euler(-90f, 0f, 0f) * st.transform.rotation; b = RendererBounds(st); }
                if (b.size.z > b.size.x) { st.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * st.transform.rotation; b = RendererBounds(st); }
                const float expectedLength = 127.8f; // medido no Blender
                if (Mathf.Abs(b.size.x - expectedLength) > 8f && b.size.x > 0.01f)
                {
                    Debug.LogWarning("[Craquez 3D] Escala do estádio ajustada de " + b.size.x.ToString("0.0") + " m.");
                    st.transform.localScale *= expectedLength / b.size.x;
                }
                st.transform.position = new Vector3(0f, -0.05f, 0f);
                foreach (var r in st.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            }

            // pós-processamento por clima
            var ppRoot = new GameObject("PosProcessamento");
            foreach (var kv in profiles)
            {
                var go = new GameObject(kv.Key);
                go.transform.SetParent(ppRoot.transform, false);
                var v = go.AddComponent<Volume>();
                v.isGlobal = true;
                v.sharedProfile = kv.Value;
                go.SetActive(kv.Key == "PP_Sol");
            }

            // reflexos (poças, gramado molhado)
            var probeGo = new GameObject("SondaReflexo");
            probeGo.transform.position = new Vector3(20f, 3f, 0f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.size = new Vector3(260f, 80f, 200f);
            probe.resolution = 128;
            probe.hdr = true;

            RenderSettings.skybox = mats["Ceu"];
            RenderSettings.sun = sun;

            // bootstrap
            var boot = new GameObject("ArenaBootstrap").AddComponent<ArenaBootstrap>();
            boot.playerPrefab = playerPrefab;
            boot.keeperPrefab = keeperPrefab;
            boot.ballPrefab = ballPrefab;
            boot.grassLight = mats["GramaClara"];
            boot.grassDark = mats["GramaEscura"];
            boot.lines = mats["Linhas"];
            boot.puddle = mats["Poca"];
            boot.soaked = mats["Encharcado"];
            boot.ripple = mats["Anel"];
            boot.net = mats["Rede"];
            boot.post = mats["Trave"];
            boot.rain = mats["Chuva"];
            boot.splash = mats["Respingo"];
            boot.droplet = mats["Borrifo"];
            boot.clouds = mats["Nuvens"];
            boot.skybox = mats["Ceu"];
            boot.fxDust = mats["FxPoeira"];
            boot.fxSmoke = mats["FxFumaca"];
            boot.fxStar = mats["FxEstrela"];
            boot.sun = sun;
            boot.mainCamera = cam;
            boot.postProcessRoot = ppRoot.transform;
            boot.reflectionProbe = probe;
            boot.uiCircle = T("UI_Circulo");
            boot.logo = T("Icone/Logo_Craquez");
            boot.introClip = AssetDatabase.LoadAssetAtPath<VideoClip>(IntroVideo);
            if (boot.introClip == null) Debug.LogWarning("[Craquez 3D] Vídeo de abertura não encontrado em " + IntroVideo);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>Prefabs e uniformes do modo Partida 11 x 11 ligados ao bootstrap da cena.</summary>
        static void LinkMatchAssets(GameObject playerB, GameObject keeperA, GameObject referee, GameObject refereeB)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var boot = UnityEngine.Object.FindAnyObjectByType<ArenaBootstrap>();
            if (boot == null) { Debug.LogError("[Craquez 3D] ArenaBootstrap não encontrado na cena."); return; }
            boot.playerPrefabB = playerB;
            boot.keeperPrefabA = keeperA;
            boot.refereePrefab = referee;
            boot.refereePrefabB = refereeB;
            // uniformes iguais para o time inteiro: casa azul, visitante branco (tools/make_kits.py)
            boot.kitA_Casa = T("Uniformes/A_Casa");
            boot.kitB_Casa = boot.kitA_Casa;
            boot.kitA_Visitante = T("Uniformes/A_Visitante");
            boot.kitB_Visitante = boot.kitA_Visitante;
            boot.kitB_GoleiroCasa = T("Goleiro_Albedo");
            boot.kitA_GoleiroVisitante = T("Uniformes/B_GoleiroVisitante");
            boot.kitA_Arbitro = T("Uniformes/A_Arbitro");
            boot.kitB_Arbitro = boot.kitA_Arbitro;
            EditorUtility.SetDirty(boot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ------------------------------------------------------------------ player settings

        /// <summary>Versão única do projeto: arquivo VERSION na raiz do repositório.</summary>
        public static string ReadVersion()
        {
            try
            {
                string p = Path.Combine(Directory.GetCurrentDirectory(), "VERSION");
                if (File.Exists(p)) return File.ReadAllText(p).Trim();
            }
            catch (Exception) { }
            return "0.1.0";
        }

        public static int VersionCode(string v)
        {
            var parts = v.Split('.');
            int major = 0, minor = 0, patch = 0;
            if (parts.Length > 0) int.TryParse(parts[0], out major);
            if (parts.Length > 1) int.TryParse(parts[1], out minor);
            if (parts.Length > 2) int.TryParse(parts[2], out patch);
            return major * 10000 + minor * 100 + patch;
        }

        public static void SetupPlayerSettings()
        {
            string version = ReadVersion();
            PlayerSettings.companyName = "Craquez";
            PlayerSettings.productName = "Craquez 3D";
            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = Mathf.Max(1, VersionCode(version));
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.craquez.craquez3d");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.craquez.craquez3d");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            // o jogo cria componentes só em tempo de execução (CreatePrimitive, AddComponent):
            // não deixar o build remover partes da engine
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);
            SetupIcons();
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
#if UNITY_2023_1_OR_NEWER
            // o atualizador usa UnityPlayer.currentActivity
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
#endif
        }

        const string IconDir = Root + "/Textures/Icone/";

        /// <summary>Ícone do app (capa do vídeo de abertura): ícone padrão + ícones adaptativos do Android 8+.</summary>
        public static void SetupIcons()
        {
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "Icone_App.png");
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "Icone_Adaptativo_Fundo.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "Icone_Adaptativo_Frente.png");
            if (full == null) { Debug.LogWarning("[Craquez 3D] Ícone não encontrado em " + IconDir); return; }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);

            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var icon in icons)
                {
                    if (icon.maxLayerCount >= 2 && bg != null && fg != null) icon.SetTextures(bg, fg);
                    else icon.SetTexture(full, 0);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
            Debug.Log("[Craquez 3D] Ícone do app configurado.");
        }
    }

    /// <summary>Executa o setup automaticamente na primeira vez que o projeto é aberto.</summary>
    [InitializeOnLoad]
    static class CraquezAutoSetup
    {
        static CraquezAutoSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists("Assets/Craquez/Scenes/ArenaTreino.unity")) return;
                if (SessionState.GetBool("craquez.setup.ran", false)) return;
                SessionState.SetBool("craquez.setup.ran", true);
                CraquezSetup.Run();
            };
        }
    }

    /// <summary>Mantém a versão do app sincronizada com o arquivo VERSION a cada build.</summary>
    public class CraquezBuildVersion : UnityEditor.Build.IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }
        public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
        {
            string v = CraquezSetup.ReadVersion();
            PlayerSettings.bundleVersion = v;
            PlayerSettings.Android.bundleVersionCode = Mathf.Max(1, CraquezSetup.VersionCode(v));
        }
    }
}
