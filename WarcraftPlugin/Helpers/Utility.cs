﻿using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using System.Drawing;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using CounterStrikeSharp.API.Modules.Memory;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Modules.Timers;
using System.Linq;
using CounterStrikeSharp.API.Modules.Entities;

namespace WarcraftPlugin.Helpers
{
    public static class Utility
    {
        static public void DrawLaserBetween(Vector startPos, Vector endPos, Color? color = null, float duration = 1, float width = 2)
        {
            CBeam beam = Utilities.CreateEntityByName<CBeam>("beam");
            if (beam == null) return;

            beam.Render = color ?? Color.Red;
            beam.Width = width;

            beam.Teleport(startPos, new QAngle(), new Vector());
            beam.EndPos.X = endPos.X;
            beam.EndPos.Y = endPos.Y;
            beam.EndPos.Z = endPos.Z;
            beam.DispatchSpawn();
            WarcraftPlugin.Instance.AddTimer(duration, beam.Remove);
        }

        public static Vector ToCenterOrigin(this CCSPlayerController player)
        {
            var pawnOrigin = player.PlayerPawn.Value.AbsOrigin;
            return new Vector(pawnOrigin.X, pawnOrigin.Y, pawnOrigin.Z + 44);
        }

        public static CParticleSystem SpawnParticle(Vector pos, string effectName, float duration = 5)
        {
            CParticleSystem particle = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
            if (!particle.IsValid) return null;
            particle.EffectName = effectName;
            particle?.Teleport(pos, new QAngle(), new Vector());
            particle.StartActive = true;
            particle?.DispatchSpawn();

            Timer timer = null;
            timer = WarcraftPlugin.Instance.AddTimer(duration, () =>
            {
                if(particle.IsValid) particle?.Remove();
                timer?.Kill();
            }, TimerFlags.REPEAT);

            return particle;
        }

        public static void SpawnExplosion(Vector pos, float damage, float radius, CCSPlayerController attacker = null)
        {
            var heProjectile = Utilities.CreateEntityByName<CHEGrenadeProjectile>("hegrenade_projectile");
            if (heProjectile == null || !heProjectile.IsValid) return;
            pos.Z += 10;
            heProjectile.TicksAtZeroVelocity = 100;
            heProjectile.Damage = damage;
            heProjectile.DmgRadius = radius;
            heProjectile.Teleport(pos, new QAngle(), new Vector(0, 0, -10));
            heProjectile.DispatchSpawn();
            heProjectile.AcceptInput("InitializeSpawnFromWorld", attacker.PlayerPawn.Value, attacker.PlayerPawn.Value, "");
            Schema.SetSchemaValue(heProjectile.Handle, "CBaseGrenade", "m_hThrower", attacker.PlayerPawn.Raw); //Fixes killfeed
            heProjectile.DetonateTime = 0;
        }

        public static CSmokeGrenadeProjectile SpawnSmoke(Vector pos, CCSPlayerPawn attacker, Color color)
        {
            //var smokeProjectile = Utilities.CreateEntityByName<CSmokeGrenadeProjectile>("smokegrenade_projectile");
            var smokeProjectile = CSmokeGrenadeProjectile_CreateFunc.Invoke(
                        pos.Handle,
                        new Vector(0,0,0).Handle,
                        new Vector(0, 0, 0).Handle,
                        new Vector(0, 0, 0).Handle,
                        nint.Zero,
                        45,
                        attacker.TeamNum);
            //Smoke color
            smokeProjectile.SmokeColor.X = color.R;
            smokeProjectile.SmokeColor.Y = color.G;
            smokeProjectile.SmokeColor.Z = color.B;

            return smokeProjectile;

            /*var smokeEffect = Utilities.CreateEntityByName<CParticleSystem>("particle_smokegrenade");
            smokeEffect.Teleport(pos, new QAngle(), new Vector(0, 0, 0));
            smokeEffect.DispatchSpawn();*/
        }

