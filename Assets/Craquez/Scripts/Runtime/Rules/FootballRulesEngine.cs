using System;
using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Motor de regras do futebol - implementa todas as regras oficiais (IFAB Laws of the Game).
    /// Gerencia: impedimento, faltas, cartões, pênaltis, escanteios, laterais, goleadas, substituições, etc.
    /// </summary>
    public class FootballRulesEngine : MonoBehaviour
    {
        public static FootballRulesEngine Instance { get; private set; }

        [Header("Configurações de Jogo")]
        public int maxPlayersPerTeam = 11;
        public int maxSubstitutes = 7;
        public int maxSubstitutions = 5;
        public float matchDurationMinutes = 90f;
        public float halfTimeDurationMinutes = 15f;
        public float addedTimePerHalf = 3f;

        [Header("Configurações de Impedimento")]
        public float offsideTolerance = 0.05f; // Tolerância em metros
        public bool useVAR = true;

        [Header("Configurações de Faltas")]
        public float foulDetectionRadius = 1.5f;
        public float dangerousPlayHeight = 1.8f;
        public float shoulderChargeAngle = 45f;

        [Header("Configurações de Cartões")]
        public int yellowCardAccumulationLimit = 2; // Cartões para expulsão indireta
        public float dissentTimeWindow = 10f; // Janela para reclamações

        // Estado da partida
        public enum MatchPhase
        {
            PreMatch,
            FirstHalf,
            HalfTime,
            SecondHalf,
            ExtraTimeFirstHalf,
            ExtraTimeHalfTime,
            ExtraTimeSecondHalf,
            Penalties,
            PostMatch
        }

        public enum MatchEventType
        {
            Goal,
            YellowCard,
            RedCard,
            Substitution,
            Offside,
            Foul,
            Penalty,
            Corner,
            GoalKick,
            ThrowIn,
            FreeKick,
            DropBall,
            Injury,
            VARReview,
            VARDecision
        }

        public struct MatchEvent
        {
            public MatchEventType type;
            public float time;
            public int teamId; // 0 = home, 1 = away
            public int playerId;
            public int? playerId2; // Para substituições, faltas com vítima, etc.
            public Vector3 position;
            public string description;
            public object data;
        }

        public struct OffsideCheck
        {
            public bool isOffside;
            public int attackerId;
            public int defenderId;
            public Vector3 offsidePosition;
            public float time;
        }

        public struct FoulInfo
        {
            public int offenderId;
            public int victimId;
            public int teamId;
            public FoulType type;
            public Vector3 position;
            public float severity; // 0-1
            public bool isPenalty;
            public bool isDOGSO; // Denying obvious goal scoring opportunity
            public bool isSPA;   // Stopping promising attack
        }

        public enum FoulType
        {
            None,
            Tripping,
            Pushing,
            Holding,
            Pulling,
            Charging,
            JumpingAt,
            Striking,
            Kicking,
            Handball,
            DangerousPlay,
            Impeding,
            Dissent,
            PersistentInfringement,
            DelayingRestart,
            EnteringWithoutPermission,
            LeavingWithoutPermission,
            UnsportingBehaviour,
            ViolentConduct,
            Spitting,
            DOGSOHandball,
            DOGSOFoul
        }

        [Serializable]
        public class TeamState
        {
            public int teamId;
            public string teamName;
            public int score = 0;
            public int shots = 0;
            public int shotsOnTarget = 0;
            public int corners = 0;
            public int fouls = 0;
            public int offsides = 0;
            public int yellowCards = 0;
            public int redCards = 0;
            public int substitutionsUsed = 0;
            public List<int> playersOnField = new List<int>(); // Player IDs
            public List<int> substitutes = new List<int>();
            public Formation formation = Formation.FourFourTwo;
            public TeamMentality mentality = TeamMentality.Balanced;
        }

        public enum Formation
        {
            FourFourTwo,
            FourThreeThree,
            FourTwoThreeOne,
            FourFiveOne,
            ThreeFiveTwo,
            ThreeFourThree,
            FiveThreeTwo,
            FiveFourOne,
            Custom
        }

        public enum TeamMentality
        {
            VeryDefensive,
            Defensive,
            Balanced,
            Attacking,
            VeryAttacking
        }

        // Estado atual
        public MatchPhase CurrentPhase { get; private set; } = MatchPhase.PreMatch;
        public float CurrentMatchTime { get; private set; } = 0f;
        public int CurrentHalf { get; private set; } = 1;
        public TeamState HomeTeam { get; private set; } = new TeamState { teamId = 0 };
        public TeamState AwayTeam { get; private set; } = new TeamState { teamId = 1 };

        // Eventos
        public event Action<MatchEvent> OnMatchEvent;
        public event Action<OffsideCheck> OnOffsideDetected;
        public event Action<FoulInfo> OnFoulDetected;
        public event Action<int, int> OnGoalScored; // teamId, playerId
        public event Action<int, int, CardType> OnCardShown; // teamId, playerId, cardType
        public event Action<int, int, int> OnSubstitution; // teamId, playerOut, playerIn
        public event Action<MatchPhase> OnPhaseChanged;

        // VAR
        public event Action<string> OnVARReviewStarted;
        public event Action<string, bool> OnVARDecision; // description, overturned

        // Referências
        public BallPhysics Ball => _ball;
        private BallPhysics _ball;
        public RefereeSystem RefereeSystem => _refereeSystem;
        private RefereeSystem _refereeSystem;
        public IReadOnlyDictionary<int, PlayerController> AllPlayers => _allPlayers;
        private Dictionary<int, PlayerController> _allPlayers = new Dictionary<int, PlayerController>();
        public IReadOnlyDictionary<int, PlayerAttributes> PlayerAttributes => _playerAttributes;
        private Dictionary<int, PlayerAttributes> _playerAttributes = new Dictionary<int, PlayerAttributes>();
        public IReadOnlyList<MatchEvent> MatchEvents => _matchEvents;
        private List<MatchEvent> _matchEvents = new List<MatchEvent>();
        public IReadOnlyList<OffsideCheck> OffsideChecks => _offsideChecks;
        private List<OffsideCheck> _offsideChecks = new List<OffsideCheck>();
        public IReadOnlyList<FoulInfo> FoulHistory => _foulHistory;
        private List<FoulInfo> _foulHistory = new List<FoulInfo>();

        // Offside tracking
        public IReadOnlyDictionary<int, Vector3> LastAttackerPositions => _lastAttackerPositions;
        private Dictionary<int, Vector3> _lastAttackerPositions = new Dictionary<int, Vector3>();
        public IReadOnlyDictionary<int, float> LastTouchTimes => _lastTouchTimes;
        private Dictionary<int, float> _lastTouchTimes = new Dictionary<int, float>();
        public int LastTouchTeamId => _lastTouchTeamId;
        private int _lastTouchTeamId = -1;
        public int LastTouchPlayerId => _lastTouchPlayerId;
        private int _lastTouchPlayerId = -1;

        // Foul detection
        public float LastFoulTime => _lastFoulTime;
        private float _lastFoulTime = -10f;
        public int ConsecutiveFoulsByPlayer => _consecutiveFoulsByPlayer;
        private int _consecutiveFoulsByPlayer = 0;
        public int LastFoulPlayerId => _lastFoulPlayerId;
        private int _lastFoulPlayerId = -1;

        // Advantage
        public bool AdvantagePlaying => _advantagePlaying;
        private bool _advantagePlaying = false;
        public float AdvantageStartTime => _advantageStartTime;
        private float _advantageStartTime = 0f;
        public int AdvantageTeamId => _advantageTeamId;
        private int _advantageTeamId = -1;
        private const float ADVANTAGE_DURATION = 3f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            _ball = BallPhysics.Instance;
            _refereeSystem = RefereeSystem.Instance;

            if (_ball != null)
            {
                BallPhysics.OnShot += OnBallShot;
                _ball.OnImpact += OnBallImpact;
            }
        }

        // =====================================================================
        // INICIALIZAÇÃO DA PARTIDA
        // =====================================================================

        public void InitializeMatch(TeamState homeTeam, TeamState awayTeam, float duration = 90f)
        {
            HomeTeam = homeTeam;
            AwayTeam = awayTeam;
            matchDurationMinutes = duration;

            CurrentPhase = MatchPhase.FirstHalf;
            CurrentMatchTime = 0f;
            CurrentHalf = 1;
            _matchEvents.Clear();
            _offsideChecks.Clear();
            _foulHistory.Clear();

            // Reset player stats
            foreach (var pid in HomeTeam.playersOnField)
                if (_playerAttributes.ContainsKey(pid)) _playerAttributes[pid].ResetMatchStats();
            foreach (var pid in AwayTeam.playersOnField)
                if (_playerAttributes.ContainsKey(pid)) _playerAttributes[pid].ResetMatchStats();

            LogEvent(new MatchEvent
            {
                type = MatchEventType.Foul, // Using Foul as generic start event
                time = 0f,
                teamId = -1,
                description = "Início da partida"
            });

            OnPhaseChanged?.Invoke(CurrentPhase);
        }

        public void RegisterPlayer(int playerId, PlayerController controller, PlayerAttributes attributes, PlayerMentality mentality = null)
        {
            _allPlayers[playerId] = controller;
            _playerAttributes[playerId] = attributes;
            if (mentality != null)
            {
                // Store mentality if needed for rules engine
                // Could add a _playerMentalities dictionary if needed
            }
        }

        public void UnregisterPlayer(int playerId)
        {
            _allPlayers.Remove(playerId);
            _playerAttributes.Remove(playerId);
        }

        // =====================================================================
        // LOOP PRINCIPAL
        // =====================================================================

        void Update()
        {
            if (CurrentPhase == MatchPhase.PreMatch || CurrentPhase == MatchPhase.PostMatch ||
                CurrentPhase == MatchPhase.HalfTime || CurrentPhase == MatchPhase.ExtraTimeHalfTime)
                return;

            CurrentMatchTime += Time.deltaTime;

            // Check for half/full time
            CheckPhaseTransitions();

            // Offside detection (continuous)
            if (CurrentPhase != MatchPhase.HalfTime)
                CheckOffsides();

            // Advantage check
            CheckAdvantage();

            // Ball out of play
            CheckBallOutOfPlay();
        }

        void CheckPhaseTransitions()
        {
            float halfDuration = matchDurationMinutes * 60f / 2f;
            float fullDuration = matchDurationMinutes * 60f;

            if (CurrentPhase == MatchPhase.FirstHalf && CurrentMatchTime >= halfDuration)
            {
                EndHalf();
            }
            else if (CurrentPhase == MatchPhase.SecondHalf && CurrentMatchTime >= fullDuration)
            {
                EndMatch();
            }
        }

        void EndHalf()
        {
            if (CurrentHalf == 1)
            {
                CurrentPhase = MatchPhase.HalfTime;
                CurrentHalf = 2;
                LogEvent(new MatchEvent
                {
                    type = MatchEventType.Foul,
                    time = CurrentMatchTime,
                    teamId = -1,
                    description = "Fim do primeiro tempo"
                });
                OnPhaseChanged?.Invoke(CurrentPhase);

                // Auto-resume after half-time delay in real implementation
                // For now, immediately start second half for testing
                Invoke(nameof(StartSecondHalf), 2f);
            }
        }

        void StartSecondHalf()
        {
            CurrentPhase = MatchPhase.SecondHalf;
            LogEvent(new MatchEvent
            {
                type = MatchEventType.Foul,
                time = CurrentMatchTime,
                teamId = -1,
                description = "Início do segundo tempo"
            });
            OnPhaseChanged?.Invoke(CurrentPhase);
        }

        void EndMatch()
        {
            CurrentPhase = MatchPhase.PostMatch;
            LogEvent(new MatchEvent
            {
                type = MatchEventType.Foul,
                time = CurrentMatchTime,
                teamId = -1,
                description = $"Fim da partida: {HomeTeam.teamName} {HomeTeam.score} x {AwayTeam.score} {AwayTeam.teamName}"
            });
            OnPhaseChanged?.Invoke(CurrentPhase);
        }

        // =====================================================================
        // IMPEDIMENTO (OFFSIDE) - Lei 11
        // =====================================================================

        void CheckOffsides()
        {
            if (_ball == null || _ball.IsHeld) return;

            // Only check when ball is played forward by attacking team
            int attackingTeam = _lastTouchTeamId;
            if (attackingTeam == -1) return;

            TeamState attackingTeamState = attackingTeam == 0 ? HomeTeam : AwayTeam;
            TeamState defendingTeamState = attackingTeam == 0 ? AwayTeam : HomeTeam;

            // Get ball position and direction
            Vector3 ballPos = _ball.transform.position;
            Vector3 ballVel = _ball.Velocity;
            Vector3 ballVelFlat = new Vector3(ballVel.x, 0f, ballVel.z);

            // Ball must be moving towards opponent's goal
            bool towardsOpponentGoal = attackingTeam == 0 ? ballVelFlat.x > 0.5f : ballVelFlat.x < -0.5f;
            if (!towardsOpponentGoal) return;

            // Check each attacker
            foreach (int attackerId in attackingTeamState.playersOnField)
            {
                if (!_allPlayers.ContainsKey(attackerId)) continue;
                var attacker = _allPlayers[attackerId];
                if (attacker == null) continue;

                Vector3 attackerPos = attacker.transform.position;

                // Attacker must be in opponent's half
                bool inOpponentHalf = attackingTeam == 0 ? attackerPos.x > 0f : attackerPos.x < 0f;
                if (!inOpponentHalf) continue;

                // Attacker must be ahead of ball
                bool aheadOfBall = attackingTeam == 0 ? attackerPos.x > ballPos.x + offsideTolerance : attackerPos.x < ballPos.x - offsideTolerance;
                if (!aheadOfBall) continue;

                // Check if level with second-last defender
                int secondLastDefenderId = GetSecondLastDefender(defendingTeamState, attackingTeam);
                if (secondLastDefenderId == -1) continue;

                var secondLastDefender = _allPlayers[secondLastDefenderId];
                if (secondLastDefender == null) continue;

                Vector3 defenderPos = secondLastDefender.transform.position;
                bool levelWithDefender = attackingTeam == 0 ?
                    attackerPos.x <= defenderPos.x + offsideTolerance :
                    attackerPos.x >= defenderPos.x - offsideTolerance;

                if (!levelWithDefender)
                {
                    // OFFSIDE!
                    var check = new OffsideCheck
                    {
                        isOffside = true,
                        attackerId = attackerId,
                        defenderId = secondLastDefenderId,
                        offsidePosition = attackerPos,
                        time = CurrentMatchTime
                    };

                    _offsideChecks.Add(check);
                    OnOffsideDetected?.Invoke(check);

                    // Award indirect free kick to defending team
                    AwardFreeKick(defendingTeamState.teamId, attackerPos, FoulType.None, false);
                    LogEvent(new MatchEvent
                    {
                        type = MatchEventType.Offside,
                        time = CurrentMatchTime,
                        teamId = attackingTeam,
                        playerId = attackerId,
                        position = attackerPos,
                        description = $"Impedimento: {GetPlayerName(attackerId)}"
                    });

                    // Ball goes to defending team
                    _ball.PlaceAt(attackerPos);
                    break; // Only one offside per play
                }
            }
        }

        int GetSecondLastDefender(TeamState defendingTeam, int attackingTeam)
        {
            // Get all defenders positions
            var defenders = new List<(int id, float x)>();
            foreach (int pid in defendingTeam.playersOnField)
            {
                if (!_allPlayers.ContainsKey(pid)) continue;
                var p = _allPlayers[pid];
                if (p == null) continue;
                defenders.Add((pid, p.transform.position.x));
            }

            if (defenders.Count < 2) return -1;

            // Sort by x position (closest to own goal line)
            defenders.Sort((a, b) => attackingTeam == 0 ?
                a.x.CompareTo(b.x) : // Defending team 1 (right side), sort ascending (closest to x=negative)
                b.x.CompareTo(a.x)); // Defending team 0 (left side), sort descending (closest to x=positive)

            // Return second-last (index 1 since GK is usually first)
            return defenders[1].id;
        }

        // =====================================================================
        // FALTAS E CARTÕES - Lei 12
        // =====================================================================

        public void DetectFoul(int offenderId, int victimId, Vector3 position, FoulType type, float severity)
        {
            if (!_allPlayers.ContainsKey(offenderId) || !_allPlayers.ContainsKey(victimId)) return;

            var offenderAttrs = _playerAttributes.ContainsKey(offenderId) ? _playerAttributes[offenderId] : null;
            var victimAttrs = _playerAttributes.ContainsKey(victimId) ? _playerAttributes[victimId] : null;

            int offenderTeam = GetPlayerTeam(offenderId);
            int victimTeam = GetPlayerTeam(victimId);

            if (offenderTeam == victimTeam) return; // Same team, no foul

            // Check if in penalty area
            bool isPenalty = IsInPenaltyArea(position, offenderTeam);

            // Check DOGSO (Denying Obvious Goal Scoring Opportunity)
            bool isDOGSO = CheckDOGSO(offenderId, victimId, position, type);

            // Check SPA (Stopping Promising Attack)
            bool isSPA = CheckSPA(offenderId, victimId, position, type);

            var foul = new FoulInfo
            {
                offenderId = offenderId,
                victimId = victimId,
                teamId = offenderTeam,
                type = type,
                position = position,
                severity = severity,
                isPenalty = isPenalty,
                isDOGSO = isDOGSO,
                isSPA = isSPA
            };

            _foulHistory.Add(foul);
            _lastFoulTime = CurrentMatchTime;

            // Track persistent infringement
            if (_lastFoulPlayerId == offenderId)
            {
                _consecutiveFoulsByPlayer++;
            }
            else
            {
                _consecutiveFoulsByPlayer = 1;
                _lastFoulPlayerId = offenderId;
            }

            OnFoulDetected?.Invoke(foul);

            // Determine sanction
            CardType card = DetermineCard(foul, offenderAttrs);
            if (card != CardType.None)
            {
                ShowCard(offenderId, card);
            }

            // Award free kick / penalty
            if (isPenalty)
            {
                AwardPenalty(victimTeam, position);
            }
            else
            {
                AwardFreeKick(victimTeam, position, type, severity > 0.7f);
            }

            LogEvent(new MatchEvent
            {
                type = MatchEventType.Foul,
                time = CurrentMatchTime,
                teamId = offenderTeam,
                playerId = offenderId,
                playerId2 = victimId,
                position = position,
                description = $"Falta: {GetPlayerName(offenderId)} em {GetPlayerName(victimId)} ({type})"
            });
        }

        CardType DetermineCard(FoulInfo foul, PlayerAttributes offender)
        {
            // Direct red card offenses
            if (foul.type == FoulType.ViolentConduct ||
                foul.type == FoulType.Spitting ||
                foul.type == FoulType.DOGSOFoul ||
                foul.type == FoulType.DOGSOHandball)
            {
                return CardType.Red;
            }

            // DOGSO = red card
            if (foul.isDOGSO) return CardType.Red;

            // SPA = yellow card
            if (foul.isSPA) return CardType.Yellow;

            // Severity based
            if (foul.severity > 0.85f) return CardType.Red;
            if (foul.severity > 0.6f) return CardType.Yellow;

            // Persistent infringement
            if (_consecutiveFoulsByPlayer >= 3) return CardType.Yellow;

            // Professional foul (tactical)
            if (foul.type == FoulType.Tripping || foul.type == FoulType.Holding ||
                foul.type == FoulType.Pulling)
            {
                // Check if it stopped a promising attack
                if (IsPromisingAttack(foul.victimId))
                    return CardType.Yellow;
            }

            // Handball
            if (foul.type == FoulType.Handball)
            {
                // Deliberate = yellow, DOGSO = red (handled above)
                return CardType.Yellow;
            }

            // Dissent
            if (foul.type == FoulType.Dissent) return CardType.Yellow;

            // Delaying restart
            if (foul.type == FoulType.DelayingRestart) return CardType.Yellow;

            return CardType.None;
        }

        void ShowCard(int playerId, CardType card)
        {
            if (!_playerAttributes.ContainsKey(playerId)) return;

            var attrs = _playerAttributes[playerId];
            int teamId = GetPlayerTeam(playerId);
            TeamState team = teamId == 0 ? HomeTeam : AwayTeam;

            if (card == CardType.Yellow)
            {
                attrs.yellowCards++;
                team.yellowCards++;

                // Second yellow = red
                if (attrs.yellowCards >= 2)
                {
                    attrs.redCard = true;
                    team.redCards++;
                    attrs.yellowCards = 0; // Reset for red card

                    OnCardShown?.Invoke(teamId, playerId, CardType.Red);
                    LogEvent(new MatchEvent
                    {
                        type = MatchEventType.RedCard,
                        time = CurrentMatchTime,
                        teamId = teamId,
                        playerId = playerId,
                        description = $"{GetPlayerName(playerId)} expulso (2º amarelo)"
                    });

                    // Remove from field
                    team.playersOnField.Remove(playerId);
                    return;
                }

                OnCardShown?.Invoke(teamId, playerId, CardType.Yellow);
                LogEvent(new MatchEvent
                {
                    type = MatchEventType.YellowCard,
                    time = CurrentMatchTime,
                    teamId = teamId,
                    playerId = playerId,
                    description = $"Cartão amarelo: {GetPlayerName(playerId)}"
                });
            }
            else if (card == CardType.Red)
            {
                attrs.redCard = true;
                team.redCards++;

                OnCardShown?.Invoke(teamId, playerId, CardType.Red);
                LogEvent(new MatchEvent
                {
                    type = MatchEventType.RedCard,
                    time = CurrentMatchTime,
                    teamId = teamId,
                    playerId = playerId,
                    description = $"{GetPlayerName(playerId)} expulso (vermelho direto)"
                });

                // Remove from field
                team.playersOnField.Remove(playerId);
            }
        }

        public enum CardType
        {
            None = 0,
            Yellow = 1,
            Red = 2
        }

        bool CheckDOGSO(int offenderId, int victimId, Vector3 position, FoulType type)
        {
            if (!_allPlayers.ContainsKey(victimId)) return false;

            var victim = _allPlayers[victimId];
            if (victim == null) return false;

            // Check if victim had obvious goal scoring opportunity
            // Criteria: distance to goal, direction, defenders, ball control
            Vector3 goalPos = GetOpponentGoal(GetPlayerTeam(victimId));
            float distToGoal = Vector3.Distance(victim.transform.position, goalPos);

            if (distToGoal > 30f) return false; // Too far

            // Direction towards goal
            Vector3 toGoal = (goalPos - victim.transform.position).normalized;
            Vector3 victimVel = victim.Velocity;
            victimVel.y = 0f;
            if (victimVel.magnitude < 1f) return false; // Not moving towards goal

            float dot = Vector3.Dot(victimVel.normalized, toGoal);
            if (dot < 0.5f) return false; // Not moving towards goal

            // Number of defenders between victim and goal
            int defendersBetween = CountDefendersBetween(victim.transform.position, goalPos, GetPlayerTeam(victimId));
            if (defendersBetween > 1) return false; // More than GK + 1

            // Ball control
            if (!victim.HasPossession) return false;

            return true;
        }

        bool CheckSPA(int offenderId, int victimId, Vector3 position, FoulType type)
        {
            if (!_allPlayers.ContainsKey(victimId)) return false;

            var victim = _allPlayers[victimId];
            if (victim == null) return false;

            // Stopping a promising attack - less strict than DOGSO
            Vector3 goalPos = GetOpponentGoal(GetPlayerTeam(victimId));
            float distToGoal = Vector3.Distance(victim.transform.position, goalPos);

            if (distToGoal > 50f) return false;

            int defendersBetween = CountDefendersBetween(victim.transform.position, goalPos, GetPlayerTeam(victimId));
            if (defendersBetween > 2) return false;

            return victim.HasPossession || victim.Velocity.magnitude > 2f;
        }

        bool IsPromisingAttack(int playerId)
        {
            if (!_allPlayers.ContainsKey(playerId)) return false;
            var player = _allPlayers[playerId];
            if (player == null) return false;

            Vector3 goalPos = GetOpponentGoal(GetPlayerTeam(playerId));
            float distToGoal = Vector3.Distance(player.transform.position, goalPos);
            return distToGoal < 40f && player.Velocity.magnitude > 1f;
        }

        int CountDefendersBetween(Vector3 from, Vector3 to, int attackingTeam)
        {
            int defendingTeam = attackingTeam == 0 ? 1 : 0;
            TeamState defenders = defendingTeam == 0 ? HomeTeam : AwayTeam;

            int count = 0;
            Vector3 direction = (to - from).normalized;
            float distance = Vector3.Distance(from, to);

            foreach (int pid in defenders.playersOnField)
            {
                if (!_allPlayers.ContainsKey(pid)) continue;
                var p = _allPlayers[pid];
                if (p == null) continue;

                Vector3 toPlayer = p.transform.position - from;
                float proj = Vector3.Dot(toPlayer, direction);
                float perp = Vector3.Dot(toPlayer, new Vector3(-direction.z, 0f, direction.x));

                if (proj > 0f && proj < distance && Mathf.Abs(perp) < 5f)
                    count++;
            }

            return count;
        }

        Vector3 GetOpponentGoal(int teamId)
        {
            return teamId == 0 ? new Vector3(Arena.HalfLength, 0f, 0f) : new Vector3(-Arena.HalfLength, 0f, 0f);
        }

        // =====================================================================
        // BOLA FORA DE JOGO - Leis 9, 15, 16, 17
        // =====================================================================

        void CheckBallOutOfPlay()
        {
            if (_ball == null || _ball.IsHeld) return;

            Vector3 pos = _ball.transform.position;
            Vector3 vel = _ball.Velocity;

            // Goal line (x = ±HalfLength)
            if (pos.x > Arena.HalfLength)
            {
                // Ball crossed home team's goal line (x positive)
                // Check if goal
                if (pos.z > -Arena.GoalHalfWidth && pos.z < Arena.GoalHalfWidth && pos.y < Arena.GoalHeight)
                {
                    // GOAL for away team
                    AwardGoal(1, -1); // -1 = own goal / no scorer
                }
                else
                {
                    // Corner or goal kick
                    bool lastTouchHome = _lastTouchTeamId == 0;
                    if (lastTouchHome)
                        AwardCorner(1, pos);
                    else
                        AwardGoalKick(0, pos);
                }
            }
            else if (pos.x < -Arena.HalfLength)
            {
                // Ball crossed away team's goal line (x negative)
                if (pos.z > -Arena.GoalHalfWidth && pos.z < Arena.GoalHalfWidth && pos.y < Arena.GoalHeight)
                {
                    // GOAL for home team
                    AwardGoal(0, -1);
                }
                else
                {
                    bool lastTouchHome = _lastTouchTeamId == 0;
                    if (lastTouchHome)
                        AwardGoalKick(1, pos);
                    else
                        AwardCorner(0, pos);
                }
            }

            // Touch line (z = ±FenceZ)
            if (pos.z > Arena.FenceZ)
            {
                bool lastTouchHome = _lastTouchTeamId == 0;
                AwardThrowIn(lastTouchHome ? 1 : 0, pos);
            }
            else if (pos.z < -Arena.FenceZ)
            {
                bool lastTouchHome = _lastTouchTeamId == 0;
                AwardThrowIn(lastTouchHome ? 1 : 0, pos);
            }
        }

        void AwardGoal(int teamId, int scorerId)
        {
            if (teamId == 0) HomeTeam.score++; else AwayTeam.score++;

            OnGoalScored?.Invoke(teamId, scorerId);

            LogEvent(new MatchEvent
            {
                type = MatchEventType.Goal,
                time = CurrentMatchTime,
                teamId = teamId,
                playerId = scorerId,
                position = _ball.transform.position,
                description = $"GOAL! {GetTeamName(teamId)} {HomeTeam.score} x {AwayTeam.score} {GetTeamName(teamId == 0 ? 1 : 0)}"
            });

            if (scorerId != -1 && _playerAttributes.ContainsKey(scorerId))
            {
                _playerAttributes[scorerId].goals++;
            }

            // Restart at center
            _ball.PlaceAt(Vector3.zero);
        }

        void AwardCorner(int teamId, Vector3 position)
        {
            TeamState team = teamId == 0 ? HomeTeam : AwayTeam;
            team.corners++;

            Vector3 cornerPos = teamId == 0 ?
                new Vector3(Arena.HalfLength - 1f, 0f, Mathf.Sign(position.z) * (Arena.FenceZ - 1f)) :
                new Vector3(-Arena.HalfLength + 1f, 0f, Mathf.Sign(position.z) * (Arena.FenceZ - 1f));

            LogEvent(new MatchEvent
            {
                type = MatchEventType.Corner,
                time = CurrentMatchTime,
                teamId = teamId,
                position = cornerPos,
                description = $"Escanteio para {GetTeamName(teamId)}"
            });

            _ball.PlaceAt(cornerPos);
        }

        void AwardGoalKick(int teamId, Vector3 position)
        {
            Vector3 goalKickPos = teamId == 0 ?
                new Vector3(-Arena.HalfLength + 5.5f, 0f, 0f) :
                new Vector3(Arena.HalfLength - 5.5f, 0f, 0f);

            LogEvent(new MatchEvent
            {
                type = MatchEventType.GoalKick,
                time = CurrentMatchTime,
                teamId = teamId,
                position = goalKickPos,
                description = $"Tiro de meta para {GetTeamName(teamId)}"
            });

            _ball.PlaceAt(goalKickPos);
        }

        void AwardThrowIn(int teamId, Vector3 position)
        {
            // Throw in from where it went out
            Vector3 throwPos = new Vector3(
                Mathf.Clamp(position.x, -Arena.HalfLength + 1f, Arena.HalfLength - 1f),
                0f,
                Mathf.Clamp(position.z, -Arena.FenceZ + 1f, Arena.FenceZ - 1f)
            );

            LogEvent(new MatchEvent
            {
                type = MatchEventType.ThrowIn,
                time = CurrentMatchTime,
                teamId = teamId,
                position = throwPos,
                description = $"Lateral para {GetTeamName(teamId)}"
            });

            _ball.PlaceAt(throwPos);
        }

        void AwardFreeKick(int teamId, Vector3 position, FoulType type, bool direct)
        {
            string fkType = direct ? "direto" : "indireto";
            LogEvent(new MatchEvent
            {
                type = MatchEventType.FreeKick,
                time = CurrentMatchTime,
                teamId = teamId,
                position = position,
                description = $"Livre {fkType} para {GetTeamName(teamId)}"
            });

            _ball.PlaceAt(position);
        }

        void AwardPenalty(int teamId, Vector3 position)
        {
            Vector3 penSpot = teamId == 0 ?
                new Vector3(Arena.HalfLength - 11f, 0f, 0f) :
                new Vector3(-Arena.HalfLength + 11f, 0f, 0f);

            LogEvent(new MatchEvent
            {
                type = MatchEventType.Penalty,
                time = CurrentMatchTime,
                teamId = teamId,
                position = penSpot,
                description = $"Pênalti para {GetTeamName(teamId)}"
            });

            _ball.PlaceAt(penSpot);
        }

        // =====================================================================
        // EVENTOS DE BOLA
        // =====================================================================

        void OnBallShot(BallPhysics.ShotInfo info)
        {
            _lastTouchTeamId = GetPlayerTeamByPosition(info.origin);
            _lastTouchPlayerId = FindClosestPlayer(info.origin, _lastTouchTeamId);
            _lastTouchTimes[_lastTouchPlayerId] = CurrentMatchTime;

            // Track for offside
            if (_lastTouchPlayerId != -1)
            {
                _lastAttackerPositions[_lastTouchPlayerId] = info.origin;
            }
        }

        void OnBallImpact(Vector3 point, float speed, Collider collider)
        {
            // Check if player touched the ball
            if (collider != null)
            {
                var pc = collider.GetComponent<PlayerController>();
                var gk = collider.GetComponent<GoalkeeperAI>();

                if (pc != null)
                {
                    _lastTouchTeamId = GetPlayerTeam(pc);
                    _lastTouchPlayerId = FindPlayerId(pc);
                    _lastTouchTimes[_lastTouchPlayerId] = CurrentMatchTime;
                }
                else if (gk != null)
                {
                    _lastTouchTeamId = GetPlayerTeam(gk);
                    _lastTouchPlayerId = FindPlayerId(gk);
                    _lastTouchTimes[_lastTouchPlayerId] = CurrentMatchTime;
                }
            }
        }

        // =====================================================================
        // VANTAGEM (ADVANTAGE)
        // =====================================================================

        void CheckAdvantage()
        {
            if (!_advantagePlaying) return;

            if (CurrentMatchTime - _advantageStartTime > ADVANTAGE_DURATION)
            {
                // Advantage over, call back foul
                _advantagePlaying = false;
                // In real implementation, would bring ball back to foul position
            }
        }

        public void PlayAdvantage(int teamId)
        {
            _advantagePlaying = true;
            _advantageStartTime = CurrentMatchTime;
            _advantageTeamId = teamId;
        }

        // =====================================================================
        // SUBSTITUIÇÕES - Lei 3
        // =====================================================================

        public bool RequestSubstitution(int teamId, int playerOutId, int playerInId)
        {
            TeamState team = teamId == 0 ? HomeTeam : AwayTeam;

            if (team.substitutionsUsed >= maxSubstitutions)
            {
                LogEvent(new MatchEvent
                {
                    type = MatchEventType.Substitution,
                    time = CurrentMatchTime,
                    teamId = teamId,
                    description = $"{GetTeamName(teamId)}: substituições esgotadas"
                });
                return false;
            }

            if (!team.playersOnField.Contains(playerOutId))
            {
                return false; // Player not on field
            }

            if (!team.substitutes.Contains(playerInId))
            {
                return false; // Player not on bench
            }

            // Perform substitution
            team.playersOnField.Remove(playerOutId);
            team.playersOnField.Add(playerInId);
            team.substitutes.Remove(playerInId);
            team.substitutionsUsed++;

            OnSubstitution?.Invoke(teamId, playerOutId, playerInId);

            LogEvent(new MatchEvent
            {
                type = MatchEventType.Substitution,
                time = CurrentMatchTime,
                teamId = teamId,
                playerId = playerOutId,
                playerId2 = playerInId,
                description = $"Substituição: {GetPlayerName(playerOutId)} sai, {GetPlayerName(playerInId)} entra"
            });

            return true;
        }

        // =====================================================================
        // UTILITÁRIOS
        // =====================================================================

        int GetPlayerTeam(int playerId)
        {
            if (HomeTeam.playersOnField.Contains(playerId)) return 0;
            if (AwayTeam.playersOnField.Contains(playerId)) return 1;
            if (HomeTeam.substitutes.Contains(playerId)) return 0;
            if (AwayTeam.substitutes.Contains(playerId)) return 1;
            return -1;
        }

        int GetPlayerTeam(PlayerController pc)
        {
            foreach (var kvp in _allPlayers)
                if (kvp.Value == pc) return GetPlayerTeam(kvp.Key);
            return -1;
        }

        int GetPlayerTeam(GoalkeeperAI gk)
        {
            foreach (var kvp in _allPlayers)
                if (kvp.Value == gk) return GetPlayerTeam(kvp.Key);
            return -1;
        }

        int FindPlayerId(PlayerController pc)
        {
            foreach (var kvp in _allPlayers)
                if (kvp.Value == pc) return kvp.Key;
            return -1;
        }

        int FindPlayerId(GoalkeeperAI gk)
        {
            foreach (var kvp in _allPlayers)
                if (kvp.Value == gk) return kvp.Key;
            return -1;
        }

        int GetPlayerTeamByPosition(Vector3 position)
        {
            // Determine team by which half the position is in
            return position.x > 0f ? 0 : 1;
        }

        int FindClosestPlayer(Vector3 position, int teamId)
        {
            TeamState team = teamId == 0 ? HomeTeam : AwayTeam;
            float bestDist = float.MaxValue;
            int bestId = -1;

            foreach (int pid in team.playersOnField)
            {
                if (!_allPlayers.ContainsKey(pid)) continue;
                var p = _allPlayers[pid];
                if (p == null) continue;

                float dist = Vector3.Distance(p.transform.position, position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestId = pid;
                }
            }

            return bestId;
        }

        bool IsInPenaltyArea(Vector3 position, int offendingTeam)
        {
            // Penalty area: 16.5m from goal line, 40.32m wide
            float goalX = offendingTeam == 0 ? Arena.HalfLength : -Arena.HalfLength;
            float distFromGoalLine = offendingTeam == 0 ? goalX - position.x : position.x - goalX;
            float widthFromCenter = Mathf.Abs(position.z);

            return distFromGoalLine <= 16.5f && widthFromCenter <= 20.16f;
        }

        string GetPlayerName(int playerId)
        {
            if (_playerAttributes.ContainsKey(playerId))
                return _playerAttributes[playerId].playerName;
            return $"Jogador #{playerId}";
        }

        string GetTeamName(int teamId)
        {
            return teamId == 0 ? HomeTeam.teamName : AwayTeam.teamName;
        }

        void LogEvent(MatchEvent evt)
        {
            _matchEvents.Add(evt);
            OnMatchEvent?.Invoke(evt);
        }

        // =====================================================================
        // VAR SYSTEM
        // =====================================================================

        public void RequestVARReview(string description, MatchEventType reviewType, Vector3 position)
        {
            if (!useVAR) return;

            OnVARReviewStarted?.Invoke($"VAR revisando: {description}");

            // Simulate VAR decision (in real implementation, would be more complex)
            bool overturned = UnityEngine.Random.value < 0.3f; // 30% chance of overturn

            OnVARDecision?.Invoke(description, overturned);

            LogEvent(new MatchEvent
            {
                type = MatchEventType.VARReview,
                time = CurrentMatchTime,
                teamId = -1,
                position = position,
                description = $"VAR: {description} - {(overturned ? "DECISÃO REVERTIDA" : "DECISÃO MANTIDA")}"
            });
        }

        // =====================================================================
        // ESTATÍSTICAS E RELATÓRIOS
        // =====================================================================

        public string GetMatchReport()
        {
            return $@"
=== RELATÓRIO DA PARTIDA ===
{HomeTeam.teamName} {HomeTeam.score} x {AwayTeam.score} {AwayTeam.teamName}

ESTATÍSTICAS:
Chutes: {HomeTeam.shots} x {AwayTeam.shots}
Chutes no gol: {HomeTeam.shotsOnTarget} x {AwayTeam.shotsOnTarget}
Escanteios: {HomeTeam.corners} x {AwayTeam.corners}
Faltas: {HomeTeam.fouls} x {AwayTeam.fouls}
Impedimentos: {HomeTeam.offsides} x {AwayTeam.offsides}
Cartões amarelos: {HomeTeam.yellowCards} x {AwayTeam.yellowCards}
Cartões vermelhos: {HomeTeam.redCards} x {AwayTeam.redCards}
Substituições: {HomeTeam.substitutionsUsed} x {AwayTeam.substitutionsUsed}

EVENTOS:
{string.Join("\n", _matchEvents.ConvertAll(e => $"[{e.time:F0}s] {e.description}"))}
";
        }

        public List<MatchEvent> GetMatchEvents() => new List<MatchEvent>(_matchEvents);
        public List<OffsideCheck> GetOffsideChecks() => new List<OffsideCheck>(_offsideChecks);
        public List<FoulInfo> GetFoulHistory() => new List<FoulInfo>(_foulHistory);
    }
}