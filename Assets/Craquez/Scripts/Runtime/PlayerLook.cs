using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    public enum Porte { Magro = 0, Normal = 1, Forte = 2, Pesado = 3 }
    public enum Penteado { Natural = 0, BlackPower = 1, Moicano = 2, Coque = 3, Faixa = 4 }

    /// <summary>Aparência individual: rosto/pele/cabelo (textura), altura, porte, peso e penteado.</summary>
    [System.Serializable]
    public class Aparencia
    {
        public int visual;          // 0..3: combinação de pele e cabelo (texturas em Resources/Visuais)
        public float altura;        // metros
        public Porte porte;
        public float peso;          // kg
        public Penteado penteado;
        public Color corCabelo;
        public string barba;
        public string rosto;        // formato do rosto (FaceShapes)

        public string Descricao()
        {
            return altura.ToString("0.00").Replace('.', ',') + " m  •  " + Mathf.RoundToInt(peso) + " kg  •  " + NomePorte(porte);
        }

        public static string NomePorte(Porte p)
        {
            switch (p)
            {
                case Porte.Magro: return "Magro";
                case Porte.Forte: return "Forte";
                case Porte.Pesado: return "Pesado";
                default: return "Normal";
            }
        }

        /// <summary>Largura relativa do corpo (ombros, tronco, membros).</summary>
        public float Largura
        {
            get
            {
                switch (porte)
                {
                    case Porte.Magro: return 0.93f;
                    case Porte.Forte: return 1.07f;
                    case Porte.Pesado: return 1.13f;
                    default: return 1f;
                }
            }
        }
    }

    /// <summary>
    /// Monta a aparência de cada jogador: sorteia altura (pela posição), porte e peso, escolhe a textura de pele e cabelo
    /// e cria o penteado 3D preso ao osso da cabeça. Os dois rostos (modelos A e B) se alternam no elenco;
    /// com 4 combinações de pele/cabelo em cada um, 5 penteados, alturas de 1,66 m a 1,96 m e 4 portes,
    /// cada jogador sai diferente.
    /// </summary>
    public static class PlayerLook
    {
        // cor do cabelo de cada visual (mesma ordem de tools/make_kits.py)
        public const int Visuais = 8;
        static readonly Color[] HairA =
        {
            new Color(0.16f, 0.11f, 0.08f), new Color(0.05f, 0.04f, 0.035f), new Color(0.035f, 0.03f, 0.028f), new Color(0.78f, 0.62f, 0.32f),
            new Color(0.2f, 0.13f, 0.08f), new Color(0.05f, 0.04f, 0.035f), new Color(0.035f, 0.03f, 0.028f), new Color(0.55f, 0.16f, 0.08f)
        };
        static readonly Color[] HairB =
        {
            new Color(0.045f, 0.04f, 0.04f), new Color(0.03f, 0.03f, 0.03f), new Color(0.42f, 0.26f, 0.14f), new Color(0.55f, 0.16f, 0.08f),
            new Color(0.045f, 0.04f, 0.04f), new Color(0.03f, 0.03f, 0.03f), new Color(0.78f, 0.62f, 0.32f), new Color(0.045f, 0.04f, 0.04f)
        };
        // barba de cada visual (mesma ordem de tools/make_kits.py)
        static readonly string[] BeardA = { "barba cheia", "barba cheia", "cavanhaque", "sem barba", "sem barba", "bigode", "sem barba", "barba por fazer" };
        static readonly string[] BeardB = { "barba cheia", "barba cheia", "sem barba", "cavanhaque", "sem barba", "bigode", "barba por fazer", "cavanhaque" };
        public static string Barba(bool modelB, int visual) { return (modelB ? BeardB : BeardA)[Mathf.Clamp(visual, 0, Visuais - 1)]; }
        // visuais de pele escura (o black power aparece mais neles)
        static bool DarkSkin(bool modelB, int visual) { return modelB ? (visual == 0 || visual == 1 || visual == 4 || visual == 5 || visual == 7) : (visual == 2 || visual == 6); }

        // medidas da cabeça na pose de repouso, em fração da altura do modelo (medidas no Blender)
        struct HeadShape { public float top, fwd, rx, rz; }
        static readonly HeadShape HeadA = new HeadShape { top = 0.1177f, fwd = 0.0259f, rx = 0.0523f, rz = 0.0675f };
        static readonly HeadShape HeadB = new HeadShape { top = 0.1378f, fwd = 0.0012f, rx = 0.0478f, rz = 0.0604f };

        public static Aparencia Sortear(Funcao f, bool modelB, int visual, Atributos atr, System.Random rng)
        {
            float lo, hi;
            switch (f)
            {
                case Funcao.Goleiro: lo = 1.84f; hi = 1.97f; break;
                case Funcao.Zagueiro: lo = 1.80f; hi = 1.95f; break;
                case Funcao.Lateral: lo = 1.69f; hi = 1.82f; break;
                case Funcao.Volante: lo = 1.74f; hi = 1.88f; break;
                case Funcao.Atacante: lo = 1.74f; hi = 1.92f; break;
                case Funcao.Ponta: lo = 1.66f; hi = 1.78f; break;
                default: lo = 1.68f; hi = 1.83f; break;
            }
            var a = new Aparencia();
            a.visual = visual;
            a.altura = Mathf.Lerp(lo, hi, (float)(rng.NextDouble() * 0.5 + rng.NextDouble() * 0.5));
            // porte: força puxa para forte/pesado, velocidade para magro
            double r = rng.NextDouble() + (atr.forca - 0.6f) * 0.8 - (atr.velocidade - 0.6f) * 0.5;
            a.porte = r < 0.22 ? Porte.Magro : (r < 0.7 ? Porte.Normal : (r < 1.0 ? Porte.Forte : Porte.Pesado));
            float bmi = a.porte == Porte.Magro ? 20.6f : (a.porte == Porte.Normal ? 22.6f : (a.porte == Porte.Forte ? 24.4f : 26.8f));
            a.peso = bmi * a.altura * a.altura + (float)(rng.NextDouble() * 3.0 - 1.5);
            // penteado
            double ph = rng.NextDouble();
            bool dark = DarkSkin(modelB, visual);
            if (ph < 0.42) a.penteado = Penteado.Natural;
            else if (ph < (dark ? 0.72 : 0.52)) a.penteado = Penteado.BlackPower;
            else if (ph < (dark ? 0.82 : 0.68)) a.penteado = Penteado.Moicano;
            else if (ph < (dark ? 0.9 : 0.84)) a.penteado = Penteado.Coque;
            else a.penteado = Penteado.Faixa;
            a.corCabelo = (modelB ? HairB : HairA)[Mathf.Clamp(visual, 0, Visuais - 1)];
            a.barba = Barba(modelB, visual);
            return a;
        }

        /// <summary>Atributos ajustados ao corpo: altos cabeceiam melhor, pesados são mais fortes e mais lentos.</summary>
        public static void AjustarAtributos(Atributos atr, Aparencia a)
        {
            float tall = (a.altura - 1.78f) / 0.15f;
            atr.cabeceio = Mathf.Clamp(atr.cabeceio + tall * 0.08f, 0.2f, 0.98f);
            switch (a.porte)
            {
                case Porte.Magro: atr.forca -= 0.06f; atr.aceleracao += 0.04f; atr.equilibrio -= 0.03f; break;
                case Porte.Forte: atr.forca += 0.07f; atr.equilibrio += 0.04f; break;
                case Porte.Pesado: atr.forca += 0.1f; atr.velocidade -= 0.06f; atr.aceleracao -= 0.07f; atr.equilibrio += 0.05f; atr.resistencia -= 0.04f; break;
            }
            atr.forca = Mathf.Clamp(atr.forca, 0.2f, 0.98f); atr.aceleracao = Mathf.Clamp(atr.aceleracao, 0.2f, 0.98f);
            atr.equilibrio = Mathf.Clamp(atr.equilibrio, 0.2f, 0.98f); atr.velocidade = Mathf.Clamp(atr.velocidade, 0.2f, 0.98f);
            atr.resistencia = Mathf.Clamp(atr.resistencia, 0.2f, 0.98f);
        }

        /// <summary>Textura de pele/cabelo do jogador ("A_Casa_2" etc.); 0 = a textura base do time.</summary>
        public static Texture2D Textura(bool modelB, int teamIndex, int visual, Texture2D baseKit)
        {
            if (visual <= 0) return baseKit;
            string key = (modelB ? "B_" : "A_") + (teamIndex == 0 ? "Casa" : "Visitante") + "_" + visual;
            var t = Resources.Load<Texture2D>("Visuais/" + key);
            return t != null ? t : baseKit;
        }

        /// <summary>
        /// Aplica altura e porte (escala do corpo, cabeça compensada) e cria o penteado.
        /// prefabHeight: altura em que o prefab foi normalizado. Chamar logo após instanciar (pose de repouso).
        /// </summary>
        public static void Aplicar(GameObject go, Aparencia a, float prefabHeight, bool modelB, Material propBase, Dictionary<Color, Material> hairMats)
        {
            float k = a.altura / prefabHeight;
            float w = a.Largura;
            go.transform.localScale = new Vector3(k * w, k, k * w);
            var anim = go.GetComponentInChildren<Animator>();
            if (anim == null || !anim.isHuman) return;
            Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return;
            // cabeça proporcional: corpos mais largos não ganham cabeça "inchada"
            float comp = 1f / Mathf.Sqrt(w);
            head.localScale = head.localScale * comp;
            if (a.penteado == Penteado.Natural || propBase == null) return;

            HeadShape hs = modelB ? HeadB : HeadA;
            float H = a.altura * comp;
            float s = H / 1.8f;
            Transform root = go.transform;
            Vector3 up = root.up, fwd = root.forward, right = root.right;
            Vector3 crown = head.position + up * hs.top * H + fwd * hs.fwd * H;
            float rx = hs.rx * H * w, rz = hs.rz * H * w;

            Material mat;
            Color c = a.penteado == Penteado.Faixa ? new Color(0.95f, 0.95f, 0.95f) : a.corCabelo;
            if (!hairMats.TryGetValue(c, out mat))
            {
                mat = new Material(propBase);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", a.penteado == Penteado.Faixa ? 0.2f : 0.35f);
                hairMats[c] = mat;
            }
            Quaternion rot = root.rotation;
            switch (a.penteado)
            {
                case Penteado.BlackPower:
                    Prop(PrimitiveType.Sphere, head, crown - up * 0.05f * s - fwd * 0.04f * s, rot, new Vector3(rx * 2.5f, 0.2f * s, rz * 2.35f), mat, "BlackPower");
                    break;
                case Penteado.Moicano:
                    Prop(PrimitiveType.Cube, head, crown - up * 0.022f * s - fwd * 0.015f * s, rot, new Vector3(0.045f * s, 0.06f * s, rz * 1.6f), mat, "Moicano");
                    break;
                case Penteado.Coque:
                    Prop(PrimitiveType.Sphere, head, crown - up * 0.04f * s - fwd * rz * 0.95f, rot, Vector3.one * 0.085f * s, mat, "Coque");
                    break;
                case Penteado.Faixa:
                    Prop(PrimitiveType.Cylinder, head, crown - up * 0.07f * s, rot, new Vector3(rx * 1.98f, 0.018f * s, rz * 1.98f), mat, "Faixa");
                    break;
            }
        }

        static void Prop(PrimitiveType t, Transform parent, Vector3 pos, Quaternion rot, Vector3 worldScale, Material mat, string name)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = "Penteado_" + name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = worldScale;
            go.transform.SetParent(parent, true);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
