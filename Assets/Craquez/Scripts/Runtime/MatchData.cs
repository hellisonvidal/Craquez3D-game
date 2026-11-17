using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Craquez
{
    /// <summary>Função tática de cada jogador na formação.</summary>
    public enum Funcao { Goleiro, Zagueiro, Lateral, Volante, Meia, MeiaAberto, Ponta, Atacante }

    /// <summary>Atributos técnicos e físicos (0 a 1).</summary>
    [Serializable]
    public class Atributos
    {
        public float velocidade, aceleracao, resistencia, forca, equilibrio;
        public float drible, passe, finalizacao, desarme, cabeceio, visao, posicionamento;

        public float Media
        {
            get { return (velocidade + aceleracao + resistencia + forca + equilibrio + drible + passe + finalizacao + desarme + cabeceio + visao + posicionamento) / 12f; }
        }
    }

    /// <summary>
    /// Mentalidade: o jeito único de cada jogador pensar o jogo.
    /// Define quando ele arrisca, quanto ajuda o time, se prefere passar ou finalizar, se respeita a posição.
    /// </summary>
    [Serializable]
    public class Mentalidade
    {
        [Tooltip("Vontade de atacar, de se infiltrar e de buscar o gol.")] public float ofensividade;
        [Tooltip("Gosto por jogadas arriscadas: drible, passe de ruptura, chute de longe.")] public float criatividade;
        [Tooltip("Respeito à posição e às ordens táticas.")] public float disciplina;
        [Tooltip("Intensidade na marcação, botes e trombadas (e mais faltas).")] public float agressividade;
        [Tooltip("Espírito de equipe: apoio ao companheiro, passe em vez de chute egoísta, recomposição, comemoração junto.")] public float espiritoEquipe;
        [Tooltip("Frieza sob pressão: decide melhor quando é cercado.")] public float compostura;
        [Tooltip("Liderança: organiza a linha e puxa o time.")] public float lideranca;

        /// <summary>Rótulo curto do perfil, mostrado no HUD do jogador controlado.</summary>
        public string Perfil(Funcao f)
        {
            if (lideranca > 0.8f && espiritoEquipe > 0.7f) return "Líder";
            if (f == Funcao.Atacante && espiritoEquipe < 0.4f && ofensividade > 0.7f) return "Artilheiro fominha";
            if (criatividade > 0.78f && (f == Funcao.Meia || f == Funcao.Ponta)) return "Maestro";
            if (criatividade > 0.75f) return "Driblador";
            if (agressividade > 0.78f && (f == Funcao.Zagueiro || f == Funcao.Volante)) return "Xerife";
            if (espiritoEquipe > 0.8f && disciplina > 0.7f) return "Carregador de piano";
            if (disciplina > 0.82f) return "Disciplinado";
            if (ofensividade > 0.78f) return "Ofensivo";
            if (compostura > 0.8f) return "Frio";
            return "Equilibrado";
        }
    }

    /// <summary>Posição base de um jogador na formação, em coordenadas do time.</summary>
    public struct Slot
    {
        /// <summary>-1 = linha do próprio gol, +1 = linha do gol adversário (bloco neutro, bola no meio).</summary>
        public float x;
        /// <summary>-1 a +1 (lado direito/esquerdo do campo, visto pelo próprio time atacando).</summary>
        public float z;
        public Funcao funcao;
        public Slot(float x, float z, Funcao f) { this.x = x; this.z = z; funcao = f; }
    }

    public static class Formacoes
    {
        public static readonly string[] Nomes = { "4-4-2", "4-3-3", "4-2-3-1" };

        /// <summary>Dez jogadores de linha (o goleiro é separado).</summary>
        public static Slot[] Get(string nome)
        {
            switch (nome)
            {
                case "4-3-3":
                    return new[]
                    {
                        new Slot(-0.66f, -0.22f, Funcao.Zagueiro), new Slot(-0.66f, 0.22f, Funcao.Zagueiro),
                        new Slot(-0.56f, -0.74f, Funcao.Lateral), new Slot(-0.56f, 0.74f, Funcao.Lateral),
                        new Slot(-0.36f, 0f, Funcao.Volante),
                        new Slot(-0.14f, -0.38f, Funcao.Meia), new Slot(-0.14f, 0.38f, Funcao.Meia),
                        new Slot(0.12f, -0.72f, Funcao.Ponta), new Slot(0.12f, 0.72f, Funcao.Ponta),
                        new Slot(0.2f, 0f, Funcao.Atacante),
                    };
                case "4-2-3-1":
                    return new[]
                    {
                        new Slot(-0.66f, -0.22f, Funcao.Zagueiro), new Slot(-0.66f, 0.22f, Funcao.Zagueiro),
                        new Slot(-0.56f, -0.74f, Funcao.Lateral), new Slot(-0.56f, 0.74f, Funcao.Lateral),
                        new Slot(-0.38f, -0.2f, Funcao.Volante), new Slot(-0.38f, 0.2f, Funcao.Volante),
                        new Slot(-0.08f, -0.66f, Funcao.MeiaAberto), new Slot(-0.04f, 0f, Funcao.Meia), new Slot(-0.08f, 0.66f, Funcao.MeiaAberto),
                        new Slot(0.2f, 0f, Funcao.Atacante),
                    };
                default: // 4-4-2
                    return new[]
                    {
                        new Slot(-0.66f, -0.22f, Funcao.Zagueiro), new Slot(-0.66f, 0.22f, Funcao.Zagueiro),
                        new Slot(-0.56f, -0.74f, Funcao.Lateral), new Slot(-0.56f, 0.74f, Funcao.Lateral),
                        new Slot(-0.3f, -0.2f, Funcao.Volante), new Slot(-0.26f, 0.2f, Funcao.Meia),
                        new Slot(-0.2f, -0.72f, Funcao.MeiaAberto), new Slot(-0.2f, 0.72f, Funcao.MeiaAberto),
                        new Slot(0.12f, -0.18f, Funcao.Atacante), new Slot(0.16f, 0.18f, Funcao.Atacante),
                    };
            }
        }

        /// <summary>Números de camisa tradicionais para cada slot (mesma ordem de <see cref="Get"/>).</summary>
        public static int[] Numeros(string nome)
        {
            switch (nome)
            {
                case "4-3-3": return new[] { 3, 4, 2, 6, 5, 8, 10, 7, 11, 9 };
                case "4-2-3-1": return new[] { 3, 4, 2, 6, 5, 8, 7, 10, 11, 9 };
                default: return new[] { 3, 4, 2, 6, 5, 8, 7, 11, 9, 10 };
            }
        }
    }

    /// <summary>Configuração visual e tática de um time.</summary>
    public class TimeInfo
    {
        public string nome, sigla;
        public Color cor, corSecundaria;
        public int indice;              // 0 = casa, 1 = visitante
        public float attackDir;         // +1 ataca o gol em +X, -1 o gol em -X
        public string formacao;
        public bool humano;             // controlado pelo usuário
        [Tooltip("0..1")] public float pressao, linhaDefensiva, largura, verticalidade;
        public float habilidade = 1f;   // dificuldade: escala de reação/precisão da IA
        public Texture2D kitA, kitB, kitGoleiro;
        public bool goleiroModeloA;

        public readonly List<MatchPlayer> jogadores = new List<MatchPlayer>();
        public GoalkeeperAI goleiro;
        public MatchPlayer goleiroInfo;

        public int gols, chutes, chutesNoGol, faltas, amarelos, vermelhos, impedimentos, escanteios;
        public float tempoPosse;

        /// <summary>Converte coordenadas do time (x: -1 próprio gol .. +1 gol adversário; z: -1..1) para o mundo.</summary>
        public Vector3 ToWorld(float tx, float tz)
        {
            return new Vector3(tx * Arena.HalfLength * attackDir, 0f, -tz * Arena.HalfWidth * attackDir);
        }

        /// <summary>Quanto um ponto está avançado no sentido do ataque deste time (-52,5 no próprio gol, +52,5 no gol adversário).</summary>
        public float Fwd(Vector3 p) { return p.x * attackDir; }

        public Vector3 OwnGoal { get { return new Vector3(-attackDir * Arena.HalfLength, 0f, 0f); } }
        public Vector3 TargetGoal { get { return new Vector3(attackDir * Arena.HalfLength, 0f, 0f); } }
        public Vector3 Forward { get { return new Vector3(attackDir, 0f, 0f); } }
    }

    /// <summary>Nomes brasileiros para gerar os elencos (camisa + nome no HUD).</summary>
    public static class NomesBR
    {
        static readonly string[] Primeiros =
        {
            "Thiago", "Rafael", "Gabriel", "Lucas", "Matheus", "Bruno", "Felipe", "Diego", "Caio", "Vinícius", "Renan", "Igor",
            "Danilo", "Everton", "Rodrigo", "Gustavo", "Wesley", "Marcelo", "André", "Leandro", "Hugo", "Paulo", "Douglas", "Júnior",
            "Pedro", "Arthur", "Davi", "Henrique", "Murilo", "Kaique", "Alisson", "Fábio", "Ronaldo", "Rogério", "Juninho", "Cristian"
        };

        static readonly string[] Sobrenomes =
        {
            "Rocha", "Silva", "Souza", "Oliveira", "Santos", "Pereira", "Lima", "Ferreira", "Costa", "Almeida", "Ribeiro", "Carvalho",
            "Gomes", "Martins", "Araújo", "Barbosa", "Cardoso", "Teixeira", "Moura", "Nunes", "Dias", "Monteiro", "Freitas", "Batista",
            "Campos", "Vieira", "Machado", "Pinto", "Mendes", "Duarte", "Farias", "Queiroz", "Prado", "Brandão", "Neves", "Bastos"
        };

        static readonly string[] Apelidos =
        {
            "Tiquinho", "Pelezinho", "Dudu", "Gabigol", "Bebeto", "Neném", "Fumaça", "Canhoto", "Paredão", "Foguete", "Bigode", "Xodó"
        };

        /// <summary>Nome curto para o HUD: sobrenome, nome ou apelido (sem repetir no mesmo elenco).</summary>
        public static string Gerar(System.Random rng, HashSet<string> usados)
        {
            for (int t = 0; t < 40; t++)
            {
                string n;
                double r = rng.NextDouble();
                if (r < 0.12) n = Apelidos[rng.Next(Apelidos.Length)];
                else if (r < 0.45) n = Primeiros[rng.Next(Primeiros.Length)];
                else n = Sobrenomes[rng.Next(Sobrenomes.Length)];
                if (usados.Add(n)) return n;
            }
            string f = Primeiros[rng.Next(Primeiros.Length)] + " " + Sobrenomes[rng.Next(Sobrenomes.Length)][0] + ".";
            usados.Add(f);
            return f;
        }
    }

    /// <summary>Cria atributos e mentalidade coerentes com a função (e uma pitada de personalidade própria).</summary>
    public static class GeradorJogador
    {
        static float R(System.Random rng, float center, float spread)
        {
            double u1 = Math.Max(1e-6, rng.NextDouble()), u2 = rng.NextDouble();
            double g = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return Mathf.Clamp(center + (float)g * spread, 0.15f, 0.98f);
        }

        public static Atributos Atributos(Funcao f, System.Random rng, float nivel)
        {
            float b = nivel; // ~0.6 a 0.75
            var a = new Atributos
            {
                velocidade = R(rng, b, 0.1f), aceleracao = R(rng, b, 0.1f), resistencia = R(rng, b, 0.08f),
                forca = R(rng, b, 0.1f), equilibrio = R(rng, b, 0.09f), drible = R(rng, b - 0.05f, 0.1f),
                passe = R(rng, b, 0.08f), finalizacao = R(rng, b - 0.08f, 0.1f), desarme = R(rng, b - 0.05f, 0.1f),
                cabeceio = R(rng, b - 0.05f, 0.1f), visao = R(rng, b - 0.02f, 0.1f), posicionamento = R(rng, b, 0.08f)
            };
            switch (f)
            {
                case Funcao.Zagueiro: a.desarme += 0.18f; a.forca += 0.15f; a.cabeceio += 0.16f; a.posicionamento += 0.1f; a.drible -= 0.12f; a.finalizacao -= 0.15f; a.velocidade -= 0.05f; break;
                case Funcao.Lateral: a.velocidade += 0.12f; a.resistencia += 0.12f; a.desarme += 0.06f; a.passe += 0.03f; break;
                case Funcao.Volante: a.desarme += 0.14f; a.passe += 0.08f; a.resistencia += 0.1f; a.posicionamento += 0.08f; a.forca += 0.06f; break;
                case Funcao.Meia: a.passe += 0.16f; a.visao += 0.18f; a.drible += 0.1f; a.desarme -= 0.06f; break;
                case Funcao.MeiaAberto: a.velocidade += 0.08f; a.passe += 0.08f; a.drible += 0.1f; a.resistencia += 0.06f; break;
                case Funcao.Ponta: a.velocidade += 0.16f; a.aceleracao += 0.14f; a.drible += 0.16f; a.finalizacao += 0.06f; a.desarme -= 0.12f; break;
                case Funcao.Atacante: a.finalizacao += 0.2f; a.cabeceio += 0.1f; a.forca += 0.05f; a.posicionamento += 0.1f; a.desarme -= 0.15f; break;
            }
            Clamp(a);
            return a;
        }

        static void Clamp(Atributos a)
        {
            a.velocidade = Mathf.Clamp(a.velocidade, 0.2f, 0.98f); a.aceleracao = Mathf.Clamp(a.aceleracao, 0.2f, 0.98f);
            a.resistencia = Mathf.Clamp(a.resistencia, 0.2f, 0.98f); a.forca = Mathf.Clamp(a.forca, 0.2f, 0.98f);
            a.equilibrio = Mathf.Clamp(a.equilibrio, 0.2f, 0.98f); a.drible = Mathf.Clamp(a.drible, 0.2f, 0.98f);
            a.passe = Mathf.Clamp(a.passe, 0.2f, 0.98f); a.finalizacao = Mathf.Clamp(a.finalizacao, 0.2f, 0.98f);
            a.desarme = Mathf.Clamp(a.desarme, 0.2f, 0.98f); a.cabeceio = Mathf.Clamp(a.cabeceio, 0.2f, 0.98f);
            a.visao = Mathf.Clamp(a.visao, 0.2f, 0.98f); a.posicionamento = Mathf.Clamp(a.posicionamento, 0.2f, 0.98f);
        }

        public static Mentalidade Mentalidade(Funcao f, System.Random rng)
        {
            var m = new Mentalidade
            {
                ofensividade = R(rng, 0.5f, 0.15f), criatividade = R(rng, 0.5f, 0.17f), disciplina = R(rng, 0.62f, 0.15f),
                agressividade = R(rng, 0.5f, 0.17f), espiritoEquipe = R(rng, 0.65f, 0.17f), compostura = R(rng, 0.55f, 0.15f),
                lideranca = R(rng, 0.45f, 0.2f)
            };
            switch (f)
            {
                case Funcao.Zagueiro: m.ofensividade -= 0.25f; m.agressividade += 0.15f; m.disciplina += 0.1f; m.lideranca += 0.1f; break;
                case Funcao.Lateral: m.ofensividade += 0.05f; m.espiritoEquipe += 0.05f; break;
                case Funcao.Volante: m.ofensividade -= 0.15f; m.agressividade += 0.12f; m.espiritoEquipe += 0.1f; m.disciplina += 0.1f; break;
                case Funcao.Meia: m.criatividade += 0.15f; m.ofensividade += 0.1f; break;
                case Funcao.MeiaAberto: m.ofensividade += 0.1f; m.criatividade += 0.05f; break;
                case Funcao.Ponta: m.ofensividade += 0.2f; m.criatividade += 0.18f; m.disciplina -= 0.1f; break;
                case Funcao.Atacante: m.ofensividade += 0.28f; m.espiritoEquipe -= 0.08f; m.disciplina -= 0.05f; break;
            }
            m.ofensividade = Mathf.Clamp01(m.ofensividade); m.criatividade = Mathf.Clamp01(m.criatividade);
            m.disciplina = Mathf.Clamp01(m.disciplina); m.agressividade = Mathf.Clamp01(m.agressividade);
            m.espiritoEquipe = Mathf.Clamp01(m.espiritoEquipe); m.compostura = Mathf.Clamp01(m.compostura);
            m.lideranca = Mathf.Clamp01(m.lideranca);
            return m;
        }

        public static string NomeFuncao(Funcao f)
        {
            switch (f)
            {
                case Funcao.Goleiro: return "GOL";
                case Funcao.Zagueiro: return "ZAG";
                case Funcao.Lateral: return "LAT";
                case Funcao.Volante: return "VOL";
                case Funcao.Meia: return "MEI";
                case Funcao.MeiaAberto: return "ME";
                case Funcao.Ponta: return "PON";
                default: return "ATA";
            }
        }
    }
}