        public static MemoryFunctionVoid<CBaseEntity, CBaseEntity, CUtlStringToken, matrix3x4_t> CBaseEntity_SetParent = new(
        Environment.OSVersion.Platform == PlatformID.Unix
            ? @"\x48\x85\xF6\x74\x2A\x48\x8B\x47\x10\xF6\x40\x31\x02\x75\x2A\x48\x8B\x46\x10\xF6\x40\x31\x02\x75\x2A\xB8\x2A\x2A\x2A\x2A"
            : @"\x4D\x8B\xD9\x48\x85\xD2\x74\x2A"
        );

        public static void SetParent(this CBaseEntity childEntity, CBaseEntity parentEntity)
        {
            if (!childEntity.IsValid || !parentEntity.IsValid) return;

            var origin = new Vector(childEntity.AbsOrigin!.X, childEntity.AbsOrigin!.Y, childEntity.AbsOrigin!.Z);
            CBaseEntity_SetParent.Invoke(childEntity, parentEntity, null, null);
            // If not teleported, the childrenEntity will not follow the parentEntity correctly.
            childEntity.Teleport(origin, new QAngle(IntPtr.Zero), new Vector(IntPtr.Zero));
        }

        public static MemoryFunctionWithReturn<nint, nint, nint, nint, nint, nint, int, CSmokeGrenadeProjectile> CSmokeGrenadeProjectile_CreateFunc = new(
                Environment.OSVersion.Platform == PlatformID.Unix
                    ? @"\x55\x4C\x89\xC1\x48\x89\xE5\x41\x57\x41\x56\x49\x89\xD6"
                    : @"\x48\x89\x5C\x24\x2A\x48\x89\x6C\x24\x2A\x48\x89\x74\x24\x2A\x57\x41\x56\x41\x57\x48\x83\xEC\x50\x4C\x8B\xB4\x24"
        );

        public static void DoDamage(this CCSPlayerController player, int damage) //TODO: Merge with TakeDamage
        {
            var victimHealth = player.PlayerPawn.Value.Health - damage;
            player.SetHp(victimHealth);
        }

        public static void SetHp(this CCSPlayerController controller, int health = 100)
        {
            var pawn = controller.PlayerPawn.Value;
            if (!controller.PawnIsAlive || pawn == null) return;

            pawn.Health = health;

            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        }

        public static void SetArmor(this CCSPlayerController controller, int armor = 100)
        {
            if (armor < 0 || !controller.PawnIsAlive || controller.PlayerPawn.Value == null) return;

            controller.PlayerPawn.Value.ArmorValue = armor;

            Utilities.SetStateChanged(controller.PlayerPawn.Value, "CCSPlayerPawn", "m_ArmorValue");
        }

        public static void SetColor(this CBaseModelEntity entity, Color color)
        {
            if (entity == null) return;

            entity.Render = color;
            Utilities.SetStateChanged(entity, "CBaseModelEntity", "m_clrRender");
        }

        public static void PlaySound(this CCSPlayerController player, string soundPath)
        {
            if (player == null || !player.IsValid) return;

            player.ExecuteClientCommand($"play {soundPath}");
        }

        public static Vector CalculateVelocityAwayFromPlayer(this CCSPlayerController player, int speed)
        {
            var pawn = player.PlayerPawn.Value;
            float yawAngleRadians = (float)(pawn.EyeAngles.Y * Math.PI / 180.0);
            float yawCos = (float)(Math.Cos(yawAngleRadians) * speed);
            float yawSin = (float)(Math.Sin(yawAngleRadians) * speed);

            float pitchAngleRadians = (float)(pawn.EyeAngles.X * Math.PI / 180.0);
            float pitchSin = (float)(Math.Sin(pitchAngleRadians) * -speed);

            var velocity = new Vector(yawCos, yawSin, pitchSin);
            return velocity;
        }

