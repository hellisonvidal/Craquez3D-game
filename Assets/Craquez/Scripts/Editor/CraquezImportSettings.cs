using System.IO;
using UnityEditor;
using UnityEngine;

namespace Craquez.EditorTools
{
    /// <summary>
    /// Configura automaticamente a importação dos assets do Craquez 3D:
    /// - Jogador/Goleiro: rig Humanoid (ossos Mixamo mapeados automaticamente => qualquer animação humanoide serve);
    /// - Animações Mixamo: Humanoid, "in place", com loop nas de locomoção;
    /// - Texturas: normal maps, metallic/smoothness linear, tamanhos para Android.
    /// </summary>
    public class CraquezImportSettings : AssetPostprocessor
    {
        const string Root = "Assets/Craquez/";
        public const int Version = 21;

        static readonly string[] LoopClips =
        {
            "Idle", "Andar", "Trote", "Correr", "Disparada", "GK_Pronto", "GK_DeslocarEsq", "GK_DeslocarDir", "GK_Andar", "GK_Correr", "GK_Recuar", "GK_Espera", "GK_Caido"
        };

        // Clipes de ação vertical/solo do goleiro: preservam a altura Y real do salto/mergulho/queda sem achatar nos pés
        static readonly string[] FreeHeightClips =
        {
            "GK_Salto", "GK_Mergulho", "GK_Soco", "GK_Caido", "GK_Levantar"
        };

        public override uint GetVersion() { return Version; }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var mi = (ModelImporter)assetImporter;
            string file = Path.GetFileNameWithoutExtension(assetPath);
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.importVisibility = false;
            // mantém o nó do esqueleto mesmo em arquivos só com animação: a hierarquia precisa ser igual
            // à do modelo para reaproveitar o avatar do personagem (CraquezSetup.LinkClipAvatars)
            mi.preserveHierarchy = true;

            if (assetPath.Contains("/Models/"))
            {
                if (file == "Jogador" || file == "Goleiro")
                {
                    mi.animationType = ModelImporterAnimationType.Human;
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    mi.importAnimation = false;
                    mi.optimizeGameObjects = false;
                    mi.skinWeights = ModelImporterSkinWeights.Standard;
                    mi.meshCompression = ModelImporterMeshCompression.Off;
                    mi.importNormals = ModelImporterNormals.Import;
                    mi.importTangents = ModelImporterTangents.CalculateMikk;
                    mi.optimizeMeshPolygons = true;
                    mi.optimizeMeshVertices = true;
                    mi.isReadable = true; // formato do rosto: a partida lê os vértices da cabeça (FaceShapes)
                }
                else
                {
                    mi.animationType = ModelImporterAnimationType.None;
                    mi.importAnimation = false;
                }
                if (file == "Estadio")
                {
                    mi.meshCompression = ModelImporterMeshCompression.Medium;
                    mi.isReadable = false;
                }
            }
            else if (assetPath.Contains("/Animations/Quaternius/"))
            {
                // Universal Animation Library (Quaternius, CC0): esqueleto do manequim UE, vários clipes por arquivo
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.bakeAxisConversion = true;
                mi.importAnimation = true;
                mi.optimizeGameObjects = false;
                mi.animationCompression = ModelImporterAnimationCompression.Optimal;
            }
            else if (assetPath.Contains("/Animations/"))
            {
                mi.animationType = ModelImporterAnimationType.Human;
                // clipes ligados ao avatar do personagem (CraquezSetup.LinkClipAvatars) mantêm a ligação
                if (!(mi.avatarSetup == ModelImporterAvatarSetup.CopyFromOther && mi.sourceAvatar != null))
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
                mi.optimizeGameObjects = false;
                mi.animationCompression = ModelImporterAnimationCompression.Optimal;
            }
        }

        /// <summary>Clipes da Quaternius em que o corpo sai do chão (queda, carrinho, levantar): mantêm a altura real.</summary>
        static readonly string[] QuaterniusFreeHeight = { "Death01", "LayToIdle", "Slide_Start", "Slide_Loop", "Slide_Exit", "Hit_Knockback", "Roll", "Jump_Start", "Jump_Loop", "Jump_Land" };

