using Fusion;
using UnityEngine;

namespace DefaultNamespace
{
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField]
        private CharacterController controller;
        [SerializeField]
        private float moveSpeed = 5f;
        
        public override void FixedUpdateNetwork()
        {
            if (!Object.HasInputAuthority || !controller) return;
            if (GetInput(out GameplayInput input))
            {
                Vector3 direction = input.MoveDirection.normalized;
                transform.rotation = Quaternion.Euler(0f, input.LookRotation.x, 0f);
                
                DoMove(direction);
            }
        }

        private void DoMove(Vector3 direction)
        {
            controller.Move(direction * moveSpeed * Runner.DeltaTime);
        }
    }
}