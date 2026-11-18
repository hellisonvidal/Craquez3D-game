using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Craquez
{
    /// <summary>
    /// Partida 11 x 11: monta os dois times (22 jogadores alternando os dois rostos, uniformes por time),
    /// os goleiros e a equipe de arbitragem; arbitra a posse de bola; aplica as regras (saída, lateral,
    /// escanteio, tiro de meta, falta, pênalti, impedimento, cartões, vantagem, contusão); controla o tempo,
    /// o intervalo (troca de lado) e o fim de jogo; e liga o jogador humano ao atleta da vez.
    /// Táticas em MatchSession.Tactics.cs e HUD em MatchSession.Hud.cs.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public partial class MatchSession : MonoBehaviour
    {
        // ------------------------------------------------------------ referências (bootstrap)
        public BallPhysics ball;
        public CameraRig cameraRig;
        public WeatherSystem weather;
        public TouchControls touch;
        public Camera mainCamera;
        public GameObject playerPrefab, playerPrefabB, keeperPrefab, keeperPrefabA, refereePrefab, refereePrefabB;
        public Texture2D kitA_Casa, kitB_Casa, kitB_GoleiroCasa, kitA_Visitante, kitB_Visitante, kitA_GoleiroVisitante, kitA_Arbitro, kitB_Arbitro;
        public Material propMaterial;
        /// <summary>Objetos do treino que somem durante a partida.</summary>
        public List<GameObject> trainingObjects = new List<GameObject>();

        public event Action OnPauseRequested;
        public event Action OnExitRequested;
        public event Action OnRestartRequested;

        public enum Fase { Inativo, BolaParada, Jogo, Comemoracao, Parado, Intervalo, Fim }
        public enum TipoReinicio { Saida, Lateral, TiroDeMeta, Escanteio, Falta, Penalti, BolaAoChao }

        public class Reinicio
        {
            public TipoReinicio tipo;
            public TimeInfo time;
            public Vector3 spot;
            public Vector3 aim;
            public MatchPlayer taker;
            public float start;
            public bool executing;
            public float executeAt;
            public bool waitingHuman;
            public bool indirect;
        }

        public bool Active { get; private set; }
        public Fase Phase { get; private set; }
        public Reinicio Restart { get; private set; }
        public TimeInfo[] Times { get; private set; }
        public TimeInfo UserTeam { get { return Times != null ? Times[userTeam] : null; } }
        public MatchPlayer Human { get; private set; }
        public MatchPlayer Owner { get; private set; }
        public MatchPlayer KeeperHolding { get; private set; }
        public TimeInfo LastPossession { get; private set; }
        public MatchPlayer LastToucher { get; private set; }
        public MatchPlayer Scorer { get; private set; }
        public Vector3 CelebrationSpot { get; private set; }
        public Vector3 BallPos { get { return ball != null ? ball.transform.position : Vector3.zero; } }
        public Vector3 BallVel { get { return ball != null ? ball.Velocity : Vector3.zero; } }
        public int Half { get; private set; }
        public float HalfSeconds { get; private set; }
        public float Clock { get; private set; }
        public int MatchMinute { get { return Mathf.Min(45, Mathf.FloorToInt(Clock / Mathf.Max(1f, HalfSeconds) * 45f)) + (Half == 2 ? 45 : 0); } }

        readonly List<MatchPlayer> all = new List<MatchPlayer>();
        readonly List<GameObject> spawned = new List<GameObject>();
        readonly List<RefereeAgent> referees = new List<RefereeAgent>();
        readonly Dictionary<PlayerController, MatchPlayer> byPc = new Dictionary<PlayerController, MatchPlayer>();
        public IList<MatchPlayer> All { get { return all; } }
        public RefereeAgent MainReferee { get; private set; }
        public IList<RefereeAgent> Referees { get { return referees; } }

        int userTeam;
        Transform savedRigPlayer, savedRigKeeper;
        int kickoffTeamFirstHalf;
        float phaseTime;
        float seenKick = -10f;
        bool keeperWasHolding;
        MatchPlayer keeperReleaser;
        float lastSwitch;
        bool prevLow, prevChip, prevShoot, prevKnock;
        // comandos apertados um pouco antes de a bola chegar (passe de primeira) continuam valendo
        float bufLow = -9f, bufChip = -9f;
        const float InputBuffer = 0.3f;
        float resumeAt = -1f;
        Action afterPause;
        readonly HashSet<MatchPlayer> offsideCandidates = new HashSet<MatchPlayer>();
        TimeInfo offsideTeam;
        float offsideStamp;
        bool restartKickExempt;
        readonly Dictionary<MatchPlayer, float> controlCooldown = new Dictionary<MatchPlayer, float>();
        readonly Dictionary<MatchPlayer, Vector3> prevVel = new Dictionary<MatchPlayer, Vector3>();
        float headerCooldown;
        Ui ui;

        // ============================================================ montagem

        void Awake() { ui = new Ui(); }

        /// <summary>Começa uma partida. userTeamIndex: 0 = Craquez FC, 1 = Atlético Celeste.</summary>
        public void Begin(Clima clima, int userTeamIndex, int halfMinutes, int dificuldade)
        {
            if (Active) Teardown();
            savedRigPlayer = cameraRig.player;
            savedRigKeeper = cameraRig.keeper;
            Active = true;
            userTeam = userTeamIndex;
            seenKick = ball.LastKickTime;
            kickoffTeleport = true;
            keeperWasHolding = false;
            HalfSeconds = halfMinutes * 60f;
            Half = 1;
            Clock = 0f;
            weather.Apply(clima);
            foreach (var go in trainingObjects) if (go != null) go.SetActive(false);

            float aiSkill = dificuldade == 0 ? 1.35f : (dificuldade == 1 ? 1f : 0.8f);
            var rng = new System.Random(Random.Range(0, int.MaxValue));
            string[] forms = Formacoes.Nomes;
            Times = new[]
            {
                new TimeInfo
                {
                    nome = "Craquez FC", sigla = "CRQ", cor = new Color(0.12f, 0.31f, 0.82f), corSecundaria = new Color(0.95f, 0.95f, 0.95f),
                    indice = 0, attackDir = 1f, formacao = forms[rng.Next(forms.Length)],
                    pressao = 0.6f, linhaDefensiva = 0.55f, largura = 0.85f, verticalidade = 0.55f,
                    kitA = kitA_Casa, kitB = kitB_Casa, kitGoleiro = kitB_GoleiroCasa, goleiroModeloA = false
                },
                new TimeInfo
                {
                    nome = "Atlético Celeste", sigla = "ATC", cor = new Color(0.36f, 0.72f, 0.91f), corSecundaria = new Color(0.95f, 0.95f, 0.95f),
                    indice = 1, attackDir = -1f, formacao = forms[rng.Next(forms.Length)],
                    pressao = 0.45f, linhaDefensiva = 0.45f, largura = 0.9f, verticalidade = 0.45f,
                    kitA = kitA_Visitante, kitB = kitB_Visitante, kitGoleiro = kitA_GoleiroVisitante, goleiroModeloA = false
                }
            };
            for (int i = 0; i < 2; i++)
            {
                Times[i].humano = i == userTeam;
                Times[i].habilidade = i == userTeam ? 1f : aiSkill;
                SpawnTeam(Times[i], rng, i == userTeam ? 0.68f : Mathf.Lerp(0.6f, 0.76f, (2f - aiSkill) / 1.2f));
            }
            SpawnReferees();

            cameraRig.matchMode = true;
            cameraRig.goalkeeperRole = false;
            cameraRig.mode = CameraRig.ModosPartida[Mathf.Clamp(GameSettings.CameraPartida, 0, CameraRig.ModosPartida.Length - 1)];
            if (touch != null)
            {
                touch.enabled = true;
                touch.goalkeeperMode = false;
                touch.matchMode = true;
                touch.matchHasBall = () => Human != null && Owner == Human;
            }
            if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.SetCrowd(0.55f);

            kickoffTeamFirstHalf = Random.value < 0.5f ? 0 : 1;
            SetHuman(ClosestTo(Times[userTeam], Vector3.zero, true));
            StartKickoff(Times[kickoffTeamFirstHalf]);
            cameraRig.Snap();
            Show("CLIMA: " + Arena.Nome(clima).ToUpper() + "  •  " + Times[0].sigla + " x " + Times[1].sigla, Color.white, 2.6f);
        }

        void SpawnTeam(TimeInfo t, System.Random rng, float nivel)
        {
            var slots = Formacoes.Get(t.formacao);
            var nums = Formacoes.Numeros(t.formacao);
            var used = new HashSet<string>();
            // cada rosto tem 4 combinações de pele/cabelo; embaralhadas para o elenco ficar variado
            var visA = Shuffled(rng); var visB = Shuffled(rng);
            int ia = 0, ib = 0;

            // goleiro
            var gkPrefab = t.goleiroModeloA ? keeperPrefabA : keeperPrefab;
            if (gkPrefab == null) gkPrefab = keeperPrefab;
            var gkGo = Instantiate(gkPrefab, t.OwnGoal + t.Forward * 1.2f, Quaternion.LookRotation(t.Forward));
            spawned.Add(gkGo);
            var gkAtr = GeradorJogador.Atributos(Funcao.Zagueiro, rng, nivel);
            var gkRostos = PlayerLook.Embaralhar("Goleiro", rng, true);   // o uniforme do goleiro só existe com pele clara
            var gkLook = PlayerLook.Sortear(Funcao.Goleiro, !t.goleiroModeloA, 0, gkAtr, rng);
            if (gkRostos.Count > 0) PlayerLook.UsarRosto(gkLook, gkRostos[t.indice % gkRostos.Count], "Goleiro");
            // uniforme antes do penteado: senão o cabelo 3D (black power, coque, faixa) recebe a textura do uniforme
            ApplyKit(gkGo, KitMaterial(gkPrefab, t.kitGoleiro));
            PlayerLook.Aplicar(gkGo, gkLook, t.goleiroModeloA ? 1.87f : 1.90f, !t.goleiroModeloA, propMaterial, hairMats);
            gkLook.rosto = FaceShapes.Aplicar(gkGo, rng);
            var gk = gkGo.GetComponent<GoalkeeperAI>();
            gk.ball = ball;
            gk.side = -t.attackDir;
            gk.sweepRange = 14f;
            gk.cameraTransform = mainCamera.transform;
            TimeInfo tt = t;
            gk.opponentDistance = p => KeeperOpponentDistance(tt, p);
            gk.distributionTarget = () => KeeperTarget(tt);
            gk.OnSave += caught => OnKeeperSave(tt, caught);
            var gkInfo = gkGo.AddComponent<MatchPlayer>();
            gkInfo.gk = gk;
            gkInfo.time = t;
            gkInfo.numero = 1;
            gkInfo.nome = NomesBR.Gerar(rng, used);
            gkInfo.funcao = Funcao.Goleiro;
            gkInfo.atr = gkAtr;
            gkInfo.aparencia = gkLook;
            gkInfo.men = GeradorJogador.Mentalidade(Funcao.Zagueiro, rng);
            gkInfo.modeloB = !t.goleiroModeloA;
            gkGo.name = t.sigla + " 1 " + gkInfo.nome;
            t.goleiro = gk;
            t.goleiroInfo = gkInfo;
            all.Add(gkInfo);

            for (int i = 0; i < slots.Length; i++)
            {
                // todos com o corpo do jogador e o mesmo uniforme; cada um com pele, cabelo, barba, rosto, altura e peso próprios
                const bool b = false;
                var prefab = b && playerPrefabB != null ? playerPrefabB : playerPrefab;
                Vector3 pos = t.ToWorld(Mathf.Min(slots[i].x * 0.9f, -0.04f), slots[i].z);
                var go = Instantiate(prefab, pos, Quaternion.LookRotation(t.Forward));
                spawned.Add(go);
                // aparência própria: pele/cabelo, altura, porte, peso e penteado
                var atr = GeradorJogador.Atributos(slots[i].funcao, rng, nivel);
                // rosto (foto) próprio, sem repetir na partida; o tom de pele do corpo acompanha o rosto
                if (rostosPartida == null || rostosPartida.Count == 0) rostosPartida = PlayerLook.Embaralhar("Jogador", rng, false);
                PlayerLook.Rosto rosto = rostosPartida.Count > 0 ? rostosPartida[rostoIdx++ % rostosPartida.Count] : null;
                int visual = rosto != null ? rosto.visual : (b ? visB[ib++ % PlayerLook.Visuais] : visA[ia++ % PlayerLook.Visuais]);
                var look = PlayerLook.Sortear(slots[i].funcao, b, visual, atr, rng);
                PlayerLook.UsarRosto(look, rosto, "Jogador");
                PlayerLook.AjustarAtributos(atr, look);
                ApplyKit(go, KitMaterial(prefab, PlayerLook.Textura(b, t.indice, visual, b ? t.kitB : t.kitA)));
                PlayerLook.Aplicar(go, look, b ? 1.80f : 1.78f, b, propMaterial, hairMats);
                look.rosto = FaceShapes.Aplicar(go, rng);

                var pc = go.GetComponent<PlayerController>();
                pc.ball = ball;
                pc.cameraTransform = mainCamera.transform;
                pc.attackDir = t.attackDir;
                pc.matchControls = true;
                pc.aiControlled = true;
                pc.InputEnabled = false;
                pc.ballAuthority = false;
                var mp = go.AddComponent<MatchPlayer>();
                mp.pc = pc;
                mp.time = t;
                mp.slot = slots[i];
                mp.funcao = slots[i].funcao;
                mp.numero = nums[i];
                mp.nome = NomesBR.Gerar(rng, used);
                mp.atr = atr;
                mp.aparencia = look;
                mp.men = GeradorJogador.Mentalidade(mp.funcao, rng);
                mp.modeloB = b;
                mp.ApplyPhysical();
                go.name = t.sigla + " " + mp.numero + " " + mp.nome;
                pc.OnBodyContact += (other, comp, n, closing) => OnBodyContact(mp, other, comp, n, closing);
                pc.OnSlideBall += p => OnSlideBall(mp);
                pc.canShoot = () => CanHumanShoot(mp);
                t.jogadores.Add(mp);
                all.Add(mp);
                byPc[pc] = mp;
                prevVel[mp] = Vector3.zero;
            }
        }

        void SpawnReferees()
        {
            var main = refereePrefab != null ? refereePrefab : refereePrefabB;
            if (main == null) return;
            Material ma = KitMaterial(refereePrefab, kitA_Arbitro);
            Material mb = KitMaterial(main, kitB_Arbitro);
            var go = Instantiate(main, new Vector3(-6f, 0f, -12f), Quaternion.LookRotation(Vector3.forward));
            ApplyKit(go, mb);
            RefereeLook(go, false);
            spawned.Add(go);
            MainReferee = go.GetComponent<RefereeAgent>();
            if (MainReferee == null) MainReferee = go.AddComponent<RefereeAgent>();
            MainReferee.Setup(this, RefereeAgent.Papel.Principal, 0f, propMaterial);
            referees.Add(MainReferee);
            // assistentes em lados opostos: um na lateral de baixo cuidando da metade +X, outro na de cima cuidando da -X
            for (int i = 0; i < 2; i++)
            {
                var prefab = refereePrefab != null ? refereePrefab : main;
                float half = i == 0 ? 1f : -1f;
                var ago = Instantiate(prefab, new Vector3(half * 20f, 0f, -half * (Arena.HalfWidth + 1.4f)), Quaternion.identity);
                ApplyKit(ago, prefab == refereePrefab ? ma : mb);
                RefereeLook(ago, false);
                spawned.Add(ago);
                var ar = ago.GetComponent<RefereeAgent>();
                if (ar == null) ar = ago.AddComponent<RefereeAgent>();
                ar.Setup(this, RefereeAgent.Papel.Assistente, half, propMaterial);
                referees.Add(ar);
            }
        }

        void RefereeLook(GameObject go, bool modelB)
        {
            var rng = new System.Random(Random.Range(0, int.MaxValue));
            var atr = GeradorJogador.Atributos(Funcao.Meia, rng, 0.6f);
            var look = PlayerLook.Sortear(Funcao.Meia, modelB, 0, atr, rng);
            var rr = PlayerLook.Embaralhar("Jogador", rng, true);           // o uniforme da arbitragem só existe com pele clara
            if (rr.Count > 0) PlayerLook.UsarRosto(look, rr[0], "Jogador");
            if (look.penteado == Penteado.BlackPower || look.penteado == Penteado.Moicano) look.penteado = Penteado.Curto;
            if (look.porte == Porte.Pesado) look.porte = Porte.Normal;
            PlayerLook.Aplicar(go, look, 1.80f, modelB, propMaterial, hairMats);
        }

        readonly Dictionary<string, Material> kitCache = new Dictionary<string, Material>();
        readonly Dictionary<Color, Material> hairMats = new Dictionary<Color, Material>();
        List<PlayerLook.Rosto> rostosPartida;
        int rostoIdx;

        static int[] Shuffled(System.Random rng)
        {
            var a = new int[PlayerLook.Visuais];
            for (int i = 0; i < a.Length; i++) a[i] = i;
            for (int i = a.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); int x = a[i]; a[i] = a[j]; a[j] = x; }
            return a;
        }

        Material KitMaterial(GameObject prefab, Texture2D tex)
        {
            if (prefab == null) return null;
            Renderer r = null;
            foreach (var x in prefab.GetComponentsInChildren<Renderer>(true)) if (PlayerLook.EhCorpo(x)) { r = x; break; }
            if (r == null || r.sharedMaterial == null) return null;
            string key = r.sharedMaterial.name + "/" + (tex != null ? tex.name : "orig");
            Material m;
            if (kitCache.TryGetValue(key, out m)) return m;
            m = new Material(r.sharedMaterial);
            if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            kitCache[key] = m;
            if (weather != null) weather.RegisterCharacterMaterial(m);
            return m;
        }

        static void ApplyKit(GameObject go, Material m)
        {
            if (m == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!PlayerLook.EhCorpo(r)) continue; // cabeça (foto) e cabelo 3D têm material próprio
                var arr = r.sharedMaterials;
                for (int i = 0; i < arr.Length; i++) arr[i] = m;
                r.sharedMaterials = arr;
            }
        }

        /// <summary>Encerra a partida e devolve o estádio ao treino.</summary>
        public void Teardown()
        {
            if (cameraRig != null)
            {
                cameraRig.povHead = null;
                cameraRig.matchMode = false;
                cameraRig.attackDir = 1f;
                cameraRig.player = savedRigPlayer;
                cameraRig.keeper = savedRigKeeper;
            }
            foreach (var go in spawned) if (go != null) Destroy(go);
            spawned.Clear();
            all.Clear();
            byPc.Clear();
            referees.Clear();
            prevVel.Clear();
            controlCooldown.Clear();
            offsideCandidates.Clear();
            foreach (var m in kitCache.Values) { if (weather != null) weather.UnregisterCharacterMaterial(m); Destroy(m); }
            kitCache.Clear();
            FaceShapes.Limpar();
            foreach (var m in hairMats.Values) Destroy(m);
            hairMats.Clear();
            rostosPartida = null; rostoIdx = 0;
            if (ParticleEffectsManager.Instance != null) ParticleEffectsManager.Instance.ClearAllEffects();
            // texturas de pele/cabelo (Resources/Visuais) e materiais da partida liberados da memória
            Resources.UnloadUnusedAssets();
            Times = null;
            Owner = null; Human = null; KeeperHolding = null; LastToucher = null; Scorer = null; Restart = null;
            Active = false;
            Phase = Fase.Inativo;
            foreach (var go in trainingObjects) if (go != null) go.SetActive(true);
            if (touch != null) { touch.matchMode = false; touch.matchHasBall = null; }
            if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.SetCrowd(0f);
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        public void SetPaused(bool paused)
        {
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (touch != null) touch.enabled = !paused && Active;
            if (!paused && Human != null && Human.pc != null) Human.pc.BlockInput(0.35f);
        }

        // ============================================================ controle humano

        void SetHuman(MatchPlayer p)
        {
            if (p == null || p == Human || p.pc == null) return;
            if (Human != null && Human.pc != null)
            {
                Human.pc.InputEnabled = false;
                Human.pc.aiControlled = true;
            }
            Human = p;
            p.pc.aiControlled = false;
            p.pc.InputEnabled = true;
            p.pc.aiMoveWorld = Vector3.zero;
            p.pc.BlockInput(0.12f);
            lastSwitch = Time.time;
            cameraRig.player = p.transform;
            cameraRig.attackDir = p.time.attackDir;
            cameraRig.povHead = p.pc.Sockets != null ? p.pc.Sockets.Head : null;
            cameraRig.keeper = OpponentOf(p.time).goleiro != null ? OpponentOf(p.time).goleiro.transform : null;
        }

        bool CanHumanShoot(MatchPlayer p)
        {
            if (!Active || p != Human) return true;
            if (Phase == Fase.BolaParada)
                return Restart != null && Restart.taker == p && Restart.waitingHuman &&
                       (Restart.tipo == TipoReinicio.Falta || Restart.tipo == TipoReinicio.Penalti);
            if (Phase != Fase.Jogo) return false;
            if (Owner == p) return true;
            Vector3 cp = p.pc.ControlPoint;
            Vector3 b = BallPos;
            return Owner == null && KeeperHolding == null && Flat(b - cp).magnitude < 1.5f && b.y < 2.3f;
        }

        void UpdateHumanInput()
        {
            bool low = InputBridge.LowShot, chip = InputBridge.Chip, shoot = InputBridge.Shoot, knock = InputBridge.KnockOn;
            bool lowDown = low && !prevLow, chipDown = chip && !prevChip, shootDown = shoot && !prevShoot, knockDown = knock && !prevKnock;
            prevLow = low; prevChip = chip; prevShoot = shoot; prevKnock = knock;
            if (Human == null || Human.pc == null || Time.timeScale == 0f) return;
            if (Human.expulso) { SetHuman(ClosestTo(Human.time, BallPos, true)); return; }

            if (Phase == Fase.BolaParada)
            {
                if (Restart != null && Restart.waitingHuman && Restart.taker == Human)
                {
                    if (lowDown || chipDown) ExecuteRestartHuman(chipDown);
                    else if (Human.pc.Busy && (Restart.tipo == TipoReinicio.Falta || Restart.tipo == TipoReinicio.Penalti)) { /* chute em andamento */ }
                }
                return;
            }
            if (Phase != Fase.Jogo) return;

            if (lowDown) bufLow = Time.time;
            if (chipDown) bufChip = Time.time;
            if (Owner == Human)
            {
                bool wantLow = Time.time - bufLow < InputBuffer, wantChip = Time.time - bufChip < InputBuffer;
                if (wantLow || wantChip)
                {
                    if (Human.pc.Busy) return;          // espera o domínio terminar e passa logo em seguida
                    bool lofted = wantChip && (!wantLow || bufChip > bufLow);
                    bufLow = bufChip = -9f;
                    HumanPass(lofted);
                }
                return;
            }
            // sem a bola: ✕ = desarme, ○ = carrinho, L1 = trocar de jogador
            if (knockDown && Owner != Human) { SwitchToBest(true); return; }
            if (lowDown)
            {
                if (Owner != null && Owner.time != Human.time && Vector3.Distance(Human.Pos, Owner.Pos) < 2.8f) TryTackle(Human, false);
            }
            else if (shootDown && !CanHumanShoot(Human))
            {
                if (Vector3.Distance(Human.Pos, BallPos) < 5.5f) TryTackle(Human, true);
            }
        }

        void HumanPass(bool lofted)
        {
            var h = Human;
            Vector2 mv = InputBridge.Move;
            Vector3 dir;
            if (mv.sqrMagnitude > 0.04f && mainCamera != null)
            {
                Vector3 f = Flat(mainCamera.transform.forward).normalized, r = Flat(mainCamera.transform.right).normalized;
                dir = (f * mv.y + r * mv.x).normalized;
            }
            else dir = Flat(h.transform.forward).normalized;

            MatchPlayer best = null; float bestScore = -9f;
            foreach (var m in h.time.jogadores)
            {
                if (m == h || !m.Disponivel) continue;
                Vector3 to = Flat(m.Pos - h.Pos);
                float d = to.magnitude;
                if (d < 3f || d > (lofted ? 60f : 42f)) continue;
                float cos = Vector3.Dot(to / d, dir);
                if (cos < 0.35f) continue;
                float score = cos * 2f - d / (lofted ? 60f : 30f) + (lofted ? Flat(m.Vel).magnitude * 0.05f : 0f);
                if (score > bestScore) { bestScore = score; best = m; }
            }
            Vector3 target;
            if (best != null)
            {
                float d = Vector3.Distance(h.Pos, best.Pos);
                float lead = lofted ? Mathf.Clamp(d / 17f, 0.6f, 2.2f) : Mathf.Clamp(d / 15f, 0.2f, 1.2f);
                target = best.Pos + Flat(best.Vel) * lead;
                if (lofted) target += best.time.Forward * 3f; // em profundidade: na frente do companheiro
            }
            else target = h.Pos + dir * (lofted ? 30f : 14f);
            target.x = Mathf.Clamp(target.x, -Arena.HalfLength + 1f, Arena.HalfLength - 1f);
            target.z = Mathf.Clamp(target.z, -Arena.HalfWidth + 1f, Arena.HalfWidth - 1f);
            float err = (1f - h.atr.passe) * 0.55f + Pressure(h) * 0.25f;
            if (h.pc.Pass(target, lofted || Vector3.Distance(h.Pos, target) > 34f, err)) NotifyPass(h, best, target);
        }

        void SwitchToBest(bool manual)
        {
            var team = UserTeam;
            MatchPlayer best = null; float bt = float.MaxValue;
            Vector3 b = BallPos;
            foreach (var m in team.jogadores)
            {
                if (!m.Disponivel || m == Human) continue;
                float t = Vector3.Distance(m.Pos, b) / 8f;
                if (manual && Human != null && Vector3.Dot(Flat(m.Pos - Human.Pos), Flat(b - Human.Pos)) < 0f) t += 0.6f;
                if (t < bt) { bt = t; best = m; }
            }
            if (best != null) SetHuman(best);
        }

        void UpdateAutoSwitch()
        {
            if (UserTeam == null) return;
            if (Human == null || Human.expulso) { SetHuman(ClosestTo(UserTeam, BallPos, true)); return; }
            if (Owner != null && Owner.time == UserTeam && Owner != Human && !Owner.IsKeeper) { SetHuman(Owner); return; }
            if (Time.time - lastSwitch < 1.3f || Owner == Human) return;
            if (Human.pc != null && (Human.pc.Busy || Human.pc.Charging)) return;
            // defendendo/bola solta: passa o controle para quem está bem mais perto da bola
            float th = Vector3.Distance(Human.Pos, BallPos);
            var near = ClosestTo(UserTeam, BallPos, true);
            if (near != null && near != Human)
            {
                float tn = Vector3.Distance(near.Pos, BallPos);
                if (th > tn * 1.8f + 6f) SetHuman(near);
            }
        }

        // ============================================================ ciclo

        void Update()
        {
            if (!Active) return;
            if (Time.timeScale == 0f) return;
            float dt = Time.deltaTime;
            phaseTime += dt;

            if (InputBridge.MenuPressed && Phase != Fase.Fim) { if (OnPauseRequested != null) OnPauseRequested(); return; }
            if (InputBridge.CameraPressed) CycleCamera();

            UpdateTacticalCaches(dt);
            foreach (var m in all) m.Tick(this, dt);
            foreach (var r in referees) if (r != null) r.Tick(dt);

            UpdateHumanInput();
            if (Phase == Fase.Jogo || Phase == Fase.BolaParada) UpdateAutoSwitch();
            UpdateSlips(dt);

            switch (Phase)
            {
                case Fase.Jogo:
                    Clock += dt;
                    TrackPossessionTime(dt);
                    CheckBallOut();
                    CheckClock();
                    break;
                case Fase.BolaParada:
                    Clock += dt * 0.5f;
                    UpdateRestart();
                    break;
                case Fase.Comemoracao:
                    if (phaseTime > 5.5f) { cameraRig.mode = savedCam; StartKickoff(concededTeam); }
                    break;
                case Fase.Parado:
                    if (resumeAt > 0f && Time.time >= resumeAt) { resumeAt = -1f; var a = afterPause; afterPause = null; if (a != null) a(); }
                    break;
                case Fase.Intervalo:
                    if (phaseTime > 4.5f || (phaseTime > 1.5f && InputBridge.ConfirmPressed)) StartSecondHalf();
                    break;
            }
        }

        void FixedUpdate()
        {
            if (!Active || ball == null) return;
            DetectKicksAndKeeper();
            if (Phase == Fase.Jogo) ArbitratePossession();
            UpdateAuthorities();
        }

        void CycleCamera()
        {
            cameraRig.Cycle();
            int i = Array.IndexOf(CameraRig.ModosPartida, cameraRig.mode);
            if (i >= 0) GameSettings.CameraPartida = i;
        }

        public void CycleCameraFromMenu() { CycleCamera(); }

        // ============================================================ posse de bola

        void DetectKicksAndKeeper()
        {
            // chute/passe: a bola sai do pé de quem tinha a posse
            if (ball.LastKickTime > seenKick + 1e-4f)
            {
                seenKick = ball.LastKickTime;
                MatchPlayer kicker = Owner;
                if (kicker == null) kicker = NearestToBall(1.6f, null);
                bool fromRestart = Phase == Fase.BolaParada && Restart != null && (Restart.executing || Restart.waitingHuman);
                if (fromRestart && Restart.taker != null) kicker = Restart.taker;
                if (kicker != null)
                {
                    LastToucher = kicker;
                    LastPossession = kicker.time;
                    SnapshotOffside(kicker);
                    CountShot(kicker);
                }
                Owner = null;
                if (fromRestart) ResumePlay();
            }
            // goleiro: segurou / soltou a bola
            MatchPlayer holder = null;
            if (ball.IsHeld && Times != null)
                foreach (var t in Times)
                    if (t.goleiro != null && t.goleiro.HoldingBall) holder = t.goleiroInfo;
            if (holder != null)
            {
                if (KeeperHolding != holder)
                {
                    KeeperHolding = holder;
                    LastToucher = holder;
                    LastPossession = holder.time;
                    Owner = null;
                    ClearOffside();
                }
                keeperWasHolding = true;
                keeperReleaser = holder;
            }
            else if (keeperWasHolding)
            {
                keeperWasHolding = false;
                KeeperHolding = null;
                if (keeperReleaser != null)
                {
                    LastToucher = keeperReleaser;
                    LastPossession = keeperReleaser.time;
                    if (Phase == Fase.BolaParada && Restart != null && Restart.tipo == TipoReinicio.TiroDeMeta) { restartKickExempt = true; ResumePlay(); }
                    else SnapshotOffside(keeperReleaser);
                }
            }
        }

        void ArbitratePossession()
        {
            if (ball.IsHeld) { Owner = null; return; }
            Vector3 b = BallPos;
            if (Owner != null)
            {
                Vector3 cp = Owner.pc.ControlPoint;
                bool keep = Owner.Disponivel && !Owner.pc.Sliding && Flat(b - cp).magnitude < 1.9f && b.y < 1.2f;
                if (!keep) { Owner = null; }
                else return;
            }
            if (Time.time - ball.LastKickTime < 0.14f) return;

            // quem alcança a bola (pé ou peito); disputa quando os dois times chegam juntos
            MatchPlayer best = null, rival = null;
            float bestD = 99f, rivalD = 99f;
            float maxY = 1.45f;
            if (b.y > maxY) { CheckHeaders(b); return; }
            Vector3 bv = BallVel;
            foreach (var m in all)
            {
                if (m.pc == null || !m.Disponivel || m.pc.Sliding) continue;
                float cd;
                if (controlCooldown.TryGetValue(m, out cd) && Time.time < cd) continue;
                Vector3 cp = m.pc.ControlPoint;
                float d = Flat(b - cp).magnitude;
                float reach = 0.72f + Mathf.Min(0.3f, m.pc.Velocity.magnitude * 0.03f);
                if (d > reach && Flat(b - m.Pos).magnitude > 0.85f) continue;
                if (best == null || d < bestD)
                {
                    if (best != null && best.time != m.time) { rival = best; rivalD = bestD; }
                    best = m; bestD = d;
                }
                else if (best.time != m.time && d < rivalD) { rival = m; rivalD = d; }
            }
            if (best == null) return;
            if (rival != null && rivalD - bestD < 0.35f)
            {
                // dividida: força, equilíbrio e técnica (com sorte)
                float sb = best.atr.forca * 0.4f + best.atr.equilibrio * 0.25f + best.atr.drible * 0.25f + Random.value * 0.45f;
                float sr = rival.atr.forca * 0.4f + rival.atr.equilibrio * 0.25f + rival.atr.drible * 0.25f + Random.value * 0.45f;
                if (sr > sb) { var tmp = best; best = rival; rival = tmp; }
                if (rival.pc != null && Random.value < 0.35f) rival.pc.Stagger(Flat(rival.Pos - best.Pos).normalized * 1.6f, false);
            }
            // domínio: bola muito rápida pode escapar (técnica e chuva)
            float rel = Flat(bv - best.Vel).magnitude;
            float wet = weather != null && weather.P != null ? weather.P.wetness : 0f;
            float miss = Mathf.Clamp01((rel - 11f) / 14f) * (1.1f - best.atr.drible * 0.6f) + wet * 0.04f;
            if (b.y > 0.6f) miss += 0.12f * (1f - best.atr.drible);
            if (Random.value < miss)
            {
                Vector3 dv = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * Flat(bv).normalized * Mathf.Clamp(rel * 0.35f, 2f, 7f);
                ball.Deflect(dv + Vector3.up * Random.Range(0.2f, 1.2f), Vector3.zero);
                controlCooldown[best] = Time.time + 0.45f;
                LastToucher = best;
                return;
            }
            if (b.y > 0.55f)
            {
                // matada no peito/coxa: a bola cai no pé
                Vector3 v = best.Vel * 0.7f + Vector3.down * 1.2f;
                ball.Deflect(v, Vector3.zero);
            }
            GainPossession(best);
        }

        void GainPossession(MatchPlayer p)
        {
            bool newTeam = LastPossession != p.time;
            if (offsideCandidates.Contains(p) && offsideTeam == p.time && Time.time - offsideStamp < 7f)
            {
                CallOffside(p);
                return;
            }
            ClearOffside();
            Owner = p;
            LastToucher = p;
            LastPossession = p.time;
            p.expectingPassUntil = 0f;
            if (newTeam && LastPassFrom != null && LastPassFrom.time != p.time) { LastPassFrom.passesErrados++; LastPassFrom = null; }
            else if (LastPassFrom != null && LastPassFrom.time == p.time && LastPassFrom != p) { LastPassFrom.passesCertos++; LastPassFrom = null; }
        }

        void CheckHeaders(Vector3 b)
        {
            if (Time.time < headerCooldown || b.y > 2.7f) return;
            MatchPlayer best = null; float bs = -9f;
            foreach (var m in all)
            {
                if (m.pc == null || !m.Disponivel) continue;
                float d = Flat(b - m.Pos).magnitude;
                if (d > 0.95f) continue;
                float jumpReach = 1.85f * m.transform.localScale.y + 0.45f * m.atr.cabeceio;
                if (b.y > jumpReach + 0.3f) continue;
                float s = m.atr.cabeceio * 0.6f + m.atr.forca * 0.3f + Random.value * 0.4f - d;
                if (s > bs) { bs = s; best = m; }
            }
            if (best == null) return;
            headerCooldown = Time.time + 0.5f;
            if (offsideCandidates.Contains(best) && offsideTeam == best.time && Time.time - offsideStamp < 7f) { CallOffside(best); return; }
            ClearOffside();
            Header(best, b);
        }

        void Header(MatchPlayer m, Vector3 b)
        {
            Vector3 target;
            float speed;
            TimeInfo t = m.time;
            Vector3 goal = t.TargetGoal;
            float dGoal = Vector3.Distance(Flat(b), goal);
            if (dGoal < 15f && Mathf.Abs(b.z) < 14f)
            {
                // cabeceio para o gol, de cima para baixo
                float z = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.6f, Arena.GoalHalfWidth - 0.4f);
                target = new Vector3(goal.x, Random.Range(0.3f, 1.6f), z);
                speed = Mathf.Lerp(11f, 17f, m.atr.cabeceio);
                t.chutes++;
            }
            else if (t.Fwd(b) < -25f)
            {
                // afastando de cabeça
                target = Flat(b) + t.Forward * 22f + new Vector3(0f, 0f, Mathf.Sign(b.z == 0f ? 1f : b.z) * 8f);
                speed = 15f;
            }
            else
            {
                var mate = ClosestTo(t, Flat(b) + t.Forward * 10f, false, m);
                target = mate != null ? mate.Pos : Flat(b) + t.Forward * 12f;
                speed = 11f;
            }
            Vector3 d = target - b;
            float err = (1f - m.atr.cabeceio) * 10f;
            Vector3 dir = Quaternion.Euler(Random.Range(-err, err) * 0.5f, Random.Range(-err, err), 0f) * d.normalized;
            if (dGoal >= 15f) dir.y = Mathf.Max(dir.y, 0.35f);
            m.pc.Play("Cabeceio", 0.06f);
            ball.Kick(dir.normalized * speed, Vector3.zero);
            if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.Kick(b, 0.4f);
            LastToucher = m;
        }

        /// <summary>Só o dono da bola (ou o cobrador) pode mexer nela.</summary>
        void UpdateAuthorities()
        {
            foreach (var m in all)
            {
                if (m.pc == null) continue;
                bool auth;
                // na bola parada o cobrador só mexe na bola quando o chute/passe já começou (a bola não "gruda" no pé antes)
                if (Phase == Fase.BolaParada) auth = Restart != null && Restart.taker == m && (Restart.executing || (Restart.waitingHuman && m.pc.Busy));
                else if (Phase == Fase.Jogo) auth = m == Owner;
                else auth = false;
                m.pc.ballAuthority = auth;
            }
        }

        void TrackPossessionTime(float dt)
        {
            if (LastPossession != null) LastPossession.tempoPosse += dt;
        }

        MatchPlayer LastPassFrom;

        public void NotifyPass(MatchPlayer from, MatchPlayer to, Vector3 target)
        {
            LastPassFrom = from;
            if (to != null)
            {
                to.expectingPassUntil = Time.time + 2.6f;
                to.expectingPassAt = target;
            }
        }

        /// <summary>A IA avisa que vai finalizar (a contagem acontece quando a bola sai do pé).</summary>
        public void NotifyShot(MatchPlayer from) { }

        /// <summary>Conta como finalização todo chute forte rumo ao gol atacado.</summary>
        void CountShot(MatchPlayer kicker)
        {
            Vector3 v = BallVel;
            Vector3 toGoal = Flat(kicker.time.TargetGoal - BallPos);
            float d = toGoal.magnitude;
            if (d > 38f || v.magnitude < 11f) return;
            if (Vector3.Dot(Flat(v).normalized, toGoal / d) < 0.82f) return;
            kicker.time.chutes++;
            shotBy = kicker;
            shotAt = Time.time;
        }

        MatchPlayer shotBy;
        float shotAt;

        void OnKeeperSave(TimeInfo keeperTeam, bool caught)
        {
            if (!Active) return;
            var gkInfo = keeperTeam.goleiroInfo;
            LastToucher = gkInfo;
            if (shotBy != null && shotBy.time != keeperTeam && Time.time - shotAt < 4f)
            {
                shotBy.time.chutesNoGol++;
                shotBy = null;
                Show(caught ? "DEFESAÇA!" : "ESPALMOU!", new Color(0.6f, 0.9f, 1f), 1.4f);
                if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.CrowdReaction(0.6f);
            }
            ClearOffside();
        }

        // ============================================================ impedimento

        void SnapshotOffside(MatchPlayer kicker)
        {
            offsideCandidates.Clear();
            offsideTeam = null;
            if (restartKickExempt) { restartKickExempt = false; return; } // lateral, escanteio e tiro de meta não têm impedimento
            var t = kicker.time;
            float line = OffsideLineStrict(t);
            float ballF = t.Fwd(BallPos);
            foreach (var m in t.jogadores)
            {
                if (m == kicker || !m.Disponivel) continue;
                float f = t.Fwd(m.Pos);
                if (f > 0.3f && f > line + 0.25f && f > ballF + 0.1f) offsideCandidates.Add(m);
            }
            if (offsideCandidates.Count > 0) { offsideTeam = t; offsideStamp = Time.time; }
        }

        void ClearOffside() { offsideCandidates.Clear(); offsideTeam = null; }

        void CallOffside(MatchPlayer p)
        {
            ClearOffside();
            p.time.impedimentos++;
            // o assistente da metade onde aconteceu levanta a bandeira
            RefereeAgent ar = null;
            foreach (var r in referees) if (r != null && r.papel == RefereeAgent.Papel.Assistente && Mathf.Sign(r.half) == Mathf.Sign(p.Pos.x)) ar = r;
            if (ar != null) ar.RaiseFlag(2f);
            if (MainReferee != null) MainReferee.Whistle(1);
            Show("IMPEDIMENTO  •  " + p.Rotulo, UiKit.Yellow, 2f);
            StartRestart(TipoReinicio.Falta, OpponentOf(p.time), Flat(p.Pos), true, 1.2f);
        }

        // ============================================================ bola fora, gol e tempo

        void CheckBallOut()
        {
            if (ball.IsHeld) return;
            Vector3 b = BallPos;
            float R = Arena.BallRadius;
            if (Mathf.Abs(b.x) > Arena.HalfLength + R)
            {
                float s = Mathf.Sign(b.x);
                bool between = Mathf.Abs(b.z) < Arena.GoalHalfWidth - R * 0.5f && b.y < Arena.GoalHeight - R * 0.3f;
                TimeInfo attacking = Times[0].attackDir == s ? Times[0] : Times[1];
                TimeInfo defending = OpponentOf(attacking);
                if (between) { Goal(attacking); return; }
                if (LastToucher != null && LastToucher.time == defending)
                {
                    attacking.escanteios++;
                    Vector3 corner = new Vector3(s * (Arena.HalfLength - 0.4f), 0f, Mathf.Sign(b.z == 0f ? 1f : b.z) * (Arena.HalfWidth - 0.4f));
                    Whistle(1);
                    Show("ESCANTEIO", Color.white, 1.4f);
                    StartRestart(TipoReinicio.Escanteio, attacking, corner, false, 1.6f);
                }
                else
                {
                    Whistle(1);
                    Show("TIRO DE META", Color.white, 1.4f);
                    StartRestart(TipoReinicio.TiroDeMeta, defending, defending.OwnGoal + defending.Forward * 5.5f, false, 1.2f);
                }
                return;
            }
            if (Mathf.Abs(b.z) > Arena.HalfWidth + R)
            {
                TimeInfo to = LastToucher != null ? OpponentOf(LastToucher.time) : Times[0];
                Vector3 spot = new Vector3(Mathf.Clamp(b.x, -Arena.HalfLength + 1f, Arena.HalfLength - 1f), 0f, Mathf.Sign(b.z) * (Arena.HalfWidth + 0.2f));
                foreach (var r in referees)
                    if (r != null && r.papel == RefereeAgent.Papel.Assistente && Mathf.Sign(r.SideZ) == Mathf.Sign(b.z)) r.RaiseFlag(1.2f);
                Show("LATERAL", Color.white, 1.2f);
                StartRestart(TipoReinicio.Lateral, to, spot, false, 1.4f);
            }
        }

        bool scorerOwnGoal;
        TimeInfo concededTeam;
        CameraRig.Modo savedCam;

        void Goal(TimeInfo scoringTeam)
        {
            scoringTeam.gols++;
            concededTeam = OpponentOf(scoringTeam);
            Scorer = LastToucher;
            scorerOwnGoal = Scorer != null && Scorer.time != scoringTeam;
            if (Scorer != null && !scorerOwnGoal) Scorer.gols++;
            if (shotBy != null && shotBy.time == scoringTeam) { scoringTeam.chutesNoGol++; shotBy = null; }
            Phase = Fase.Comemoracao;
            phaseTime = 0f;
            Owner = null;
            ClearOffside();
            Whistle(1);
            if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.CrowdReaction(1f);
            if (ParticleEffectsManager.Instance != null && ball != null) ParticleEffectsManager.Instance.PlayGoalCelebration(ball.transform.position);
            GameSettings.Vibrar();
            string who = Scorer != null ? (scorerOwnGoal ? Scorer.Rotulo + " (contra)" : Scorer.Rotulo) : scoringTeam.sigla;
            Show("GOOOL!  " + who, scoringTeam.cor, 4.5f);
            if (Scorer != null && !scorerOwnGoal && Scorer.pc != null)
            {
                float zs = Mathf.Sign(Scorer.Pos.z == 0f ? 1f : Scorer.Pos.z);
                CelebrationSpot = new Vector3(scoringTeam.attackDir * (Arena.HalfLength - 4f), 0f, zs * (Arena.HalfWidth - 5f));
                    StartCoroutine(CelebrationDance(Scorer));
                savedCam = cameraRig.mode;
                cameraRig.player = Scorer.transform;
                cameraRig.mode = CameraRig.Modo.Comemoracao;
            }
            else
            {
                savedCam = cameraRig.mode;
                CelebrationSpot = Flat(BallPos);
            }
            if (concededTeam.goleiro != null) concededTeam.goleiro.PlayGesture("GK_Frustracao");
        }

        System.Collections.IEnumerator CelebrationDance(MatchPlayer p)
        {
            // corre para a torcida e depois comemora (dança ou "sim" com o punho)
            yield return new WaitForSeconds(1.7f);
            if (p == null || p.pc == null || Phase != Fase.Comemoracao) yield break;
            p.celebrating = true;
            p.pc.Play(p.men.criatividade > 0.5f ? "Comemorar" : "ComemorarSim", 0.3f);
            yield return new WaitForSeconds(2.6f);
            if (p != null) p.celebrating = false;
            if (p != null && p.pc != null && Phase == Fase.Comemoracao) p.pc.Play("Locomocao", 0.4f);
        }

        void CheckClock()
        {
            if (Clock < HalfSeconds) return;
            // não interrompe um ataque perigoso no último segundo
            if (Owner != null && Owner.time.Fwd(Owner.Pos) > 30f && Clock < HalfSeconds + 6f) return;
            if (Half == 1)
            {
                Phase = Fase.Intervalo;
                phaseTime = 0f;
                Whistle(2);
                Show("INTERVALO  •  " + Times[0].sigla + " " + Times[0].gols + " x " + Times[1].gols + " " + Times[1].sigla, Color.white, 4f);
            }
            else
            {
                Phase = Fase.Fim;
                phaseTime = 0f;
                Whistle(3);
                if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.CrowdReaction(0.8f);
                Show("FIM DE JOGO", Color.white, 3f);
                foreach (var m in all) if (m.pc != null) { m.pc.aiMoveWorld = Vector3.zero; m.pc.InputEnabled = false; m.pc.aiControlled = true; }
            }
        }

        void StartSecondHalf()
        {
            kickoffTeleport = true;
            Half = 2;
            Clock = 0f;
            // troca de lado
            foreach (var t in Times)
            {
                t.attackDir = -t.attackDir;
                foreach (var m in t.jogadores) if (m.pc != null) m.pc.attackDir = t.attackDir;
                if (t.goleiro != null) t.goleiro.side = -t.attackDir;
            }
            if (Human != null) cameraRig.attackDir = Human.time.attackDir;
            Show("2º TEMPO", Color.white, 1.8f);
            StartKickoff(Times[1 - kickoffTeamFirstHalf]);
        }

        // ============================================================ reinícios

        void StartKickoff(TimeInfo team)
        {
            Scorer = null;
            StartRestart(TipoReinicio.Saida, team, Vector3.zero, false, 2.2f);
            foreach (var t in Times)
            {
                if (t.goleiro != null) t.goleiro.ResetTo(t.OwnGoal + t.Forward * 1.2f);
                // todos para o próprio campo
                foreach (var m in t.jogadores)
                {
                    if (m.pc == null || !m.Disponivel) continue;
                    Vector3 p = RestartSpot(m);
                    if (Vector3.Distance(m.Pos, p) > 25f && Phase == Fase.BolaParada && phaseTime < 0.1f && kickoffTeleport)
                        m.pc.Teleport(p, BallPos);
                }
            }
            kickoffTeleport = false;
            cameraRig.player = Human != null ? Human.transform : null;
        }
        bool kickoffTeleport = true;

        /// <summary>Prepara uma bola parada: posiciona a bola, escolhe o cobrador e espera o time se arrumar.</summary>
        void StartRestart(TipoReinicio tipo, TimeInfo team, Vector3 spot, bool indirect, float minWait)
        {
            Phase = Fase.BolaParada;
            phaseTime = 0f;
            Owner = null;
            ClearOffside();
            spot.y = Arena.BallRadius;
            if (tipo != TipoReinicio.Lateral)
            {
                spot.x = Mathf.Clamp(spot.x, -Arena.HalfLength + 0.4f, Arena.HalfLength - 0.4f);
                spot.z = Mathf.Clamp(spot.z, -Arena.HalfWidth + 0.4f, Arena.HalfWidth - 0.4f);
            }
            Restart = new Reinicio { tipo = tipo, time = team, spot = spot, start = Time.time, indirect = indirect, executeAt = Time.time + minWait };
            restartKickExempt = tipo == TipoReinicio.Lateral || tipo == TipoReinicio.Escanteio || tipo == TipoReinicio.TiroDeMeta;

            if (tipo == TipoReinicio.TiroDeMeta && team.goleiro != null)
            {
                Vector3 gp = team.OwnGoal + team.Forward * 5.5f;
                team.goleiro.ResetTo(gp);
                ball.PlaceAt(gp + Vector3.up * 1f);
                team.goleiro.TakeBallInHands();
                Restart.taker = team.goleiroInfo;
                Restart.aim = gp + team.Forward * 30f;
                return;
            }
            ball.PlaceAt(tipo == TipoReinicio.Lateral ? new Vector3(spot.x, Arena.BallRadius, Mathf.Sign(spot.z) * (Arena.HalfWidth - 0.05f)) : spot);

            // cobrador
            MatchPlayer taker;
            if (tipo == TipoReinicio.Penalti || tipo == TipoReinicio.Falta && ShotQuality(spot, team) > 0.1f)
                taker = BestBy(team, m => m.atr.finalizacao * 0.7f + m.men.compostura * 0.3f);
            else if (tipo == TipoReinicio.Escanteio)
                taker = BestBy(team, m => m.atr.passe + (m.funcao == Funcao.Meia || m.funcao == Funcao.MeiaAberto || m.funcao == Funcao.Ponta ? 0.2f : 0f) - Vector3.Distance(m.Pos, spot) * 0.004f);
            else if (tipo == TipoReinicio.Saida)
                taker = BestBy(team, m => (m.funcao == Funcao.Atacante ? 1f : 0f) + m.atr.passe * 0.2f);
            else taker = ClosestTo(team, spot, false);
            Restart.taker = taker;
            Vector3 goal = team.TargetGoal;
            Restart.aim = tipo == TipoReinicio.Saida ? spot - team.Forward * 10f : (tipo == TipoReinicio.Lateral ? spot + new Vector3(0f, 0f, -Mathf.Sign(spot.z) * 10f) : goal);
            if (tipo == TipoReinicio.Penalti)
            {
                var opp = OpponentOf(team);
                if (opp.goleiro != null) opp.goleiro.ResetTo(opp.OwnGoal + opp.Forward * 0.2f);
            }
            if (taker != null && taker.time.humano && taker.pc != null && UserTeam == team) SetHuman(taker);
        }

        void UpdateRestart()
        {
            var r = Restart;
            if (r == null) { ResumePlay(); return; }
            if (r.tipo == TipoReinicio.TiroDeMeta) return; // o goleiro repõe sozinho (DetectKicksAndKeeper retoma o jogo)
            var tk = r.taker;
            if (tk == null || tk.pc == null || !tk.Disponivel)
            {
                r.taker = ClosestTo(r.time, r.spot, false);
                return;
            }
            if (r.executing)
            {
                if (Time.time - r.start > 14f) ResumePlay(); // segurança
                return;
            }
            Vector3 stand = TakerStand(r);
            float d = Flat(tk.Pos - stand).magnitude;
            bool ready = d < 0.8f && Time.time >= r.executeAt;
            // espera o time se posicionar (até um limite)
            if (ready && Time.time - r.start < 4.5f && !TeamsSettled()) ready = false;
            if (!ready)
            {
                if (Time.time - r.start > 9f && d > 0.8f) tk.pc.Teleport(stand, r.spot);
                if (r.tipo == TipoReinicio.Lateral && d < 1.2f && !ball.IsHeld) ball.Hold(tk.transform, new Vector3(0f, 2.15f, 0.12f));
                return;
            }
            if (r.tipo == TipoReinicio.Lateral && !ball.IsHeld) ball.Hold(tk.transform, new Vector3(0f, 2.15f, 0.12f));
            bool humanTaker = tk == Human && tk.time.humano;
            if (humanTaker)
            {
                if (!r.waitingHuman) { r.waitingHuman = true; if (r.tipo == TipoReinicio.Falta || r.tipo == TipoReinicio.Penalti || r.tipo == TipoReinicio.Saida) Whistle(1); }
                if (Time.time - r.start > 16f) ExecuteRestartAI(r);
                return;
            }
            ExecuteRestartAI(r);
        }

        bool TeamsSettled()
        {
            int far = 0;
            foreach (var m in all)
            {
                if (m.pc == null || !m.Disponivel) continue;
                if (Flat(m.Pos - RestartSpot(m)).magnitude > 4f) far++;
            }
            return far <= 2;
        }

        Vector3 TakerStand(Reinicio r)
        {
            Vector3 aimDir = Flat(r.aim - r.spot);
            if (aimDir.sqrMagnitude < 0.01f) aimDir = r.time.Forward;
            aimDir.Normalize();
            if (r.tipo == TipoReinicio.Lateral) return new Vector3(r.spot.x, 0f, Mathf.Sign(r.spot.z) * (Arena.HalfWidth + 0.35f));
            return Flat(r.spot) - aimDir * (r.tipo == TipoReinicio.Penalti ? 1.6f : 0.75f);
        }

        void ExecuteRestartAI(Reinicio r)
        {
            var tk = r.taker;
            r.executing = true;
            r.waitingHuman = false;
            tk.pc.ballAuthority = true;
            Vector3 goal = r.time.TargetGoal;
            switch (r.tipo)
            {
                case TipoReinicio.Penalti:
                    {
                        Whistle(1);
                        float z = (Random.value < 0.5f ? -1f : 1f) * Random.Range(1.2f, Arena.GoalHalfWidth - 0.5f);
                        if (!tk.pc.AIShoot(Random.value < 0.6f ? TipoChute.Colocado : TipoChute.Forte, Random.Range(0.6f, 0.85f), z)) r.executing = false;
                        else NotifyShot(tk);
                        break;
                    }
                case TipoReinicio.Lateral:
                    StartCoroutine(ThrowIn(r));
                    break;
                case TipoReinicio.Escanteio:
                    {
                        Vector3 box = goal - r.time.Forward * Random.Range(6f, 12f) + new Vector3(0f, 0f, Random.Range(-6f, 6f));
                        var target = ClosestTo(r.time, box, false, tk);
                        Vector3 tp = target != null ? Vector3.Lerp(box, target.Pos, 0.5f) : box;
                        if (!tk.pc.Pass(tp, true, (1f - tk.atr.passe) * 0.6f)) r.executing = false;
                        else NotifyPass(tk, target, tp);
                        break;
                    }
                case TipoReinicio.Falta:
                    {
                        if (!r.indirect && ShotQuality(r.spot, r.time) > 0.12f && Random.value < 0.55f + tk.men.criatividade * 0.3f)
                        {
                            Whistle(1);
                            float z = (Random.value < 0.5f ? -1f : 1f) * (Arena.GoalHalfWidth - 0.7f);
                            if (!tk.pc.AIShoot(TipoChute.Colocado, 0.75f, z)) r.executing = false;
                            else NotifyShot(tk);
                        }
                        else RestartPass(r, tk);
                        break;
                    }
                default:
                    RestartPass(r, tk);
                    break;
            }
        }

        void RestartPass(Reinicio r, MatchPlayer tk)
        {
            MatchPlayer mate; Vector3 target; bool lofted; float u;
            BestPass(tk, 0f, out mate, out target, out lofted, out u);
            if (r.tipo == TipoReinicio.Saida)
            {
                // saída de bola: toque para trás
                mate = ClosestTo(r.time, Flat(r.spot) - r.time.Forward * 9f, false, tk);
                target = mate != null ? mate.Pos : Flat(r.spot) - r.time.Forward * 8f;
                lofted = false;
                Whistle(1);
            }
            if (mate == null) target = Flat(r.spot) + r.time.Forward * 15f;
            if (!tk.pc.Pass(target, lofted, (1f - tk.atr.passe) * 0.4f)) { r.executing = false; return; }
            NotifyPass(tk, mate, target);
        }

        void ExecuteRestartHuman(bool lofted)
        {
            var r = Restart;
            if (r == null || r.taker == null) return;
            r.executing = true;
            r.waitingHuman = false;
            r.taker.pc.ballAuthority = true;
            if (r.tipo == TipoReinicio.Lateral) { StartCoroutine(ThrowIn(r)); return; }
            HumanPass(lofted);
            if (!r.taker.pc.PassPending) r.executing = false;
        }

        System.Collections.IEnumerator ThrowIn(Reinicio r)
        {
            var tk = r.taker;
            tk.pc.Play("Lateral", 0.1f);
            MatchPlayer mate = null;
            Vector3 target;
            if (tk == Human && tk.time.humano)
            {
                Vector2 mv = InputBridge.Move;
                Vector3 dir = mv.sqrMagnitude > 0.04f ? (Flat(mainCamera.transform.forward).normalized * mv.y + Flat(mainCamera.transform.right).normalized * mv.x).normalized : new Vector3(0f, 0f, -Mathf.Sign(r.spot.z));
                mate = BestInCone(tk, dir, 30f);
            }
            else
            {
                MatchPlayer bm; Vector3 bt; bool bl; float bu;
                BestPass(tk, 0f, out bm, out bt, out bl, out bu);
                mate = bm != null && Vector3.Distance(bm.Pos, tk.Pos) < 28f ? bm : ClosestTo(tk.time, r.spot + new Vector3(0f, 0f, -Mathf.Sign(r.spot.z) * 8f), false, tk);
            }
            target = mate != null ? mate.Pos + Flat(mate.Vel) * 0.6f : r.spot + new Vector3(0f, 0f, -Mathf.Sign(r.spot.z) * 10f);
            yield return new WaitForSeconds(0.55f);
            if (!Active || Restart != r) yield break;
            Vector3 from = BallPos;
            Vector3 d = Flat(target - from);
            float D = Mathf.Clamp(d.magnitude, 3f, 26f);
            float T = Mathf.Clamp(D / 13f, 0.5f, 1.6f);
            Vector3 v = d.normalized * (D / T) + Vector3.up * (0.5f * 9.81f * T - 1.2f);
            ball.Release();
            if (mate != null) NotifyPass(tk, mate, target);
            ball.Kick(v, Vector3.zero);
        }

        void ResumePlay()
        {
            Phase = Fase.Jogo;
            phaseTime = 0f;
            Restart = null;
        }

        // ============================================================ faltas, cartões e contusões

        void OnBodyContact(MatchPlayer self, PlayerController otherPc, Component other, Vector3 n, float closing)
        {
            if (!Active || Phase != Fase.Jogo || self.pc == null) return;
            MatchPlayer o = null;
            if (otherPc != null) byPc.TryGetValue(otherPc, out o);
            if (o == null) return;
            if (o.time == self.time) return; // companheiros só se afastam

            float share = o.pc.bodyMass / (self.pc.bodyMass + o.pc.bodyMass);
            float impact = closing * share * 2f;
            float balance = 2.2f + self.atr.equilibrio * 2.4f + self.atr.forca * 1.2f;
            var ws = weather != null ? weather.P : null;
            if (ws != null) balance *= Mathf.Lerp(1f, 0.82f, ws.wetness); // gramado molhado: mais quedas

            // carrinho que pega o jogador antes da bola
            bool slideFoul = o.pc.Sliding && !o.pc.SlideTouchedBall && Time.time - o.pc.SlideStartTime < 0.9f;
            // empurrão por trás com força
            Vector3 myFwd = Flat(self.transform.forward).normalized;
            bool fromBehind = Vector3.Dot(myFwd, n) > 0.45f;
            Vector3 oVel = Flat(o.Vel);
            bool aggressor = Vector3.Dot(oVel, -n) > Vector3.Dot(Flat(self.Vel), n) + 0.5f;

            bool fell = false;
            if (slideFoul || impact > balance * 1.55f)
            {
                bool injured = (slideFoul && Random.value < 0.18f + o.men.agressividade * 0.15f) || (impact > balance * 2.3f && Random.value < 0.25f);
                float lie = injured ? Random.Range(4f, 6.5f) : Random.Range(0.9f, 1.8f);
                self.pc.Fall(n * Mathf.Clamp(impact * 0.6f, 1.5f, 4.5f), lie, injured);
                fell = true;
                if (injured) Injure(self);
            }
            else if (impact > balance) self.pc.Stagger(n * impact * 0.5f, false);

            if (Owner == self && (fell || impact > balance)) Owner = null;

            bool foul = slideFoul || (fell && aggressor && (fromBehind || o.men.agressividade > 0.7f) && Random.value < 0.75f) ||
                        (impact > balance && aggressor && fromBehind && Random.value < 0.4f);
            if (foul)
            {
                float severity = slideFoul ? 0.45f + Flat(o.Vel).magnitude * 0.04f + (fromBehind ? 0.25f : 0f) : 0.25f + impact * 0.05f + (fromBehind ? 0.15f : 0f);
                severity += o.men.agressividade * 0.15f + Random.Range(-0.1f, 0.1f);
                Foul(o, self, Flat(self.Pos), severity);
            }
        }

        void OnSlideBall(MatchPlayer slider)
        {
            LastToucher = slider;
            slider.desarmes++;
            if (Owner != null && Owner.time != slider.time) Owner = null;
        }

        /// <summary>Bote em pé ou carrinho.</summary>
        public void TryTackle(MatchPlayer t, bool slide)
        {
            if (t.pc == null || t.pc.Busy) return;
            Vector3 b = BallPos;
            if (slide)
            {
                Vector3 aim = b + Flat(BallVel) * 0.25f - t.Pos;
                t.pc.StartSlide(Flat(aim));
                return;
            }
            var victim = Owner;
            Vector3 toBall = Flat(b - t.Pos);
            t.pc.Lunge(toBall);
            if (victim == null || victim.time == t.time) return;
            float d = Flat(b - t.pc.ControlPoint).magnitude;
            if (d > 1.5f) return;
            Vector3 vf = Flat(victim.transform.forward).normalized;
            bool facingAway = Vector3.Dot(vf, Flat(t.Pos - victim.Pos).normalized) < -0.3f;
            float p = 0.32f + 0.55f * (t.atr.desarme - victim.atr.drible * 0.7f) + (facingAway ? 0.12f : 0f) - d * 0.12f;
            if (victim.pc != null && victim.pc.Busy) p += 0.15f;
            p = Mathf.Clamp(p, 0.08f, 0.85f);
            if (Random.value < p)
            {
                // desarme limpo: a bola sobra para o lado de quem roubou
                Vector3 v = (toBall.normalized * -0.6f + Flat(t.transform.right) * Random.Range(-0.7f, 0.7f)).normalized * Random.Range(2.5f, 4.5f);
                ball.Deflect(v + Vector3.up * 0.3f, Vector3.zero);
                Owner = null;
                LastToucher = t;
                t.desarmes++;
                if (victim.pc != null && Random.value < 0.4f) victim.pc.Stagger(Flat(victim.Pos - t.Pos).normalized * 1.5f, false);
                return;
            }
            // errou o bote: pode derrubar (falta) ou ficar para trás
            float foulChance = 0.18f + t.men.agressividade * 0.32f + (facingAway ? 0.15f : 0f);
            if (Flat(victim.Pos - t.Pos).magnitude < 1.25f && Random.value < foulChance)
            {
                victim.pc.Fall(Flat(victim.Pos - t.Pos).normalized * 2.2f, Random.Range(0.9f, 1.6f), Random.value < 0.06f);
                if (Owner == victim) Owner = null;
                Foul(t, victim, Flat(victim.Pos), 0.3f + t.men.agressividade * 0.3f + (facingAway ? 0.2f : 0f) + Random.Range(-0.1f, 0.1f));
            }
            else t.pc.Stagger(Flat(t.transform.forward) * 0.8f, false);
        }

        void Foul(MatchPlayer offender, MatchPlayer victim, Vector3 at, float severity)
        {
            if (Phase != Fase.Jogo) return;
            // o árbitro precisa ver
            float seen = 1f;
            if (MainReferee != null)
            {
                float d = Vector3.Distance(MainReferee.transform.position, at);
                seen = Mathf.Clamp01(0.99f - Mathf.Max(0f, d - 22f) * 0.012f);
                foreach (var r in referees)
                    if (r != null && r.papel == RefereeAgent.Papel.Assistente && Vector3.Distance(r.transform.position, at) < 18f) seen = Mathf.Max(seen, 0.92f);
            }
            if (Random.value > seen)
            {
                if (victim.pc != null && victim.pc.aiControlled && Random.value < 0.6f) victim.pc.Play("Reclamar", 0.2f);
                return;
            }
            offender.time.faltas++;
            // cartões
            if (severity > 0.92f) GiveCard(offender, true);
            else if (severity > 0.6f) GiveCard(offender, false);

            // vantagem: o time que sofreu segue com a bola atacando
            bool advantage = Owner != null && Owner.time == victim.time && Owner != victim && victim.time.Fwd(Owner.Pos) > -15f;
            if (advantage)
            {
                Show("VANTAGEM", UiKit.Green, 1.4f);
                if (MainReferee != null) MainReferee.SignalAdvantage();
                return;
            }
            Whistle(1);
            var defending = offender.time; // o infrator defende o próprio gol
            bool inBox = InPenaltyArea(at, defending);
            if (inBox)
            {
                Show("PÊNALTI!", UiKit.Red, 2.2f);
                if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.CrowdReaction(0.8f);
                Vector3 spot = defending.OwnGoal + defending.Forward * 11f;
                StartRestart(TipoReinicio.Penalti, victim.time, spot, false, 2.5f);
            }
            else
            {
                if (severity <= 0.6f) Show("FALTA  •  " + offender.Rotulo, Color.white, 1.5f);
                PendingInjuryRestart(victim, TipoReinicio.Falta, at);
            }
        }

        void GiveCard(MatchPlayer p, bool red)
        {
            if (!red) { p.amarelos++; if (p.amarelos >= 2) red = true; p.time.amarelos++; }
            if (MainReferee != null) MainReferee.ShowCard(red);
            if (red)
            {
                p.time.vermelhos++;
                Show("CARTÃO VERMELHO  •  " + p.Rotulo, UiKit.Red, 2.6f);
                SendOff(p);
            }
            else Show("CARTÃO AMARELO  •  " + p.Rotulo, UiKit.Yellow, 2.2f);
        }

        void SendOff(MatchPlayer p)
        {
            p.expulso = true;
            if (Owner == p) Owner = null;
            if (p == Human) SetHuman(ClosestTo(p.time, BallPos, true));
            p.time.jogadores.Remove(p);
            StartCoroutine(WalkOff(p));
        }

        System.Collections.IEnumerator WalkOff(MatchPlayer p)
        {
            Vector3 exit = new Vector3(0f, 0f, -Arena.HalfWidth - 6f);
            float t0 = Time.time;
            while (p != null && p.pc != null && Time.time - t0 < 12f)
            {
                p.pc.aiControlled = true;
                p.pc.InputEnabled = false;
                Vector3 d = Flat(exit - p.Pos);
                p.pc.aiMoveWorld = d.magnitude > 1f ? d.normalized * 0.45f : Vector3.zero;
                p.pc.aiSprint = false;
                yield return null;
            }
            if (p != null) p.gameObject.SetActive(false);
        }

        void Injure(MatchPlayer p)
        {
            p.lesao = Mathf.Clamp01(p.lesao + Random.Range(0.25f, 0.5f));
            Show("CONTUSÃO  •  " + p.Rotulo, new Color(1f, 0.55f, 0.5f), 2.4f);
        }

        /// <summary>Se alguém ficou caído contundido, para o jogo para o atendimento antes de reiniciar.</summary>
        void PendingInjuryRestart(MatchPlayer victim, TipoReinicio tipo, Vector3 at)
        {
            var team = victim.time;
            if (victim.lesao > 0f && victim.pc != null && victim.pc.Fallen)
            {
                Phase = Fase.Parado;
                phaseTime = 0f;
                Show("ATENDIMENTO MÉDICO  •  " + victim.Rotulo, new Color(1f, 0.6f, 0.55f), 3f);
                resumeAt = Time.time + 4.5f;
                afterPause = () => StartRestart(tipo, team, at, false, 1.2f);
                return;
            }
            StartRestart(tipo, team, at, false, 1.3f);
        }

        // ============================================================ escorregões na chuva

        void UpdateSlips(float dt)
        {
            if (Phase != Fase.Jogo || weather == null || weather.P == null || weather.P.wetness < 0.4f) return;
            float wet = weather.P.wetness;
            foreach (var m in all)
            {
                if (m.pc == null || !m.Disponivel || m.pc.Sliding) continue;
                Vector3 v = Flat(m.pc.Velocity);
                Vector3 pv;
                prevVel.TryGetValue(m, out pv);
                prevVel[m] = v;
                float sp = v.magnitude;
                if (sp < 6.2f || pv.sqrMagnitude < 25f) continue;
                float turn = Vector3.Angle(pv, v) / Mathf.Max(dt, 1e-3f); // graus por segundo
                if (turn < 160f) continue;
                float chance = (turn - 160f) / 600f * wet * (1.15f - m.atr.equilibrio) * dt * 6f;
                if (Random.value < chance)
                {
                    m.pc.Fall(v.normalized * 1.5f, Random.Range(0.8f, 1.3f), false);
                    if (Owner == m) Owner = null;
                    if (m == Human || Vector3.Distance(m.Pos, BallPos) < 15f) Show("ESCORREGOU!  " + m.Rotulo, UiKit.Cyan, 1.1f);
                    if (PuddleField.Instance != null) PuddleField.Instance.FootStep(m.Pos, 1f);
                }
            }
        }

        // ============================================================ utilidades

        public TimeInfo OpponentOf(TimeInfo t) { return Times[0] == t ? Times[1] : Times[0]; }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void Whistle(int n)
        {
            if (MainReferee != null) MainReferee.Whistle(n);
            else if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.Whistle(n);
        }

        public bool InPenaltyArea(Vector3 p, TimeInfo defending)
        {
            float fromLine = Arena.HalfLength - Mathf.Abs(p.x);
            bool rightEnd = Mathf.Sign(p.x) == Mathf.Sign(defending.OwnGoal.x);
            return rightEnd && fromLine < 16.5f && Mathf.Abs(p.z) < 20.16f;
        }

        public MatchPlayer ClosestTo(TimeInfo t, Vector3 p, bool includeHuman, MatchPlayer except = null)
        {
            MatchPlayer best = null; float bd = float.MaxValue;
            foreach (var m in t.jogadores)
            {
                if (m == except || !m.Disponivel || m.pc == null) continue;
                if (!includeHuman && m == Human && m.time.humano) continue;
                float d = Flat(m.Pos - p).sqrMagnitude;
                if (d < bd) { bd = d; best = m; }
            }
            return best;
        }

        MatchPlayer BestBy(TimeInfo t, Func<MatchPlayer, float> score)
        {
            MatchPlayer best = null; float bs = float.MinValue;
            foreach (var m in t.jogadores)
            {
                if (!m.Disponivel || m.pc == null) continue;
                float s = score(m);
                if (s > bs) { bs = s; best = m; }
            }
            return best;
        }

        MatchPlayer BestInCone(MatchPlayer from, Vector3 dir, float maxDist)
        {
            MatchPlayer best = null; float bs = -9f;
            foreach (var m in from.time.jogadores)
            {
                if (m == from || !m.Disponivel) continue;
                Vector3 to = Flat(m.Pos - from.Pos);
                float d = to.magnitude;
                if (d < 2f || d > maxDist) continue;
                float s = Vector3.Dot(to / d, dir) * 2f - d / maxDist;
                if (s > bs) { bs = s; best = m; }
            }
            return best;
        }

        MatchPlayer NearestToBall(float maxDist, TimeInfo team)
        {
            MatchPlayer best = null; float bd = maxDist;
            Vector3 b = BallPos;
            foreach (var m in all)
            {
                if (m.pc == null || (team != null && m.time != team)) continue;
                float d = Flat(m.pc.ControlPoint - b).magnitude;
                if (d < bd) { bd = d; best = m; }
            }
            return best;
        }

        float KeeperOpponentDistance(TimeInfo keeperTeam, Vector3 p)
        {
            if (Owner != null && Owner.time == keeperTeam) return 0f; // não toma a bola do próprio zagueiro
            if (Phase != Fase.Jogo) return 0f;
            float bd = 99f;
            foreach (var m in OpponentOf(keeperTeam).jogadores)
            {
                if (!m.Disponivel) continue;
                float d = Flat(m.Pos - p).magnitude;
                if (d < bd) bd = d;
            }
            return bd;
        }

        Vector3? KeeperTarget(TimeInfo t)
        {
            MatchPlayer best = null; float bs = -9f;
            var keeper = t.goleiroInfo;
            foreach (var m in t.jogadores)
            {
                if (!m.Disponivel) continue;
                float d = Vector3.Distance(m.Pos, keeper.Pos);
                if (d < 8f || d > 55f) continue;
                float open = NearestOpponentDistance(t, m.Pos);
                float s = Mathf.Clamp01((open - 2f) / 8f) + (m.funcao == Funcao.Lateral || m.funcao == Funcao.Volante ? 0.15f : 0f) - d / 120f + Random.value * 0.2f;
                if (s > bs) { bs = s; best = m; }
            }
            if (best == null) return null;
            return best.Pos + Flat(best.Vel) * 0.8f;
        }

        public float NearestOpponentDistance(TimeInfo t, Vector3 p)
        {
            float bd = 99f;
            var opp = OpponentOf(t);
            foreach (var m in opp.jogadores)
            {
                if (!m.Disponivel) continue;
                float d = Flat(m.Pos - p).magnitude;
                if (d < bd) bd = d;
            }
            return bd;
        }

        public void RequestRestartMatch() { if (OnRestartRequested != null) OnRestartRequested(); }
        public void RequestExit() { if (OnExitRequested != null) OnExitRequested(); }
    }
}
