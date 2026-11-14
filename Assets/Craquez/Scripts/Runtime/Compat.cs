using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Pequena camada de compatibilidade entre versões do Unity
    /// (Unity 6 renomeou velocity/drag/PhysicMaterial).
    /// </summary>
    public static class Compat
    {
        public static Vector3 GetVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDamping(Rigidbody rb, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linear;
            rb.angularDamping = angular;
#else
            rb.drag = linear;
            rb.angularDrag = angular;
#endif
        }

        /// <summary>Cria e aplica um material físico ao collider.</summary>
        public static void SetPhysicsMaterial(Collider c, string name, float dynamicFriction, float staticFriction, float bounciness, bool multiplyBounce)
        {
            SetPhysicsMaterial(c, name, dynamicFriction, staticFriction, bounciness, multiplyBounce ? 1 : 0);
        }

        /// <summary>combine: 0 = média, 1 = multiplicar, 2 = mínimo (atrito e quique).</summary>
        public static void SetPhysicsMaterial(Collider c, string name, float dynamicFriction, float staticFriction, float bounciness, int combine)
        {
#if UNITY_6000_0_OR_NEWER
            var m = new PhysicsMaterial(name);
            m.bounceCombine = combine == 2 ? PhysicsMaterialCombine.Minimum : (combine == 1 ? PhysicsMaterialCombine.Multiply : PhysicsMaterialCombine.Average);
            m.frictionCombine = combine == 2 ? PhysicsMaterialCombine.Minimum : PhysicsMaterialCombine.Average;
#else
            var m = new PhysicMaterial(name);
            m.bounceCombine = combine == 2 ? PhysicMaterialCombine.Minimum : (combine == 1 ? PhysicMaterialCombine.Multiply : PhysicMaterialCombine.Average);
            m.frictionCombine = combine == 2 ? PhysicMaterialCombine.Minimum : PhysicMaterialCombine.Average;
#endif
            m.dynamicFriction = dynamicFriction;
            m.staticFriction = staticFriction;
            m.bounciness = bounciness;
            c.sharedMaterial = m;
        }

        public static void SetBounciness(Collider c, float bounciness)
        {
            if (c.sharedMaterial != null) c.sharedMaterial.bounciness = bounciness;
        }

        public static Font DefaultFont()
        {
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }
    }
}
