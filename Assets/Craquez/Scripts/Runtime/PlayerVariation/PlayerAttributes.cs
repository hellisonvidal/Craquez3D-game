using System;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Define os atributos físicos, técnicos e mentais de um jogador.
    /// Cada jogador tem valores únicos que afetam seu comportamento em campo.
    /// </summary>
    [Serializable]
    public class PlayerAttributes
    {
        [Header("Identidade")]
        public string playerName = "Jogador";
        public int shirtNumber = 10;
        public PlayerPosition preferredPosition = PlayerPosition.CM;
        public Foot preferredFoot = Foot.Right;
        public int age = 25;
        public float height = 1.80f;
        public float weight = 75f;

        [Header("Atributos Físicos (1-99)")]
        [Range(1, 99)] public int pace = 70;           // Velocidade pura
        [Range(1, 99)] public int acceleration = 70;   // Aceleração
        [Range(1, 99)] public int stamina = 70;        // Resistência
        [Range(1, 99)] public int strength = 70;       // Força física
        [Range(1, 99)] public int agility = 70;        // Agilidade/equilíbrio
        [Range(1, 99)] public int jumping = 70;        // Impulsão
        [Range(1, 99)] public int balance = 70;        // Equilíbrio

        [Header("Atributos Técnicos (1-99)")]
        [Range(1, 99)] public int ballControl = 70;    // Controle de bola
        [Range(1, 99)] public int dribbling = 70;      // Drible
        [Range(1, 99)] public int passing = 70;        // Passe curto
        [Range(1, 99)] public int crossing = 70;       // Cruzamento
        [Range(1, 99)] public int finishing = 70;      // Finalização
        [Range(1, 99)] public int heading = 70;        // Cabeceio
        [Range(1, 99)] public int volleys = 70;        // Voleios
        [Range(1, 99)] public int longShots = 70;      // Chutes de longe
        [Range(1, 99)] public int freeKicks = 70;      // Faltas
        [Range(1, 99)] public int penalties = 70;      // Pênaltis
        [Range(1, 99)] public int curve = 70;          // Efeito na bola
        [Range(1, 99)] public int technique = 70;      // Técnica geral
        [Range(1, 99)] public int firstTouch = 70;     // Primeiro toque
        [Range(1, 99)] public int tackling = 70;       // Desarme
        [Range(1, 99)] public int marking = 70;        // Marcação
        [Range(1, 99)] public int interception = 70;   // Interceptação
        [Range(1, 99)] public int slidingTackle = 70;  // Carrinho

        [Header("Atributos Mentais (1-99)")]
        [Range(1, 99)] public int composure = 70;      // Compostura
        [Range(1, 99)] public int concentration = 70;  // Concentração
        [Range(1, 99)] public int decisions = 70;      // Tomada de decisão
        [Range(1, 99)] public int vision = 70;         // Visão de jogo
        [Range(1, 99)] public int positioning = 70;    // Posicionamento
        [Range(1, 99)] public int anticipation = 70;   // Antecipação
        [Range(1, 99)] public int aggression = 70;     // Agressividade
        [Range(1, 99)] public int bravery = 70;        // Bravura
        [Range(1, 99)] public int determination = 70;  // Determinação
        [Range(1, 99)] public int flair = 70;          // Criatividade/flair
        [Range(1, 99)] public int leadership = 70;     // Liderança
        [Range(1, 99)] public int offTheBall = 70;     // Movimento sem bola
        [Range(1, 99)] public int teamwork = 70;       // Trabalho em equipe
        [Range(1, 99)] public int workRate = 70;       // Taxa de trabalho

        [Header("Atributos de Goleiro (1-99) - apenas para GK")]
        [Range(1, 99)] public int gkDiving = 70;       // Mergulho
        [Range(1, 99)] public int gkHandling = 70;     // Mãos/segurança
        [Range(1, 99)] public int gkKicking = 70;      // Chute/lançamento
        [Range(1, 99)] public int gkPositioning = 70;  // Posicionamento GK
        [Range(1, 99)] public int gkReflexes = 70;     // Reflexos
        [Range(1, 99)] public int gkRushingOut = 70;   // Saída do gol
        [Range(1, 99)] public int gkOneOnOnes = 70;    // 1 contra 1

        [Header("Traits/Especialidades")]
        public PlayerTrait traits = PlayerTrait.None;

        [Header("Aparência (para variações visuais)")]
        public int skinToneIndex = 0;
        public int hairStyleIndex = 0;
        public int hairColorIndex = 0;
        public int faceTypeIndex = 0;
        public bool hasBeard = false;
        public int sleeveLength = 0; // 0=curta, 1=longa
        public int sockHeight = 0;   // 0=baixa, 1=alta, 2=joelho
        public int bootColorIndex = 0;

        // Runtime state
        [NonSerialized] public float currentStamina = 100f;
        [NonSerialized] public float matchFitness = 100f;
        [NonSerialized] public int yellowCards = 0;
        [NonSerialized] public bool redCard = false;
        [NonSerialized] public int goals = 0;
        [NonSerialized] public int assists = 0;
        [NonSerialized] public float distanceCovered = 0f;
        [NonSerialized] public int passesAttempted = 0;
        [NonSerialized] public int passesCompleted = 0;
        [NonSerialized] public int shots = 0;
        [NonSerialized] public int shotsOnTarget = 0;
        [NonSerialized] public int tacklesWon = 0;
        [NonSerialized] public int interceptions = 0;
        [NonSerialized] public int foulsCommitted = 0;
        [NonSerialized] public int foulsSuffered = 0;
        [NonSerialized] public int offsides = 0;
        [NonSerialized] public int saves = 0; // GK only
        [NonSerialized] public int goalsConceded = 0; // GK only

        /// <summary>
        /// Calcula a avaliação geral (overall) baseada na posição
        /// </summary>
        public int CalculateOverall()
        {
            switch (preferredPosition)
            {
                case PlayerPosition.GK:
                    return Mathf.RoundToInt((gkDiving + gkHandling + gkKicking + gkPositioning + gkReflexes + gkRushingOut + gkOneOnOnes) / 7f);
                case PlayerPosition.CB:
                    return Mathf.RoundToInt((defending * 0.3f + physical * 0.25f + mental * 0.2f + technical * 0.15f + pace * 0.1f));
                case PlayerPosition.FB:
                case PlayerPosition.WB:
                    return Mathf.RoundToInt((pace * 0.25f + defending * 0.2f + technical * 0.2f + physical * 0.2f + mental * 0.15f));
                case PlayerPosition.CDM:
                    return Mathf.RoundToInt((defending * 0.25f + passing * 0.2f + physical * 0.2f + mental * 0.2f + pace * 0.15f));
                case PlayerPosition.CM:
                    return Mathf.RoundToInt((passing * 0.25f + technical * 0.2f + mental * 0.2f + physical * 0.15f + pace * 0.1f + defending * 0.1f));
                case PlayerPosition.AM:
                    return Mathf.RoundToInt((technical * 0.3f + mental * 0.25f + passing * 0.2f + pace * 0.15f + physical * 0.1f));
                case PlayerPosition.WM:
                case PlayerPosition.WF:
                    return Mathf.RoundToInt((pace * 0.3f + technical * 0.25f + mental * 0.2f + physical * 0.15f + finishing * 0.1f));
                case PlayerPosition.ST:
                case PlayerPosition.CF:
                    return Mathf.RoundToInt((finishing * 0.3f + pace * 0.2f + physical * 0.2f + technical * 0.15f + mental * 0.15f));
                default:
                    return Mathf.RoundToInt((pace + shooting + passing + dribbling + defending + physical) / 6f);
            }
        }

        // Propriedades computadas para grupos de atributos
        public float physical => (pace + acceleration + stamina + strength + agility + jumping + balance) / 7f;
        public float technical => (ballControl + dribbling + passing + crossing + finishing + heading + volleys + longShots + freeKicks + penalties + curve + technique + firstTouch + tackling + marking + interception + slidingTackle) / 18f;
        public float mental => (composure + concentration + decisions + vision + positioning + anticipation + aggression + bravery + determination + flair + leadership + offTheBall + teamwork + workRate) / 14f;
        public float defending => (marking + interception + tackling + slidingTackle) / 4f;
        public float shooting => (finishing + heading + volleys + longShots + freeKicks + penalties) / 6f;

        /// <summary>
        /// Gera atributos aleatórios baseados na posição e qualidade
        /// </summary>
        public static PlayerAttributes GenerateRandom(PlayerPosition position, int quality = 70, int variance = 15)
        {
            var attrs = new PlayerAttributes();
            attrs.preferredPosition = position;

            // Base attributes around quality with variance
            Func<int> roll = () => Mathf.Clamp(quality + UnityEngine.Random.Range(-variance, variance), 1, 99);

            attrs.pace = roll();
            attrs.acceleration = roll();
            attrs.stamina = roll();
            attrs.strength = roll();
            attrs.agility = roll();
            attrs.jumping = roll();
            attrs.balance = roll();

            attrs.ballControl = roll();
            attrs.dribbling = roll();
            attrs.passing = roll();
            attrs.crossing = roll();
            attrs.finishing = roll();
            attrs.heading = roll();
            attrs.volleys = roll();
            attrs.longShots = roll();
            attrs.freeKicks = roll();
            attrs.penalties = roll();
            attrs.curve = roll();
            attrs.technique = roll();
            attrs.firstTouch = roll();
            attrs.tackling = roll();
            attrs.marking = roll();
            attrs.interception = roll();
            attrs.slidingTackle = roll();

            attrs.composure = roll();
            attrs.concentration = roll();
            attrs.decisions = roll();
            attrs.vision = roll();
            attrs.positioning = roll();
            attrs.anticipation = roll();
            attrs.aggression = roll();
            attrs.bravery = roll();
            attrs.determination = roll();
            attrs.flair = roll();
            attrs.leadership = roll();
            attrs.offTheBall = roll();
            attrs.teamwork = roll();
            attrs.workRate = roll();

            // GK specific
            if (position == PlayerPosition.GK)
            {
                attrs.gkDiving = roll();
                attrs.gkHandling = roll();
                attrs.gkKicking = roll();
                attrs.gkPositioning = roll();
                attrs.gkReflexes = roll();
                attrs.gkRushingOut = roll();
                attrs.gkOneOnOnes = roll();
            }

            // Add some random traits
            AddRandomTraits(attrs, position);

            // Generate random appearance
            GenerateRandomAppearance(attrs);

            return attrs;
        }

        static void GenerateRandomAppearance(PlayerAttributes attrs)
        {
            // Skin tone: 0-5 (light to dark)
            attrs.skinToneIndex = UnityEngine.Random.Range(0, 6);

            // Hair style: 0-8 (bald, short, medium, long, afro, dreadlocks, mohawk, curly, ponytail)
            attrs.hairStyleIndex = UnityEngine.Random.Range(0, 9);

            // Hair color: 0-7 (black, dark brown, brown, light brown, blonde, red, gray, white)
            attrs.hairColorIndex = UnityEngine.Random.Range(0, 8);

            // Face type: 0-5 (different face shapes/features)
            attrs.faceTypeIndex = UnityEngine.Random.Range(0, 6);

            // Beard: 0-1 (no beard, beard)
            attrs.hasBeard = UnityEngine.Random.value < 0.3f;

            // Sleeve length: 0-1 (short, long)
            attrs.sleeveLength = UnityEngine.Random.Range(0, 2);

            // Sock height: 0-2 (low, high, knee)
            attrs.sockHeight = UnityEngine.Random.Range(0, 3);

            // Boot color: 0-7 (black, white, red, blue, yellow, green, orange, pink)
            attrs.bootColorIndex = UnityEngine.Random.Range(0, 8);
        }

        static void AddRandomTraits(PlayerAttributes attrs, PlayerPosition position)
        {
            var allTraits = System.Enum.GetValues(typeof(PlayerTrait));
            var possibleTraits = new System.Collections.Generic.List<PlayerTrait>();

            foreach (PlayerTrait t in allTraits)
            {
                if (IsTraitValidForPosition(t, position) && UnityEngine.Random.value < 0.15f)
                    possibleTraits.Add(t);
            }

            // Shuffle and pick 0-3 traits
            for (int i = possibleTraits.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var temp = possibleTraits[i];
                possibleTraits[i] = possibleTraits[j];
                possibleTraits[j] = temp;
            }

            int traitCount = UnityEngine.Random.Range(0, 4);
            PlayerTrait combinedTraits = PlayerTrait.None;
            var selectedTraits = possibleTraits.GetRange(0, Mathf.Min(traitCount, possibleTraits.Count));
            foreach (var t in selectedTraits)
            {
                combinedTraits |= t;
            }
            attrs.traits = combinedTraits;
        }

        static bool IsTraitValidForPosition(PlayerTrait trait, PlayerPosition position)
        {
            switch (trait)
            {
                case PlayerTrait.GKPenaltyStopper: return position == PlayerPosition.GK;
                case PlayerTrait.GKOneOnOne: return position == PlayerPosition.GK;
                case PlayerTrait.GKLongThrow: return position == PlayerPosition.GK;
                case PlayerTrait.GKFlatKick: return position == PlayerPosition.GK;
                case PlayerTrait.PenaltyTaker: return position == PlayerPosition.ST || position == PlayerPosition.CF || position == PlayerPosition.AM;
                case PlayerTrait.FreeKickSpecialist: return position != PlayerPosition.GK;
                case PlayerTrait.LongShotTaker: return position != PlayerPosition.GK && position != PlayerPosition.CB;
                case PlayerTrait.Playmaker: return position == PlayerPosition.CM || position == PlayerPosition.AM || position == PlayerPosition.CDM;
                case PlayerTrait.TargetMan: return position == PlayerPosition.ST || position == PlayerPosition.CF;
                case PlayerTrait.Poacher: return position == PlayerPosition.ST || position == PlayerPosition.CF;
                case PlayerTrait.CompleteForward: return position == PlayerPosition.ST || position == PlayerPosition.CF;
                case PlayerTrait.FalseNine: return position == PlayerPosition.CF || position == PlayerPosition.AM;
                case PlayerTrait.Winger: return position == PlayerPosition.WM || position == PlayerPosition.WF;
                case PlayerTrait.InsideForward: return position == PlayerPosition.WF || position == PlayerPosition.AM;
                case PlayerTrait.BoxToBox: return position == PlayerPosition.CM;
                case PlayerTrait.DeepLyingPlaymaker: return position == PlayerPosition.CDM || position == PlayerPosition.CM;
                case PlayerTrait.BallWinningMidfielder: return position == PlayerPosition.CDM || position == PlayerPosition.CM;
                case PlayerTrait.AnchorMan: return position == PlayerPosition.CDM;
                case PlayerTrait.FullBackAttack: return position == PlayerPosition.FB || position == PlayerPosition.WB;
                case PlayerTrait.InvertedFullBack: return position == PlayerPosition.FB || position == PlayerPosition.WB;
                case PlayerTrait.CentralDefender: return position == PlayerPosition.CB;
                case PlayerTrait.BallPlayingDefender: return position == PlayerPosition.CB;
                case PlayerTrait.SweeperKeeper: return position == PlayerPosition.GK;
                default: return true;
            }
        }

        public void ResetMatchStats()
        {
            currentStamina = 100f;
            yellowCards = 0;
            redCard = false;
            goals = 0;
            assists = 0;
            distanceCovered = 0f;
            passesAttempted = 0;
            passesCompleted = 0;
            shots = 0;
            shotsOnTarget = 0;
            tacklesWon = 0;
            interceptions = 0;
            foulsCommitted = 0;
            foulsSuffered = 0;
            offsides = 0;
            saves = 0;
            goalsConceded = 0;
        }
    }

    /// <summary>
    /// Posições táticas no campo
    /// </summary>
    public enum PlayerPosition
    {
        GK = 0,      // Goleiro
        CB = 1,      // Zagueiro Central
        FB = 2,      // Lateral Direito/Esquerdo
        WB = 3,      // Ala (Wing-back)
        CDM = 4,     // Volante/Primeiro Volante
        CM = 5,      // Meia Central
        AM = 6,      // Meia Atacante
        WM = 7,      // Meia Lateral (Winger)
        WF = 8,      // Ponta/Extremo
        CF = 9,      // Centroavante/Falso 9
        ST = 10      // Atacante/Ponta de Lança
    }

    /// <summary>
    /// Pé preferido
    /// </summary>
    public enum Foot
    {
        Right = 0,
        Left = 1,
        Both = 2
    }

    /// <summary>
    /// Traits/Especialidades que modificam comportamento
    /// </summary>
    [Flags]
    public enum PlayerTrait
    {
        None = 0,
        // GK Traits
        GKPenaltyStopper = 1 << 0,
        GKOneOnOne = 1 << 1,
        GKLongThrow = 1 << 2,
        GKFlatKick = 1 << 3,
        SweeperKeeper = 1 << 4,

        // Attacking Traits
        PenaltyTaker = 1 << 5,
        FreeKickSpecialist = 1 << 6,
        LongShotTaker = 1 << 7,
        Playmaker = 1 << 8,
        TargetMan = 1 << 9,
        Poacher = 1 << 10,
        CompleteForward = 1 << 11,
        FalseNine = 1 << 12,
        Winger = 1 << 13,
        InsideForward = 1 << 14,

        // Midfield Traits
        BoxToBox = 1 << 15,
        DeepLyingPlaymaker = 1 << 16,
        BallWinningMidfielder = 1 << 17,
        AnchorMan = 1 << 18,

        // Defensive Traits
        FullBackAttack = 1 << 19,
        InvertedFullBack = 1 << 20,
        CentralDefender = 1 << 21,
        BallPlayingDefender = 1 << 22,

        // Physical Traits
        Speedster = 1 << 23,
        StrengthBeast = 1 << 24,
        AerialThreat = 1 << 25,
        InjuryProne = 1 << 26,

        // Mental Traits
        Leader = 1 << 27,
        ClutchPlayer = 1 << 28,
        Consistent = 1 << 29,
        Temperamental = 1 << 30
    }

    /// <summary>
    /// Mentalidade tática do jogador (afeta decisões da IA)
    /// </summary>
    [Serializable]
    public class PlayerMentality
    {
        [Header("Estilo de Jogo (0-1)")]
        [Range(0f, 1f)] public float attackingIntent = 0.5f;      // Quanto busca o ataque
        [Range(0f, 1f)] public float defensiveDuty = 0.5f;        // Quanto ajuda na defesa
        [Range(0f, 1f)] public float riskTaking = 0.5f;           // Arriscar passes/dribles
        [Range(0f, 1f)] public float pressingIntensity = 0.5f;    // Intensidade do pressionamento
        [Range(0f, 1f)] public float positionalDiscipline = 0.5f; // Disciplina tática
        [Range(0f, 1f)] public float creativeFreedom = 0.5f;      // Liberdade criativa
        [Range(0f, 1f)] public float supportDistance = 0.5f;      // Distância de apoio aos companheiros
        [Range(0f, 1f)] public float shootingFrequency = 0.5f;    // Frequência de chutes
        [Range(0f, 1f)] public float crossingFrequency = 0.5f;    // Frequência de cruzamentos
        [Range(0f, 1f)] public float throughBallFrequency = 0.5f; // Frequência de passes em profundidade
        [Range(0f, 1f)] public float dribblingFrequency = 0.5f;   // Frequência de dribles
        [Range(0f, 1f)] public float tacklingAggression = 0.5f;   // Agressividade nos desarmes
        [Range(0f, 1f)] public float interceptionTendency = 0.5f; // Tendência a interceptar
        [Range(0f, 1f)] public float offTheBallMovement = 0.5f;   // Movimentação sem bola

        [Header("Estados Emocionais (Runtime)")]
        [NonSerialized] public float confidence = 1f;      // 0-2 (baixa->alta)
        [NonSerialized] public float fatigue = 0f;         // 0-1
        [NonSerialized] public float frustration = 0f;     // 0-1
        [NonSerialized] public float motivation = 1f;      // 0-2
        [NonSerialized] public float matchSharpness = 1f;  // 0-1

        /// <summary>
        /// Gera mentalidade baseada na posição e atributos
        /// </summary>
        public static PlayerMentality GenerateForPosition(PlayerPosition position, PlayerAttributes attrs)
        {
            var mentality = new PlayerMentality();

            switch (position)
            {
                case PlayerPosition.GK:
                    mentality.attackingIntent = 0.1f;
                    mentality.defensiveDuty = 1f;
                    mentality.riskTaking = 0.2f;
                    mentality.pressingIntensity = 0.3f;
                    mentality.positionalDiscipline = 0.9f;
                    mentality.creativeFreedom = 0.2f;
                    mentality.supportDistance = 0.3f;
                    mentality.tacklingAggression = 0.4f;
                    break;

                case PlayerPosition.CB:
                    mentality.attackingIntent = 0.2f;
                    mentality.defensiveDuty = 1f;
                    mentality.riskTaking = 0.2f;
                    mentality.pressingIntensity = 0.4f;
                    mentality.positionalDiscipline = 0.9f;
                    mentality.creativeFreedom = 0.3f;
                    mentality.supportDistance = 0.4f;
                    mentality.tacklingAggression = 0.7f;
                    mentality.interceptionTendency = 0.8f;
                    break;

                case PlayerPosition.FB:
                case PlayerPosition.WB:
                    mentality.attackingIntent = 0.6f;
                    mentality.defensiveDuty = 0.8f;
                    mentality.riskTaking = 0.5f;
                    mentality.pressingIntensity = 0.6f;
                    mentality.positionalDiscipline = 0.7f;
                    mentality.creativeFreedom = 0.6f;
                    mentality.supportDistance = 0.6f;
                    mentality.crossingFrequency = 0.7f;
                    mentality.dribblingFrequency = 0.5f;
                    break;

                case PlayerPosition.CDM:
                    mentality.attackingIntent = 0.3f;
                    mentality.defensiveDuty = 0.9f;
                    mentality.riskTaking = 0.3f;
                    mentality.pressingIntensity = 0.7f;
                    mentality.positionalDiscipline = 0.8f;
                    mentality.creativeFreedom = 0.4f;
                    mentality.supportDistance = 0.5f;
                    mentality.tacklingAggression = 0.8f;
                    mentality.interceptionTendency = 0.9f;
                    mentality.throughBallFrequency = 0.4f;
                    break;

                case PlayerPosition.CM:
                    mentality.attackingIntent = 0.5f;
                    mentality.defensiveDuty = 0.6f;
                    mentality.riskTaking = 0.5f;
                    mentality.pressingIntensity = 0.6f;
                    mentality.positionalDiscipline = 0.7f;
                    mentality.creativeFreedom = 0.6f;
                    mentality.supportDistance = 0.6f;
                    mentality.throughBallFrequency = 0.6f;
                    mentality.dribblingFrequency = 0.4f;
                    break;

                case PlayerPosition.AM:
                    mentality.attackingIntent = 0.8f;
                    mentality.defensiveDuty = 0.4f;
                    mentality.riskTaking = 0.7f;
                    mentality.pressingIntensity = 0.6f;
                    mentality.positionalDiscipline = 0.5f;
                    mentality.creativeFreedom = 0.9f;
                    mentality.supportDistance = 0.7f;
                    mentality.throughBallFrequency = 0.8f;
                    mentality.dribblingFrequency = 0.6f;
                    mentality.shootingFrequency = 0.6f;
                    break;

                case PlayerPosition.WM:
                case PlayerPosition.WF:
                    mentality.attackingIntent = 0.8f;
                    mentality.defensiveDuty = 0.5f;
                    mentality.riskTaking = 0.7f;
                    mentality.pressingIntensity = 0.6f;
                    mentality.positionalDiscipline = 0.5f;
                    mentality.creativeFreedom = 0.8f;
                    mentality.supportDistance = 0.6f;
                    mentality.crossingFrequency = 0.8f;
                    mentality.dribblingFrequency = 0.8f;
                    mentality.shootingFrequency = 0.5f;
                    break;

                case PlayerPosition.CF:
                case PlayerPosition.ST:
                    mentality.attackingIntent = 1f;
                    mentality.defensiveDuty = 0.3f;
                    mentality.riskTaking = 0.6f;
                    mentality.pressingIntensity = 0.7f;
                    mentality.positionalDiscipline = 0.4f;
                    mentality.creativeFreedom = 0.7f;
                    mentality.supportDistance = 0.5f;
                    mentality.shootingFrequency = 0.9f;
                    mentality.dribblingFrequency = 0.5f;
                    mentality.offTheBallMovement = 0.9f;
                    break;
            }

            // Ajusta baseado nos atributos mentais
            float mentalFactor = attrs.mental / 99f;
            mentality.attackingIntent = Mathf.Lerp(mentality.attackingIntent, 0.5f, 1f - mentalFactor);
            mentality.defensiveDuty = Mathf.Lerp(mentality.defensiveDuty, 0.5f, 1f - mentalFactor);
            mentality.positionalDiscipline = Mathf.Lerp(mentality.positionalDiscipline, 0.5f, 1f - mentalFactor);
            // attrs.decisions afeta positionalDiscipline (tomada de decisão -> disciplina tática)
            mentality.positionalDiscipline = Mathf.Lerp(mentality.positionalDiscipline, attrs.decisions / 99f, 0.5f);

            // Traits modificam mentalidade
            if ((attrs.traits & PlayerTrait.Playmaker) != 0)
            {
                mentality.creativeFreedom = Mathf.Min(1f, mentality.creativeFreedom + 0.2f);
                mentality.throughBallFrequency = Mathf.Min(1f, mentality.throughBallFrequency + 0.2f);
            }
            if ((attrs.traits & PlayerTrait.Poacher) != 0)
            {
                mentality.shootingFrequency = Mathf.Min(1f, mentality.shootingFrequency + 0.2f);
                mentality.offTheBallMovement = Mathf.Min(1f, mentality.offTheBallMovement + 0.2f);
                mentality.riskTaking = Mathf.Min(1f, mentality.riskTaking + 0.1f);
            }
            if ((attrs.traits & PlayerTrait.BallWinningMidfielder) != 0)
            {
                mentality.tacklingAggression = Mathf.Min(1f, mentality.tacklingAggression + 0.2f);
                mentality.interceptionTendency = Mathf.Min(1f, mentality.interceptionTendency + 0.2f);
                mentality.pressingIntensity = Mathf.Min(1f, mentality.pressingIntensity + 0.2f);
            }
            if ((attrs.traits & PlayerTrait.AnchorMan) != 0)
            {
                mentality.positionalDiscipline = Mathf.Min(1f, mentality.positionalDiscipline + 0.2f);
                mentality.defensiveDuty = Mathf.Min(1f, mentality.defensiveDuty + 0.1f);
                mentality.attackingIntent = Mathf.Max(0f, mentality.attackingIntent - 0.2f);
            }
            if ((attrs.traits & PlayerTrait.Leader) != 0)
            {
                mentality.motivation = 1.5f;
                mentality.confidence = 1.3f;
            }

            return mentality;
        }

        public void UpdateEmotionalState(float performanceRating, bool teamWinning, float minutesPlayed)
        {
            // Atualiza confiança baseada na performance
            confidence = Mathf.Clamp(confidence + (performanceRating - 6.5f) * 0.05f, 0.3f, 2f);

            // Fadiga aumenta com minutos jogados
            fatigue = Mathf.Clamp01(minutesPlayed / 90f * 0.8f);

            // Frustração se perdendo ou performance ruim
            if (!teamWinning) frustration = Mathf.Clamp01(frustration + 0.01f);
            if (performanceRating < 5f) frustration = Mathf.Clamp01(frustration + 0.02f);
            if (performanceRating > 8f) frustration = Mathf.Clamp01(frustration - 0.01f);

            // Motivação
            motivation = Mathf.Clamp(motivation + (teamWinning ? 0.005f : -0.005f), 0.3f, 2f);

            // Sharpness decai se não joga
            if (minutesPlayed == 0) matchSharpness = Mathf.Clamp01(matchSharpness - 0.02f);
            else matchSharpness = Mathf.Clamp01(matchSharpness + 0.01f);
        }

        public float GetEffectiveAttribute(float baseValue, string attributeName)
        {
            float modifier = 1f;

            // Confiança afeta tudo
            modifier *= confidence;

            // Fadiga reduz atributos físicos
            if (attributeName.Contains("pace") || attributeName.Contains("stamina") ||
                attributeName.Contains("strength") || attributeName.Contains("agility"))
            {
                modifier *= 1f - fatigue * 0.3f;
            }

            // Frustração reduz tomada de decisão e compostura
            if (attributeName.Contains("decision") || attributeName.Contains("composure") ||
                attributeName.Contains("concentration"))
            {
                modifier *= 1f - frustration * 0.3f;
            }

            // Motivação afeta work rate e determination
            if (attributeName.Contains("workRate") || attributeName.Contains("determination") ||
                attributeName.Contains("bravery"))
            {
                modifier *= motivation;
            }

            // Sharpness afeta atributos técnicos
            if (attributeName.Contains("technique") || attributeName.Contains("firstTouch") ||
                attributeName.Contains("dribbling") || attributeName.Contains("ballControl"))
            {
                modifier *= matchSharpness;
            }

            return baseValue * modifier;
        }
    }
}