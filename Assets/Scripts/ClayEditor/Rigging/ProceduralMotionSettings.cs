using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// 手続き的アニメーションのチューニング値を一括管理するScriptableObject
    /// 走行・攻撃・ステップ・被弾・待機の各パラメータをここで調整する
    /// </summary>
    [CreateAssetMenu(fileName = "ProceduralMotionSettings", menuName = "ClayEditor/ProceduralMotionSettings")]
    public sealed class ProceduralMotionSettings : ScriptableObject
    {
        [Header("Run")]
        [Tooltip("走行の周波数(速さ)")]
        [SerializeField] private float runFrequency = 8f;
        [Tooltip("背骨(ルート寄りの軸)1関節あたりの揺れ角(度)")]
        [SerializeField] private float runSpineAmplitude = 6f;
        [Tooltip("背骨の進行波の位相差(深さ1あたりのラジアン)")]
        [SerializeField] private float spinePhasePerDepth = 0.6f;
        [Tooltip("手足の振り角(度)")]
        [SerializeField] private float runLimbAmplitude = 35f;

        [Header("LegRun(足のある走り)")]
        [Tooltip("脚を動かす周波数(速さ)")]
        [SerializeField] private float legRunFrequency = 9f;
        [Tooltip("脚の振り角(度)")]
        [SerializeField] private float legRunLegAmplitude = 45f;
        [Tooltip("腕の振り角(度、脚と逆位相で振る)")]
        [SerializeField] private float legRunArmAmplitude = 30f;
        [Tooltip("胴体の上下バウンド角(度)")]
        [SerializeField] private float legRunBodyAmplitude = 5f;
        [Tooltip("脚を振るローカル軸(前後に振るので通常はX)")]
        [SerializeField] private Vector3 legRunSwingAxis = Vector3.right;

        [Header("Attack 共通")]
        [Tooltip("攻撃1回の長さ(秒)・硬直と揃えないときの既定値")]
        [SerializeField] private float attackDuration = 0.72f;
        [Tooltip("攻撃の振り角(度、先端ほど大きく振る)")]
        [SerializeField] private float attackAmplitude = 90f;
        [Tooltip("打撃開始前に間を空ける割合(0..1)溜め無し時のみ")]
        [SerializeField] private float attackAnticipationRatio = 0.08f;
        [Tooltip("間の後に一気に振り出す割合(0..1)")]
        [SerializeField] private float attackStrikePortion = 0.28f;
        [Tooltip("振り切り時のオーバーシュート量(0..0.45)")]
        [SerializeField] private float attackStrikeOvershoot = 0.18f;
        [Tooltip("溜め姿勢を攻撃へ持ち越す強さ(0..1)")]
        [SerializeField] private float chargeCarryWeight = 0.9f;
        [Tooltip("ルート移動無効時でも見た目だけ踏み込む距離倍率")]
        [SerializeField] private float visualLungeScale = 0.35f;
        [Tooltip("前方とみなすワールド方向(造形時の正面)")]
        [SerializeField] private Vector3 forwardDirection = Vector3.forward;

        [Header("Tackle(体当たり)")]
        [Tooltip("体当たりで前進する距離")]
        [SerializeField] private float tackleDistance = 2.2f;
        [Tooltip("体当たりの前傾角(度)")]
        [SerializeField] private float tackleLeanAngle = 42f;

        [Header("Punch(パンチ)")]
        [Tooltip("パンチで腕を振り出す角(度)")]
        [SerializeField] private float punchAmplitude = 105f;
        [Tooltip("パンチで前進する距離")]
        [SerializeField] private float punchDistance = 0.75f;

        [Header("Kick(キック)")]
        [Tooltip("キックで脚を振り上げる角(度)")]
        [SerializeField] private float kickAmplitude = 110f;
        [Tooltip("キックで前進する距離")]
        [SerializeField] private float kickDistance = 0.65f;

        [Header("SpinTackle(回転体当たり)")]
        [Tooltip("回転体当たりで前進する距離")]
        [SerializeField] private float spinTackleDistance = 3f;
        [Tooltip("回転体当たりの回転速度(度/秒)")]
        [SerializeField] private float spinTackleSpeed = 1080f;

        [Header("TailWhip(しっぽ攻撃)")]
        [Tooltip("しっぽを薙ぎ払う角(度)")]
        [SerializeField] private float tailWhipAmplitude = 115f;
        [Tooltip("しっぽを薙ぎ払うローカル軸(水平に振るので通常はY)")]
        [SerializeField] private Vector3 tailWhipAxis = Vector3.up;

        [Header("AttackCharge(溜め)")]
        [Tooltip("溜めで後ろに引く角(度)")]
        [SerializeField] private float chargePullbackAngle = 32f;
        [Tooltip("溜めで手足を引く角(度)")]
        [SerializeField] private float chargeLimbPullbackAngle = 48f;
        [Tooltip("溜めで後方へ引く距離")]
        [SerializeField] private float chargePullbackDistance = 0.38f;
        [Tooltip("最大溜め姿勢を保つ割合(0..1)")]
        [SerializeField] private float chargeHoldRatio = 0.28f;
        [Tooltip("溜めの引き込みに使う時間割合(0..1)")]
        [SerializeField] private float chargeBuildRatio = 0.42f;

        [Header("Headbutt(頭突き)")]
        [SerializeField] private float headbuttAmplitude = 88f;
        [SerializeField] private float headbuttDistance = 0.95f;

        [Header("Elbow(エルボー)")]
        [SerializeField] private float elbowAmplitude = 120f;
        [SerializeField] private float elbowDistance = 0.5f;

        [Header("Stomp(ストンプ)")]
        [SerializeField] private float stompLiftAngle = 78f;
        [SerializeField] private float stompSlamAngle = 55f;

        [Header("BodySlam(ボディスラム)")]
        [SerializeField] private float bodySlamDistance = 1.75f;
        [SerializeField] private float bodySlamLeanAngle = 52f;

        [Header("Step(ステップ)")]
        [Tooltip("ステップ1回の長さ(秒)")]
        [SerializeField] private float stepDuration = 0.25f;
        [Tooltip("ステップ中の視覚的な前後移動量")]
        [SerializeField] private float stepVisualDistance = 1.8f;
        [Tooltip("ステップ中に進行方向へ体を傾ける角(度)")]
        [SerializeField] private float stepLeanAngle = 16f;
        [Tooltip("転がる前のしゃがみ込み角(度)")]
        [SerializeField] private float stepPrepCrouchAngle = 14f;
        [Tooltip("転がる直前の手足のわずかな曲げ角(度)")]
        [SerializeField] private float stepLimbPrepAngle = 10f;
        [Tooltip("転がる前のしゃがみ込み時間割合(0..1)")]
        [SerializeField] private float stepPrepRatio = 0.1f;
        [Tooltip("着地後の立ち上がり時間割合(0..1)")]
        [SerializeField] private float stepLandRatio = 0.12f;
        [Tooltip("転がり中の手足をわずかに丸める角(度)")]
        [SerializeField] private float stepRollTuckAngle = 18f;

        [Header("Hit(被弾)")]
        [Tooltip("被弾モーション1回の長さ(秒)")]
        [SerializeField] private float hitDuration = 0.3f;
        [Tooltip("被弾時の後仰ぎ角(度)")]
        [SerializeField] private float hitSpineAmplitude = 42f;
        [Tooltip("被弾時の手足のはじき角(度)")]
        [SerializeField] private float hitLimbAmplitude = 32f;
        [Tooltip("被弾時のわずかな後退距離")]
        [SerializeField] private float hitKnockbackDistance = 0.28f;
        [Tooltip("被弾の鋭いピークまでの時間割合(0..1)")]
        [SerializeField] private float hitSnapRatio = 0.22f;

        [Header("Idle")]
        [Tooltip("待機の周波数")]
        [SerializeField] private float idleFrequency = 1.5f;
        [Tooltip("待機の揺れ角(度)")]
        [SerializeField] private float idleAmplitude = 3f;

        [Header("Bend Axes (ボーンのローカル軸)")]
        [Tooltip("背骨を曲げるローカル軸(ボーンの+Zが中心線方向)")]
        [SerializeField] private Vector3 spineBendAxis = Vector3.right;
        [Tooltip("手足を振るローカル軸")]
        [SerializeField] private Vector3 limbSwingAxis = Vector3.right;
        [Tooltip("攻撃で曲げるローカル軸")]
        [SerializeField] private Vector3 attackBendAxis = Vector3.right;

        private void OnValidate()
        {
            spineBendAxis = SanitizeBendAxis(spineBendAxis);
            limbSwingAxis = SanitizeBendAxis(limbSwingAxis);
            attackBendAxis = SanitizeBendAxis(attackBendAxis);
            legRunSwingAxis = SanitizeBendAxis(legRunSwingAxis);
            tailWhipAxis = SanitizeBendAxis(tailWhipAxis);
        }

        private static Vector3 SanitizeBendAxis(Vector3 axis)
        {
            Vector3 normalized = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.right;
            float absX = Mathf.Abs(normalized.x);
            float absY = Mathf.Abs(normalized.y);
            float absZ = Mathf.Abs(normalized.z);
            if (absZ >= absX && absZ >= absY)
            {
                return absY >= absX ? Vector3.up : Vector3.right;
            }

            Vector3 flattened = new Vector3(normalized.x, normalized.y, 0f);
            return flattened.sqrMagnitude > 1e-6f ? flattened.normalized : Vector3.right;
        }

        public float RunFrequency => runFrequency;
        public float RunSpineAmplitude => runSpineAmplitude;
        public float SpinePhasePerDepth => spinePhasePerDepth;
        public float RunLimbAmplitude => runLimbAmplitude;
        public float LegRunFrequency => legRunFrequency;
        public float LegRunLegAmplitude => legRunLegAmplitude;
        public float LegRunArmAmplitude => legRunArmAmplitude;
        public float LegRunBodyAmplitude => legRunBodyAmplitude;
        public Vector3 LegRunSwingAxis => legRunSwingAxis;
        public float AttackDuration => attackDuration;
        public float AttackAmplitude => attackAmplitude;
        public float AttackAnticipationRatio => attackAnticipationRatio;
        public float AttackStrikePortion => attackStrikePortion;
        public float AttackStrikeOvershoot => attackStrikeOvershoot;
        public float ChargeCarryWeight => chargeCarryWeight;
        public float VisualLungeScale => visualLungeScale;
        public Vector3 ForwardDirection => forwardDirection;
        public float TackleDistance => tackleDistance;
        public float TackleLeanAngle => tackleLeanAngle;
        public float PunchAmplitude => punchAmplitude;
        public float PunchDistance => punchDistance;
        public float KickAmplitude => kickAmplitude;
        public float KickDistance => kickDistance;
        public float SpinTackleDistance => spinTackleDistance;
        public float SpinTackleSpeed => spinTackleSpeed;
        public float TailWhipAmplitude => tailWhipAmplitude;
        public Vector3 TailWhipAxis => tailWhipAxis;
        public float ChargePullbackAngle => chargePullbackAngle;
        public float ChargeLimbPullbackAngle => chargeLimbPullbackAngle;
        public float ChargePullbackDistance => chargePullbackDistance;
        public float ChargeHoldRatio => chargeHoldRatio;
        public float ChargeBuildRatio => chargeBuildRatio;
        public float HeadbuttAmplitude => headbuttAmplitude;
        public float HeadbuttDistance => headbuttDistance;
        public float ElbowAmplitude => elbowAmplitude;
        public float ElbowDistance => elbowDistance;
        public float StompLiftAngle => stompLiftAngle;
        public float StompSlamAngle => stompSlamAngle;
        public float BodySlamDistance => bodySlamDistance;
        public float BodySlamLeanAngle => bodySlamLeanAngle;
        public float StepDuration => stepDuration;
        public float StepVisualDistance => stepVisualDistance;
        public float StepLeanAngle => stepLeanAngle;
        public float StepPrepCrouchAngle => stepPrepCrouchAngle;
        public float StepLimbPrepAngle => stepLimbPrepAngle;
        public float StepPrepRatio => stepPrepRatio;
        public float StepLandRatio => stepLandRatio;
        public float StepRollTuckAngle => stepRollTuckAngle;
        public float HitDuration => hitDuration;
        public float HitSpineAmplitude => hitSpineAmplitude;
        public float HitLimbAmplitude => hitLimbAmplitude;
        public float HitKnockbackDistance => hitKnockbackDistance;
        public float HitSnapRatio => hitSnapRatio;
        public float IdleFrequency => idleFrequency;
        public float IdleAmplitude => idleAmplitude;
        public Vector3 SpineBendAxis => spineBendAxis;
        public Vector3 LimbSwingAxis => limbSwingAxis;
        public Vector3 AttackBendAxis => attackBendAxis;
    }
}
