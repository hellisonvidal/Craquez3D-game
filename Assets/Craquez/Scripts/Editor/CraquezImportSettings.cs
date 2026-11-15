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
        public const int Version = 11;

        static readonly string[] LoopClips =
        {
            "Idle", "Andar", "Trote", "Correr", "Disparada", "GK_Pronto", "GK_DeslocarEsq", "GK_DeslocarDir", "GK_Caido"
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

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root + "Animations/")) return;
            var mi = (ModelImporter)assetImporter;
            string file = Path.GetFileNameWithoutExtension(assetPath);
            bool loop = System.Array.IndexOf(LoopClips, file) >= 0;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                c.name = clips.Length == 1 ? file : file + "_" + i;
                c.loopTime = loop;
                c.loopPose = loop;
                // Rotação, altura e deslocamentos horizontais travados (In-Place puro) para evitar desalinhamento com a cápsula
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = false;
                c.heightFromFeet = true; // altura do corpo calculada a partir dos pés: nada de "flutuar"
                clips[i] = c;
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            string f = Path.GetFileNameWithoutExtension(assetPath);
            ti.maxTextureSize = f == "Estadio_Albedo" ? 4096 : 2048;
            ti.wrapMode = TextureWrapMode.Clamp;

            if (f.EndsWith("_Normal"))
            {
                ti.textureType = TextureImporterType.NormalMap;
            }
            if (f.EndsWith("_MetallicSmoothness"))
            {
                ti.sRGBTexture = false;
                ti.maxTextureSize = 1024;
            }
            if (f.StartsWith("Grama") || f.StartsWith("Poca") || f == "Rede" || f == "Nuvens")
            {
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 8;
                ti.mipmapEnabled = true;
            }
            if (f == "Chuva_Gota" || f == "Respingo" || f == "Rede" || f == "Nuvens" || f == "UI_Circulo" || f == "Anel")
            {
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
            }
            if (f == "UI_Circulo")
            {
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
            }
            if (f == "Rede") ti.mipmapEnabled = true;
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
