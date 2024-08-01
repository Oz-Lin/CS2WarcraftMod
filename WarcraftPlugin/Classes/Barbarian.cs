﻿using System;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using CounterStrikeSharp.API.Modules.Memory;
using WarcraftPlugin.Effects;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using System.Drawing;

namespace WarcraftPlugin.Races
{
    public class Barbarian : WarcraftClass
    {
        public override string InternalName => "barbarian";
        public override string DisplayName => "Barbarian";
        public override DefaultClassModel DefaultModel => new()
        {
            TModel = "characters/models/tm_phoenix_heavy/tm_phoenix_heavy.vmdl",
            CTModel = "characters/models/ctm_heavy/ctm_heavy.vmdl"
        };
        public override Color DefaultColor => Color.Khaki;

        private readonly int _battleHardenedHealthMultiplier = 20;
        private readonly float _bloodlustLength = 10;

        public override void Register()
        {
            AddAbility(new WarcraftAbility("carnage", "Carnage",
                i => $"{ChatColors.BlueGrey}Increase damage dealt with {ChatColors.Green}shotguns."));

            AddAbility(new WarcraftAbility("battle_hardened", "Battle-Hardened",
                i => $"{ChatColors.BlueGrey}Increase your health by {ChatColors.Blue}20/40/60/80/100."));

            AddAbility(new WarcraftAbility("throwing_axe", "Throwing Axe",
                i => $"{ChatColors.BlueGrey}Chance to hurl an exploding {ChatColors.Yellow}throwing axe{ChatColors.BlueGrey} when firing."));

            AddAbility(new WarcraftCooldownAbility("bloodlust", "Bloodlust",
                i => $"{ChatColors.BlueGrey}Grants {ChatColors.Red}infinite ammo, movement speed & health regeneration.",
                50f));

            HookEvent<EventPlayerHurt>("player_hurt_other", PlayerHurtOther);
            HookEvent<EventPlayerSpawn>("player_spawn", PlayerSpawn);
            HookEvent<EventWeaponFire>("player_shoot", PlayerShoot);

            HookAbility(3, Ultimate);
        }

        private void PlayerShoot(EventWeaponFire @event)
        {
            if (WarcraftPlayer.GetAbilityLevel(2) > 0)
            {
                double rolledValue = Random.Shared.NextDouble();
                float chanceToAxe = WarcraftPlayer.GetAbilityLevel(2) * 0.05f;

                if (rolledValue <= chanceToAxe)
                {
                    ThrowAxe();
                }
            }
        }

        private void ThrowAxe()
        {
            var throwingAxe = Utilities.CreateEntityByName<CHEGrenadeProjectile>("hegrenade_projectile");

            Vector velocity = Player.CalculateVelocityAwayFromPlayer(1800);

            var rotation = new QAngle(0, Player.PlayerPawn.Value.EyeAngles.Y + 90, 0);

            throwingAxe.Teleport(Player.CalculatePositionInFront(60, 60), rotation, velocity);
            throwingAxe.DispatchSpawn();
            throwingAxe.SetModel("models/weapons/v_axe.vmdl");
            Schema.SetSchemaValue(throwingAxe.Handle, "CBaseGrenade", "m_hThrower", Player.PlayerPawn.Raw); //Fixes killfeed

            throwingAxe.AcceptInput("InitializeSpawnFromWorld");
            throwingAxe.Damage = 40;
            throwingAxe.DmgRadius = 80;
            throwingAxe.DetonateTime = float.MaxValue;
            DispatchEffect(new ThrowingAxeEffect(Player, throwingAxe, 2));
        }

        private void PlayerSpawn(EventPlayerSpawn @event)
        {
            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                Player.SetHp(100 + WarcraftPlayer.GetAbilityLevel(1) * _battleHardenedHealthMultiplier);
                Player.PlayerPawn.Value.MaxHealth = Player.PlayerPawn.Value.Health;
            }
        }

        private void SetBloodlust()
        {
            Player.PlayerPawn.Value.HealthShotBoostExpirationTime = Server.CurrentTime + _bloodlustLength;
            Utilities.SetStateChanged(Player.PlayerPawn.Value, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
            DispatchEffect(new BloodlustEffect(Player, _bloodlustLength));
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            SetBloodlust();
            StartCooldown(3);
        }

        private void PlayerHurtOther(EventPlayerHurt @event)
        {
            if (!@event.Userid.IsValid || !@event.Userid.PawnIsAlive || @event.Userid.UserId == Player.UserId) return;

            var carnageLevel = WarcraftPlayer.GetAbilityLevel(0);

            if (carnageLevel > 0 && WeaponTypes.Shotguns.Contains(@event.Weapon))
            {
                var victim = @event.Userid;
                victim.TakeDamage(carnageLevel * 5f, Player);
                Utility.SpawnParticle(victim.PlayerPawn.Value.AbsOrigin.With(z: victim.PlayerPawn.Value.AbsOrigin.Z + 60), "particles/blood_impact/blood_impact_basic.vpcf");
                Player.PlaySound("sounds/physics/body/body_medium_break3.vsnd");
            }
        }
    }

    public class ThrowingAxeEffect : WarcraftEffect
    {
        private readonly CHEGrenadeProjectile _axe;

        public ThrowingAxeEffect(CCSPlayerController owner, CHEGrenadeProjectile axe, float duration) : base(owner, duration) { _axe = axe; }

        public override void OnStart()
        {
            Owner.PlaySound("sounds/player/effort_m_09.vsnd");
        }

        public override void OnTick()
        {
            var hasHitPlayer = _axe?.HasEverHitPlayer ?? false;
            if (hasHitPlayer)
            {
                try
                {
                    _axe.DetonateTime = 0;
                }
                catch { }
            }
        }

        public override void OnFinish()
        {
            _axe.DetonateTime = 0;
            WarcraftPlugin.Instance.AddTimer(1, () => _axe?.Remove());
        }
    }

    public class BloodlustEffect : WarcraftEffect
    {
        public BloodlustEffect(CCSPlayerController owner, float duration) : base(owner, duration) { }

        private const float _maxSize = 1.1f;

        public override void OnStart()
        {
            Owner.PlayerPawn.Value.VelocityModifier = 1.3f;
            Owner.PlayerPawn.Value.SetColor(Color.IndianRed);
            Owner.PlaySound("sounds/vo/agents/balkan/t_death03.vsnd");
        }

        public override void OnTick()
        {
            //Refill ammo
            Owner.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value.Clip1 = Owner.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value.GetVData<CBasePlayerWeaponVData>().MaxClip1;

            //Regenerate healthw
            if (Owner.PlayerPawn.Value.Health < Owner.PlayerPawn.Value.MaxHealth)
            {
                Owner.SetHp(Owner.PlayerPawn.Value.Health + 1); 
            }

            //Rage growth spurt
            if (Owner.PlayerPawn.Value.CBodyComponent.SceneNode.GetSkeletonInstance().Scale < _maxSize)
            {
                Owner.PlayerPawn.Value.CBodyComponent.SceneNode.GetSkeletonInstance().Scale += 0.01f;
                Utilities.SetStateChanged(Owner.PlayerPawn.Value, "CBaseEntity", "m_CBodyComponent");
            }
        }

        public override void OnFinish()
        {
            Owner.PlayerPawn.Value.SetColor(Color.White);
            Owner.PlayerPawn.Value.VelocityModifier = 1f;
            Owner.PlayerPawn.Value.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = 1f;
            Utilities.SetStateChanged(Owner.PlayerPawn.Value, "CBaseEntity", "m_CBodyComponent");
        }
    }
}