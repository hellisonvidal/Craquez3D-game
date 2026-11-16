using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Craquez
{
    /// <summary>
    /// Sistema de arbitragem com árbitro principal e dois assistentes (bandeirinhas).
    /// Baseado nas regras oficiais da IFAB - Lei 5 (Árbitro) e Lei 6 (Assistentes).
    /// </summary>
    public class RefereeSystem : MonoBehaviour
    {
        public static RefereeSystem Instance { get; private set; }

        [Header("Configurações do Árbitro Principal")]
        public float mainRefereeSpeed = 6f;
        public float mainRefereeSprintSpeed = 9f;
        public float mainRefereeAcceleration = 12f;
        public float mainRefereeDeceleration = 20f;
        public float optimalDistanceToBall = 15f; // Distância ideal da bola
        public float maxDistanceFromBall = 25f;
        public float diagonalSystemAngle = 45f; // Sistema diagonal

        [Header("Configurações dos Assistentes")]
        public float assistantSpeed = 5f;
        public float assistantSprintSpeed = 8f;
        public float offsideLineSmoothing = 0.1f;

        [Header("Comportamento")]
        public float decisionTime = 0.5f; // Tempo para decidir falta/cartão
        public float whistleBlowCooldown = 1f;
        public bool useAdvantage = true;

        [Header("Visual")]
        public GameObject mainRefereePrefab;
        public GameObject assistantRefereePrefab;
        public Material mainRefereeMaterial;
        public Material assistantRefereeMaterial;
        public Color homeTeamColor = Color.blue;
        public Color awayTeamColor = Color.red;

        // Estado
        public enum RefereeState
        {
            Idle,
            FollowingPlay,
            SprintingToPosition,
            WhistleBlowing,
            ShowingCard,
            SignalingAdvantage,
            SignalingOffside,
            SignalingCorner,
            SignalingGoalKick,
            SignalingThrowIn,
            SignalingPenalty,
            SignalingGoal,
            VARReview,
            InjuredPlayer
        }

        public struct RefereeDecision
        {
            public int playerId;
            public FootballRulesEngine.FoulType foulType;
            public FootballRulesEngine.CardType cardType;
            public bool isPenalty;
            public Vector3 position;
            public float time;
            public string reasoning;
        }

        // Componentes
        private NavMeshAgent _mainRefereeAgent;
        private NavMeshAgent _assistant1Agent;
        private NavMeshAgent _assistant2Agent;
        private Animator _mainRefereeAnim;
        private Animator _assistant1Anim;
        private Animator _assistant2Anim;
        private FootballRulesEngine _rulesEngine;

        // Estado atual
        private RefereeState _mainState = RefereeState.Idle;
        private RefereeState _assistant1State = RefereeState.Idle;
        private RefereeState _assistant2State = RefereeState.Idle;

        private Vector3 _mainTargetPosition;
        private Vector3 _assistant1TargetPosition;
        private Vector3 _assistant2TargetPosition;

        private float _lastWhistleTime = -10f;
        private List<RefereeDecision> _decisionHistory = new List<RefereeDecision>();

        // Assistentes posicionados nas laterais opostas
        // Assistant 1: Lado do banco de reservas (geralmente lado esquerdo olhando para gol do time visitante)
        // Assistant 2: Lado oposto
        private int _assistant1Side = -1; // -1 = esquerda, 1 = direita (relativo ao gol do time mandante)
        private int _assistant2Side = 1;

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
            _rulesEngine = FootballRulesEngine.Instance;
            CreateRefereeObjects();
            SubscribeToEvents();
        }

        void CreateRefereeObjects()
        {
            // Create main referee
            GameObject mainRefObj;
            if (mainRefereePrefab != null)
            {
                mainRefObj = Instantiate(mainRefereePrefab);
            }
            else
            {
                mainRefObj = CreateDefaultReferee("MainReferee", mainRefereeMaterial ?? CreateDefaultMaterial(Color.black));
            }
            mainRefObj.transform.SetParent(transform);
            _mainRefereeAgent = mainRefObj.GetComponent<NavMeshAgent>();
            if (_mainRefereeAgent == null) _mainRefereeAgent = mainRefObj.AddComponent<NavMeshAgent>();
            ConfigureAgent(_mainRefereeAgent, mainRefereeSpeed, mainRefereeAcceleration);
            _mainRefereeAnim = mainRefObj.GetComponentInChildren<Animator>();

            // Create assistant 1 (left side)
            GameObject asst1Obj;
            if (assistantRefereePrefab != null)
            {
                asst1Obj = Instantiate(assistantRefereePrefab);
            }
            else
            {
                asst1Obj = CreateDefaultReferee("AssistantReferee1", assistantRefereeMaterial ?? CreateDefaultMaterial(Color.yellow));
            }
            asst1Obj.transform.SetParent(transform);
            _assistant1Agent = asst1Obj.GetComponent<NavMeshAgent>();
            if (_assistant1Agent == null) _assistant1Agent = asst1Obj.AddComponent<NavMeshAgent>();
            ConfigureAgent(_assistant1Agent, assistantSpeed, mainRefereeAcceleration);
            _assistant1Anim = asst1Obj.GetComponentInChildren<Animator>();

            // Create assistant 2 (right side)
            GameObject asst2Obj;
            if (assistantRefereePrefab != null)
            {
                asst2Obj = Instantiate(assistantRefereePrefab);
            }
            else
            {
                asst2Obj = CreateDefaultReferee("AssistantReferee2", assistantRefereeMaterial ?? CreateDefaultMaterial(Color.yellow));
            }
            asst2Obj.transform.SetParent(transform);
            _assistant2Agent = asst2Obj.GetComponent<NavMeshAgent>();
            if (_assistant2Agent == null) _assistant2Agent = asst2Obj.AddComponent<NavMeshAgent>();
            ConfigureAgent(_assistant2Agent, assistantSpeed, mainRefereeAcceleration);
            _assistant2Anim = asst2Obj.GetComponentInChildren<Animator>();

            // Posicionamento inicial
            PositionRefereesForKickoff();
        }

        GameObject CreateDefaultReferee(string name, Material material)
        {
            var obj = new GameObject(name);
            // Add a simple capsule representation
            var capsule = obj.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            var renderer = obj.AddComponent<MeshRenderer>();
            var filter = obj.AddComponent<MeshFilter>();
            filter.mesh = CreateCapsuleMesh();
            renderer.material = material;

            var rb = obj.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            return obj;
        }

        Mesh CreateCapsuleMesh()
        {
            // Simple capsule mesh for visualization
            var mesh = new Mesh();
            int rings = 8;
            int segments = 12;
            float height = 1.8f;
            float radius = 0.3f;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();

            // Generate capsule geometry
            for (int i = 0; i <= rings; i++)
            {
                float v = (float)i / rings;
                float y = Mathf.Lerp(-height / 2f, height / 2f, v);
                float r = radius;

                // Adjust radius for hemispherical caps
                if (v < 0.5f)
                {
                    float capV = v * 2f;
                    r = radius * Mathf.Sin(capV * Mathf.PI * 0.5f);
                    y = -height / 2f + radius * (1f - Mathf.Cos(capV * Mathf.PI * 0.5f));
                }
                else
                {
                    float capV = (v - 0.5f) * 2f;
                    r = radius * Mathf.Sin((1f - capV) * Mathf.PI * 0.5f);
                    y = height / 2f - radius * (1f - Mathf.Cos((1f - capV) * Mathf.PI * 0.5f));
                }

                for (int j = 0; j <= segments; j++)
                {
                    float u = (float)j / segments;
                    float angle = u * Mathf.PI * 2f;
                    float x = r * Mathf.Cos(angle);
                    float z = r * Mathf.Sin(angle);
                    vertices.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(u, v));
                }
            }

            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int a = i * (segments + 1) + j;
                    int b = i * (segments + 1) + j + 1;
                    int c = (i + 1) * (segments + 1) + j;
                    int d = (i + 1) * (segments + 1) + j + 1;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        Material CreateDefaultMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            return mat;
        }

        void ConfigureAgent(NavMeshAgent agent, float speed, float accel)
        {
            agent.speed = speed;
            agent.acceleration = accel;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.5f;
            agent.autoBraking = true;
            agent.baseOffset = 0f;
            agent.height = 1.8f;
            agent.radius = 0.3f;
        }

        void SubscribeToEvents()
        {
            if (_rulesEngine != null)
            {
                _rulesEngine.OnFoulDetected += OnFoulDetected;
                _rulesEngine.OnOffsideDetected += OnOffsideDetected;
                _rulesEngine.OnGoalScored += OnGoalScored;
                _rulesEngine.OnCardShown += OnCardShown;
                _rulesEngine.OnSubstitution += OnSubstitution;
                _rulesEngine.OnMatchEvent += OnMatchEvent;
                _rulesEngine.OnPhaseChanged += OnPhaseChanged;
                _rulesEngine.OnVARReviewStarted += OnVARReviewStarted;
                _rulesEngine.OnVARDecision += OnVARDecision;
            }
        }

        void PositionRefereesForKickoff()
        {
            // Main referee at center circle
            _mainTargetPosition = Vector3.zero;
            _mainRefereeAgent.Warp(_mainTargetPosition);

            // Assistant 1 at halfway line on left side (touchline)
            _assistant1TargetPosition = new Vector3(0f, 0f, -Arena.FenceZ + 1f);
            _assistant1Agent.Warp(_assistant1TargetPosition);

            // Assistant 2 at halfway line on right side (touchline)
            _assistant2TargetPosition = new Vector3(0f, 0f, Arena.FenceZ - 1f);
            _assistant2Agent.Warp(_assistant2TargetPosition);
        }

        void Update()
        {
            if (_rulesEngine == null || _rulesEngine.CurrentPhase == FootballRulesEngine.MatchPhase.PreMatch ||
                _rulesEngine.CurrentPhase == FootballRulesEngine.MatchPhase.PostMatch)
                return;

            UpdateMainReferee();
            UpdateAssistant1();
            UpdateAssistant2();
            UpdateAnimations();
        }

        // =====================================================================
        // ÁRBITRO PRINCIPAL - Sistema Diagonal
        // =====================================================================

        void UpdateMainReferee()
        {
            if (_rulesEngine.Ball == null) return;

            Vector3 ballPos = _rulesEngine.Ball.transform.position;
            Vector3 ballVel = _rulesEngine.Ball.Velocity;
            Vector3 ballVelFlat = new Vector3(ballVel.x, 0f, ballVel.z);

            // Calculate ideal position using diagonal system
            // Referee stays on diagonal from corner to corner, on opposite side of ball from play
            _mainTargetPosition = CalculateDiagonalPosition(ballPos, ballVelFlat);

            // Adjust speed based on ball speed
            float ballSpeed = ballVelFlat.magnitude;
            float targetSpeed = ballSpeed > 10f ? mainRefereeSprintSpeed : mainRefereeSpeed;
            _mainRefereeAgent.speed = Mathf.Lerp(_mainRefereeAgent.speed, targetSpeed, Time.deltaTime * 2f);

            // Move to target
            if (Vector3.Distance(_mainRefereeAgent.transform.position, _mainTargetPosition) > 1f)
            {
                _mainRefereeAgent.SetDestination(_mainTargetPosition);
                _mainState = RefereeState.FollowingPlay;
            }
            else
            {
                _mainState = RefereeState.Idle;
            }

            // Face the ball
            Vector3 lookDir = ballPos - _mainRefereeAgent.transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                _mainRefereeAgent.transform.rotation = Quaternion.RotateTowards(
                    _mainRefereeAgent.transform.rotation, targetRot, 360f * Time.deltaTime);
            }
        }

        Vector3 CalculateDiagonalPosition(Vector3 ballPos, Vector3 ballVel)
        {
            // Diagonal system: referee runs from corner flag to corner flag
            // Staying on the diagonal that keeps play between referee and assistant

            // Determine which diagonal we're on based on ball position
            // Diagonal 1: Bottom-left to Top-right (assistant on left touchline)
            // Diagonal 2: Bottom-right to Top-left (assistant on right touchline)

            // For simplicity, use diagonal 1 (most common)
            // Referee position is offset from ball perpendicular to touchline

            float fieldWidth = Arena.FenceZ * 2f;
            float fieldLength = Arena.HalfLength * 2f;

            // Calculate diagonal line: from (-HalfLength, -FenceZ) to (HalfLength, FenceZ)
            // Equation: z = (FenceZ/HalfLength) * x + offset

            // Stay on diagonal but offset towards play
            float diagonalZ = (Arena.FenceZ / Arena.HalfLength) * ballPos.x;

            // Offset: stay on the side of the ball opposite to the direction of attack
            // If ball moving towards positive x (home attacking), referee on negative z side
            // If ball moving towards negative x (away attacking), referee on positive z side
            float offset = ballVel.x > 0 ? -8f : 8f;

            float targetZ = Mathf.Clamp(diagonalZ + offset, -Arena.FenceZ + 2f, Arena.FenceZ - 2f);
            float targetX = Mathf.Clamp(ballPos.x, -Arena.HalfLength + 5f, Arena.HalfLength - 5f);

            return new Vector3(targetX, 0f, targetZ);
        }

        // =====================================================================
        // ASSISTENTE 1 - Lado esquerdo (olhando do gol do time mandante)
        // =====================================================================

        void UpdateAssistant1()
        {
            if (_rulesEngine.Ball == null) return;

            Vector3 ballPos = _rulesEngine.Ball.transform.position;

            // Assistant 1 stays on left touchline (negative z), level with second-last defender
            // or ball, whichever is closer to goal line they're watching

            int defendingTeam = _rulesEngine.HomeTeam.score > _rulesEngine.AwayTeam.score ? 0 : 1; // Simplified
            // Actually, assistant watches the offside line for the team attacking their side

            // Assistant 1 watches left side (negative z touchline)
            // Offside line is determined by second-last defender of team defending that goal
            // Goal at negative x is defended by Home team (team 0)
            // So assistant 1 watches offside for Away team (team 1) attacking Home goal

            Vector3 offsideLinePos = CalculateOffsideLine(1); // Away team attacking
            float targetZ = -Arena.FenceZ + 1f; // Left touchline

            // Assistant stays level with offside line or ball, whichever is closer to goal line
            float goalLineX = -Arena.HalfLength;
            float offsideX = offsideLinePos.x;

            // If ball is between offside line and goal, stay with ball
            // Otherwise stay with offside line
            float targetX;
            if ((ballPos.x < offsideX && ballPos.x > goalLineX) ||
                (ballPos.x > offsideX && ballPos.x < goalLineX))
            {
                targetX = Mathf.Clamp(ballPos.x, goalLineX + 1f, -goalLineX - 1f);
            }
            else
            {
                targetX = Mathf.Clamp(offsideX, goalLineX + 1f, -goalLineX - 1f);
            }

            _assistant1TargetPosition = new Vector3(targetX, 0f, targetZ);
            _assistant1Agent.SetDestination(_assistant1TargetPosition);

            // Face the field (positive z direction)
            _assistant1Agent.transform.rotation = Quaternion.RotateTowards(
                _assistant1Agent.transform.rotation,
                Quaternion.LookRotation(Vector3.forward),
                360f * Time.deltaTime);

            // Check for offside signal
            CheckAssistantOffsideSignal(1);
        }

        // =====================================================================
        // ASSISTENTE 2 - Lado direito
        // =====================================================================

        void UpdateAssistant2()
        {
            if (_rulesEngine.Ball == null) return;

            Vector3 ballPos = _rulesEngine.Ball.transform.position;

            // Assistant 2 stays on right touchline (positive z)
            // Watches offside for Home team (team 0) attacking Away goal
            // Goal at positive x is defended by Away team

            Vector3 offsideLinePos = CalculateOffsideLine(0); // Home team attacking
            float targetZ = Arena.FenceZ - 1f; // Right touchline

            float goalLineX = Arena.HalfLength;
            float offsideX = offsideLinePos.x;

            float targetX;
            if ((ballPos.x > offsideX && ballPos.x < goalLineX) ||
                (ballPos.x < offsideX && ballPos.x > goalLineX))
            {
                targetX = Mathf.Clamp(ballPos.x, -goalLineX + 1f, goalLineX - 1f);
            }
            else
            {
                targetX = Mathf.Clamp(offsideX, -goalLineX + 1f, goalLineX - 1f);
            }

            _assistant2TargetPosition = new Vector3(targetX, 0f, targetZ);
            _assistant2Agent.SetDestination(_assistant2TargetPosition);

            // Face the field (negative z direction)
            _assistant2Agent.transform.rotation = Quaternion.RotateTowards(
                _assistant2Agent.transform.rotation,
                Quaternion.LookRotation(Vector3.back),
                360f * Time.deltaTime);

            CheckAssistantOffsideSignal(2);
        }

        Vector3 CalculateOffsideLine(int attackingTeam)
        {
            // Get second-last defender of defending team
            int defendingTeam = attackingTeam == 0 ? 1 : 0;
            FootballRulesEngine.TeamState defenders = defendingTeam == 0 ? _rulesEngine.HomeTeam : _rulesEngine.AwayTeam;

            float minX = float.MaxValue;
            float maxX = float.MinValue;

            foreach (int pid in defenders.playersOnField)
            {
                if (!_rulesEngine.AllPlayers.ContainsKey(pid)) continue;
                var p = _rulesEngine.AllPlayers[pid];
                if (p == null) continue;

                float x = p.transform.position.x;
                if (attackingTeam == 0) // Home attacking, offside line is most negative x (closest to away goal)
                {
                    if (x > maxX) maxX = x;
                }
                else // Away attacking, offside line is most positive x (closest to home goal)
                {
                    if (x < minX) minX = x;
                }
            }

            float lineX = attackingTeam == 0 ? maxX : minX;
            return new Vector3(lineX, 0f, 0f);
        }

        void CheckAssistantOffsideSignal(int assistantNum)
        {
            // Check if offside was recently detected by rules engine
            if (_rulesEngine.OffsideChecks.Count > 0)
            {
                var lastOffside = _rulesEngine.OffsideChecks[_rulesEngine.OffsideChecks.Count - 1];
                if (_rulesEngine.CurrentMatchTime - lastOffside.time < 2f)
                {
                    if (assistantNum == 1)
                        _assistant1State = RefereeState.SignalingOffside;
                    else
                        _assistant2State = RefereeState.SignalingOffside;
                }
            }
        }

        // =====================================================================
        // ANIMAÇÕES
        // =====================================================================

        void UpdateAnimations()
        {
            if (_mainRefereeAnim != null)
            {
                float speed = _mainRefereeAgent.velocity.magnitude;
                _mainRefereeAnim.SetFloat("Speed", speed / mainRefereeSprintSpeed);
                _mainRefereeAnim.SetBool("IsWhistling", _mainState == RefereeState.WhistleBlowing);
                _mainRefereeAnim.SetInteger("Signal", GetSignalAnimation(_mainState));
            }

            if (_assistant1Anim != null)
            {
                float speed = _assistant1Agent.velocity.magnitude;
                _assistant1Anim.SetFloat("Speed", speed / assistantSprintSpeed);
                _assistant1Anim.SetBool("FlagUp", _assistant1State == RefereeState.SignalingOffside ||
                    _assistant1State == RefereeState.SignalingCorner ||
                    _assistant1State == RefereeState.SignalingGoalKick ||
                    _assistant1State == RefereeState.SignalingThrowIn);
            }

            if (_assistant2Anim != null)
            {
                float speed = _assistant2Agent.velocity.magnitude;
                _assistant2Anim.SetFloat("Speed", speed / assistantSprintSpeed);
                _assistant2Anim.SetBool("FlagUp", _assistant2State == RefereeState.SignalingOffside ||
                    _assistant2State == RefereeState.SignalingCorner ||
                    _assistant2State == RefereeState.SignalingGoalKick ||
                    _assistant2State == RefereeState.SignalingThrowIn);
            }
        }

        int GetSignalAnimation(RefereeState state)
        {
            switch (state)
            {
                case RefereeState.WhistleBlowing: return 1;
                case RefereeState.ShowingCard: return 2;
                case RefereeState.SignalingAdvantage: return 3;
                case RefereeState.SignalingOffside: return 4;
                case RefereeState.SignalingCorner: return 5;
                case RefereeState.SignalingGoalKick: return 6;
                case RefereeState.SignalingThrowIn: return 7;
                case RefereeState.SignalingPenalty: return 8;
                case RefereeState.SignalingGoal: return 9;
                default: return 0;
            }
        }

        // =====================================================================
        // EVENT HANDLERS
        // =====================================================================

        void OnFoulDetected(FootballRulesEngine.FoulInfo foul)
        {
            _mainState = RefereeState.WhistleBlowing;
            _lastWhistleTime = Time.time;

            // Move to foul position
            _mainTargetPosition = foul.position;
            _mainRefereeAgent.SetDestination(foul.position);

            // Record decision
            var decision = new RefereeDecision
            {
                playerId = foul.offenderId,
                foulType = foul.type,
                cardType = FootballRulesEngine.CardType.None, // Will be updated
                isPenalty = foul.isPenalty,
                position = foul.position,
                time = _rulesEngine.CurrentMatchTime,
                reasoning = $"Falta: {foul.type} (severidade: {foul.severity:F2})"
            };

            _decisionHistory.Add(decision);

            // Signal foul
            if (foul.isPenalty)
                _mainState = RefereeState.SignalingPenalty;
            else if (foul.type != FootballRulesEngine.FoulType.None)
                _mainState = RefereeState.WhistleBlowing;
        }

        void OnOffsideDetected(FootballRulesEngine.OffsideCheck offside)
        {
            // Assistants will signal via their update loop
            _mainState = RefereeState.WhistleBlowing;
            _lastWhistleTime = Time.time;
        }

        void OnGoalScored(int teamId, int playerId)
        {
            _mainState = RefereeState.SignalingGoal;
            _lastWhistleTime = Time.time;

            // Run to center circle
            _mainTargetPosition = Vector3.zero;
            _mainRefereeAgent.SetDestination(Vector3.zero);
        }

        void OnCardShown(int teamId, int playerId, FootballRulesEngine.CardType card)
        {
            _mainState = RefereeState.ShowingCard;
            // Animation will play card showing
        }

        void OnSubstitution(int teamId, int playerOut, int playerIn)
        {
            // Signal substitution
            _mainState = RefereeState.WhistleBlowing;
        }

        void OnMatchEvent(FootballRulesEngine.MatchEvent evt)
        {
            switch (evt.type)
            {
                case FootballRulesEngine.MatchEventType.Corner:
                    DetermineCornerSide(evt.position);
                    break;
                case FootballRulesEngine.MatchEventType.GoalKick:
                    DetermineGoalKickSide(evt.position);
                    break;
                case FootballRulesEngine.MatchEventType.ThrowIn:
                    DetermineThrowInSide(evt.position);
                    break;
            }
        }

        void DetermineCornerSide(Vector3 position)
        {
            // Which assistant signals corner?
            bool leftSide = position.z < 0;
            if (leftSide)
                _assistant1State = RefereeState.SignalingCorner;
            else
                _assistant2State = RefereeState.SignalingCorner;
        }

        void DetermineGoalKickSide(Vector3 position)
        {
            bool leftSide = position.z < 0;
            if (leftSide)
                _assistant1State = RefereeState.SignalingGoalKick;
            else
                _assistant2State = RefereeState.SignalingGoalKick;
        }

        void DetermineThrowInSide(Vector3 position)
        {
            bool leftSide = position.z < 0;
            if (leftSide)
                _assistant1State = RefereeState.SignalingThrowIn;
            else
                _assistant2State = RefereeState.SignalingThrowIn;
        }

        void OnPhaseChanged(FootballRulesEngine.MatchPhase phase)
        {
            if (phase == FootballRulesEngine.MatchPhase.FirstHalf ||
                phase == FootballRulesEngine.MatchPhase.SecondHalf)
            {
                PositionRefereesForKickoff();
            }
        }

        void OnVARReviewStarted(string description)
        {
            _mainState = RefereeState.VARReview;
        }

        void OnVARDecision(string description, bool overturned)
        {
            _mainState = overturned ? RefereeState.WhistleBlowing : RefereeState.Idle;
            if (overturned)
            {
                // Reverse decision
                _lastWhistleTime = Time.time;
            }
        }

        // =====================================================================
        // PUBLIC API
        // =====================================================================

        public Vector3 GetMainRefereePosition() => _mainRefereeAgent.transform.position;
        public Vector3 GetAssistant1Position() => _assistant1Agent.transform.position;
        public Vector3 GetAssistant2Position() => _assistant2Agent.transform.position;

        public RefereeState GetMainRefereeState() => _mainState;
        public RefereeState GetAssistant1State() => _assistant1State;
        public RefereeState GetAssistant2State() => _assistant2State;

        public List<RefereeDecision> GetDecisionHistory() => new List<RefereeDecision>(_decisionHistory);

        public void RequestAdvantage(int teamId)
        {
            if (useAdvantage)
            {
                _mainState = RefereeState.SignalingAdvantage;
                _rulesEngine.PlayAdvantage(teamId);
            }
        }

        public void SetAssistantSides(int assistant1Side, int assistant2Side)
        {
            _assistant1Side = Mathf.Clamp(assistant1Side, -1, 1);
            _assistant2Side = Mathf.Clamp(assistant2Side, -1, 1);
        }
    }
}