        public static Vector CalculatePositionInFront(this CCSPlayerController player, float offSetXY, float offSetZ = 0)
        {
            var pawn = player.PlayerPawn.Value;
            // Extract yaw angle from player's rotation QAngle
            float yawAngle = pawn.EyeAngles.Y;

            // Convert yaw angle from degrees to radians
            float yawAngleRadians = (float)(yawAngle * Math.PI / 180.0);

            // Calculate offsets in x and y directions
            float offsetX = offSetXY * (float)Math.Cos(yawAngleRadians);
            float offsetY = offSetXY * (float)Math.Sin(yawAngleRadians);

            // Calculate position in front of the player
            var positionInFront = new Vector
            {
                X = pawn.AbsOrigin.X + offsetX,
                Y = pawn.AbsOrigin.Y + offsetY,
                Z = pawn.AbsOrigin.Z + offSetZ
            };

            return positionInFront;
        }

        public static Vector CalculateVelocity(Vector positionA, Vector positionB, float timeDuration)
        {
            // Step 1: Determine direction from A to B
            Vector directionVector = positionB - positionA;

            // Step 2: Calculate distance between A and B
            float distance = directionVector.Length();

            // Step 3: Choose a desired time duration for the movement
            // Ensure that timeDuration is not zero to avoid division by zero
            if (timeDuration == 0)
            {
                timeDuration = 1;
            }

            // Step 4: Calculate velocity magnitude based on distance and time
            float velocityMagnitude = distance / timeDuration;

            // Step 5: Normalize direction vector
            if (distance != 0)
            {
                directionVector /= distance;
            }

            // Step 6: Scale direction vector by velocity magnitude to get velocity vector
            Vector velocityVector = directionVector * velocityMagnitude;

            return velocityVector;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct CAttackerInfo
        {
            public CAttackerInfo(CEntityInstance attacker)
            {
                NeedInit = false;
                IsWorld = true;
                Attacker = attacker.EntityHandle.Raw;
                if (attacker.DesignerName != "cs_player_controller") return;

                var controller = attacker.As<CCSPlayerController>();
                IsWorld = false;
                IsPawn = true;
                AttackerUserId = (ushort)(controller.UserId ?? 0xFFFF);
                TeamNum = controller.TeamNum;
                TeamChecked = controller.TeamNum;
            }

            [FieldOffset(0x0)] public bool NeedInit = true;
            [FieldOffset(0x1)] public bool IsPawn = false;
            [FieldOffset(0x2)] public bool IsWorld = false;

            [FieldOffset(0x4)]
            public UInt32 Attacker;

            [FieldOffset(0x8)]
            public ushort AttackerUserId;

            [FieldOffset(0x0C)] public int TeamChecked = -1;
            [FieldOffset(0x10)] public int TeamNum = -1;
        }

        public static void TakeDamage(this CCSPlayerController player, float damage, CCSPlayerController attacker, CCSPlayerController inflictor = null)
        {
            var size = Schema.GetClassSize("CTakeDamageInfo");
            var ptr = Marshal.AllocHGlobal(size);

            for (var i = 0; i < size; i++)
                Marshal.WriteByte(ptr, i, 0);

            var damageInfo = new CTakeDamageInfo(ptr);
            var attackerInfo = new CAttackerInfo(player);

            Marshal.StructureToPtr(attackerInfo, new IntPtr(ptr.ToInt64() + 0x80), false);

            Schema.SetSchemaValue(damageInfo.Handle, "CTakeDamageInfo", "m_hInflictor", inflictor?.Pawn?.Raw ?? attacker.Pawn.Raw);
            Schema.SetSchemaValue(damageInfo.Handle, "CTakeDamageInfo", "m_hAttacker", attacker.Pawn.Raw);

            damageInfo.Damage = damage;

            VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Invoke(player.Pawn.Value, damageInfo);
            Marshal.FreeHGlobal(ptr);
        }

        public static void DropWeaponByDesignerName(this CCSPlayerController player, string weaponName)
        {
            var matchedWeapon = player.PlayerPawn.Value.WeaponServices.MyWeapons
                .Where(x => x.Value.DesignerName == weaponName).FirstOrDefault();

            if (matchedWeapon != null && matchedWeapon.IsValid)
            {
                player.PlayerPawn.Value.WeaponServices.ActiveWeapon.Raw = matchedWeapon.Raw;
                player.DropActiveWeapon();
            }
        }
    }
}
