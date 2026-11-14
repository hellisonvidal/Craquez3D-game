using System;
using UnityEngine;

namespace Craquez
{
    /// <summary>Repassa OnAnimatorIK do objeto do Animator (modelo) para o controlador na raiz.</summary>
    [RequireComponent(typeof(Animator))]
    public class IKRelay : MonoBehaviour
    {
        public Action<int> onIK;
        void OnAnimatorIK(int layerIndex) { if (onIK != null) onIK(layerIndex); }
        // impede que o Animator mova o objeto com root motion (o movimento é controlado por código)
        void OnAnimatorMove() { }
    }
}
