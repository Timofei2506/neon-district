using UnityEngine;
using UnityEngine.AI;

namespace NeonDistrict.Player
{
    /// <summary>
    /// Прохожий: гуляет по тротуарам (NavMesh), пугается выстрелов и убегает,
    /// реагирует на сигнал машины, может быть сбит.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Pedestrian : MonoBehaviour, IDamageable
    {
        [Header("Поведение")]
        public float walkSpeed = 1.35f;
        public float runSpeed = 4.6f;
        public float idleChance = 0.25f;
        public float wanderRadius = 60f;
        public float health = 60f;

        [Header("Ссылки")]
        public Animator animator;

        private NavMeshAgent _agent;
        private Vector3 _home;
        private float _idleTimer;
        private bool _fleeing;
        private float _fleeTimer;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AliveHash = Animator.StringToHash("Alive");

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _home = transform.position;
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            _agent.speed = walkSpeed;
            PickWanderTarget();
        }

        private void Update()
        {
            if (animator != null) animator.SetFloat(SpeedHash, _agent.velocity.magnitude / runSpeed, 0.15f, Time.deltaTime);

            if (_fleeing)
            {
                _fleeTimer -= Time.deltaTime;
                if (_fleeTimer <= 0f)
                {
                    _fleeing = false;
                    _agent.speed = walkSpeed;
                    PickWanderTarget();
                }
                return;
            }

            if (_idleTimer > 0f)
            {
                _idleTimer -= Time.deltaTime;
                return;
            }

            if (!_agent.pathPending && _agent.remainingDistance < 1.2f)
            {
                if (Random.value < idleChance) _idleTimer = Random.Range(1.5f, 5f);
                PickWanderTarget();
            }
        }

        private void PickWanderTarget()
        {
            for (int i = 0; i < 8; i++)
            {
                var p = _home + new Vector3(Random.Range(-wanderRadius, wanderRadius), 0f, Random.Range(-wanderRadius, wanderRadius));
                if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                    return;
                }
            }
        }

        /// <summary>Убежать от источника страха (выстрел, машина, кулак).</summary>
        public void Flee(Vector3 from)
        {
            _fleeing = true;
            _fleeTimer = Random.Range(3f, 6f);
            _agent.speed = runSpeed;
            var dir = (transform.position - from).normalized;
            var target = transform.position + dir * 25f;
            if (NavMesh.SamplePosition(target, out var hit, 10f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 direction)
        {
            health -= amount;
            Flee(point);
            if (health <= 0f) Die();
        }

        private void Die()
        {
            if (animator != null) animator.SetBool(AliveHash, false);
            _agent.enabled = false;
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 70f;
            rb.AddForce(Vector3.up * 60f + transform.forward * 40f, ForceMode.Impulse);
            Destroy(gameObject, 20f);
        }
    }
}
