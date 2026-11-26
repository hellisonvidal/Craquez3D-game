using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Leitura tática da partida, usada pelos jogadores da IA: formação que acompanha a bola, linha de impedimento,
    /// quem vai na bola, quem pressiona, quem marca quem, qualidade de chute e de passe, espaço para conduzir
    /// e o posicionamento nas bolas paradas.
    /// </summary>
    public partial class MatchSession
    {
        readonly MatchPlayer[] chasers = new MatchPlayer[2];
        readonly MatchPlayer[,] pressers = new MatchPlayer[2, 2];
        readonly MatchPlayer[,] supporters = new MatchPlayer[2, 2];
        readonly Dictionary<MatchPlayer, MatchPlayer> marks = new Dictionary<MatchPlayer, MatchPlayer>();
        float markRefresh;

        // ============================================================ caches por quadro

        void UpdateTacticalCaches(float dt)
        {
            if (Times == null) return;
            for (int i = 0; i < 2; i++)
            {
                var t = Times[i];
                // quem vai na bola solta (o humano conta se for o mais rápido)
                MatchPlayer best = null; float bt = float.MaxValue; float humanT = float.MaxValue;
                foreach (var m in t.jogadores)
                {
                    if (!m.Disponivel || m.pc == null) continue;
                    float tt;
                    InterceptFor(m, out tt);
                    if (m == Human) { humanT = tt; continue; }
                    if (tt < bt) { bt = tt; best = m; }
                }
                chasers[i] = humanT < bt * 0.95f ? null : best;

                // pressão no portador
                pressers[i, 0] = pressers[i, 1] = null;
                var owner = Owner;
                if (owner != null && owner.time != t)
                {
                    MatchPlayer p0 = null, p1 = null; float d0 = float.MaxValue, d1 = float.MaxValue; float dh = float.MaxValue;
                    foreach (var m in t.jogadores)
                    {
                        if (!m.Disponivel || m.pc == null) continue;
                        float d = Vector3.Distance(m.Pos, owner.Pos) / Mathf.Max(0.5f, m.pc.speedScale);
                        if (m == Human) { dh = d; continue; }
                        if (d < d0) { p1 = p0; d1 = d0; p0 = m; d0 = d; }
                        else if (d < d1) { p1 = m; d1 = d; }
                    }
                    bool humanPresses = dh < d0;
                    bool dangerous = t.Fwd(owner.Pos) < -20f;
                    bool second = t.pressao > 0.5f || dangerous;
                    if (humanPresses) { pressers[i, 0] = null; pressers[i, 1] = second ? p0 : null; }
                    else { pressers[i, 0] = p0; pressers[i, 1] = second ? p1 : null; }
                }

                // apoio ao portador do próprio time
                supporters[i, 0] = supporters[i, 1] = null;
                if (owner != null && owner.time == t)
                {
                    MatchPlayer s0 = null, s1 = null; float a0 = float.MaxValue, a1 = float.MaxValue;
                    foreach (var m in t.jogadores)
                    {
                        if (m == owner || !m.Disponivel || m.pc == null) continue;
                        float d = Vector3.Distance(m.Pos, owner.Pos) * Mathf.Lerp(1.3f, 0.75f, m.men.espiritoEquipe);
                        if (d < a0) { s1 = s0; a1 = a0; s0 = m; a0 = d; }
                        else if (d < a1) { s1 = m; a1 = d; }
                    }
                    supporters[i, 0] = s0; supporters[i, 1] = s1;
                }
            }

            markRefresh -= dt;
            if (markRefresh <= 0f) { markRefresh = 0.5f; RefreshMarking(); }
        }

        void RefreshMarking()
        {
            marks.Clear();
            for (int i = 0; i < 2; i++)
            {
                var def = Times[i];
                var att = OpponentOf(def);
                var free = new List<MatchPlayer>();
                foreach (var m in def.jogadores) if (m.Disponivel && m.pc != null && PresserRank(m) < 0) free.Add(m);
                // atacantes mais perigosos (mais perto do nosso gol) primeiro
                var threats = new List<MatchPlayer>(att.jogadores);
                threats.RemoveAll(x => !x.Disponivel || x == Owner);
                threats.Sort((a, b) => def.Fwd(a.Pos).CompareTo(def.Fwd(b.Pos)));
                foreach (var th in threats)
                {
                    if (def.Fwd(th.Pos) > 10f) continue; // longe do nosso gol: a zona cuida
                    MatchPlayer best = null; float bd = 15f;
                    foreach (var d in free)
                    {
                        float dist = Vector3.Distance(FormationTarget(d, false), th.Pos);
                        if (dist < bd) { bd = dist; best = d; }
                    }
                    if (best != null) { marks[best] = th; free.Remove(best); }
                }
            }
        }

        public MatchPlayer Chaser(TimeInfo t) { return chasers[t.indice]; }

        public int PresserRank(MatchPlayer m)
        {
            int i = m.time.indice;
            if (pressers[i, 0] == m) return 0;
            if (pressers[i, 1] == m) return 1;
            return -1;
        }

        public bool IsSupporter(MatchPlayer m)
        {
            int i = m.time.indice;
            return supporters[i, 0] == m || supporters[i, 1] == m;
        }

        public MatchPlayer MarkingTarget(MatchPlayer m)
        {
            MatchPlayer t;
            return marks.TryGetValue(m, out t) && t != null && t.Disponivel ? t : null;
        }

        // ============================================================ bola

        /// <summary>Posição prevista da bola daqui a t segundos (rolando com atrito ou voando).</summary>
        public Vector3 PredictBall(float t)
        {
            Vector3 p = BallPos, v = BallVel;
            if (ball.IsHeld) return Flat(p);
            if (p.y > 0.4f || v.y > 1f)
            {
                Vector3 q = p + v * t + 0.5f * Physics.gravity * t * t;
                if (q.y < Arena.BallRadius)
                {
                    // depois do quique segue rolando mais devagar
                    float tl = (v.y + Mathf.Sqrt(Mathf.Max(0f, v.y * v.y + 2f * 9.81f * (p.y - Arena.BallRadius)))) / 9.81f;
                    Vector3 land = p + Flat(v) * tl;
                    float rest = t - tl;
                    q = land + Flat(v).normalized * Flat(v).magnitude * 0.7f * rest;
                }
                return Flat(q);
            }
            Vector3 fv = Flat(v);
            float s = fv.magnitude;
            if (s < 0.05f) return Flat(p);
            float dec = (weather != null ? weather.RollingDeceleration : 1.1f) * 1.6f;
            float ts = Mathf.Min(t, s / dec);
            return Flat(p) + fv / s * (s * ts - 0.5f * dec * ts * ts);
        }

        Vector3 InterceptFor(MatchPlayer m, out float time)
        {
            float spd = Mathf.Max(3f, m.pc.sprintSpeed * m.pc.speedScale * 0.92f);
            Vector3 pos = Flat(m.Pos);
            Vector3 q = Flat(BallPos);
            for (float t = 0f; t <= 3f; t += 0.1f)
            {
                q = PredictBall(t);
                float reach = spd * Mathf.Max(0f, t - 0.2f) + 0.7f;
                if (Vector3.Distance(pos, q) <= reach) { time = t; return q; }
            }
            time = 3f + Vector3.Distance(pos, q) / spd;
            return q;
        }

        public Vector3 InterceptPoint(MatchPlayer m)
        {
            float t;
            return InterceptFor(m, out t);
        }

        public float Pressure(MatchPlayer m)
        {
            float d = NearestOpponentDistance(m.time, m.Pos);
            return Mathf.Clamp01(1f - (d - 1f) / 5f);
        }

        public float OpponentKeeperZ(TimeInfo t)
        {
            var gk = OpponentOf(t).goleiro;
            return gk != null ? gk.transform.position.z : 0f;
        }

        // ============================================================ formação

        /// <summary>Posição desejada do jogador na formação, deslocada com a bola, a fase (ataque/defesa) e o estilo do time.</summary>
        public Vector3 FormationTarget(MatchPlayer m, bool attacking)
        {
            return FormationTargetFor(m, attacking, BallPos);
        }

        Vector3 FormationTargetFor(MatchPlayer m, bool attacking, Vector3 ballPos)
        {
            var t = m.time;
            var s = m.slot;
            float ballF = t.Fwd(ballPos);
            float ad = t.attackDir;
            float half = Arena.HalfLength;

            float shift = attacking ? Mathf.Lerp(0.5f, 0.72f, t.verticalidade) : 0.55f;
            float f = s.x * half * (attacking ? 1f : 0.82f) + ballF * shift;
            if (!attacking) f = Mathf.Lerp(f, ballF - 8f, 0.12f);                    // bloco compacto atrás da bola
            // personalidade: os ofensivos sobem mais, os disciplinados ficam no lugar
            f += (m.men.ofensividade - 0.5f) * (attacking ? 9f : 3f) * (1f - m.men.disciplina * 0.5f);

            bool defender = m.funcao == Funcao.Zagueiro || m.funcao == Funcao.Lateral;
            if (defender)
            {
                float maxLine = attacking ? Mathf.Lerp(-5f, 12f, t.linhaDefensiva) : Mathf.Lerp(-34f, -12f, t.linhaDefensiva) + Mathf.Max(0f, ballF) * 0.35f;
                if (m.funcao == Funcao.Lateral && attacking) maxLine += 14f * m.men.ofensividade;
                f = Mathf.Min(f, maxLine);
                f = Mathf.Min(f, ballF - (attacking ? 4f : 2f));
            }
            if (m.funcao == Funcao.Atacante || m.funcao == Funcao.Ponta)
            {
                if (!attacking) f = Mathf.Max(f, Mathf.Lerp(-12f, 2f, m.men.ofensividade)); // fica para o contra-ataque
                else f = Mathf.Min(f, OffsideLine(t) - 1f);
            }
            f = Mathf.Clamp(f, -half + 6f, half - 4f);

            float width = attacking ? Mathf.Lerp(0.62f, 0.95f, t.largura) : 0.58f;
            float zTeam = s.z * width * Arena.HalfWidth;          // coordenadas do time (+ = direita de quem ataca)
            float worldZ = -zTeam * ad + ballPos.z * (attacking ? 0.22f : 0.42f);
            worldZ = Mathf.Clamp(worldZ, -Arena.HalfWidth + 2.5f, Arena.HalfWidth - 2.5f);
            return new Vector3(f * ad, 0f, worldZ);
        }

        /// <summary>Linha de impedimento do time que ataca (em coordenadas de avanço): penúltimo defensor ou a bola, nunca antes do meio.</summary>
        public float OffsideLine(TimeInfo attacking)
        {
            return Mathf.Max(OffsideLineStrict(attacking), Mathf.Max(attacking.Fwd(BallPos), 0f));
        }

        public float OffsideLineStrict(TimeInfo attacking)
        {
            var def = OpponentOf(attacking);
            float first = -999f, second = -999f;
            void Consider(Vector3 p)
            {
                float f = attacking.Fwd(p);
                if (f > first) { second = first; first = f; }
                else if (f > second) second = f;
            }
            foreach (var m in def.jogadores) if (!m.expulso) Consider(m.Pos);
            if (def.goleiro != null) Consider(def.goleiro.transform.position);
            return second;
        }

        // ============================================================ decisões com a bola

        /// <summary>Qualidade da finalização de um ponto (ângulo do gol, distância e quem está no caminho).</summary>
        public float ShotQuality(Vector3 pos, TimeInfo t)
        {
            Vector3 goal = t.TargetGoal;
            Vector3 p = Flat(pos);
            float D = Vector3.Distance(p, goal);
            if (D > 38f) return 0f;
            Vector3 a = goal + new Vector3(0f, 0f, Arena.GoalHalfWidth), b = goal - new Vector3(0f, 0f, Arena.GoalHalfWidth);
            float ang = Vector3.Angle(a - p, b - p);
            float q = Mathf.Clamp01(ang / 38f) * Mathf.Clamp01(1.32f - D / 25f);
            foreach (var o in OpponentOf(t).jogadores)
            {
                if (!o.Disponivel) continue;
                Vector3 op = Flat(o.Pos);
                if (Vector3.Distance(op, goal) > D) continue;
                float dl = DistToSegment(op, p, goal);
                if (dl < 1.1f) q *= 0.6f + dl * 0.2f;
            }
            return q;
        }

        /// <summary>Melhor passe disponível: segurança da linha, companheiro livre, progressão e chance de gol de quem recebe.</summary>
        public void BestPass(MatchPlayer from, float pressure, out MatchPlayer mate, out Vector3 target, out bool lofted, out float utility)
        {
            mate = null; target = from.Pos; lofted = false; utility = -1f;
            var t = from.time;
            Vector3 me = Flat(from.Pos);
            float maxRange = 18f + from.atr.visao * 34f;
            float line = OffsideLine(t);
            float myF = t.Fwd(me);
            foreach (var m in t.jogadores)
            {
                if (m == from || !m.Disponivel || m.pc == null) continue;
                Vector3 mp = Flat(m.Pos);
                float d0 = Vector3.Distance(me, mp);
                Vector3 recv = mp + Flat(m.Vel) * Mathf.Clamp(d0 / 16f, 0.2f, 1.1f) * 0.9f;
                if (Time.time < m.runUntil) recv = Vector3.Lerp(recv, m.runTarget, 0.35f);
                recv.x = Mathf.Clamp(recv.x, -Arena.HalfLength + 1.5f, Arena.HalfLength - 1.5f);
                recv.z = Mathf.Clamp(recv.z, -Arena.HalfWidth + 1.5f, Arena.HalfWidth - 1.5f);
                float d = Vector3.Distance(me, recv);
                if (d < 4f || d > maxRange) continue;
                // jogador com visão enxerga quem está impedido
                float mf = t.Fwd(mp);
                if (mf > line + 0.3f && mf > 0f && Random.value < 0.55f + from.atr.visao * 0.45f) continue;

                float progress = Mathf.Clamp((t.Fwd(recv) - myF) / 25f, -1f, 1f);
                float open = Mathf.Clamp01((NearestOpponentDistance(t, recv) - 1.5f) / 7f);
                float xgR = ShotQuality(recv, t);
                for (int k = 0; k < 2; k++)
                {
                    bool air = k == 1;
                    if (air && d < 14f) continue;
                    float risk = LaneRisk(t, me, recv, air);
                    float u = (1f - risk) * 0.6f + open * 0.25f + progress * (0.22f + from.men.ofensividade * 0.3f) + xgR * 0.6f - d / 95f;
                    if (air) u -= 0.07f + (1f - from.atr.passe) * 0.08f;
                    if (progress < 0f) u += pressure * 0.15f - 0.06f;
                    if (air && myF > 28f && Mathf.Abs(me.z) > 15f && t.Fwd(recv) > 38f && Mathf.Abs(recv.z) < 12f) u += 0.12f; // cruzamento
                    if (u > utility) { utility = u; mate = m; target = recv; lofted = air; }
                }
            }
        }

        /// <summary>Risco de um adversário cortar o passe (rasteiro: ao longo da linha; alto: na saída e na chegada).</summary>
        float LaneRisk(TimeInfo t, Vector3 from, Vector3 to, bool air)
        {
            float D = Vector3.Distance(from, to);
            float vAvg = air ? 16f : 13f;
            float risk = 0f;
            foreach (var o in OpponentOf(t).jogadores)
            {
                if (!o.Disponivel) continue;
                Vector3 op = Flat(o.Pos);
                float along;
                float dl = DistToSegment(op, from, to, out along);
                if (air && along > 0.18f && along < 0.82f) continue;
                float tb = along * D / vAvg;
                float margin = (dl - 0.7f) - 6.5f * tb * 0.5f;
                risk = Mathf.Max(risk, Mathf.Clamp01(1f - margin / 1.6f) * (air ? 0.7f : 1f));
            }
            var gk = OpponentOf(t).goleiro;
            if (gk != null)
            {
                float along;
                float dl = DistToSegment(Flat(gk.transform.position), from, to, out along);
                if (dl < 2.5f && along > 0.6f) risk = Mathf.Max(risk, 0.85f);
            }
            return risk;
        }

        /// <summary>Melhor direção para conduzir (espaço à frente e progressão para o gol), ou proteger a bola sob pressão.</summary>
        public float BestCarry(MatchPlayer m, out Vector3 dir, out float space)
        {
            var t = m.time;
            Vector3 fwd = t.Forward;
            Vector3 me = Flat(m.Pos);
            float bestU = -9f;
            dir = fwd; space = 0f;
            float[] angles = { 0f, 28f, -28f, 55f, -55f, 95f, -95f };
            foreach (float a in angles)
            {
                Vector3 d = Quaternion.Euler(0f, a, 0f) * fwd;
                // aponta para o gol quando já está perto
                if (t.Fwd(me) > 25f) d = Vector3.Slerp(d, (t.TargetGoal - me).normalized, 0.5f).normalized;
                float sp = SpaceAhead(m, d);
                Vector3 next = me + d * 6f;
                float edge = Mathf.Abs(next.z) > Arena.HalfWidth - 2f || Mathf.Abs(next.x) > Arena.HalfLength - 1f ? 0.6f : 0f;
                float u = Mathf.Clamp01(sp / 14f) * 0.5f + Vector3.Dot(d, fwd) * (0.18f + m.men.ofensividade * 0.2f) - edge;
                if (u > bestU) { bestU = u; dir = d; space = sp; }
            }
            // sob pressão forte e sem espaço: protege a bola de costas para o marcador
            if (space < 2.5f)
            {
                MatchPlayer near = null; float nd = 99f;
                foreach (var o in OpponentOf(t).jogadores) { if (!o.Disponivel) continue; float d = Vector3.Distance(o.Pos, me); if (d < nd) { nd = d; near = o; } }
                if (near != null) { dir = Flat(me - near.Pos).normalized; bestU = 0.2f + (1f - m.atr.drible) * 0.2f; }
            }
            return bestU;
        }

        public float SpaceAhead(MatchPlayer m, Vector3 dir)
        {
            Vector3 me = Flat(m.Pos);
            float best = 15f;
            foreach (var o in OpponentOf(m.time).jogadores)
            {
                if (!o.Disponivel) continue;
                Vector3 r = Flat(o.Pos) - me;
                float along = Vector3.Dot(r, dir);
                if (along < -0.5f || along > 15f) continue;
                float lateral = (r - dir * along).magnitude;
                if (lateral < 1.5f + along * 0.35f) best = Mathf.Min(best, Mathf.Max(0f, along - 0.5f));
            }
            return best;
        }

        public void AIDribbleMove(MatchPlayer m)
        {
            // corte para o lado com mais espaço
            Vector3 fwd = Flat(m.transform.forward).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            Vector3 l = (fwd * 0.4f + side).normalized, r = (fwd * 0.4f - side).normalized;
            Vector3 d = SpaceAhead(m, l) > SpaceAhead(m, r) ? l : r;
            m.pc.AISkill(d);
        }

        // ============================================================ sem a bola

        public Vector3 SupportSpot(MatchPlayer m, MatchPlayer owner)
        {
            var t = m.time;
            Vector3 o = Flat(owner.Pos);
            Vector3 fwd = t.Forward;
            Vector3 formation = FormationTarget(m, true);
            Vector3 best = formation; float bs = -9f;
            float[] angles = { 35f, -35f, 75f, -75f, 120f, -120f, 0f };
            foreach (float a in angles)
            {
                for (int k = 0; k < 2; k++)
                {
                    float r = k == 0 ? 10f : 15f;
                    Vector3 p = o + Quaternion.Euler(0f, a, 0f) * fwd * r;
                    if (Mathf.Abs(p.z) > Arena.HalfWidth - 2f || Mathf.Abs(p.x) > Arena.HalfLength - 2f) continue;
                    float open = Mathf.Clamp01((NearestOpponentDistance(t, p) - 1.5f) / 7f);
                    float progress = Vector3.Dot(p - o, fwd) / 15f;
                    float lane = LaneRisk(t, o, p, false);
                    float far = Vector3.Distance(p, formation) / 30f;
                    float onside = t.Fwd(p) > OffsideLine(t) - 0.5f ? 1f : 0f;
                    float s = open * 0.5f + progress * (0.15f + m.men.ofensividade * 0.25f) - lane * 0.45f - far * m.men.disciplina - onside;
                    if (s > bs) { bs = s; best = p; }
                }
            }
            return best;
        }

        /// <summary>Ponto que fecha a linha de passe mais perigosa a partir do portador.</summary>
        public Vector3 DangerousLane(MatchPlayer m, MatchPlayer owner)
        {
            var att = owner.time;
            MatchPlayer danger = null; float bs = -99f;
            foreach (var a in att.jogadores)
            {
                if (a == owner || !a.Disponivel) continue;
                float s = att.Fwd(a.Pos) * 0.05f + Mathf.Clamp01((NearestOpponentDistance(att, a.Pos) - 2f) / 8f) - Vector3.Distance(a.Pos, owner.Pos) / 60f;
                if (s > bs) { bs = s; danger = a; }
            }
            if (danger == null) return FormationTarget(m, false);
            return Vector3.Lerp(Flat(owner.Pos), Flat(danger.Pos), 0.45f);
        }

        public Vector3 IdleSpot(MatchPlayer m)
        {
            if (m.expulso) return new Vector3(0f, 0f, -Arena.HalfWidth - 6f);
            return FormationTargetFor(m, false, Vector3.zero);
        }

        // ============================================================ bolas paradas

        /// <summary>Onde cada jogador fica na bola parada atual.</summary>
        public Vector3 RestartSpot(MatchPlayer m)
        {
            var r = Restart;
            if (r == null) return FormationTarget(m, m.time == LastPossession);
            if (r.taker == m) return TakerStand(r);
            bool attacking = m.time == r.time;
            Vector3 spot = Flat(r.spot);
            Vector3 p = FormationTargetFor(m, attacking, spot);
            var t = m.time;
            switch (r.tipo)
            {
                case TipoReinicio.Saida:
                    {
                        // cada time no seu campo; quem não tem a saída fora do círculo central
                        float f = t.Fwd(p);
                        if (f > -1f) p = new Vector3(-1.5f * t.attackDir, 0f, p.z);
                        if (!attacking && p.magnitude < 9.6f) p = p.sqrMagnitude > 0.01f ? p.normalized * 9.8f : -t.Forward * 9.8f;
                        if (attacking && m == ClosestTo(t, spot - t.Forward * 9f, false, r.taker)) p = spot - t.Forward * 8f + new Vector3(0f, 0f, 3f);
                        break;
                    }
                case TipoReinicio.Escanteio:
                    {
                        Vector3 goal = t == r.time ? t.TargetGoal : t.OwnGoal;
                        Vector3 inward = (attacking ? -t.Forward : t.Forward);
                        int idx = BoxIndex(m);
                        if (attacking && idx >= 0)
                        {
                            Vector3[] spots = { new Vector3(0f, 0f, 0f), new Vector3(2.5f, 0f, -4f), new Vector3(2.5f, 0f, 4f), new Vector3(-1.5f, 0f, -1.5f), new Vector3(6f, 0f, 0f) };
                            Vector3 s = spots[idx % spots.Length];
                            p = goal + inward * (7f + s.x) + new Vector3(0f, 0f, s.z);
                        }
                        else if (!attacking && m.funcao != Funcao.Atacante && m.funcao != Funcao.Ponta)
                        {
                            var mark = NearestAttackerInBox(m, goal);
                            p = mark != null ? Flat(mark.Pos) + (goal - Flat(mark.Pos)).normalized * 1.2f : goal + inward * 6f + new Vector3(0f, 0f, Random.Range(-5f, 5f));
                        }
                        break;
                    }
                case TipoReinicio.Penalti:
                    {
                        // todos fora da área e da meia-lua
                        Vector3 goal = r.time.TargetGoal;
                        Vector3 outward = -r.time.Forward;
                        float lineF = r.time.Fwd(goal) - 18.5f;
                        float f = r.time.Fwd(p);
                        if (f > lineF) p = new Vector3(lineF * r.time.attackDir, 0f, Mathf.Clamp(p.z, -18f, 18f));
                        if (Vector3.Distance(p, spot) < 9.6f) p = spot + outward * 10f + new Vector3(0f, 0f, p.z >= 0f ? 4f : -4f);
                        break;
                    }
                case TipoReinicio.Falta:
                    {
                        if (!attacking)
                        {
                            // barreira quando a falta é perto do gol
                            var goal = t.OwnGoal;
                            int w = WallIndex(m, r);
                            if (w >= 0 && Vector3.Distance(spot, goal) < 32f)
                            {
                                Vector3 dirG = (goal - spot).normalized;
                                Vector3 side = Vector3.Cross(Vector3.up, dirG);
                                p = spot + dirG * 9.3f + side * ((w - 1) * 0.6f);
                            }
                            else if (Vector3.Distance(p, spot) < 9.4f) p = spot + Flat(p - spot).normalized * 9.6f;
                        }
                        break;
                    }
                case TipoReinicio.TiroDeMeta:
                    {
                        // Regra 16 atual: companheiros podem receber dentro da própria área; adversários ficam fora da grande área até o toque
                        if (attacking && (m.funcao == Funcao.Zagueiro || m.funcao == Funcao.Lateral))
                        {
                            float sideZ = Mathf.Sign(m.slot.z == 0f ? 1f : -m.slot.z * t.attackDir) * (m.funcao == Funcao.Zagueiro ? 11f : 22f);
                            p = t.OwnGoal + t.Forward * (m.funcao == Funcao.Zagueiro ? 10.5f : 18f) + new Vector3(0f, 0f, sideZ);
                        }
                        else if (!attacking && InPenaltyArea(p, r.time))
                        {
                            float safeX = (Arena.HalfLength - 17.5f) * Mathf.Sign(r.time.OwnGoal.x);
                            p = new Vector3(safeX, 0f, p.z);
                        }
                        break;
                    }
                case TipoReinicio.BolaAoChao:
                    {
                        // Regra 8 atual (Bola ao chão): todos os demais atletas de ambos os times a pelo menos 4 m da bola
                        if (Vector3.Distance(p, spot) < 4.4f)
                        {
                            Vector3 away = Flat(p - spot);
                            if (away.sqrMagnitude < 0.01f) away = -t.Forward;
                            p = spot + away.normalized * 4.6f;
                        }
                        break;
                    }
                case TipoReinicio.Lateral:
                    {
                        // Regra 15: adversários a pelo menos 2 m do ponto de cobrança na linha lateral
                        if (!attacking && Vector3.Distance(p, spot) < 2.6f) p = spot + new Vector3(0f, 0f, -Mathf.Sign(spot.z) * 3f);
                        break;
                    }
            }
            p.x = Mathf.Clamp(p.x, -Arena.HalfLength + 1f, Arena.HalfLength - 1f);
            p.z = Mathf.Clamp(p.z, -Arena.HalfWidth + 1f, Arena.HalfWidth - 1f);
            return p;
        }

        /// <summary>Os 5 melhores cabeceadores (fora o cobrador) sobem para a área no escanteio.</summary>
        int BoxIndex(MatchPlayer m)
        {
            var list = new List<MatchPlayer>();
            foreach (var x in m.time.jogadores) if (x.Disponivel && x.pc != null && Restart != null && x != Restart.taker) list.Add(x);
            list.Sort((a, b) => (b.atr.cabeceio + (b.funcao == Funcao.Atacante ? 0.3f : 0f)).CompareTo(a.atr.cabeceio + (a.funcao == Funcao.Atacante ? 0.3f : 0f)));
            int i = list.IndexOf(m);
            return i >= 0 && i < 5 ? i : -1;
        }

        int WallIndex(MatchPlayer m, Reinicio r)
        {
            var list = new List<MatchPlayer>();
            foreach (var x in m.time.jogadores) if (x.Disponivel && x.pc != null && (x.funcao == Funcao.Volante || x.funcao == Funcao.Meia || x.funcao == Funcao.MeiaAberto || x.funcao == Funcao.Atacante)) list.Add(x);
            list.Sort((a, b) => Vector3.Distance(a.Pos, r.spot).CompareTo(Vector3.Distance(b.Pos, r.spot)));
            int i = list.IndexOf(m);
            return i >= 0 && i < 3 ? i : -1;
        }

        MatchPlayer NearestAttackerInBox(MatchPlayer def, Vector3 goal)
        {
            MatchPlayer best = null; float bd = 99f;
            foreach (var a in OpponentOf(def.time).jogadores)
            {
                if (!a.Disponivel || Vector3.Distance(Flat(a.Pos), goal) > 20f) continue;
                bool taken = false;
                foreach (var kv in marks) if (kv.Value == a && kv.Key != def) taken = true;
                if (taken) continue;
                float d = Vector3.Distance(a.Pos, def.Pos);
                if (d < bd) { bd = d; best = a; }
            }
            if (best != null) marks[def] = best;
            return best;
        }

        // ============================================================ geometria

        static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            float t;
            return DistToSegment(p, a, b, out t);
        }

        static float DistToSegment(Vector3 p, Vector3 a, Vector3 b, out float t)
        {
            Vector3 ab = b - a;
            float l2 = ab.sqrMagnitude;
            t = l2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
