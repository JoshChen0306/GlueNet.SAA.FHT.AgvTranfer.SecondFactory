using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace SCP.Models;

public partial class agvDB_1400004Context : DbContext
{
    public agvDB_1400004Context(DbContextOptions<agvDB_1400004Context> options)
        : base(options)
    {
    }

    public virtual DbSet<oMission> oMission { get; set; }

    public virtual DbSet<oNeed> oNeed { get; set; }

    public virtual DbSet<oPairWay> oPairWay { get; set; }

    public virtual DbSet<oPort> oPort { get; set; }

    public virtual DbSet<oRequire> oRequire { get; set; }

    public virtual DbSet<oShuttle> oShuttle { get; set; }

    public virtual DbSet<pFunction> pFunction { get; set; }

    public virtual DbSet<pGroup> pGroup { get; set; }

    public virtual DbSet<pShift> pShift { get; set; }

    public virtual DbSet<pUser> pUser { get; set; }

    public virtual DbSet<ubActivation> ubActivation { get; set; }

    public virtual DbSet<ubMission> ubMission { get; set; }

    public virtual DbSet<pRoute> pRoute { get; set; }

    public virtual DbSet<pUserRoute> pUserRoute { get; set; }

    public virtual DbSet<oPortBinding> oPortBinding { get; set; }

