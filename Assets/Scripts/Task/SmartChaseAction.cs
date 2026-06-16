using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using NavMeshAgent = UnityEngine.AI.NavMeshAgent;

namespace NodeCanvas.Tasks.Actions
{
    [Name("Smart Seek (GameObject)")]
    [Category("Movement/Pathfinding")]
    [Description("Di chuyển đuổi theo mục tiêu thông minh, tự động phanh gấp dứt khoát không trượt và tối ưu CPU.")]
    public class SmartChaseAction : ActionTask<NavMeshAgent>
    {
        [RequiredField]
        public BBParameter<GameObject> target;
        public BBParameter<float> speed = 4f;
        public BBParameter<float> keepDistance = 0.1f;

        [Tooltip("Gia tốc phanh (Số càng to phanh càng gắt, chống trượt lên người Player)")]
        public float acceleration = 100f;

        [Tooltip("Tần suất tính toán lại đường đi (giây) để tiết kiệm CPU.")]
        public float pathUpdateInterval = 0.2f;

        private Vector3? lastRequest;
        private float lastUpdateTime;

        protected override string info
        {
            get { return "Smart Seek " + target; }
        }

        protected override void OnExecute()
        {
            if (target.value == null) { EndAction(false); return; }

            // 1. Kích hoạt lại Agent và gán tốc độ + gia tốc phanh gắt
            agent.isStopped = false;
            agent.speed = speed.value;
            agent.acceleration = acceleration;

            // Sử dụng chính khoảng cách keepDistance để làm mốc dừng tự động của NavMesh
            agent.stoppingDistance = keepDistance.value;

            // Kiểm tra xem hiện tại đã đứng đủ gần chưa, nếu đủ rồi thì dừng luôn
            if (Vector3.Distance(agent.transform.position, target.value.transform.position) <= agent.stoppingDistance)
            {
                StopAgentAndFinish(true);
                return;
            }

            lastUpdateTime = 0f;
            UpdatePath();
        }

        protected override void OnUpdate()
        {
            if (target.value == null) { StopAgentAndFinish(false); return; }

            // 2. Tối ưu hiệu năng: Giới hạn tần suất gọi hàm SetDestination dựa trên interval
            if (Time.time - lastUpdateTime >= pathUpdateInterval)
            {
                UpdatePath();
            }

            // 3. Kiểm tra nếu Agent đã đi vào vùng stoppingDistance thành công
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                StopAgentAndFinish(true);
            }
        }

        private void UpdatePath()
        {
            if (target.value == null) return;

            var pos = target.value.transform.position;
            if (lastRequest != pos)
            {
                if (!agent.SetDestination(pos))
                {
                    StopAgentAndFinish(false);
                    return;
                }
                lastUpdateTime = Time.time;
            }
            lastRequest = pos;
        }

        protected override void OnPause() { OnStop(); }

        protected override void OnStop()
        {
            // 4. BẤT KỲ khi nào Node bị dừng/hủy ngang (do DYNAMIC Selector ngắt đè):
            // Ép Agent phanh khựng lại ngay lập tức tại chỗ, triệt tiêu quán tính trượt.
            if (agent != null && agent.gameObject.activeSelf && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero; // Xóa lực trượt quán tính
                agent.ResetPath();
            }
            lastRequest = null;
        }

        private void StopAgentAndFinish(bool success)
        {
            if (agent != null && agent.gameObject.activeSelf && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero; // Phanh gấp
            }
            EndAction(success);
        }

        public override void OnDrawGizmosSelected()
        {
            if (target.value != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(target.value.transform.position, keepDistance.value);
            }
        }
    }
}