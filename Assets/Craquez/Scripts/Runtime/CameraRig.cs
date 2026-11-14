using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Câmeras do treino. Padrão: atrás do atacante, enquadrando a corrida para o gol.
    /// Outras: dinâmica baixa, transmissão de TV, aérea, atrás do gol e (modo goleiro) atrás do goleiro.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public enum Modo { AtrasDoJogador = 0, Dinamica = 1, TV = 2, Aerea = 3, AtrasDoGol = 4, Goleiro = 5, Menu = 6 }

        public static readonly Modo[] ModosAtacante = { Modo.AtrasDoJogador, Modo.Dinamica, Modo.TV, Modo.Aerea, Modo.AtrasDoGol };
        public static readonly Modo[] ModosGoleiro = { Modo.Goleiro, Modo.AtrasDoGol, Modo.TV, Modo.Aerea };

        public static string Nome(Modo m)
        {
            switch (m)
            {
                case Modo.AtrasDoJogador: return "Atrás do atacante";
                case Modo.Dinamica: return "Dinâmica (baixa)";
                case Modo.TV: return "Transmissão TV";
                case Modo.Aerea: return "Aérea";
                case Modo.AtrasDoGol: return "Atrás do gol";
                case Modo.Goleiro: return "Goleiro";
                default: return "Menu";
            }
        }

        public Transform player;
        public Transform keeper;
        public BallPhysics ball;
        public Modo mode = Modo.AtrasDoJogador;
        public bool goalkeeperRole;

        Camera cam;
        Vector3 vel;
        Vector3 lookPoint;
        float shake;
        float orbit;

        void Awake()
        {
            cam = GetComponent<Camera>();
            WeatherSystem.OnLightning += OnThunder;
        }

        void OnDestroy() { WeatherSystem.OnLightning -= OnThunder; }

        void OnThunder(float v) { shake = Mathf.Max(shake, 0.06f * v); }

        public void Cycle()
        {
            var list = goalkeeperRole ? ModosGoleiro : ModosAtacante;
            int i = System.Array.IndexOf(list, mode);
            mode = list[(i + 1 + list.Length) % list.Length];
        }

        public void Snap()
        {
            Vector3 pos, look; float fov;
            Desired(out pos, out look, out fov);
            transform.position = pos;
            lookPoint = look;
            transform.rotation = Quaternion.LookRotation(look - pos);
            if (cam != null) cam.fieldOfView = fov;
            vel = Vector3.zero;
        }

        void Desired(out Vector3 pos, out Vector3 look, out float fov)
        {
            Vector3 goal = Arena.AttackGoalCenter + Vector3.up * 1.0f;
            Vector3 pp = player != null ? player.position : Vector3.zero;
            Vector3 bp = ball != null ? ball.transform.position : pp;
            Vector3 kp = keeper != null ? keeper.position : Arena.AttackGoalCenter;
            switch (mode)
            {
                case Modo.TV:
                    {
                        float x = Mathf.Clamp(Mathf.Lerp(pp.x, bp.x, 0.5f), -10f, 46f);
                        pos = new Vector3(x - 4f, 17f, -Arena.HalfWidth - 16f);
                        look = new Vector3(Mathf.Lerp(pp.x, bp.x, 0.6f) + 3f, 0.5f, Mathf.Lerp(pp.z, bp.z, 0.5f) * 0.8f);
                        fov = 34f;
                        break;
                    }
                case Modo.Aerea:
                    {
                        Vector3 c = Vector3.Lerp(pp, bp, 0.5f);
                        pos = new Vector3(c.x - 14f, 32f, c.z * 0.6f);
                        look = new Vector3(c.x + 4f, 0f, c.z * 0.8f);
                        fov = 48f;
                        break;
                    }
                case Modo.AtrasDoGol:
                    {
                        pos = Arena.AttackGoalCenter + new Vector3(9f, 4.2f, Mathf.Clamp(bp.z * 0.25f, -3f, 3f));
                        look = new Vector3(Mathf.Lerp(bp.x, Arena.HalfLength - 12f, 0.3f), 0.8f, bp.z * 0.7f);
                        fov = 50f;
                        break;
                    }
                case Modo.Goleiro:
                    {
                        Vector3 toBall = bp - kp; toBall.y = 0f;
                        Vector3 dir = toBall.sqrMagnitude > 0.5f ? toBall.normalized : -Vector3.right;
                        pos = kp - dir * 4.5f + Vector3.up * 2.6f;
                        pos.x = Mathf.Min(pos.x, Arena.HalfLength + 1.5f);
                        look = Vector3.Lerp(kp + Vector3.up * 1.2f, bp, 0.6f);
                        fov = 58f;
                        break;
                    }
                case Modo.Dinamica:
                    {
                        Vector3 toGoal = goal - pp; toGoal.y = 0f;
                        Vector3 dir = toGoal.sqrMagnitude > 1f ? toGoal.normalized : Vector3.right;
                        Vector3 side = Vector3.Cross(Vector3.up, dir);
                        pos = pp - dir * 3.6f + Vector3.up * 1.45f + side * 1.1f;
                        look = pp + dir * 6f + Vector3.up * 1.0f;
                        fov = 60f;
                        break;
                    }
                case Modo.Menu:
                    {
                        orbit += Time.deltaTime * 4f;
                        Vector3 c = new Vector3(38f, 0f, 0f);
                        pos = c + Quaternion.Euler(0f, orbit, 0f) * new Vector3(-26f, 9f, -18f);
                        look = Arena.AttackGoalCenter + new Vector3(-10f, 1.2f, 0f);
                        fov = 45f;
                        break;
                    }
                default:
                    {
                        // atrás do atacante, enquadrando a corrida até o gol
                        Vector3 toGoal = goal - pp; toGoal.y = 0f;
                        Vector3 dir = toGoal.sqrMagnitude > 1f ? toGoal.normalized : Vector3.right;
                        Vector3 side = Vector3.Cross(Vector3.up, dir);
                        float dist = Mathf.Lerp(6.5f, 9f, Mathf.Clamp01(toGoal.magnitude / 35f));
                        pos = pp - dir * dist + Vector3.up * 3.1f + side * Mathf.Clamp((bp - pp).z * 0.15f, -1.5f, 1.5f);
                        look = Vector3.Lerp(pp + Vector3.up * 1.2f + dir * 5f, goal, 0.25f);
                        fov = 52f;
                        break;
                    }
            }
        }

        void LateUpdate()
        {
            Vector3 pos, look; float fov;
            Desired(out pos, out look, out fov);
            float smooth = mode == Modo.Menu ? 0.5f : (mode == Modo.Dinamica ? 0.12f : 0.22f);
            transform.position = Vector3.SmoothDamp(transform.position, pos, ref vel, smooth);
            lookPoint = Vector3.Lerp(lookPoint, look, 1f - Mathf.Exp(-6f * Time.deltaTime));
            Vector3 fwd = lookPoint - transform.position;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.right;
            Quaternion q = Quaternion.LookRotation(fwd);
            if (shake > 0f)
            {
                q *= Quaternion.Euler(Random.insideUnitSphere * shake * 20f);
                shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 0.15f);
            }
            transform.rotation = q;
            if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-4f * Time.deltaTime));
        }
    }
}
