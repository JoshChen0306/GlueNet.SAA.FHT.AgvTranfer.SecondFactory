/// <summary>
/// 任務 Model
/// </summary>
public class oMissionModel
{
    /// <summary>
    /// 任務時間
    /// </summary>
    public string TaskDateTime { get; set; }
    public string SerialNo { get; set; }
    /// <summary>
    /// 起點
    /// </summary>
    public string BeginStation { get; set; }
    /// <summary>
    /// 終點
    /// </summary>
    public string EndStation { get; set; }
    public string TaskSource { get; set; }
    /// <summary>
    /// AGV 返回任務單號
    /// </summary>
    public string TaskCode { get; set; }
    /// <summary>
    /// AGV 車號
    /// </summary>
    public string ShuttleId { get; set; }
    //載具編號
    public string RackId { get; set; }
    /// <summary>
    /// 工單編號
    /// </summary>
    public string WorkOrder { get; set; }
    /// <summary>
    /// Remark1
    /// </summary>
    public string Remark1 { get; set; }
    /// <summary>
    /// Remark2
    /// </summary>
    public string Remark2 { get; set; }
    /// <summary>
    /// Remark3
    /// </summary>
    public string Remark3 { get; set; }
    /// <summary>
    /// 任務狀態(初始：null；R：執行中；Y：任務結束)
    /// </summary>
    public string OkFlag { get; set; }
    /// <summary>
    /// AGV 開始時間
    /// </summary>
    public string BeginTime { get; set; }
    public string EndTime { get; set; }
    /// <summary>
    /// 觸發此預調度的 MCS 任務 TaskDateTime（無關聯則 null）
    /// </summary>
    public string ParentTaskDateTime { get; set; }
    public override string ToString()
    {
        return $@"[Task Date Time] : {TaskDateTime}; [Serial No] : {SerialNo}; [Begin Station] : {BeginStation}; [End Station] : {EndStation}; [Task Source] : {TaskSource}; [Task Code] : {TaskCode}; [Shuttle ID] : {ShuttleId}; [Rack ID] : {RackId}; [Work Order] : {WorkOrder}; [Remark1] : {Remark1}; [Remark2] : {Remark2}; [Remark3] : {Remark3}; [Ok Flag] : {OkFlag}; [Begin Time] : {BeginTime}; [End Time] : {EndTime}";
    }
}