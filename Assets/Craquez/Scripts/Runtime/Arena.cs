using UnityEngine;

namespace Craquez
{
    public enum Clima { Sol = 0, Chuva = 1, Tempestade = 2 }

    /// <summary>Medidas oficiais do campo (FIFA) usadas por todo o jogo. Campo centrado na origem, comprimento no eixo X.</summary>
    public static class Arena
    {
        public const float Length = 105f;
        public const float Width = 68f;
        public const float HalfLength = Length * 0.5f;   // linha de gol do gol atacado em x = +52.5
        public const float HalfWidth = Width * 0.5f;
        public const float GoalHalfWidth = 3.66f;        // 7,32 m entre as traves (face interna)
        public const float GoalHeight = 2.44f;
        public const float PostRadius = 0.06f;
        public const float BallRadius = 0.11f;
        public const float BallMass = 0.43f;
        public const float NetDepth = 2.0f;

        /// <summary>Camadas de física dedicadas (Camada 1: PlayerBody ignora colisão direta com Ball).</summary>
        public const int LayerPlayerBody = 8;
        public const int LayerBall = 9;
        public const int LayerGKGloves = 10;

        public static void ConfigurePhysicsLayers()
        {
            // Regra Crítica Camada 1: A Cápsula Principal (PlayerBody) IGNORA colisão direta com a Bola (Ball)
            Physics.IgnoreLayerCollision(LayerPlayerBody, LayerBall, true);
            // Jogadores colidem entre si (corpo-a-corpo)
            Physics.IgnoreLayerCollision(LayerPlayerBody, LayerPlayerBody, false);
            // Luvas/Sockets de interceptação usam SphereCast/Trigger dedicado, não bloqueiam locomoção
            Physics.IgnoreLayerCollision(LayerPlayerBody, LayerGKGloves, true);
        }

        /// <summary>Gol onde o jogador treina finalização (+X).</summary>
        public static Vector3 AttackGoalCenter { get { return new Vector3(HalfLength, 0f, 0f); } }

        /// <summary>Limites internos do alambrado.</summary>
        public const float FenceX = 60f;
        public const float FenceZ = 44f;

        public static string Nome(Clima c)
        {
            switch (c)
            {
                case Clima.Sol: return "Sol";
                case Clima.Chuva: return "Chuva";
                default: return "Tempestade";
            }
        }
    }

    /// <summary>Configuração escolhida no menu (persiste entre cenas/sessões do app).</summary>
    public static class Selecao
    {
        public static bool Aleatorio = false;
        public static Clima Clima = Clima.Sol;

        public static Clima Resolver()
        {
            if (Aleatorio) Clima = (Clima)Random.Range(0, 3);
            return Clima;
        }
    }
}
