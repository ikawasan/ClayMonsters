using System.Collections.Generic;
using Extensions;
using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// 再生するモーションの種類
    /// </summary>
    public enum MotionType
    {
        /// <summary>
        /// 停止
        /// </summary>
        None,
        /// <summary>
        /// 待機
        /// </summary>
        Idle,
        /// <summary>
        /// 走行(足のないモンスター用、付属肢を振る)
        /// </summary>
        Run,
        /// <summary>
        /// 走行(足のあるモンスター用、脚を交互に動かす)
        /// </summary>
        LegRun,
        /// <summary>
        /// 体当たり(腕のないモンスター用、前方へ突進する)
        /// </summary>
        Tackle,
        /// <summary>
        /// パンチ(腕で殴る)
        /// </summary>
        Punch,
        /// <summary>
        /// キック(脚で蹴る)
        /// </summary>
        Kick,
        /// <summary>
        /// 回転体当たり(高速回転しながら突進する)
        /// </summary>
        SpinTackle,
        /// <summary>
        /// しっぽ攻撃(尻尾を左右に薙ぎ払う)
        /// </summary>
        TailWhip,
        /// <summary>
        /// 前進ステップ(ジャンプ前転)
        /// </summary>
        StepForward,
        /// <summary>
        /// 後退ステップ(ジャンプ後転)
        /// </summary>
        StepBackward,
        /// <summary>
        /// 被弾(後仰ぎとわずかなノックバック)
        /// </summary>
        Hit,
        /// <summary>
        /// 攻撃前の溜め(内部演出用)
        /// </summary>
        AttackCharge,
        /// <summary>
        /// 頭突き(前方向へ突き出す)
        /// </summary>
        Headbutt,
        /// <summary>
        /// エルボー(近距離の肘攻撃)
        /// </summary>
        Elbow,
        /// <summary>
        /// ストンプ(足を振り下ろす)
        /// </summary>
        Stomp,
        /// <summary>
        /// ボディスラム(全体を叩きつける)
        /// </summary>
        BodySlam,
        /// <summary>
        /// アッパー(腕を下から突き上げる)
        /// </summary>
        Uppercut,
        /// <summary>
        /// 膝蹴り(近距離で膝を突き上げる)
        /// </summary>
        Knee,
        /// <summary>
        /// ショルダー(体を横に振りながら突進する)
        /// </summary>
        ShoulderRam,
        /// <summary>
        /// のしかかり(跳ね上がって全体を叩きつける)
        /// </summary>
        BellyFlop,
        /// <summary>
        /// 腰ブン(回旋しながら体当たりする)
        /// </summary>
        HipCheck,
        /// <summary>
        /// グラウンドパウンド(その場で跳ねて叩きつける)
        /// </summary>
        GroundPound,
        /// <summary>
        /// 平打ち(腕を大きく振り抜く)
        /// </summary>
        Slap,
        /// <summary>
        /// 足払い(脚を低く薙ぎ払う)
        /// </summary>
        LowSweep,
        /// <summary>
        /// 噛みつき(前部を突き出して噛む)
        /// </summary>
        Bite
    }

    /// <summary>
    /// 自動生成したボーン(チェーン+分岐)に対して、走行や攻撃などの動きを与える手続き的アニメーション
    /// </summary>
    public class ProceduralMotionCharacter : MonoBehaviour
    {
        [Tooltip("手続き的アニメーションの調整値。未設定ならScriptableObject既定値を使う")]
        [SerializeField] private ProceduralMotionSettings settings;

        private static ProceduralMotionSettings fallbackSettings;

        private ProceduralMotionSettings MotionSettings
        {
            get
            {
                if (settings != null)
                {
                    return settings;
                }

                if (fallbackSettings == null)
                {
                    fallbackSettings = ScriptableObject.CreateInstance<ProceduralMotionSettings>();
                }

                return fallbackSettings;
            }
        }

        /// <summary>
        /// ボーンごとの情報
        /// </summary>
        private struct BoneInfo
        {
            public Transform transform;
            public Quaternion baseLocalRotation;
            public int depth;
            public bool isLimb;
            public float limbPhase;   // 手足の位相(方位角ベース)
            public bool isLeg;        // 脚に属するか
            public bool isArm;        // 腕に属するか
            public bool isBack;       // 後ろ(尻尾)に属するか
            public bool isFront;      // 前(頭)に属するか
            public int limbIndex;     // 何番目の付属肢か(左右交互の位相に使う)
            public float lateralSign; // ボーンの左右位置(+1=右 -1=左)左右で逆向きに振るのに使う
        }

        private readonly List<BoneInfo> infos = new();
        private int maxDepth = 1;

        // 歩行診断を一度だけ出すためのフラグ
        private bool loggedLocomotionDiagnostics;

        private MotionType currentMotion = MotionType.None;
        private MotionType previousMotion = MotionType.Idle;
        private float time;
        private float attackStartTime;
        private float activeMotionDuration;
        private bool isFinishingTimedMotion;
        private MotionType chargeUpcomingAttack = MotionType.None;
        private float chargeStartTime;
        private float chargeDuration;
        private Vector3 chargeBasePosition;
        private bool chargeRootSaved;
        private float lastChargeIntensity;
        private float strikeChargeCarry;
        private Vector3 rootBoneBaseLocalPosition;
        private bool rootBoneLocalPositionSaved;

        // 攻撃中の位置とルート回転の復元用に開始時の状態を記録する
        private Vector3 attackStartPosition;
        private Quaternion attackStartWorldRotation;
        private Quaternion rootBoneBaseRotation;
        private bool attackStateSaved;

        // ルートボーン(回転体当たりで回す対象)
        private Transform rootBone;

        // 戦闘中に敵の右側へはみ出さないための制約
        private Transform battleConstraintEnemy;
        private Vector3 battleConstraintApproachAxis;
        private float battleConstraintMinSeparation;
        private float battleConstraintMaxSeparation;
        private bool hasBattlePositionConstraint;

        // ClayEditプレビューではfalseにしてルート移動を止める
        [Tooltip("攻撃・ステップ時のルートTransform移動を有効にする(シーン固有)")]
        [SerializeField] private bool enableRootTranslation = true;

        // 脚を交互に動かすための部位分類器(脚や腕を識別するのに使う)
        private SkeletonPartAnalyzer partAnalyzer;

        /// <summary>
        /// 再生準備ができているか
        /// </summary>
        public bool IsReady => infos.Count > 0;

        /// <summary>
        /// 現在再生中のモーション
        /// </summary>
        public MotionType CurrentMotion => currentMotion;

        /// <summary>
        /// 攻撃溜め中の力の強さ(0-1)
        /// </summary>
        public float ChargeIntensity => lastChargeIntensity;

        /// <summary>
        /// 攻撃溜めモーション中か
        /// </summary>
        public bool IsCharging => currentMotion == MotionType.AttackCharge;

        /// <summary>
        /// 脚部位を持つか
        /// </summary>
        public bool HasLegs { get; private set; }

        /// <summary>
        /// 攻撃・ステップ時のルートTransform移動を有効にする
        /// </summary>
        /// <param name="enabled">有効ならtrue</param>
        public void SetRootTranslationEnabled(bool enabled)
        {
            enableRootTranslation = enabled;
        }

        /// <summary>
        /// 別インスタンスのSettings参照とシーン固有オプションをコピーする
        /// </summary>
        /// <param name="source">コピー元</param>
        public void CopyConfigurationFrom(ProceduralMotionCharacter source)
        {
            if (source == null)
            {
                return;
            }

            settings = source.settings;
            enableRootTranslation = source.enableRootTranslation;
        }

        /// <summary>
        /// 手続き的アニメーションの調整値ScriptableObjectを設定する
        /// </summary>
        /// <param name="motionSettings">調整値</param>
        public void SetMotionSettings(ProceduralMotionSettings motionSettings)
        {
            settings = motionSettings;
        }

        /// <summary>
        /// ボーン階層を受け取り、部位(背骨や手足)を推定して再生準備をする
        /// 基準ポーズ(baseLocalRotation)を記録するため、ボーンが静止している状態で呼ぶこと
        /// </summary>
        /// <param name="bones">対象のボーンTransform群(チェーン+分岐、ルートが体の中心)</param>
        public void Initialize(Transform[] bones)
        {
            infos.Clear();
            currentMotion = MotionType.None;
            time = 0f;
            rootBone = null;
            HasLegs = false;

            if (bones == null || bones.Length == 0)
            {
                return;
            }

            int n = bones.Length;

            // ボーンからインデックス
            var indexOf = new Dictionary<Transform, int>(n);
            for (int i = 0; i < n; i++)
            {
                if (bones[i] != null && !indexOf.ContainsKey(bones[i]))
                {
                    indexOf.Add(bones[i], i);
                }
            }

            // 集合内での親インデックスと子リスト
            int[] parentIdx = new int[n];
            var children = new List<int>[n];
            for (int i = 0; i < n; i++)
            {
                children[i] = new List<int>();
            }

            for (int i = 0; i < n; i++)
            {
                parentIdx[i] = -1;
                if (bones[i] == null)
                {
                    continue;
                }

                Transform p = bones[i].parent;
                if (p != null && indexOf.TryGetValue(p, out int pi))
                {
                    parentIdx[i] = pi;
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (parentIdx[i] >= 0)
                {
                    children[parentIdx[i]].Add(i);
                }
            }

            // 深さ
            int[] depth = new int[n];
            maxDepth = 1;
            for (int i = 0; i < n; i++)
            {
                int d = 0;
                int c = i;
                while (parentIdx[c] >= 0 && d <= n)
                {
                    c = parentIdx[c];
                    d++;
                }

                depth[i] = d;
                if (d > maxDepth)
                {
                    maxDepth = d;
                }
            }

            // ルート(depth=0)を体の中心とみなす
            int root = -1;
            for (int i = 0; i < n; i++)
            {
                if (parentIdx[i] == -1)
                {
                    root = i;
                    break;
                }
            }

            if (root >= 0)
            {
                rootBone = bones[root];
            }

            // 各ボーンが属する手足(ルート直下の祖先)を求める
            int[] limbRoot = new int[n];
            for (int i = 0; i < n; i++)
            {
                limbRoot[i] = -1;
            }

            for (int i = 0; i < n; i++)
            {
                // 自分から親をたどって、ルート直下(親がルート)のノードを見つける
                int c = i;
                int prev = i;
                while (c >= 0 && c != root)
                {
                    prev = c;
                    c = parentIdx[c];
                }

                // c==root なら prev がルート直下の手足、i==root の場合は -1(背骨/中心)
                limbRoot[i] = (i == root) ? -1 : prev;
            }

            // ルート位置(方位角の基準)
            Vector3 rootPos = root >= 0 && bones[root] != null ? bones[root].position : Vector3.zero;

            // 左右判定に使うモデルの右方向(前後スイング軸)
            Vector3 gaitRightWorld = ResolveLocomotionSwingWorldAxis();

            // 主軸(ルートから見た体の上方向)の決定、ここでは簡易に world Up を主軸とし、その平面上の角度で方位を求める
            Vector3 axis = Vector3.up;
            Vector3 refDir = Vector3.forward;
            Vector3 sideDir = Vector3.Cross(axis, refDir).normalized;
            if (sideDir.sqrMagnitude < 1e-6f)
            {
                refDir = Vector3.right;
                sideDir = Vector3.Cross(axis, refDir).normalized;
            }

            // 手足ごとの方位角を求める(主軸まわりの角度)
            var limbRootAngle = new Dictionary<int, float>();
            for (int i = 0; i < n; i++)
            {
                if (parentIdx[i] == root && i != root)
                {
                    Vector3 dir = (bones[i].position - rootPos);
                    Vector3 planar = Vector3.ProjectOnPlane(dir, axis);
                    float angle;
                    if (planar.sqrMagnitude < 1e-8f)
                    {
                        angle = 0f;
                    }
                    else
                    {
                        float x = Vector3.Dot(planar, refDir);
                        float y = Vector3.Dot(planar, sideDir);
                        angle = Mathf.Atan2(y, x); // -π..π
                    }

                    limbRootAngle[i] = angle;
                }
            }

            // 情報確定
            for (int i = 0; i < n; i++)
            {
                if (bones[i] == null)
                {
                    continue;
                }

                bool isLimb = limbRoot[i] >= 0;
                float phase = 0f;
                if (isLimb && limbRootAngle.TryGetValue(limbRoot[i], out float a))
                {
                    phase = a;
                }

                // ボーン自身の左右位置で符号を決める(左右で逆向きに振り交互歩行にする)
                float lateral = Vector3.Dot(bones[i].position - rootPos, gaitRightWorld);
                float lateralSign = lateral >= 0f ? 1f : -1f;

                infos.Add(new BoneInfo
                {
                    transform = bones[i],
                    baseLocalRotation = bones[i].localRotation,
                    depth = depth[i],
                    isLimb = isLimb,
                    limbPhase = phase,
                    lateralSign = lateralSign
                });
            }

            // 各ボーンの部位(脚/腕)を割り当てる
            AssignParts(bones);
            UpdateHasLegs();
        }

        private void UpdateHasLegs()
        {
            for (int i = 0; i < infos.Count; i++)
            {
                if (infos[i].isLeg)
                {
                    HasLegs = true;
                    return;
                }
            }

            HasLegs = false;
        }

        /// <summary>
        /// 部位分類器を指定してボーン階層を受け取り初期化する(脚や腕を識別するため)
        /// </summary>
        /// <param name="bones">対象のボーン群</param>
        /// <param name="analyzer">脚や腕を判定する部位分類器</param>
        public void Initialize(Transform[] bones, SkeletonPartAnalyzer analyzer)
        {
            partAnalyzer = analyzer;
            Initialize(bones);
        }

        /// <summary>
        /// 部位分類器で各ボーンの脚や腕を判定し、BoneInfoへ反映する
        /// あわせて付属肢ごとに連番(limbIndex)を振り、左右交互の位相に使う
        /// </summary>
        /// <param name="bones">対象のボーン群</param>
        private void AssignParts(Transform[] bones)
        {
            if (partAnalyzer == null)
            {
                return;
            }

            var partMap = partAnalyzer.ClassifyBones(bones);

            var limbIndexMap = new Dictionary<Transform, int>();
            int nextLimbIndex = 0;

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                BonePart part = BonePart.Body;
                if (partMap.TryGetValue(info.transform, out BonePart p))
                {
                    part = p;
                }

                info.isLeg = part == BonePart.Leg;
                info.isArm = part == BonePart.Arm;
                info.isBack = part == BonePart.Back;
                info.isFront = part == BonePart.Front;

                Transform limbRootTransform = FindLimbRoot(info.transform, partMap);
                if (limbRootTransform != null)
                {
                    if (!limbIndexMap.TryGetValue(limbRootTransform, out int idx))
                    {
                        idx = nextLimbIndex++;
                        limbIndexMap[limbRootTransform] = idx;
                    }

                    info.limbIndex = idx;
                }

                infos[i] = info;
            }

            // 左右位置ではなく脚(腕)を"別々の枝"に分け枝ごとに逆位相を割り当てる
            // 左右脚が中央寄りで重なっていても別枝なら確実に交互になる
            AssignAlternatingSwingByBranch(b => b.isLeg, firstSign: 1f);
            AssignAlternatingSwingByBranch(b => b.isArm, firstSign: -1f);
        }

        /// <summary>
        /// 指定部位のボーンを分岐(fork)ごとの独立枝に分け枝ごとに交互の符号を割り当てる
        /// 共有トランク(枝が1本しかない幹)は振らないよう符号0にする
        /// </summary>
        /// <param name="isTargetPart">対象部位か判定する関数</param>
        /// <param name="firstSign">先頭枝に与える符号</param>
        private void AssignAlternatingSwingByBranch(System.Func<BoneInfo, bool> isTargetPart, float firstSign)
        {
            // ボーンからinfosの添字を引く辞書
            var indexOf = new Dictionary<Transform, int>(infos.Count);
            for (int i = 0; i < infos.Count; i++)
            {
                if (infos[i].transform != null)
                {
                    indexOf[infos[i].transform] = i;
                }
            }

            // 対象部位ボーンの集合と親添字を求める
            var isTarget = new bool[infos.Count];
            var parentIndex = new int[infos.Count];
            for (int i = 0; i < infos.Count; i++)
            {
                parentIndex[i] = -1;
                if (infos[i].transform == null)
                {
                    continue;
                }

                isTarget[i] = isTargetPart(infos[i]);
                Transform p = infos[i].transform.parent;
                if (p != null && indexOf.TryGetValue(p, out int pi))
                {
                    parentIndex[i] = pi;
                }
            }

            // 対象部位の子の数を数える(分岐点fork検出に使う)
            var targetChildCount = new int[infos.Count];
            for (int i = 0; i < infos.Count; i++)
            {
                if (isTarget[i] && parentIndex[i] >= 0 && isTarget[parentIndex[i]])
                {
                    targetChildCount[parentIndex[i]]++;
                }
            }

            // 各対象ボーンの区間頭(fork直下や部位境界までさかのぼった枝の先頭)を求める
            var segmentHead = new int[infos.Count];
            for (int i = 0; i < infos.Count; i++)
            {
                segmentHead[i] = -1;
                if (!isTarget[i])
                {
                    continue;
                }

                int head = i;
                while (true)
                {
                    int p = parentIndex[head];
                    // 親が対象部位で分岐のない一本道の間だけさかのぼる
                    if (p >= 0 && isTarget[p] && targetChildCount[p] == 1)
                    {
                        head = p;
                    }
                    else
                    {
                        break;
                    }
                }

                segmentHead[i] = head;
            }

            // 区間頭を接続先(親)ごとにまとめる 同じ接続先に複数頭があれば独立肢(交互に振る対象)
            var headsByAttach = new Dictionary<int, List<int>>();
            var distinctHeads = new HashSet<int>();
            for (int i = 0; i < infos.Count; i++)
            {
                if (segmentHead[i] < 0)
                {
                    continue;
                }

                distinctHeads.Add(segmentHead[i]);
            }

            foreach (int head in distinctHeads)
            {
                int attach = parentIndex[head];
                if (!headsByAttach.TryGetValue(attach, out List<int> list))
                {
                    list = new List<int>();
                    headsByAttach[attach] = list;
                }

                list.Add(head);
            }

            // 区間頭ごとの符号を決める
            Vector3 forwardWorld = ResolveLocomotionForwardWorldAxis();
            Vector3 centerWorld = rootBone != null ? rootBone.position : transform.position;
            var signOfHead = new Dictionary<int, float>();

            foreach (var pair in headsByAttach)
            {
                List<int> heads = pair.Value;
                if (heads.Count < 2)
                {
                    // 分岐のない共有トランクは振らない
                    signOfHead[heads[0]] = 0f;
                    continue;
                }

                // 同じ接続先から複数の肢が出るので前後位置で並べて交互に振る
                heads.Sort((a, b) =>
                    Vector3.Dot(infos[a].transform.position - centerWorld, forwardWorld)
                        .CompareTo(Vector3.Dot(infos[b].transform.position - centerWorld, forwardWorld)));
                for (int h = 0; h < heads.Count; h++)
                {
                    signOfHead[heads[h]] = h % 2 == 0 ? firstSign : -firstSign;
                }
            }

            // 各ボーンへ区間頭の符号を反映する
            for (int i = 0; i < infos.Count; i++)
            {
                if (segmentHead[i] < 0)
                {
                    continue;
                }

                if (signOfHead.TryGetValue(segmentHead[i], out float sign))
                {
                    BoneInfo info = infos[i];
                    info.lateralSign = sign;
                    infos[i] = info;
                }
            }
        }

        /// <summary>
        /// あるボーンが属する付属肢の付け根(部位がBody以外になる最も浅いボーン)を返す
        /// </summary>
        /// <param name="bone">対象ボーン</param>
        /// <param name="partMap">ボーンから部位への辞書</param>
        /// <returns>付属肢の付け根Transform、付属肢でなければnull</returns>
        private Transform FindLimbRoot(Transform bone, Dictionary<Transform, BonePart> partMap)
        {
            Transform current = bone;
            Transform limbRoot = null;

            while (current != null)
            {
                if (partMap.TryGetValue(current, out BonePart part) && part != BonePart.Body)
                {
                    limbRoot = current;
                }

                current = current.parent;
            }

            return limbRoot;
        }

        /// <summary>
        /// 指定のモーションを再生する
        /// </summary>
        /// <param name="type">再生するモーション</param>
        /// <param name="duration">限定モーションの長さ(秒)0以下なら既定値</param>
        public void Play(MotionType type, float duration = -1f)
        {
            if (type == MotionType.None)
            {
                currentMotion = MotionType.None;
                ResetPose();
                return;
            }

            if (!IsAttackMotion(type) && !IsStepMotion(type) && !IsHitMotion(type) && currentMotion == type)
            {
                return;
            }

            if (IsAttackMotion(type) || IsStepMotion(type) || IsHitMotion(type))
            {
                bool comingFromCharge = currentMotion == MotionType.AttackCharge && IsAttackMotion(type);
                strikeChargeCarry = comingFromCharge
                    ? Mathf.Clamp01(lastChargeIntensity * MotionSettings.ChargeCarryWeight)
                    : 0f;

                previousMotion = ResolvePreviousMotionBeforeTimedMotion();
                attackStartTime = time;
                activeMotionDuration = ResolveTimedMotionDuration(type, duration);

                // 攻撃開始時の位置とルート回転を記録する(終了時に復元するため)
                attackStartPosition = transform.position;
                attackStartWorldRotation = transform.rotation;
                rootBoneBaseRotation = rootBone != null ? rootBone.localRotation : Quaternion.identity;
                if (rootBone != null && (!comingFromCharge || !rootBoneLocalPositionSaved))
                {
                    rootBoneBaseLocalPosition = rootBone.localPosition;
                    rootBoneLocalPositionSaved = true;
                }
                else if (rootBone == null)
                {
                    rootBoneLocalPositionSaved = false;
                }

                attackStateSaved = true;
                chargeRootSaved = false;
            }

            currentMotion = type;
        }

        /// <summary>
        /// 指定攻撃の溜めモーションを再生する
        /// </summary>
        /// <param name="upcomingAttack">この後に出す攻撃</param>
        /// <param name="duration">溜め時間(秒)</param>
        public void PlayCharge(MotionType upcomingAttack, float duration)
        {
            if (!IsAttackMotion(upcomingAttack))
            {
                return;
            }

            chargeUpcomingAttack = upcomingAttack;
            chargeDuration = Mathf.Max(0.05f, duration);
            chargeStartTime = time;
            chargeBasePosition = transform.position;
            chargeRootSaved = true;
            lastChargeIntensity = 0f;
            strikeChargeCarry = 0f;
            if (rootBone != null)
            {
                rootBoneBaseLocalPosition = rootBone.localPosition;
                rootBoneLocalPositionSaved = true;
            }

            if (currentMotion != MotionType.AttackCharge)
            {
                previousMotion = ResolvePreviousMotionBeforeTimedMotion();
            }

            currentMotion = MotionType.AttackCharge;
        }

        /// <summary>
        /// 指定のモーションが攻撃かどうかを返す
        /// </summary>
        /// <param name="type">判定するモーション</param>
        /// <returns>攻撃ならtrue</returns>
        public static bool IsAttackMotion(MotionType type)
        {
            return type == MotionType.Tackle
                || type == MotionType.Punch
                || type == MotionType.Kick
                || type == MotionType.SpinTackle
                || type == MotionType.TailWhip
                || type == MotionType.Headbutt
                || type == MotionType.Elbow
                || type == MotionType.Stomp
                || type == MotionType.BodySlam
                || type == MotionType.Uppercut
                || type == MotionType.Knee
                || type == MotionType.ShoulderRam
                || type == MotionType.BellyFlop
                || type == MotionType.HipCheck
                || type == MotionType.GroundPound
                || type == MotionType.Slap
                || type == MotionType.LowSweep
                || type == MotionType.Bite;
        }

        /// <summary>
        /// 指定のモーションが攻撃溜めかどうかを返す
        /// </summary>
        /// <param name="type">判定するモーション</param>
        /// <returns>攻撃溜めならtrue</returns>
        public static bool IsChargeMotion(MotionType type)
        {
            return type == MotionType.AttackCharge;
        }

        /// <summary>
        /// 指定のモーションがステップかどうかを返す
        /// </summary>
        /// <param name="type">判定するモーション</param>
        /// <returns>ステップならtrue</returns>
        public static bool IsStepMotion(MotionType type)
        {
            return type == MotionType.StepForward
                || type == MotionType.StepBackward;
        }

        /// <summary>
        /// 指定のモーションが被弾かどうかを返す
        /// </summary>
        /// <param name="type">判定するモーション</param>
        /// <returns>被弾ならtrue</returns>
        public static bool IsHitMotion(MotionType type)
        {
            return type == MotionType.Hit;
        }

        /// <summary>
        /// フィールド配置変更後に攻撃モーションの基準位置を同期する
        /// </summary>
        public void OnLayoutPositionChanged()
        {
            if (attackStateSaved)
            {
                attackStartPosition = transform.position;
                attackStartWorldRotation = transform.rotation;
            }

            if (chargeRootSaved)
            {
                chargeBasePosition = transform.position;
            }
        }

        /// <summary>
        /// 戦闘中に敵の右側へはみ出さないよう位置制約を設定する
        /// </summary>
        /// <param name="enemy">敵モデルのTransform</param>
        /// <param name="approachAxisFromEnemy">敵からプレイヤー側への単位方向</param>
        /// <param name="minSeparation">最小間隔</param>
        /// <param name="maxSeparation">最大間隔</param>
        public void ConfigureBattlePositionConstraint(
            Transform enemy,
            Vector3 approachAxisFromEnemy,
            float minSeparation,
            float maxSeparation = -1f)
        {
            battleConstraintEnemy = enemy;
            battleConstraintApproachAxis = approachAxisFromEnemy;
            battleConstraintMinSeparation = Mathf.Max(0.1f, minSeparation);
            battleConstraintMaxSeparation = maxSeparation > 0f ? maxSeparation : battleConstraintMinSeparation;
            hasBattlePositionConstraint = enemy != null;
        }

        /// <summary>
        /// 戦闘用位置制約を解除する
        /// </summary>
        public void ClearBattlePositionConstraint()
        {
            hasBattlePositionConstraint = false;
            battleConstraintEnemy = null;
        }

        /// <summary>
        /// 育成表示へ戻す前にモーション状態を初期化する
        /// </summary>
        public void ResetForTrainingDisplay()
        {
            attackStateSaved = false;
            chargeRootSaved = false;
            isFinishingTimedMotion = false;
            enableRootTranslation = false;
            ClearBattlePositionConstraint();
            currentMotion = MotionType.None;
            ResetPose();
            Play(MotionType.Idle);
        }

        /// <summary>
        /// 勝利演出など配置前に攻撃状態を捨てIdle基準ポーズへ戻す
        /// </summary>
        public void PreparePresentationIdle()
        {
            attackStateSaved = false;
            chargeRootSaved = false;
            isFinishingTimedMotion = false;
            ClearBattlePositionConstraint();
            ResetPose();
            currentMotion = MotionType.Idle;
            time = 0f;
        }

        /// <summary>
        /// すべてのボーンを基準ポーズへ戻す
        /// </summary>
        public void ResetPose()
        {
            for (int i = 0; i < infos.Count; i++)
            {
                if (infos[i].transform != null)
                {
                    infos[i].transform.localRotation = infos[i].baseLocalRotation;
                }
            }
        }

        private void Update()
        {
            if (infos.Count == 0)
            {
                return;
            }

            if (currentMotion != MotionType.None)
            {
                float motionDelta = currentMotion == MotionType.Hit && GameplayTime.IsHitStopActive
                    ? Time.unscaledDeltaTime
                    : GameplayTime.DeltaTime;
                time += motionDelta;

                switch (currentMotion)
                {
                    case MotionType.Idle:
                        ApplyIdle();
                        break;
                    case MotionType.Run:
                        ApplyRun();
                        break;
                    case MotionType.LegRun:
                        ApplyLegRun();
                        break;
                    case MotionType.Tackle:
                        ApplyTackle();
                        break;
                    case MotionType.Punch:
                        ApplyPunch();
                        break;
                    case MotionType.Kick:
                        ApplyKick();
                        break;
                    case MotionType.SpinTackle:
                        ApplySpinTackle();
                        break;
                    case MotionType.TailWhip:
                        ApplyTailWhip();
                        break;
                    case MotionType.AttackCharge:
                        ApplyAttackCharge();
                        break;
                    case MotionType.Headbutt:
                        ApplyHeadbutt();
                        break;
                    case MotionType.Elbow:
                        ApplyElbow();
                        break;
                    case MotionType.Stomp:
                        ApplyStomp();
                        break;
                    case MotionType.BodySlam:
                        ApplyBodySlam();
                        break;
                    case MotionType.Uppercut:
                        ApplyUppercut();
                        break;
                    case MotionType.Knee:
                        ApplyKnee();
                        break;
                    case MotionType.ShoulderRam:
                        ApplyShoulderRam();
                        break;
                    case MotionType.BellyFlop:
                        ApplyBellyFlop();
                        break;
                    case MotionType.HipCheck:
                        ApplyHipCheck();
                        break;
                    case MotionType.GroundPound:
                        ApplyGroundPound();
                        break;
                    case MotionType.Slap:
                        ApplySlap();
                        break;
                    case MotionType.LowSweep:
                        ApplyLowSweep();
                        break;
                    case MotionType.Bite:
                        ApplyBite();
                        break;
                    case MotionType.StepForward:
                        ApplyStepForward();
                        break;
                    case MotionType.StepBackward:
                        ApplyStepBackward();
                        break;
                    case MotionType.Hit:
                        ApplyHit();
                        break;
                }
            }

            if (hasBattlePositionConstraint)
            {
                EnforceBattlePositionConstraint();
            }
        }

        private float ResolveTimedMotionDuration(MotionType type, float duration)
        {
            if (duration > 0f)
            {
                return duration;
            }

            return IsStepMotion(type) ? MotionSettings.StepDuration : IsHitMotion(type) ? MotionSettings.HitDuration : MotionSettings.AttackDuration;
        }

        /// <summary>
        /// 足のない走り、手足を左右で逆向きに前後(矢状面)へ振って歩く
        /// </summary>
        private void ApplyRun()
        {
            float w = time * MotionSettings.RunFrequency;
            Vector3 sagittalAxis = ResolveLocomotionSwingWorldAxis();
            LogLocomotionDiagnostics("Run", sagittalAxis);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null || info.transform == rootBone)
                {
                    if (info.transform != null)
                    {
                        info.transform.localRotation = info.baseLocalRotation;
                    }

                    continue;
                }

                if (info.isLimb)
                {
                    float swing = Mathf.Sin(w) * MotionSettings.RunLimbAmplitude * info.lateralSign;
                    info.transform.localRotation = WorldSwingLocalRotation(info, sagittalAxis, swing);
                }
                else
                {
                    info.transform.localRotation = info.baseLocalRotation;
                }
            }
        }

        /// <summary>
        /// 足のある走り、脚を左右で逆向きに前後へ振り腕は脚と逆位相で振って歩く
        /// </summary>
        private void ApplyLegRun()
        {
            float w = time * MotionSettings.LegRunFrequency;
            Vector3 sagittalAxis = ResolveLocomotionSwingWorldAxis();
            LogLocomotionDiagnostics("LegRun", sagittalAxis);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null || info.transform == rootBone)
                {
                    if (info.transform != null)
                    {
                        info.transform.localRotation = info.baseLocalRotation;
                    }

                    continue;
                }

                if (info.isLeg)
                {
                    // 左右のボーンで符号が逆になるので片脚前片脚後ろで交互になる
                    float swing = Mathf.Sin(w) * MotionSettings.LegRunLegAmplitude * info.lateralSign;
                    info.transform.localRotation = WorldSwingLocalRotation(info, sagittalAxis, swing);
                }
                else if (info.isArm)
                {
                    // 腕は同じ側の脚と逆位相にして自然な相互振りにする
                    float swing = Mathf.Sin(w) * MotionSettings.LegRunArmAmplitude * (-info.lateralSign);
                    info.transform.localRotation = WorldSwingLocalRotation(info, sagittalAxis, swing);
                }
                else
                {
                    info.transform.localRotation = info.baseLocalRotation;
                }
            }
        }

        /// <summary>
        /// 歩行の実行時状態を一度だけログ出力する(原因特定用)
        /// </summary>
        /// <param name="motionName">再生中の歩行名</param>
        /// <param name="sagittalAxis">前後スイングに使うワールド軸</param>
        private void LogLocomotionDiagnostics(string motionName, Vector3 sagittalAxis)
        {
            if (loggedLocomotionDiagnostics)
            {
                return;
            }

            loggedLocomotionDiagnostics = true;

            int legBones = 0;
            int armBones = 0;
            var legSigns = new HashSet<float>();
            for (int i = 0; i < infos.Count; i++)
            {
                if (infos[i].transform == null)
                {
                    continue;
                }

                if (infos[i].isLeg)
                {
                    legBones++;
                    legSigns.Add(infos[i].lateralSign);
                }

                if (infos[i].isArm)
                {
                    armBones++;
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.Append($"[歩行診断] motion={motionName} HasLegs={HasLegs} 脚ボーン={legBones} 腕ボーン={armBones}");
            sb.Append($" 脚の符号種類数={legSigns.Count}(2なら左右分離OK)");

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform != null && info.isLeg)
                {
                    Transform p = info.transform.parent;
                    sb.Append($"\n  脚 {info.transform.name} parent={(p != null ? p.name : "null")} sign={info.lateralSign} pos={info.transform.position}");
                }
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 体当たり、体を前傾させて前方へ突進し、戻る
        /// </summary>
        private void ApplyTackle()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            // 0->1->0 の山なりで前進して戻る
            float strike = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, strike) * MotionSettings.TackleDistance);

            // 全身を前傾させる(負のときは溜めの後傾)
            float lean = strike * MotionSettings.TackleLeanAngle;
            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(lean * depthFactor, ResolveBendAxis(MotionSettings.AttackBendAxis));
            }
        }

        /// <summary>
        /// パンチ、腕を前方へ振り出して戻る、わずかに前進する
        /// </summary>
        private void ApplyPunch()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float strike = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, strike) * MotionSettings.PunchDistance);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                float tip = 0.55f + (maxDepth > 0 ? (float)info.depth / maxDepth : 1f) * 0.45f;
                if (info.isArm)
                {
                    float armAngle = ResolveSwingAngle(
                        strike,
                        MotionSettings.ChargeLimbPullbackAngle * 1.2f,
                        MotionSettings.PunchAmplitude);
                    // 先端ほど大きくひねりを足す
                    Quaternion swing = Quaternion.AngleAxis(armAngle * tip, ResolveBendAxis(MotionSettings.AttackBendAxis));
                    Quaternion twist = Quaternion.AngleAxis(
                        Mathf.Max(0f, strike) * 18f * info.lateralSign * tip,
                        Vector3.up);
                    info.transform.localRotation = info.baseLocalRotation * swing * twist;
                }
                else if (info.isLeg)
                {
                    float brace = Mathf.Max(0f, strike) * MotionSettings.ChargeLimbPullbackAngle * 0.2f;
                    info.transform.localRotation = info.baseLocalRotation
                        * Quaternion.AngleAxis(brace * tip, ResolveBendAxis(MotionSettings.LegRunSwingAxis));
                }
                else
                {
                    float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                    float bodyTwist = -strike * MotionSettings.ChargePullbackAngle * 0.55f * depthFactor;
                    info.transform.localRotation = info.baseLocalRotation
                        * Quaternion.AngleAxis(bodyTwist, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
            }
        }

        /// <summary>
        /// キック、脚を前方へ振り上げて戻る、わずかに前進する
        /// </summary>
        private void ApplyKick()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float strike = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, strike) * MotionSettings.KickDistance);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                float tip = 0.55f + (maxDepth > 0 ? (float)info.depth / maxDepth : 1f) * 0.45f;
                if (info.isLeg)
                {
                    float legAngle = ResolveSwingAngle(
                        strike,
                        MotionSettings.ChargeLimbPullbackAngle * 1.35f,
                        MotionSettings.KickAmplitude);
                    info.transform.localRotation = info.baseLocalRotation
                        * Quaternion.AngleAxis(legAngle * tip, ResolveBendAxis(MotionSettings.AttackBendAxis));
                }
                else if (info.isArm)
                {
                    float counter = Mathf.Max(0f, strike) * MotionSettings.ChargeLimbPullbackAngle * 0.35f;
                    info.transform.localRotation = info.baseLocalRotation
                        * Quaternion.AngleAxis(counter * tip, ResolveBendAxis(MotionSettings.LimbSwingAxis));
                }
                else
                {
                    float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                    float bodyLean = -strike * MotionSettings.ChargePullbackAngle * 0.45f * depthFactor;
                    info.transform.localRotation = info.baseLocalRotation
                        * Quaternion.AngleAxis(bodyLean, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
            }
        }

        /// <summary>
        /// 回転体当たり、ルートボーンをY軸まわりに高速回転させながら前進し、戻る
        /// </summary>
        private void ApplySpinTackle()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float strike = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, strike) * MotionSettings.SpinTackleDistance);

            float spin = (time - attackStartTime) * MotionSettings.SpinTackleSpeed;
            transform.rotation = Quaternion.AngleAxis(spin, Vector3.up) * attackStartWorldRotation;
            if (rootBone != null)
            {
                rootBone.localRotation = rootBoneBaseRotation;
            }

            float lean = strike * MotionSettings.TackleLeanAngle * 0.65f;
            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null || info.transform == rootBone)
                {
                    continue;
                }

                float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                if (info.isLimb)
                {
                    float limbLean = lean * 0.85f * (0.6f + info.depth * 0.15f);
                    info.transform.localRotation = info.baseLocalRotation
                        * Quaternion.AngleAxis(limbLean, ResolveBendAxis(MotionSettings.LimbSwingAxis));
                }
                else
                {
                    info.transform.localRotation = info.baseLocalRotation
                        * Quaternion.AngleAxis(lean * depthFactor, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
            }
        }

        /// <summary>
        /// しっぽ攻撃、尻尾(Back)を左右に薙ぎ払う、その場で振る
        /// </summary>
        private void ApplyTailWhip()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float swing = EvaluateStrikeEnvelope(u);
            float whip = ResolveSwingAngle(
                swing,
                MotionSettings.ChargeLimbPullbackAngle * 1.25f,
                MotionSettings.TailWhipAmplitude);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                // 尻尾(Back)だけを薙ぎ払う、それ以外は基準ポーズ
                if (info.isBack)
                {
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(whip, ResolveBendAxis(MotionSettings.TailWhipAxis));
                }
                else
                {
                    info.transform.localRotation = info.baseLocalRotation;
                }
            }
        }

        /// <summary>
        /// 攻撃溜め、技種別に後ろへ引いて力を溜め一瞬キープする
        /// </summary>
        private void ApplyAttackCharge()
        {
            float elapsed = time - chargeStartTime;
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(chargeDuration, 0.01f));
            float buildEnd = Mathf.Clamp(MotionSettings.ChargeBuildRatio, 0.2f, 0.7f);
            float holdEnd = Mathf.Clamp(buildEnd + MotionSettings.ChargeHoldRatio, buildEnd + 0.05f, 0.92f);

            float intensity;
            if (normalized < buildEnd)
            {
                float t = normalized / buildEnd;
                intensity = t * t * (3f - 2f * t);
            }
            else if (normalized < holdEnd)
            {
                intensity = 1f;
            }
            else
            {
                float t = (normalized - holdEnd) / Mathf.Max(1f - holdEnd, 0.01f);
                // 溜め終わりは解放せず震えを強めて攻撃へつなぐ
                intensity = 1f + Mathf.Sin(t * Mathf.PI * 6f) * 0.08f;
            }

            lastChargeIntensity = Mathf.Clamp01(intensity);
            ApplyChargeBackwardMove(intensity * MotionSettings.ChargePullbackDistance);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                float angle;
                Vector3 axis;

                switch (chargeUpcomingAttack)
                {
                    case MotionType.Kick:
                    case MotionType.Stomp:
                    case MotionType.Knee:
                        if (info.isLeg)
                        {
                            angle = -intensity * MotionSettings.ChargeLimbPullbackAngle * 1.35f;
                            axis = MotionSettings.AttackBendAxis;
                        }
                        else if (info.isArm)
                        {
                            angle = intensity * MotionSettings.ChargeLimbPullbackAngle * 0.35f;
                            axis = MotionSettings.LimbSwingAxis;
                        }
                        else
                        {
                            angle = intensity * MotionSettings.ChargePullbackAngle * 0.55f * depthFactor;
                            axis = MotionSettings.SpineBendAxis;
                        }
                        break;

                    case MotionType.Punch:
                    case MotionType.Elbow:
                    case MotionType.Uppercut:
                        if (info.isArm)
                        {
                            angle = -intensity * MotionSettings.ChargeLimbPullbackAngle * 1.2f;
                            axis = MotionSettings.AttackBendAxis;
                        }
                        else if (info.isLeg)
                        {
                            angle = intensity * MotionSettings.ChargeLimbPullbackAngle * 0.25f;
                            axis = MotionSettings.LegRunSwingAxis;
                        }
                        else
                        {
                            angle = intensity * MotionSettings.ChargePullbackAngle * 0.65f * depthFactor;
                            axis = MotionSettings.SpineBendAxis;
                        }
                        break;

                    case MotionType.TailWhip:
                        if (info.isBack)
                        {
                            angle = -intensity * MotionSettings.ChargeLimbPullbackAngle * 1.25f;
                            axis = MotionSettings.TailWhipAxis;
                        }
                        else
                        {
                            angle = intensity * MotionSettings.ChargePullbackAngle * 0.5f * depthFactor;
                            axis = MotionSettings.SpineBendAxis;
                        }
                        break;

                    case MotionType.Headbutt:
                        if (info.isFront)
                        {
                            angle = -intensity * MotionSettings.ChargeLimbPullbackAngle;
                            axis = MotionSettings.AttackBendAxis;
                        }
                        else
                        {
                            angle = intensity * MotionSettings.ChargePullbackAngle * 1.1f * depthFactor;
                            axis = MotionSettings.SpineBendAxis;
                        }
                        break;

                    case MotionType.Tackle:
                    case MotionType.SpinTackle:
                    case MotionType.BodySlam:
                    case MotionType.ShoulderRam:
                    case MotionType.BellyFlop:
                    case MotionType.HipCheck:
                    case MotionType.GroundPound:
                        angle = intensity * MotionSettings.ChargePullbackAngle * 1.15f * depthFactor;
                        axis = MotionSettings.SpineBendAxis;
                        break;

                    case MotionType.Slap:
                        if (info.isArm)
                        {
                            angle = -intensity * MotionSettings.ChargeLimbPullbackAngle * 1.1f;
                            axis = MotionSettings.LimbSwingAxis;
                        }
                        else
                        {
                            angle = intensity * MotionSettings.ChargePullbackAngle * 0.55f * depthFactor;
                            axis = MotionSettings.SpineBendAxis;
                        }
                        break;

                    case MotionType.LowSweep:
                        if (info.isLeg)
                        {
                            angle = intensity * MotionSettings.ChargeLimbPullbackAngle * 0.9f;
                            axis = MotionSettings.LegRunSwingAxis;
                        }
                        else
                        {
                            angle = intensity * MotionSettings.ChargePullbackAngle * 0.45f * depthFactor;
                            axis = MotionSettings.SpineBendAxis;
                        }
                        break;

                    case MotionType.Bite:
                        if (info.isFront)
                        {
                            angle = -intensity * MotionSettings.ChargeLimbPullbackAngle * 1.15f;
                            axis = MotionSettings.AttackBendAxis;
                        }
                        else
                        {
                            angle = intensity * MotionSettings.ChargePullbackAngle * 0.75f * depthFactor;
                            axis = MotionSettings.SpineBendAxis;
                        }
                        break;

                    default:
                        angle = intensity * MotionSettings.ChargePullbackAngle * depthFactor;
                        axis = MotionSettings.SpineBendAxis;
                        break;
                }

                info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(angle, ResolveBendAxis(axis));
            }
        }

        /// <summary>
        /// 頭突き、前部を前方へ突き出す
        /// </summary>
        private void ApplyHeadbutt()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float thrust = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, thrust) * MotionSettings.HeadbuttDistance);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                if (info.isFront)
                {
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(thrust * MotionSettings.HeadbuttAmplitude, ResolveBendAxis(MotionSettings.AttackBendAxis));
                }
                else
                {
                    float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(thrust * MotionSettings.HeadbuttAmplitude * 0.35f * depthFactor, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
            }
        }

        /// <summary>
        /// エルボー、腕を短距離で突き出す
        /// </summary>
        private void ApplyElbow()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float strike = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, strike) * MotionSettings.ElbowDistance);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                if (info.isArm)
                {
                    float armAngle = ResolveSwingAngle(
                        strike,
                        MotionSettings.ChargeLimbPullbackAngle * 1.2f,
                        MotionSettings.ElbowAmplitude);
                    info.transform.localRotation = info.baseLocalRotation
                        * Quaternion.AngleAxis(armAngle, ResolveBendAxis(MotionSettings.AttackBendAxis));
                }
                else
                {
                    float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                    float brace = -strike * MotionSettings.ChargePullbackAngle * 0.25f * depthFactor;
                    info.transform.localRotation = info.baseLocalRotation
                        * Quaternion.AngleAxis(brace, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
            }
        }

        /// <summary>
        /// ストンプ、脚を上げて振り下ろす
        /// </summary>
        private void ApplyStomp()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float anticipation = MotionSettings.AttackAnticipationRatio;
            float liftEnd = 0.32f;
            float holdEnd = liftEnd + anticipation;
            float slamEnd = holdEnd + MotionSettings.AttackStrikePortion;

            float lift = u < liftEnd ? u / liftEnd : 1f;
            float slam = 0f;
            if (u >= holdEnd && u < slamEnd)
            {
                float t = (u - holdEnd) / Mathf.Max(slamEnd - holdEnd, 0.01f);
                slam = 1f - Mathf.Pow(1f - t, 3f);
            }
            else if (u >= slamEnd)
            {
                float t = (u - slamEnd) / Mathf.Max(1f - slamEnd, 0.01f);
                slam = 1f - t * t;
            }

            float legAngle = u < holdEnd
                ? -lift * MotionSettings.StompLiftAngle
                : slam * MotionSettings.StompSlamAngle;

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                if (info.isLeg)
                {
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(legAngle, ResolveBendAxis(MotionSettings.AttackBendAxis));
                }
                else
                {
                    float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                    float bodyAngle = lift * MotionSettings.StompLiftAngle * 0.25f * depthFactor;
                    if (u >= holdEnd)
                    {
                        bodyAngle -= slam * MotionSettings.StompSlamAngle * 0.2f * depthFactor;
                    }

                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(bodyAngle, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
            }
        }

        /// <summary>
        /// アッパー、腕を下から突き上げる
        /// </summary>
        private void ApplyUppercut()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float strike = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, strike) * MotionSettings.ElbowDistance * 0.6f);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                if (info.isArm)
                {
                    float angle = -strike * MotionSettings.ElbowAmplitude * 0.95f;
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(angle, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
                else
                {
                    float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(strike * MotionSettings.HeadbuttAmplitude * 0.2f * depthFactor, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
            }
        }

        /// <summary>
        /// 膝蹴り、脚を前方へ突き上げる
        /// </summary>
        private void ApplyKnee()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float strike = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, strike) * MotionSettings.KickDistance * 0.55f);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                if (info.isLeg)
                {
                    float angle = -strike * MotionSettings.KickAmplitude * 0.75f;
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(angle, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
                else
                {
                    float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                    float brace = strike * MotionSettings.ChargePullbackAngle * 0.2f * depthFactor;
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(brace, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
            }
        }

        /// <summary>
        /// ボディスラム、全体を叩きつける
        /// </summary>
        private void ApplyBodySlam()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float move = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, move) * MotionSettings.BodySlamDistance);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(move * MotionSettings.BodySlamLeanAngle * depthFactor, ResolveBendAxis(MotionSettings.AttackBendAxis));
            }
        }

        /// <summary>
        /// ショルダー、体を横に振りながら突進する
        /// </summary>
        private void ApplyShoulderRam()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float strike = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, strike) * MotionSettings.TackleDistance * 0.9f);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                float sideSign = (info.limbIndex & 1) == 0 ? 1f : -1f;
                float sideLean = strike * MotionSettings.TackleLeanAngle * 0.55f * sideSign * depthFactor;
                float forwardLean = strike * MotionSettings.TackleLeanAngle * 0.35f * depthFactor;
                Quaternion rotation = Quaternion.AngleAxis(sideLean, ResolveBendAxis(MotionSettings.SpineBendAxis))
                    * Quaternion.AngleAxis(forwardLean, ResolveBendAxis(MotionSettings.AttackBendAxis));
                info.transform.localRotation = info.baseLocalRotation * rotation;
            }
        }

        /// <summary>
        /// のしかかり、跳ね上がって全体を前方へ叩きつける
        /// </summary>
        private void ApplyBellyFlop()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float jumpEnd = 0.34f;
            float slamEnd = jumpEnd + MotionSettings.AttackStrikePortion;
            float intensity;
            if (u < jumpEnd)
            {
                float t = u / jumpEnd;
                intensity = -Mathf.Sin(t * Mathf.PI * 0.5f);
            }
            else if (u < slamEnd)
            {
                float t = (u - jumpEnd) / Mathf.Max(slamEnd - jumpEnd, 0.01f);
                intensity = Mathf.Sin(t * Mathf.PI * 0.5f);
                ApplyForwardMove(Mathf.Max(0f, intensity) * MotionSettings.BodySlamDistance * 1.15f);
            }
            else
            {
                float t = (u - slamEnd) / Mathf.Max(1f - slamEnd, 0.01f);
                intensity = 1f - t;
            }

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                float angle = intensity * MotionSettings.BodySlamLeanAngle * 1.15f * depthFactor;
                info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(angle, ResolveBendAxis(MotionSettings.AttackBendAxis));
            }
        }

        /// <summary>
        /// 腰ブン、回旋しながら体当たりする
        /// </summary>
        private void ApplyHipCheck()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float strike = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, strike) * MotionSettings.TackleDistance * 0.65f);
            transform.rotation = Quaternion.AngleAxis(strike * 42f, Vector3.up) * attackStartWorldRotation;
            if (rootBone != null)
            {
                rootBone.localRotation = rootBoneBaseRotation;
            }

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                float angle = strike * MotionSettings.TackleLeanAngle * 0.75f * depthFactor;
                info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(angle, ResolveBendAxis(MotionSettings.SpineBendAxis));
            }
        }

        /// <summary>
        /// グラウンドパウンド、その場で跳ねて叩きつける
        /// </summary>
        private void ApplyGroundPound()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float liftEnd = 0.3f;
            float holdEnd = liftEnd + MotionSettings.AttackAnticipationRatio;
            float slamEnd = holdEnd + MotionSettings.AttackStrikePortion;
            float intensity;
            if (u < liftEnd)
            {
                intensity = u / liftEnd;
            }
            else if (u < holdEnd)
            {
                intensity = 1f;
            }
            else if (u < slamEnd)
            {
                float t = (u - holdEnd) / Mathf.Max(slamEnd - holdEnd, 0.01f);
                intensity = 1f - t * t;
            }
            else
            {
                intensity = 0f;
            }

            float liftAngle = intensity * MotionSettings.BodySlamLeanAngle * 0.55f;
            float slamAngle = intensity * MotionSettings.StompSlamAngle * 1.1f;
            bool isSlamPhase = u >= holdEnd && u < slamEnd;
            float angle = isSlamPhase ? -slamAngle : liftAngle;

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                Vector3 axis = info.isLeg
                    ? ResolveBendAxis(MotionSettings.AttackBendAxis)
                    : ResolveBendAxis(MotionSettings.SpineBendAxis);
                info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(angle * depthFactor, axis);
            }
        }

        /// <summary>
        /// 平打ち、腕を大きく振り抜く
        /// </summary>
        private void ApplySlap()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float strike = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, strike) * MotionSettings.PunchDistance * 0.55f);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                if (info.isArm)
                {
                    float sideSign = (info.limbIndex & 1) == 0 ? 1f : -1f;
                    float swing = strike * MotionSettings.PunchAmplitude * 0.95f * sideSign;
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(swing, ResolveBendAxis(MotionSettings.LimbSwingAxis));
                }
                else
                {
                    float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                    float twist = strike * MotionSettings.ChargePullbackAngle * 0.35f * depthFactor;
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(twist, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
            }
        }

        /// <summary>
        /// ロースイープ、脚を低く薙ぎ払う
        /// </summary>
        private void ApplyLowSweep()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float strike = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, strike) * MotionSettings.KickDistance * 0.45f);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                if (info.isLeg)
                {
                    float angle = strike * MotionSettings.KickAmplitude * 0.55f;
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(angle, ResolveBendAxis(MotionSettings.LegRunSwingAxis));
                }
                else
                {
                    float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                    float lean = strike * MotionSettings.ChargePullbackAngle * 0.3f * depthFactor;
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(lean, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
            }
        }

        /// <summary>
        /// 噛みつき、前部を突き出して噛む
        /// </summary>
        private void ApplyBite()
        {
            float u = AttackProgress();
            if (u >= 1f)
            {
                FinishAttack();
                return;
            }

            float strike = EvaluateStrikeEnvelope(u);
            ApplyForwardMove(Mathf.Max(0f, strike) * MotionSettings.HeadbuttDistance * 0.85f);

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                if (info.isFront)
                {
                    float snap = strike * MotionSettings.HeadbuttAmplitude * 1.1f;
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(snap, ResolveBendAxis(MotionSettings.AttackBendAxis));
                }
                else
                {
                    float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                    float pull = -strike * MotionSettings.HeadbuttAmplitude * 0.25f * depthFactor;
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(pull, ResolveBendAxis(MotionSettings.SpineBendAxis));
                }
            }
        }

        /// <summary>
        /// 前進ステップ体を少し前へ傾けながら前方へ移動する
        /// </summary>
        private void ApplyStepForward()
        {
            ApplyStepLean(forwardSign: 1f);
        }

        /// <summary>
        /// 後退ステップ体を少し後ろへ傾けながら後方へ移動する
        /// </summary>
        private void ApplyStepBackward()
        {
            ApplyStepLean(forwardSign: -1f);
        }

        /// <summary>
        /// 被弾、後仰ぎとわずかなノックバック後に元のモーションへ戻る
        /// </summary>
        private void ApplyHit()
        {
            float u = TimedMotionProgress();
            if (u >= 1f)
            {
                FinishTimedMotion();
                return;
            }

            float snapEnd = Mathf.Clamp(MotionSettings.HitSnapRatio, 0.12f, 0.4f);
            float recoil;
            if (u < snapEnd)
            {
                float t = u / snapEnd;
                recoil = 1f - Mathf.Pow(1f - t, 3f);
            }
            else
            {
                float t = (u - snapEnd) / Mathf.Max(1f - snapEnd, 0.01f);
                float bounce = Mathf.Sin(t * Mathf.PI) * 0.18f;
                recoil = Mathf.Max(0f, (1f - t * t) + bounce);
            }

            ApplyBackwardMove(recoil * MotionSettings.HitKnockbackDistance);
            if (!enableRootTranslation || hasBattlePositionConstraint)
            {
                ApplyVisualLunge(-recoil * MotionSettings.HitKnockbackDistance);
            }

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                float tip = 0.55f + depthFactor * 0.45f;
                float angle;
                Vector3 axis;

                if (info.isLimb)
                {
                    angle = -recoil * MotionSettings.HitLimbAmplitude * tip;
                    axis = MotionSettings.LimbSwingAxis;
                    Quaternion flail = Quaternion.AngleAxis(
                        recoil * 12f * info.lateralSign,
                        Vector3.up);
                    info.transform.localRotation = info.baseLocalRotation
                        * Quaternion.AngleAxis(angle, ResolveBendAxis(axis))
                        * flail;
                }
                else
                {
                    angle = -recoil * MotionSettings.HitSpineAmplitude * depthFactor;
                    axis = MotionSettings.AttackBendAxis;
                    info.transform.localRotation = info.baseLocalRotation
                        * Quaternion.AngleAxis(angle, ResolveBendAxis(axis));
                }
            }
        }

        /// <summary>
        /// 回転せず進行方向へ体を傾けながら前後へステップする
        /// forwardSignが正で前傾負で後傾になる
        /// </summary>
        private void ApplyStepLean(float forwardSign)
        {
            float u = TimedMotionProgress();
            if (u >= 1f)
            {
                FinishTimedMotion();
                return;
            }

            float moveEase = u * u * (3f - 2f * u);
            ApplyStepMove(moveEase * MotionSettings.StepVisualDistance * forwardSign);

            float leanEnvelope = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
            float lean = leanEnvelope * MotionSettings.StepLeanAngle * forwardSign;
            float limbSwing = leanEnvelope * MotionSettings.StepLimbPrepAngle * forwardSign;

            transform.rotation = attackStartWorldRotation;
            if (rootBone != null)
            {
                rootBone.localRotation = rootBoneBaseRotation;
            }

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null || info.transform == rootBone)
                {
                    continue;
                }

                if (info.isLimb)
                {
                    float angle = limbSwing * (0.35f + info.depth * 0.1f);
                    info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(angle, ResolveBendAxis(MotionSettings.LimbSwingAxis));
                    continue;
                }

                float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                float spineAngle = lean * depthFactor;
                info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(spineAngle, ResolveBendAxis(MotionSettings.AttackBendAxis));
            }
        }

        private void ApplyStepMove(float forwardDistance)
        {
            if (!attackStateSaved || !enableRootTranslation || hasBattlePositionConstraint)
            {
                return;
            }

            Vector3 localForward = MotionSettings.ForwardDirection.sqrMagnitude > 1e-6f ? MotionSettings.ForwardDirection.normalized : Vector3.forward;
            Vector3 dir = attackStartWorldRotation * localForward;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f)
            {
                dir.Normalize();
            }

            Vector3 pos = attackStartPosition + dir * forwardDistance;
            pos.y = attackStartPosition.y;
            transform.position = pos;
            EnforceBattlePositionConstraint();
        }

        /// <summary>
        /// 開始位置からの前進量を反映する(正面方向はSettings参照)
        /// </summary>
        /// <param name="distance">開始位置からの前進距離</param>
        private void ApplyForwardMove(float distance)
        {
            if (!attackStateSaved)
            {
                return;
            }

            if (!enableRootTranslation || hasBattlePositionConstraint)
            {
                ApplyVisualLunge(distance * MotionSettings.VisualLungeScale);
                return;
            }

            Vector3 localForward = MotionSettings.ForwardDirection.sqrMagnitude > 1e-6f ? MotionSettings.ForwardDirection.normalized : Vector3.forward;
            Vector3 dir = transform.TransformDirection(localForward);
            transform.position = attackStartPosition + dir * distance;
            EnforceBattlePositionConstraint();
        }

        /// <summary>
        /// ルート移動が使えないとき見た目だけ踏み込む
        /// </summary>
        private void ApplyVisualLunge(float distance)
        {
            if (!rootBoneLocalPositionSaved || rootBone == null || Mathf.Abs(distance) < 1e-5f)
            {
                return;
            }

            Vector3 localForward = MotionSettings.ForwardDirection.sqrMagnitude > 1e-6f
                ? MotionSettings.ForwardDirection.normalized
                : Vector3.forward;
            rootBone.localPosition = rootBoneBaseLocalPosition + localForward * distance;
        }

        private void RestoreVisualLunge()
        {
            if (!rootBoneLocalPositionSaved || rootBone == null)
            {
                return;
            }

            rootBone.localPosition = rootBoneBaseLocalPosition;
            rootBoneLocalPositionSaved = false;
        }

        private void ApplyBackwardMove(float distance)
        {
            if (!attackStateSaved || !enableRootTranslation || hasBattlePositionConstraint)
            {
                return;
            }

            Vector3 localForward = MotionSettings.ForwardDirection.sqrMagnitude > 1e-6f
                ? MotionSettings.ForwardDirection.normalized
                : Vector3.forward;
            Vector3 dir = transform.TransformDirection(localForward);
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f)
            {
                dir.Normalize();
            }

            Vector3 pos = attackStartPosition - dir * distance;
            pos.y = attackStartPosition.y;
            transform.position = pos;
            EnforceBattlePositionConstraint();
        }

        private void EnforceBattlePositionConstraint()
        {
            if (!hasBattlePositionConstraint || battleConstraintEnemy == null)
            {
                return;
            }

            Vector3 axis = battleConstraintApproachAxis;
            axis.y = 0f;
            if (axis.sqrMagnitude < 1e-6f)
            {
                return;
            }

            axis.Normalize();
            Vector3 enemyPosition = battleConstraintEnemy.position;
            Vector3 enemyPos = enemyPosition;
            enemyPos.y = 0f;

            Vector3 playerPos = transform.position;
            float groundY = enemyPosition.y;
            playerPos.y = 0f;

            float along = Vector3.Dot(playerPos - enemyPos, axis);
            float minAlong = battleConstraintMinSeparation;
            float maxAlong = Mathf.Max(minAlong, battleConstraintMaxSeparation);
            float clampedAlong = Mathf.Clamp(along, minAlong, maxAlong);
            if (Mathf.Approximately(along, clampedAlong))
            {
                return;
            }

            Vector3 corrected = enemyPos + axis * clampedAlong;
            corrected.y = groundY;
            transform.position = corrected;

            if (attackStateSaved)
            {
                attackStartPosition = transform.position;
            }
        }

        /// <summary>
        /// 攻撃の進行度(0..1)を返す
        /// </summary>
        /// <returns>0が開始、1が終了</returns>
        private float AttackProgress()
        {
            return TimedMotionProgress();
        }

        /// <summary>
        /// 攻撃の打撃フェーズを符号付きで返す
        /// 負=溜め残渣の引き・正=振り出し・1超=オーバーシュート
        /// </summary>
        private float EvaluateStrikeEnvelope(float progress)
        {
            float carry = Mathf.Clamp01(strikeChargeCarry);
            float overshoot = Mathf.Clamp(MotionSettings.AttackStrikeOvershoot, 0f, 0.45f);
            float strikePortion = Mathf.Clamp(MotionSettings.AttackStrikePortion, 0.16f, 0.45f);

            // 溜めからの継続時は引き残渣→解放→振りを一続きにする
            if (carry > 0.01f)
            {
                float releaseEnd = 0.12f;
                float strikeEnd = Mathf.Clamp(releaseEnd + strikePortion, releaseEnd + 0.12f, 0.72f);
                float peak = 1f + overshoot;

                if (progress < releaseEnd)
                {
                    float t = progress / Mathf.Max(releaseEnd, 0.01f);
                    float eased = t * t * (3f - 2f * t);
                    return Mathf.Lerp(-carry, 0.08f, eased);
                }

                if (progress < strikeEnd)
                {
                    float t = (progress - releaseEnd) / Mathf.Max(strikeEnd - releaseEnd, 0.01f);
                    float eased = 1f - Mathf.Pow(1f - t, 4f);
                    return Mathf.Lerp(0.08f, peak, eased);
                }

                float returnT = (progress - strikeEnd) / Mathf.Max(1f - strikeEnd, 0.01f);
                float returnEased = 1f - Mathf.Pow(1f - Mathf.Clamp01(returnT), 2f);
                return Mathf.Lerp(peak, 0f, returnEased);
            }

            float anticipation = Mathf.Clamp(MotionSettings.AttackAnticipationRatio, 0.04f, 0.22f);
            float strikeEndNoCarry = Mathf.Clamp(anticipation + strikePortion, anticipation + 0.12f, 0.75f);
            float peakNoCarry = 1f + overshoot;

            if (progress < anticipation)
            {
                float t = progress / Mathf.Max(anticipation, 0.01f);
                // わずかに引いてから振り出す予兆
                return Mathf.Lerp(0f, -0.22f, t * t * (3f - 2f * t));
            }

            if (progress < strikeEndNoCarry)
            {
                float t = (progress - anticipation) / Mathf.Max(strikeEndNoCarry - anticipation, 0.01f);
                float eased = 1f - Mathf.Pow(1f - t, 4f);
                return Mathf.Lerp(-0.22f, peakNoCarry, eased);
            }

            float recoverT = (progress - strikeEndNoCarry) / Mathf.Max(1f - strikeEndNoCarry, 0.01f);
            float recoverEased = 1f - Mathf.Pow(1f - Mathf.Clamp01(recoverT), 2f);
            return Mathf.Lerp(peakNoCarry, 0f, recoverEased);
        }

        /// <summary>
        /// 符号付き振り量から攻撃角を求める
        /// </summary>
        private static float ResolveSwingAngle(float swing, float pullbackAngle, float strikeAmplitude)
        {
            if (swing < 0f)
            {
                return swing * Mathf.Abs(pullbackAngle);
            }

            return swing * strikeAmplitude;
        }

        private void ApplyChargeBackwardMove(float distance)
        {
            if (!chargeRootSaved)
            {
                return;
            }

            if (!enableRootTranslation || hasBattlePositionConstraint)
            {
                if (rootBone != null)
                {
                    Vector3 localForward = MotionSettings.ForwardDirection.sqrMagnitude > 1e-6f
                        ? MotionSettings.ForwardDirection.normalized
                        : Vector3.forward;
                    rootBone.localPosition = rootBoneBaseLocalPosition - localForward * distance * MotionSettings.VisualLungeScale;
                }

                return;
            }

            Vector3 chargeForward = MotionSettings.ForwardDirection.sqrMagnitude > 1e-6f
                ? MotionSettings.ForwardDirection.normalized
                : Vector3.forward;
            Vector3 dir = transform.TransformDirection(chargeForward);
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f)
            {
                dir.Normalize();
            }

            Vector3 pos = chargeBasePosition - dir * distance;
            pos.y = chargeBasePosition.y;
            transform.position = pos;
            EnforceBattlePositionConstraint();
        }

        /// <summary>
        /// 時間制限付きモーションの進行度(0..1)を返す
        /// </summary>
        /// <returns>0が開始、1が終了</returns>
        private float TimedMotionProgress()
        {
            return (time - attackStartTime) / Mathf.Max(activeMotionDuration, 0.01f);
        }
        /// <summary>
        /// 攻撃を終えて位置とルート回転を復元し、元のモーションへ戻る
        /// </summary>
        private void FinishAttack()
        {
            FinishTimedMotion();
        }

        /// <summary>
        /// 時間制限付きモーションを終えて位置と姿勢を復元する
        /// </summary>
        private void FinishTimedMotion()
        {
            if (isFinishingTimedMotion)
            {
                return;
            }

            isFinishingTimedMotion = true;
            try
            {
                if (attackStateSaved)
                {
                    transform.position = attackStartPosition;
                    transform.rotation = attackStartWorldRotation;
                    if (rootBone != null)
                    {
                        rootBone.localRotation = rootBoneBaseRotation;
                    }

                    RestoreVisualLunge();
                    attackStateSaved = false;
                }

                strikeChargeCarry = 0f;
                lastChargeIntensity = 0f;

                MotionType resume = ResolveResumeMotion();
                ResetPose();
                currentMotion = MotionType.None;
                Play(resume);
            }
            finally
            {
                isFinishingTimedMotion = false;
            }
        }

        private MotionType ResolveResumeMotion()
        {
            MotionType resume = previousMotion;
            if (IsAttackMotion(resume) || IsStepMotion(resume) || IsHitMotion(resume) || IsChargeMotion(resume))
            {
                return MotionType.Idle;
            }

            if (!enableRootTranslation && IsLocomotionMotion(resume))
            {
                return MotionType.Idle;
            }

            return resume;
        }

        private MotionType ResolvePreviousMotionBeforeTimedMotion()
        {
            if (IsAttackMotion(currentMotion) || IsStepMotion(currentMotion) || IsHitMotion(currentMotion))
            {
                return MotionType.Idle;
            }

            if (!enableRootTranslation && IsLocomotionMotion(currentMotion))
            {
                return MotionType.Idle;
            }

            return currentMotion;
        }

        private static bool IsLocomotionMotion(MotionType type)
        {
            return type == MotionType.Run || type == MotionType.LegRun;
        }

        /// <summary>
        /// 歩行の前後スイングに使うワールド軸(モデルの左右方向)を返す
        /// リグごとのローカル軸差に依存せず前後へ振るための基準にする
        /// </summary>
        private Vector3 ResolveLocomotionSwingWorldAxis()
        {
            Vector3 localForward = MotionSettings.ForwardDirection.sqrMagnitude > 1e-6f
                ? MotionSettings.ForwardDirection.normalized
                : Vector3.forward;
            Vector3 forward = transform.TransformDirection(localForward);
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = transform.forward;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return right.sqrMagnitude > 1e-6f ? right.normalized : transform.right;
        }

        /// <summary>
        /// 歩行の進行方向(モデル前方)のワールド軸を返す
        /// 枝の前後位置を測る基準に使う
        /// </summary>
        private Vector3 ResolveLocomotionForwardWorldAxis()
        {
            Vector3 localForward = MotionSettings.ForwardDirection.sqrMagnitude > 1e-6f
                ? MotionSettings.ForwardDirection.normalized
                : Vector3.forward;
            Vector3 forward = transform.TransformDirection(localForward);
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = transform.forward;
            }

            return forward.normalized;
        }

        /// <summary>
        /// 指定ワールド軸まわりへ角度だけ回した結果のローカル回転を返す
        /// ボーンのローカル軸のねじれに依存せず常に同じワールド方向へ振るために使う
        /// </summary>
        /// <param name="info">対象ボーン情報</param>
        /// <param name="worldAxis">回転の基準となるワールド軸</param>
        /// <param name="angle">回転角(度)</param>
        /// <returns>親基準のローカル回転</returns>
        private Quaternion WorldSwingLocalRotation(BoneInfo info, Vector3 worldAxis, float angle)
        {
            Transform parent = info.transform.parent;
            Quaternion parentWorld = parent != null ? parent.rotation : Quaternion.identity;
            Quaternion boneRestWorld = parentWorld * info.baseLocalRotation;
            return Quaternion.Inverse(parentWorld) * Quaternion.AngleAxis(angle, worldAxis) * boneRestWorld;
        }

        /// <summary>
        /// ボーン中心線(Z)まわりのねじりを避けた弯曲軸を返す
        /// </summary>
        private static Vector3 ResolveBendAxis(Vector3 preferredAxis)
        {
            Vector3 axis = preferredAxis.sqrMagnitude > 1e-6f ? preferredAxis.normalized : Vector3.right;
            float absX = Mathf.Abs(axis.x);
            float absY = Mathf.Abs(axis.y);
            float absZ = Mathf.Abs(axis.z);
            if (absZ >= absX && absZ >= absY)
            {
                return absY >= absX ? Vector3.up : Vector3.right;
            }

            Vector3 flattened = new Vector3(axis.x, axis.y, 0f);
            return flattened.sqrMagnitude > 1e-6f ? flattened.normalized : Vector3.right;
        }

        /// <summary>
        /// 待機、全体をゆっくり小さく揺らす
        /// </summary>
        private void ApplyIdle()
        {
            float angle = Mathf.Sin(time * MotionSettings.IdleFrequency) * MotionSettings.IdleAmplitude;

            for (int i = 0; i < infos.Count; i++)
            {
                BoneInfo info = infos[i];
                if (info.transform == null)
                {
                    continue;
                }

                float depthFactor = maxDepth > 0 ? (float)info.depth / maxDepth : 1f;
                info.transform.localRotation = info.baseLocalRotation * Quaternion.AngleAxis(angle * depthFactor, ResolveBendAxis(MotionSettings.SpineBendAxis));
            }
        }
    }
}