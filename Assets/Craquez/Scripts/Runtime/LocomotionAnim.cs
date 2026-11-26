using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Liga a velocidade real do personagem ao blend de locomoção sem os pés deslizarem:
    /// - divide pela escala do corpo (um jogador mais alto cobre mais chão a cada passada);
    /// - acima do clipe mais rápido acelera a reprodução (só na locomoção, até 1,5x).
    /// </summary>
    public static class LocomotionAnim
    {
        static readonly int LocoState = Animator.StringToHash("Locomocao");

        public static void Drive(Animator anim, int speedHash, float speed, float bodyScale, float topSpeed, float dt, float damp)
        {
            if (anim == null) return;
            float s = speed / Mathf.Max(0.5f, bodyScale);
            if (s < 0.08f)
            {
                // Parado de verdade: zera na hora para não misturar frações do clipe de caminhada (pés mexendo parados)
                float cur = anim.GetFloat(speedHash);
                float next = Mathf.MoveTowards(cur, 0f, 12f * Mathf.Max(dt, 0.001f));
                anim.SetFloat(speedHash, next < 0.02f ? 0f : next);
                s = 0f;
            }
            else
            {
                anim.SetFloat(speedHash, s, damp, dt);
            }
            bool loco = !anim.IsInTransition(0) && anim.GetCurrentAnimatorStateInfo(0).shortNameHash == LocoState;
            if (!loco) { anim.speed = 1f; return; } // chutes, quedas e gestos sempre no ritmo original
            float rate = topSpeed > 0.5f && s > topSpeed ? Mathf.Min(s / topSpeed, 1.5f) : 1f;
            anim.speed = Mathf.MoveTowards(anim.speed, rate, 4f * Mathf.Max(dt, 0.001f));
        }
    }
}
