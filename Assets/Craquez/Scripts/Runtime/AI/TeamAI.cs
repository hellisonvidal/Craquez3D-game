using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Sistema de IA coletiva para times de 11 jogadores.
    /// Implementa: formações, transições defensivas/ofensivas, pressão, cobertura, apoio,
    /// movimentações sincronizadas, jogadas ensaiadas, mentalidade coletiva.
    /// </summary>
    public class TeamAI : MonoBehaviour
    {
        public static TeamAI Instance { get; private set; }

        [Header("Configurações Gerais")]
        public int teamId = 0; // 0 = home, 1 = away
        public FootballRulesEngine.Formation formation = FootballRulesEngine.Formation.FourFourTwo;
        public FootballRulesEngine.TeamMentality mentality = FootballRulesEngine.TeamMentality.Balanced;
        public float tacticalDiscipline = 0.7f; // 0-1: quão fiel à formação
        public float creativeFreedom = 0.5f;    // 0-1: liberdade criativa

        [Header("Comportamento Defensivo")]
        public float defensiveLineHeight = 0.5f;     // 0 = recuada, 1 = adiantada
        public float pressingIntensity = 0.5f;       // 0-1: intensidade do pressionamento
        public float interceptionTendency = 0.5f;    // Tendência a interceptar passes
        public float tackleAggression = 0.5f;        // Agressividade nos desarmes
        public float offsideTrapFrequency = 0.3f;    // Frequência de linha de impedimento

        [Header("Comportamento Ofensivo")]
        public float attackingWidth = 0.6f;          // Amplitude do ataque
        public float directness = 0.5f;              // 0 = posse, 1 = vertical/direto
        public float crossingFrequency = 0.5f;       // Frequência de cruzamentos
        public float throughBallFrequency = 0.5f;    // Frequência de passes em profundidade
        public float shootingFromDistance = 0.4f;    // Chutes de fora da área
        public float counterAttackSpeed = 0.7f;      // Velocidade do contra-ataque

        [Header("Transições")]
        public float defensiveTransitionSpeed = 0.8f; // Rapidez na recomposição
        public float offensiveTransitionSpeed = 0.8f; // Rapidez no contra-ataque
        public float counterPressingDuration = 5f;    // Segundos de pressão pós-perda

        [Header("Jogadas Ensaiadas")]
        public bool useSetPieces = true;
        public float cornerRoutineVariation = 0.5f;
        public float freeKickRoutineVariation = 0.5f;
        public int penaltyTakerId = -1;

        // Estado interno
        private FootballRulesEngine _rulesEngine;
        private List<int> _playersOnField = new List<int>();
        private Dictionary<int, PlayerController> _playerControllers = new Dictionary<int, PlayerController>();
        private Dictionary<int, PlayerAttributes> _playerAttributes = new Dictionary<int, PlayerAttributes>();
        private Dictionary<int, PlayerMentality> _playerMentalities = new Dictionary<int, PlayerMentality>();

        // Posições de formação
        private Dictionary<int, Vector3> _formationPositions = new Dictionary<int, Vector3>();
        private Dictionary<int, Vector3> _defensivePositions = new Dictionary<int, Vector3>();
        private Dictionary<int, Vector3> _offensivePositions = new Dictionary<int, Vector3>();

        // Estado tático
        private enum TeamPhase
        {
            DefensiveOrganization,
            DefensiveTransition,
            OffensiveOrganization,
            OffensiveTransition,
            SetPiece,
            CounterAttack
        }

        private TeamPhase _currentPhase = TeamPhase.DefensiveOrganization;
        private float _phaseStartTime = 0f;
        private bool _inPossession = false;
        private bool _lastPossession = false;
        private Vector3 _ballPositionAtLoss = Vector3.zero;
        private int _playerWhoLostBall = -1;
        private float _counterPressingEndTime = 0f;

        // Jogadas ensaiadas
        private enum SetPieceType
        {
            None,
            Corner,
            FreeKickAttacking,
            FreeKickDefending,
            Penalty,
            GoalKick,
            ThrowIn
        }

        private SetPieceType _activeSetPiece = SetPieceType.None;
        private int _setPieceTaker = -1;
        private int _setPieceTarget = -1;
        private Vector3 _setPieceTargetPosition;
        private float _setPieceStartTime = 0f;

        // Referências
        private BallPhysics _ball;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            _rulesEngine = FootballRulesEngine.Instance;
            _ball = BallPhysics.Instance;

            InitializeFormationPositions();
            SubscribeToEvents();
        }

        void InitializeFormationPositions()
        {
            // Define posições base para cada formação (coordenadas normalizadas -1 a 1)
            // x: -1 (próprio gol) a 1 (gol adversário)
            // z: -1 (lado esquerdo) a 1 (lado direito)

            var positions = new Dictionary<FootballRulesEngine.Formation, Vector3[]>();

            // 4-4-2
            positions[FootballRulesEngine.Formation.FourFourTwo] = new Vector3[]
            {
                new Vector3(-0.9f, 0f, 0f),           // GK
                new Vector3(-0.6f, 0f, -0.4f),        // RB
                new Vector3(-0.6f, 0f, 0f),           // CB1
                new Vector3(-0.6f, 0f, 0.4f),         // CB2
                new Vector3(-0.6f, 0f, 0.4f),         // LB
                new Vector3(-0.2f, 0f, -0.7f),        // RM
                new Vector3(-0.2f, 0f, -0.2f),        // CM1
                new Vector3(-0.2f, 0f, 0.2f),         // CM2
                new Vector3(-0.2f, 0f, 0.7f),         // LM
                new Vector3(0.4f, 0f, -0.2f),         // ST1
                new Vector3(0.4f, 0f, 0.2f)           // ST2
            };

            // 4-3-3
            positions[FootballRulesEngine.Formation.FourThreeThree] = new Vector3[]
            {
                new Vector3(-0.9f, 0f, 0f),           // GK
                new Vector3(-0.6f, 0f, -0.4f),        // RB
                new Vector3(-0.6f, 0f, 0f),           // CB1
                new Vector3(-0.6f, 0f, 0.4f),         // CB2
                new Vector3(-0.6f, 0f, 0.4f),         // LB
                new Vector3(-0.1f, 0f, 0f),           // CDM
                new Vector3(-0.1f, 0f, -0.3f),        // CM1
                new Vector3(-0.1f, 0f, 0.3f),         // CM2
                new Vector3(0.5f, 0f, -0.6f),         // RW
                new Vector3(0.5f, 0f, 0f),            // ST
                new Vector3(0.5f, 0f, 0.6f)           // LW
            };

            // 4-2-3-1
            positions[FootballRulesEngine.Formation.FourTwoThreeOne] = new Vector3[]
            {
                new Vector3(-0.9f, 0f, 0f),           // GK
                new Vector3(-0.6f, 0f, -0.4f),        // RB
                new Vector3(-0.6f, 0f, 0f),           // CB1
                new Vector3(-0.6f, 0f, 0.4f),         // CB2
                new Vector3(-0.6f, 0f, 0.4f),         // LB
                new Vector3(-0.3f, 0f, -0.2f),        // CDM1
                new Vector3(-0.3f, 0f, 0.2f),         // CDM2
                new Vector3(0.1f, 0f, -0.5f),         // RW
                new Vector3(0.1f, 0f, 0f),            // CAM
                new Vector3(0.1f, 0f, 0.5f),          // LW
                new Vector3(0.5f, 0f, 0f)             // ST
            };

            // 3-5-2
            positions[FootballRulesEngine.Formation.ThreeFiveTwo] = new Vector3[]
            {
                new Vector3(-0.9f, 0f, 0f),           // GK
                new Vector3(-0.5f, 0f, -0.3f),        // CB1 (left)
                new Vector3(-0.5f, 0f, 0f),           // CB2 (center)
                new Vector3(-0.5f, 0f, 0.3f),         // CB3 (right)
                new Vector3(-0.1f, 0f, -0.7f),        // RWB
                new Vector3(-0.1f, 0f, -0.3f),        // CM1
                new Vector3(-0.1f, 0f, 0f),           // CM2
                new Vector3(-0.1f, 0f, 0.3f),         // CM3
                new Vector3(-0.1f, 0f, 0.7f),         // LWB
                new Vector3(0.4f, 0f, -0.2f),         // ST1
                new Vector3(0.4f, 0f, 0.2f)           // ST2
            };

            // Converter para coordenadas do mundo
            float halfLength = Arena.HalfLength;
            float fenceZ = Arena.FenceZ;

            if (positions.ContainsKey(formation))
            {
                var basePositions = positions[formation];
                for (int i = 0; i < basePositions.Length; i++)
                {
                    Vector3 norm = basePositions[i];
                    // Inverter x para time visitante
                    float worldX = teamId == 0 ?
                        Mathf.Lerp(-halfLength, halfLength, (norm.x + 1f) * 0.5f) :
                        Mathf.Lerp(halfLength, -halfLength, (norm.x + 1f) * 0.5f);
                    float worldZ = Mathf.Lerp(-fenceZ, fenceZ, (norm.z + 1f) * 0.5f);

                    _formationPositions[i] = new Vector3(worldX, 0f, worldZ);
                    _defensivePositions[i] = new Vector3(worldX * 0.8f, 0f, worldZ * 0.9f); // Mais recuado e compacto
                    _offensivePositions[i] = new Vector3(worldX * 1.1f, 0f, worldZ * 1.1f); // Mais adiantado e aberto
                }
            }
        }

        void SubscribeToEvents()
        {
            if (_rulesEngine != null)
            {
                _rulesEngine.OnMatchEvent += OnMatchEvent;
                _rulesEngine.OnPhaseChanged += OnPhaseChanged;
            }
        }

        public void RegisterPlayer(int playerId, PlayerController controller, PlayerAttributes attributes, PlayerMentality mentality)
        {
            _playersOnField.Add(playerId);
            _playerControllers[playerId] = controller;
            _playerAttributes[playerId] = attributes;
            _playerMentalities[playerId] = mentality;
        }

        public void UnregisterPlayer(int playerId)
        {
            _playersOnField.Remove(playerId);
            _playerControllers.Remove(playerId);
            _playerAttributes.Remove(playerId);
            _playerMentalities.Remove(playerId);
        }

        void Update()
        {
            if (_rulesEngine == null || _ball == null) return;
            if (_rulesEngine.CurrentPhase == FootballRulesEngine.MatchPhase.PreMatch ||
                _rulesEngine.CurrentPhase == FootballRulesEngine.MatchPhase.PostMatch)
                return;

            UpdatePossession();
            UpdateTeamPhase();
            UpdatePlayerInstructions();
            UpdateSetPieces();
        }

        // =====================================================================
        // POSSE DE BOLA E FASES
        // =====================================================================

        void UpdatePossession()
        {
            _inPossession = false;
            int ballHolder = -1;

            if (!_ball.IsHeld)
            {
                // Check which team has possession
                float minDist = float.MaxValue;
                foreach (int pid in _playersOnField)
                {
                    if (!_playerControllers.ContainsKey(pid)) continue;
                    var pc = _playerControllers[pid];
                    if (pc == null) continue;

                    if (pc.HasPossession)
                    {
                        _inPossession = true;
                        ballHolder = pid;
                        break;
                    }

                    float dist = Vector3.Distance(pc.transform.position, _ball.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        ballHolder = pid;
                    }
                }

                // If no one has possession, check closest player
                if (!_inPossession && ballHolder != -1)
                {
                    var pc = _playerControllers[ballHolder];
                    if (pc != null && minDist < 3f)
                    {
                        _inPossession = true;
                    }
                }
            }
            else
            {
                // Ball is held by someone
                _inPossession = true;
                ballHolder = _ball.Holder != null ? FindPlayerId(_ball.Holder) : -1;
            }

            // Detect possession change
            if (_inPossession != _lastPossession)
            {
                if (!_inPossession && _lastPossession)
                {
                    // Lost possession
                    OnPossessionLost(ballHolder);
                }
                else if (_inPossession && !_lastPossession)
                {
                    // Gained possession
                    OnPossessionGained(ballHolder);
                }
                _lastPossession = _inPossession;
            }
        }

        void OnPossessionLost(int playerId)
        {
            _ballPositionAtLoss = _ball.transform.position;
            _playerWhoLostBall = playerId;
            _currentPhase = TeamPhase.DefensiveTransition;
            _phaseStartTime = Time.time;
            _counterPressingEndTime = Time.time + counterPressingDuration;

            // Instruir time a pressionar imediatamente
            SetTeamInstruction(TeamInstruction.CounterPress);
        }

        void OnPossessionGained(int playerId)
        {
            _currentPhase = TeamPhase.OffensiveTransition;
            _phaseStartTime = Time.time;

            // Check if counter-attack opportunity
            if (IsCounterAttackOpportunity())
            {
                _currentPhase = TeamPhase.CounterAttack;
                SetTeamInstruction(TeamInstruction.CounterAttack);
            }
            else
            {
                SetTeamInstruction(TeamInstruction.BuildUp);
            }
        }

        bool IsCounterAttackOpportunity()
        {
            if (_playerWhoLostBall == -1) return false;

            // Counter-attack if:
            // 1. Ball won in defensive half
            // 2. Few defenders between ball and goal
            // 3. Fast players available

            Vector3 ballPos = _ball.transform.position;
            bool inDefensiveHalf = teamId == 0 ? ballPos.x < 0f : ballPos.x > 0f;
            if (!inDefensiveHalf) return false;

            int defendersBetween = CountDefendersBetween(ballPos, GetOpponentGoal());
            if (defendersBetween > 3) return false;

            // Check if we have fast players forward
            foreach (int pid in _playersOnField)
            {
                if (!_playerAttributes.ContainsKey(pid)) continue;
                var attrs = _playerAttributes[pid];
                if (attrs.pace > 80 && attrs.offTheBall > 70)
                {
                    var pc = _playerControllers[pid];
                    if (pc != null)
                    {
                        Vector3 toGoal = GetOpponentGoal() - pc.transform.position;
                        toGoal.y = 0f;
                        if (Vector3.Dot(pc.Velocity.normalized, toGoal.normalized) > 0.5f)
                            return true;
                    }
                }
            }

            return false;
        }

        void UpdateTeamPhase()
        {
            float timeInPhase = Time.time - _phaseStartTime;

            // Transition from defensive transition to organization
            if (_currentPhase == TeamPhase.DefensiveTransition && timeInPhase > 3f)
            {
                _currentPhase = TeamPhase.DefensiveOrganization;
                SetTeamInstruction(TeamInstruction.HoldShape);
            }

            // Transition from offensive transition to organization
            if (_currentPhase == TeamPhase.OffensiveTransition && timeInPhase > 5f)
            {
                _currentPhase = TeamPhase.OffensiveOrganization;
                SetTeamInstruction(TeamInstruction.BuildUp);
            }

            // Counter-attack timeout
            if (_currentPhase == TeamPhase.CounterAttack && timeInPhase > 8f)
            {
                _currentPhase = TeamPhase.OffensiveOrganization;
                SetTeamInstruction(TeamInstruction.BuildUp);
            }

            // Counter-pressing timeout
            if (Time.time > _counterPressingEndTime && _currentPhase == TeamPhase.DefensiveTransition)
            {
                _currentPhase = TeamPhase.DefensiveOrganization;
                SetTeamInstruction(TeamInstruction.HoldShape);
            }
        }

        // =====================================================================
        // INSTRUÇÕES PARA JOGADORES
        // =====================================================================

        enum TeamInstruction
        {
            None,
            HoldShape,
            HighPress,
            CounterPress,
            CounterAttack,
            BuildUp,
            OverloadWings,
            PlayThroughCenter,
            ShootOnSight,
            TimeWasting,
            OffsideTrap,
            ManToManMarking,
            ZonalMarking
        }

        private TeamInstruction _currentInstruction = TeamInstruction.None;

        void SetTeamInstruction(TeamInstruction instruction)
        {
            _currentInstruction = instruction;
            // Instructions are applied in UpdatePlayerInstructions
        }

        void UpdatePlayerInstructions()
        {
            foreach (int pid in _playersOnField)
            {
                if (!_playerControllers.ContainsKey(pid)) continue;
                var pc = _playerControllers[pid];
                if (pc == null || pc.aiControlled) continue; // Only control AI players

                var attrs = _playerAttributes.ContainsKey(pid) ? _playerAttributes[pid] : null;
                var mentality = _playerMentalities.ContainsKey(pid) ? _playerMentalities[pid] : null;
                if (attrs == null || mentality == null) continue;

                // Get formation index for this player
                int formationIndex = _playersOnField.IndexOf(pid);
                if (formationIndex < 0) formationIndex = 0;

                Vector3 targetPosition = CalculateTargetPosition(pid, formationIndex, attrs, mentality);
                Vector3 moveDirection = (targetPosition - pc.transform.position).normalized;

                // Apply instruction modifiers
                moveDirection = ApplyInstructionModifier(pid, moveDirection, attrs, mentality);

                // Set AI inputs
                pc.aiControlled = true;
                pc.aiMoveWorld = moveDirection;
                pc.aiSprint = ShouldSprint(pid, attrs, mentality);
                pc.aiShoot = ShouldShoot(pid, attrs, mentality);
                pc.aiFinesse = ShouldFinesse(pid, attrs, mentality);
                pc.aiChip = ShouldChip(pid, attrs, mentality);
                pc.aiLow = ShouldLowShot(pid, attrs, mentality);
                pc.aiPass = ShouldPass(pid, attrs, mentality);
                pc.aiThroughBall = ShouldThroughBall(pid, attrs, mentality);
                pc.aiCross = ShouldCross(pid, attrs, mentality);
                pc.aiAimWorld = GetAimTarget(pid, attrs, mentality);
            }
        }

        Vector3 CalculateTargetPosition(int playerId, int formationIndex, PlayerAttributes attrs, PlayerMentality mentality)
        {
            Vector3 basePos = _formationPositions.ContainsKey(formationIndex) ?
                _formationPositions[formationIndex] : Vector3.zero;

            Vector3 defensivePos = _defensivePositions.ContainsKey(formationIndex) ?
                _defensivePositions[formationIndex] : basePos;

            Vector3 offensivePos = _offensivePositions.ContainsKey(formationIndex) ?
                _offensivePositions[formationIndex] : basePos;

            // Blend based on phase and mentality
            float offensiveWeight = 0f;
            switch (_currentPhase)
            {
                case TeamPhase.DefensiveOrganization:
                case TeamPhase.DefensiveTransition:
                    offensiveWeight = 0.1f;
                    break;
                case TeamPhase.OffensiveOrganization:
                    offensiveWeight = 0.7f;
                    break;
                case TeamPhase.OffensiveTransition:
                case TeamPhase.CounterAttack:
                    offensiveWeight = 0.9f;
                    break;
                case TeamPhase.SetPiece:
                    offensiveWeight = 0.5f;
                    break;
            }

            // Adjust by team mentality
            offensiveWeight *= GetMentalityOffensiveFactor();

            // Adjust by player mentality
            offensiveWeight *= Mathf.Lerp(0.5f, 1.5f, mentality.attackingIntent);

            Vector3 target = Vector3.Lerp(defensivePos, offensivePos, offensiveWeight);

            // Adjust for ball position (support)
            if (_ball != null && !_ball.IsHeld)
            {
                Vector3 ballPos = _ball.transform.position;
                Vector3 toBall = ballPos - target;
                toBall.y = 0f;
                float distToBall = toBall.magnitude;

                // Support distance based on player role
                float supportDist = GetSupportDistance(attrs, mentality);
                if (distToBall > supportDist * 1.5f)
                {
                    // Move towards ball to support
                    target = Vector3.Lerp(target, ballPos - toBall.normalized * supportDist, 0.3f);
                }
            }

            // Marking assignments in defensive phase
            if (_currentPhase == TeamPhase.DefensiveOrganization || _currentPhase == TeamPhase.DefensiveTransition)
            {
                target = ApplyDefensivePositioning(playerId, target, attrs, mentality);
            }

            return target;
        }

        float GetMentalityOffensiveFactor()
        {
            switch (mentality)
            {
                case FootballRulesEngine.TeamMentality.VeryDefensive: return 0.3f;
                case FootballRulesEngine.TeamMentality.Defensive: return 0.5f;
                case FootballRulesEngine.TeamMentality.Balanced: return 0.7f;
                case FootballRulesEngine.TeamMentality.Attacking: return 1.0f;
                case FootballRulesEngine.TeamMentality.VeryAttacking: return 1.3f;
                default: return 0.7f;
            }
        }

        float GetSupportDistance(PlayerAttributes attrs, PlayerMentality mentality)
        {
            float baseDist = 10f;
            baseDist *= Mathf.Lerp(0.8f, 1.2f, mentality.supportDistance);
            baseDist *= Mathf.Lerp(0.8f, 1.2f, attrs.offTheBall / 99f);
            return baseDist;
        }

        Vector3 ApplyDefensivePositioning(int playerId, Vector3 target, PlayerAttributes attrs, PlayerMentality mentality)
        {
            // Assign marking targets
            int opponentCount = _rulesEngine.HomeTeam.playersOnField.Count + _rulesEngine.AwayTeam.playersOnField.Count;
            // Simplified: mark closest opponent in zone
            FootballRulesEngine.TeamState opponents = teamId == 0 ? _rulesEngine.AwayTeam : _rulesEngine.HomeTeam;

            float bestDist = float.MaxValue;
            int markTarget = -1;
            foreach (int oppId in opponents.playersOnField)
            {
                if (!_rulesEngine.AllPlayers.ContainsKey(oppId)) continue;
                var opp = _rulesEngine.AllPlayers[oppId];
                if (opp == null) continue;

                float dist = Vector3.Distance(target, opp.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    markTarget = oppId;
                }
            }

            if (markTarget != -1 && bestDist < 15f)
            {
                var markTargetObj = _rulesEngine.AllPlayers[markTarget];
                Vector3 markPos = markTargetObj.transform.position;

                // Position between opponent and goal
                Vector3 goalPos = GetOwnGoal();
                Vector3 toGoal = goalPos - markPos;
                toGoal.y = 0f;

                // Intercept position
                Vector3 interceptPos = markPos + toGoal.normalized * 3f;
                interceptPos = Vector3.Lerp(target, interceptPos, mentality.defensiveDuty * 0.5f);

                // Offside trap
                if (_currentInstruction == TeamInstruction.OffsideTrap && attrs.positioning > 70)
                {
                    // Push up to opponent's level
                    float offsideLine = GetOffsideLine();
                    if (teamId == 0)
                        interceptPos.x = Mathf.Max(interceptPos.x, offsideLine);
                    else
                        interceptPos.x = Mathf.Min(interceptPos.x, offsideLine);
                }

                return interceptPos;
            }

            return target;
        }

        float GetOffsideLine()
        {
            FootballRulesEngine.TeamState defenders = teamId == 0 ? _rulesEngine.HomeTeam : _rulesEngine.AwayTeam;
            float line = teamId == 0 ? float.MinValue : float.MaxValue;

            foreach (int pid in defenders.playersOnField)
            {
                if (!_rulesEngine.AllPlayers.ContainsKey(pid)) continue;
                var p = _rulesEngine.AllPlayers[pid];
                if (p == null) continue;

                if (teamId == 0)
                    line = Mathf.Max(line, p.transform.position.x);
                else
                    line = Mathf.Min(line, p.transform.position.x);
            }

            return line;
        }

        Vector3 ApplyInstructionModifier(int playerId, Vector3 direction, PlayerAttributes attrs, PlayerMentality mentality)
        {
            switch (_currentInstruction)
            {
                case TeamInstruction.HighPress:
                    // Push up aggressively
                    direction = Vector3.Lerp(direction, GetOpponentGoal() - _playerControllers[playerId].transform.position, 0.5f);
                    break;

                case TeamInstruction.CounterPress:
                    // Immediate pressure on ball
                    if (_ball != null)
                        direction = (_ball.transform.position - _playerControllers[playerId].transform.position).normalized;
                    break;

                case TeamInstruction.CounterAttack:
                    // Direct forward runs
                    direction = Vector3.Lerp(direction, (GetOpponentGoal() - _playerControllers[playerId].transform.position).normalized, 0.7f);
                    break;

                case TeamInstruction.OverloadWings:
                    // Wide players push wider
                    var pc = _playerControllers[playerId];
                    if (pc != null)
                    {
                        float z = pc.transform.position.z;
                        if (teamId == 0)
                            direction += new Vector3(0f, 0f, z > 0 ? 0.5f : -0.5f);
                        else
                            direction += new Vector3(0f, 0f, z > 0 ? -0.5f : 0.5f);
                    }
                    break;

                case TeamInstruction.PlayThroughCenter:
                    // Central players prioritized
                    pc = _playerControllers[playerId];
                    if (pc != null)
                    {
                        float z = Mathf.Abs(pc.transform.position.z);
                        if (z < 10f) // Near center
                            direction = Vector3.Lerp(direction, (GetOpponentGoal() - pc.transform.position).normalized, 0.5f);
                    }
                    break;

                case TeamInstruction.OffsideTrap:
                    // Push defensive line up
                    pc = _playerControllers[playerId];
                    if (pc != null && IsDefender(attrs.preferredPosition))
                    {
                        float line = GetOffsideLine();
                        if (teamId == 0)
                            direction.x = Mathf.Max(direction.x, line - pc.transform.position.x + 2f);
                        else
                            direction.x = Mathf.Min(direction.x, line - pc.transform.position.x - 2f);
                    }
                    break;
            }

            return direction;
        }

        bool IsDefender(PlayerPosition pos)
        {
            return pos == PlayerPosition.CB || pos == PlayerPosition.FB || pos == PlayerPosition.WB;
        }

        bool ShouldSprint(int playerId, PlayerAttributes attrs, PlayerMentality mentality)
        {
            var pc = _playerControllers[playerId];
            if (pc == null) return false;

            // Sprint in counter-attacks, when chasing ball, or breaking forward
            if (_currentPhase == TeamPhase.CounterAttack) return true;
            if (_currentInstruction == TeamInstruction.CounterPress) return true;

            // Sprint if space ahead
            Vector3 forward = pc.transform.forward;
            RaycastHit hit;
            if (!Physics.Raycast(pc.transform.position + Vector3.up, forward, out hit, 15f))
                return true;

            return attrs.pace > 70 && mentality.attackingIntent > 0.6f && UnityEngine.Random.value < 0.3f;
        }

        bool ShouldShoot(int playerId, PlayerAttributes attrs, PlayerMentality mentality)
        {
            var pc = _playerControllers[playerId];
            if (pc == null || _ball == null) return false;

            if (!_ball.IsHeld || pc != _ball.Holder?.GetComponent<PlayerController>()) return false;

            Vector3 ballPos = _ball.transform.position;
            Vector3 goalPos = GetOpponentGoal();
            float distToGoal = Vector3.Distance(ballPos, goalPos);

            // Shoot if good opportunity
            if (distToGoal < 25f && mentality.shootingFrequency > 0.5f)
            {
                // Check angle
                Vector3 toGoal = (goalPos - ballPos).normalized;
                Vector3 toGoalFlat = new Vector3(toGoal.x, 0f, toGoal.z).normalized;
                Vector3 forward = pc.transform.forward;
                float angle = Vector3.Angle(forward, toGoalFlat);
                if (angle < 45f) return true;
            }

            // Long shots
            if (distToGoal > 25f && distToGoal < 35f && mentality.shootingFrequency > 0.7f)
            {
                if ((attrs.traits & PlayerTrait.LongShotTaker) != 0)
                    return UnityEngine.Random.value < 0.2f;
            }

            return false;
        }

        bool ShouldFinesse(int playerId, PlayerAttributes attrs, PlayerMentality mentality)
        {
            return (attrs.traits & PlayerTrait.FreeKickSpecialist) != 0 ||
                   (attrs.traits & PlayerTrait.Playmaker) != 0;
        }

        bool ShouldChip(int playerId, PlayerAttributes attrs, PlayerMentality mentality)
        {
            var pc = _playerControllers[playerId];
            if (pc == null || _ball == null) return false;

            Vector3 ballPos = _ball.transform.position;
            Vector3 goalPos = GetOpponentGoal();
            float distToGoal = Vector3.Distance(ballPos, goalPos);

            // Chip if GK off line
            if (distToGoal < 20f)
            {
                // Check GK position
                FootballRulesEngine.TeamState opp = teamId == 0 ? _rulesEngine.AwayTeam : _rulesEngine.HomeTeam;
                foreach (int pid in opp.playersOnField)
                {
                    if (!_rulesEngine.PlayerAttributes.ContainsKey(pid)) continue;
                    var oppAttrs = _rulesEngine.PlayerAttributes[pid];
                    if (oppAttrs.preferredPosition == PlayerPosition.GK)
                    {
                        var gk = _rulesEngine.AllPlayers[pid];
                        if (gk != null)
                        {
                            float gkDistToLine = teamId == 0 ? gk.transform.position.x - (-Arena.HalfLength) : Arena.HalfLength - gk.transform.position.x;
                            if (gkDistToLine > 5f) return true;
                        }
                    }
                }
            }

            return false;
        }

        bool ShouldLowShot(int playerId, PlayerAttributes attrs, PlayerMentality mentality)
        {
            return (attrs.traits & PlayerTrait.Poacher) != 0;
        }

        bool ShouldPass(int playerId, PlayerAttributes attrs, PlayerMentality mentality)
        {
            var pc = _playerControllers[playerId];
            if (pc == null || _ball == null || !pc.HasPossession) return false;

            // Pass if teammate is in good position and not too much pressure
            // High passing attribute increases pass frequency
            if (attrs.passing < 60) return false;

            // Check for nearby teammates
            foreach (int teammateId in _playersOnField)
            {
                if (teammateId == playerId) continue;
                if (!_playerControllers.ContainsKey(teammateId)) continue;
                var teammate = _playerControllers[teammateId];
                if (teammate == null) continue;

                float dist = Vector3.Distance(pc.transform.position, teammate.transform.position);
                if (dist < 25f && dist > 3f)
                {
                    // Teammate in passing range
                    Vector3 toTeammate = teammate.transform.position - pc.transform.position;
                    Vector3 toGoal = GetOpponentGoal() - pc.transform.position;
                    toTeammate.y = 0f; toGoal.y = 0f;

                    // Prefer forward/forward-side passes
                    if (Vector3.Dot(toTeammate.normalized, toGoal.normalized) > 0.3f)
                    {
                        // Check if passing lane is open (no opponents in between)
                        if (IsPassingLaneOpen(pc.transform.position, teammate.transform.position))
                        {
                            // Higher passing attribute = more likely to pass
                            float passChance = Mathf.Lerp(0.15f, 0.5f, attrs.passing / 99f);
                            passChance *= mentality.creativeFreedom;
                            return UnityEngine.Random.value < passChance;
                        }
                    }
                }
            }
            return false;
        }

        bool ShouldThroughBall(int playerId, PlayerAttributes attrs, PlayerMentality mentality)
        {
            var pc = _playerControllers[playerId];
            if (pc == null || _ball == null || !pc.HasPossession) return false;

            // Through ball requires good vision and passing
            if (attrs.passing < 70 || attrs.vision < 65) return false;

            // Look for forward runs
            foreach (int teammateId in _playersOnField)
            {
                if (teammateId == playerId) continue;
                if (!_playerControllers.ContainsKey(teammateId)) continue;
                var teammate = _playerControllers[teammateId];
                if (teammate == null) continue;

                float dist = Vector3.Distance(pc.transform.position, teammate.transform.position);
                if (dist < 35f && dist > 10f)
                {
                    Vector3 toTeammate = teammate.transform.position - pc.transform.position;
                    Vector3 teammateVel = teammate.Velocity;
                    teammateVel.y = 0f;

                    // Teammate making forward run
                    if (teammateVel.magnitude > 3f)
                    {
                        Vector3 toGoal = GetOpponentGoal() - pc.transform.position;
                        toGoal.y = 0f;
                        if (Vector3.Dot(toTeammate.normalized, toGoal.normalized) > 0.5f &&
                            Vector3.Dot(teammateVel.normalized, toGoal.normalized) > 0.5f)
                        {
                            if (IsPassingLaneOpen(pc.transform.position, teammate.transform.position + teammateVel * 1.5f))
                            {
                                float throughBallChance = Mathf.Lerp(0.05f, 0.25f, (attrs.passing + attrs.vision) / 198f);
                                throughBallChance *= mentality.throughBallFrequency;
                                return UnityEngine.Random.value < throughBallChance;
                            }
                        }
                    }
                }
            }
            return false;
        }

        bool ShouldCross(int playerId, PlayerAttributes attrs, PlayerMentality mentality)
        {
            var pc = _playerControllers[playerId];
            if (pc == null || _ball == null || !pc.HasPossession) return false;

            // Cross from wide positions
            Vector3 pos = pc.transform.position;
            bool isWide = Mathf.Abs(pos.z) > Arena.FenceZ * 0.6f;
            bool isInFinalThird = (teamId == 0 ? pos.x > Arena.HalfLength * 0.3f : pos.x < -Arena.HalfLength * 0.3f);

            if (!isWide || !isInFinalThird) return false;

            // Need good crossing attribute
            if (attrs.crossing < 65) return false;

            // Check for teammates in box
            int teammatesInBox = 0;
            foreach (int teammateId in _playersOnField)
            {
                if (teammateId == playerId) continue;
                if (!_playerControllers.ContainsKey(teammateId)) continue;
                var teammate = _playerControllers[teammateId];
                if (teammate == null) continue;

                Vector3 tPos = teammate.transform.position;
                bool inBox = (teamId == 0 ? tPos.x > Arena.HalfLength - 18f : tPos.x < -Arena.HalfLength + 18f) &&
                             Mathf.Abs(tPos.z) < Arena.GoalHalfWidth * 2f;
                if (inBox) teammatesInBox++;
            }

            if (teammatesInBox == 0) return false;

            float crossChance = Mathf.Lerp(0.1f, 0.4f, attrs.crossing / 99f);
            crossChance *= mentality.crossingFrequency;
            return UnityEngine.Random.value < crossChance;
        }

        bool IsPassingLaneOpen(Vector3 from, Vector3 to)
        {
            // Check if any opponent is in the passing lane
            if (_ball == null) return true;

            Vector3 laneDir = (to - from).normalized;
            float laneDist = Vector3.Distance(from, to);
            float laneWidth = 1.5f; // Width of passing lane

            foreach (int oppId in _playersOnField)
            {
                // This would need opponent team's players - simplified for now
                // In a real implementation, we'd check the opposing team's players
            }
            return true; // Simplified - assume open for now
        }

        Vector3 GetAimTarget(int playerId, PlayerAttributes attrs, PlayerMentality mentality)
        {
            Vector3 goalPos = GetOpponentGoal();
            Vector3 ballPos = _ball != null ? _ball.transform.position : Vector3.zero;

            // Aim for corners generally
            float zOffset = UnityEngine.Random.Range(-Arena.GoalHalfWidth * 0.7f, Arena.GoalHalfWidth * 0.7f);
            float yOffset = UnityEngine.Random.Range(0.5f, Arena.GoalHeight * 0.8f);

            // Adjust for player traits
            if ((attrs.traits & PlayerTrait.FreeKickSpecialist) != 0)
            {
                // Top corners
                yOffset = Arena.GoalHeight * 0.9f;
            }
            if ((attrs.traits & PlayerTrait.Poacher) != 0)
            {
                // Low corners
                yOffset = 0.3f;
            }

            return new Vector3(goalPos.x, yOffset, zOffset);
        }

        // =====================================================================
        // JOGADAS ENSAIADAS (SET PIECES)
        // =====================================================================

        void OnMatchEvent(FootballRulesEngine.MatchEvent evt)
        {
            if (!useSetPieces) return;

            switch (evt.type)
            {
                case FootballRulesEngine.MatchEventType.Corner:
                    SetupCorner(evt.teamId, evt.position);
                    break;
                case FootballRulesEngine.MatchEventType.FreeKick:
                    SetupFreeKick(evt.teamId, evt.position, true); // attacking
                    break;
                case FootballRulesEngine.MatchEventType.Penalty:
                    SetupPenalty(evt.teamId);
                    break;
                case FootballRulesEngine.MatchEventType.GoalKick:
                    SetupGoalKick(evt.teamId);
                    break;
                case FootballRulesEngine.MatchEventType.ThrowIn:
                    SetupThrowIn(evt.teamId, evt.position);
                    break;
            }
        }

        void SetupCorner(int teamId, Vector3 position)
        {
            if (teamId != this.teamId) return; // Only handle our corners

            _activeSetPiece = SetPieceType.Corner;
            _setPieceStartTime = Time.time;
            _setPieceTaker = FindBestCornerTaker();
            _setPieceTarget = FindBestCornerTarget();

            // Position players for corner routine
            PositionForCorner(teamId, position);
        }

        void SetupFreeKick(int teamId, Vector3 position, bool attacking)
        {
            if (teamId != this.teamId) return;

            _activeSetPiece = attacking ? SetPieceType.FreeKickAttacking : SetPieceType.FreeKickDefending;
            _setPieceStartTime = Time.time;
            _setPieceTaker = FindBestFreeKickTaker();
            _setPieceTargetPosition = position;
        }

        void SetupPenalty(int teamId)
        {
            if (teamId != this.teamId) return;

            _activeSetPiece = SetPieceType.Penalty;
            _setPieceStartTime = Time.time;
            _setPieceTaker = penaltyTakerId >= 0 ? penaltyTakerId : FindBestPenaltyTaker();
        }

        void SetupGoalKick(int teamId)
        {
            if (teamId != this.teamId) return;

            _activeSetPiece = SetPieceType.GoalKick;
            _setPieceStartTime = Time.time;
        }

        void SetupThrowIn(int teamId, Vector3 position)
        {
            if (teamId != this.teamId) return;

            _activeSetPiece = SetPieceType.ThrowIn;
            _setPieceStartTime = Time.time;
            _setPieceTargetPosition = position;
        }

        void UpdateSetPieces()
        {
            if (_activeSetPiece == SetPieceType.None) return;

            float elapsed = Time.time - _setPieceStartTime;

            // Timeout after 30 seconds
            if (elapsed > 30f)
            {
                _activeSetPiece = SetPieceType.None;
                return;
            }

            // Execute set piece routine
            switch (_activeSetPiece)
            {
                case SetPieceType.Corner:
                    ExecuteCornerRoutine();
                    break;
                case SetPieceType.FreeKickAttacking:
                    ExecuteFreeKickRoutine();
                    break;
                case SetPieceType.Penalty:
                    ExecutePenaltyRoutine();
                    break;
            }
        }

        int FindBestCornerTaker()
        {
            int bestId = -1;
            float bestScore = -1f;

            foreach (int pid in _playersOnField)
            {
                if (!_playerAttributes.ContainsKey(pid)) continue;
                var attrs = _playerAttributes[pid];
                if (attrs.preferredPosition == PlayerPosition.GK) continue;

                float score = (attrs.crossing + attrs.curve + attrs.technique) / 3f;
                if ((attrs.traits & PlayerTrait.FreeKickSpecialist) != 0) score += 10f;
                if ((attrs.traits & PlayerTrait.Playmaker) != 0) score += 5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = pid;
                }
            }

            return bestId;
        }

        int FindBestCornerTarget()
        {
            int bestId = -1;
            float bestScore = -1f;

            foreach (int pid in _playersOnField)
            {
                if (!_playerAttributes.ContainsKey(pid)) continue;
                var attrs = _playerAttributes[pid];

                float score = (attrs.heading + attrs.jumping + attrs.strength + attrs.positioning) / 4f;
                if ((attrs.traits & PlayerTrait.AerialThreat) != 0) score += 15f;
                if ((attrs.traits & PlayerTrait.TargetMan) != 0) score += 10f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = pid;
                }
            }

            return bestId;
        }

        int FindBestFreeKickTaker()
        {
            int bestId = -1;
            float bestScore = -1f;

            foreach (int pid in _playersOnField)
            {
                if (!_playerAttributes.ContainsKey(pid)) continue;
                var attrs = _playerAttributes[pid];

                float score = (attrs.freeKicks + attrs.curve + attrs.technique) / 3f;
                if ((attrs.traits & PlayerTrait.FreeKickSpecialist) != 0) score += 20f;
                if ((attrs.traits & PlayerTrait.PenaltyTaker) != 0) score += 5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = pid;
                }
            }

            return bestId;
        }

        int FindBestPenaltyTaker()
        {
            int bestId = -1;
            float bestScore = -1f;

            foreach (int pid in _playersOnField)
            {
                if (!_playerAttributes.ContainsKey(pid)) continue;
                var attrs = _playerAttributes[pid];

                float score = (attrs.penalties + attrs.composure + attrs.finishing) / 3f;
                if ((attrs.traits & PlayerTrait.PenaltyTaker) != 0) score += 20f;
                if ((attrs.traits & PlayerTrait.ClutchPlayer) != 0) score += 10f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = pid;
                }
            }

            return bestId;
        }

        void PositionForCorner(int teamId, Vector3 cornerPos)
        {
            // Position players for corner routine
            // This would move AI players to designated zones
            // For now, just log
            Debug.Log($"[TeamAI] Time {teamId} posicionando para escanteio");
        }

        void ExecuteCornerRoutine()
        {
            // Corner routine logic
            // Taker crosses to target
            if (_setPieceTaker >= 0 && _playerControllers.ContainsKey(_setPieceTaker))
            {
                var taker = _playerControllers[_setPieceTaker];
                if (taker != null && taker.HasBall)
                {
                    taker.aiControlled = true;
                    taker.aiShoot = true;
                    taker.aiFinesse = true; // Curved cross
                    taker.aiAimWorld = _setPieceTarget >= 0 && _rulesEngine.AllPlayers.ContainsKey(_setPieceTarget) ?
                        _rulesEngine.AllPlayers[_setPieceTarget].transform.position + Vector3.up * 2f :
                        GetOpponentGoal();
                }
            }

            // Target makes run
            if (_setPieceTarget >= 0 && _playerControllers.ContainsKey(_setPieceTarget))
            {
                var target = _playerControllers[_setPieceTarget];
                if (target != null)
                {
                    target.aiControlled = true;
                    target.aiMoveWorld = (GetOpponentGoal() - target.transform.position).normalized;
                    target.aiSprint = true;
                }
            }

            // Other players make runs
            foreach (int pid in _playersOnField)
            {
                if (pid == _setPieceTaker || pid == _setPieceTarget) continue;
                if (!_playerControllers.ContainsKey(pid)) continue;

                var attrs = _playerAttributes[pid];
                if (attrs.heading > 70 || (attrs.traits & PlayerTrait.AerialThreat) != 0)
                {
                    var pc = _playerControllers[pid];
                    if (pc != null)
                    {
                        pc.aiControlled = true;
                        pc.aiMoveWorld = (GetOpponentGoal() - pc.transform.position).normalized;
                        pc.aiSprint = true;
                    }
                }
            }
        }

        void ExecuteFreeKickRoutine()
        {
            if (_setPieceTaker >= 0 && _playerControllers.ContainsKey(_setPieceTaker))
            {
                var taker = _playerControllers[_setPieceTaker];
                if (taker != null && taker.HasBall)
                {
                    taker.aiControlled = true;
                    taker.aiShoot = true;
                    taker.aiFinesse = true;
                    taker.aiAimWorld = _setPieceTargetPosition;
                }
            }
        }

        void ExecutePenaltyRoutine()
        {
            if (_setPieceTaker >= 0 && _playerControllers.ContainsKey(_setPieceTaker))
            {
                var taker = _playerControllers[_setPieceTaker];
                if (taker != null && taker.HasBall)
                {
                    taker.aiControlled = true;
                    taker.aiShoot = true;
                    taker.aiAimWorld = GetOpponentGoal();
                }
            }
        }

        void OnPhaseChanged(FootballRulesEngine.MatchPhase phase)
        {
            if (phase == FootballRulesEngine.MatchPhase.FirstHalf ||
                phase == FootballRulesEngine.MatchPhase.SecondHalf)
            {
                _currentPhase = TeamPhase.DefensiveOrganization;
                _phaseStartTime = Time.time;
            }
        }

        // =====================================================================
        // UTILITÁRIOS
        // =====================================================================

        int CountDefendersBetween(Vector3 from, Vector3 to)
        {
            FootballRulesEngine.TeamState defenders = teamId == 0 ? _rulesEngine.AwayTeam : _rulesEngine.HomeTeam;

            int count = 0;
            Vector3 direction = (to - from).normalized;
            float distance = Vector3.Distance(from, to);

            foreach (int pid in defenders.playersOnField)
            {
                if (!_rulesEngine.AllPlayers.ContainsKey(pid)) continue;
                var p = _rulesEngine.AllPlayers[pid];
                if (p == null) continue;

                Vector3 toPlayer = p.transform.position - from;
                float proj = Vector3.Dot(toPlayer, direction);
                float perp = Vector3.Dot(toPlayer, new Vector3(-direction.z, 0f, direction.x));

                if (proj > 0f && proj < distance && Mathf.Abs(perp) < 5f)
                    count++;
            }

            return count;
        }

        Vector3 GetOpponentGoal()
        {
            return teamId == 0 ? new Vector3(Arena.HalfLength, 0f, 0f) : new Vector3(-Arena.HalfLength, 0f, 0f);
        }

        Vector3 GetOwnGoal()
        {
            return teamId == 0 ? new Vector3(-Arena.HalfLength, 0f, 0f) : new Vector3(Arena.HalfLength, 0f, 0f);
        }

        int FindPlayerId(Transform holder)
        {
            foreach (var kvp in _playerControllers)
            {
                if (kvp.Value.transform == holder || kvp.Value.transform.IsChildOf(holder))
                    return kvp.Key;
            }
            return -1;
        }
    }
}