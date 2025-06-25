using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Runtime.Intrinsics.Arm;
using Dalamud.Game.ClientState.Objects;
using OCRez.Windows;

namespace OCRez.Managers;

public class DeadPlayersEventArgs : EventArgs
{
    public List<IPlayerCharacter> Players { get; set; }
}

public class RezManager : IDisposable
{

    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly ITargetManager targetManager;
    private ushort targetTerritory = 0;
    private List<uint> marksFoundInArea = new();

    private TimeSpan _lastUpdate = new(0);
    private TimeSpan _execDelay = new(0, 0, 1);

    public event EventHandler<DeadPlayersEventArgs> OnNewDeadPlayers;

    public bool Available { get; private set; } = false;
    public bool CanAct
    {
        get
        {
            if (clientState.LocalPlayer == null)
                return false;
            if (condition[ConditionFlag.BetweenAreas] ||
                condition[ConditionFlag.BetweenAreas51] ||
                condition[ConditionFlag.BeingMoved] ||
                condition[ConditionFlag.Casting] ||
                condition[ConditionFlag.Casting87] ||
                condition[ConditionFlag.Jumping] ||
                condition[ConditionFlag.Jumping61] ||
                condition[ConditionFlag.LoggingOut] ||
                condition[ConditionFlag.Occupied] ||
               condition[ConditionFlag.Unconscious] ||
                clientState.LocalPlayer.CurrentHp < 1)
                return false;
            return true;
        }
    }

    public RezManager(
        IDalamudPluginInterface pluginInterface,
        IChatGui chat,
        IPluginLog log,
        IObjectTable objectTable,
        IClientState clientState,
        ICondition condition,
        ITargetManager targetManager,
        IFramework framework
    )
    {
        this.chat = chat;
        this.log = log;
        this.objectTable = objectTable;
        this.condition = condition;
        this.targetManager = targetManager;
        this.clientState = clientState;
        this.framework = framework;

        log.Debug("------ Wow we are instanced!");
        this.framework.Update += Tick;

    }
    private void Tick(IFramework framework)
    {
        _lastUpdate += framework.UpdateDelta;
        if (_lastUpdate > _execDelay)
        {
            _lastUpdate = new(0);
            DoUpdate();            
        }
    }

    private unsafe void CastRevive() {
        log.Debug($"Unsafe!");
        var am = ActionManager.Instance();
        log.Debug($"Do it!");
        var cds = am->Cooldowns;
        foreach (var c in cds)
        {
            if (c.ActionId == 0)
                continue;
            log.Debug($"Cooldown {c.ActionId} on cd {c.IsActive} total: {c.Total}");
        }
        //am->UseAction(ActionType.Action, 7523);
        //am->UseAction(ActionType.GeneralAction,33);
    }

    private void CheckObjectTable()
    {
    List<IPlayerCharacter> deadPlayers = new();
        foreach (var obj in objectTable)
        {
            if (obj is not IPlayerCharacter) continue;
            var plr = obj as IPlayerCharacter;
            // Only dead players
            if (!plr.IsDead)
                continue;
            bool hasRez = false;
            // Only without rez buff
            foreach (var status in plr.StatusList) {
                if (status.StatusId == 148){
                    hasRez = true;
                    break;
                }
            }
            if (hasRez)
                continue;
            if (plr.YalmDistanceX > 30 || plr.YalmDistanceZ > 30)
                continue;
            log.Debug($"Found valid plr {plr.Name} range {plr.YalmDistanceX} / {plr.YalmDistanceZ}");
            targetManager.FocusTarget = plr;
            deadPlayers.Add(plr);
        }
        if(deadPlayers.Count < 1) {
            targetManager.FocusTarget = null;
        }
        /*
        var args = new DeadPlayersEventArgs();
        args.Players = deadPlayers;
        log.Debug($"{deadPlayers.Count} dead players.");
        OnNewDeadPlayers.Invoke(this, args);
        */
    }

    private unsafe void DoUpdate()
    {
        CheckObjectTable();
    }

    public void Dispose()
    {
        log.Debug("------ Wow we are disposed!");
        this.framework.Update -= Tick;
    }

    private void OnDisable()
    {
    }
}
