using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Atacante controlado pela IA (modo goleiro): conduz a bola até um ponto de finalização,
    /// escolhe tipo de chute, força e canto, e finaliza. Varia o estilo para treinar o goleiro.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class ShooterAI : MonoBehaviour
    {
        public BallPhysics ball;
        PlayerController pc;

        enum Fase { Esperando, Conduzindo, Carregando, Finalizado }
        Fase fase;
        float timer;
        Vector3 shootSpot;
        TipoChute tipo;
        float targetPower;
        Vector3 aim;

        void Awake() { pc = GetComponent<PlayerController>(); }

        void OnEnable() { Restart(); }

        public void Restart()
        {
            fase = Fase.Esperando;
            timer = Random.Range(1.0f, 2.2f);
            ClearInputs();
        }

        void ClearInputs()
        {
            if (pc == null) return;
            pc.aiMoveWorld = Vector3.zero;
            pc.aiShoot = pc.aiFinesse = pc.aiChip = pc.aiSprint = false;
        }

        void PlanShot()
        {
            Vector3 g = Arena.AttackGoalCenter;
            float dist = Random.Range(11f, 22f);
            float ang = Random.Range(-28f, 28f);
            shootSpot = g + Quaternion.Euler(0f, ang, 0f) * (-Vector3.right) * dist;
            float r = Random.value;
            tipo = r < 0.55f ? TipoChute.Forte : (r < 0.88f ? TipoChute.Colocado : TipoChute.Cavadinha);
            targetPower = tipo == TipoChute.Forte ? Random.Range(0.55f, 0.92f) : Random.Range(0.45f, 0.85f);
            // canto: lateral (z) aleatório com preferência pelos cantos
            float side = Random.value < 0.5f ? -1f : 1f;
            float lat = Random.value < 0.75f ? side * Random.Range(0.55f, 1f) : Random.Range(-0.3f, 0.3f);
            aim = new Vector3(0f, 0f, lat); // ComputeShot usa a componente z da mira
        }

        void Update()
        {
            if (pc == null || ball == null || !pc.aiControlled) return;
            timer -= Time.deltaTime;
            Vector3 pos = transform.position;
            Vector3 bp = ball.transform.position;

            switch (fase)
            {
                case Fase.Esperando:
                    ClearInputs();
                    if (timer <= 0f) { PlanShot(); fase = Fase.Conduzindo; timer = 8f; }
                    break;

                case Fase.Conduzindo:
                    {
                        Vector3 toBall = bp - pos; toBall.y = 0f;
                        Vector3 target = pc.HasBall ? shootSpot : bp;
                        Vector3 to = target - pos; to.y = 0f;
                        pc.aiMoveWorld = to.magnitude > 0.3f ? to.normalized : Vector3.zero;
                        pc.aiSprint = !pc.HasBall && toBall.magnitude > 3f;
                        bool there = pc.HasBall && (to.magnitude < 1.2f || pos.x > shootSpot.x);
                        if (there || timer <= 0f)
                        {
                            fase = Fase.Carregando;
                            timer = 0f;
                            pc.aiAimWorld = aim;
                            SetButton(true);
                        }
                        break;
                    }

                case Fase.Carregando:
                    {
                        Vector3 toGoal = Arena.AttackGoalCenter - pos; toGoal.y = 0f;
                        pc.aiMoveWorld = toGoal.normalized * 0.3f;
                        if (pc.Charge >= targetPower || !pc.Charging && timer < -1.5f)
                        {
                            SetButton(false);
                            fase = Fase.Finalizado;
                            timer = 0f;
                        }
                        break;
                    }

                case Fase.Finalizado:
                    pc.aiMoveWorld = Vector3.zero;
                    SetButton(false);
                    break;
            }
        }

        void SetButton(bool down)
        {
            pc.aiShoot = down && tipo == TipoChute.Forte;
            pc.aiFinesse = down && tipo == TipoChute.Colocado;
            pc.aiChip = down && tipo == TipoChute.Cavadinha;
        }
    }
}
