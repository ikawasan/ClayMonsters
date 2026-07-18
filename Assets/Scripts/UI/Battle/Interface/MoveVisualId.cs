namespace UI.Battle.Interface
{
    /// <summary>
    /// 技アイコンを選ぶためのUI識別子ドメイン列挙に依存しないUI側の語彙
    /// </summary>
    public enum MoveIconId
    {
        None,
        Tackle,
        Punch,
        Kick,
        SpinTackle,
        TailWhip,
        Headbutt,
        Elbow,
        Stomp,
        BodySlam
    }

    /// <summary>
    /// 技の破壊対象部位画像を選ぶためのUI識別子ドメイン列挙に依存しないUI側の語彙
    /// </summary>
    public enum MoveTargetPartId
    {
        None,
        Arm,
        Leg,
        Front,
        Back,
        Body,
        Any
    }
}