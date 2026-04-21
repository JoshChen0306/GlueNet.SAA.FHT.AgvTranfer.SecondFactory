namespace HikAGVWebAPI.App_Start
{
    /// <summary>
    /// DecideNextCrossFloorAction 決策結果種類
    /// </summary>
    public enum CrossFloorDecisionKind
    {
        None,           // 無待派任務 / 無法決定
        SameFloor,      // 有同樓層任務，直接派發該任務
        CooldownHit,    // 冷卻期命中，回傳父任務避免重派預調度
        NeedDispatch    // 需要派預調度任務，將車移至觸發任務起點樓層
    }

    /// <summary>
    /// DecideNextCrossFloorAction 決策結果（純資料，無 side effect）
    /// </summary>
    public class CrossFloorDecision
    {
        public CrossFloorDecisionKind Kind { get; set; }
        public oMissionModel Task { get; set; }
        public string FromFloor { get; set; }
        public string ToFloor { get; set; }

        public static CrossFloorDecision None()
            => new CrossFloorDecision { Kind = CrossFloorDecisionKind.None };

        public static CrossFloorDecision SameFloor(oMissionModel task)
            => new CrossFloorDecision { Kind = CrossFloorDecisionKind.SameFloor, Task = task };

        public static CrossFloorDecision CooldownHit(oMissionModel triggerTask)
            => new CrossFloorDecision { Kind = CrossFloorDecisionKind.CooldownHit, Task = triggerTask };

        public static CrossFloorDecision NeedDispatch(oMissionModel triggerTask, string fromFloor, string toFloor)
            => new CrossFloorDecision
            {
                Kind = CrossFloorDecisionKind.NeedDispatch,
                Task = triggerTask,
                FromFloor = fromFloor,
                ToFloor = toFloor
            };
    }
}
