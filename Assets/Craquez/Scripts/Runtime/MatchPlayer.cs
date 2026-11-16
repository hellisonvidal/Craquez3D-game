using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Um jogador da partida 11 x 11: identidade (nome, número, função, rosto), atributos, mentalidade,
    /// cansaço, cartões e contusões — e o "cérebro" que decide o que fazer quando a IA o controla.
    ///
    /// Cada jogador pensa do seu jeito:
    /// - com a bola: compara finalizar, passar (curto, em profundidade, cruzamento), conduzir/driblar e afastar,
    ///   pesando visão, técnica, pressão do marcador, frieza e espírito de equipe (o fominha chuta mais, o solidário passa mais);
    /// - sem a bola no ataque: dá opção de passe, abre o campo, ataca o espaço nas costas da defesa sem ficar impedido;
    /// - sem a bola na defesa: pressiona o portador, fecha linhas de passe, marca por zona/individual e recompõe;
    /// - bola solta: o mais rápido de cada time vai nela, os outros se reposicionam.
    /// </summary>
    public class MatchPlayer : MonoBehaviour
    {
        public PlayerController pc;
        public GoalkeeperAI gk;
        public TimeInfo time;
        public int numero;
        public string nome;
        public Funcao funcao;
        public Slot slot;
        public Atributos atr;
        public Mentalidade men;
        public bool modeloB;

        [Range(0f, 1f)] public float stamina = 1f;
        [Range(0f, 1f)] public float lesao;   // contusão: deixa o jogador mais lento até o fim
        public int amarelos;
        public bool expulso;
        public int gols, passesCertos, passesErrados, desarmes;

        public bool IsKeeper { get { return gk != null; } }
        public bool Humano { get { return pc != null && pc.InputEnabled && !pc.aiControlled; } }
        public bool Disponivel { get { return !expulso && (pc == null || !pc.Incapacitated); } }
        public Vector3 Pos { get { return transform.position; } }
        public Vector3 Vel { get { return pc != null ? pc.Velocity : (gk != null ? gk.Velocity : Vector3.zero); } }
        public string Rotulo { get { return numero + " " + nome; } }

        // estado da decisão
        public string Acao { get; private set; }
        float nextDecision;
        Vector3 carryDir;
        float carryUntil;
        public float runUntil;
        public Vector3 runTarget;
        float tackleCooldown;
        float supportPick;
        Vector3 supportSpot;
        public float expectingPassUntil;
        [System.NonSerialized] public bool celebrating;
        public Vector3 expectingPassAt;

        // ------------------------------------------------------------ físico

        /// <summary>Aplica atributos, cansaço e contusão ao corpo (velocidade, aceleração, precisão, massa).</summary>
        public void ApplyPhysical()
        {
            if (pc == null) return;
            float fatigue = Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(stamina / 0.6f));
            pc.speedScale = (0.86f + 0.24f * atr.velocidade) * fatigue * (1f - lesao * 0.28f);
            pc.accelScale = (0.82f + 0.32f * atr.aceleracao) * Mathf.Lerp(0.85f, 1f, stamina);
            pc.accuracyScale = Mathf.Lerp(1.45f, 0.72f, atr.finalizacao) * (1f + (1f - stamina) * 0.35f) * (time != null ? time.habilidade : 1f);
            pc.bodyMass = 66f + atr.forca * 22f;
        }

        void UpdateStamina(float dt)
        {
            if (pc == null) return;
            float spd = pc.Velocity.magnitude;
            float drain;
            if (spd > pc.jogSpeed * pc.speedScale * 0.98f) drain = 0.011f;      // disparada
            else if (spd > 3.5f) drain = 0.0022f;                                // trote/corrida
            else drain = -0.010f;                                               // recupera andando/parado
            float k = drain > 0f ? Mathf.Lerp(1.35f, 0.65f, atr.resistencia) : Mathf.Lerp(0.8f, 1.3f, atr.resistencia);
            stamina = Mathf.Clamp01(stamina - drain * k * dt);
        }

        // ------------------------------------------------------------ ciclo

        public void Tick(MatchSession m, float dt)
        {
            if (pc == null || expulso) return;
            UpdateStamina(dt);
            ApplyPhysical();
            if (Humano) { Acao = "Você"; return; }
            if (pc.Incapacitated || pc.Sliding) { Stop(); return; }
            tackleCooldown -= dt;

            switch (m.Phase)
            {
                case MatchSession.Fase.Jogo: PlayTick(m, dt); break;
                case MatchSession.Fase.BolaParada: SetPieceTick(m); break;
                case MatchSession.Fase.Comemoracao: CelebrateTick(m); break;
                default: MoveTo(m.IdleSpot(this), 0.25f, false, m.BallPos); break;
            }
        }

        void Stop()
        {
            pc.aiMoveWorld = Vector3.zero;
            pc.aiSprint = false;
        }

        // ------------------------------------------------------------ jogo corrido

        void PlayTick(MatchSession m, float dt)
        {
            var owner = m.Owner;
            bool keeperHolds = m.KeeperHolding != null;
            if (owner == this) { OnBall(m, dt); return; }

            bool ourBall = (owner != null && owner.time == time) || (keeperHolds && m.KeeperHolding.time == time);
            bool theirBall = (owner != null && owner.time != time) || (keeperHolds && m.KeeperHolding.time != time);

            if (!ourBall && !theirBall)
            {
                // bola solta: o mais rápido do time vai nela, o segundo dá cobertura, os outros guardam posição
                if (m.Chaser(time) == this) { ChaseBall(m); return; }
                if (Time.time < expectingPassUntil) { MoveTo(expectingPassAt, 1f, true, m.BallPos); Acao = "Recebendo"; return; }
                bool wasOurs = m.LastPossession == time;
                if (wasOurs) OffBallAttack(m, dt); else OffBallDefend(m, dt, null);
                return;
            }
            if (ourBall) OffBallAttack(m, dt);
            else OffBallDefend(m, dt, owner);
        }

        void ChaseBall(MatchSession m)
        {
            Vector3 p = m.InterceptPoint(this);
            MoveTo(p, 1f, true, m.BallPos, 0.1f);
            Acao = "Na bola";
        }

        // ---------------------------------------------------- com a bola

        void OnBall(MatchSession m, float dt)
        {
            if (pc.Busy) return;
            float pressure = m.Pressure(this);
            if (Time.time < nextDecision && Time.time < carryUntil)
            {
                Carry(m, carryDir, pressure);
                return;
            }
            float think = Mathf.Lerp(0.55f, 0.22f, atr.visao) * (time != null ? Mathf.Lerp(1f, 1.35f, time.habilidade - 1f) : 1f);
            nextDecision = Time.time + think * Random.Range(0.85f, 1.15f);

            Vector3 pos = Pos;
            Vector3 goal = time.TargetGoal;
            float noise = 0.07f * (1.25f - men.compostura) * (1f + pressure);

            // 1) finalizar
            float xg = m.ShotQuality(pos, time);
            float dGoal = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), goal);
            float uShoot = xg * (0.75f + atr.finalizacao * 0.6f) * (1f + (0.55f - men.espiritoEquipe) * 0.6f);
            if (dGoal > 22f) uShoot *= 0.35f + men.criatividade * 0.9f;
            uShoot += Gauss() * noise;

            // 2) passes (curto, longo, profundidade, cruzamento)
            MatchPlayer bestMate; Vector3 bestTarget; bool bestLofted; float uPass;
            m.BestPass(this, pressure, out bestMate, out bestTarget, out bestLofted, out uPass);
            uPass += men.espiritoEquipe * 0.1f + Gauss() * noise;

            // 3) conduzir / driblar
            Vector3 dir; float space;
            float uCarry = m.BestCarry(this, out dir, out space);
            uCarry += atr.drible * 0.15f + men.criatividade * 0.08f - pressure * 0.3f * (1.1f - atr.drible) + Gauss() * noise;
            if (men.espiritoEquipe < 0.4f) uCarry += 0.05f;

            // 4) afastar o perigo (zagueiro pressionado na própria área)
            float fwd = time.Fwd(pos);
            float uClear = -1f;
            if (fwd < -30f && pressure > 0.55f) uClear = 0.35f + pressure * 0.4f - men.compostura * 0.3f;

            float best = Mathf.Max(Mathf.Max(uShoot, uPass), Mathf.Max(uCarry, uClear));
            float shootBar = 0.2f - men.ofensividade * 0.06f;
            if (best == uShoot && uShoot > shootBar && dGoal < 34f)
            {
                Shoot(m, dGoal);
                return;
            }
            if (best == uClear)
            {
                Vector3 t = pos + time.Forward * Random.Range(35f, 50f) + new Vector3(0f, 0f, Random.Range(-18f, 18f));
                t.z = Mathf.Clamp(t.z, -Arena.HalfWidth + 2f, Arena.HalfWidth - 2f);
                if (pc.Pass(t, true, 0.55f)) { Acao = "Afastou"; m.NotifyPass(this, null, t); }
                return;
            }
            if (best == uPass && bestMate != null)
            {
                float err = (1f - atr.passe) * 0.75f + pressure * 0.35f * (1f - men.compostura);
                err *= time != null ? time.habilidade : 1f;
                if (pc.Pass(bestTarget, bestLofted, Mathf.Clamp01(err)))
                {
                    Acao = bestLofted ? "Lançou" : "Tocou";
                    m.NotifyPass(this, bestMate, bestTarget);
                }
                return;
            }
            carryDir = dir;
            carryUntil = Time.time + Random.Range(0.35f, 0.8f);
            Carry(m, dir, pressure);
        }

        void Carry(MatchSession m, Vector3 dir, float pressure)
        {
            bool sprint = stamina > 0.25f && pressure < 0.75f && m.SpaceAhead(this, dir) > 7f;
            pc.aiMoveWorld = dir;
            pc.aiSprint = sprint;
            pc.aiFace = false;
            Acao = pressure > 0.5f ? "Protegendo" : (sprint ? "Arrancando" : "Conduzindo");
            // drible curto quando o marcador chega perto
            if (pressure > 0.7f && atr.drible > 0.55f && Random.value < Time.deltaTime * (1f + men.criatividade * 2f))
                m.AIDribbleMove(this);
        }

        void Shoot(MatchSession m, float dGoal)
        {
            // canto mais longe do goleiro (com pontaria de acordo com a técnica)
            float gkz = m.OpponentKeeperZ(time);
            float side = gkz > 0.2f ? -1f : (gkz < -0.2f ? 1f : (Random.value < 0.5f ? -1f : 1f));
            float corner = Arena.GoalHalfWidth - Mathf.Lerp(1.2f, 0.45f, atr.finalizacao);
            float z = side * corner;
            TipoChute t;
            if (dGoal < 13f && men.criatividade > 0.5f && Random.value < 0.4f) t = TipoChute.Colocado;
            else if (dGoal < 11f && Random.value < 0.3f) t = TipoChute.Rasteiro;
            else if (dGoal > 18f && men.criatividade > 0.65f && Random.value < 0.5f) t = TipoChute.Colocado;
            else t = TipoChute.Forte;
            float power = Mathf.Clamp(0.45f + dGoal / 40f + Random.Range(-0.08f, 0.1f), 0.4f, 0.92f);
            if (pc.AIShoot(t, power, z))
            {
                Acao = "Finalizou";
                m.NotifyShot(this);
            }
        }

        // ---------------------------------------------------- sem a bola: atacando

        void OffBallAttack(MatchSession m, float dt)
        {
            Vector3 target = m.FormationTarget(this, true);
            var owner = m.Owner;
            float offside = m.OffsideLine(time);
            bool running = Time.time < runUntil;
            bool urgent = false;

            if (owner != null && owner != this && !IsKeeper)
            {
                float dOwner = Vector3.Distance(Pos, owner.Pos);
                // infiltração: atacantes e jogadores ofensivos atacam o espaço quando o portador olha para frente
                bool attacker = funcao == Funcao.Atacante || funcao == Funcao.Ponta || men.ofensividade > 0.7f;
                if (!running && attacker && time.Fwd(owner.Pos) > -10f && dOwner < 40f &&
                    Random.value < dt * (0.18f + men.ofensividade * 0.35f) * (1f + time.verticalidade))
                {
                    float lane = Mathf.Clamp(Pos.z + Random.Range(-8f, 8f), -Arena.HalfWidth + 6f, Arena.HalfWidth - 6f);
                    float depth = Mathf.Min(offside + Random.Range(7f, 14f), Arena.HalfLength - 8f);
                    runTarget = new Vector3(depth * time.attackDir, 0f, lane);
                    runUntil = Time.time + Random.Range(2f, 3.2f);
                    running = true;
                }
                // apoio: os mais solidários e próximos oferecem linha de passe
                if (!running && m.IsSupporter(this))
                {
                    if (Time.time > supportPick)
                    {
                        supportSpot = m.SupportSpot(this, owner);
                        supportPick = Time.time + Random.Range(0.8f, 1.4f);
                    }
                    target = Vector3.Lerp(target, supportSpot, Mathf.Lerp(0.55f, 0.9f, men.espiritoEquipe));
                    Acao = "Dando opção";
                }
            }
            if (running)
            {
                target = runTarget;
                urgent = true;
                Acao = "Infiltrando";
                // continua até o passe (ou desiste se ficou impedido sem receber)
                if (time.Fwd(Pos) > offside + 0.3f && m.Owner != null && m.Owner.time == time && Time.time - runUntil > -1.2f)
                    runUntil = 0f;
            }
            else
            {
                // segura a linha do impedimento
                float tf = time.Fwd(target);
                if (tf > offside - 0.8f) target = new Vector3((offside - 0.8f) * time.attackDir, 0f, target.z);
                if (string.IsNullOrEmpty(Acao) || Acao == "Infiltrando") Acao = "Posicionando";
            }
            MoveTo(target, urgent ? 1f : 0.65f, urgent && stamina > 0.3f, m.BallPos);
        }

        // ---------------------------------------------------- sem a bola: defendendo

        void OffBallDefend(MatchSession m, float dt, MatchPlayer owner)
        {
            Vector3 target = m.FormationTarget(this, false);
            Vector3 ball = m.BallPos;
            bool sprint = false;
            float urgency = 0.7f;

            int pressRank = m.PresserRank(this);
            if (owner != null && pressRank >= 0)
            {
                Vector3 op = owner.Pos;
                Vector3 toGoal = (time.OwnGoal - op); toGoal.y = 0f; toGoal.Normalize();
                if (pressRank == 0)
                {
                    // primeiro homem: fica entre o portador e o gol, encosta e dá o bote
                    float d = Vector3.Distance(Pos, op);
                    target = op + toGoal * Mathf.Lerp(1.1f, 0.6f, men.agressividade) + owner.Vel * 0.25f;
                    sprint = d > 3.5f && stamina > 0.2f;
                    urgency = 1f;
                    Acao = "Pressionando";
                    if (d < 1.7f && tackleCooldown <= 0f)
                    {
                        float want = (0.7f + men.agressividade * 1.4f) * (0.5f + atr.desarme) * dt * 2f;
                        if (owner.pc != null && owner.pc.Busy) want *= 1.8f;
                        if (Random.value < want)
                        {
                            bool slide = d > 1.1f && men.agressividade > 0.6f && Random.value < 0.22f;
                            m.TryTackle(this, slide);
                            tackleCooldown = Random.Range(1.1f, 2f);
                        }
                    }
                }
                else
                {
                    // segundo homem: fecha a linha de passe mais perigosa
                    Vector3 lane = m.DangerousLane(this, owner);
                    target = Vector3.Lerp(target, lane, 0.75f);
                    urgency = 0.9f;
                    sprint = Vector3.Distance(Pos, target) > 8f && stamina > 0.35f;
                    Acao = "Cobertura";
                }
            }
            else
            {
                var mark = m.MarkingTarget(this);
                if (mark != null)
                {
                    Vector3 op = mark.Pos;
                    Vector3 goalSide = (time.OwnGoal - op); goalSide.y = 0f; goalSide.Normalize();
                    Vector3 markSpot = op + goalSide * 1.6f + (ball - op).normalized * 0.8f;
                    target = Vector3.Lerp(markSpot, target, men.disciplina * 0.55f);
                    Acao = "Marcando " + mark.numero;
                }
                else Acao = "Recompondo";
                // recomposição: se a bola passou dele, corre de volta (quanto mais espírito de equipe, mais rápido)
                if (time.Fwd(Pos) > time.Fwd(ball) + 4f)
                {
                    sprint = stamina > Mathf.Lerp(0.6f, 0.2f, men.espiritoEquipe);
                    urgency = 1f;
                }
            }
            MoveTo(target, urgency, sprint, ball);
        }

        // ------------------------------------------------------------ bola parada e comemoração

        void SetPieceTick(MatchSession m)
        {
            Vector3 t = m.RestartSpot(this);
            bool taker = m.Restart.taker == this;
            MoveTo(t, taker ? 0.8f : 0.6f, Vector3.Distance(Pos, t) > 15f, taker ? m.Restart.aim : m.BallPos, 0.25f);
            Acao = taker ? "Cobrador" : "Posicionando";
        }

        void CelebrateTick(MatchSession m)
        {
            var scorer = m.Scorer;
            if (scorer == null) { Stop(); return; }
            if (scorer == this)
            {
                if (celebrating) { Stop(); return; }
                MoveTo(m.CelebrationSpot, 1f, true, m.CelebrationSpot + time.Forward * 5f, 1.2f);
                Acao = "Comemorando";
                return;
            }
            if (scorer.time == time)
            {
                // espírito de equipe: quem é solidário corre para abraçar o artilheiro
                bool joins = men.espiritoEquipe > 0.38f && Vector3.Distance(Pos, scorer.Pos) < 45f;
                if (joins)
                {
                    Vector3 around = scorer.Pos + (Pos - scorer.Pos).normalized * 1.3f;
                    MoveTo(around, 1f, true, scorer.Pos, 0.9f);
                    Acao = "Abraçando";
                    return;
                }
            }
            MoveTo(m.IdleSpot(this), 0.3f, false, m.BallPos);
        }

        // ------------------------------------------------------------ movimento

        /// <summary>Anda/corre até o alvo, desviando dos companheiros, chegando devagar e encarando um ponto.</summary>
        public void MoveTo(Vector3 target, float urgency, bool sprint, Vector3 face, float arriveRadius = 0.45f)
        {
            if (pc == null) return;
            Vector3 pos = Pos;
            target.y = 0f;
            target.x = Mathf.Clamp(target.x, -Arena.HalfLength - 3f, Arena.HalfLength + 3f);
            target.z = Mathf.Clamp(target.z, -Arena.HalfWidth - 3f, Arena.HalfWidth + 3f);
            Vector3 d = target - pos; d.y = 0f;
            float dist = d.magnitude;

            // separação: não amontoar com os companheiros
            Vector3 sep = Vector3.zero;
            foreach (var o in time.jogadores)
            {
                if (o == this || o.expulso) continue;
                Vector3 r = pos - o.Pos; r.y = 0f;
                float rd = r.magnitude;
                if (rd < 3.2f && rd > 0.01f) sep += r / rd * (3.2f - rd) / 3.2f;
            }

            Vector3 move = Vector3.zero;
            if (dist > arriveRadius)
            {
                float speed01 = Mathf.Clamp01(dist / 3f);
                move = d / dist * speed01 * Mathf.Lerp(0.5f, 1f, urgency);
            }
            move += sep * 0.6f;
            pc.aiMoveWorld = Vector3.ClampMagnitude(move, 1f);
            pc.aiSprint = sprint && dist > 2f && stamina > 0.12f;
            pc.aiFace = true;
            pc.aiFacePoint = face;
        }

        static float Gauss()
        {
            float u1 = Mathf.Max(1e-6f, Random.value), u2 = Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }
    }
}
