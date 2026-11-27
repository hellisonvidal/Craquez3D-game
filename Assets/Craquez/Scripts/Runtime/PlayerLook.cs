using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    public enum Porte { Magro = 0, Normal = 1, Forte = 2, Pesado = 3 }
    /// <summary>Cortes de cabelo: malhas 3D ajustadas ao crânio (Cabelo_&lt;nome&gt; no FBX, tools/blender/cabeca_cabelos.py).</summary>
    public enum Penteado { Curto = 0, BlackPower = 1, Moicano = 2, Topete = 3, Raspado = 4, Cacheado = 5, Careca = 6 }

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
        public string rostoId;      // foto do rosto (Resources/Rostos/<modelo>/Rosto_<id>)
        public string modeloRosto = "Jogador";

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
        static bool DarkSkin(bool modelB, int visual) { return visual == 1 || visual == 5 || visual == 7; } // visuais de pele escura do corpo

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
            // penteado (black power e cacheado mais comuns em quem tem pele escura)
            double ph = rng.NextDouble();
            bool dark = DarkSkin(modelB, visual);
            if (dark) a.penteado = ph < 0.22 ? Penteado.BlackPower : ph < 0.42 ? Penteado.Cacheado : ph < 0.62 ? Penteado.Raspado : ph < 0.8 ? Penteado.Curto : ph < 0.9 ? Penteado.Moicano : Penteado.Careca;
            else a.penteado = ph < 0.36 ? Penteado.Curto : ph < 0.56 ? Penteado.Topete : ph < 0.72 ? Penteado.Raspado : ph < 0.84 ? Penteado.Cacheado : ph < 0.92 ? Penteado.Moicano : ph < 0.97 ? Penteado.Careca : Penteado.BlackPower;
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
            // Escala SEMPRE uniforme na raiz: antes a largura (porte) ia só em X/Z do corpo inteiro e os ossos que giram
            // (braços, pernas, tronco inclinado) eram esticados ou achatados conforme o ângulo, deformando a postura.
            // O porte agora alarga só o tronco (escala uniforme da coluna), com pescoço, cabeça e braços compensados suavemente.
            float w = a.Largura;
            float torso = 1f + (w - 1f) * 0.6f;           // ±8% no tronco já mostra magro/forte/pesado
            const float torsoShare = 0.28f;               // fração da altura entre a coluna e o pescoço
            float k = a.altura / prefabHeight / (1f + torsoShare * (torso - 1f));
            go.transform.localScale = Vector3.one * k;

            var cap = go.GetComponent<CapsuleCollider>();
            if (cap != null)
            {
                float baseR = go.GetComponent<GoalkeeperAI>() != null ? 0.25f : 0.24f;
                cap.radius = baseR * Mathf.Lerp(1f, w, 0.65f);
            }

            var anim = go.GetComponentInChildren<Animator>();
            if (anim == null || !anim.isHuman) return;
            Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
            Transform spine = anim.GetBoneTransform(HumanBodyBones.Spine);
            if (spine != null && Mathf.Abs(torso - 1f) > 0.005f)
            {
                spine.localScale = spine.localScale * torso;
                float inv = 1f / torso;
                foreach (var hb in new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm })
                {
                    var arm = anim.GetBoneTransform(hb);
                    if (arm != null) arm.localScale = arm.localScale * inv; // braços com o comprimento normal
                }
                if (head != null)
                {
                    // Mantém Neck com a mesma escala de Spine2 para selar 100% o colarinho e compensa suavemente na cabeça
                    head.localScale = head.localScale * Mathf.Lerp(1f, inv, 0.82f);
                }
            }
            if (head == null) return;
            AplicarCabeca(go, a);
        }

        // ------------------------------------------------------------ rosto (foto) e cabelo 3D

        [System.Serializable] public class Rosto { public string id; public int visual; public float[] pele; public float[] cabelo; }
        [System.Serializable] class Banco { public Rosto[] rostos; }
        static readonly Dictionary<string, Rosto[]> bancos = new Dictionary<string, Rosto[]>();
        static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap"), BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>Rostos disponíveis para o modelo ("Jogador" ou "Goleiro"), gerados por tools/rostos.py.</summary>
        public static Rosto[] Rostos(string modelo)
        {
            Rosto[] r;
            if (bancos.TryGetValue(modelo, out r)) return r;
            var ta = Resources.Load<TextAsset>("Rostos/" + modelo + "/Rosto");
            r = ta != null ? (JsonUtility.FromJson<Banco>(ta.text).rostos ?? new Rosto[0]) : new Rosto[0];
            bancos[modelo] = r;
            return r;
        }

        /// <summary>Rostos embaralhados (sem repetir dentro do time). soClaros: só tons do corpo que existem no uniforme sem variações (goleiro, árbitro).</summary>
        public static List<Rosto> Embaralhar(string modelo, System.Random rng, bool soClaros)
        {
            var l = new List<Rosto>();
            foreach (var r in Rostos(modelo)) if (!soClaros || !DarkSkin(false, r.visual)) l.Add(r);
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var x = l[i]; l[i] = l[j]; l[j] = x; }
            return l;
        }

        /// <summary>Usa o rosto escolhido na aparência (visual do corpo = tom de pele do rosto; cor do cabelo da foto).</summary>
        public static void UsarRosto(Aparencia a, Rosto r, string modelo)
        {
            if (r == null) return;
            a.rostoId = r.id;
            a.modeloRosto = modelo;
            a.visual = r.visual;
            if (r.cabelo != null && r.cabelo.Length >= 3) a.corCabelo = new Color(r.cabelo[0], r.cabelo[1], r.cabelo[2]);
            a.barba = "";
        }

        /// <summary>Renderer do corpo (recebe o uniforme)? A cabeça e os cabelos têm material próprio.</summary>
        public static bool EhCorpo(Renderer r)
        {
            string n = r.name;
            return n != "Cabeca" && !n.StartsWith("Cabelo_") && !n.StartsWith("Penteado_");
        }

        /// <summary>Foto do rosto na cabeça e o corte de cabelo escolhido (as demais malhas de cabelo ficam desligadas).</summary>
        public static void AplicarCabeca(GameObject go, Aparencia a)
        {
            Texture2D face = string.IsNullOrEmpty(a.rostoId) ? null : Resources.Load<Texture2D>("Rostos/" + a.modeloRosto + "/Rosto_" + a.rostoId);
            string corte = "Cabelo_" + a.penteado;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "Cabeca")
                {
                    if (face == null) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetTexture(BaseMap, face);
                    r.SetPropertyBlock(mpb);
                    mpb.Clear();
                }
                else if (r.name.StartsWith("Cabelo_"))
                {
                    bool on = r.name == corte;
                    r.enabled = on;
                    if (!on) continue;
                    // cor do cabelo (a faixa branca do estilo antigo virou só cor clara)
                    Color c = a.corCabelo.maxColorComponent > 0.001f ? a.corCabelo : new Color(0.1f, 0.075f, 0.06f);
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor(BaseColor, c * 1.6f);   // a textura de fios é clara (~0,65): compensa para chegar na cor da foto
                    r.SetPropertyBlock(mpb);
                    mpb.Clear();
                }
            }
        }
    }
}
