using Fusion;
using UnityEngine;

namespace DefaultNamespace
{
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private Rigidbody rigidBody;
        [SerializeField] private float moveSpeed = 100f;
        [SerializeField] private float acceleration = 25f;

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasInputAuthority || !rigidBody) return;
            if (GetInput(out GameplayInput input))
            {
                Vector3 direction = input.MoveDirection.normalized;
                rigidBody.MoveRotation(Quaternion.Euler(0f, input.LookRotation.x, 0f));

                DoMove(direction);
            }
        }

        private void DoMove(Vector3 direction)
        {
            Vector3 currentVelocity = rigidBody.linearVelocity;
            Vector3 wishVelocity = direction * moveSpeed;
            rigidBody.AddForce((wishVelocity - new Vector3(currentVelocity.x, 0, currentVelocity.z)) * acceleration,
                ForceMode.Acceleration);
        }
    }
}