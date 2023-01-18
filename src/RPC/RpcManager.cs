using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hazel;
using InnerNet;
using VentLib.Extensions;
using VentLib.Logging;
using VentLib.RPC.Attributes;
using VentLib.Utilities;

namespace VentLib.RPC;

public static class RpcManager
{
    internal static void Register(ModRPC rpc)
    {
        if (Vents.BuiltinRPCs.Contains(rpc.CallId) && rpc.Attribute is not VentRPCAttribute)
            throw new ArgumentException($"RPC {rpc.CallId} shares an ID with a Builtin-VentLib RPC. Please choose a different ID. (Builtin-IDs: {Vents.BuiltinRPCs.StrJoin()})");

        if (!Vents.RpcBindings.ContainsKey(rpc.CallId))
            Vents.RpcBindings.Add(rpc.CallId, new List<ModRPC>());

        Vents.RpcBindings[rpc.CallId].Add(rpc);
    }

    internal static bool HandleRpc(byte callId, MessageReader reader)
    {
        if (callId != 203) return true;
        uint customId = reader.ReadUInt32();
        RpcActors actor = (RpcActors)reader.ReadByte();
        if (!CanReceive(actor)) return true;
        uint senderId = reader.ReadPackedUInt32();
        PlayerControl? player = null;
        if (AmongUsClient.Instance.allObjectsFast.TryGet(senderId, out InnerNetObject? netObject))
        {
            player = netObject!.TryCast<PlayerControl>();
            if (player != null) Vents.LastSenders[customId] = player;
        }

        if (player != null && player.PlayerId == PlayerControl.LocalPlayer.PlayerId) return true;
        string sender = "Client: " + (player == null ? "?" : player.GetClientId());
        string receiverType = AmongUsClient.Instance.AmHost ? "Host" : "NonHost";
        VentLogger.Info($"Custom RPC Received ({customId}) from \"{sender}\" as {receiverType}", "VentLib");
        if (!Vents.RpcBindings.TryGetValue(customId, out List<ModRPC>? rpcs))
        {
            VentLogger.Warn($"Received Unknown RPC: {customId}", "VentLib");
            reader.Recycle();
            return false;
        }

        object[]? args = null;
        foreach (ModRPC modRPC in rpcs)
        {
            // Cases in which the client is not the correct listener
            if (!CanReceive(actor, modRPC.Receivers)) continue;
            if (!Vents.CallingAssemblyFlag(modRPC.Assembly).HasFlag(VentControlFlag.AllowedReceiver)) continue;
            args ??= ParameterHelper.Cast(modRPC.Parameters, reader);
            modRPC.InvokeTrampoline(args);
        }

        return true;
    }

    private static bool CanReceive(RpcActors actor, RpcActors localActor = RpcActors.Everyone)
    {
        return actor switch
        {
            RpcActors.None => false,
            RpcActors.Host => AmongUsClient.Instance.AmHost && localActor is RpcActors.Host or RpcActors.NonHosts,
            RpcActors.NonHosts => !AmongUsClient.Instance.AmHost && localActor is RpcActors.Everyone or RpcActors.NonHosts,
            RpcActors.LastSender => localActor is RpcActors.Everyone or RpcActors.LastSender,
            RpcActors.Everyone => true,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

