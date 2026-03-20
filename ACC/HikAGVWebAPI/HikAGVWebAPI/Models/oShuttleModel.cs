using System;

public class oShuttleModel
{
    public string ShuttleId { get; set; }
    public string GustomerName { get; set; }
    public string Ip { get; set; }
    public string Port { get; set; }
    public string ShuttleSize { get; set; }
    public string Enabled { get; set; }
    public string Battery { get; set; }
    public string Status { get; set; }
    public string LastStation { get; set; }
    public string BeginStation { get; set; }
    public string EndStation { get; set; }
    public string PosX { get; set; }
    public string PosY { get; set; }
    public string RobotDir { get; set; }
    public string MapCode { get; set; }
    public DateTime? UpdateTime { get; set; }
    public override string ToString()
    {
        return $@"[Shuttle ID] : {ShuttleId}; [Customer Name] : {GustomerName}; [IP] : {Ip}; [Port] : {Port}; [Shuttle Size] : {ShuttleSize}; [Enabled] : {Enabled}; [Battery] : {Battery}; [Status] : {Status}; [Last Station] : {LastStation}; [Begin Station] : {BeginStation}; [End Station] : {EndStation}; [Pos X] : {PosX}; [Pos Y] : {PosY}; [Robot Dir] : {RobotDir}";
    }
}