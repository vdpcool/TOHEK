namespace TOHE.Roles.Impostor
{
    public static class Camouflager
    {
        private static readonly int Id = 903534;

        private static OptionItem CamouflageCooldown;
        private static OptionItem CamouflageDuration;
        public static OptionItem CanVent;
        public static bool IsActive;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Camouflager);
            CamouflageCooldown = FloatOptionItem.Create(Id + 2, "CamouflageCooldown", new(1f, 999f, 1f), 25f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
                .SetValueFormat(OptionFormat.Seconds);
            CamouflageDuration = FloatOptionItem.Create(Id + 4, "CamouflageDuration", new(1f, 999f, 1f), 10f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(Id + 5, "CanVent", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager]);
        }
        public static void CanUseVent(PlayerControl player)
        {
            bool Camouflager_canUse = CanVent.GetBool();
            DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.ToggleVisible(Camouflager_canUse && !player.Data.IsDead);
            player.Data.Role.CanVent = Camouflager_canUse;
        }
        public static void ApplyGameOptions()
        {
            AURoleOptions.ShapeshifterCooldown = CamouflageCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = CamouflageDuration.GetFloat();
        }
        public static void Init()
        {
            IsActive = false;
        }
        public static void OnShapeshift()
        {
            IsActive = true;
            Camouflage.CheckCamouflage();
        }
        public static void OnReportDeadBody()
        {
            IsActive = false;
            Camouflage.CheckCamouflage();
        }
        public static void isDead(PlayerControl target)
        {
            if (!target.Data.IsDead || GameStates.IsMeeting) return;

            if(target.Is(CustomRoles.Camouflager) && target.Data.IsDead)
            {
                IsActive = false;
                Camouflage.CheckCamouflage();
                Utils.NotifyRoles();
            }
        }
    }
}