        public static string TakeName(string n)
        {
            int i = n.LastIndexOf('|');
            return i >= 0 ? n.Substring(i + 1) : n;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root + "Animations/")) return;
            var mi = (ModelImporter)assetImporter;
            if (assetPath.Contains("/Quaternius/"))
            {
                var takes = mi.defaultClipAnimations;
                if (takes == null || takes.Length == 0) return;
                for (int i = 0; i < takes.Length; i++)
                {
                    var c = takes[i];
                    string n = TakeName(c.takeName);
                    bool loopQ = n.EndsWith("_Loop");
                    bool free = System.Array.IndexOf(QuaterniusFreeHeight, n) >= 0;
                    c.name = n;
                    c.loopTime = loopQ;
                    c.loopPose = loopQ;
                    c.lockRootRotation = true;
                    c.keepOriginalOrientation = true;
                    c.lockRootHeightY = !free;
                    c.keepOriginalPositionY = true;
                    c.lockRootPositionXZ = true;
                    c.keepOriginalPositionXZ = false;
                    c.heightFromFeet = !free;
                    takes[i] = c;
                }
                mi.clipAnimations = takes;
                return;
            }
            string file = Path.GetFileNameWithoutExtension(assetPath);
            bool loop = System.Array.IndexOf(LoopClips, file) >= 0;
            bool freeHeight = System.Array.IndexOf(FreeHeightClips, file) >= 0;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                c.name = clips.Length == 1 ? file : file + "_" + i;
                c.loopTime = loop;
                c.loopPose = loop;
                // Rotação e deslocamentos horizontais travados (In-Place) para evitar desalinhamento com a cápsula.
                // Nos clipes de salto/mergulho/queda (FreeHeightClips), preserva a curva vertical real (RootT.y).
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = false;
                c.heightFromFeet = !freeHeight;
                clips[i] = c;
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            string f = Path.GetFileNameWithoutExtension(assetPath);
            bool visual = assetPath.Contains("/Visuais/");          // pele/cabelo por jogador (1024)
            bool kit = assetPath.Contains("/Uniformes/") || visual;
            bool isCharTex = f.StartsWith("Jogador_") || f.StartsWith("Goleiro_") || kit;
            ti.maxTextureSize = visual ? 1024 : (kit ? 2048 : ((f == "Estadio_Albedo" || isCharTex) ? 4096 : 2048));
            ti.wrapMode = TextureWrapMode.Clamp;

            if (isCharTex)
            {
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.anisoLevel = 8;
                ti.mipmapEnabled = true;
            }
            if (f.EndsWith("_Normal"))
            {
                ti.textureType = TextureImporterType.NormalMap;
            }
            if (f.EndsWith("_MetallicSmoothness"))
            {
                ti.sRGBTexture = false;
                ti.maxTextureSize = isCharTex ? 4096 : 1024;
            }
            if (f.StartsWith("Grama") || f == "Poca_Normal" || f == "Rede" || f == "Nuvens")
            {
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 8;
                ti.mipmapEnabled = true;
            }
            if (f == "Poca_Mascara" || f == "Chuva_Gota" || f == "Respingo" || f == "Rede" || f == "Nuvens" || f == "UI_Circulo" || f == "Anel")
            {
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
            }
            if (f == "Poca_Mascara")
            {
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.anisoLevel = 8;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
            }
            if (f == "UI_Circulo")
            {
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
            }
            if (f == "Rede") ti.mipmapEnabled = true;
            if (assetPath.Contains("/Rostos/"))
            {
                // rostos: a UV da cabeça dá a volta (costura atrás) -> repete em U; 512 px bastam
                ti.maxTextureSize = 512;
                ti.wrapModeU = TextureWrapMode.Repeat;
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 4;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
            }
            if (f.StartsWith("Cabelo_"))
            {
                ti.maxTextureSize = 512;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 4;
                ti.mipmapEnabled = true;
            }
            if (f.StartsWith("Icone_") || f.StartsWith("Logo_"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.maxTextureSize = 1024;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
            }
        }
    }
}