    public virtual DbSet<oTaskTypeRoute> oTaskTypeRoute { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<oMission>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.BeginStation).HasMaxLength(20);
            entity.Property(e => e.BeginTime).HasMaxLength(20);
            entity.Property(e => e.EndStation).HasMaxLength(20);
            entity.Property(e => e.EndTime).HasMaxLength(20);
            entity.Property(e => e.OkFlag).HasMaxLength(1);
            entity.Property(e => e.RackId).HasMaxLength(20);
            entity.Property(e => e.Remark1).HasMaxLength(50);
            entity.Property(e => e.Remark2).HasMaxLength(50);
            entity.Property(e => e.Remark3).HasMaxLength(50);
            entity.Property(e => e.TaskCode).HasMaxLength(50);
            entity.Property(e => e.TaskDateTime).HasMaxLength(20);
            entity.Property(e => e.TaskSource).HasMaxLength(20);
            entity.Property(e => e.WorkOrder).HasMaxLength(100);
            entity.Property(e => e.ParentTaskDateTime).HasMaxLength(50);
        });

        modelBuilder.Entity<oNeed>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.AssignFlag).HasMaxLength(1);
            entity.Property(e => e.EndStation).HasMaxLength(20);
            entity.Property(e => e.ObjStation).HasMaxLength(20);
            entity.Property(e => e.RackId).HasMaxLength(50);
            entity.Property(e => e.TaskDateTime).HasMaxLength(20);
            entity.Property(e => e.TaskSource).HasMaxLength(20);
            entity.Property(e => e.WorkOrder).HasMaxLength(100);
        });

        modelBuilder.Entity<oPairWay>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.InStation).HasMaxLength(20);
            entity.Property(e => e.OutStation_01).HasMaxLength(20);
            entity.Property(e => e.OutStation_02).HasMaxLength(20);
            entity.Property(e => e.OutStation_03).HasMaxLength(20);
            entity.Property(e => e.OutStation_04).HasMaxLength(20);
            entity.Property(e => e.OutStation_05).HasMaxLength(20);
            entity.Property(e => e.OutStation_06).HasMaxLength(20);
            entity.Property(e => e.OutStation_07).HasMaxLength(20);
            entity.Property(e => e.OutStation_08).HasMaxLength(20);
            entity.Property(e => e.OutStation_09).HasMaxLength(20);
            entity.Property(e => e.OutStation_10).HasMaxLength(20);
            entity.Property(e => e.OutStation_11).HasMaxLength(20);
            entity.Property(e => e.OutStation_12).HasMaxLength(20);
            entity.Property(e => e.OutStation_13).HasMaxLength(20);
            entity.Property(e => e.OutStation_14).HasMaxLength(20);
            entity.Property(e => e.OutStation_15).HasMaxLength(20);
            entity.Property(e => e.OutStation_16).HasMaxLength(20);
            entity.Property(e => e.OutStation_17).HasMaxLength(20);
            entity.Property(e => e.OutStation_18).HasMaxLength(20);
            entity.Property(e => e.OutStation_19).HasMaxLength(50);
            entity.Property(e => e.OutStation_20).HasMaxLength(20);
            entity.Property(e => e.OutStation_21).HasMaxLength(20);
            entity.Property(e => e.OutStation_22).HasMaxLength(20);
            entity.Property(e => e.OutStation_23).HasMaxLength(20);
            entity.Property(e => e.OutStation_24).HasMaxLength(50);
            entity.Property(e => e.OutStation_25).HasMaxLength(20);
            entity.Property(e => e.OutStation_26).HasMaxLength(20);
            entity.Property(e => e.OutStation_27).HasMaxLength(20);
            entity.Property(e => e.OutStation_28).HasMaxLength(20);
            entity.Property(e => e.OutStation_29).HasMaxLength(20);
            entity.Property(e => e.OutStation_30).HasMaxLength(20);
        });

        modelBuilder.Entity<oPort>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.Area).HasMaxLength(10);
            entity.Property(e => e.BgnToEnd).HasMaxLength(20);
            entity.Property(e => e.Block).HasMaxLength(10);
            entity.Property(e => e.HaveFlag).HasMaxLength(1);
            entity.Property(e => e.InterfaceName).HasMaxLength(200);
            entity.Property(e => e.MachineName).HasMaxLength(20);
            entity.Property(e => e.PanelCallShuttle).HasMaxLength(1);
            entity.Property(e => e.ProductionPartNo).HasMaxLength(50);
            entity.Property(e => e.PutTime).HasMaxLength(20);
            entity.Property(e => e.RackId).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(100);
            entity.Property(e => e.StationNo).HasMaxLength(20);
            entity.Property(e => e.UseFlag).HasMaxLength(1);
            entity.Property(e => e.WorkOrder).HasMaxLength(100);
        });

        modelBuilder.Entity<oRequire>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.AssignFlag).HasMaxLength(1);
            entity.Property(e => e.BeginStation).HasMaxLength(20);
            entity.Property(e => e.EndStation).HasMaxLength(20);
            entity.Property(e => e.ObjStation).HasMaxLength(20);
            entity.Property(e => e.OkFlag).HasMaxLength(1);
            entity.Property(e => e.RackId).HasMaxLength(20);
            entity.Property(e => e.TaskDateTime).HasMaxLength(20);
            entity.Property(e => e.TaskSource).HasMaxLength(20);
            entity.Property(e => e.WorkOrder).HasMaxLength(100);
        });

        modelBuilder.Entity<oShuttle>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.BeginStation).HasMaxLength(20);
            entity.Property(e => e.Enabled).HasMaxLength(1);
            entity.Property(e => e.EndStation).HasMaxLength(20);
            entity.Property(e => e.GustomerName).HasMaxLength(100);
            entity.Property(e => e.Ip).HasMaxLength(24);
            entity.Property(e => e.LastStation).HasMaxLength(20);
            entity.Property(e => e.Port).HasMaxLength(4);
            entity.Property(e => e.PosX).HasMaxLength(8);
            entity.Property(e => e.PosY).HasMaxLength(8);
            entity.Property(e => e.RobotDir).HasMaxLength(4);
            entity.Property(e => e.ShuttleSize).HasMaxLength(1);
            entity.Property(e => e.Status).HasMaxLength(2);
        });

        modelBuilder.Entity<pFunction>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.ControlFlag).HasMaxLength(1);
            entity.Property(e => e.FunctionChineseName).HasMaxLength(50);
            entity.Property(e => e.FunctionEnglishName).HasMaxLength(50);
            entity.Property(e => e.FunctionType).HasMaxLength(50);
            entity.Property(e => e.WebIcon)
                .HasMaxLength(50)
                .IsFixedLength();
            entity.Property(e => e.WebUrl).HasMaxLength(200);
        });

        modelBuilder.Entity<pGroup>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.FunctionGroup).HasMaxLength(2000);
            entity.Property(e => e.GroupCName).HasMaxLength(50);
            entity.Property(e => e.GroupEName).HasMaxLength(50);
            entity.Property(e => e.GroupId).HasMaxLength(20);
            entity.Property(e => e.ModifiedTime).HasMaxLength(14);
        });

        modelBuilder.Entity<pShift>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.BeginDateTime).HasMaxLength(20);
            entity.Property(e => e.EffectDateTime).HasMaxLength(20);
            entity.Property(e => e.EndDateTime).HasMaxLength(20);
            entity.Property(e => e.ModifiedTime).HasMaxLength(14);
            entity.Property(e => e.ShiftCode).HasMaxLength(20);
            entity.Property(e => e.ShiftName).HasMaxLength(10);
        });

        modelBuilder.Entity<pUser>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.GroupId).HasMaxLength(20);
            entity.Property(e => e.Mail).HasMaxLength(50);
            entity.Property(e => e.ModifiedTime).HasMaxLength(14);
            entity.Property(e => e.Password).HasMaxLength(50);
            entity.Property(e => e.Tel).HasMaxLength(20);
            entity.Property(e => e.UserId).HasMaxLength(50);
            entity.Property(e => e.UserName).HasMaxLength(50);
        });

        modelBuilder.Entity<ubActivation>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.BeginStation).HasMaxLength(20);
            entity.Property(e => e.BeginTime).HasMaxLength(20);
            entity.Property(e => e.EndStation).HasMaxLength(20);
            entity.Property(e => e.EndTime).HasMaxLength(20);
            entity.Property(e => e.ReceivingTime).HasMaxLength(20);
            entity.Property(e => e.ShuttleId).HasMaxLength(20);
            entity.Property(e => e.ShuttleStation).HasMaxLength(20);
            entity.Property(e => e.TaskDateTime).HasMaxLength(20);
            entity.Property(e => e.TaskType).HasMaxLength(10);
        });

        modelBuilder.Entity<ubMission>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.BeginStation).HasMaxLength(20);
            entity.Property(e => e.BeginTime).HasMaxLength(20);
            entity.Property(e => e.EndStation).HasMaxLength(20);
            entity.Property(e => e.EndTime).HasMaxLength(20);
            entity.Property(e => e.OkFlag).HasMaxLength(1);
            entity.Property(e => e.RackId).HasMaxLength(20);
            entity.Property(e => e.Remark1).HasMaxLength(50);
            entity.Property(e => e.Remark2).HasMaxLength(50);
            entity.Property(e => e.Remark3).HasMaxLength(50);
            entity.Property(e => e.TaskCode).HasMaxLength(50);
            entity.Property(e => e.TaskDateTime).HasMaxLength(20);
            entity.Property(e => e.TaskSource).HasMaxLength(20);
            entity.Property(e => e.WorkOrder).HasMaxLength(100);
        });

        modelBuilder.Entity<pRoute>(entity =>
        {
            entity.HasKey(e => e.RouteId);

            entity.Property(e => e.RouteId).HasMaxLength(30);
            entity.Property(e => e.RouteName).HasMaxLength(50);
            entity.Property(e => e.SourceFloor).HasMaxLength(10);
            entity.Property(e => e.TargetFloor).HasMaxLength(10);
            entity.Property(e => e.RouteType).HasMaxLength(20);
            entity.Property(e => e.SourceAreas).HasMaxLength(100);
            entity.Property(e => e.TargetAreas).HasMaxLength(100);
            entity.Property(e => e.ControlFlag).HasMaxLength(1);
            entity.Property(e => e.DispatchMode).HasMaxLength(20);
        });

        modelBuilder.Entity<pUserRoute>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RouteId });

            entity.Property(e => e.UserId).HasMaxLength(20);
            entity.Property(e => e.RouteId).HasMaxLength(30);
        });

        modelBuilder.Entity<oPortBinding>(entity =>
        {
            entity.HasKey(e => e.LoadingPort);

            entity.Property(e => e.LoadingPort).HasMaxLength(20);
            entity.Property(e => e.UnloadingPort).HasMaxLength(20);
            entity.Property(e => e.FallbackAreas).HasMaxLength(100);
            entity.Property(e => e.UseFlag).HasMaxLength(1);
        });

        modelBuilder.Entity<oTaskTypeRoute>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MoveType).HasMaxLength(20);
            entity.Property(e => e.FromFloor).HasMaxLength(10);
            entity.Property(e => e.ToFloor).HasMaxLength(10);
            entity.Property(e => e.TaskType).HasMaxLength(50);
            entity.Property(e => e.UseFlag).HasMaxLength(1);
            entity.Property(e => e.Remark).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
